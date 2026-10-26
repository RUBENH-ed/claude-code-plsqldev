'use strict';
// Servidor del panel de sesiones de Claude Code.
//
// Escucha solo en 127.0.0.1. Reúne:
//   - los anfitriones (anfitrion.js), uno por sesión de Claude abierta desde el plug-in: le mandan lo que sale por
//     su terminal y reciben lo que se escribe en el panel;
//   - el mod de cada sesión (mod/), que avisa de lo que hace Claude: turnos, herramientas, permisos;
//   - los navegadores con el panel abierto.
//
// Seguridad: los anfitriones y el plug-in se identifican con la clave de secretos.json; el navegador, con una
// cookie que se consigue con un código de un solo uso que pide el plug-in (abrir.js). Se comprueban Host y Origin
// para que una página web cualquiera no pueda hablar con el panel. El mod de cada sesión solo puede informar del
// estado de su propia sesión, con una clave que solo sirve para eso.
//
// Se cierra solo tras 20 minutos sin sesiones vivas ni navegadores conectados.

const fs = require('fs');
const path = require('path');
const http = require('http');
const crypto = require('crypto');
const { WebSocketServer } = require('ws');
const C = require('./comun');

const PUERTO = C.puerto();
const DATOS = C.asegurarCarpeta();
const SECRETOS = C.secretos();
const WEB = path.join(__dirname, 'web');
const MODULOS = path.join(__dirname, 'node_modules');
const INACTIVIDAD_MS = parseInt(process.env.PANEL_INACTIVIDAD_MS || '', 10) || 20 * 60 * 1000;
const MAX_EVENTOS = 200;
const MAX_COLA = 4000;

// ------------------------------------------------------------------------------------------------ registro

const LOG = path.join(DATOS, 'servidor.log');
function log(...partes) {
  const linea = `${new Date().toISOString()} ${partes.join(' ')}\n`;
  try {
    if (fs.existsSync(LOG) && fs.statSync(LOG).size > 1024 * 1024) fs.renameSync(LOG, `${LOG}.1`);
    fs.appendFileSync(LOG, linea, { mode: 0o600 });
  } catch {
    // sin registro no pasa nada
  }
  if (process.env.PANEL_LOG_CONSOLA) process.stderr.write(linea);
}

// ------------------------------------------------------------------------------------------------ sesiones

/** @type {Map<string, any>} */
const sesiones = new Map();
/** @type {Set<import('ws').WebSocket>} */
const navegadores = new Set();
const codigos = new Map(); // código de un solo uso -> caduca (ms)

function nuevaSesion(hola) {
  return {
    id: hola.id,
    claveMod: hola.claveMod,
    titulo: String(hola.titulo || 'Claude Code').slice(0, 200),
    carpeta: String(hola.carpeta || '').slice(0, 1000),
    pid: hola.pid | 0,
    inicio: Number(hola.inicio) || Date.now(),
    cols: hola.cols | 0 || 120,
    rows: hola.rows | 0 || 30,
    host: null,
    viva: true,
    finCodigo: null,
    fin: null,
    ultimaSalida: 0,
    // Estado que manda el mod (si la sesión lo tiene)
    mod: { activo: false, estado: '', actividad: '', desde: 0, claudeId: '', modelo: '' },
    eventos: [],
    visores: new Map(), // navegador -> { listo, cola }
    pendientes: new Map(), // petición de foto -> navegador
    snapFinal: null,
    resumenAnterior: '',
  };
}

/** Estado que se enseña: el del mod si lo hay; si no, se deduce de si la terminal ha sacado algo hace poco. */
function estadoDe(s) {
  if (!s.viva) return s.host === null && s.fin === null ? 'desconectada' : 'cerrada';
  if (s.mod.activo && s.mod.estado) return s.mod.estado;
  return Date.now() - s.ultimaSalida < 2500 ? 'activa' : 'reposo';
}

