'use strict';
// Prueba de punta a punta del panel (Linux o macOS): arranca una sesión de bash dentro del anfitrión, que levanta el
// servidor; entra como lo haría el navegador; mira la pantalla, escribe en la sesión, manda avisos como el mod,
// comprueba las protecciones y cierra la sesión desde el panel.
//
//   npm run prueba

const fs = require('fs');
const os = require('os');
const path = require('path');
const http = require('http');
const assert = require('assert/strict');
const { spawn } = require('child_process');
const WebSocket = require('ws');

const DATOS = fs.mkdtempSync(path.join(os.tmpdir(), 'panel-prueba-'));
const PUERTO = 20000 + Math.floor(Math.random() * 20000);
const env = { ...process.env, PANEL_DATOS: DATOS, PANEL_PUERTO: String(PUERTO), PANEL_PETICIONES: path.join(DATOS, 'peticiones') };
const BASE = `http://127.0.0.1:${PUERTO}`;
const raiz = path.join(__dirname, '..');

const esperar = ms => new Promise(r => setTimeout(r, ms));

function peticion(metodo, ruta, { cabeceras = {}, cuerpo } = {}) {
  return new Promise((resolve, reject) => {
    const req = http.request(`${BASE}${ruta}`, { method: metodo, headers: cabeceras }, res => {
      let t = '';
      res.on('data', c => (t += c));
      res.on('end', () => resolve({ estado: res.statusCode, cabeceras: res.headers, texto: t }));
    });
    req.on('error', reject);
    if (cuerpo) req.write(cuerpo);
    req.end();
  });
}

async function hasta(cond, ms, que) {
  const fin = Date.now() + ms;
  while (Date.now() < fin) {
    const v = await cond();
    if (v) return v;
    await esperar(100);
  }
  throw new Error(`No ha pasado a tiempo: ${que}`);
}

