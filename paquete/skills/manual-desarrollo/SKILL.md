---
name: manual-desarrollo
description: >-
  Genera el "Manual de Desarrollo" de EDISA/LIBRA: el documento que se entrega al
  CLIENTE explicando, a nivel de usuario, los cambios realizados en un desarrollo.
  Úsala SIEMPRE que el usuario quiera crear, generar o redactar un manual de
  desarrollo, un "manual de usuario del desarrollo", o documentar para el cliente
  cómo funciona un desarrollo terminado — aunque no diga literalmente "manual".
  Dispara con "hazme el manual de este desarrollo", "documenta esto para el
  cliente", "genera el manual del expediente X", al pasar un A10 (necesidades del
  cliente) y/o un D10 (solución técnica) pidiendo documentación, o al aportar
  capturas de pantalla de LIBRA junto a un expediente y un título. Produce un
  .docx idéntico a la plantilla oficial (portada LIBRA, índice automático,
  títulos corporativos y página de cierre).
license: Proprietary
---

# Manual de Desarrollo (EDISA / LIBRA)

## Qué es y cómo funciona

El **Manual de Desarrollo** es el documento que EDISA entrega al **cliente** al
finalizar un desarrollo de LIBRA. Explica qué se ha hecho y cómo se usa, con
capturas de pantalla. **Lo lee el cliente**, no un técnico: la redacción debe ser
a nivel de usuario.

> **NORMA ABSOLUTA**: el manual **no se genera nunca** sin que el usuario haya
> pasado los cambios que ha realizado. Siempre, todos, sin excepciones. Ver
> "Los cambios realizados los pasa SIEMPRE el usuario".

Esta skill **no reconstruye el documento desde cero**. Lleva la plantilla oficial
(`assets/Manual_plantilla.docx`: portada LIBRA con título, índice, estilos
Título 1–4, cabecera/pie corporativos y página de cierre con las oficinas) y el
script `scripts/build_manual.py` inserta el contenido redactado en la zona
central, regenerando el índice. La salida es `.docx`.

## Modos de entrada

### A) Documentos sueltos (chat normal)
El usuario adjunta el A10/D10 (PDF o Word) y las capturas directamente en la
conversación. Es el modo por defecto.

### B) Carpeta de proyecto (típico en Cowork)
El usuario pasa la **ruta de una carpeta** llamada `{expediente} - {nombre}`
(p.ej. `1813283 - INTEGRACION CON PAYHAWK`), con la misma estructura que usa la
skill del A18:

```
{expediente} - {nombre}/
├── cfstd/
├── cfcliente/
├── doc/            <- AQUÍ están el A10 y el D10, y AQUÍ se guarda el manual
├── instalacion/
└── test/
```

- **Expediente y título** salen del nombre de la carpeta.
- **Busca el A10 y el D10 en la subcarpeta `doc/`**:
  - Ficheros con `d10` en el nombre (p.ej. `d10-integración_con_payhawk_v4-firmado.pdf`) → D10.
  - Ficheros con `a10` en el nombre → A10. **Ojo**: el A10 a veces va **sin
    prefijo** (p.ej. `integración_con_payhawk_v2-firmado.pdf`); si un PDF de
    `doc/` no se identifica por el nombre, ábrelo y mira la cabecera: el A10
    dice "SOLICITUD DE CAMBIO-DESARROLLO" y el D10 "ANÁLISIS REQUISITOS
    ESPECÍFICOS".
  - Si hay varias versiones (`_v2`, `_v4`...), usa **la de número más alto**.
  - Si tras buscar en `doc/` sigue faltando el A10 (o el D10 cuando existe),
    aplica "Contexto completo obligatorio" y pídeselo al usuario.
- **El manual generado se guarda en esa misma carpeta `doc/`**.

## Entradas (se combinan; reúne lo que falte)

1. **Expediente y título**: p.ej. `1813283 - INTEGRACION CON PAYHAWK`. Suelen
   venir en los propios documentos A10/D10 o los da el usuario. El expediente
   tiene 7 dígitos; el título en MAYÚSCULAS y sin tildes.
2. **A10** (Solicitud de cambio-desarrollo): las **necesidades del cliente**.
   Fuente principal para entender qué quería el cliente y con qué vocabulario
   funcional lo expresa.