function resumen(s) {
  return {
    id: s.id,
    titulo: s.titulo,
    carpeta: s.carpeta,
    inicio: s.inicio,
    viva: s.viva,
    estado: estadoDe(s),
    actividad: s.mod.activo ? s.mod.actividad : '',
    desde: s.mod.activo ? s.mod.desde : 0,
    ultimaSalida: s.ultimaSalida,
    cols: s.cols,
    rows: s.rows,
    finCodigo: s.finCodigo,
    fin: s.fin,
    conMod: s.mod.activo,
    modelo: s.mod.modelo,
  };
}

function enviar(ws, msg) {
  if (ws && ws.readyState === 1) ws.send(JSON.stringify(msg));
}

let listaPendiente = null;
/** Manda la lista a los navegadores (agrupando los cambios seguidos). */
function avisarLista() {
  if (listaPendiente) return;
  listaPendiente = setTimeout(() => {
    listaPendiente = null;
    const lista = [...sesiones.values()].map(resumen);
    for (const s of sesiones.values()) s.resumenAnterior = JSON.stringify(resumen(s));
    for (const n of navegadores) enviar(n, { t: 'lista', sesiones: lista });
  }, 150);
}

// Cada segundo: las sesiones sin mod pasan de "activa" a "reposo" cuando su terminal se calla
setInterval(() => {
  for (const s of sesiones.values()) {
    if (JSON.stringify(resumen(s)) !== s.resumenAnterior) {
      avisarLista();
      break;
    }
  }
}, 1000).unref();

function añadirEvento(s, ev) {
  const e = { ...ev, hora: Date.now() };
  s.eventos.push(e);
  if (s.eventos.length > MAX_EVENTOS) s.eventos.splice(0, s.eventos.length - MAX_EVENTOS);
  for (const n of s.visores.keys()) enviar(n, { t: 'evento', id: s.id, ev: e });
}

// ------------------------------------------------------------------------------------------------ eventos del mod

const recortar = (v, n) => String(v ?? '').replace(/\s+/g, ' ').trim().slice(0, n);

function aplicarEventoMod(s, ev) {
  if (!ev || typeof ev.tipo !== 'string') return;
  const m = s.mod;
  const ahora = Date.now();
  const sub = ev.subagente ? '(subagente) ' : '';
  const cambiar = (estado, actividad) => {
    if (m.estado !== estado) m.desde = ahora;
    m.estado = estado;
    m.actividad = actividad;
  };
  m.activo = true;

  switch (ev.tipo) {
    case 'inicio':
      if (ev.motivo === 'clear') {
        cambiar('esperando', 'Conversación nueva. Esperando tu mensaje');
        añadirEvento(s, { tipo: 'inicio', texto: 'Conversación nueva (/clear)' });
        break;
      }
      m.claudeId = recortar(ev.sesion, 100);
      m.modelo = recortar(ev.modelo, 60);
      cambiar('esperando', 'Lista para tu mensaje');
      añadirEvento(s, { tipo: 'inicio', texto: `Sesión de Claude iniciada${m.modelo ? ` (${m.modelo})` : ''}` });
      break;
    case 'turno':
      if (ev.subagente) break;
      cambiar('trabajando', 'Pensando…');
      añadirEvento(s, { tipo: 'mensaje', texto: recortar(ev.texto, 500) || '(continúa)' });
      break;
    case 'herramienta':
      cambiar('trabajando', `${sub}${recortar(ev.herramienta, 60)}${ev.resumen ? ': ' + recortar(ev.resumen, 160) : ''}`);
      añadirEvento(s, { tipo: 'herramienta', herramienta: recortar(ev.herramienta, 60), texto: recortar(ev.resumen, 300), subagente: !!ev.subagente });
      break;
    case 'herramienta_fin':
      if (m.estado === 'permiso') cambiar('trabajando', m.actividad.replace(/^Pide permiso para /, ''));
      if (ev.denegada) añadirEvento(s, { tipo: 'denegada', herramienta: recortar(ev.herramienta, 60), texto: recortar(ev.motivo, 200) });
      break;
    case 'permiso':
      cambiar('permiso', `Pide permiso para ${sub}${recortar(ev.herramienta, 60)}${ev.resumen ? ': ' + recortar(ev.resumen, 160) : ''}`);
      añadirEvento(s, { tipo: 'permiso', herramienta: recortar(ev.herramienta, 60), texto: recortar(ev.resumen, 300) });
      break;
    case 'turno_fin': {
      if (ev.subagente) break;
      const motivo = { answer: 'Ha terminado', aborted: 'Interrumpida', refusal: 'Se ha negado', error: 'Error de la API' }[ev.motivo] || 'Ha terminado';
      cambiar('esperando', motivo === 'Ha terminado' ? 'Esperando tu mensaje' : `${motivo}. Esperando tu mensaje`);
      añadirEvento(s, { tipo: 'respuesta', motivo: recortar(ev.motivo, 20), texto: recortar(ev.texto, 600) });
      break;
    }
    case 'fin':
      m.activo = false;
      m.estado = '';
      m.actividad = '';
      añadirEvento(s, { tipo: 'fin', texto: 'Sesión de Claude terminada' });
      break;
    default:
      return;
  }
  avisarLista();
}

