'use strict';
// Anfitrión de una sesión: ejecuta un comando (el lanzador de Claude que prepara el plug-in) dentro de una
// pseudoterminal y la comparte con el panel de sesiones.
//
//   node anfitrion.js --titulo "Claude Code - 1234567 Título" --carpeta "C:\...\1234567 - Título" -- powershell.exe -NoLogo ...
//
// Para quien está en la ventana de Windows Terminal no cambia nada: ve y teclea en la sesión como siempre. Además:
//   - lo que sale por la terminal se manda al servidor del panel, que lo enseña en el navegador;
//   - lo que se escribe en el panel llega a la sesión como si se tecleara en la ventana;
//   - guarda en memoria cómo está la pantalla, para que el panel la pinte entera al abrirla.
// Si el servidor no está en marcha lo arranca; si se cae, la sesión sigue igual y se vuelve a conectar sola.
// Al comando le pasa CLAUDE_PANEL_SESION, CLAUDE_PANEL_CLAVE y CLAUDE_PANEL_URL para el mod del panel.

const path = require('path');
const crypto = require('crypto');
const { StringDecoder } = require('string_decoder');
const WebSocket = require('ws');
const pty = require('@lydell/node-pty');
const { Terminal } = require('@xterm/headless');
const { SerializeAddon } = require('@xterm/addon-serialize');
const { Unicode11Addon } = require('@xterm/addon-unicode11');
const C = require('./comun');

// ------------------------------------------------------------------------------------------------ argumentos

function leerArgumentos(argv) {
  const op = { titulo: 'Claude Code', carpeta: process.cwd(), servidor: true, comando: [] };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--') {
      op.comando = argv.slice(i + 1);
      break;
    }
    if (a === '--titulo') op.titulo = argv[++i] ?? op.titulo;
    else if (a === '--carpeta') op.carpeta = argv[++i] ?? op.carpeta;
    else if (a === '--sin-servidor') op.servidor = false;
    else {
      op.comando = argv.slice(i);
      break;
    }
  }
  if (op.comando.length === 0) {
    op.comando = process.platform === 'win32' ? ['powershell.exe', '-NoLogo'] : [process.env.SHELL || 'bash'];
  }
  return op;
}

const op = leerArgumentos(process.argv.slice(2));
const ID = crypto.randomBytes(12).toString('base64url');
const CLAVE_MOD = crypto.randomBytes(24).toString('base64url');
const URL_PANEL = `http://127.0.0.1:${C.puerto()}`;
const esTty = !!process.stdout.isTTY;

let cols = (esTty && process.stdout.columns) || 120;
let rows = (esTty && process.stdout.rows) || 30;

// Título de la ventana desde el principio: el plug-in la busca por él para colocarla y reutilizarla
if (process.platform === 'win32') process.title = op.titulo; // en una consola normal (sin Windows Terminal)
if (esTty) process.stdout.write(`\x1b]0;${op.titulo.replace(/[\x00-\x1f\x7f]/g, '')}\x07`);

// ------------------------------------------------------------------------------------------------ pantalla en memoria

const pantalla = new Terminal({ cols, rows, scrollback: 3000, allowProposedApi: true });
const serializador = new SerializeAddon();
pantalla.loadAddon(serializador);
pantalla.loadAddon(new Unicode11Addon());
pantalla.unicode.activeVersion = '11';

// ------------------------------------------------------------------------------------------------ la pseudoterminal

const entorno = {
  ...process.env,
  CLAUDE_PANEL_SESION: ID,
  CLAUDE_PANEL_CLAVE: CLAVE_MOD,
  CLAUDE_PANEL_URL: URL_PANEL,
};

function arrancar(usarDll) {
  const opciones = { name: 'xterm-256color', cols, rows, cwd: op.carpeta, env: entorno };
  if (process.platform === 'win32') opciones.useConptyDll = usarDll;
  return pty.spawn(op.comando[0], op.comando.slice(1), opciones);
}

let proceso;
try {
  // En Windows, la ConPTY que trae node-pty (más moderna que la del sistema); si no arranca, la del sistema
  proceso = arrancar(process.platform === 'win32' && process.env.PANEL_CONPTY_DLL !== '0');
} catch (e) {
  if (process.platform !== 'win32') throw e;
  proceso = arrancar(false);
}

let seq = 0;
let pendiente = '';
let temporizador = null;

function enviarSalida() {
  temporizador = null;
  if (!pendiente) return;
  seq++;
  enviar({ t: 'out', s: seq, d: pendiente });
  pendiente = '';
}

proceso.onData(d => {
  if (esTty) process.stdout.write(d);
  pantalla.write(d);
  // Se agrupa lo que sale en pocos milisegundos para no mandar cientos de mensajes diminutos
  pendiente += d;
  if (pendiente.length > 64 * 1024) {
    if (temporizador) clearTimeout(temporizador);
    enviarSalida();
  } else if (!temporizador) temporizador = setTimeout(enviarSalida, 8);
});

