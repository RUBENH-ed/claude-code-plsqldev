---
name: "a18-acta-entrega"
description: "Genera el documento A18 \"Acta de Entrega de Desarrollo\" de EDISA/LIBRA. Úsala SIEMPRE que el usuario quiera crear, generar o rellenar un A18, un \"acta de entrega\", o documentar la entrega/instalación de un desarrollo de LIBRA — aunque no diga literalmente \"A18\". Dispara también con \"hazme el acta de este desarrollo\", \"necesito el A18 del expediente X\", \"genera el acta de entrega\", al pasar una carpeta de proyecto \"expediente - nombre\", al pasar una captura de la pantalla de Desarrollos LIBRA, o cuando alguien termina un desarrollo y aporta un expediente, un título y/o una lista de ficheros (.fmb, .sql, .pll, .olb, paquetes pk_, etc.). Produce un .docx idéntico a la plantilla oficial de EDISA (logo, cabecera y pie)."
---

# A18 — Acta de Entrega de Desarrollo (EDISA / LIBRA)

## Qué es y cómo funciona

El **A18** es el acta con la que se entrega un desarrollo de LIBRA: recoge el
expediente, el título, los programas a instalar (clasificados por tipo de objeto),
el técnico responsable y unos textos fijos de validación.

Esta skill **no reconstruye el documento desde cero**. Lleva la plantilla oficial
(`assets/A18_plantilla.docx`) con huecos `{{TOKEN}}` y solo sustituye el texto
variable, así que el acta sale calcada —logo, cabecera repetida en cada página y pie
con la tabla Código/Versión/Aprobación— sin descuadrar nada. La salida es `.docx`.

## Modos de entrada (se combinan)

Los datos pueden llegar de tres formas. Identifica cuál aplica y reúne lo que falte.

### A) Datos sueltos (chat normal)
El usuario aporta **expediente**, **título** y la **lista de archivos** (en texto o
en foto/captura). Es el modo por defecto en una conversación normal.

### B) Carpeta de proyecto (típico en Cowork)
El usuario pasa la **ruta de una carpeta** llamada `{expediente} - {nombre}`
(p.ej. `1813283 - INTEGRACION CON PAYHAWK`), con esta estructura:

```
{expediente} - {nombre}/
├── cfstd/          (+ subcarpetas como cfsubidos)        <- se recoge TODO
├── cfcliente/      (+ subcarpetas como prog_estandar/…)  <- se recoge TODO
├── doc/            <- AQUÍ se guarda el A18 generado
├── instalacion/    (NO se recoge)
└── test/           (NO se recoge)
```

De la carpeta sale **todo**: el expediente y el nombre (del nombre de la carpeta) y
los archivos (todo lo que cuelgue de `cfstd` y `cfcliente`, a cualquier profundidad,
sin duplicar nombres). El A18 se guarda en la subcarpeta **`doc`**.

### C) Captura de pantalla de "Desarrollos LIBRA"
El usuario pasa una captura de la pantalla de expediente. De ella extrae:
- **Expediente**: el número arriba a la derecha. Tiene **siempre 7 dígitos** (solo
  números). Si aparece como `1813283 / 1270838`, el expediente es **el de la
  izquierda de la `/`** (`1813283`); ignora la `/` y los números de su derecha.
- **Título/nombre**: el campo de descripción grande (bajo los botones), p.ej.
  `INTEGRACIÓN CON PAYHAWK`. Transcríbelo **sin tildes** (`Ó`→`O`, `Á`→`A`, …;
  **conserva la ñ**) → `INTEGRACION CON PAYHAWK`.

La captura aporta expediente + título; los archivos vendrán de una carpeta (modo B)
o de una lista (modo A).

> **Normalización del título**: el título va **sin tildes** (conservando ñ), igual
> que en los ejemplos de EDISA. En modo carpeta el script ya lo hace; al leerlo de
> una captura, transcríbelo ya sin tildes. **Nunca cambies las mayúsculas/minúsculas
> de la descripción**: respétalas exactamente como vienen (solo quitas las tildes).

## Flujo

**1. Reúne expediente, título y la lista de archivos** según el modo:

- **Modo carpeta (B)** — una sola llamada te da expediente, nombre y el reparto ya
  hecho (recoge de `cfstd`+`cfcliente` recursivo y deduplica):
  ```bash
  python scripts/classify_files.py --project-dir "/ruta/1813283 - INTEGRACION CON PAYHAWK"
  ```
  Devuelve `expediente`, `nombre`, las 6 categorías y `omitidos`.

- **Modo lista (A)** — si la lista viene como **imagen**, lee de ella los nombres de
  fichero; si viene como texto, úsala. Vuelca los nombres a un fichero (uno por
  línea) y clasifícalos:
  ```bash
  printf '%s\n' "afip_impuestos.fmb" "pk_afip_modelos.sql" ... > /tmp/a18_files.txt
  python scripts/classify_files.py --list /tmp/a18_files.txt
  ```

- **Captura (C)** — saca expediente y título de la imagen con las reglas de arriba.

**1-bis. Quita SIEMPRE los formularios base `manbase`.** Después de clasificar y
antes de enseñar el reparto, elimina de la categoría **Formulario** (y de cualquier
otra donde hubieran caído) los ficheros `manbase.fmb` y `manbase6.fmb`
(comparación **sin distinguir mayúsculas/minúsculas**, y también cualquier variante
`manbase*.fmb` tipo `MANBASE.FMB`, `manbase6.FMB`). El script no los filtra, así que
este descarte lo haces tú al montar el JSON. **No los menciones ni preguntes por
ellos**: se descartan en silencio, igual que las extensiones excluidas. Solo entran
si el usuario lo pide **expresamente**.

**2. Enseña el reparto de las 6 categorías** para que el usuario pueda corregir
alguna si algún fichero raro cayó donde no toca. **Los ficheros con extensión
excluida (`.xml`, `.xsd`, `.fmx`, `.err`, `.rtf`, `.txt`) y los `manbase.fmb` /
`manbase6.fmb` se descartan en silencio: NO los menciones ni preguntes por ellos.**
Solo se incluye uno de esos si el usuario lo pide **expresamente** (para extensiones
usa `--include .xml` o añádelo a mano al JSON; para `manbase`, añádelo a mano).
- Para **mover** un fichero de categoría: ajusta el JSON a mano antes de generar.

