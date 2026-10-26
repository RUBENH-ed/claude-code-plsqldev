'use strict';
// Abre el panel de sesiones en el navegador (lo lanza el botón "Panel de sesiones" del plug-in).
//
//   node abrir.js              arranca el servidor si hace falta y abre el panel en una ventana propia de Edge
//                              (o en el navegador predeterminado si no hay Edge)
//   node abrir.js --navegador  siempre en el navegador predeterminado
//   node abrir.js --imprimir   solo escribe el enlace de entrada (vale una vez y durante 2 minutos)

const fs = require('fs');
const path = require('path');
const http = require('http');
const { spawn } = require('child_process');
const C = require('./comun');

function pedirCodigo() {
  return new Promise((resolve, reject) => {
    const req = http.request(
      {
        host: '127.0.0.1',
        port: C.puerto(),
        path: '/api/codigo',
        method: 'POST',
        headers: { Authorization: `Bearer ${C.secretos().clave}`, 'Content-Length': 0 },
        timeout: 3000,
      },
      res => {
        let cuerpo = '';
        res.setEncoding('utf8');
        res.on('data', c => (cuerpo += c));
        res.on('end', () => {
          try {
            const r = JSON.parse(cuerpo);
            if (res.statusCode === 200 && r.url) resolve(r.url);
            else reject(new Error(r.error || `respuesta ${res.statusCode}`));
          } catch (e) {
            reject(e);
          }
        });
      }
    );
    req.on('timeout', () => req.destroy(new Error('el servidor no contesta')));
    req.on('error', reject);
    req.end();
  });
}

function lanzar(cmd, args, literal = false) {
  return new Promise(resolve => {
    try {
      const p = spawn(cmd, args, { detached: true, stdio: 'ignore', windowsHide: true, windowsVerbatimArguments: literal });
      p.on('error', () => resolve(false));
      p.on('spawn', () => {
        p.unref();
        resolve(true);
      });
    } catch {
      resolve(false);
    }
  });
}

function rutaEdge() {
  const bases = [process.env['ProgramFiles(x86)'], process.env.ProgramFiles, process.env.LOCALAPPDATA].filter(Boolean);
  return bases.map(b => path.join(b, 'Microsoft', 'Edge', 'Application', 'msedge.exe')).find(f => fs.existsSync(f));
}

async function abrirEnNavegador(url, predeterminado) {
  if (process.platform === 'win32') {
    // Ventana propia de Edge, sin pestañas ni barra de direcciones
    const edge = !predeterminado && rutaEdge();
    if (edge && (await lanzar(edge, [`--app=${url}`]))) return;
    // El enlace solo lleva letras, números, : / ? = _ y -: no hay nada que escapar para cmd
    await lanzar('cmd.exe', ['/d', '/s', '/c', `"start "" ${url}"`], true);
    return;
  }
  await lanzar(process.platform === 'darwin' ? 'open' : 'xdg-open', [url]);
}

(async () => {
  const args = process.argv.slice(2);
  if (!(await C.asegurarServidor())) {
    console.error(`No he podido arrancar el servidor del panel. Mira ${C.carpetaDatos()}\\servidor.log`);
    process.exit(1);
  }
  const url = await pedirCodigo();
  if (args.includes('--imprimir')) {
    console.log(url);
    return;
  }
  await abrirEnNavegador(url, args.includes('--navegador'));
})().catch(e => {
  console.error(`No he podido abrir el panel: ${e.message}`);
  process.exit(1);
});
