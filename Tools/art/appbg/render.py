#!/usr/bin/env python3
"""
アプリの UI の背景を描く（2026-11-19）。決まりは Tools/art/appbg/FORMAT.md。
  python3 Tools/art/appbg/render.py                     全部 → Assets/Resources/AppBackgrounds/Bg_<ラベル>.png
  python3 Tools/art/appbg/render.py --only Slack --out DIR [--preview]
  python3 Tools/art/appbg/render.py --sheet FILE.png    全部を並べた一覧
"""
import argparse
import glob
import json
import os
import random
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
OUT_DEFAULT = os.path.join(ROOT, "Assets", "Resources", "AppBackgrounds")
W, H = 320, 180

# ───────── 3×5 の字 ─────────
FONT = {
    "A": ["010", "101", "111", "101", "101"], "B": ["110", "101", "110", "101", "110"],
    "C": ["011", "100", "100", "100", "011"], "D": ["110", "101", "101", "101", "110"],
    "E": ["111", "100", "110", "100", "111"], "F": ["111", "100", "110", "100", "100"],
    "G": ["011", "100", "101", "101", "011"], "H": ["101", "101", "111", "101", "101"],
    "I": ["111", "010", "010", "010", "111"], "J": ["001", "001", "001", "101", "010"],
    "K": ["101", "101", "110", "101", "101"], "L": ["100", "100", "100", "100", "111"],
    "M": ["101", "111", "111", "101", "101"], "N": ["110", "101", "101", "101", "101"],
    "O": ["010", "101", "101", "101", "010"], "P": ["110", "101", "110", "100", "100"],
    "Q": ["010", "101", "101", "110", "011"], "R": ["110", "101", "110", "101", "101"],
    "S": ["011", "100", "010", "001", "110"], "T": ["111", "010", "010", "010", "010"],
    "U": ["101", "101", "101", "101", "111"], "V": ["101", "101", "101", "101", "010"],
    "W": ["101", "101", "111", "111", "101"], "X": ["101", "101", "010", "101", "101"],
    "Y": ["101", "101", "010", "010", "010"], "Z": ["111", "001", "010", "100", "111"],
    "0": ["111", "101", "101", "101", "111"], "1": ["010", "110", "010", "010", "111"],
    "2": ["110", "001", "010", "100", "111"], "3": ["110", "001", "010", "001", "110"],
    "4": ["101", "101", "111", "001", "001"], "5": ["111", "100", "110", "001", "110"],
    "6": ["011", "100", "111", "101", "111"], "7": ["111", "001", "010", "010", "010"],
    "8": ["111", "101", "111", "101", "111"], "9": ["111", "101", "111", "001", "110"],
    " ": ["000"] * 5, "#": ["01010", "11111", "01010", "11111", "01010"], "@": ["010", "101", "111", "100", "011"],
    ".": ["000", "000", "000", "000", "010"], ",": ["000", "000", "000", "010", "100"],
    ":": ["000", "010", "000", "010", "000"], ";": ["000", "010", "000", "010", "100"],
    "-": ["000", "000", "111", "000", "000"], "+": ["000", "010", "111", "010", "000"],
    "/": ["001", "001", "010", "100", "100"], "%": ["101", "001", "010", "100", "101"],
    "!": ["010", "010", "010", "000", "010"], "?": ["110", "001", "010", "000", "010"],
    "(": ["001", "010", "010", "010", "001"], ")": ["100", "010", "010", "010", "100"],
    "[": ["011", "010", "010", "010", "011"], "]": ["110", "010", "010", "010", "110"],
    "<": ["001", "010", "100", "010", "001"], ">": ["100", "010", "001", "010", "100"],
    "_": ["000", "000", "000", "000", "111"], "=": ["000", "111", "000", "111", "000"],
    "'": ["010", "010", "000", "000", "000"], '"': ["101", "101", "000", "000", "000"],
    "*": ["000", "101", "010", "101", "000"], "&": ["010", "101", "010", "101", "011"],
    "$": ["011", "110", "010", "011", "110"], "~": ["000", "011", "110", "000", "000"],
    "|": ["010", "010", "010", "010", "010"],
}