// ------------------------------------------------------------------------------------------------ HTTP

const TIPOS = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
};

// Ficheros que sirve el panel: los de web/ y estos de xterm.js
const VENDOR = {
  '/vendor/xterm.js': path.join(MODULOS, '@xterm/xterm/lib/xterm.js'),
  '/vendor/xterm.css': path.join(MODULOS, '@xterm/xterm/css/xterm.css'),
  '/vendor/addon-unicode11.js': path.join(MODULOS, '@xterm/addon-unicode11/lib/addon-unicode11.js'),
  '/vendor/addon-web-links.js': path.join(MODULOS, '@xterm/addon-web-links/lib/addon-web-links.js'),
};

const HOSTS_VALIDOS = new Set([`127.0.0.1:${PUERTO}`, `localhost:${PUERTO}`]);
const ORIGENES_VALIDOS = new Set([`http://127.0.0.1:${PUERTO}`, `http://localhost:${PUERTO}`]);

function cabecerasSeguridad(res) {
  res.setHeader('X-Content-Type-Options', 'nosniff');
  res.setHeader('X-Frame-Options', 'DENY');
  res.setHeader('Referrer-Policy', 'no-referrer');
  res.setHeader('Cache-Control', 'no-store');
  res.setHeader(
    'Content-Security-Policy',
    "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
      `connect-src 'self' ws://127.0.0.1:${PUERTO} ws://localhost:${PUERTO}; frame-ancestors 'none'; base-uri 'none'; form-action 'none'`
  );
}

function cookies(req) {
  const out = {};
  for (const parte of String(req.headers.cookie || '').split(';')) {
    const i = parte.indexOf('=');
    if (i > 0) out[parte.slice(0, i).trim()] = parte.slice(i + 1).trim();
  }
  return out;
}

const conCookie = req => C.iguales(cookies(req).panel_sesiones, SECRETOS.web);
const conClave = req => C.iguales(String(req.headers.authorization || '').replace(/^Bearer\s+/i, ''), SECRETOS.clave);

function responder(res, estado, cuerpo, tipo = 'application/json; charset=utf-8') {
  cabecerasSeguridad(res);
  res.writeHead(estado, { 'Content-Type': tipo });
  res.end(typeof cuerpo === 'string' || Buffer.isBuffer(cuerpo) ? cuerpo : JSON.stringify(cuerpo));
}

function leerCuerpo(req, max = 256 * 1024) {
  return new Promise((resolve, reject) => {
    let tam = 0;
    const trozos = [];
    req.on('data', c => {
      tam += c.length;
      if (tam > max) {
        reject(new Error('demasiado grande'));
        req.destroy();
      } else trozos.push(c);
    });
    req.on('end', () => resolve(Buffer.concat(trozos).toString('utf8')));
    req.on('error', reject);
  });
}

function servirFichero(res, fichero) {
  fs.readFile(fichero, (err, datos) => {
    if (err) return responder(res, 404, { error: 'no existe' });
    responder(res, 200, datos, TIPOS[path.extname(fichero)] || 'application/octet-stream');
  });
}

