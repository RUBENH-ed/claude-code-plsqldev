import type { EngineInterface, Register } from 'claude-code'

// Mod del panel de sesiones del plug-in de PL/SQL Developer.
//
// Cuando la sesión corre dentro del anfitrión del panel (anfitrion.js), este le pone CLAUDE_PANEL_SESION,
// CLAUDE_PANEL_CLAVE y CLAUDE_PANEL_URL, y el mod avisa al servidor del panel de lo que hace Claude: empieza un
// turno, usa una herramienta, pide permiso, termina. Así el panel sabe en qué está cada sesión sin tener que
// adivinarlo por lo que sale en la terminal. Sin esas variables no hace nada.
//
// Solo observa: no cambia ninguna decisión ni ninguna llamada. La clave que usa solo sirve para informar del estado
// de esta sesión; no deja escribir en ninguna terminal.

type Evento = Record<string, string | boolean | undefined> & { tipo: string }

let sesion = ''
let clave = ''
let url = ''
let cola: Evento[] = []
let enviandoDesde = 0

function encolar(ev: Evento): void {
  if (!sesion) return
  cola.push(ev)
  if (cola.length > 300) cola = cola.slice(-300)
}

async function vaciar($: EngineInterface): Promise<void> {
  // Si un envío se ha quedado colgado (el servidor no contesta), a los 5 s se deja de esperarlo
  if (!sesion || cola.length === 0 || Date.now() - enviandoDesde < 5000) return
  enviandoDesde = Date.now()
  const lote = cola
  cola = []
  try {
    await $.http.fetch(`${url}/api/sesiones/${sesion}/eventos`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${clave}` },
      body: JSON.stringify({ eventos: lote }),
    })
  } catch {
    // El panel no está en marcha: lo de ahora se pierde, no hace falta guardarlo
  } finally {
    enviandoDesde = 0
  }
}

const corto = (v: unknown, n = 160): string =>
  typeof v === 'string' ? v.replace(/\s+/g, ' ').trim().slice(0, n) : ''

const nombreFichero = (ruta: unknown): string => {
  const r = corto(ruta, 400)
  return r.split(/[\\/]/).filter(Boolean).pop() ?? r
}

/** Qué hace la llamada, en pocas palabras: el comando, el fichero, lo que busca... */
function resumen(herramienta: string, entrada: Record<string, unknown>): string {
  switch (herramienta) {
    case 'Bash':
    case 'PowerShell':
      return corto(entrada.command)
    case 'Read':
    case 'Write':
    case 'Edit':
    case 'MultiEdit':
    case 'NotebookEdit':
      return nombreFichero(entrada.file_path ?? entrada.notebook_path)
    case 'Grep':
    case 'Glob':
      return corto(entrada.pattern)
    case 'WebFetch':
      return corto(entrada.url)
    case 'WebSearch':
      return corto(entrada.query)
    case 'Agent':
    case 'Task':
      return corto(entrada.description)
    default: {
      const primero = Object.values(entrada).find(v => typeof v === 'string')
      return corto(primero)
    }
  }
}

export const register: Register = on => {
  on('session.start', async ($, e, next) => {
    const resultado = await next(e)
    sesion = (await $.env.get('CLAUDE_PANEL_SESION')) ?? ''
    clave = (await $.env.get('CLAUDE_PANEL_CLAVE')) ?? ''
    url = (await $.env.get('CLAUDE_PANEL_URL')) ?? ''
    if (!/^[A-Za-z0-9_-]{8,64}$/.test(sesion) || !clave || !/^http:\/\/127\.0\.0\.1:\d+$/.test(url)) {
      sesion = ''
      return resultado
    }
    encolar({ tipo: 'inicio', sesion: await $.session.id(), modelo: await $.session.model(), carpeta: e.cwd })
    // Los avisos salen cada poco desde aquí, nunca desde dentro de una herramienta o de un permiso: si el panel
    // tardara en contestar, Claude no lo notaría
    $.clock.every(700, () => {
      void vaciar($)
    })
    return resultado
  })

  on('turn.start', async ($, e, next) => {
    encolar({ tipo: 'turno', texto: corto(e.text, 500), subagente: e.agentId !== undefined })
    return next(e)
  })

  on('tool.call', async ($, e, next) => {
    const herramienta = String(e.tool)
    const entrada = e as unknown as Record<string, unknown>
    encolar({ tipo: 'herramienta', herramienta, resumen: resumen(herramienta, entrada), subagente: e.agentId !== undefined })
    const resultado = await next(e)
    encolar({
      tipo: 'herramienta_fin',
      herramienta,
      denegada: resultado.deny !== undefined,
      motivo: resultado.deny !== undefined ? corto(resultado.deny, 200) : undefined,
    })
    return resultado
  }).catch(($, e, next) => next(e)) // si algo de aquí fallara, la llamada sigue como si el mod no estuviera

  // Si la decisión es preguntar a la persona, Claude se queda esperando: el panel lo marca en rojo
  on('tool.check', async ($, e, next) => {
    const resultado = await next(e)
    if (e.tool_use_id !== undefined && resultado.decision === 'ask' && e.approver === 'person') {
      const herramienta = String(e.tool)
      const entrada = (e.input ?? {}) as Record<string, unknown>
      encolar({ tipo: 'permiso', herramienta, resumen: resumen(herramienta, entrada), subagente: e.agentId !== undefined })
    }
    return resultado
  }).catch(($, e, next) => next(e)) // nunca cambia la decisión: si falla, queda la de debajo

  on('turn.complete', async ($, e, next) => {
    encolar({ tipo: 'turno_fin', motivo: e.reason, texto: corto(e.answer, 600), subagente: e.agentId !== undefined })
    return next(e)
  })

  on('session.end', async ($, e, next) => {
    // Con /clear la sesión sigue, con una conversación nueva
    encolar(e.reason === 'clear' ? { tipo: 'inicio', motivo: 'clear' } : { tipo: 'fin' })
    enviandoDesde = 0
    // Al salir no hay temporizador que lo mande: se manda ya, esperando como mucho un segundo
    await Promise.race([vaciar($), $.clock.sleep(1000)])
    return next(e)
  })
}