# ───────── 7×7 の印 ─────────
ICONS = {
    "avatar": ["0011100", "0111110", "0111110", "0011100", "0000000", "0111110", "1111111"],
    "hash": ["0100100", "1111111", "0100100", "0100100", "1111111", "0100100", "0000000"],
    "bell": ["0001000", "0011100", "0111110", "0111110", "0111110", "1111111", "0001000"],
    "search": ["0111000", "1000100", "1000100", "1000100", "0111100", "0000110", "0000011"],
    "plus": ["0001000", "0001000", "0001000", "1111111", "0001000", "0001000", "0001000"],
    "play": ["0100000", "0110000", "0111000", "0111100", "0111000", "0110000", "0100000"],
    "pause": ["0110110", "0110110", "0110110", "0110110", "0110110", "0110110", "0110110"],
    "next": ["1000101", "1100101", "1110101", "1111101", "1110101", "1100101", "1000101"],
    "prev": ["1010001", "1010011", "1010111", "1011111", "1010111", "1010011", "1010001"],
    "heart": ["0110110", "1111111", "1111111", "1111111", "0111110", "0011100", "0001000"],
    "star": ["0001000", "0001000", "1111111", "0111110", "0011100", "0110110", "1000001"],
    "check": ["0000001", "0000011", "0000110", "1001100", "1111000", "0110000", "0000000"],
    "gear": ["0010100", "0111110", "1110111", "1100011", "1110111", "0111110", "0010100"],
    "mic": ["0011100", "0011100", "0011100", "1011101", "0111110", "0001000", "0011100"],
    "camera": ["0000000", "1111100", "1111110", "1111111", "1111110", "1111100", "0000000"],
    "folder": ["1110000", "1111111", "1000001", "1000001", "1000001", "1111111", "0000000"],
    "file": ["1111100", "1000110", "1000011", "1000001", "1000001", "1000001", "1111111"],
    "chat": ["1111111", "1000001", "1000001", "1111111", "0110000", "0100000", "0000000"],
    "home": ["0001000", "0011100", "0111110", "1111111", "0110110", "0110110", "0110110"],
    "user": ["0011100", "0011100", "0011100", "0000000", "0111110", "1111111", "1111111"],
    "close": ["1000001", "0100010", "0010100", "0001000", "0010100", "0100010", "1000001"],
    "menu": ["1111111", "0000000", "0000000", "1111111", "0000000", "0000000", "1111111"],
    "lock": ["0011100", "0100010", "0100010", "1111111", "1110111", "1110111", "1111111"],
    "cloud": ["0000000", "0011100", "0111110", "1111111", "1111111", "0000000", "0000000"],
    "music": ["0001111", "0001001", "0001001", "0001001", "0111001", "1111111", "0110110"],
    "grid": ["1101101", "1101101", "0000000", "1101101", "1101101", "0000000", "1101101"],
    "list": ["1101111", "0000000", "1101111", "0000000", "1101111", "0000000", "1101111"],
    "pen": ["0000011", "0000111", "0001110", "0011100", "0111000", "1110000", "1100000"],
    "brush": ["0000011", "0000110", "0001100", "0011000", "0110000", "1110000", "1100000"],
    "cube": ["0011100", "0111110", "1111111", "1011101", "1001001", "0101010", "0011100"],
    "sun": ["1001001", "0101010", "0011100", "1111111", "0011100", "0101010", "1001001"],
    "moon": ["0011100", "0110000", "1100000", "1100000", "1100000", "0110000", "0011100"],
    "leaf": ["0000111", "0011111", "0111111", "0111110", "1111100", "1011000", "1000000"],
    "bolt": ["0001100", "0011000", "0110000", "1111110", "0001100", "0011000", "0110000"],
    "flag": ["1111110", "1111100", "1111110", "1000000", "1000000", "1000000", "1000000"],
    "trophy": ["1111111", "1111111", "0111110", "0011100", "0001000", "0011100", "0111110"],
    "cart": ["1000000", "1111111", "0111110", "0111110", "0111100", "0000000", "0100100"],
    "download": ["0001000", "0001000", "0001000", "0101010", "0011100", "0001000", "1111111"],
    "upload": ["1111111", "0001000", "0011100", "0101010", "0001000", "0001000", "0001000"],
    "shield": ["1111111", "1111111", "1111111", "1111111", "0111110", "0011100", "0001000"],
    "puzzle": ["0011000", "1111110", "1111111", "0111110", "1111111", "1111110", "0011000"],
    "globe": ["0011100", "0101010", "1111111", "1001001", "1111111", "0101010", "0011100"],
    "compass": ["0011100", "0100010", "1000101", "1001001", "1010001", "0100010", "0011100"],
    "terminal": ["1111111", "1000001", "1010001", "1001001", "1010111", "1000001", "1111111"],
    "code": ["0000000", "0100010", "1000001", "1000001", "1000001", "0100010", "0000000"],
    "branch": ["1000000", "1000010", "1000100", "1111000", "1000000", "1000000", "1000000"],
}