3. **D10** (Análisis de requisitos específicos): la solución técnica. **Lo que
   el desarrollador ha construido es EXCLUSIVAMENTE lo que aparece bajo el
   título "SOLUCIÓN PROPUESTA"**: esa sección es la base del manual. Ignora
   como contenido el resto del D10 (participantes, requisitos solicitados —que
   suele ser el A10 incrustado—, valoración económica, aceptación/firmas).
   - El D10 a menudo **hace referencia a cosas del A10** ("según la estructura
     definida en el A10", "el formato indicado en el A10"...). Cuando pase,
     **ve a buscar esa parte concreta en el A10** y úsala para completar la
     redacción; no dejes la referencia colgando ni te la inventes.
4. **Cambios realizados por el desarrollador** (OBLIGATORIO, los pasa el
   usuario). Ver "Los cambios realizados los pasa SIEMPRE el usuario".
5. **Capturas de pantalla** de los programas de LIBRA. Las aporta el usuario.
   Si no ha adjuntado ninguna, **pregúntale** si quiere añadirlas y en qué
   secciones van, o si genera el manual sin capturas (podrá insertarlas él
   después). No inventes rutas de imagen.

### Los cambios realizados los pasa SIEMPRE el usuario

**NORMA ABSOLUTA: nunca, bajo ninguna circunstancia, generes el manual sin que
el usuario haya pasado los cambios que ha realizado.** El A10 y el D10 dicen lo
que el cliente *pidió* y lo que se *propuso*, no lo que finalmente se hizo. El
manual documenta lo realmente implementado, y eso solo lo sabe el desarrollador.

- El usuario tiene que pasar **todos** los cambios, **siempre**. No hay
  excepciones: ni "el D10 ya lo explica todo", ni "es un desarrollo pequeño",
  ni "ya te lo conté antes", ni la prisa del usuario.
- La única variante admitida es que el usuario **indique explícitamente** que
  algo de lo previsto **no se ha hecho** o **se ha cambiado** respecto al
  D10/A10. Esa indicación forma parte de los cambios pasados: documenta lo
  implementado tal como él lo describe y deja fuera lo descartado.
- **Si no los ha pasado, para y pídelos** antes de redactar una sola línea. No
  redactes un borrador "provisional", ni deduzcas los cambios del D10, del
  código, de los ficheros de la carpeta o de las capturas.
- Vale cualquier forma de aportarlos: una lista en el chat, un documento, notas
  o la explicación pantalla por pantalla. Lo que no vale es que no estén.
- Si el usuario insiste en generarlo sin pasarlos, explícale que el manual
  saldría inventado y vuelve a pedirlos. **No lo generes.**

### Contexto completo obligatorio

Además de los cambios, **nunca generes el manual sin contexto completo**. Si el
usuario pide el manual y no ha adjuntado el A10 ni el D10, **pídeselos antes de
redactar nada**.

- Caso normal: A10 + D10 + cambios pasados por el usuario → el manual se redacta
  desde la "SOLUCIÓN PROPUESTA" del D10, resolviendo sus referencias contra el
  A10 y ajustándolo a los cambios que ha pasado el usuario.
- **A veces solo existe el A10** (desarrollos sin D10): es válido; redacta a
  partir del A10 y de los cambios que pase el usuario.
- Si solo pasa el D10, pide también el A10 (el D10 suele referenciarlo).
- Solo si el usuario dice explícitamente que no hay documentos, redacta a partir
  de los cambios que él explique — que siguen siendo obligatorios.

## Reglas de redacción (lo más importante)

- **Nivel usuario, no técnico.** El cliente NO debe leer nombres de tablas,
  paquetes PL/SQL, triggers, columnas por número, ni jerga interna
  (nada de `integra_payhawk_cab`, `pk_...`, `GTT`, `FORMS_DDL`...).
  SÍ puede aparecer lo que el usuario ve en pantalla: nombres de programas
  (`IMP_PAYHAWK`), procesos/predefinidos (`CONTA_PAYHAW`, `ASMA_XLS`), pestañas,
  botones/plugins, campos del formulario, mensajes de error y validaciones
  descritas en términos funcionales ("la fecha debe pertenecer a un periodo
  abierto", no "se valida contra periodos_contables").
- **Formal pero claro.** Frases directas, impersonal o "se ha creado / se
  podrá...". Sin coloquialismos y sin florituras.
- **Orientado al uso**: qué hace cada pantalla, qué pasos sigue el usuario, qué
  validaciones se aplican y qué verá si algo falla, dónde consultar históricos.
- **Bien estructurado**: una sección Título 1 (`h1`) por área funcional (p.ej.
  "Proceso y asientos predefinidos", "Programa de importación", "Histórico de
  importaciones"), con `h2`/`h3` para sub-apartados si la sección es larga.
  Opcionalmente una "Introducción" breve al principio si el desarrollo lo pide.
  El índice se genera solo a partir de los títulos: no lo escribas a mano.
- **Capturas junto al texto que ilustran**, nunca agrupadas al final. Con una
  frase antes que diga qué se ve ("En la segunda pestaña se pueden consultar...").
- Longitud típica: 2–5 páginas de contenido. Es un manual de cambios, no la
  documentación completa del módulo.

## Flujo de trabajo

1. **Comprueba que el usuario ha pasado los cambios realizados.** Si no lo ha
   hecho, **pídeselos y para aquí** — es la norma absoluta, no la saltes nunca.
2. **Comprueba el contexto**: en modo carpeta, busca primero el A10/D10 en
   `doc/` (según "Modos de entrada"); si tras eso faltan documentos, pídelos y
   para aquí (salvo los casos permitidos de "Contexto completo obligatorio").
3. **Lee las fuentes**: del D10, céntrate en la sección **SOLUCIÓN PROPUESTA**
   y resuelve sus referencias al A10; localiza expediente, título y las áreas
   funcionales del desarrollo.
   - **Contrasta la cobertura**: haz una lista de los cambios que recoge la
     SOLUCIÓN PROPUESTA del D10 y compárala con los cambios que ha pasado el
     usuario. Si detectas cambios del D10 que **no** aparecen en lo aportado,
     **pregunta al usuario antes de generar**: ¿es todo lo que hay que
     documentar o va a pasar más cambios? Puede que algo del D10 no se
     implementara al final — no lo incluyas por tu cuenta ni lo descartes en
     silencio; que lo confirme él.
4. **Reúne las capturas**: identifica las imágenes adjuntas y decide en qué
   sección va cada una (pregunta al usuario si no está claro). Guárdalas como
   ficheros PNG/JPG accesibles en disco.
5. **Redacta el contenido** partiendo de los cambios que ha pasado el usuario,
   siguiendo las reglas anteriores, y móntalo en un `contenido.json` (formato
   abajo). Si la estructura es dudosa, propón antes el índice de secciones en el
   chat.
6. **Genera el docx**:
   ```
   python3 scripts/build_manual.py \
       --titulo "1813283 - INTEGRACION CON PAYHAWK" \
       --contenido contenido.json \
       --out "Manual Desarrollo - 1813283 - INTEGRACION CON PAYHAWK.docx"
   ```
7. **Verifica el resultado**: conviértelo a PDF y mira las páginas renderizadas
   (portada con el título, índice con todas las secciones, capturas bien
   colocadas y legibles, página de cierre al final). Corrige y regenera si algo
   descuadra.
   ```
   soffice --headless --convert-to pdf salida.docx && pdftoppm -jpeg -r 60 salida.pdf pag
   ```
8. **Entrega el fichero** al usuario. Los números de página del índice se
   recalculan solos al abrir el documento en Word (updateFields); avísalo si
   procede.

### Nombre y ubicación del fichero

- Nombre: `Manual Desarrollo - {expediente} - {TITULO}.docx`.
- En **modo carpeta de proyecto**, guárdalo en la subcarpeta **`doc/`** de la
  carpeta del expediente (la misma donde encontraste el A10/D10). En chat
  normal, entrégalo como fichero en outputs.

## Formato de contenido.json

Lista ordenada de bloques; el script los inserta tal cual entre el índice y la
página de cierre:

```json
[
  {"tipo": "h1", "texto": "Programa de importación"},
  {"tipo": "h2", "texto": "Revisión de los registros"},
  {"tipo": "p",  "texto": "Se ha creado un programa **IMP_PAYHAWK**..."},
  {"tipo": "bullet", "texto": "**Fecha de asiento**: por defecto la del día."},
  {"tipo": "img", "ruta": "/ruta/absoluta/captura.png"}
]
```

- Tipos: `h1`–`h4` (títulos, entran en el índice), `p` (párrafo), `bullet`
  (viñeta), `img` (captura PNG/JPG/GIF, se escala al ancho de página).
- `**texto**` dentro de `p`/`bullet` sale en negrita. Úsala para nombres de
  programas, campos y pestañas.
- No metas texto vacío ni bloques `img` con rutas inexistentes (el script
  aborta si la imagen no existe).

## Solución de problemas

- `No se encontró el ancla ...`: la plantilla del asset ha cambiado o está
  corrupta; usa la copia original de `assets/Manual_plantilla.docx`.
- Captura enorme o borrosa: el script limita ancho (~6,27") y alto; si aun así
  se ve mal, recorta la imagen fuente antes de insertarla.
- Índice con números "3" en el PDF de verificación: es normal; son valores en
  caché. Word los actualiza automáticamente al abrir el documento.
