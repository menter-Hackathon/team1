# -*- coding: utf-8 -*-
"""ウイルスのドット絵の部品（2026-11-20。コンセプト画 make_virus.py の A 方向〔スス玉〕から写した）。
絵の形はコンセプトのシートと同じ。書き出しは build_virus.py。"""
import math, random
from PIL import Image, ImageDraw

K = (13, 13, 13); G1 = (77, 77, 77); G2 = (143, 143, 143); R = (255, 0, 0); W = (255, 255, 255)
GOLD = (248, 200, 48); GOLDD = (178, 124, 20); STEEL = (186, 192, 204); STEELD = (104, 112, 130)
CYAN = (64, 224, 240); LIME = (140, 226, 60); PURP = (170, 104, 232); ORNG = (255, 156, 40)
AMBER = (255, 196, 64); BLUE = (66, 159, 236); BLUEL = (128, 198, 252); PAPER = (246, 246, 240)
MAG = (238, 80, 208); GREEN = (64, 220, 144); WOOD = (168, 112, 64); CODE = (0, 168, 96)
BIO = (176, 234, 92); SLAB = (122, 132, 152); SLABD = (88, 96, 114)
LIGHT_BG = (206, 211, 219); SLATE_BG = (84, 96, 116)
FW, FH, GY = 26, 24, 22   # frame size, ground row