def parse_color(c, alpha=1.0):
    if c is None:
        return (0, 0, 0, 0)
    s = str(c).lstrip("#")
    if len(s) == 3:
        r, g, b = (int(ch * 2, 16) for ch in s); a = 255
    elif len(s) == 6:
        r, g, b = int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16); a = 255
    elif len(s) == 8:
        r, g, b, a = int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), int(s[6:8], 16)
    else:
        raise ValueError(f"色が読めない: {c}")
    return (r, g, b, int(round(a * max(0.0, min(1.0, float(alpha))))))


class Canvas:
    def __init__(self, base):
        self.im = Image.new("RGBA", (W, H), parse_color(base))

    def layer(self):
        return Image.new("RGBA", (W, H), (0, 0, 0, 0))

    def paste(self, lay):
        self.im = Image.alpha_composite(self.im, lay)


def draw_glyphs(d, x, y, rows, color, scale=1):
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch == "1":
                d.rectangle([x + i * scale, y + j * scale, x + (i + 1) * scale - 1, y + (j + 1) * scale - 1], fill=color)


def draw_text(d, x, y, text, color, scale=1):
    cx = x
    for ch in str(text):
        g = FONT.get(ch.upper())
        if g is None:
            g = FONT["?"]
        draw_glyphs(d, cx, y, g, color, scale)
        cx += (len(g[0]) + 1) * scale  # ふつうは 3＋1 ドット（# だけ 5 ドット幅）


