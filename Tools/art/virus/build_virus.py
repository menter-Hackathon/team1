# -*- coding: utf-8 -*-
"""ウイルスの絵を書き出す（2026-11-20。docs/virus-types-design.md 6章）。

python3 Tools/art/virus/build_virus.py [--preview <フォルダ>]

- Assets/Resources/Virus/Virus_<Type>.png：1コマ 26×24px・横1列・すき間なし（virus_art.py の t_* をそのまま）
- Assets/Resources/Virus/Nest_Worm.png（30×20px×6コマ）・Nest_Rootkit.png（44×16px×6コマ）：巣
- 原点（足元）の決め方は VirusArt.cs（Unity 側）。取り込み設定は Editor/Art/VirusImport.cs
- --preview：5倍に広げた確認用の絵を、そのフォルダに書く（プロジェクトの外に出す）
"""
import os, sys, random
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import virus_art as v  # noqa: E402

PROJ = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
OUT = os.path.join(PROJ, 'Assets', 'Resources', 'Virus')

# 種類の名前（Unity 側の "Virus_<Type>"）→ 絵のコマ
TYPES = [
    ('Worm', lambda: v.t_worm('A', bud=False)),
    ('Rootkit', lambda: v.t_rootkit(slab=False, roots=False, clip=True, frames=3)),
    ('Trojan', lambda: v.t_trojan()),
    ('Ransomware', lambda: v.t_ransom('A')),
    ('Spyware', lambda: v.t_spy('A')),
    ('Adware', lambda: v.t_adware()),
    ('Botnet', lambda: v.t_botnet()[:3]),  # コマ3（2体つなぎ）は使わない
    ('Bomb', lambda: v.t_bomb()),
    ('Miner', lambda: v.t_miner()),
    ('Poly', lambda: v.t_poly()),
    ('Macro', lambda: v.t_macro()),
    ('Phish', lambda: v.t_phish()),
]


