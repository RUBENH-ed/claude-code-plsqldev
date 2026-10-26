'use strict';
// Piezas comunes del panel de sesiones: dónde guarda sus datos, el puerto y las claves, y cómo arrancar el servidor.
//
// Los datos van en %APPDATA%\ClaudeCodePlsql\panel (en Linux/macOS, ~/.config/ClaudeCodePlsql/panel), o en la
// carpeta de la variable PANEL_DATOS. Ahí quedan:
//   secretos.json  las claves del panel (se crean una vez; solo para tu usuario de Windows)
//   servidor.json  el puerto y el PID del servidor que está en marcha
//   servidor.log   el registro del servidor
// El hook de solo consultas del plug-in impide que Claude lea esta carpeta.

const fs = require('fs');
const os = require('os');
const path = require('path');
const crypto = require('crypto');
const http = require('http');
const { spawn } = require('child_process');

const VERSION = require('./package.json').version;
const PUERTO_DEFECTO = 47811;

function carpetaDatos() {
  if (process.env.PANEL_DATOS) return process.env.PANEL_DATOS;
  if (process.platform === 'win32') {
    const appdata = process.env.APPDATA || path.join(os.homedir(), 'AppData', 'Roaming');
    return path.join(appdata, 'ClaudeCodePlsql', 'panel');
  }
  const base = process.env.XDG_CONFIG_HOME || path.join(os.homedir(), '.config');
  return path.join(base, 'ClaudeCodePlsql', 'panel');
}

function asegurarCarpeta() {
  const dir = carpetaDatos();
  fs.mkdirSync(dir, { recursive: true, mode: 0o700 });
  return dir;
}

function puerto() {
  const p = parseInt(process.env.PANEL_PUERTO || '', 10);
  return p > 0 && p < 65536 ? p : PUERTO_DEFECTO;
}

function leerJson(fichero) {
  try {
    return JSON.parse(fs.readFileSync(fichero, 'utf8'));
  } catch {
    return null;
  }
}

/** Escribe un fichero de forma atómica (en uno temporal y renombrando), solo legible por el usuario. */
function escribirSeguro(fichero, texto) {
  const tmp = `${fichero}.${process.pid}.tmp`;
  fs.writeFileSync(tmp, texto, { mode: 0o600 });
  fs.renameSync(tmp, fichero);
}

/**
 * Claves del panel, creadas la primera vez:
 *   clave  la usan el plug-in y los anfitriones de las sesiones para hablar con el servidor
 *   web    el valor de la cookie del navegador (se consigue con un código de un solo uso)
 */
function secretos() {
  const fichero = path.join(asegurarCarpeta(), 'secretos.json');
  let s = leerJson(fichero);
  if (!s || typeof s.clave !== 'string' || typeof s.web !== 'string' || s.clave.length < 32 || s.web.length < 32) {
    s = { clave: crypto.randomBytes(32).toString('base64url'), web: crypto.randomBytes(32).toString('base64url') };
    try {
      // 'wx': si otro proceso la ha creado a la vez, se usa la suya
      fs.writeFileSync(fichero, JSON.stringify(s), { mode: 0o600, flag: 'wx' });
    } catch {
      const otro = leerJson(fichero);
      if (otro && otro.clave && otro.web) return otro;
      escribirSeguro(fichero, JSON.stringify(s));
    }
  }
  return s;
}

/** Compara dos cadenas en tiempo constante. */
function iguales(a, b) {
  if (typeof a !== 'string' || typeof b !== 'string') return false;
  const x = Buffer.from(a);
  const y = Buffer.from(b);
  return x.length === y.length && crypto.timingSafeEqual(x, y);
}

/** Pregunta al servidor si está en marcha (GET /api/ping). Resuelve true o false, como mucho en `ms`. */
function servidorVivo(ms = 800) {
  return new Promise(resolve => {
    const req = http.get({ host: '127.0.0.1', port: puerto(), path: '/api/ping', timeout: ms }, res => {
      let cuerpo = '';
      res.setEncoding('utf8');
      res.on('data', c => (cuerpo += c));
      res.on('end', () => {
        try {
          resolve(res.statusCode === 200 && JSON.parse(cuerpo).panel === 'sesiones-claude');
        } catch {
          resolve(false);
        }
      });
    });
    req.on('timeout', () => req.destroy());
    req.on('error', () => resolve(false));
  });
}

/** Arranca el servidor en segundo plano, sin ventana, independiente de quien lo lanza. */
function lanzarServidor() {
  const hijo = spawn(process.execPath, [path.join(__dirname, 'servidor.js')], {
    detached: true,
    stdio: 'ignore',
    windowsHide: true,
    cwd: __dirname,
    env: process.env,
  });
  hijo.unref();
}

/** Deja el servidor en marcha: si no responde, lo arranca y espera a que conteste (como mucho `ms`). */
async function asegurarServidor(ms = 8000) {
  if (await servidorVivo()) return true;
  lanzarServidor();
  const hasta = Date.now() + ms;
  while (Date.now() < hasta) {
    await new Promise(r => setTimeout(r, 250));
    if (await servidorVivo(500)) return true;
  }
  return false;
}

module.exports = {
  VERSION,
  carpetaDatos,
  asegurarCarpeta,
  puerto,
  leerJson,
  escribirSeguro,
  secretos,
  iguales,
  servidorVivo,
  lanzarServidor,
  asegurarServidor,
};
