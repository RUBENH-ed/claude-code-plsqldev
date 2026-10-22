#!/usr/bin/env python3
"""
build_manual.py — Genera un "Manual de Desarrollo" EDISA/LIBRA a partir de la
plantilla oficial (assets/Manual_plantilla.docx) y un JSON de contenido.

Uso:
  python3 build_manual.py --titulo "1813283 - INTEGRACION CON PAYHAWK" \
      --contenido contenido.json --out "Manual Desarrollo - ....docx" \
      [--plantilla ruta/Manual_plantilla.docx]

Formato de contenido.json (lista ordenada de bloques):
  [
    {"tipo": "h1", "texto": "Programa de importación"},
    {"tipo": "h2", "texto": "Subsección"},          # también h3, h4
    {"tipo": "p",  "texto": "Párrafo. Admite **negrita**."},
    {"tipo": "bullet", "texto": "Elemento de lista. Admite **negrita**."},
    {"tipo": "img", "ruta": "/ruta/captura.png"}    # png o jpg
  ]

- El índice se regenera con los títulos h1–h4 y se marca updateFields para que
  Word recalcule los números de página al abrir el documento.
- Las imágenes se escalan al ancho de página (máx ~6,27") conservando proporción.
"""
import argparse
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import zipfile

# --- Anclas fijas de la plantilla (paraId) ---
PARA_PB_TRAS_INDICE = 'w14:paraId="4723CF3D"'   # párrafo con salto de página tras el índice
PARA_PB_ANTES_CIERRE = 'w14:paraId="4C977E54"'  # párrafo con salto de página antes de la página final
PARA_TOC_INICIO = 'w14:paraId="195AAC04"'       # primera entrada del TOC (contiene field begin)
PARA_TOC_FIN = 'w14:paraId="5C3631A9"'          # párrafo con field end del TOC

MAX_W_EMU = 5731510  # ~6,27 in: ancho útil de página
MAX_H_EMU = 7200000  # tope de alto para capturas verticales
EMU_PER_PX_96 = 9525  # 914400 / 96 dpi


def esc(t):
    return t.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')


def runs_con_negrita(texto):
    """Convierte texto con **negrita** en runs XML."""
    out = []
    for i, parte in enumerate(re.split(r'\*\*', texto)):
        if parte == '':
            continue
        if i % 2 == 1:
            out.append(f'<w:r><w:rPr><w:b/><w:bCs/></w:rPr><w:t xml:space="preserve">{esc(parte)}</w:t></w:r>')
        else:
            out.append(f'<w:r><w:t xml:space="preserve">{esc(parte)}</w:t></w:r>')
    return ''.join(out)


def png_jpg_dims(path):
    """Dimensiones (px) de un PNG/JPEG/GIF sin dependencias externas."""
    with open(path, 'rb') as f:
        head = f.read(26)
        # PNG
        if head.startswith(b'\x89PNG'):
            w, h = struct.unpack('>II', head[16:24])
            return w, h
        # GIF
        if head[:6] in (b'GIF87a', b'GIF89a'):
            w, h = struct.unpack('<HH', head[6:10])
            return w, h
        # JPEG
        if head.startswith(b'\xff\xd8'):
            f.seek(2)
            while True:
                b = f.read(1)
                if not b:
                    break
                if b != b'\xff':
                    continue
                marker = f.read(1)
                while marker == b'\xff':
                    marker = f.read(1)
                m = marker[0]
                if 0xC0 <= m <= 0xCF and m not in (0xC4, 0xC8, 0xCC):
                    f.read(3)
                    h, w = struct.unpack('>HH', f.read(4))
                    return w, h
                elif m in (0xD8, 0xD9):
                    continue
                else:
                    (seglen,) = struct.unpack('>H', f.read(2))
                    f.seek(seglen - 2, 1)
    raise ValueError(f'Formato de imagen no soportado: {path}')


def p_heading(nivel, texto, bm_id, bm_name):
    return (f'<w:p><w:pPr><w:pStyle w:val="Ttulo{nivel}"/></w:pPr>'
            f'<w:bookmarkStart w:name="{bm_name}" w:id="{bm_id}"/>'
            f'<w:r><w:t xml:space="preserve">{esc(texto)}</w:t></w:r>'
            f'<w:bookmarkEnd w:id="{bm_id}"/></w:p>')


def p_texto(texto):
    return (f'<w:p><w:pPr><w:pStyle w:val="Textocuerpo"/></w:pPr>'
            f'{runs_con_negrita(texto)}</w:p>')


def p_bullet(texto):
    return (f'<w:p><w:pPr><w:pStyle w:val="Prrafodelista"/></w:pPr>'
            f'{runs_con_negrita(texto)}</w:p>')