const PAGINA_SIN_ACCESO = `<!doctype html><html lang="es"><head><meta charset="utf-8"><title>Panel de sesiones</title>
<style>body{font:15px system-ui,sans-serif;background:#16171d;color:#e6e6e6;display:grid;place-items:center;height:100vh;margin:0}
main{max-width:460px;line-height:1.5}h1{font-size:18px;color:#f0a37f}code{background:#26272f;padding:1px 5px;border-radius:4px}</style></head>
<body><main><h1>Panel de sesiones de Claude</h1><p>Para entrar, ábrelo desde PL/SQL Developer: pestaña <b>Claude Code</b> &gt;
<b>Panel de sesiones</b>. El enlace de entrada solo vale una vez.</p></main></body></html>`;

async function atender(req, res) {
  // Solo se habla con 127.0.0.1/localhost en el puerto del panel (evita el "DNS rebinding")
  if (!HOSTS_VALIDOS.has(String(req.headers.host || ''))) return responder(res, 421, { error: 'host no válido' });

  const url = new URL(req.url, `http://127.0.0.1:${PUERTO}`);
  const ruta = url.pathname;

  if (ruta === '/api/ping') return responder(res, 200, { panel: 'sesiones-claude', version: C.VERSION, pid: process.pid });

  // El plug-in (abrir.js) pide un código de un solo uso para entrar desde el navegador
  if (ruta === '/api/codigo' && req.method === 'POST') {
    if (!conClave(req)) return responder(res, 401, { error: 'sin permiso' });
    const codigo = crypto.randomBytes(24).toString('base64url');
    codigos.set(codigo, Date.now() + 2 * 60 * 1000);
    return responder(res, 200, { codigo, url: `http://127.0.0.1:${PUERTO}/entrar?codigo=${codigo}` });
  }

  if (ruta === '/entrar') {
    const codigo = url.searchParams.get('codigo') || '';
    const caduca = codigos.get(codigo);
    codigos.delete(codigo);
    for (const [c, t] of codigos) if (t < Date.now()) codigos.delete(c);
    if (!caduca || caduca < Date.now()) {
      if (conCookie(req)) {
        res.writeHead(302, { Location: '/' });
        return res.end();
      }
      return responder(res, 403, PAGINA_SIN_ACCESO, 'text/html; charset=utf-8');
    }
    cabecerasSeguridad(res);
    res.writeHead(302, {
      Location: '/',
      'Set-Cookie': `panel_sesiones=${SECRETOS.web}; HttpOnly; SameSite=Strict; Path=/; Max-Age=31536000`,
    });
    return res.end();
  }

  // El mod de una sesión informa de lo que hace Claude
  const m = /^\/api\/sesiones\/([A-Za-z0-9_-]{8,64})\/eventos$/.exec(ruta);
  if (m && req.method === 'POST') {
    const s = sesiones.get(m[1]);
    const clave = String(req.headers.authorization || '').replace(/^Bearer\s+/i, '');
    if (!s || !C.iguales(clave, s.claveMod)) return responder(res, 404, { error: 'sesión desconocida' });
    let datos;
    try {
      datos = JSON.parse(await leerCuerpo(req));
    } catch {
      return responder(res, 400, { error: 'JSON no válido' });
    }
    for (const ev of Array.isArray(datos.eventos) ? datos.eventos.slice(0, 100) : []) aplicarEventoMod(s, ev);
    return responder(res, 200, { ok: true });
  }

  if (req.method !== 'GET') return responder(res, 405, { error: 'método no permitido' });

  if (ruta === '/' || ruta === '/index.html') {
    if (!conCookie(req)) return responder(res, 403, PAGINA_SIN_ACCESO, 'text/html; charset=utf-8');
    return servirFichero(res, path.join(WEB, 'index.html'));
  }
  if (VENDOR[ruta]) return servirFichero(res, VENDOR[ruta]);
  if (/^\/[a-z0-9-]+\.(js|css|svg|png|ico)$/.test(ruta)) return servirFichero(res, path.join(WEB, ruta.slice(1)));
  return responder(res, 404, { error: 'no existe' });
}

const servidor = http.createServer((req, res) => {
  atender(req, res).catch(e => {
    log('error', e && e.stack);
    try {
      responder(res, 500, { error: 'error interno' });
    } catch {
      // la respuesta ya había empezado
    }
  });
});

// ------------------------------------------------------------------------------------------------ WebSockets

