'use strict';
// Panel de sesiones de Claude: lista, terminal en directo, actividad, vista de todas las sesiones, sesiones nuevas
// y preferencias (disposición, colores, ventanas de terminal).
/* global Terminal, Unicode11Addon, WebLinksAddon */

(() => {
  const $ = id => document.getElementById(id);
  const html = document.documentElement;
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

  /** @type {Map<string, any>} */
  let sesiones = new Map();
  let seleccion = null;
  let pendienteDeEnfocar = null; // sesión que hay que enseñar en cuanto aparezca (la abre el plug-in o el panel)
  let historial = [];
  let ws = null;
  let conectado = false;

  // ------------------------------------------------------------------------------------------ preferencias

  const COLORES = [
    ['fondo', 'Fondo'],
    ['panel', 'Paneles'],
    ['texto', 'Texto'],
    ['acento', 'Acento (foco y selección)'],
    ['terminal', 'Fondo de la terminal'],
    ['terminal-texto', 'Texto de la terminal'],
    ['trabajando', 'Piloto: trabajando'],
    ['permiso', 'Piloto: pide permiso'],
    ['espera', 'Piloto: te espera'],
  ];
  const DEFECTO = {
    tema: 'pizarra',
    lista: 'izquierda',
    actividad: 'derecha',
    listaPlegada: false,
    actividadPlegada: false,
    caja: true,
    letra: 14,
    ventanas: 'mostrar',
    mensajes: 'auto',
    colores: {},
  };
  let prefs = { ...DEFECTO };
  try {
    prefs = normalizar(JSON.parse(localStorage.getItem('panel-prefs') || '{}'));
  } catch {
    // sin preferencias en este navegador
  }

  function normalizar(p) {
    const r = { ...DEFECTO, ...(p && typeof p === 'object' ? p : {}) };
    const elige = (v, ...validos) => (validos.includes(v) ? v : validos[0]);
    r.tema = elige(r.tema, 'pizarra', 'noche', 'claro', 'contraste');
    r.lista = elige(r.lista, 'izquierda', 'arriba', 'derecha');
    r.actividad = elige(r.actividad, 'derecha', 'abajo', 'izquierda');
    r.ventanas = elige(r.ventanas, 'mostrar', 'ocultar');
    r.mensajes = elige(r.mensajes, 'auto', 'detallados', 'breves');
    r.listaPlegada = !!r.listaPlegada;
    r.actividadPlegada = !!r.actividadPlegada;
    r.caja = r.caja !== false;
    r.letra = Math.min(20, Math.max(10, Number(r.letra) || 14));
    const colores = {};
    for (const [k] of COLORES) if (/^#[0-9a-f]{6}$/i.test(r.colores?.[k] || '')) colores[k] = r.colores[k].toLowerCase();
    r.colores = colores;
    return r;
  }

  function aplicarPrefs() {
    html.dataset.tema = prefs.tema;
    html.dataset.lista = prefs.lista;
    html.dataset.actividad = prefs.actividad;
    html.dataset.listaPlegada = prefs.listaPlegada ? 'si' : 'no';
    html.dataset.actividadPlegada = prefs.actividadPlegada ? 'si' : 'no';
    html.dataset.caja = prefs.caja ? 'si' : 'no';
    for (const [k] of COLORES) {
      if (prefs.colores[k]) html.style.setProperty(`--${k}`, prefs.colores[k]);
      else html.style.removeProperty(`--${k}`);
    }
    const plegarLista = $('btn-plegar-lista');
    plegarLista.setAttribute('aria-expanded', String(!prefs.listaPlegada));
    const tLista = prefs.listaPlegada ? 'Desplegar la lista de sesiones' : 'Plegar la lista de sesiones';
    plegarLista.title = tLista;
    plegarLista.querySelector('.oculto').textContent = tLista;
    const plegarAct = $('btn-plegar-actividad');
    plegarAct.setAttribute('aria-expanded', String(!prefs.actividadPlegada));
    const tAct = prefs.actividadPlegada ? 'Desplegar la actividad' : 'Plegar la actividad';
    plegarAct.title = tAct;
    plegarAct.querySelector('.oculto').textContent = tAct;
    $('btn-ampliar').setAttribute('aria-pressed', String(prefs.listaPlegada && prefs.actividadPlegada));
    if (sesiones.size) pintarLista(); // plegada o no, cambia cómo se nombra cada sesión
    aplicarTemaTerminales();
    // La barra de título de la ventana de Edge toma el color del fondo
    document.querySelector('meta[name="theme-color"]').content = aHex(css('--fondo'));
    requestAnimationFrame(() => {
      principal.ajustar();
      for (const t of teselas.values()) t.vista.ajustar();
    });
  }

  let guardado = null;
  function cambiarPrefs(cambios) {
    prefs = normalizar({ ...prefs, ...cambios });
    aplicarPrefs();
    try {
      localStorage.setItem('panel-prefs', JSON.stringify(prefs));
    } catch {
      // el navegador no deja guardar: se guardan igualmente en el equipo
    }
    clearTimeout(guardado);
    guardado = setTimeout(() => enviar({ t: 'prefs', prefs }), 300);
  }

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
    return `hace ${Math.round(m / 60)} h`;
  }

  const FORMATO_HORA = new Intl.DateTimeFormat('es-ES', { hour: '2-digit', minute: '2-digit' });
  const hora = ms => FORMATO_HORA.format(new Date(ms));

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
    for (const h of hijos) if (h !== null && h !== undefined && h !== '') e.append(h);
    return e;
  }

  function nombreNodo(s) {
    const { numero, texto } = partirTitulo(s.titulo);
    return el('span', { class: 'nombre' }, numero ? el('span', { class: 'numero' }, numero) : null, texto);
  }

  /** Nombre corto para la lista plegada: el número de expediente o el principio del título. */
  function corto(s) {
    const { numero, texto } = partirTitulo(s.titulo);
    return numero ? numero.slice(-4) : texto.slice(0, 4);
  }

  const anunciar = (() => {
    let t = null;
    return texto => {
      clearTimeout(t);
      t = setTimeout(() => ($('anuncios').textContent = texto), 50);
    };
  })();

  // ------------------------------------------------------------------------------------------ terminales

  const vistas = new Map(); // id -> Set<vista>

  function css(nombre) {
    return getComputedStyle(html).getPropertyValue(nombre).trim();
  }

  /** Colores de la terminal a partir de los del tema (y los que haya cambiado la persona). */
  function temaTerminal() {
    const claro = prefs.tema === 'claro';
    const ansi = claro
      ? { black: '#1b2330', red: '#c6372c', green: '#1d7f57', yellow: '#8a5d00', blue: '#2f6db5', magenta: '#8a46b8', cyan: '#147c8c', white: '#d0d6df',
          brightBlack: '#5f6b7e', brightRed: '#e0473b', brightGreen: '#23966a', brightYellow: '#a8700c', brightBlue: '#3c80cf', brightMagenta: '#a05cd0', brightCyan: '#1b93a5', brightWhite: '#ffffff' }
      : { black: '#1a2130', red: '#f2665c', green: '#4fc79a', yellow: '#e9b44c', blue: '#7fa8e0', magenta: '#c49be8', cyan: '#5fc4d4', white: '#d5dbe5',
          brightBlack: '#66728a', brightRed: '#ff8a80', brightGreen: '#7fe0b8', brightYellow: '#f5cd7a', brightBlue: '#a6c4f0', brightMagenta: '#d9baf5', brightCyan: '#8edbe6', brightWhite: '#ffffff' };
    const fondo = css('--terminal') || '#131924';
    const texto = css('--terminal-texto') || '#e6ebf2';
    return { ...ansi, background: fondo, foreground: texto, cursor: texto, cursorAccent: fondo, selectionBackground: `${(css('--acento') || '#7fa8e0').slice(0, 7)}55` };
  }

  function aplicarTemaTerminales() {
    const tema = temaTerminal();
    principal.term.options.theme = tema;
    for (const t of teselas.values()) t.vista.term.options.theme = tema;
  }

  const MONO = css('--mono') || 'monospace';

  /** Medida de una celda (ancho y alto por cada punto de letra) con la letra de la terminal. */
  const CELDA = (() => {
    const c = document.createElement('canvas').getContext('2d');
    c.font = `100px ${MONO}`;
    return { ancho: c.measureText('W').width / 100, alto: 1.2 };
  })();

  /** Letra para que cols x rows quepan en el hueco (sesión con ventana: manda el tamaño de su ventana). */
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
      fontSize: prefs.letra,
      theme: temaTerminal(),
      scrollback: 3000,
      allowProposedApi: true,
      disableStdin: !interactiva,
      cursorBlink: interactiva,
      cursorInactiveStyle: 'outline',
      screenReaderMode: false,
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

  const vistasDe = id => vistas.get(id) || new Set();

  // Terminal principal: una sola, que cambia de sesión
  const principal = {
    id: null,
    term: crearTerminal($('terminal'), true),
    cols: 120,
    rows: 30,
    ajustar() {
      const marco = $('marco');
      const ancho = marco.clientWidth - 18;
      const alto = marco.clientHeight - 18;
      if (ancho <= 0 || alto <= 0) return;
      const s = sesiones.get(this.id);
      if (s && s.sinVentana && s.viva) {
        // Sin ventana: la terminal toma el tamaño del panel, con la letra elegida
        if (this.term.options.fontSize !== prefs.letra) this.term.options.fontSize = prefs.letra;
        const cols = Math.max(20, Math.floor((ancho - 4) / (CELDA.ancho * prefs.letra)));
        const rows = Math.max(5, Math.floor(alto / (CELDA.alto * prefs.letra)));
        if (cols !== s.cols || rows !== s.rows) pedirTamaño(this.id, cols, rows);
        return;
      }
      const fs = letraPara(this.cols, this.rows, ancho, alto, prefs.letra);
      if (this.term.options.fontSize !== fs) this.term.options.fontSize = fs;
    },
  };
  principal.term.onData(d => {
    if (principal.id) enviar({ t: 'in', id: principal.id, d });
  });
  // Tab y Mayús+Tab son de la sesión (Claude los usa): para salir de la terminal con el teclado, F6 va a la lista de
  // sesiones y Mayús+F6 a los botones de la barra
  principal.term.attachCustomKeyEventHandler(e => {
    if (e.type !== 'keydown' || e.key !== 'F6' || e.ctrlKey || e.altKey || e.metaKey) return true;
    e.preventDefault();
    if (e.shiftKey) document.querySelector('.barra-acciones button:not([disabled]):not([hidden])')?.focus();
    else (document.querySelector('.sesion[aria-current="true"]') || document.querySelector('.sesion') || $('btn-nueva')).focus();
    return false;
  });
  new ResizeObserver(() => principal.ajustar()).observe($('marco'));

  let tamañoPendiente = null;
  function pedirTamaño(id, cols, rows) {
    clearTimeout(tamañoPendiente);
    tamañoPendiente = setTimeout(() => enviar({ t: 'tam', id, cols, rows }), 120);
  }

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
      case 'prefs':
        // Las guardadas en el equipo mandan sobre las de este navegador (si hay alguna)
        if (msg.prefs && Object.keys(msg.prefs).length > 0) {
          prefs = normalizar(msg.prefs);
          try {
            localStorage.setItem('panel-prefs', JSON.stringify(prefs));
          } catch {
            // nada
          }
          aplicarPrefs();
          if ($('dlg-ajustes').open) pintarAjustes();
        }
        break;
      case 'historial':
        historial = Array.isArray(msg.lista) ? msg.lista : [];
        break;
      case 'enfocar':
        enfocar(msg.id);
        break;
      case 'nueva':
        resultadoNueva(msg);
        break;
      case 'asignar':
        resultadoAsignar(msg);
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
      case 'size': {
        const s = sesiones.get(msg.id);
        if (s) {
          s.cols = msg.cols;
          s.rows = msg.rows;
        }
        for (const v of vistasDe(msg.id)) {
          v.cols = msg.cols;
          v.rows = msg.rows;
          v.term.resize(msg.cols, msg.rows);
          v.ajustar();
        }
        break;
      }
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

    if (pendienteDeEnfocar && sesiones.has(pendienteDeEnfocar.id)) {
      const id = pendienteDeEnfocar.id;
      pendienteDeEnfocar = null;
      elegir(id, false);
      return;
    }
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
    principal.ajustar();
  }

  function pintarLista() {
    const ul = $('lista');
    const lista = [...sesiones.values()];
    $('vacio').hidden = lista.length > 0;
    const enfocado = document.activeElement && document.activeElement.closest && document.activeElement.closest('.sesion');
    const idEnfocado = enfocado ? enfocado.dataset.id : null;
    ul.replaceChildren(
      ...lista.map(s => {
        const estado = ESTADOS[s.estado] || s.estado;
        return el(
          'li',
          {},
          el(
            'a',
            {
              class: 'sesion',
              href: `#${s.id}`,
              'data-id': s.id,
              'data-estado': s.estado,
              'aria-current': s.id === seleccion ? 'true' : 'false',
              title: `${s.titulo}: ${estado}${detalle(s) ? `. ${detalle(s)}` : ''}`,
              // Plegada, el estado no puede ir solo en el color: el nombre accesible lo dice entero
              'aria-label': prefs.listaPlegada ? `${s.titulo}: ${estado}` : null,
              onclick: e => {
                e.preventDefault();
                elegir(s.id, true);
              },
            },
            nombreNodo(s),
            el('span', { class: 'estado' }, estado),
            el('span', { class: 'detalle' }, detalle(s)),
            el('span', { class: 'corto', 'aria-hidden': 'true' }, corto(s), s.estado === 'permiso' ? el('b', { class: 'marca' }, '!') : null)
          )
        );
      })
    );
    if (idEnfocado) ul.querySelector(`.sesion[data-id="${CSS.escape(idEnfocado)}"]`)?.focus();

    const n = c => lista.filter(s => s.estado === c).length;
    const abiertas = lista.filter(s => s.viva).length;
    const partes = [`${abiertas} ${abiertas === 1 ? 'abierta' : 'abiertas'}`];
    if (n('permiso')) partes.push(`${n('permiso')} ${n('permiso') === 1 ? 'pide' : 'piden'} permiso`);
    if (n('esperando')) partes.push(`${n('esperando')} te ${n('esperando') === 1 ? 'espera' : 'esperan'}`);
    if (n('trabajando')) partes.push(`${n('trabajando')} trabajando`);
    $('resumen-estado').textContent = partes.join(', ');
    const pendientes = n('permiso') + n('esperando');
    document.title = pendientes ? `(${pendientes}) Sesiones de Claude` : 'Sesiones de Claude';
  }

  function pintarCabecera() {
    const s = sesiones.get(seleccion);
    const titulo = $('barra-titulo');
    if (!s) {
      $('titulo-numero').textContent = '';
      $('titulo-texto').textContent = sesiones.size ? 'Elige una sesión' : 'Ninguna sesión abierta';
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
    $('btn-pegar').disabled = !viva;
    $('btn-asignar').disabled = !viva || asignando;
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
      aviso.textContent = `${s.actividad}. Contesta en la terminal (con las flechas e Intro).`;
    } else if (s && !s.viva) {
      aviso.hidden = false;
      aviso.dataset.estado = s.estado;
      aviso.textContent =
        s.estado === 'cerrada'
          ? 'Esta sesión ya no está abierta: ves cómo quedó su terminal. Para seguir, vuelve a abrirla desde PL/SQL Developer o con Nueva sesión.'
          : 'Se ha perdido la conexión con la terminal de esta sesión (la ventana se cerró o el programa terminó de golpe).';
    } else aviso.hidden = true;
  }

  function elegir(id, foco) {
    seleccion = id;
    // La sesión elegida queda en la dirección (#id): al recargar, el panel vuelve a ella
    if (location.hash !== `#${id}`) history.replaceState(null, '', `#${id}`);
    if (app.dataset.vista !== 'terminal') cambiarVista('terminal');
    pintarLista();
    pintarCabecera();
    mirar(id);
    principal.ajustar();
    if (foco) principal.term.focus();
  }

  /** El plug-in pide enseñar una sesión (recién abierta o reutilizada): se elige en cuanto aparece en la lista. */
  function enfocar(id) {
    if (sesiones.has(id)) elegir(id, true);
    else pendienteDeEnfocar = { id, hasta: Date.now() + 20000 };
    try {
      window.focus();
    } catch {
      // el navegador decide si deja traer la ventana al frente
    }
  }

  // ------------------------------------------------------------------------------------------ actividad

  function pintarEventos(lista, desdeCero) {
    const ol = $('eventos');
    if (desdeCero) ol.replaceChildren();
    for (const ev of lista) {
      const que = el('div', { class: 'que' });
      if (ev.herramienta) {
        const etiqueta = ev.tipo === 'permiso' ? `Permiso para ${ev.herramienta}` : ev.tipo === 'denegada' ? `${ev.herramienta} denegada` : ev.herramienta;
        que.append(el('span', { class: 'herr' }, etiqueta));
        if (ev.texto) que.append(el('div', { class: 'texto' }, ev.texto));
      } else if (ev.tipo === 'mensaje' || ev.tipo === 'respuesta') {
        que.append(el('span', { class: 'quien' }, ev.tipo === 'mensaje' ? 'Tú:' : 'Claude:'), ev.texto || '');
      } else {
        que.append(ev.texto || '');
      }
      ol.prepend(el('li', { 'data-tipo': ev.tipo }, el('time', { class: 'hora', datetime: new Date(ev.hora).toISOString() }, hora(ev.hora)), que));
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
        // La miniatura es solo para mirar: inert la deja fuera del teclado y de los lectores de pantalla
        const mini = el('div', { class: 'mini' });
        mini.inert = true;
        const nodo = el('article', { class: 'tesela' });
        nodo.append(el('button', { type: 'button', class: 'tesela-abrir', onclick: () => elegir(s.id, true) }), el('div', { class: 'detalle' }), mini);
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
      t.nodo.querySelector('.tesela-abrir').replaceChildren(nombreNodo(s), el('span', { class: 'estado' }, ESTADOS[s.estado] || s.estado));
      t.nodo.querySelector('.detalle').textContent = detalle(s);
    }
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

  // ------------------------------------------------------------------------------------------ acciones de la barra

  $('btn-esc').addEventListener('click', () => {
    if (seleccion) enviar({ t: 'in', id: seleccion, d: '\x1b' });
    principal.term.focus();
  });

  // Pegar: muchas veces Ctrl+V no llega a la terminal (o el foco está en otro sitio). Si lo copiado es texto, se pega
  // con las marcas de pegado; si no hay texto (una captura, por ejemplo), Alt+V para que Claude lea él mismo la imagen
  // del portapapeles de Windows, como hace al pulsarlo en su ventana.
  function avisoBreve(texto) {
    const r = $('resumen-estado');
    r.textContent = texto;
    anunciar(texto);
    clearTimeout(avisoBreve.t);
    avisoBreve.t = setTimeout(() => pintarLista(), 6000);
  }

  $('btn-pegar').addEventListener('click', async () => {
    const id = principal.id;
    const s = sesiones.get(id);
    if (!s || !s.viva) return;
    let texto = null;
    try {
      texto = await navigator.clipboard.readText();
    } catch {
      texto = null; // el navegador no deja leer el portapapeles
    }
    if (texto === null) {
      avisoBreve('El navegador no me deja leer el portapapeles: pega con Ctrl+V en la caja de abajo.');
      mensaje.focus();
      return;
    }
    if (texto) {
      principal.term.paste(texto.replace(/\r\n/g, '\n'));
      avisoBreve(`Pegado (${texto.length} caracteres). Revísalo y pulsa Intro.`);
    } else {
      enviar({ t: 'in', id, d: '\x1bv' });
      avisoBreve('Sin texto copiado: le he pedido a Claude que pegue la imagen del portapapeles (Alt+V).');
    }
    principal.term.focus();
  });

  // Asignar expediente: los cuadros (número, título, carpeta) los enseña PL/SQL Developer
  let asignando = false;
  $('btn-asignar').addEventListener('click', () => {
    const s = sesiones.get(seleccion);
    if (!s || !s.viva) return;
    asignando = true;
    $('btn-asignar').disabled = true;
    $('btn-asignar').textContent = 'Esperando a PL/SQL Developer…';
    enviar({ t: 'asignar', id: s.id });
  });

  function resultadoAsignar(r) {
    asignando = false;
    $('btn-asignar').textContent = 'Asignar expediente';
    pintarCabecera();
    if (r.ok) avisoBreve('Contesta en PL/SQL Developer: número, título y carpeta del expediente o tema.');
    else alert(r.error || 'No se ha podido asignar el expediente.');
  }

  $('btn-cerrar').addEventListener('click', () => {
    const s = sesiones.get(seleccion);
    if (!s) return;
    const ventana = s.sinVentana ? '' : ' También se cierra su ventana de Windows Terminal.';
    if (confirm(`¿Cerrar la sesión «${s.titulo}»?\n\nSe cierra su terminal.${ventana} La conversación queda guardada y se retoma al volver a abrirla.`)) {
      enviar({ t: 'cerrar', id: s.id });
    }
  });

  $('btn-olvidar').addEventListener('click', () => {
    if (seleccion) enviar({ t: 'olvidar', id: seleccion });
  });

  $('btn-plegar-lista').addEventListener('click', () => cambiarPrefs({ listaPlegada: !prefs.listaPlegada }));
  $('btn-plegar-actividad').addEventListener('click', () => cambiarPrefs({ actividadPlegada: !prefs.actividadPlegada }));
  $('btn-ampliar').addEventListener('click', () => {
    const ampliada = prefs.listaPlegada && prefs.actividadPlegada;
    cambiarPrefs({ listaPlegada: !ampliada, actividadPlegada: !ampliada });
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
    const id = principal.id;
    setTimeout(() => enviar({ t: 'in', id, d: '\r' }), 80);
    mensaje.value = '';
    crecer();
  });

  // ------------------------------------------------------------------------------------------ nueva sesión

  const dlgNueva = $('dlg-nueva');

  function nombreCarpeta(c) {
    return c.split(/[\\/]/).filter(Boolean).pop() || c;
  }

  function abrirNueva() {
    const cont = $('opciones-carpeta');
    const vistasCarpetas = new Set();
    const opciones = [];
    // Primero el expediente de la sesión elegida, luego los recientes
    const elegida = sesiones.get(seleccion);
    // Expedientes («1234567 - Título») y temas (el servidor los marca como proyecto en el historial)
    const proyectos = new Set(historial.filter(h => h.proyecto).map(h => h.carpeta.toLowerCase()));
    for (const c of [elegida?.carpeta, ...historial.map(h => h.carpeta)]) {
      if (!c || vistasCarpetas.has(c.toLowerCase()) || !(/^\d{7} - ./.test(nombreCarpeta(c)) || proyectos.has(c.toLowerCase()))) continue;
      vistasCarpetas.add(c.toLowerCase());
      opciones.push(c);
    }
    cont.replaceChildren(
      ...opciones.slice(0, 12).map((c, i) =>
        el(
          'label',
          { class: 'opcion' },
          el('input', { type: 'radio', name: 'carpeta', value: c, checked: i === 0 }),
          el('span', {}, nombreCarpeta(c), el('small', {}, c))
        )
      ),
      el(
        'label',
        { class: 'opcion' },
        el('input', { type: 'radio', name: 'carpeta', value: '', checked: opciones.length === 0 }),
        el('span', {}, 'Carpeta general', el('small', {}, 'Fuera de cualquier expediente (CarpetaTrabajo del plug-in)'))
      )
    );
    $('carpeta-otra').value = '';
    $('nueva-error').hidden = true;
    $('nueva-abrir').disabled = false;
    $('nueva-abrir').textContent = 'Abrir sesión';
    dlgNueva.showModal();
  }

  $('btn-nueva').addEventListener('click', abrirNueva);
  $('nueva-cancelar').addEventListener('click', () => dlgNueva.close());
  $('carpeta-otra').addEventListener('input', () => {
    const otra = dlgNueva.querySelector('input[name="carpeta"][value="__otra__"]');
    if (otra) otra.checked = true;
  });

  $('form-nueva').addEventListener('submit', e => {
    e.preventDefault();
    const elegido = dlgNueva.querySelector('input[name="carpeta"]:checked');
    let carpeta = elegido ? elegido.value : '';
    if (carpeta === '__otra__') carpeta = $('carpeta-otra').value.trim().replace(/^"|"$/g, '');
    if (elegido && elegido.value === '__otra__' && !carpeta) {
      mostrarErrorNueva('Escribe la ruta de la carpeta del expediente.');
      $('carpeta-otra').focus();
      return;
    }
    $('nueva-error').hidden = true;
    $('nueva-abrir').disabled = true;
    $('nueva-abrir').textContent = 'Abriendo…';
    enviar({ t: 'nueva', carpeta });
  });

  function mostrarErrorNueva(texto) {
    const p = $('nueva-error');
    p.textContent = texto;
    p.hidden = false;
  }

  function resultadoNueva(r) {
    if (!dlgNueva.open) return;
    $('nueva-abrir').disabled = false;
    $('nueva-abrir').textContent = 'Abrir sesión';
    if (r.ok) {
      dlgNueva.close();
      $('resumen-estado').textContent = 'Abriendo la sesión…';
    } else {
      mostrarErrorNueva(r.error || 'No se ha podido abrir la sesión.');
    }
  }

  // ------------------------------------------------------------------------------------------ personalizar

  const dlgAjustes = $('dlg-ajustes');
  const formAjustes = $('form-ajustes');

  function pintarAjustes() {
    for (const nombre of ['lista', 'actividad', 'ventanas', 'mensajes', 'tema']) {
      const r = formAjustes.querySelector(`input[name="${nombre}"][value="${prefs[nombre]}"]`);
      if (r) r.checked = true;
    }
    formAjustes.elements.letra.value = prefs.letra;
    $('letra-valor').textContent = `${prefs.letra} px`;
    formAjustes.elements.caja.checked = prefs.caja;
    const cont = $('colores');
    cont.replaceChildren(
      ...COLORES.map(([k, etiqueta]) => {
        const valor = prefs.colores[k] || aHex(css(`--${k}`));
        return el(
          'label',
          { class: 'color' },
          el('input', { type: 'color', 'data-color': k, value: valor }),
          el('span', {}, etiqueta)
        );
      })
    );
  }

  /** Color calculado del tema en #rrggbb (para el selector de color). */
  function aHex(valor) {
    if (/^#[0-9a-f]{6}$/i.test(valor)) return valor.toLowerCase();
    const c = document.createElement('canvas').getContext('2d');
    c.fillStyle = '#000';
    c.fillStyle = valor;
    const v = c.fillStyle;
    return /^#[0-9a-f]{6}$/i.test(v) ? v : '#000000';
  }

  $('btn-ajustes').addEventListener('click', () => {
    pintarAjustes();
    dlgAjustes.showModal();
  });

  formAjustes.addEventListener('change', e => {
    const t = e.target;
    if (t.name === 'tema') cambiarPrefs({ tema: t.value, colores: {} });
    else if (['lista', 'actividad', 'ventanas', 'mensajes'].includes(t.name)) cambiarPrefs({ [t.name]: t.value });
    else if (t.name === 'caja') cambiarPrefs({ caja: t.checked });
    if (t.name === 'tema') pintarAjustes();
  });
  formAjustes.addEventListener('input', e => {
    const t = e.target;
    if (t.name === 'letra') {
      $('letra-valor').textContent = `${t.value} px`;
      cambiarPrefs({ letra: Number(t.value) });
    } else if (t.dataset.color) {
      cambiarPrefs({ colores: { ...prefs.colores, [t.dataset.color]: t.value } });
    }
  });
  $('colores-restablecer').addEventListener('click', () => {
    cambiarPrefs({ colores: {} });
    pintarAjustes();
  });
  $('ajustes-restablecer').addEventListener('click', () => {
    cambiarPrefs({ ...DEFECTO, colores: {} });
    pintarAjustes();
  });

  // ------------------------------------------------------------------------------------------ ayuda

  $('btn-ayuda').addEventListener('click', () => $('dlg-ayuda').showModal());

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
      const { numero, texto: nombre } = partirTitulo(s.titulo);
      anunciar(`${numero || nombre}: ${texto}`);
      const mirandola = !document.hidden && app.dataset.vista === 'terminal' && seleccion === s.id;
      if (mirandola || !('Notification' in window) || Notification.permission !== 'granted') continue;
      const n = new Notification(numero ? `${numero} ${nombre}` : nombre, { body: texto, tag: s.id, icon: '/icono.svg' });
      n.onclick = () => {
        window.focus();
        elegir(s.id, true);
        n.close();
      };
    }
  }

  // El texto "hace x min" se actualiza solo; una sesión pendiente que no llega se deja de esperar
  setInterval(() => {
    if (pendienteDeEnfocar && Date.now() > pendienteDeEnfocar.hasta) pendienteDeEnfocar = null;
    if (sesiones.size) pintarLista();
  }, 30 * 1000);

  // Enlace de entrada con ?sesion=<id>: el plug-in abrió el panel para enseñar esa sesión
  // (o con #<id>: la sesión que se estaba mirando antes de recargar)
  const pedida = new URLSearchParams(location.search).get('sesion') || location.hash.slice(1);
  if (pedida && /^[A-Za-z0-9_-]{8,64}$/.test(pedida)) {
    pendienteDeEnfocar = { id: pedida, hasta: Date.now() + 20000 };
    history.replaceState(null, '', `/#${pedida}`);
  }

  aplicarPrefs();
  conectar();
})();
