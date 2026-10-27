"""Genera hojas de caminar con movimiento de piernas para Mateo (3 direcciones) y el coyote."""
import sys
import numpy as np
from PIL import Image

ASSETS = r"c:/Users/jlore/Desktop/UTNG/10mo Cuatrimestre/Barron/Unidad 2/Videogame/SombrasDeLaFrontera/Assets/Sprites/"
PREVIEW = len(sys.argv) > 1 and sys.argv[1] == "preview"
SCRATCH = r"C:/Users/jlore/AppData/Local/Temp/claude/c--Users-jlore-Desktop-UTNG-10mo-Cuatrimestre-Barron-Unidad-2-Videogame/ff8140b6-e453-495f-a410-07af72c7e8b3/scratchpad/"


def load(path):
    return np.array(Image.open(ASSETS + path).convert("RGBA"))


def row_bands(a, rows):
    has = (a[:, :, 3] > 40).any(axis=1)  # imagen: y=0 arriba
    out = []; st = None; last = None
    for y, v in enumerate(has):
        if v:
            if st is None: st = y
            last = y
        elif st is not None and y - last > 6:
            out.append((st, last)); st = None
    if st is not None: out.append((st, last))
    out = [b for b in out if b[1] - b[0] > 12]
    assert len(out) == rows, (len(out), rows)
    return out  # de arriba hacia abajo


def cells(img, cols, rows, row):
    """Devuelve los cuadros de una fila (recortados a la franja) y su línea de pies."""
    H, W = img.shape[:2]
    b0, b1 = row_bands(img, rows)[row]
    cw = W / cols
    frames = []
    for c in range(cols):
        x0, x1 = int(round(c * cw)), int(round((c + 1) * cw))
        frames.append(img[b0:b1 + 1, x0:x1].copy())
    return frames


def clean_cell(f):
    """Elimina pixeles sueltos de cuadros vecinos que tocan los bordes laterales de la celda."""
    from collections import deque
    a = f[:, :, 3] > 20
    H, W = a.shape
    # componente principal = la que contiene más pixeles
    lab = np.zeros(a.shape, int); n = 0; sizes = {}
    for y in range(H):
        for x in range(W):
            if a[y, x] and lab[y, x] == 0:
                n += 1; q = deque([(y, x)]); lab[y, x] = n; cnt = 0
                while q:
                    cy, cx = q.popleft(); cnt += 1
                    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        ny, nx = cy + dy, cx + dx
                        if 0 <= ny < H and 0 <= nx < W and a[ny, nx] and lab[ny, nx] == 0:
                            lab[ny, nx] = n; q.append((ny, nx))
                sizes[n] = cnt
    if not sizes: return f
    keep = max(sizes, key=sizes.get)
    out = f.copy()
    for k, s in sizes.items():
        if k == keep: continue
        ys, xs = np.nonzero(lab == k)
        touches_edge = xs.min() == 0 or xs.max() == W - 1
        if touches_edge or s < 25:
            out[lab == k] = 0
    return out