def draw_icon(d, name, x, y, size, color):
    rows = ICONS.get(name)
    if rows is None:
        raise ValueError(f"知らない icon: {name}")
    k = max(1, int(size) // 7)
    draw_glyphs(d, x, y, rows, color, k)


def draw_element(cv, e, i_rep=None):
    t = e.get("type")
    col = parse_color(e.get("color"), e.get("alpha", 1.0)) if "color" in e else None
    lay = cv.layer()
    d = ImageDraw.Draw(lay)

    def sub(v):
        return str(v).replace("{i}", str(i_rep)) if i_rep is not None else str(v)

    if t == "rect":
        x, y, w, h = e["x"], e["y"], e["w"], e["h"]
        if w > 0 and h > 0:
            d.rectangle([x, y, x + w - 1, y + h - 1], fill=col)
    elif t == "rrect":
        x, y, w, h = e["x"], e["y"], e["w"], e["h"]
        if w > 0 and h > 0:
            d.rounded_rectangle([x, y, x + w - 1, y + h - 1], radius=int(e.get("r", 2)), fill=col)
    elif t == "frame":
        x, y, w, h = e["x"], e["y"], e["w"], e["h"]
        d.rectangle([x, y, x + w - 1, y + h - 1], outline=col, width=int(e.get("width", 1)))
    elif t == "line":
        d.line([e["x1"], e["y1"], e["x2"], e["y2"]], fill=col, width=int(e.get("width", 1)))
    elif t == "hlines":
        x, y, w, h, step = e["x"], e["y"], e["w"], e["h"], max(1, int(e["step"]))
        for yy in range(y, y + h, step):
            d.line([x, yy, x + w - 1, yy], fill=col)
    elif t == "vlines":
        x, y, w, h, step = e["x"], e["y"], e["w"], e["h"], max(1, int(e["step"]))
        for xx in range(x, x + w, step):
            d.line([xx, y, xx, y + h - 1], fill=col)
    elif t == "grid":
        x, y, w, h = e["x"], e["y"], e["w"], e["h"]
        cw, ch = max(1, int(e["cell_w"])), max(1, int(e["cell_h"]))
        for xx in range(x, x + w + 1, cw):
            d.line([xx, y, xx, y + h], fill=col)
        for yy in range(y, y + h + 1, ch):
            d.line([x, yy, x + w, yy], fill=col)
    elif t == "checker":
        x, y, w, h, s = e["x"], e["y"], e["w"], e["h"], max(1, int(e["size"]))
        c1, c2 = parse_color(e["c1"], e.get("alpha", 1.0)), parse_color(e["c2"], e.get("alpha", 1.0))
        for yy in range(y, y + h, s):
            for xx in range(x, x + w, s):
                c = c1 if ((xx - x) // s + (yy - y) // s) % 2 == 0 else c2
                d.rectangle([xx, yy, min(x + w, xx + s) - 1, min(y + h, yy + s) - 1], fill=c)
    elif t == "gradient":
        x, y, w, h = e["x"], e["y"], e["w"], e["h"]
        c1, c2 = parse_color(e["c1"], e.get("alpha", 1.0)), parse_color(e["c2"], e.get("alpha", 1.0))
        steps = int(e.get("steps", 6))
        vert = e.get("dir", "v") == "v"
        n = h if vert else w
        for k in range(steps):
            a0, a1 = n * k // steps, n * (k + 1) // steps
            f = k / max(1, steps - 1)
            c = tuple(int(round(c1[j] + (c2[j] - c1[j]) * f)) for j in range(4))
            if vert:
                d.rectangle([x, y + a0, x + w - 1, y + a1 - 1], fill=c)
            else:
                d.rectangle([x + a0, y, x + a1 - 1, y + h - 1], fill=c)
    elif t == "circle":
        cx, cy, r = e["cx"], e["cy"], e["r"]
        if e.get("fill", True):
            d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col)
        else:
            d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=col)
    elif t == "text":
        draw_text(d, e["x"], e["y"], sub(e["text"]), col, int(e.get("scale", 1)))
    elif t == "bars":
        rng = random.Random(int(e.get("seed", 1)) + (i_rep or 0) * 7919)
        x, y, w = e["x"], e["y"], e["w"]
        rows, row_h, bar_h = int(e["rows"]), int(e.get("row_h", 7)), int(e.get("bar_h", 2))
        mn = float(e.get("min", 0.35)); indent = int(e.get("indent", 0))
        for r in range(rows):
            ln = int(w * (mn + (1 - mn) * rng.random()))
            ox = indent if (indent and rng.random() < 0.3) else 0
            yy = y + r * row_h
            d.rectangle([x + ox, yy, x + ox + max(2, ln - ox) - 1, yy + bar_h - 1], fill=col)
    elif t == "icon":
        draw_icon(d, e["name"], e["x"], e["y"], e.get("size", 7), col)
    elif t == "noise":
        rng = random.Random(int(e.get("seed", 1)))
        x, y, w, h = e["x"], e["y"], e["w"], e["h"]
        dens = float(e.get("density", 0.05))
        for _ in range(int(w * h * dens)):
            d.point((x + rng.randrange(max(1, w)), y + rng.randrange(max(1, h))), fill=col)
    elif t == "repeat":
        n, dx, dy = int(e["count"]), e.get("dx", 0), e.get("dy", 0)
        for k in range(n):
            for it in e["items"]:
                moved = dict(it)
                for kx in ("x", "x1", "x2", "cx"):
                    if kx in moved:
                        moved[kx] = moved[kx] + dx * k
                for ky in ("y", "y1", "y2", "cy"):
                    if ky in moved:
                        moved[ky] = moved[ky] + dy * k
                draw_element(cv, moved, k)
        return
    else:
        raise ValueError(f"知らない type: {t}")
    cv.paste(lay)


