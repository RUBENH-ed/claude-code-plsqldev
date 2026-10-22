#!/usr/bin/env python3
"""
Rellena la plantilla del A18 (con huecos {{TOKEN}}) y genera el .docx final.

La plantilla conserva logo, cabecera, pie y maquetación; este script solo
sustituye texto dentro de word/document.xml y word/header1.xml, así que el
formato queda intacto.

Datos esperados (JSON, --data datos.json):
  expediente   (obligatorio)   p.ej. "1806466"
  titulo       (obligatorio)   p.ej. "233.IBA.A10. MODIFICACION SIRE (...)"
  tecnico      (obligatorio)   nombre de la persona que entrega
  fecha_acta   (opcional)      "DD/MM/YYYY"  -> por defecto, hoy
  nombre_manual(opcional)      -> por defecto "Manual Desarrollo - {titulo}.docx"
  formulario / informe / libreria / objeto_bd / sqls / otros (opcionales)
        cada uno puede ser una cadena ya formateada o una lista de nombres
        (las listas se unen con ", "). Vacío -> celda vacía.

Uso:
  python fill_a18.py --data datos.json --template ../assets/A18_plantilla.docx \
                     --out "/mnt/user-data/outputs/A18 233.IBA.A10....docx"
"""
import argparse, datetime, json, os, re, sys, zipfile
from xml.sax.saxutils import escape

TOKEN_PARTS = ["word/document.xml", "word/header1.xml"]
BUCKETS = ["formulario", "informe", "libreria", "objeto_bd", "sqls", "otros"]


def as_text(value):
    """Lista -> 'a, b, c'; cadena -> tal cual; None -> ''.
    Nunca deja saltos de línea: cualquier espacio en blanco (incluidos intros y
    tabuladores) se colapsa a un espacio simple, para que las celdas de objetos
    queden en una sola línea separadas por comas."""
    if value is None:
        return ""
    if isinstance(value, (list, tuple)):
        items = [re.sub(r"\s+", " ", str(v)).strip() for v in value]
        return ", ".join(v for v in items if v)
    return re.sub(r"\s+", " ", str(value)).strip()


def build_token_map(data):
    titulo = (data.get("titulo") or "").strip()
    if not titulo:
        sys.exit("ERROR: falta 'titulo'")
    if not (data.get("expediente") or "").strip():
        sys.exit("ERROR: falta 'expediente'")
    if not (data.get("tecnico") or "").strip():
        sys.exit("ERROR: falta 'tecnico' (nombre de quien entrega)")

    fecha = (data.get("fecha_acta") or "").strip() or \
        datetime.date.today().strftime("%d/%m/%Y")
    manual = (data.get("nombre_manual") or "").strip() or \
        f"Manual Desarrollo - {titulo}.docx"

    m = {
        "{{EXPEDIENTE}}":    data["expediente"].strip(),
        "{{TITULO}}":        titulo,
        "{{FECHA_ACTA}}":    fecha,
        "{{TECNICO}}":       data["tecnico"].strip(),
        "{{NOMBRE_MANUAL}}": manual,
    }
    for b in BUCKETS:
        m["{{" + b.upper() + "}}"] = as_text(data.get(b))
    return m


def fill(template, token_map, out):
    # Escapamos los valores para XML (& < > y comillas) antes de insertarlos.
    safe = {tok: escape(val, {'"': "&quot;", "'": "&apos;"})
            for tok, val in token_map.items()}

    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    zin = zipfile.ZipFile(template, "r")
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as zout:
        leftover = dict(safe)
        for item in zin.infolist():
            data = zin.read(item.filename)
            if item.filename in TOKEN_PARTS:
                text = data.decode("utf-8")
                for tok, val in safe.items():
                    if tok in text:
                        text = text.replace(tok, val)
                        leftover.pop(tok, None)
                data = text.encode("utf-8")
            zout.writestr(item, data)
    zin.close()
    if leftover:
        print("AVISO: estos huecos no se encontraron en la plantilla: "
              + ", ".join(sorted(leftover)), file=sys.stderr)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", required=True, help="JSON con los datos del A18")
    ap.add_argument("--template", required=True, help="ruta a A18_plantilla.docx")
    ap.add_argument("--out", required=True, help="ruta del .docx de salida")
    args = ap.parse_args()

    data = json.load(open(args.data, encoding="utf-8"))
    token_map = build_token_map(data)
    path = fill(args.template, token_map, args.out)
    print("Generado: " + path)


if __name__ == "__main__":
    main()