def leg_cycle(frame, hip_frac, t, stride_scale=1.0):
    """
    Ciclo de piernas para un cuadro de perfil (mirando a la derecha) con zancada fija.
    Cada pierna (solo la zona del pantalón, sin las manos) gira como pieza rígida desde la cadera:
    t=0 zancada original, t=0.5 piernas bajo la cadera, t=1 zancada invertida.
    """
    import math
    from scipy import ndimage
    a = frame[:, :, 3] > 40
    H, W = a.shape
    ys, xs = np.nonzero(a)
    top, bottom = ys.min(), ys.max()
    rgb = frame[:, :, :3].astype(int)
    r_, g_, b_ = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = rgb.max(axis=2)
    yy = np.arange(H)[:, None].repeat(W, 1)

    # Colores: mezclilla (azul-gris), piel (manos) y chamarra (verde olivo)
    denim = a & (b_ >= r_ - 4) & (mx > 60) & (yy > top + (bottom - top) * 0.45)
    skin = a & (r_ > g_ + 18) & (r_ > b_ + 35) & (mx > 85)
    jacket = a & (g_ >= b_ + 12) & (r_ <= g_ + 18) & (mx > 60)

    rows_with_denim = np.nonzero(denim.sum(axis=1) >= 3)[0]
    hip = int(rows_with_denim.min()) if len(rows_with_denim) else int(top + (bottom - top) * hip_frac)

    # Piernas = todo lo opaco bajo la cadera que no sea piel ni chamarra; se descartan trozos sueltos (contorno de las manos)
    leg = a & (yy >= hip) & ~skin & ~jacket
    lab, n = ndimage.label(leg)
    if n > 0:
        sizes = ndimage.sum(leg, lab, range(1, n + 1))
        keep = [i + 1 for i, s in enumerate(sizes) if s >= 0.15 * sizes.max()]
        leg = np.isin(lab, keep)
    # Incluir el contorno oscuro pegado a las piernas
    leg = leg | (a & (yy >= hip) & ~skin & ~jacket & ndimage.binary_dilation(leg, iterations=2))

    hip_cols = np.nonzero(leg[hip:hip + 4].any(axis=0))[0]
    split = (hip_cols.min() + hip_cols.max()) / 2.0 if len(hip_cols) else W / 2.0
    foot = leg[bottom - 8:bottom + 1].any(axis=0)
    fx = np.nonzero(foot)[0]
    f0, f1 = (fx.min(), fx.max()) if len(fx) else (split - 10, split + 10)
    depth = max(1, bottom - hip)

    xx = np.arange(W)[None, :].repeat(H, 0)
    back_mask = leg & (xx < split)
    front_mask = leg & (xx >= split)

    back_angle = math.degrees(math.atan2(split - f0, depth))
    front_angle = math.degrees(math.atan2(f1 - split, depth))

    def piece(mask):
        p = np.zeros_like(frame); p[mask] = frame[mask]
        return Image.fromarray(p)

    out = frame.copy()
    # Borrar las piernas originales y su borde suavizado (pixeles semitransparentes)
    halo = ndimage.binary_dilation(leg, iterations=3) & (yy >= hip) & ~skin & ~jacket
    out[leg | halo] = 0
    base = Image.fromarray(out)
    pivot = (split, hip)
    # Las piezas incluyen el borde suavizado para no perder el antialias
    back_mask = (leg | halo) & (xx < split)
    front_mask = (leg | halo) & (xx >= split)
    # PIL rota en sentido antihorario con ángulos positivos (con y hacia abajo, el pie se mueve a la derecha con ángulo positivo)
    back_img = piece(back_mask).rotate(2 * back_angle * t * stride_scale, resample=Image.BICUBIC, center=pivot)
    front_img = piece(front_mask).rotate(-2 * front_angle * t * stride_scale, resample=Image.BICUBIC, center=pivot)
    base.alpha_composite(back_img)
    base.alpha_composite(front_img)
    return np.array(base)


def leg_cycle_shear(frame, hip_frac, t, stride_scale=1.0):
    """
    Mueve las piernas de un cuadro de perfil (mirando a la derecha) que tiene una zancada fija.
    t=0: zancada original; t=0.5: piernas juntas bajo la cadera; t=1: zancada invertida.
    Cada pierna se desplaza en horizontal de forma proporcional a la distancia desde la cadera,
    así los zapatos conservan su orientación (siempre apuntan hacia el frente).
    """
    a = frame[:, :, 3] > 40
    H, W = a.shape
    ys, xs = np.nonzero(a)
    top, bottom = ys.min(), ys.max()
    hip = int(top + (bottom - top) * hip_frac)

    hip_row = np.nonzero(a[hip])[0]
    split = int((hip_row.min() + hip_row.max()) / 2) if len(hip_row) else W // 2
    foot_rows = a[bottom - 6:bottom + 1].any(axis=0)
    fx = np.nonzero(foot_rows)[0]
    back_reach = max(4, split - fx.min()) if len(fx) else 10
    front_reach = max(4, fx.max() - split) if len(fx) else 10

    out = frame.copy()
    out[hip:] = 0
    legs = frame[hip:].copy()
    back = legs.copy(); back[:, split:] = 0
    front = legs.copy(); front[:, :split] = 0
    depth = max(1, bottom - hip)

    def shear(layer, amount):
        res = np.zeros_like(layer)
        for i in range(layer.shape[0]):
            k = i / depth
            dx = int(round(amount * k))
            row = layer[i]
            if dx > 0:
                res[i, dx:] = row[:W - dx]
            elif dx < 0:
                res[i, :W + dx] = row[-dx:]
            else:
                res[i] = row
        return res

    b = shear(back, +2 * back_reach * t * stride_scale)
    fr = shear(front, -2 * front_reach * t * stride_scale)
    # Pierna lejana primero, cercana encima
    region = out[hip:]
    for layer in (b, fr):
        m = layer[:, :, 3] > 0
        region[m] = layer[m]
    out[hip:] = region
    return out