def strip(frames):
    fw, fh = frames[0].w, frames[0].h
    im = Image.new('RGBA', (fw * len(frames), fh), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        im.alpha_composite(f.im, (i * fw, 0))
    return im


# ───────── 巣 ─────────
def soot_mound(c, cx, base_y, w, h, seed, puff=0):
    """下の辺が base_y のススの山（ふちに毛）"""
    rx = w / 2 + puff
    c.ell(cx, base_y, rx, h, v.K, True)
    rnd = random.Random(seed)
    for i in range(3):
        a = rnd.uniform(3.4, 6.0)  # 上半分
        c.ell(cx + rx * 0.6 * (rnd.random() * 2 - 1), base_y - h * 0.55, w * 0.18, h * 0.32, v.K, True)
    # 下の辺より下は消す（面に付く）
    for y in range(base_y + 1, c.h):
        for x in range(c.w):
            c.im.putpixel((x, y), (0, 0, 0, 0)); c.body.discard((x, y))
    c.fuzz(seed, 0.8, 0.3)
    for y in range(base_y + 1, c.h):
        for x in range(c.w):
            c.im.putpixel((x, y), (0, 0, 0, 0))


def n_worm():
    """あいたポートの巣：30×20。下の辺（y=19）が付く面"""
    W, H = 30, 20
    base = H - 1
    frames = []
    for f in range(6):
        c = v.C(W, H)
        cx = W // 2
        if f < 5:
            soot_mound(c, cx, base, 26, 13, 301 + (f % 2), puff=1 if f == 2 else 0)
            # 卵 2つ
            ey = 4 - (1 if f == 1 else 0)
            for ex in (cx - 7, cx + 6):
                c.rect(ex, ey, ex + 2, ey + 2, v.K); c.p(ex + 1, ey + 1, v.LIME)
            # 小さい穴 3つ
            for hx, hy in ((cx - 10, base - 4), (cx + 9, base - 5), (cx - 5, base - 10)):
                c.rect(hx, hy, hx + 1, hy + 1, (0, 0, 0)); c.p(hx - 1, hy, v.G1); c.p(hx + 2, hy + 1, v.G1)
            # 差しこみ口 10×7
            px0, py0 = cx - 5, base - 9
            c.rect(px0, py0, px0 + 9, py0 + 6, v.STEELD)
            c.rect(px0 + 1, py0 + 1, px0 + 8, py0 + 5, (0, 0, 0))
            for k in range(4): c.p(px0 + 2 + k * 2, py0 + 1, v.STEEL)
            if f == 2:
                c.rect(px0 + 1, py0 + 4, px0 + 8, py0 + 5, v.LIME)
            # ランプ
            lime_on = f in (0, 2)
            amber_on = f in (1, 2)
            c.rect(px0 - 3, base - 3, px0 - 2, base - 2, v.LIME if lime_on else v.G1)
            c.rect(px0 + 11, base - 3, px0 + 12, base - 2, v.AMBER if amber_on and f < 3 else v.G1)
            # ケーブル（面にそって左右へ。2px 描いて 1px あける）
            for side in (-1, 1):
                x = cx + side * 13
                for k in range(8):
                    if k % 3 == 2: continue
                    if f >= 3 and side > 0 and k in (3, 4): continue  # 片方切れている
                    c.p(x + side * k, base, v.LIME)
            if f in (3, 4):  # ひび
                for (x, y) in ((cx - 8, base - 9), (cx - 7, base - 8), (cx - 8, base - 7), (cx - 7, base - 6),
                               (cx + 7, base - 8), (cx + 8, base - 7), (cx + 7, base - 6), (cx + 8, base - 5)):
                    c.p(x, y + (f - 3), v.G2)
        else:
            px0, py0 = cx - 5, base - 9
            c.rect(px0, py0, px0 + 9, py0 + 6, v.STEELD)
            c.rect(px0 + 1, py0 + 1, px0 + 8, py0 + 5, (0, 0, 0, 0)[:3])
            for y in range(py0 + 1, py0 + 6):
                for x in range(px0 + 1, px0 + 9): c.im.putpixel((x, y), (0, 0, 0, 0))
            c.rect(px0 - 3, base - 3, px0 - 2, base - 2, v.G1)
            c.rect(px0 + 11, base - 3, px0 + 12, base - 2, v.G1)
        frames.append(c)
    return frames


LILAC = (206, 160, 255)


def n_rootkit():
    """床下の根とチップ：44×16。下から 12px（y=3 の行の下の境目）が床の線。上 4px が床の上"""
    W, H = 44, 16
    floor = 4  # この行から下が床の中
    cx = W // 2
    frames = []
    for f in range(6):
        c = v.C(W, H)
        root = LILAC if f == 2 else (v.G1 if f == 5 else v.PURP)
        if f < 5:
            # 根：幹（幅2・下へ10）
            c.rect(cx - 1, floor, cx, floor + 9, root)
            # 枝（左右2本ずつ。t_rootkit の根を2倍に）
            for sgn in (-1, 1):
                if f in (3, 4) and sgn < 0:
                    c.line([(cx - 1, floor + 2), (cx - 5, floor + 5)], root)  # 途中で切れた
                else:
                    c.line([(cx + sgn, floor + 2), (cx + sgn * 8, floor + 6), (cx + sgn * 12, floor + 11)], root)
                c.line([(cx + sgn * 8, floor + 6), (cx + sgn * 18, floor + 8)], root)
                tip = (f % 2) if f < 2 else 1
                if tip: c.p(cx + sgn * 20, floor + 9, root)
            for (x, y) in ((cx - 9, floor + 6), (cx + 13, floor + 7), (cx + 6, floor + 5)):
                c.rect(x, y, x + 1, y + 1, root)
            # チップ（5×4・足つき）
            chx, chy = cx - 3, floor + 7
            c.rect(chx, chy, chx + 4, chy + 3, v.STEELD)
            for k in (1, 3):
                c.p(chx - 1, chy + k - 1, v.STEEL); c.p(chx + 5, chy + k - 1, v.STEEL)
            c.p(chx + 1, chy, root); c.p(chx + 3, chy, root); c.p(chx + 2, chy + 1, root)  # 巻きつく根
            # 穴（幅12：床の線の上1px〜下2px）
            gw = 6 + (1 if f == 2 else 0)
            c.rect(cx - gw, floor - 1, cx + gw - 1, floor + 2, v.K)
            # 赤い目 2組（交代でまたたく）
            on_a = f != 1
            on_b = f != 0 or f == 2
            if f >= 3: on_b = False
            c.p(cx - 4, floor, v.R if on_a else v.K); c.p(cx - 2, floor, v.R if on_a else v.K)
            c.p(cx + 1, floor + 1, v.R if on_b else v.K); c.p(cx + 3, floor + 1, v.R if on_b else v.K)
            # 床のかけら
            bits = [(cx - 10, floor - 2, 2, 2), (cx - 13, floor - 3, 3, 3), (cx + 8, floor - 2, 3, 2), (cx + 12, floor - 4, 2, 4)]
            if f in (3, 4): bits.append((cx - 16, floor - 2, 2, 2))
            for (x, y, w, h) in bits:
                c.rect(x, y, x + w - 1, y + h - 1, v.SLAB); c.rect(x, y + h - 1, x + w - 1, y + h - 1, v.SLABD)
        else:
            c.rect(cx - 3, floor, cx + 2, floor, v.K)
            c.rect(cx - 1, floor + 1, cx, floor + 6, root)
            c.line([(cx - 1, floor + 2), (cx - 6, floor + 5)], root); c.line([(cx, floor + 2), (cx + 6, floor + 5)], root)
        frames.append(c)
    return frames


def main():
    preview = None
    if '--preview' in sys.argv:
        preview = sys.argv[sys.argv.index('--preview') + 1]
        os.makedirs(preview, exist_ok=True)
    os.makedirs(OUT, exist_ok=True)
    sheets = []
    for name, fn in TYPES:
        fr = fn()
        assert fr[0].w == 26 and fr[0].h == 24, name
        im = strip(fr)
        im.save(os.path.join(OUT, 'Virus_%s.png' % name))
        sheets.append((name, im))
        print('Virus_%s.png  %dx%d  %d frames' % (name, im.width, im.height, len(fr)))
    for name, fn in (('Worm', n_worm), ('Rootkit', n_rootkit)):
        fr = fn()
        im = strip(fr)
        im.save(os.path.join(OUT, 'Nest_%s.png' % name))
        sheets.append(('Nest_' + name, im))
        print('Nest_%s.png  %dx%d  %d frames' % (name, im.width, im.height, len(fr)))
    if preview:
        s = 5
        wmax = max(im.width for _, im in sheets) * s
        hsum = sum(im.height * s + 6 for _, im in sheets)
        sheet = Image.new('RGBA', (wmax, hsum), v.LIGHT_BG + (255,))
        y = 0
        for _, im in sheets:
            sheet.alpha_composite(im.resize((im.width * s, im.height * s), Image.NEAREST), (0, y))
            y += im.height * s + 6
        sheet.save(os.path.join(preview, 'virus_export_preview.png'))
        print('preview:', os.path.join(preview, 'virus_export_preview.png'))


if __name__ == '__main__':
    main()