const wssHost = new WebSocketServer({ noServer: true, maxPayload: 8 * 1024 * 1024 });
const wssNav = new WebSocketServer({ noServer: true, maxPayload: 1024 * 1024 });

servidor.on('upgrade', (req, socket, cabeza) => {
  const rechazar = codigo => {
    socket.write(`HTTP/1.1 ${codigo} No\r\nConnection: close\r\n\r\n`);
    socket.destroy();
  };
  if (!HOSTS_VALIDOS.has(String(req.headers.host || ''))) return rechazar(421);
  const ruta = new URL(req.url, 'http://x').pathname;

  if (ruta === '/ws/anfitrion') {
    if (!conClave(req)) return rechazar(401);
    return wssHost.handleUpgrade(req, socket, cabeza, ws => conectarAnfitrion(ws));
  }
  if (ruta === '/ws/panel') {
    if (!conCookie(req) || !ORIGENES_VALIDOS.has(String(req.headers.origin || ''))) return rechazar(401);
    return wssNav.handleUpgrade(req, socket, cabeza, ws => conectarNavegador(ws));
  }
  rechazar(404);
});

function conectarAnfitrion(ws) {
  let s = null;
  ws.on('message', raw => {
    let msg;
    try {
      msg = JSON.parse(raw);
    } catch {
      return;
    }
    if (!s) {
      if (msg.t !== 'hola' || !/^[A-Za-z0-9_-]{8,64}$/.test(String(msg.id)) || typeof msg.claveMod !== 'string') return ws.close();
      s = sesiones.get(msg.id);
      if (s && s.host && s.host !== ws) s.host.close();
      if (!s) {
        s = nuevaSesion(msg);
        sesiones.set(s.id, s);
        log('sesión nueva', s.id, JSON.stringify(s.titulo), s.carpeta);
      } else {
        // El anfitrión vuelve después de que se reiniciara el servidor o se cortara la conexión
        s.viva = true;
        s.fin = null;
        s.cols = msg.cols | 0 || s.cols;
        s.rows = msg.rows | 0 || s.rows;
        log('sesión reconectada', s.id);
      }
      s.host = ws;
      // Los navegadores que ya la estaban mirando piden una foto nueva
      for (const n of s.visores.keys()) pedirFoto(s, n);
      avisarLista();
      return;
    }

    switch (msg.t) {
      case 'out': {
        s.ultimaSalida = Date.now();
        const salida = { t: 'out', id: s.id, d: msg.d };
        for (const [n, v] of s.visores) {
          if (v.listo) enviar(n, salida);
          else if (v.cola.length < MAX_COLA) v.cola.push({ s: msg.s, d: msg.d });
        }
        break;
      }
      case 'snap': {
        const n = s.pendientes.get(msg.r);
        s.pendientes.delete(msg.r);
        const v = n && s.visores.get(n);
        if (!v) break;
        enviar(n, { t: 'snap', id: s.id, d: msg.d, cols: msg.cols, rows: msg.rows });
        for (const c of v.cola) if (c.s > msg.s) enviar(n, { t: 'out', id: s.id, d: c.d });
        v.cola = [];
        v.listo = true;
        break;
      }
      case 'size':
        s.cols = msg.cols | 0 || s.cols;
        s.rows = msg.rows | 0 || s.rows;
        for (const n of s.visores.keys()) enviar(n, { t: 'size', id: s.id, cols: s.cols, rows: s.rows });
        avisarLista();
        break;
      case 'fin':
        s.viva = false;
        s.finCodigo = msg.codigo ?? null;
        s.fin = Date.now();
        s.snapFinal = typeof msg.d === 'string' ? msg.d : null;
        s.mod.activo = false;
        añadirEvento(s, { tipo: 'cerrada', texto: `Terminal cerrada${msg.codigo ? ` (código ${msg.codigo})` : ''}` });
        log('sesión cerrada', s.id, msg.codigo);
        avisarLista();
        break;
    }
  });
  ws.on('close', () => {
    if (s && s.host === ws) {
      s.host = null;
      if (s.viva) {
        s.viva = false; // sin "fin": el anfitrión se ha caído o se ha cerrado la ventana de golpe
        log('anfitrión desconectado', s.id);
      }
      for (const v of s.visores.values()) v.listo = true;
      s.pendientes.clear();
      avisarLista();
    }
  });
  ws.on('error', () => {});
}