def compose_sheet(rows_of_frames, cell_w, cell_h, baseline):
    """Coloca cada cuadro alineado por los pies (fondo de la franja) y centrado en su celda."""
    cols = max(len(r) for r in rows_of_frames)
    sheet = np.zeros((cell_h * len(rows_of_frames), cell_w * cols, 4), np.uint8)
    for r, frames in enumerate(rows_of_frames):
        for c, f in enumerate(frames):
            h, w = f.shape[:2]
            y1 = (r + 1) * cell_h - baseline
            y0 = y1 - h
            x0 = c * cell_w + (cell_w - w) // 2
            dst = sheet[max(0, y0):y1, x0:x0 + w]
            src = f[max(0, -y0):, :dst.shape[1]]
            m = src[:, :, 3] > 0
            dst[m] = src[m]
    return sheet


def scale(f, k):
    if abs(k - 1) < 1e-3: return f
    im = Image.fromarray(f)
    return np.array(im.resize((max(1, int(round(f.shape[1] * k))), max(1, int(round(f.shape[0] * k)))), Image.LANCZOS))


# ---------------- COYOTE ----------------
coy = load("Enemies/coyote_part1.png")
src = [clean_cell(f) for f in cells(coy, 6, 3, 0)]
ts = [0.0, 0.33, 0.66, 1.0, 0.66, 0.33]
coy_frames = [leg_cycle(src[i], 0.60, ts[i]) for i in range(6)]
coy_sheet = compose_sheet([coy_frames], 112, 125, 4)

# ---------------- MATEO ----------------
# Mateo de perfil y de frente: mateoFrente.png (PPU 116) reescalado al tamaño de mateo_part1 (PPU 100)
front_sheet = load("Characters/mateoFrente.png")
side = [scale(clean_cell(f), 0.89) for f in cells(front_sheet, 6, 2, 0)]
down = [scale(clean_cell(f), 0.90) for f in cells(front_sheet, 6, 2, 1)]
# De espaldas: mateo_part2 fila 0 cuadros 2,3,4 (paso) + los mismos espejados (paso con la otra pierna)
back_sheet = load("Characters/mateo_part2.png")
back_src = [clean_cell(f) for f in cells(back_sheet, 6, 2, 0)]
up = [back_src[2], back_src[3], back_src[4]] + [np.ascontiguousarray(back_src[i][:, ::-1]) for i in (2, 3, 4)]
mateo_sheet = compose_sheet([side, down, up], 128, 236, 10)

if PREVIEW:
    def show(sheet, name, zoom=1):
        bg = Image.new("RGBA", (sheet.shape[1], sheet.shape[0]), (120, 120, 140, 255))
        bg.alpha_composite(Image.fromarray(sheet))
        if zoom != 1: bg = bg.resize((bg.size[0] * zoom, bg.size[1] * zoom), Image.NEAREST)
        bg.convert("RGB").save(SCRATCH + name)
    show(coy_sheet, "coy_walk_preview.png", 1)
    show(mateo_sheet, "mateo_walk_preview.png", 1)
    print("preview ok", coy_sheet.shape, mateo_sheet.shape)
else:
    Image.fromarray(coy_sheet).save(ASSETS + "Enemies/coyote_walk_gen.png")
    Image.fromarray(mateo_sheet).save(ASSETS + "Characters/mateo_walk_gen.png")
    print("saved", coy_sheet.shape, mateo_sheet.shape)
