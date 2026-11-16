'use strict';
// Abre el panel de sesiones en el navegador y atiende al plug-in.
//
//   node abrir.js                          arranca el servidor si hace falta y abre el panel en una ventana propia
//                                          de Edge (o en el navegador predeterminado si no hay Edge)
//   node abrir.js --sesion <id>            enseña esa sesión: en el panel que ya esté abierto o en uno nuevo
//   node abrir.js --titulo <t> [--pegar <fichero>]
//                                          si hay una sesión abierta con ese título, deja el texto del fichero en su
//                                          cuadro de mensaje (sin enviarlo) y la enseña; si no la hay, sale con código 3
//   node abrir.js --id <id> [--pegar <fichero>]
//                                          lo mismo, con la sesión elegida por su id
//   node abrir.js --lista                  escribe en JSON las sesiones (id, titulo, carpeta, viva, sinVentana, claudeId)
//   node abrir.js --cerrar <id>            cierra esa sesión y espera a que termine (código 4 si no termina en 20 s)
//   node abrir.js --navegador              siempre en el navegador predeterminado
//   node abrir.js --imprimir               solo escribe el enlace de entrada (vale una vez y durante 2 minutos)

const fs = require('fs');
const path = require('path');
const http = require('http');
const { spawn } = require('child_process');
const C = require('./comun');

const NO_HAY_SESION = 3;

/** Llamada a la API del servidor con la clave del panel. */
function api(metodo, ruta, cuerpo) {
  return new Promise((resolve, reject) => {
    const datos = cuerpo === undefined ? '' : JSON.stringify(cuerpo);
    const req = http.request(
      {
        host: '127.0.0.1',
        port: C.puerto(),
        path: ruta,
        method: metodo,
        headers: {
          Authorization: `Bearer ${C.secretos().clave}`,
          'Content-Type': 'application/json',
          'Content-Length': Buffer.byteLength(datos),
        },
        timeout: 4000,
      },
      res => {
        let texto = '';
        res.setEncoding('utf8');
        res.on('data', c => (texto += c));
        res.on('end', () => {
          try {
            const r = JSON.parse(texto);
            if (res.statusCode === 200) resolve(r);
            else reject(new Error(r.error || `respuesta ${res.statusCode}`));
          } catch (e) {
            reject(e);
          }
        });
      }
    );
    req.on('timeout', () => req.destroy(new Error('el servidor no contesta')));
    req.on('error', reject);
    req.end(datos);
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
    // El enlace solo lleva letras, números, : / ? = & _ y -: se escapa el & para cmd
    await lanzar('cmd.exe', ['/d', '/s', '/c', `"start "" ${url.replace(/&/g, '^&')}"`], true);
    return;
  }
  await lanzar(process.platform === 'darwin' ? 'open' : 'xdg-open', [url]);
}

/** Enlace de entrada de un solo uso; con `sesion`, el panel se abre en esa sesión. */
async function enlace(sesion) {
  const { url } = await api('POST', '/api/codigo');
  return sesion ? `${url}&sesion=${encodeURIComponent(sesion)}` : url;
}

/** Enseña una sesión: si hay algún panel abierto, ese la pone delante; si no, se abre uno. */
async function enseñar(id, predeterminado) {
  const { navegadores } = await api('POST', '/api/enfocar', { id });
  if (navegadores === 0) await abrirEnNavegador(await enlace(id), predeterminado);
}

function valor(args, nombre) {
  const i = args.indexOf(nombre);
  return i >= 0 ? args[i + 1] : undefined;
}

(async () => {
  const args = process.argv.slice(2);
  const predeterminado = args.includes('--navegador');
  if (!(await C.asegurarServidor())) {
    console.error(`No he podido arrancar el servidor del panel. Mira ${path.join(C.carpetaDatos(), 'servidor.log')}`);
    process.exit(1);
  }

  if (args.includes('--lista')) {
    const { sesiones } = await api('GET', '/api/sesiones');
    process.stdout.write(JSON.stringify(sesiones));
    return;
  }

  const cerrar = valor(args, '--cerrar');
  if (cerrar !== undefined) {
    await api('POST', `/api/sesiones/${encodeURIComponent(cerrar)}/cerrar`);
    // Espera a que la sesión termine (Claude guarda la conversación al salir)
    const hasta = Date.now() + 20000;
    while (Date.now() < hasta) {
      await new Promise(r => setTimeout(r, 400));
      const { sesiones } = await api('GET', '/api/sesiones');
      const s = sesiones.find(x => x.id === cerrar);
      if (!s || !s.viva) return;
    }
    console.error('La sesión no ha terminado en 20 segundos.');
    process.exit(4);
  }

  const titulo = valor(args, '--titulo');
  const porId = valor(args, '--id');
  if (titulo !== undefined || porId !== undefined) {
    const { sesiones } = await api('GET', '/api/sesiones');
    const s = sesiones.find(x => x.viva && (porId !== undefined ? x.id === porId : x.titulo === titulo));
    if (!s) process.exit(NO_HAY_SESION);
    const fichero = valor(args, '--pegar');
    if (fichero) {
      const texto = fs.readFileSync(fichero, 'utf8').replace(/^﻿/, '');
      if (texto.trim()) await api('POST', `/api/sesiones/${s.id}/pegar`, { texto });
    }
    await enseñar(s.id, predeterminado);
    console.log(s.id);
    return;
  }

  const sesion = valor(args, '--sesion');
  if (sesion !== undefined) {
    await enseñar(sesion, predeterminado);
    return;
  }

  const url = await enlace();
  if (args.includes('--imprimir')) {
    console.log(url);
    return;
  }
  await abrirEnNavegador(url, predeterminado);
})().catch(e => {
  console.error(`No he podido abrir el panel: ${e.message}`);
  process.exit(1);
});
