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
const env = { ...process.env, PANEL_DATOS: DATOS, PANEL_PUERTO: String(PUERTO) };
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

    // 7. Cerrar desde el panel
    ws.send(JSON.stringify({ t: 'cerrar', id: sesion.id }));
    await hasta(() => lista.find(s => s.id === sesion.id && !s.viva), 5000, 'sesión cerrada');
    await hasta(() => salioAnfitrion !== null, 5000, 'el anfitrión termina');
    console.log('ok  cerrar la sesión desde el panel');
    ws.close();
    console.log('\nTodo bien.');
  } finally {
    try {
      anfitrion.kill();
    } catch {
      // ya había terminado
    }
    const srv = JSON.parse(fs.readFileSync(path.join(DATOS, 'servidor.json'), 'utf8').toString() || '{}');
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