def render(spec):
    cv = Canvas(spec.get("base", "#808080"))
    for e in spec.get("elements", []):
        draw_element(cv, e)
    veils = spec.get("veil")
    if veils:
        for v in (veils if isinstance(veils, list) else [veils]):
            draw_element(cv, {"type": "rect", "x": v["x"], "y": v["y"], "w": v["w"], "h": v["h"],
                              "color": v["color"], "alpha": v.get("alpha", 0.2)})
    return cv.im.convert("RGBA")


# ───────── ステージの足場とギミック（FORMAT.md「足場とギミック」） ─────────
PPU = 15.0
CAM_X, CAM_Y = 0.0, 5.5          # 1画面のステージのカメラの真ん中（ワールド座標）
FOLDER_SLOTS = ((100, 115), (205, 220))  # 今のフォルダの足場の真ん中あたり（x。上の面は y 127〜128）
BEHAVIORS = ("static", "move", "blink", "scroll", "fall", "button")
EFFECTS = ("clear_bullets", "stun_boss", "refill")


def px_to_world(x, y):
    return (CAM_X + (x - W / 2) / PPU, CAM_Y + (H / 2 - y) / PPU)


def render_part(pl):
    """足場1枚の絵（skin の座標は足場の左上が 0,0）。skin が無ければ、白っぽい板"""
    w, h = int(pl["w"]), int(pl["h"])
    cv = Canvas("#00000000")
    cv.im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    skin = pl.get("skin") or [{"type": "rrect", "x": 0, "y": 0, "w": w, "h": h, "r": 1, "color": "#dfe4ea"}]
    for e in skin:
        draw_element(cv, e)
    return cv.im.crop((0, 0, w, h))


def check_stage(spec):
    """足場の決まりを調べて、注意の文の配列を返す"""
    st = spec.get("stage") or {}
    pls = st.get("platforms") or []
    warns = []
    if not pls:
        return warns
    if len(pls) > 8:
        warns.append(f"足場が多い（{len(pls)}。8 まで）")
    ids = set()
    tops = []
    for pl in pls:
        pid = pl.get("id", "?")
        if pid in ids:
            warns.append(f"id が2回: {pid}")
        ids.add(pid)
        x, y, w, h = pl["x"], pl["y"], pl["w"], pl["h"]
        b = (pl.get("behavior") or {}).get("type", "static")
        if b not in BEHAVIORS:
            warns.append(f"{pid}: 知らない behavior {b}")
        if b == "button" and (pl.get("behavior") or {}).get("effect") not in EFFECTS:
            warns.append(f"{pid}: button の effect は {EFFECTS} のどれか")
        if x < 12 or x + w > 308:
            warns.append(f"{pid}: 横が壁にかかる（x 12〜308）")
        if y < 60 or y > 160:
            warns.append(f"{pid}: 上の面が高すぎ・低すぎ（y 60〜160）")
        if w < 12 or h < 3:
            warns.append(f"{pid}: 小さすぎる（幅 12・高さ 3 以上）")
        if b != "button":
            tops.append((x, y, w, pid))
    for (a0, a1) in FOLDER_SLOTS:
        if not any(abs(y - 127.5) <= 3 and x <= a0 and x + w >= a1 for (x, y, w, _p) in tops):
            warns.append(f"フォルダの場所（x {a0}〜{a1}・y 127）に乗れる足場が無い（雑魚が乗る場所）")
    # 届くか：床（y 172）か、ほかの足場から 46 ドット以内の高さ・横に 50 ドット以内
    surfaces = [(0, 172, 320)] + [(x, y, w) for (x, y, w, _p) in tops]
    for (x, y, w, pid) in tops:
        ok = False
        for (sx, sy, sw) in surfaces:
            if sy <= y:
                continue
            if sy - y <= 46 and not (sx > x + w + 50 or sx + sw < x - 50):
                ok = True
                break
        if not ok:
            warns.append(f"{pid}: 下から届かないかも（ジャンプは約 48 ドット）")
    return warns


