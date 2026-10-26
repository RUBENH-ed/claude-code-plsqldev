'use strict';
// Panel de sesiones de Claude: lista, terminal en directo, actividad y vista de todas las sesiones.
/* global Terminal, Unicode11Addon, WebLinksAddon */

(() => {
  const $ = id => document.getElementById(id);
  const app = $('app');

  const ESTADOS = {
    trabajando: 'Trabajando',
    permiso: 'Pide permiso',
    esperando: 'Te espera',
    activa: 'Activa',
    reposo: 'En reposo',
    cerrada: 'Cerrada',
    desconectada: 'Desconectada',
  };

  const MONO = getComputedStyle(document.documentElement).getPropertyValue('--mono').trim() || 'monospace';
  const TEMA = {
    background: '#131924',
    foreground: '#e6ebf2',
    cursor: '#e6ebf2',
    cursorAccent: '#131924',
    selectionBackground: '#3a4a66',
    black: '#1a2130', red: '#f2665c', green: '#4fc79a', yellow: '#e9b44c',
    blue: '#7fa8e0', magenta: '#c49be8', cyan: '#5fc4d4', white: '#d5dbe5',
    brightBlack: '#66728a', brightRed: '#ff8a80', brightGreen: '#7fe0b8', brightYellow: '#f5cd7a',
    brightBlue: '#a6c4f0', brightMagenta: '#d9baf5', brightCyan: '#8edbe6', brightWhite: '#ffffff',
  };

  /** @type {Map<string, any>} */
  let sesiones = new Map();
  let seleccion = null;
  let ws = null;
  let conectado = false;

  // ------------------------------------------------------------------------------------------ utilidades

  function partirTitulo(titulo) {
    const t = String(titulo || '').replace(/^Claude Code\s*-\s*/, '');
    const m = /^(\d{5,8})\s*[-–]\s*(.*)$/.exec(t);
    return m ? { numero: m[1], texto: m[2] || t } : { numero: '', texto: t || 'Claude Code' };
  }

  function hace(ms) {
    if (!ms) return '';
    const s = Math.max(0, Math.round((Date.now() - ms) / 1000));
    if (s < 60) return 'hace un momento';
    const m = Math.round(s / 60);
    if (m < 60) return `hace ${m} min`;
    const h = Math.round(m / 60);
    return `hace ${h} h`;
  }

  function hora(ms) {
    const d = new Date(ms);
    return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
  }

  function detalle(s) {
    if (s.estado === 'cerrada') return `Terminal cerrada ${hace(s.fin)}`;
    if (s.estado === 'desconectada') return 'Se ha perdido la conexión con su terminal';
    if (s.actividad) return s.actividad;
    if (!s.conMod) return s.estado === 'activa' ? 'Saliendo texto por la terminal' : `Sin novedades ${hace(s.ultimaSalida || s.inicio)}`;
    return '';
  }

  function el(tag, attrs = {}, ...hijos) {
    const e = document.createElement(tag);
    for (const [k, v] of Object.entries(attrs)) {
      if (v === undefined || v === null || v === false) continue;
      if (k === 'class') e.className = v;
      else if (k.startsWith('on')) e.addEventListener(k.slice(2), v);
      else e.setAttribute(k, v === true ? '' : v);
    }
    for (const h of hijos) if (h !== null && h !== undefined) e.append(h);
    return e;
  }

  function nombreNodo(s) {
    const { numero, texto } = partirTitulo(s.titulo);
    return el('span', { class: 'nombre', title: s.titulo }, numero ? el('span', { class: 'numero' }, numero) : null, texto);
  }

  // ------------------------------------------------------------------------------------------ terminales

  /** Vistas de terminal abiertas por sesión: la principal y las del mosaico. */
  const vistas = new Map(); // id -> Set<{ term, ajustar }>

  function medirCelda() {
    const c = document.createElement('canvas').getContext('2d');
    c.font = `100px ${MONO}`;
    const m = c.measureText('W');
    return { ancho: m.width / 100, alto: 1.2 };
  }
  const CELDA = medirCelda();

  /** Tamaño de letra para que cols x rows quepan en el hueco (la sesión manda el tamaño, no el panel). */
  function letraPara(cols, rows, ancho, alto, max) {
    const porAncho = ancho / (cols * CELDA.ancho);
    const porAlto = alto / (rows * CELDA.alto);
    return Math.max(6, Math.min(max, Math.floor(Math.min(porAncho, porAlto) * 2) / 2));
  }

  function crearTerminal(contenedor, interactiva) {
    const term = new Terminal({
      cols: 120,
      rows: 30,
      fontFamily: MONO,
      fontSize: 14,
      theme: TEMA,
      scrollback: 3000,
      allowProposedApi: true,
      disableStdin: !interactiva,
      cursorBlink: interactiva,
      cursorInactiveStyle: 'outline',
      macOptionIsMeta: true,
    });
    term.loadAddon(new Unicode11Addon.Unicode11Addon());
    term.unicode.activeVersion = '11';
    if (interactiva) term.loadAddon(new WebLinksAddon.WebLinksAddon());
    term.open(contenedor);
    return term;
  }

  function añadirVista(id, vista) {
    let set = vistas.get(id);
    if (!set) vistas.set(id, (set = new Set()));
    set.add(vista);
    // Cada vista nueva necesita la pantalla entera: el servidor manda una foto a todas las de esa sesión
    enviar({ t: 'sub', id });
  }

  function quitarVista(id, vista) {
    const set = vistas.get(id);
    if (!set) return;
    set.delete(vista);
    if (set.size === 0) {
      vistas.delete(id);
      enviar({ t: 'unsub', id });
    }
  }

  function vistasDe(id) {
    return vistas.get(id) || new Set();
  }

  // Terminal principal (una sola, que cambia de sesión)
  const principal = {
    id: null,
    term: crearTerminal($('terminal'), true),
    cols: 120,
    rows: 30,
    ajustar() {
      const marco = $('marco');
      const fs = letraPara(this.cols, this.rows, marco.clientWidth - 18, marco.clientHeight - 18, 16);
      if (this.term.options.fontSize !== fs) this.term.options.fontSize = fs;
    },
  };
  principal.term.onData(d => {
    if (principal.id) enviar({ t: 'in', id: principal.id, d });
  });
  new ResizeObserver(() => principal.ajustar()).observe($('marco'));

  function mirar(id) {
    if (principal.id === id) return;
    if (principal.id) quitarVista(principal.id, principal);
    principal.id = id;
    principal.term.reset();
    $('eventos').replaceChildren();
    if (id) añadirVista(id, principal);
  }

  // ------------------------------------------------------------------------------------------ conexión

  function enviar(msg) {
    if (ws && ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(msg));
  }

  function conectar() {
    ws = new WebSocket(`ws://${location.host}/ws/panel`);
    ws.onopen = () => {
      conectado = true;
      $('desconectado').hidden = true;
      // Al volver, se piden otra vez las pantallas que estaban abiertas
      for (const id of vistas.keys()) enviar({ t: 'sub', id });
    };
    ws.onmessage = ev => recibir(JSON.parse(ev.data));
    ws.onclose = () => {
      if (conectado) $('desconectado').hidden = false;
      conectado = false;
      setTimeout(conectar, 1500);
    };
  }

  function recibir(msg) {
    switch (msg.t) {
      case 'lista':
        actualizarLista(msg.sesiones);
        break;
      case 'snap':
        for (const v of vistasDe(msg.id)) {
          // Una foto final vacía (la terminal se cerró de golpe) no borra lo que ya se veía
          if (msg.final && !msg.d) continue;
          v.cols = msg.cols;
          v.rows = msg.rows;
          v.term.reset();
          v.term.resize(msg.cols, msg.rows);
          v.term.write(msg.d);
          v.ajustar();
        }
        break;
      case 'out':
        for (const v of vistasDe(msg.id)) v.term.write(msg.d);
        break;
      case 'size':
        for (const v of vistasDe(msg.id)) {
          v.cols = msg.cols;
          v.rows = msg.rows;
          v.term.resize(msg.cols, msg.rows);
          v.ajustar();
        }
        break;
      case 'eventos':
        if (msg.id === principal.id) pintarEventos(msg.lista, true);
        break;
      case 'evento':
        if (msg.id === principal.id) pintarEventos([msg.ev], false);
        break;
    }
  }

  // ------------------------------------------------------------------------------------------ lista

  function actualizarLista(lista) {
    const antes = sesiones;
    sesiones = new Map(lista.map(s => [s.id, s]));
    avisar(antes, sesiones);

    if (seleccion && !sesiones.has(seleccion)) seleccion = null;
    if (!seleccion && lista.length > 0) {
      // Por defecto, la que más te necesita: la que pide permiso, luego la que te espera, luego la más reciente viva
      const orden = ['permiso', 'esperando', 'trabajando', 'activa', 'reposo'];
      const vivas = lista.filter(s => s.viva).sort((a, b) => orden.indexOf(a.estado) - orden.indexOf(b.estado) || b.inicio - a.inicio);
      seleccion = (vivas[0] || lista[lista.length - 1]).id;
    }
    pintarLista();
    pintarCabecera();
    if (app.dataset.vista === 'mosaico') pintarMosaico();
    if (app.dataset.vista === 'terminal') mirar(seleccion);
  }

  function pintarLista() {
    const ul = $('lista');
    const lista = [...sesiones.values()];
    $('vacio').hidden = lista.length > 0;
    ul.replaceChildren(
      ...lista.map(s =>
        el(
          'li',
          {},
          el(
            'a',
            {
              class: 'sesion',
              href: '#',
              'data-estado': s.estado,
              'aria-current': s.id === seleccion ? 'true' : 'false',
              onclick: e => {
                e.preventDefault();
                elegir(s.id);
              },
            },
            nombreNodo(s),
            el('span', { class: 'estado' }, ESTADOS[s.estado] || s.estado),
            el('span', { class: 'detalle', title: detalle(s) }, detalle(s))
          )
        )
      )
    );

    const n = c => lista.filter(s => s.estado === c).length;
    const abiertas = lista.filter(s => s.viva).length;
    const partes = [`${abiertas} ${abiertas === 1 ? 'abierta' : 'abiertas'}`];
    if (n('permiso')) partes.push(`${n('permiso')} ${n('permiso') === 1 ? 'pide' : 'piden'} permiso`);
    if (n('esperando')) partes.push(`${n('esperando')} te ${n('esperando') === 1 ? 'espera' : 'esperan'}`);
    if (n('trabajando')) partes.push(`${n('trabajando')} ${n('trabajando') === 1 ? 'trabajando' : 'trabajando'}`);
    $('resumen-estado').textContent = partes.join(', ');
    const pendientes = n('permiso') + n('esperando');
    document.title = pendientes ? `(${pendientes}) Sesiones de Claude` : 'Sesiones de Claude';
  }

  function pintarCabecera() {
    const s = sesiones.get(seleccion);
    const titulo = $('barra-titulo');
    if (!s) {
      $('titulo-numero').textContent = '';
      $('titulo-texto').textContent = 'Ninguna sesión elegida';
      $('titulo-carpeta').textContent = '';
      titulo.removeAttribute('data-estado');
    } else {
      const { numero, texto } = partirTitulo(s.titulo);
      $('titulo-numero').textContent = numero;
      $('titulo-texto').textContent = texto;
      $('titulo-carpeta').textContent = s.carpeta;
      $('titulo-carpeta').title = s.carpeta;
      titulo.dataset.estado = s.estado;
    }
    const viva = !!(s && s.viva);
    $('btn-esc').disabled = !viva;
    $('btn-cerrar').hidden = !!s && !viva;
    $('btn-cerrar').disabled = !viva;
    $('btn-olvidar').hidden = !s || viva;
    $('mensaje').disabled = !viva;
    $('escritura').querySelector('button').disabled = !viva;
    principal.term.options.disableStdin = !viva;
    $('sin-mod').hidden = !s || s.conMod || !s.viva;

    const aviso = $('aviso-sesion');
    if (s && s.estado === 'permiso') {
      aviso.hidden = false;
      aviso.dataset.estado = 'permiso';
      aviso.textContent = `${s.actividad}. Contesta en la terminal (por ejemplo, con las flechas e Intro).`;
    } else if (s && !s.viva) {
      aviso.hidden = false;
      aviso.dataset.estado = s.estado;
      aviso.textContent =
        s.estado === 'cerrada'
          ? 'Esta sesión ya no está abierta: ves cómo quedó su terminal. Para seguir, vuelve a abrirla desde PL/SQL Developer.'
          : 'Se ha perdido la conexión con la terminal de esta sesión (la ventana se cerró o el programa terminó de golpe).';
    } else aviso.hidden = true;
  }

  function elegir(id) {
    seleccion = id;
    if (app.dataset.vista !== 'terminal') cambiarVista('terminal');
    pintarLista();
    pintarCabecera();
    mirar(id);
    principal.term.focus();
  }

  // ------------------------------------------------------------------------------------------ actividad

  function pintarEventos(lista, desdeCero) {
    const ol = $('eventos');
    if (desdeCero) ol.replaceChildren();
    for (const ev of lista) {
      const que = el('div', { class: 'que' });
      if (ev.herramienta) {
        const etiqueta = ev.tipo === 'permiso' ? `Permiso para ${ev.herramienta}` : ev.tipo === 'denegada' ? `${ev.herramienta} denegada` : ev.herramienta;
        que.append(el('span', { class: 'herr' }, etiqueta), ev.texto ? el('div', { class: 'texto' }, ev.texto) : '');
      } else {
        que.append(ev.texto || '');
      }
      ol.prepend(el('li', { 'data-tipo': ev.tipo }, el('span', { class: 'hora' }, hora(ev.hora)), que));
    }
    while (ol.children.length > 200) ol.lastChild.remove();
  }

  // ------------------------------------------------------------------------------------------ mosaico

  const teselas = new Map(); // id -> { nodo, vista }

  function pintarMosaico() {
    const cont = $('mosaico');
    const ids = new Set(sesiones.keys());
    for (const [id, t] of teselas) {
      if (!ids.has(id)) {
        quitarVista(id, t.vista);
        t.vista.term.dispose();
        t.nodo.remove();
        teselas.delete(id);
      }
    }
    for (const s of sesiones.values()) {
      let t = teselas.get(s.id);
      if (!t) {
        const mini = el('div', { class: 'mini' });
        const nodo = el('article', {
          class: 'tesela',
          tabindex: '0',
          onclick: () => elegir(s.id),
          onkeydown: e => {
            if (e.key === 'Enter') elegir(s.id);
          },
        });
        nodo.append(el('header'), el('div', { class: 'detalle' }), mini);
        cont.append(nodo);
        const vista = {
          term: crearTerminal(mini, false),
          cols: s.cols,
          rows: s.rows,
          ajustar() {
            const fs = letraPara(this.cols, this.rows, mini.clientWidth - 12, mini.clientHeight - 12, 9);
            if (this.term.options.fontSize !== fs) this.term.options.fontSize = fs;
          },
        };
        t = { nodo, vista };
        teselas.set(s.id, t);
        añadirVista(s.id, vista);
      }
      t.nodo.dataset.estado = s.estado;
      t.nodo.querySelector('header').replaceChildren(nombreNodo(s), el('span', { class: 'estado' }, ESTADOS[s.estado] || s.estado));
      t.nodo.querySelector('.detalle').textContent = detalle(s);
      t.nodo.setAttribute('aria-label', `${s.titulo}: ${ESTADOS[s.estado] || s.estado}`);
    }
    $('mosaico').classList.toggle('vacio-mosaico', sesiones.size === 0);
  }

  function cerrarMosaico() {
    for (const [id, t] of teselas) {
      quitarVista(id, t.vista);
      t.vista.term.dispose();
      t.nodo.remove();
    }
    teselas.clear();
  }

  function cambiarVista(v) {
    app.dataset.vista = v;
    for (const b of document.querySelectorAll('.vistas button')) b.setAttribute('aria-pressed', String(b.dataset.vista === v));
    if (v === 'mosaico') {
      pintarMosaico();
    } else {
      cerrarMosaico();
      mirar(seleccion);
      requestAnimationFrame(() => principal.ajustar());
    }
  }

  for (const b of document.querySelectorAll('.vistas button')) b.addEventListener('click', () => cambiarVista(b.dataset.vista));

  // ------------------------------------------------------------------------------------------ acciones

  $('btn-esc').addEventListener('click', () => {
    if (seleccion) enviar({ t: 'in', id: seleccion, d: '\x1b' });
    principal.term.focus();
  });

  $('btn-actividad').addEventListener('click', () => {
    const mostrar = app.dataset.actividad === 'no';
    app.dataset.actividad = mostrar ? 'si' : 'no';
    $('btn-actividad').setAttribute('aria-pressed', String(mostrar));
    requestAnimationFrame(() => principal.ajustar());
  });

  $('btn-cerrar').addEventListener('click', () => {
    const s = sesiones.get(seleccion);
    if (!s) return;
    if (confirm(`¿Cerrar la sesión «${s.titulo}»?\n\nSe cierra su terminal, también la ventana de Windows Terminal. La conversación queda guardada y se retoma al volver a abrirla desde PL/SQL Developer.`)) {
      enviar({ t: 'cerrar', id: s.id });
    }
  });

  $('btn-olvidar').addEventListener('click', () => {
    if (seleccion) enviar({ t: 'olvidar', id: seleccion });
  });

  // Escribir desde la caja de abajo: se pega en la sesión y se pulsa Intro
  const mensaje = $('mensaje');
  function crecer() {
    mensaje.style.height = 'auto';
    mensaje.style.height = `${Math.min(mensaje.scrollHeight, 160)}px`;
  }
  mensaje.addEventListener('input', crecer);
  mensaje.addEventListener('keydown', e => {
    if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) {
      e.preventDefault();
      $('escritura').requestSubmit();
    }
  });
  $('escritura').addEventListener('submit', e => {
    e.preventDefault();
    const texto = mensaje.value.replace(/\r\n/g, '\n');
    if (!texto.trim() || !principal.id) return;
    // paste() le pone las marcas de pegado si la sesión las usa, para que los saltos de línea no envíen antes de tiempo
    principal.term.paste(texto);
    setTimeout(() => enviar({ t: 'in', id: principal.id, d: '\r' }), 80);
    mensaje.value = '';
    crecer();
  });

  // ------------------------------------------------------------------------------------------ avisos

  const botonAvisos = $('avisos');
  function pintarAvisos() {
    if (!('Notification' in window)) {
      botonAvisos.hidden = true;
      return;
    }
    if (Notification.permission === 'granted') {
      botonAvisos.textContent = 'Avisos activados';
      botonAvisos.disabled = true;
    } else if (Notification.permission === 'denied') {
      botonAvisos.textContent = 'Los avisos están bloqueados en este navegador';
      botonAvisos.disabled = true;
    }
  }
  botonAvisos.addEventListener('click', () => Notification.requestPermission().then(pintarAvisos));
  pintarAvisos();

  function avisar(antes, ahora) {
    for (const s of ahora.values()) {
      const previa = antes.get(s.id);
      if (!previa || previa.estado === s.estado) continue;
      let texto = null;
      if (s.estado === 'permiso') texto = s.actividad || 'Pide permiso';
      else if (s.estado === 'esperando' && previa.estado === 'trabajando') texto = 'Ha terminado y te espera';
      if (!texto) continue;
      const mirandola = !document.hidden && app.dataset.vista === 'terminal' && seleccion === s.id;
      if (mirandola || !('Notification' in window) || Notification.permission !== 'granted') continue;
      const { numero, texto: nombre } = partirTitulo(s.titulo);
      const n = new Notification(numero ? `${numero} ${nombre}` : nombre, { body: texto, tag: s.id, icon: '/icono.svg' });
      n.onclick = () => {
        window.focus();
        elegir(s.id);
        n.close();
      };
    }
  }

  // El texto "hace x min" se actualiza solo
  setInterval(() => {
    if (sesiones.size) pintarLista();
  }, 30 * 1000);

  conectar();
})();
