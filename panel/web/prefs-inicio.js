'use strict';
// Se ejecuta antes de pintar la página: aplica la disposición y los colores guardados en este navegador para que el
// panel no aparezca un instante con los de serie. Después, app.js recibe del servidor los guardados en el equipo.
(() => {
  const html = document.documentElement;
  html.dataset.tema = 'pizarra';
  html.dataset.lista = 'izquierda';
  html.dataset.actividad = 'derecha';
  html.dataset.listaPlegada = 'no';
  html.dataset.actividadPlegada = 'no';
  html.dataset.caja = 'si';
  try {
    const p = JSON.parse(localStorage.getItem('panel-prefs') || '{}');
    const elige = (v, ...validos) => (validos.includes(v) ? v : validos[0]);
    html.dataset.tema = elige(p.tema, 'pizarra', 'noche', 'claro', 'contraste');
    html.dataset.lista = elige(p.lista, 'izquierda', 'arriba', 'derecha');
    html.dataset.actividad = elige(p.actividad, 'derecha', 'abajo', 'izquierda');
    html.dataset.listaPlegada = p.listaPlegada ? 'si' : 'no';
    html.dataset.actividadPlegada = p.actividadPlegada ? 'si' : 'no';
    html.dataset.caja = p.caja === false ? 'no' : 'si';
    for (const [k, v] of Object.entries(p.colores || {})) {
      if (/^[a-z-]+$/.test(k) && /^#[0-9a-f]{6}$/i.test(v)) html.style.setProperty(`--${k}`, v);
    }
  } catch {
    // sin preferencias guardadas (o el navegador no deja guardar): las de serie
  }
})();