def stage_json(spec):
    """Unity が読む足場の表（ワールド座標）"""
    st = spec.get("stage") or {}
    out = {"label": spec["id"], "hideFolders": bool(st.get("hide_folders", True)), "platforms": []}
    for pl in st.get("platforms") or []:
        x, y, w, h = pl["x"], pl["y"], pl["w"], pl["h"]
        cx, cy = px_to_world(x + w / 2, y + h / 2)
        bh = dict(pl.get("behavior") or {"type": "static"})
        conv = {}
        for k, v in bh.items():
            if k in ("dx", "speed", "range"):
                conv[k] = v / PPU
            elif k == "dy":
                conv[k] = -v / PPU  # 絵は下向きが +、ワールドは上向きが +
            else:
                conv[k] = v
        out["platforms"].append({
            "id": pl["id"], "x": round(cx, 4), "y": round(cy, 4), "w": round(w / PPU, 4), "h": round(h / PPU, 4),
            "top": round(px_to_world(0, y)[1], 4), "sprite": f"AppBackgrounds/Parts/{spec['id']}__{pl['id']}",
            "behavior": bh.get("type", "static"), "params": conv,
        })
    return out


def preview(img, spec=None):
    """HUD・壁・床・足場・敵・プレイヤー・予告を重ねて、3倍にした見本"""
    im = img.copy()
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 9, H - 1], fill=(43, 43, 51, 255))
    d.rectangle([310, 0, W - 1, H - 1], fill=(43, 43, 51, 255))
    d.rectangle([0, 170, W - 1, H - 1], fill=(90, 140, 190, 255))
    d.rectangle([0, 172, W - 1, H - 1], fill=(60, 60, 70, 255))
    # HUD
    d.rectangle([3, 3, 50, 20], fill=(220, 220, 220, 255), outline=(255, 255, 255, 255))
    d.rectangle([55, 8, 80, 14], fill=(66, 193, 15, 255))
    for k in range(3):
        d.rectangle([87 + k * 21, 3, 87 + k * 21 + 17, 20], fill=(200, 200, 200, 255))
    d.rectangle([244, 3, 316, 13], fill=(240, 240, 240, 255), outline=(60, 60, 60, 255))
    d.rectangle([242, 19, 316, 25], fill=(40, 40, 40, 255))
    # 足場（ステージの足場があればそれ、無ければ見本の板）
    pls = ((spec or {}).get("stage") or {}).get("platforms") or []
    if pls:
        for pl in pls:
            part = render_part(pl)
            im.alpha_composite(part, (int(pl["x"]), int(pl["y"])))
            b = (pl.get("behavior") or {}).get("type", "static")
            if b != "static":
                # 動く・消える足場には、小さい印（見本だけ）
                d.rectangle([int(pl["x"]), int(pl["y"]) - 3, int(pl["x"]) + 2, int(pl["y"]) - 1],
                            fill={"move": (80, 200, 255, 255), "blink": (255, 200, 60, 255), "scroll": (120, 255, 160, 255),
                                  "fall": (255, 140, 60, 255), "button": (255, 80, 220, 255)}.get(b, (255, 255, 255, 255)))
    else:
        for x0, y0 in ((90, 128), (195, 128), (140, 95)):
            d.rectangle([x0, y0, x0 + 35, y0 + 3], fill=(200, 230, 245, 255), outline=(30, 30, 40, 255))
    # 予告の赤い線・赤い枠
    d.line([20, 110, 300, 110], fill=(230, 40, 40, 255), width=1)
    d.rectangle([240, 60, 280, 100], outline=(230, 40, 40, 255))
    # 敵（黒いもじゃもじゃ＋赤い目）
    for cx, cy in ((160, 160), (280, 160), (107, 118), (215, 118)):
        d.ellipse([cx - 5, cy - 5, cx + 5, cy + 5], fill=(10, 10, 12, 255))
        for k in range(8):
            d.point((cx - 6 + (k * 3) % 13, cy - 6 + (k * 5) % 13), fill=(70, 70, 75, 255))
        d.point((cx - 2, cy - 1), fill=(255, 30, 30, 255)); d.point((cx + 2, cy - 1), fill=(255, 30, 30, 255))
    # プレイヤー
    d.rectangle([30, 158, 37, 169], fill=(240, 220, 90, 255)); d.rectangle([32, 160, 35, 162], fill=(60, 30, 90, 255))
    # 弾
    for cx, cy, c in ((70, 140, (255, 255, 255, 255)), (120, 70, (120, 220, 255, 255)), (230, 140, (255, 200, 60, 255))):
        d.ellipse([cx - 2, cy - 2, cx + 2, cy + 2], fill=c)
    return im.resize((W * 3, H * 3), Image.NEAREST)