**3. Pregunta SIEMPRE el nombre del técnico** para el apartado *Recursos*. Sugiere
por defecto el de la persona que está usando la skill **si lo conoces por el contexto
o por tu memoria** (p.ej. "¿A nombre de qué técnico hago el acta? Por defecto pondría
'Nombre Apellido'."), pero **pregunta siempre y espera su respuesta** — no lo des por
hecho aunque lo sepas. Puedes hacer esta pregunta en el mismo mensaje del paso 2.

**4. Determina fecha** (hoy por defecto, `DD/MM/YYYY`). El nombre del manual se
calcula solo (`Manual Desarrollo - {título}.docx`, con guion) y la Fase va en blanco.

**5. Construye el `datos.json`** combinando metadatos + reparto confirmado (ya sin
`manbase.fmb` ni `manbase6.fmb`):
```json
{
  "expediente": "1813283",
  "titulo": "INTEGRACION CON PAYHAWK",
  "tecnico": "Nombre Apellido",
  "fecha_acta": "26/06/2026",
  "formulario": ["..."], "informe": ["..."], "libreria": ["..."],
  "objeto_bd": ["..."], "sqls": ["..."], "otros": ["..."]
}
```
`fecha_acta` y `nombre_manual` pueden omitirse (se aplican los valores por defecto).
Cada categoría puede ser lista (se une con `, `) o cadena ya hecha.

**6. Genera el acta:**
```bash
python scripts/fill_a18.py --data /tmp/a18_datos.json \
  --template assets/A18_plantilla.docx --out "<RUTA_SALIDA>"
```
La **ruta de salida** depende del modo:
- **Modo carpeta (B)**: guárdalo en la subcarpeta `doc` del proyecto →
  `"/ruta/1813283 - INTEGRACION CON PAYHAWK/doc/A18 INTEGRACION CON PAYHAWK.docx"`.
- **Modo lista/chat (A)**: `"/mnt/user-data/outputs/A18 {título}.docx"`.

Nombra el fichero `A18 {título}.docx`, sustituyendo por espacios los caracteres no
válidos en nombres de fichero (`/ \ : * ? " < > |`). El título ya va sin tildes.

**7. (Opcional) Vista previa.** Si hay LibreOffice, conviértelo a imagen y enséñasela
para que valide antes de darlo por bueno:
```bash
python /mnt/skills/public/docx/scripts/office/soffice.py --headless --convert-to pdf --outdir /tmp/prev "<salida>.docx"
pdftoppm -jpeg -r 120 /tmp/prev/*.pdf /tmp/prev/a18
```

**8. Verifica antes de entregar.** Comprueba que en la celda de **Formulario** no
aparece ningún `manbase` y que los ficheros excluidos tampoco están.

**9. Entrega.** En modo lista/chat, usa `present_files` con el `.docx`. En modo
carpeta, el fichero ya queda en `doc/`; preséntalo también si la herramienta está
disponible y confirma la ruta.

## Reglas de clasificación (referencia)

Se aplican en este orden (de más específica a más general). Las codifica
`classify_files.py`; esta tabla es solo orientativa.

| Patrón del nombre de archivo                 | Tipo de Objeto |
| -------------------------------------------- | -------------- |
| `manbase.fmb`, `manbase6.fmb` (y `manbase*.fmb`) | *omitidos* (formularios base; nunca van al acta salvo petición expresa) |
| Excluidos por defecto: `.xml .xsd .fmx .err .rtf .txt` | *omitidos* (no van al acta salvo petición expresa) |
| `*.fmb`                                       | Formulario     |
| `*_inf.sql`, `*.xdmz`, `*.xdoz`              | Informe        |
| `*.pll`, `*.olb`                             | Librería       |
| `pk*.sql`                                     | Objeto de BD   |
| cualquier otro `*.sql`                        | Sqls           |
| lo que no encaje en nada anterior            | Otros          |

En modo carpeta se recoge **todo** lo que cuelga de `cfstd` y `cfcliente` (incluidas
subcarpetas como `cfsubidos` o `prog_estandar`), **deduplicando** por nombre (un mismo
fichero en varios sitios aparece una sola vez). Las carpetas `instalacion`, `test` y
`doc` no se recogen.

## Valores por defecto

- **Técnico**: **siempre se pregunta** (paso 3). Por defecto se sugiere el nombre de
  la persona que usa la skill si se conoce por el contexto.
- **Fecha del acta**: hoy (`DD/MM/YYYY`).
- **Nombre del manual o guía**: `Manual Desarrollo - {título}.docx` (automático,
  **con guion** " - " entre "Manual Desarrollo" y la descripción).
- **Fase**: en blanco.
- Los textos de *Función a realizar*, *Acciones de validación*, *observaciones* y
  *destinatarios* son fijos y ya vienen en la plantilla.

## Reglas que no se rompen nunca

- **`manbase` fuera**: `manbase.fmb` y `manbase6.fmb` (en cualquier combinación de
  mayúsculas/minúsculas) **nunca** aparecen en el acta. Se descartan en silencio.
- **Mayúsculas/minúsculas**: respeta la descripción tal cual viene; no la pongas en
  mayúsculas ni en minúsculas. Lo único que se le quita son las tildes.
- **Sin saltos de línea en las celdas de objetos**: cuando un tipo tiene varios
  ficheros, van en una sola línea separados por `, ` (nunca un fichero por línea).
  El script ya lo garantiza.
- **Ficheros excluidos**: los `.xml`, `.xsd`, `.fmx`, `.err`, `.rtf`, `.txt` se
  ignoran sin preguntar; solo entran si el usuario lo pide expresamente.

## Notas

- Los scripts solo usan la librería estándar de Python (no requieren `pip`/`npm`).
- Si piden el acta en `.doc` clásico, genera el `.docx` y conviértelo:
  `python /mnt/skills/public/docx/scripts/office/soffice.py --headless --convert-to doc --outdir <dir> "<salida>.docx"`
- El manual de desarrollo (el otro documento que acompaña al A18) es un entregable
  distinto; si se añade en el futuro, irá como skill aparte.

