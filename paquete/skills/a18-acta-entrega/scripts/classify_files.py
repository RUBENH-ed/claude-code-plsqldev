#!/usr/bin/env python3
"""
Clasifica una lista plana de nombres de archivo en los 6 tipos de objeto del A18.

Reglas (se evalúan en este orden, de más específica a más general):
  1. Extensiones excluidas por defecto -> 'omitidos' (no van al acta salvo que el
     usuario lo pida): .xml .xsd .fmx .err .rtf .txt
  2. *.fmb                                  -> formulario
  3. *_inf.sql | *.xdmz | *.xdoz           -> informe
  4. *.pll | *.olb                         -> libreria
  5. pk*.sql                               -> objeto_bd   (packages)
  6. *.sql (cualquier otro)                -> sqls
  7. lo demás                              -> otros

Entrada: nombres por stdin o como fichero (--list ruta), uno por línea. También
acepta separación por comas o punto y coma dentro de una línea. Si llegan rutas,
se queda con el nombre de fichero.

Salida: JSON con las 6 listas + 'omitidos', preservando el orden de entrada.

Uso:
  python classify_files.py --list ficheros.txt
  echo "a.fmb, pk_x.sql, foo_inf.sql" | python classify_files.py
  python classify_files.py --list ficheros.txt --include .xml   # no excluir .xml
"""
import argparse, json, os, re, sys
import unicodedata

DEFAULT_BLACKLIST = {".xml", ".xsd", ".fmx", ".err", ".rtf", ".txt"}
BUCKETS = ["formulario", "informe", "libreria", "objeto_bd", "sqls", "otros"]
CARPETAS_PROYECTO = {"cfstd", "cfcliente"}  # de dónde se recogen los ficheros


def strip_acentos(s):
    """Quita tildes de las vocales (á->a, ó->o, ü->u...) pero conserva la ñ."""
    out = []
    for ch in s:
        if ch in "ñÑ":
            out.append(ch)
            continue
        d = unicodedata.normalize("NFD", ch)
        out.append("".join(c for c in d if unicodedata.category(c) != "Mn"))
    return "".join(out)


def parse_project_name(project_dir):
    """De una carpeta '{7 dígitos} - {nombre}' devuelve (expediente, nombre).
    El nombre se devuelve sin tildes. Si no encaja el patrón, expediente='' y se
    devuelve el mejor nombre posible (también sin tildes)."""
    base = os.path.basename(os.path.normpath(project_dir))
    m = re.match(r"^\s*(\d{7})\s*-\s*(.+?)\s*$", base)
    if m:
        return m.group(1), strip_acentos(m.group(2))
    return "", strip_acentos(base)


def collect_from_project(project_dir):
    """Recoge todos los ficheros que cuelgan de cfstd y cfcliente (recursivo,
    a cualquier profundidad) dentro de la carpeta del proyecto. Deduplica por
    nombre (cada nombre aparece una sola vez) y devuelve la lista ordenada."""
    seen = set()
    for dirpath, dirnames, filenames in os.walk(project_dir):
        dirnames.sort()
        rel = os.path.relpath(dirpath, project_dir)
        parts = {p.lower() for p in rel.split(os.sep)}
        if CARPETAS_PROYECTO & parts:          # estamos dentro de cfstd/cfcliente
            for f in filenames:
                seen.add(f)
    return sorted(seen, key=str.lower)


def parse_names(raw):
    """Trocea el texto en nombres: separa por saltos de línea, comas y ';'."""
    names = []
    for line in raw.splitlines():
        for piece in re.split(r"[,;]", line):
            piece = piece.strip().strip('"').strip("'").strip()
            if not piece:
                continue
            names.append(os.path.basename(piece))
    return names


def classify(names, include_exts=()):
    blacklist = DEFAULT_BLACKLIST - {e.lower() for e in include_exts}
    out = {k: [] for k in BUCKETS}
    out["omitidos"] = []
    seen = set()
    for name in names:
        if name in seen:           # evita duplicados conservando el primero
            continue
        seen.add(name)
        low = name.lower()
        ext = os.path.splitext(low)[1]
        if ext in blacklist:
            out["omitidos"].append(name)
        elif low.endswith(".fmb"):
            out["formulario"].append(name)
        elif low.endswith("_inf.sql") or low.endswith(".xdmz") or low.endswith(".xdoz"):
            out["informe"].append(name)
        elif ext in (".pll", ".olb"):
            out["libreria"].append(name)
        elif low.startswith("pk") and low.endswith(".sql"):
            out["objeto_bd"].append(name)
        elif low.endswith(".sql"):
            out["sqls"].append(name)
        else:
            out["otros"].append(name)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--list", help="fichero con los nombres (uno por línea)")
    ap.add_argument("--project-dir",
                    help="carpeta del proyecto: recoge de cfstd y cfcliente (recursivo)")
    ap.add_argument("--include", nargs="*", default=[],
                    help="extensiones a NO excluir, p.ej. --include .xml .rtf")
    args = ap.parse_args()

    if args.project_dir:
        names = collect_from_project(args.project_dir)
    elif args.list:
        names = parse_names(open(args.list, encoding="utf-8").read())
    else:
        names = parse_names(sys.stdin.read())
    result = classify(names, include_exts=args.include)
    if args.project_dir:
        exp, nombre = parse_project_name(args.project_dir)
        result = {"expediente": exp, "nombre": nombre, **result}
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