def p_imagen(rid, cx, cy, doc_id):
    return ('<w:p><w:pPr><w:pStyle w:val="Textocuerpo"/><w:jc w:val="center"/></w:pPr><w:r><w:drawing>'
            f'<wp:inline distT="0" distB="0" distL="0" distR="0">'
            f'<wp:extent cx="{cx}" cy="{cy}"/><wp:effectExtent l="0" t="0" r="0" b="0"/>'
            f'<wp:docPr id="{doc_id}" name="Imagen {doc_id}"/>'
            '<wp:cNvGraphicFramePr><a:graphicFrameLocks xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" noChangeAspect="1"/></wp:cNvGraphicFramePr>'
            '<a:graphic xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">'
            '<a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">'
            '<pic:pic xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">'
            f'<pic:nvPicPr><pic:cNvPr id="{doc_id}" name=""/><pic:cNvPicPr/></pic:nvPicPr>'
            f'<pic:blipFill><a:blip r:embed="{rid}"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>'
            f'<pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="{cx}" cy="{cy}"/></a:xfrm>'
            '<a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr>'
            '</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>')


def toc_xml(entradas):
    """entradas: lista de (nivel, texto, bm_name). Replica el TOC de la plantilla."""
    if not entradas:
        entradas = [(1, ' ', '_TocGEN0')]
    paras = []
    for i, (nivel, texto, bm) in enumerate(entradas):
        pos = 9016
        inicio = ''
        if i == 0:
            inicio = ('<w:r><w:fldChar w:fldCharType="begin"/>'
                      '<w:instrText>TOC \\o \\z \\u \\h</w:instrText>'
                      '<w:fldChar w:fldCharType="separate"/></w:r>')
        paras.append(
            f'<w:p><w:pPr><w:pStyle w:val="TDC{nivel}"/>'
            f'<w:tabs><w:tab w:val="right" w:leader="dot" w:pos="{pos}"/></w:tabs>'
            '<w:rPr><w:rFonts w:eastAsiaTheme="minorEastAsia"/><w:noProof/><w:sz w:val="24"/>'
            '<w:szCs w:val="24"/><w:lang w:eastAsia="es-ES_tradnl"/></w:rPr></w:pPr>'
            f'{inicio}'
            f'<w:hyperlink w:history="1" w:anchor="{bm}">'
            f'<w:r><w:rPr><w:rStyle w:val="Hipervnculo"/><w:noProof/></w:rPr>'
            f'<w:t xml:space="preserve">{esc(texto)}</w:t></w:r>'
            '<w:r><w:rPr><w:noProof/><w:webHidden/></w:rPr><w:tab/>'
            '<w:fldChar w:fldCharType="begin"/>'
            f'<w:instrText xml:space="preserve"> PAGEREF {bm} \\h </w:instrText>'
            '<w:fldChar w:fldCharType="separate"/><w:t>3</w:t>'
            '<w:fldChar w:fldCharType="end"/></w:r></w:hyperlink></w:p>')
    paras.append('<w:p><w:pPr><w:pStyle w:val="TDC2"/>'
                 '<w:tabs><w:tab w:val="right" w:leader="dot" w:pos="9015"/></w:tabs>'
                 '<w:rPr><w:rStyle w:val="Hipervnculo"/></w:rPr></w:pPr>'
                 '<w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>')
    return ''.join(paras)