class C:
    def __init__(s, w=FW, h=FH):
        s.w, s.h = w, h
        s.im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        s.body = set()

    def p(s, x, y, c, body=False):
        x, y = int(x), int(y)
        if 0 <= x < s.w and 0 <= y < s.h:
            s.im.putpixel((x, y), tuple(c) + (255,))
            if body: s.body.add((x, y))
            else: s.body.discard((x, y))

    def g(s, x, y):
        if 0 <= x < s.w and 0 <= y < s.h:
            v = s.im.getpixel((x, y))
            return v[:3] if v[3] else None
        return None

    def _mask(s, fn, c, body):
        m = Image.new('L', (s.w, s.h), 0); fn(ImageDraw.Draw(m))
        for y in range(s.h):
            for x in range(s.w):
                if m.getpixel((x, y)): s.p(x, y, c, body)

    def ell(s, cx, cy, rx, ry, c, body=False):
        s._mask(lambda d: d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255), c, body)

    def poly(s, pts, c, body=False):
        s._mask(lambda d: d.polygon(pts, fill=255, outline=255), c, body)

    def rect(s, x0, y0, x1, y1, c, body=False):
        for y in range(int(y0), int(y1) + 1):
            for x in range(int(x0), int(x1) + 1): s.p(x, y, c, body)

    def line(s, pts, c, body=False):
        s._mask(lambda d: d.line(pts, fill=255), c, body)

    def grid(s, x, y, rows, pal):
        for j, row in enumerate(rows):
            for i, ch in enumerate(row):
                if ch in pal: s.p(x + i, y + j, pal[ch])

    def fuzz(s, seed, p1=0.8, p2=0.3):
        rnd = random.Random(seed)
        ring = set()
        for (x, y) in sorted(s.body):
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                q = (x + dx, y + dy)
                if s.g(*q) is None and 0 <= q[0] < s.w and 0 <= q[1] < s.h: ring.add(q)
        r1 = [q for q in sorted(ring) if rnd.random() < p1]
        for q in r1: s.p(q[0], q[1], G1)
        for (x, y) in r1:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                q = (x + dx, y + dy)
                if s.g(*q) is None and rnd.random() < p2: s.p(q[0], q[1], G2)

    def paste(s, o, dx=0, dy=0):
        for y in range(o.h):
            for x in range(o.w):
                c = o.g(x, y)
                if c is not None: s.p(x + dx, y + dy, c)

    def outlined(s, c=K):
        add = []
        for y in range(s.h):
            for x in range(s.w):
                if s.g(x, y) is None and any(s.g(x + dx, y + dy) is not None for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    add.append((x, y))
        for q in add: s.p(q[0], q[1], c)
        return s


# ---------- shared kit ----------
def eyes(c, cx, y, gap=1, look=0, size=2, col=R):
    for sx in (cx - gap - size + look, cx + gap + look + 1):
        c.rect(sx, y, sx + size - 1, y + size - 1, col)
        c.p(sx + (size - 1 if look > 0 else 0), y, W)


def body(c, st, cx, cy, rx, ry, seed, f=0, bumps=3):
    """Style-dependent base body. A=soot fuzz, B=bio capsid, C=glitch blocks, D=dead app icon."""
    rnd = random.Random(seed)
    if st == 'A':
        c.ell(cx, cy, rx, ry, K, True)
        for i in range(bumps):
            a = rnd.uniform(0, 6.28)
            c.ell(cx + math.cos(a) * rx * .75, cy + math.sin(a) * ry * .7, rx * .42, ry * .42, K, True)
        c.fuzz(seed * 7 + f % 2, 0.8, 0.3)
    elif st == 'B':
        pts = [(cx, cy - ry), (cx + rx, cy - ry / 2), (cx + rx, cy + ry / 2), (cx, cy + ry), (cx - rx, cy + ry / 2), (cx - rx, cy - ry / 2)]
        # spike proteins first (behind)
        n = 8 if rx >= 4 else 5
        for i in range(n):
            a = (i + 0.5 * (f % 2)) / n * 6.283 + 0.4
            if 0.35 < (a % 6.283) / 3.1416 < 0.65: continue   # keep the underside free for legs
            x0, y0 = cx + math.cos(a) * rx * .8, cy + math.sin(a) * ry * .8
            x1, y1 = cx + math.cos(a) * (rx + 2.2), cy + math.sin(a) * (ry + 2.2)
            c.line([(x0, y0), (x1, y1)], K)
            c.p(round(cx + math.cos(a) * (rx + 3.0)), round(cy + math.sin(a) * (ry + 3.0)), BIO)
        c.poly(pts, K, True)
        # facet highlights (icosahedral capsid)
        c.line([(cx - rx + 1, cy - ry / 2 + 1), (cx, cy - ry + 1)], G1)
        c.line([(cx, cy - ry + 2), (cx, cy - ry + 3)], G1)
        c.p(round(cx + rx - 1), round(cy + ry / 2 - 1), G1)
    elif st == 'C':
        x0, x1, y0, y1 = round(cx - rx), round(cx + rx), round(cy - ry), round(cy + ry)
        r2 = random.Random(seed * 13 + f)
        for y in range(y0, y1 + 1):
            off = r2.choice([0, 0, 0, 0, -2, 2, 1, -1]) if (y - y0) % 2 == 0 else None
            if off is None: off = prev
            prev = off
            for x in range(x0, x1 + 1): c.p(x + off, y, K, True)
            if off:
                c.p(x0 + off - 1, y, CYAN); c.p(x1 + off + 1, y, MAG)
        for i in range(3):   # corrupted code lines inside
            yy = y0 + 2 + i * max(2, (y1 - y0) // 3) + (1 if i else ry >= 4 and 4 or 3)
            if yy < y1:
                xx = x0 + 1 + r2.randint(0, 2)
                for k in range(r2.randint(2, max(2, x1 - x0 - 3))):
                    if c.g(xx + k, yy) == K and (k % 4) != 2: c.p(xx + k, yy, CODE, True)
        for i in range(4):   # detached pixels
            a = r2.uniform(0, 6.28); d = r2.uniform(1.4, 1.8)
            px, py = round(cx + math.cos(a) * rx * d), round(cy + math.sin(a) * ry * d)
            if c.g(px, py) is None and py < GY: c.p(px, py, K if i % 2 else G1)
    elif st == 'D':
        x0, x1, y0, y1 = round(cx - rx), round(cx + rx), round(cy - ry), round(cy + ry)
        c.rect(x0 + 2, y0, x1 - 2, y1, K, True); c.rect(x0, y0 + 2, x1, y1 - 2, K, True)
        c.rect(x0 + 1, y0 + 1, x1 - 1, y1 - 1, K, True)
        c.line([(x0 + 2, y0 + 1), (x1 - 3, y0 + 1)], G1); c.p(x0 + 1, y0 + 2, G1)   # icon gloss
        for i in range(3):   # melting drips
            dx = rnd.randint(x0 + 1, x1 - 1); ln = rnd.randint(1, 3) + (f % 2 if i == 0 else 0)
            for k in range(ln): c.p(dx, y1 + 1 + k, K, True)
        c.fuzz(seed * 5 + f % 2, 0.18, 0.5)


def legs(c, st, cx, ytop, f, spread=2, gy=GY):
    ytop = int(round(ytop)); cx = int(round(cx))
    if st in ('A', 'D'):
        for i, sx in enumerate((cx - spread, cx + spread)):
            lift = 1 if (f % 2 == i and f % 4 in (1, 3) or (f % 4 == 1 and i == 0)) else 0
            lift = 1 if (f % 2 == 1 and i == (f // 2) % 2) else 0
            for y in range(ytop, gy + 1 - lift): c.p(sx, y, K)
            c.p(sx + 1, gy - lift, K)
    elif st == 'B':   # bacteriophage tail fibres
        k = f % 2
        for sgn in (-1, 1):
            c.line([(cx + sgn * 1, ytop - 1), (cx + sgn * (3 + k), ytop + 1), (cx + sgn * (3 + k), gy)], K)
            c.line([(cx + sgn * 3, ytop - 2), (cx + sgn * (6 - k), ytop), (cx + sgn * (6 - k), gy)], K)
        c.line([(cx, ytop - 1), (cx, gy - 2)], K)   # tail sheath
        c.p(cx, gy - 1, G1)
    elif st == 'C':   # hovering: flicker shadow pixels
        for i, dx in enumerate((-3, -1, 2, 4)):
            if (i + f) % 2 == 0: c.p(cx + dx, gy, G1)
        c.p(cx - 1 + f % 2, gy - 2, K)


# ---------- types that exist in every style ----------
def t_ransom(st='A'):
    fr = []
    for f in range(3):
        c = C(); bob = f % 2; cx, cy = 12, 13 + bob
        # chain dragging behind
        for i in range(6):
            c.p(cx - 6 - i, min(GY, cy + 3 + i), STEEL if i % 2 == 0 else STEELD)
        for i in range(2): c.p(cx - 11 - i * 0, GY, STEELD)
        legs(c, st, cx, cy + 4, f)
        # shackle arch (behind body, sticks out above the head)
        sh = C(); top = cy - 9 + (2 if f == 2 else 0)
        sh.rect(cx - 3, top, cx + 3, top + 1, STEEL); sh.rect(cx - 4, top + 1, cx - 3, cy - 3, STEEL); sh.rect(cx + 3, top + 1, cx + 4, cy - 3, STEEL)
        sh.p(cx - 2, top, W); sh.p(cx - 1, top, W)
        c.paste(sh.outlined())
        if st == 'A': body(c, st, cx, cy, 5.5, 4.8, 11, f, bumps=2)
        else: body(c, st, cx, cy, 5.5 if st != 'B' else 6, 5, 11, f)
        eyes(c, cx, cy - 3, 1)
        c.rect(cx - 2, cy, cx + 2, cy + 3, GOLD); c.rect(cx - 2, cy + 3, cx + 2, cy + 3, GOLDD)
        c.p(cx, cy + 1, K); c.p(cx, cy + 2, K)
        if f == 2:   # "lock!" snap spark
            for q in ((cx - 6, top), (cx + 6, top), (cx, top - 2)): c.p(q[0], q[1], GOLD)
        fr.append(c)
    return fr


def t_worm(st='A', bud=True):
    """bud=False：しっぽの芽（分裂）を描かない（ゲーム用。分裂は作らないため）"""
    fr = []
    segs = [(19, 4.0), (13, 3.2), (8, 2.7), (4, 2.0)]
    for f in range(4):
        c = C()
        for i in reversed(range(4)):
            x, r = segs[i]
            y = GY - r - 1 + round(math.sin(f * math.pi / 2 + i * 1.5) * 1.2) - (1 if i == 0 else 0)
            x += (1 if (f + i) % 2 else 0)
            if st == 'C' and i == 3:
                for k in range(5): c.p(x - 2 + (k * 3 + f) % 5, y - 2 + (k * 2 + f) % 5, K if k % 2 else G1)
                continue
            if st == 'B' and i > 0:
                c.ell(x, y, r, r, K, True); c.p(round(x), round(y - r - 1), BIO); c.p(round(x), round(y - r), K)
            elif st == 'A': body(c, st, x, y, r, r, 30 + i, f, bumps=0)
            else: body(c, st, x, y, r if st != 'D' else r - .3, r if st != 'D' else r - .3, 30 + i, f)
            if i > 0 and st != 'C': c.line([(x, y - r + 1), (x, y + r - 1)], LIME)
            if i == 0:
                eyes(c, round(x) + 1, round(y) - 1, 0, look=1)
                c.p(round(x) + 1, round(y - r) - 2, K); c.p(round(x) + 3, round(y - r) - 3, K); c.p(round(x) + 2, round(y - r) - 1, K)
        # self-replication: a bud grows on the tail and hops off
        if bud and f >= 2:
            bx, by = (5, 13) if f == 2 else (3, 10)
            c.ell(bx, by, 1.4, 1.4, K, True); c.p(bx + 1, by, R)
            if st == 'A': c.p(bx - 2, by, G1); c.p(bx, by - 2, G1)
            c.p(bx + 3, by - 3, LIME); c.p(bx + 4, by - 3, LIME); c.p(bx + 4, by - 4, LIME); c.p(bx + 4, by - 2, LIME)  # "+"
            c.p(bx + 5, by - 3, LIME)
        fr.append(c)
    return fr


def t_spy(st='A'):
    fr = []
    for f in range(4):
        c = C(); cx, cy = 11, 12 + (f % 2)
        if st == 'B': legs(c, st, cx, cy + 5, f)
        if st == 'C': legs(c, st, cx, cy + 5, f)
        if st == 'D': legs(c, st, cx, cy + 5, f)
        body(c, st, cx, cy, 6, 5.5, 21, f, bumps=2)
        if st == 'A':   # wispy tail instead of legs: it floats
            for i in range(3): c.p(cx - 1 + (i + f) % 2, cy + 7 + i, G1 if i else K)
        look = (-1, 0, 1, 0)[f]
        if f == 3:   # blink
            c.line([(cx - 3, cy), (cx + 3, cy)], W)
        else:
            c.ell(cx, cy, 3.2, 2.6, W)
            c.rect(cx - 1 + look, cy - 1, cx + 1 + look, cy + 1, R); c.p(cx + look, cy, K); c.p(cx - 1 + look, cy - 1, W)
        # magnifier ring + handle
        lens = C()
        for a in range(0, 360, 6):
            lens.p(round(cx + math.cos(math.radians(a)) * 4.6), round(cy + math.sin(math.radians(a)) * 4.1), CYAN)
        c.paste(lens)
        h = C(); h.line([(cx + 4, cy + 4), (cx + 8, cy + 8)], WOOD); h.line([(cx + 5, cy + 4), (cx + 9, cy + 8)], WOOD)
        c.paste(h.outlined())
        fr.append(c)
    return fr


# ---------- direction-A-only types ----------
def t_trojan():
    fr = []
    icon_rows = ["..bbbbbbbbb..", ".bLLLLLLLLLb.", "bLLLLLLLLLLLb", "bbbbbbbbbbbbb", "bbbebbbbbebbb", "bbbebbbbbebbb", "bbbbbbbbbbbbb",
                 "bbwbbbbbbbwbb", "bbbwbbbbbwbbb", "bbbbwwwwwbbbb", "bbbbbbbbbbbbb", ".bbbbbbbbbbb.", "..bbbbbbbbb.."]
    for f in range(4):
        c = C(); bob = f % 2; cx, cy = 12, 12 + bob
        legs(c, 'A', cx, cy + 6, f, spread=3)
        body(c, 'A', cx, cy + 1, 7.4, 6.2, 41, f, bumps=0)
        pal = {'b': BLUE, 'L': BLUEL, 'w': W, 'e': W if f == 0 else R}
        if f < 3:
            ic = C(); ic.grid(cx - 6, cy - 6, icon_rows, pal)
            if f == 2:
                for i, dx in enumerate((0, 1, 0, -1, 0, 1, 1, 0, -1, 0, 1, 0, 0)): ic.p(cx + dx, cy - 6 + i, K)
                ic.p(cx - 2, cy - 1, K); ic.p(cx + 3, cy + 2, K)
            c.paste(ic.outlined())
        else:
            for side in (-1, 1):
                ic = C()
                rows = [r[:6] if side < 0 else r[7:] for r in icon_rows]
                ic.grid(cx - 6 + (0 if side < 0 else 7) + side * 4, cy - 5 + (1 if side > 0 else 3), rows, pal)
                c.paste(ic.outlined())
            c.rect(cx - 3, cy - 5, cx + 3, cy + 5, K, True)
            c.fuzz(77, 0.7, 0.3)
            eyes(c, cx, cy - 2, 1)
            c.line([(cx - 2, cy + 2), (cx + 2, cy + 2)], W); c.p(cx - 1, cy + 2, K); c.p(cx + 1, cy + 2, K)
        fr.append(c)
    return fr


def t_adware():
    fr = []
    for f in range(3):
        c = C(); bob = f % 2; cx, cy = 12, 16 + bob
        legs(c, 'A', cx, cy + 3, f)
        c.line([(cx - 4, cy - 1), (cx - 5, cy - 6)], K); c.line([(cx + 4, cy - 1), (cx + 5, cy - 6)], K)  # arms
        body(c, 'A', cx, cy, 4.4, 3.8, 51, f, bumps=1)
        eyes(c, cx, cy - 2, 1)

        def popup(x, y, w, h, col):
            win = C(); win.rect(x, y, x + w - 1, y + h - 1, col); win.rect(x, y, x + w - 1, y + 1, (229, 229, 234))
            win.p(x + 1, y, G2); win.p(x + 3, y, AMBER); win.p(x + 5, y, GREEN)
            mx = x + w // 2
            win.rect(mx, y + 3, mx, y + h - 4, W); win.p(mx, y + h - 2, W)   # "!"
            c.paste(win.outlined())
        if f >= 1: popup(cx + 3, 0 + bob, 9, 7, MAG)
        if f >= 2: popup(cx - 11, 4, 8, 6, CYAN)
        popup(cx - 6, cy - 14, 13, 8, ORNG)
        fr.append(c)
    return fr


def t_rootkit(slab=True, roots=True, clip=False, frames=4):
    """ゲーム用は slab=False, roots=False, clip=True, frames=3（床の板・根を描かず、床の線〔sy+1 の行〕より下の体を消す）"""
    fr = []
    for f in range(frames):
        c = C(); cx = 12; lvl = (0, 1, 2, 1)[f]; sy = 15
        # roots below the floor
        for sgn in ((-1, 1) if roots else ()):
            c.line([(cx + sgn, sy + 2), (cx + sgn * 4, sy + 4), (cx + sgn * 6, sy + 7)], PURP)
            c.line([(cx + sgn * 4, sy + 4), (cx + sgn * 9, sy + 5)], PURP)
            c.p(cx + sgn * (10 + f % 2), sy + 6, PURP)
        if roots: c.line([(cx, sy + 2), (cx, sy + 7)], PURP)
        if lvl == 2:
            c.line([(cx - 1, 11), (cx - 2, sy + 2)], K); c.line([(cx + 1, 11), (cx + 2, sy + 2)], K); c.line([(cx, 11), (cx, sy + 2)], PURP)
            body(c, 'A', cx, 7, 5.2, 4.6, 61, f, bumps=2); eyes(c, cx, 6, 1)
        elif lvl == 1:
            body(c, 'A', cx, sy, 5.2, 5, 61, f, bumps=0); eyes(c, cx, sy - 3, 1)
        if clip:   # 床の線より下の体を消す（板が無いので、下半分が床の上に出ないように）
            for yy in range(sy + 2, c.h):
                for xx in range(c.w): c.im.putpixel((xx, yy), (0, 0, 0, 0)); c.body.discard((xx, yy))
        # floor slab with a crack
        if slab: c.rect(0, sy, FW - 1, sy + 1, SLAB); c.rect(0, sy + 1, FW - 1, sy + 1, SLABD)
        gap = (3, 5, 2)[lvl]
        c.rect(cx - gap, sy, cx + gap, sy + 1, K)
        if lvl == 0:
            c.p(cx - 2, sy, R); c.p(cx + 1, sy, R)
            c.p(cx - 4, sy - 1, G1); c.p(cx + 4, sy - 1, G1); c.p(cx, sy - 1, G1)
        fr.append(c)
    return fr


def t_botnet():
    fr = []

    def bot(c, cx, cy, r, f, seed, on):
        legs(c, 'A', cx, cy + r - 1, f, spread=1 if r < 4 else 2)
        c.line([(cx, cy - r), (cx, cy - r - 4)], K)
        body(c, 'A', cx, cy, r, r - .4, seed, f, bumps=0)
        eyes(c, cx, cy - 1, 1 if r >= 4 else 0, size=2 if r >= 4 else 1)
        t = C(); t.rect(cx - 1, cy - r - 6, cx, cy - r - 5, W if on else CYAN); c.paste(t.outlined())
        if on:
            for q in ((-3, -7), (2, -7), (-4, -5), (3, -5), (-3, -8), (2, -8)): c.p(cx + q[0], cy - r + q[1] + 1, CYAN)
    for f in range(4):
        c = C()
        if f < 3:
            bot(c, 12, 16 + f % 2, 4.2, f, 71, f == 1)
        else:
            bot(c, 5, 17, 3, 0, 72, False); bot(c, 20, 17, 3, 1, 73, False)
            for x in range(7, 19, 2): c.p(x, 9 + (1 if 10 < x < 15 else 0), CYAN)
        fr.append(c)
    return fr


DIG = {'3': ["###", "..#", "###", "..#", "###"], '2': ["###", "..#", "###", "#..", "###"], '1': [".#.", "##.", ".#.", ".#.", "###"], '0': ["###", "#.#", "#.#", "#.#", "###"]}


def t_bomb():
    fr = []
    for f in range(4):
        c = C(); cx, cy = 12, 14; r = 5.6 if f < 3 else 7.0
        legs(c, 'A', cx, cy + 5, f if f < 3 else 0, spread=2)
        fuse = [(12, 7), (12, 6), (13, 5), (14, 4), (15, 4), (16, 3)][: 6 - f * 2 if f < 3 else 1]
        c.ell(cx, cy, r, r - .3, K, True)
        c.fuzz(81 + f % 2, 0.55, 0.25)
        cap = C(); cap.rect(cx - 1, cy - int(r) - 1, cx + 1, cy - int(r), STEELD); c.paste(cap)
        for q in fuse: c.p(q[0], q[1] - (1 if f == 3 else 0), WOOD)
        sx, sy = fuse[-1][0] + (0 if f == 3 else 1), fuse[-1][1] - 1 - (1 if f == 3 else 0)
        for q in ((0, 0),): c.p(sx, sy, W)
        for q in ((1, 0), (-1, 0), (0, -1), (0, 1)) if f % 2 == 0 else ((1, -1), (-1, 1), (1, 1), (-1, -1)): c.p(sx + q[0], sy + q[1], AMBER)
        if f < 3:
            eyes(c, cx, cy - 4, 1)
            c.grid(cx - 1, cy - 1, DIG['321'[f]], {'#': AMBER})
        else:
            eyes(c, cx, cy - 4, 2, size=3, col=W); c.p(cx - 3, cy - 3, R); c.p(cx + 4, cy - 3, R)
            c.grid(cx - 1, cy, DIG['0'], {'#': W})
            for a in range(0, 360, 45):
                c.p(round(cx + math.cos(math.radians(a)) * 10), round(cy + math.sin(math.radians(a)) * 9), AMBER)
        fr.append(c)
    return fr


def t_miner():
    fr = []
    for f in range(3):
        c = C(); bob = 1 if f == 1 else 0; cx, cy = 9, 16 + bob
        legs(c, 'A', cx, cy + 3, f)
        body(c, 'A', cx, cy, 4.4, 4, 91, f, bumps=1)
        eyes(c, cx + 1, cy - 1, 1, look=0)
        hat = C(); hat.rect(cx - 3, cy - 6, cx + 3, cy - 5, AMBER); hat.rect(cx - 2, cy - 7, cx + 2, cy - 7, AMBER)
        hat.rect(cx - 4, cy - 4, cx + 5, cy - 4, GOLDD); hat.p(cx + 3, cy - 6, W); hat.p(cx + 4, cy - 6, W)
        c.paste(hat.outlined())
        pk = C()
        if f != 1:   # raised
            pk.line([(cx + 5, cy + 1), (cx + 9, cy - 9)], WOOD)
            pk.line([(cx + 5, cy - 9), (cx + 8, cy - 11), (cx + 11, cy - 11), (cx + 13, cy - 8)], STEEL)
        else:        # strike
            pk.line([(cx + 5, cy), (cx + 13, cy - 1)], WOOD)
            pk.line([(cx + 12, cy - 5), (cx + 14, cy - 2), (cx + 14, cy + 1), (cx + 12, cy + 3)], STEEL)
        c.paste(pk.outlined())
        coin = C(); kx, ky = (20, 19) if f < 2 else (15, 9)
        coin.rect(kx, ky - 1, kx + 2, ky + 3, GREEN); coin.rect(kx - 1, ky, kx + 3, ky + 2, GREEN); coin.p(kx + 1, ky + 1, W); coin.p(kx + 1, ky, W); coin.p(kx + 1, ky + 2, W)
        c.paste(coin.outlined())
        if f == 1:
            for q in ((23, 14), (25, 16), (21, 13)): c.p(q[0], q[1], W)
        if f == 2:
            c.p(kx - 2, ky + 4, GREEN); c.p(kx - 3, ky + 6, GREEN)
        for i in range(2): c.p(cx - 4 - i, cy - 8 - i * 2 - f % 2, G2)   # overheating steam
        fr.append(c)
    return fr


def t_poly():
    fr = []
    for f in range(4):
        c = C(); cx, cy = 12, 13
        legs(c, 'A', cx, cy + 4, f)
        if f == 0: c.ell(cx, cy, 5.6, 5.2, K, True)
        elif f == 1: c.rect(cx - 5, cy - 5, cx + 5, cy + 5, K, True)
        elif f == 2: c.poly([(cx, cy - 8), (cx + 7, cy + 5), (cx - 7, cy + 5)], K, True)
        else:
            pts = []
            for i in range(16):
                a = i / 16 * 6.283; rr = 8.2 if i % 2 == 0 else 3.6
                pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
            c.poly(pts, K, True)
        c.fuzz(101 + f, 0.75, 0.3)
        eyes(c, cx, cy - 1 + (2 if f == 2 else 0), 1)
        rnd = random.Random(500 + f)
        for i in range(4):   # morph glitch pixels
            a = rnd.uniform(0, 6.28); d = rnd.uniform(7.5, 9.5)
            c.p(round(cx + math.cos(a) * d), round(min(GY - 2, cy + math.sin(a) * d * .8)), MAG if i % 2 else CYAN)
        fr.append(c)
    return fr


def t_macro():
    fr = []
    for f in range(3):
        c = C(); cx = 12; hid = (f == 2); top = 4 + (f % 2) + (3 if hid else 0)
        if not hid: legs(c, 'A', cx, top + 14, f, spread=2)
        pg = C()
        pg.rect(cx - 5, top, cx + 5, top + 14, PAPER)
        for i in range(3): pg.rect(cx + 3 + i, top, cx + 5, top + 2 - i, (0, 0, 0)) if False else None
        for (x, y) in ((cx + 5, top), (cx + 4, top), (cx + 5, top + 1)): pg.im.putpixel((x, y), (0, 0, 0, 0))
        pg.p(cx + 4, top + 1, G2); pg.p(cx + 3, top + 1, G2); pg.p(cx + 4, top + 2, G2)
        for j in range(2, 14, 2):
            pg.line([(cx - 3, top + j), (cx + (2 if j % 4 else 3), top + j)], (120, 150, 214))
        pg.rect(cx - 4, top + 1, cx - 3, top + 1 + 1, BLUE)  # generic doc badge
        c.paste(pg.outlined())
        if hid:
            c.p(cx - 2, top + 6, R); c.p(cx + 1, top + 6, R)
        else:
            hole = C(); hole.ell(cx, top + 8, 3.6, 3.4, K, True); hole.fuzz(111 + f, 0.9, 0.5); c.paste(hole)
            eyes(c, cx, top + 7, 0)
            c.line([(cx - 5, top + 9), (cx - 8, top + 7 + f)], K); c.line([(cx + 5, top + 9), (cx + 8, top + 8 - f)], K)  # arms through the page
        fr.append(c)
    return fr


def t_phish():
    fr = []
    for f in range(4):
        c = C(); bob = f % 2; cx, cy = 7, 16 + bob; sway = (0, 1, 2, 1)[f]
        legs(c, 'A', cx, cy + 3, f)
        body(c, 'A', cx, cy, 4.6, 4, 121, f, bumps=1)
        eyes(c, cx + 1, cy - 2, 1)
        if f >= 2:   # grin
            for i in range(5): c.p(cx - 1 + i, cy + 1, W if i % 2 == 0 else K)
        c.line([(cx, cy - 4), (cx + 1, cy - 9), (cx + 4, cy - 12), (cx + 8, cy - 12), (cx + 10, cy - 10)], K)   # lure stalk
        ex, ey = cx + 8 + sway, cy - 6
        c.line([(cx + 10, cy - 10), (ex + 3, ey - 1)], G1)
        env = C(); env.rect(ex, ey, ex + 7, ey + 4, PAPER)
        env.line([(ex, ey), (ex + 3, ey + 2), (ex + 4, ey + 2), (ex + 7, ey)], G2)
        env.p(ex + 6, ey + 3, AMBER)   # stamp
        c.paste(env.outlined())
        c.p(ex + 3, ey + 6, STEEL); c.p(ex + 3, ey + 7, STEEL); c.p(ex + 2, ey + 8, STEEL); c.p(ex + 1, ey + 7, STEEL)   # hook under the mail
        fr.append(c)
    return fr


TYPES = [
    ('ワーム', 'Worm', t_worm), ('トロイの木馬', 'Trojan', t_trojan), ('ランサムウェア', 'Ransomware', t_ransom),
    ('スパイウェア', 'Spyware', t_spy), ('アドウェア', 'Adware', t_adware), ('ルートキット', 'Rootkit', t_rootkit),
    ('ボットネット', 'Botnet', t_botnet), ('ロジックボム', 'Logic bomb', t_bomb), ('クリプトマイナー', 'Cryptominer', t_miner),
    ('ポリモーフィック', 'Polymorphic', t_poly), ('マクロウイルス', 'Macro virus', t_macro), ('フィッシング', 'Phishing', t_phish),
]