proceso.onExit(({ exitCode }) => {
  if (temporizador) clearTimeout(temporizador);
  enviarSalida();
  pantalla.write('', () => {
    enviar({ t: 'fin', codigo: exitCode, d: foto() });
    const cerrar = () => {
      try {
        if (esTty && process.stdin.isTTY) process.stdin.setRawMode(false);
      } catch {
        // nada
      }
      process.exit(exitCode || 0);
    };
    if (ws && ws.readyState === WebSocket.OPEN) {
      ws.once('close', cerrar);
      ws.close();
      setTimeout(cerrar, 1500).unref();
    } else cerrar();
  });
});

function foto() {
  try {
    return serializador.serialize({ scrollback: 3000 });
  } catch {
    return '';
  }
}

// ------------------------------------------------------------------------------------------------ teclado y tamaño

/**
 * Pegar varias líneas: en Windows la consola entrega lo pegado como teclas sueltas, sin las marcas de "pegado"
 * que Claude Code usa para no enviar el mensaje en el primer salto de línea. Si llega de golpe un trozo con saltos
 * de línea en medio y la sesión ha pedido el modo de pegado, se le ponen esas marcas.
 */
function conMarcasDePegado(texto) {
  if (process.platform !== 'win32' || !pantalla.modes.bracketedPasteMode) return texto;
  if (texto.includes('\x1b[200~') || texto.startsWith('\x1b')) return texto;
  const salto = texto.search(/[\r\n]/);
  if (salto < 0) return texto;
  const hayTextoDespues = /[^\r\n]/.test(texto.slice(salto));
  if (!hayTextoDespues && texto.length < 64) return texto;
  return `\x1b[200~${texto}\x1b[201~`;
}

if (esTty && process.stdin.isTTY) {
  process.stdin.setRawMode(true);
  const decodificador = new StringDecoder('utf8');
  process.stdin.on('data', b => {
    const texto = decodificador.write(b);
    if (texto) proceso.write(conMarcasDePegado(texto));
  });
  process.stdin.resume();
}

if (esTty) {
  process.stdout.on('resize', () => {
    cols = process.stdout.columns || cols;
    rows = process.stdout.rows || rows;
    try {
      proceso.resize(cols, rows);
    } catch {
      // ya ha terminado
    }
    pantalla.resize(cols, rows);
    enviar({ t: 'size', cols, rows });
  });
}

// Al cerrar la ventana de golpe, que no quede nada colgado
for (const senal of ['SIGHUP', 'SIGTERM', 'SIGBREAK']) {
  try {
    process.on(senal, () => {
      try {
        proceso.kill();
      } catch {
        // nada
      }
      process.exit(0);
    });
  } catch {
    // señal no disponible en esta plataforma
  }
}

// ------------------------------------------------------------------------------------------------ conexión con el panel

let ws = null;
let espera = 1000;
let ultimoArranque = 0;

function enviar(msg) {
  if (ws && ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(msg));
}

async function conectar() {
  if (!op.servidor) return;
  if (!(await C.servidorVivo())) {
    // Arranca el servidor como mucho una vez por minuto; si no llega, lo vuelve a intentar más tarde
    if (Date.now() - ultimoArranque > 60 * 1000) {
      ultimoArranque = Date.now();
      C.lanzarServidor();
    }
    return reintentar();
  }

  let clave;
  try {
    clave = C.secretos().clave;
  } catch {
    return reintentar();
  }

  ws = new WebSocket(`ws://127.0.0.1:${C.puerto()}/ws/anfitrion`, {
    headers: { Authorization: `Bearer ${clave}` },
    perMessageDeflate: false,
  });
  ws.on('open', () => {
    espera = 1000;
    enviar({
      t: 'hola',
      id: ID,
      claveMod: CLAVE_MOD,
      titulo: op.titulo,
      carpeta: path.resolve(op.carpeta),
      cols,
      rows,
      pid: process.pid,
      inicio: INICIO,
      version: C.VERSION,
    });
  });
  ws.on('message', raw => {
    let msg;
    try {
      msg = JSON.parse(raw);
    } catch {
      return;
    }
    switch (msg.t) {
      case 'snap?': {
        // Foto de la pantalla para un navegador que acaba de abrir la sesión. `s` dice hasta qué trozo de salida
        // incluye: el servidor le manda después solo lo posterior.
        if (temporizador) {
          clearTimeout(temporizador);
          enviarSalida();
        }
        const hasta = seq;
        pantalla.write('', () => enviar({ t: 'snap', r: msg.r, s: hasta, d: foto(), cols, rows }));
        break;
      }
      case 'in':
        if (typeof msg.d === 'string') proceso.write(msg.d);
        break;
      case 'cerrar':
        try {
          proceso.kill();
        } catch {
          // ya había terminado
        }
        break;
    }
  });
  ws.on('close', () => {
    ws = null;
    reintentar();
  });
  ws.on('error', () => {
    // el 'close' que viene detrás se encarga
  });
}

let reintento = null;
function reintentar() {
  if (reintento) return;
  reintento = setTimeout(() => {
    reintento = null;
    conectar().catch(() => reintentar());
  }, espera);
  reintento.unref();
  espera = Math.min(espera * 2, 15000);
}

const INICIO = Date.now();
conectar().catch(() => reintentar());