def para_completo(xml, marcador):
    """Devuelve (inicio, fin) del <w:p> que contiene el marcador dado."""
    i = xml.find(marcador)
    if i < 0:
        raise RuntimeError(f'No se encontró el ancla {marcador} en la plantilla')
    ini = xml.rfind('<w:p ', 0, i)
    fin = xml.find('</w:p>', i)
    if fin < 0:  # párrafo autocontenido <w:p .../>
        fin = xml.find('/>', i)
        return ini, fin + 2
    return ini, fin + len('</w:p>')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--titulo', required=True)
    ap.add_argument('--contenido', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--plantilla',
                    default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                         '..', 'assets', 'Manual_plantilla.docx'))
    args = ap.parse_args()

    with open(args.contenido, encoding='utf-8') as f:
        bloques = json.load(f)

    tmp = tempfile.mkdtemp(prefix='manual_')
    unp = os.path.join(tmp, 'unp')
    with zipfile.ZipFile(args.plantilla) as z:
        z.extractall(unp)
    # seguridad: eliminar symlinks y basura de la plantilla
    for root, dirs, files in os.walk(unp):
        for n in files + dirs:
            p = os.path.join(root, n)
            if os.path.islink(p):
                os.unlink(p)
    trash = os.path.join(unp, '[trash]')
    if os.path.isdir(trash):
        shutil.rmtree(trash)

    docp = os.path.join(unp, 'word', 'document.xml')
    xml = open(docp, encoding='utf-8').read()

    # 1) Título de portada
    if '<w:t>titulo</w:t>' not in xml:
        raise RuntimeError('No se encontró el placeholder de título en la plantilla')
    xml = xml.replace('<w:t>titulo</w:t>',
                      f'<w:t xml:space="preserve">{esc(args.titulo)}</w:t>', 1)

    # 2) Relaciones e imágenes
    relsp = os.path.join(unp, 'word', '_rels', 'document.xml.rels')
    rels = open(relsp, encoding='utf-8').read()
    existentes = [int(m) for m in re.findall(r'Id="rId(\d+)"', rels)]
    next_rid = max(existentes) + 1
    media_dir = os.path.join(unp, 'word', 'media')
    img_count = len([f for f in os.listdir(media_dir)])
    nuevos_rels = []
    doc_id = 9000

    # 3) Construir cuerpo
    cuerpo = []
    entradas_toc = []
    bm_n = 0
    for b in bloques:
        t = b.get('tipo')
        if t in ('h1', 'h2', 'h3', 'h4'):
            bm_n += 1
            bm = f'_TocGEN{bm_n}'
            nivel = int(t[1])
            cuerpo.append(p_heading(nivel, b['texto'], 800 + bm_n, bm))
            entradas_toc.append((nivel, b['texto'], bm))
        elif t == 'p':
            cuerpo.append(p_texto(b['texto']))
        elif t == 'bullet':
            cuerpo.append(p_bullet(b['texto']))
        elif t == 'img':
            ruta = b['ruta']
            if not os.path.isfile(ruta):
                raise RuntimeError(f'Imagen no encontrada: {ruta}')
            w, h = png_jpg_dims(ruta)
            cx, cy = w * EMU_PER_PX_96, h * EMU_PER_PX_96
            if cx > MAX_W_EMU:
                cy = int(cy * MAX_W_EMU / cx)
                cx = MAX_W_EMU
            if cy > MAX_H_EMU:
                cx = int(cx * MAX_H_EMU / cy)
                cy = MAX_H_EMU
            img_count += 1
            ext = os.path.splitext(ruta)[1].lower().lstrip('.') or 'png'
            if ext == 'jpg':
                ext = 'jpeg'
            nombre = f'imageG{img_count}.{ext}'
            shutil.copy(ruta, os.path.join(media_dir, nombre))
            rid = f'rId{next_rid}'
            next_rid += 1
            nuevos_rels.append(
                f'<Relationship Id="{rid}" '
                'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" '
                f'Target="media/{nombre}"/>')
            doc_id += 1
            cuerpo.append(p_imagen(rid, int(cx), int(cy), doc_id))
        else:
            raise RuntimeError(f'Tipo de bloque desconocido: {t}')

    if nuevos_rels:
        rels = rels.replace('</Relationships>', ''.join(nuevos_rels) + '</Relationships>')
        open(relsp, 'w', encoding='utf-8').write(rels)
        # content types para jpeg/gif si hiciera falta
        ctp = os.path.join(unp, '[Content_Types].xml')
        ct = open(ctp, encoding='utf-8').read()
        for ext, mime in (('jpeg', 'image/jpeg'), ('gif', 'image/gif')):
            if f'Extension="{ext}"' not in ct and any(f'.{ext}' in r for r in nuevos_rels):
                ct = ct.replace('</Types>', f'<Default Extension="{ext}" ContentType="{mime}"/></Types>')
        open(ctp, 'w', encoding='utf-8').write(ct)

    # 4) Sustituir la zona de contenido (entre los dos saltos de página ancla)
    _, fin_pb1 = para_completo(xml, PARA_PB_TRAS_INDICE)
    ini_pb2, _ = para_completo(xml, PARA_PB_ANTES_CIERRE)
    xml = xml[:fin_pb1] + ''.join(cuerpo) + xml[ini_pb2:]

    # 5) Regenerar el índice
    ini_toc, _ = para_completo(xml, PARA_TOC_INICIO)
    _, fin_toc = para_completo(xml, PARA_TOC_FIN)
    xml = xml[:ini_toc] + toc_xml(entradas_toc) + xml[fin_toc:]

    open(docp, 'w', encoding='utf-8').write(xml)

    # 5b) Forzar idioma español (la plantilla arrastra en-US en varios estilos
    # y provoca falsos errores de ortografía en Word)
    for parte in ('styles.xml', 'document.xml'):
        pp = os.path.join(unp, 'word', parte)
        s = open(pp, encoding='utf-8').read()
        s = s.replace('<w:lang w:val="en-US"', '<w:lang w:val="es-ES"')
        open(pp, 'w', encoding='utf-8').write(s)

    # 6) updateFields para que Word recalcule el índice al abrir
    setp = os.path.join(unp, 'word', 'settings.xml')
    st = open(setp, encoding='utf-8').read()
    if '<w:updateFields' not in st:
        ins = '<w:updateFields w:val="true"/>'
        for tag in ('<w:hdrShapeDefaults', '<w:footnotePr', '<w:endnotePr',
                    '<w:compat', '<w:rsids', '<w:mathPr'):
            i = st.find(tag)
            if i >= 0:
                st = st[:i] + ins + st[i:]
                break
        else:
            st = st.replace('</w:settings>', ins + '</w:settings>')
        open(setp, 'w', encoding='utf-8').write(st)

    # 7) Empaquetar
    out = os.path.abspath(args.out)
    if os.path.exists(out):
        os.remove(out)
    base = os.getcwd()
    os.chdir(unp)
    try:
        subprocess.run(['zip', '-Xrq', out, '.'], check=True)
    finally:
        os.chdir(base)
    shutil.rmtree(tmp)
    print(f'Generado: {out}')


if __name__ == '__main__':
    main()