def load_specs(only):
    specs = []
    for p in sorted(glob.glob(os.path.join(HERE, "apps", "*.json"))):
        with open(p, encoding="utf-8") as f:
            s = json.load(f)
        s.setdefault("id", os.path.basename(p)[:-5])
        if only and s["id"] not in only:
            continue
        specs.append((p, s))
    return specs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--out", default=OUT_DEFAULT)
    ap.add_argument("--preview", action="store_true")
    ap.add_argument("--sheet", default="")
    a = ap.parse_args()
    only = set(x.strip() for x in a.only.split(",") if x.strip())
    specs = load_specs(only)
    if not specs:
        print("レイアウトが無い（Tools/art/appbg/apps/*.json）")
        return 1
    errors = 0
    images = []
    for p, s in specs:
        try:
            img = render(s)
        except Exception as ex:  # noqa: BLE001
            errors += 1
            print(f"まちがい: {os.path.basename(p)}: {ex}")
            continue
        images.append((s["id"], img, s))
        for w_ in check_stage(s):
            print(f"注意: {s['id']}: {w_}")
        if not a.sheet:
            os.makedirs(a.out, exist_ok=True)
            img.save(os.path.join(a.out, f"Bg_{s['id']}.png"))
            pls = (s.get("stage") or {}).get("platforms") or []
            stage_path = os.path.join(a.out, f"Stage_{s['id']}.json")
            if pls:
                os.makedirs(os.path.join(a.out, "Parts"), exist_ok=True)
                for pl in pls:
                    render_part(pl).save(os.path.join(a.out, "Parts", f"{s['id']}__{pl['id']}.png"))
                with open(stage_path, "w", encoding="utf-8") as f:
                    json.dump(stage_json(s), f, ensure_ascii=False, indent=1)
            elif os.path.exists(stage_path):
                os.remove(stage_path)
            if a.preview:
                os.makedirs(os.path.join(a.out, "preview"), exist_ok=True)
                preview(img, s).save(os.path.join(a.out, "preview", f"Preview_{s['id']}.png"))
    if a.sheet:
        cols = 4
        rows = (len(images) + cols - 1) // cols
        sheet = Image.new("RGBA", (cols * (W + 8) + 8, rows * (H + 18) + 8), (24, 24, 28, 255))
        sd = ImageDraw.Draw(sheet)
        for k, (name, img, spec) in enumerate(images):
            cx, cy = 8 + (k % cols) * (W + 8), 8 + (k // cols) * (H + 18)
            sheet.paste(preview(img, spec).resize((W, H), Image.NEAREST), (cx, cy))
            draw_text(sd, cx, cy + H + 4, name, (230, 230, 230, 255), 2)
        sheet.save(a.sheet)
        print(f"一覧: {a.sheet}（{len(images)} 個）")
    else:
        print(f"描いた {len(images)} 個・まちがい {errors} 個 → {a.out}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