let peticion = 0;
function pedirFoto(s, n) {
  const v = s.visores.get(n);
  if (!v) return;
  if (s.host && s.viva) {
    v.listo = false;
    v.cola = [];
    const r = ++peticion;
    s.pendientes.set(r, n);
    enviar(s.host, { t: 'snap?', r });
  } else {
    v.listo = true;
    enviar(n, { t: 'snap', id: s.id, d: s.snapFinal || '', cols: s.cols, rows: s.rows, final: true });
  }
}

function conectarNavegador(ws) {
  navegadores.add(ws);
  enviar(ws, { t: 'lista', sesiones: [...sesiones.values()].map(resumen), version: C.VERSION });

  ws.on('message', raw => {
    let msg;
    try {
      msg = JSON.parse(raw);
    } catch {
      return;
    }
    const s = sesiones.get(String(msg.id));
    switch (msg.t) {
      case 'sub':
        if (!s) return;
        s.visores.set(ws, { listo: false, cola: [] });
        enviar(ws, { t: 'eventos', id: s.id, lista: s.eventos });
        pedirFoto(s, ws);
        break;
      case 'unsub':
        if (s) s.visores.delete(ws);
        break;
      case 'in':
        // Lo que se escribe en el panel va a la terminal de esa sesión, como si se tecleara en ella
        if (s && s.host && s.viva && typeof msg.d === 'string' && msg.d.length <= 64 * 1024) enviar(s.host, { t: 'in', d: msg.d });
        break;
      case 'cerrar':
        if (s && s.host && s.viva) {
          log('cerrar desde el panel', s.id);
          enviar(s.host, { t: 'cerrar' });
        }
        break;
      case 'olvidar':
        if (s && !s.viva) {
          sesiones.delete(s.id);
          avisarLista();
        }
        break;
    }
  });
  ws.on('close', () => {
    navegadores.delete(ws);
    for (const s of sesiones.values()) s.visores.delete(ws);
  });
  ws.on('error', () => {});
}

// ------------------------------------------------------------------------------------------------ arranque y cierre

let sinActividadDesde = Date.now();
setInterval(() => {
  const ocupado = navegadores.size > 0 || [...sesiones.values()].some(s => s.viva);
  if (ocupado) sinActividadDesde = Date.now();
  else if (Date.now() - sinActividadDesde > INACTIVIDAD_MS) terminar('sin sesiones ni navegadores');
}, 15 * 1000).unref();

const FICHERO_SERVIDOR = path.join(DATOS, 'servidor.json');

function terminar(motivo) {
  log('cierre:', motivo);
  try {
    const actual = C.leerJson(FICHERO_SERVIDOR);
    if (actual && actual.pid === process.pid) fs.unlinkSync(FICHERO_SERVIDOR);
  } catch {
    // nada
  }
  process.exit(0);
}

process.on('SIGINT', () => terminar('SIGINT'));
process.on('SIGTERM', () => terminar('SIGTERM'));
process.on('uncaughtException', e => log('excepción', e && e.stack));

servidor.on('error', async e => {
  if (e.code === 'EADDRINUSE') {
    // Normalmente es otro servidor del panel que ya está en marcha (por ejemplo, dos sesiones a la vez)
    const vivo = await C.servidorVivo();
    log(vivo ? 'ya hay un servidor del panel en marcha; este se cierra' : `el puerto ${PUERTO} lo usa otro programa (PANEL_PUERTO para cambiarlo)`);
    process.exit(vivo ? 0 : 1);
  }
  log('error del servidor', e && e.stack);
  process.exit(1);
});

servidor.listen(PUERTO, '127.0.0.1', () => {
  C.escribirSeguro(FICHERO_SERVIDOR, JSON.stringify({ puerto: PUERTO, pid: process.pid, inicio: Date.now(), version: C.VERSION }));
  log(`en marcha en http://127.0.0.1:${PUERTO} (pid ${process.pid})`);
});