(async () => {
  const carpeta = fs.mkdtempSync(path.join(os.tmpdir(), '1234567 - Prueba '));
  const anfitrion = spawn(
    process.execPath,
    [path.join(raiz, 'anfitrion.js'), '--titulo', 'Claude Code - 1234567 - Prueba', '--carpeta', carpeta, '--', 'bash', '--norc', '--noprofile', '-i'],
    { env, stdio: 'ignore' }
  );
  let salioAnfitrion = null;
  anfitrion.on('exit', c => (salioAnfitrion = c ?? 0));

  try {
    // 1. El anfitrión arranca el servidor solo
    await hasta(async () => (await peticion('GET', '/api/ping').catch(() => null))?.estado === 200, 10000, 'servidor en marcha');
    console.log('ok  el anfitrión arranca el servidor');

    // 2. Entrada con un código de un solo uso
    const secretos = JSON.parse(fs.readFileSync(path.join(DATOS, 'secretos.json'), 'utf8'));
    const host = { Host: `127.0.0.1:${PUERTO}` };
    assert.equal((await peticion('POST', '/api/codigo', { cabeceras: host })).estado, 401);
    const { codigo } = JSON.parse((await peticion('POST', '/api/codigo', { cabeceras: { ...host, Authorization: `Bearer ${secretos.clave}` } })).texto);
    const entrada = await peticion('GET', `/entrar?codigo=${codigo}`, { cabeceras: host });
    assert.equal(entrada.estado, 302);
    const cookie = String(entrada.cabeceras['set-cookie']).split(';')[0];
    assert.equal((await peticion('GET', `/entrar?codigo=${codigo}`, { cabeceras: host })).estado, 403, 'el código vale una sola vez');
    assert.equal((await peticion('GET', '/', { cabeceras: host })).estado, 403, 'sin cookie no hay panel');
    assert.equal((await peticion('GET', '/', { cabeceras: { ...host, Cookie: cookie } })).estado, 200);
    assert.equal((await peticion('GET', '/api/ping', { cabeceras: { Host: `ejemplo.com:${PUERTO}` } })).estado, 421, 'Host ajeno');
    console.log('ok  entrada con código, cookie y comprobación de Host');

    // 3. Un navegador de otro origen no puede conectarse
    const ajeno = new WebSocket(`ws://127.0.0.1:${PUERTO}/ws/panel`, { headers: { Cookie: cookie, Origin: 'http://ejemplo.com' } });
    await new Promise(resolve => {
      ajeno.on('error', resolve);
      ajeno.on('open', () => assert.fail('aceptó un origen ajeno'));
    });
    console.log('ok  rechaza otros orígenes');

    // 4. Panel: lista, pantalla y escribir en la sesión
    const ws = new WebSocket(`ws://127.0.0.1:${PUERTO}/ws/panel`, { headers: { Cookie: cookie, Origin: BASE } });
    let lista = [];
    let pantalla = '';
    ws.on('message', m => {
      const msg = JSON.parse(m);
      if (msg.t === 'lista') lista = msg.sesiones;
      if (msg.t === 'snap') pantalla = msg.d;
      if (msg.t === 'out') pantalla += msg.d;
    });
    await new Promise(r => ws.on('open', r));
    const sesion = await hasta(() => lista.find(s => s.titulo.includes('1234567')), 10000, 'la sesión aparece en la lista');
    ws.send(JSON.stringify({ t: 'sub', id: sesion.id }));
    await hasta(() => pantalla.length > 0, 5000, 'foto de la pantalla');
    ws.send(JSON.stringify({ t: 'in', id: sesion.id, d: 'echo PRUEBA_$((40+2)); echo "CLAVE=$CLAUDE_PANEL_CLAVE"\r' }));
    await hasta(() => pantalla.includes('PRUEBA_42'), 5000, 'la sesión recibe lo que se escribe en el panel');
    console.log('ok  pantalla en directo y escritura desde el panel');

    // 5. Una segunda vista recibe la pantalla entera
    const ws2 = new WebSocket(`ws://127.0.0.1:${PUERTO}/ws/panel`, { headers: { Cookie: cookie, Origin: BASE } });
    let foto2 = null;
    ws2.on('message', m => {
      const msg = JSON.parse(m);
      if (msg.t === 'snap') foto2 = msg.d;
    });
    await new Promise(r => ws2.on('open', r));
    ws2.send(JSON.stringify({ t: 'sub', id: sesion.id }));
    await hasta(() => foto2 && foto2.includes('PRUEBA_42'), 5000, 'foto completa para otra vista');
    ws2.close();
    console.log('ok  foto completa al abrir la sesión en otra vista');

    // 6. Avisos del mod con la clave de la sesión
    const claveMod = await hasta(() => (/CLAVE=([A-Za-z0-9_-]{20,})/.exec(pantalla) || [])[1], 5000, 'clave del mod en el entorno');
    const avisar = (clave, eventos) =>
      peticion('POST', `/api/sesiones/${sesion.id}/eventos`, {
        cabeceras: { ...host, Authorization: `Bearer ${clave}`, 'Content-Type': 'application/json' },
        cuerpo: JSON.stringify({ eventos }),
      });
    assert.equal((await avisar('clave-falsa-123', [{ tipo: 'turno', texto: 'x' }])).estado, 404);
    assert.equal((await avisar(claveMod, [{ tipo: 'inicio', modelo: 'prueba' }, { tipo: 'turno', texto: 'Revisa el paquete' }, { tipo: 'permiso', herramienta: 'Bash', resumen: 'ls' }])).estado, 200);
    await hasta(() => lista.find(s => s.id === sesion.id && s.estado === 'permiso'), 3000, 'estado "pide permiso"');
    await avisar(claveMod, [{ tipo: 'herramienta_fin', herramienta: 'Bash' }, { tipo: 'turno_fin', motivo: 'answer', texto: 'Hecho' }]);
    await hasta(() => lista.find(s => s.id === sesion.id && s.estado === 'esperando'), 3000, 'estado "te espera"');
    console.log('ok  estado de la sesión con los avisos del mod');

    // 7. Sin ventana (el anfitrión no tiene terminal): toma el tamaño que manda el panel
    assert.equal(sesion.sinVentana, true, 'sin terminal la sesión es "sin ventana"');
    ws.send(JSON.stringify({ t: 'tam', id: sesion.id, cols: 101, rows: 33 }));
    await hasta(() => lista.find(s => s.id === sesion.id && s.cols === 101 && s.rows === 33), 3000, 'tamaño del panel');
    ws.send(JSON.stringify({ t: 'in', id: sesion.id, d: 'stty size\r' }));
    await hasta(() => pantalla.includes('33 101'), 3000, 'la pseudoterminal tiene el tamaño nuevo');
    console.log('ok  sesión sin ventana con el tamaño del panel');

    // 8. Lo que usa el plug-in: buscar una sesión por título, dejarle texto escrito y enseñarla en el panel
    const abrir = (...args) =>
      new Promise(resolve => {
        const h = spawn(process.execPath, [path.join(raiz, 'abrir.js'), ...args], { env });
        let out = '';
        h.stdout.on('data', c => (out += c));
        h.on('exit', codigo => resolve({ codigo, out: out.trim() }));
      });
    let enfocada = null;
    ws.on('message', m => {
      const msg = JSON.parse(m);
      if (msg.t === 'enfocar') enfocada = msg.id;
    });
    const fichero = path.join(DATOS, 'pegar.txt');
    fs.writeFileSync(fichero, 'echo TEXTO_PEGADO');
    assert.equal((await abrir('--titulo', 'Claude Code - 9999999 - No existe')).codigo, 3, 'sin sesión con ese título');
    const r = await abrir('--titulo', 'Claude Code - 1234567 - Prueba', '--pegar', fichero);
    assert.equal(r.codigo, 0);
    assert.equal(r.out, sesion.id);
    await hasta(() => pantalla.includes('echo TEXTO_PEGADO'), 3000, 'texto pegado en la sesión');
    assert.ok(!pantalla.includes('\r\nTEXTO_PEGADO'), 'pegar no pulsa Intro');
    await hasta(() => enfocada === sesion.id, 3000, 'el panel abierto enseña la sesión');
    ws.send(JSON.stringify({ t: 'in', id: sesion.id, d: '\x15' })); // borra la línea pegada
    console.log('ok  buscar por título, dejar texto escrito y enseñar la sesión');

    // 9. Nueva sesión: el servidor deja la petición al plug-in y espera a que la recoja
    const peticiones = path.join(DATOS, 'peticiones');
    let respuesta = null;
    ws.on('message', m => {
      const msg = JSON.parse(m);
      if (msg.t === 'nueva') respuesta = msg;
    });
    ws.send(JSON.stringify({ t: 'nueva', carpeta: '/no/es/un/expediente' }));
    await hasta(() => respuesta, 3000, 'respuesta a una carpeta no válida');
    assert.equal(respuesta.ok, false);
    respuesta = null;
    const expediente = fs.mkdtempSync(path.join(os.tmpdir(), 'exp-'));
    const carpetaExp = path.join(expediente, '7654321 - Otro expediente');
    fs.mkdirSync(carpetaExp);
    ws.send(JSON.stringify({ t: 'nueva', carpeta: carpetaExp }));
    const nombrePeticion = await hasta(() => fs.existsSync(peticiones) && fs.readdirSync(peticiones).find(f => f.endsWith('.json')), 3000, 'petición para el plug-in');
    const datosPeticion = JSON.parse(fs.readFileSync(path.join(peticiones, nombrePeticion), 'utf8'));
    assert.equal(datosPeticion.accion, 'claude');
    assert.equal(datosPeticion.carpeta, carpetaExp);
    fs.unlinkSync(path.join(peticiones, nombrePeticion)); // como hace el plug-in al recogerla
    await hasta(() => respuesta, 3000, 'respuesta cuando el plug-in la recoge');
    assert.equal(respuesta.ok, true);
    console.log('ok  nueva sesión: petición al plug-in');

    // 9b. Un tema (carpeta con .px/tema.json) vale como un expediente para abrir una sesión
    respuesta = null;
    const tema = path.join(expediente, 'Revisión de cierres');
    fs.mkdirSync(path.join(tema, '.px'), { recursive: true });
    fs.writeFileSync(path.join(tema, '.px', 'tema.json'), '{"titulo":"Revisión de cierres"}');
    ws.send(JSON.stringify({ t: 'nueva', carpeta: tema }));
    const peticionTema = await hasta(() => fs.existsSync(peticiones) && fs.readdirSync(peticiones).find(f => f.endsWith('.json')), 3000, 'petición del tema');
    assert.equal(JSON.parse(fs.readFileSync(path.join(peticiones, peticionTema), 'utf8')).carpeta, tema);
    fs.unlinkSync(path.join(peticiones, peticionTema));
    await hasta(() => respuesta, 3000, 'respuesta del tema');
    assert.equal(respuesta.ok, true);
    console.log('ok  nueva sesión en un tema');

    // 9c. Lista para el plug-in, con el id de la conversación que manda el mod en cada turno; pegar por id
    await avisar(claveMod, [{ tipo: 'turno', texto: 'sigue', sesion: 'conv-1234-abcd' }]);
    const listaPlugin = await hasta(async () => {
      const l = JSON.parse((await abrir('--lista')).out || '[]');
      return l.find(s => s.id === sesion.id && s.claudeId === 'conv-1234-abcd') && l;
    }, 3000, 'id de la conversación en la lista');
    assert.ok(Array.isArray(listaPlugin));
    fs.writeFileSync(fichero, 'echo POR_ID');
    assert.equal((await abrir('--id', 'noexiste12345', '--pegar', fichero)).codigo, 3, 'sin sesión con ese id');
    assert.equal((await abrir('--id', sesion.id, '--pegar', fichero)).codigo, 0);
    await hasta(() => pantalla.includes('echo POR_ID'), 3000, 'texto pegado por id');
    ws.send(JSON.stringify({ t: 'in', id: sesion.id, d: '\x15' }));
    console.log('ok  lista de sesiones con su conversación y pegar por id');

    // 9d. Asignar expediente: el servidor deja la petición al plug-in con la sesión
    let asignar = null;
    ws.on('message', m => {
      const msg = JSON.parse(m);
      if (msg.t === 'asignar') asignar = msg;
    });
    ws.send(JSON.stringify({ t: 'asignar', id: sesion.id }));
    const peticionAsignar = await hasta(() => fs.existsSync(peticiones) && fs.readdirSync(peticiones).find(f => f.endsWith('.json')), 3000, 'petición de asignar');
    const datosAsignar = JSON.parse(fs.readFileSync(path.join(peticiones, peticionAsignar), 'utf8'));
    assert.equal(datosAsignar.accion, 'asignar');
    assert.equal(datosAsignar.sesion, sesion.id);
    fs.unlinkSync(path.join(peticiones, peticionAsignar));
    await hasta(() => asignar, 3000, 'respuesta de asignar');
    assert.equal(asignar.ok, true);
    console.log('ok  asignar expediente: petición al plug-in');

    // 10. Preferencias guardadas en el equipo
    ws.send(JSON.stringify({ t: 'prefs', prefs: { tema: 'claro', ventanas: 'ocultar', mensajes: 'breves' } }));
    await hasta(() => fs.existsSync(path.join(DATOS, 'preferencias.json')), 3000, 'preferencias guardadas');
    const guardadas = JSON.parse(fs.readFileSync(path.join(DATOS, 'preferencias.json'), 'utf8'));
    assert.equal(guardadas.ventanas, 'ocultar');
    assert.equal(guardadas.mensajes, 'breves', 'el tipo de mensaje también lo lee el plug-in');
    console.log('ok  preferencias guardadas (las lee el plug-in)');

    // 11. Cerrar como lo hace el plug-in al asignar un expediente: espera a que la sesión termine
    const cierre = await abrir('--cerrar', sesion.id);
    assert.equal(cierre.codigo, 0, 'abrir.js --cerrar espera a que termine');
    await hasta(() => lista.find(s => s.id === sesion.id && !s.viva), 5000, 'sesión cerrada');
    await hasta(() => salioAnfitrion !== null, 5000, 'el anfitrión termina');
    console.log('ok  cerrar la sesión (plug-in)');
    ws.close();
    console.log('\nTodo bien.');
  } finally {
    try {
      anfitrion.kill();
    } catch {
      // ya había terminado
    }
    let srv = {};
    try {
      srv = JSON.parse(fs.readFileSync(path.join(DATOS, 'servidor.json'), 'utf8'));
    } catch {
      // el servidor ya se había ido
    }
    if (srv.pid) {
      try {
        process.kill(srv.pid);
      } catch {
        // ya había terminado
      }
    }
  }
})().catch(e => {
  console.error(`FALLO: ${e.stack || e}`);
  process.exit(1);
});
