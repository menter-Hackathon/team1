#!/usr/bin/env python3
"""
【仮】ゲームの絵を作るスクリプト（2026-11-17）。

デザイン担当が描いた絵（Assets/Sprites/Provided。プレイヤー・煙・壁紙・見本）の色と雰囲気に合わせて、まだ絵が無い物の仮の絵を
Assets/Sprites/Generated に書き出す。デザイン担当の本番の絵ができたら、同じ名前・同じ大きさで差し替えてよい。

ドットの大きさ
- ゲーム中の物：1マス＝18ドット（プレイヤーの絵の体 13×18 ドットが、当たり判定 0.7×1.0 マスにちょうど合う）
- 背景：1マス＝15ドット（320×180 の絵が、画面 21.33×12 マスにちょうど収まる）

使い方（リポジトリの一番上で）： python3 Tools/art/gen_sprites.py
必要なもの：Pillow（pip install pillow）
"""
import math
import os
import random

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SPRITES = os.path.join(ROOT, "Assets", "Sprites")
PROVIDED = os.path.join(SPRITES, "Provided")
GEN = os.path.join(SPRITES, "Generated")


def C(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


CLEAR = (0, 0, 0, 0)
# デザイン担当の絵から取った色
K = C("000000")          # 線
CREAM = C("f5f198")      # プレイヤーの体
OLIVE = C("b3b06f")      # プレイヤーの影
SCREEN = C("22005c")     # プレイヤーの画面
EYE_G = C("2aff00")      # プレイヤーの目
RED = C("ff0000")        # アンテナ・手
GREY_P = C("696a6a")
PLUM = C("45283c")
W51 = C("515151")        # ウィンドウの外枠
W79 = C("797979")        # ウィンドウの枠
WHITE = C("ffffff")
WDC = C("dcdcdc")        # ウィンドウの中
WA2 = C("a2a2a2")
W8B = C("8b8b8b")
W68 = C("686868")
ORANGE = C("df7126")     # 閉じるボタン
DRED = C("970000")
SKY = C("429fec")
GREEN = C("2ab11a")
GREEN_L = C("39cf16")
GREEN_HL = C("5fe016")
GREEN_D = C("1d5319")
ROAD_TOP = C("a6a6a6")
ROAD = C("797979")
ROAD_DOT = C("767676")
S0 = C("0d0d0d")         # ウイルスの煙（エフェクトの絵の3色）
S1 = C("4d4d4d")
S2 = C("8f8f8f")
# 追加した色（アプリの中身）
CODE_BG = C("1e1e1e")
CODE_SIDE = C("252526")
CODE_ACT = C("333333")
CODE_TITLE = C("3c3c3c")
CODE_TAB = C("2d2d2d")
CODE_DIM = C("858585")
CODE_BLUE = C("569cd6")
CODE_LBLUE = C("9cdcfe")
CODE_STR = C("ce9178")
CODE_FN = C("dcdcaa")
CODE_CMT = C("6a9955")
CODE_PURP = C("c586c0")
CODE_ERR = C("f14c4c")
CODE_STATUS = C("007acc")
GOLD = C("ffd23f")


# ───────── 道具 ─────────

def new(w, h, fill=CLEAR):
    return Image.new("RGBA", (w, h), fill)


def put(im, x, y, c):
    if 0 <= x < im.width and 0 <= y < im.height and c is not None:
        im.putpixel((x, y), c)


def rect(im, x0, y0, x1, y1, c):
    """x0..x1, y0..y1 を含む四角を塗る"""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(im, x, y, c)


def hline(im, x0, x1, y, c):
    rect(im, x0, y, x1, y, c)


def vline(im, x, y0, y1, c):
    rect(im, x, y0, x, y1, c)


def outline_rect(im, x0, y0, x1, y1, c):
    hline(im, x0, x1, y0, c)
    hline(im, x0, x1, y1, c)
    vline(im, x0, y0, y1, c)
    vline(im, x1, y0, y1, c)


def ascii_art(rows, legend):
    w = max(len(r) for r in rows)
    im = new(w, len(rows))
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            c = legend.get(ch)
            if c is not None:
                put(im, x, y, c)
    return im


def ellipse(im, cx, cy, rx, ry, c):
    for y in range(im.height):
        for x in range(im.width):
            if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1.0:
                put(im, x, y, c)


def paste(dst, src, x, y):
    dst.alpha_composite(src, (x, y))


def sheet(frames):
    w, h = frames[0].size
    out = new(w * len(frames), h)
    for i, f in enumerate(frames):
        out.paste(f, (i * w, 0))
    return out


def save(im, rel):
    path = os.path.join(GEN, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)
    return path


def outline_alpha(im, c=K):
    """不透明な所のまわり1ドットに線を引く"""
    out = new(im.width + 2, im.height + 2)
    out.paste(im, (1, 1))
    src = out.copy()
    for y in range(out.height):
        for x in range(out.width):
            if src.getpixel((x, y))[3] > 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                xx, yy = x + dx, y + dy
                if 0 <= xx < out.width and 0 <= yy < out.height and src.getpixel((xx, yy))[3] > 0:
                    out.putpixel((x, y), c)
                    break
    return out


# ───────── 背景（デザイン担当の壁紙から、下の道路を消したもの） ─────────

def make_background():
    """壁紙（Provided/Bg_Wallpaper.png）の下の道路を消して、丘を下まで伸ばす。床はゲームの床の絵が描く。
    壁紙が無ければ、見本（Mockup_Desktop.png）からウィンドウを消して作る"""
    clean = os.path.join(PROVIDED, "Bg_Wallpaper.png")
    im = Image.open(clean).convert("RGBA") if os.path.exists(clean) else erase_window_from_mockup()
    remove_road(im)
    save(im, "Background/Bg_Desktop.png")
    return im


def erase_window_from_mockup():
    """見本の絵からウィンドウを消して、丘と空を描き足す（壁紙が無かったときの予備）"""
    im = Image.open(os.path.join(PROVIDED, "Mockup_Desktop.png")).convert("RGBA")
    px = im.load()
    sky_set = {SKY[:3], WHITE[:3]}
    w, h = im.size
    x0, x1, y0, y1 = 31, 213, 14, 126  # ウィンドウと閉じるボタン（x1, y1 は含まない）

    def boundary_at(col):
        return next(y for y in range(h) if px[col, y][:3] not in sky_set)

    bl, br = boundary_at(x0 - 1), boundary_at(x1)
    prof_l = [px[x0 - 1, y] for y in range(h)]
    prof_r = [px[x1, y] for y in range(h)]

    def boundary(x):
        s = (x - (x0 - 1)) / (x1 - (x0 - 1))
        crest = -7.0 * math.sin(min(1.0, s / 0.45) * math.pi * 0.5) * (1.0 - s) ** 0.6
        return int(round(bl + (br - bl) * s * s * (3 - 2 * s) + crest))

    for x in range(x0, x1):
        s = (x - x0) / (x1 - x0)
        b = boundary(x)
        for y in range(y0, y1):
            if y < b:
                px[x, y] = SKY
                continue
            wobble = 0.07 * math.sin(y * 0.21 + x * 0.05) + 0.04 * math.sin(y * 0.6)
            if s + wobble < 0.62:
                yy = min(y - b + bl, y1 - 1)
                px[x, y] = prof_l[yy]
            else:
                yy = min(y - b + br, h - 1)
                px[x, y] = prof_r[yy]
    def is_white(x, y):
        return px[x, y][:3] == WHITE[:3]

    # 上の雲：ウィンドウで切れた下の端を、でこぼこに丸めて伸ばす
    x = x0
    while x < x1:
        if not is_white(x, y0 - 1):
            x += 1
            continue
        a = x
        while x < x1 and is_white(x, y0 - 1):
            x += 1
        b = x - 1
        for xx in range(a, b + 1):
            dist = min(xx - a, b - xx)
            ext = min(dist // 2, 2 + int(round(1.2 * math.sin(xx * 0.45))))
            for k in range(max(0, ext)):
                px[xx, y0 + k] = WHITE
    # 左の雲：ウィンドウで切れた右の端を、丸く閉じる
    ys = [y for y in range(y0, y1) if is_white(x0 - 1, y)]
    if ys:
        top, bot = min(ys), max(ys)
        for y in ys:
            t = (y - top + 0.5) / (bot - top + 1)
            ext = int(round(4.0 * math.sin(math.pi * t)))
            for k in range(ext):
                px[x0 + k, y] = WHITE
    # 右の雲を1つ写して、空いた空に置く（下にある別の雲は写さない）
    cloud = im.crop((243, 30, 298, 54))
    cpx = cloud.load()
    for yy in range(cloud.height):
        for xx in range(cloud.width):
            if cpx[xx, yy][:3] == WHITE[:3]:
                tx, ty = 92 + xx, 34 + yy
                if ty < boundary(tx) - 2:
                    px[tx, ty] = WHITE
    return im


def remove_road(im):
    """下の道路を消して、丘を下まで伸ばす（床はゲームの床の絵が描く）"""
    px = im.load()
    w, h = im.size
    road_top = next(y for y in range(h - 1, 0, -1) if px[0, y][:3] == ROAD_TOP[:3])
    while px[0, road_top - 1][:3] == ROAD_TOP[:3]:
        road_top -= 1
    for x in range(w):
        base = px[x, road_top - 1]
        for y in range(road_top, h):
            d = y - road_top
            if d < 3:
                px[x, y] = base
            elif d < 6:
                px[x, y] = GREEN_D
            else:
                px[x, y] = C("318c2a") if (x * 7 + d * 13) % 23 == 0 else C("287322")


# ───────── 地形 ─────────

def make_terrain():
    rnd = random.Random(7)
    # 床（道路）：上の3ドットは縁。中は横にも縦にも並べて使う
    f = new(18, 18, ROAD)
    hline(f, 0, 17, 0, ROAD_TOP)
    hline(f, 0, 17, 1, ROAD_TOP)
    hline(f, 0, 17, 2, W68)
    for _ in range(14):
        put(f, rnd.randrange(18), rnd.randrange(4, 18), ROAD_DOT if rnd.random() < 0.6 else W8B)
    save(f, "Stage/Tile_Floor.png")

    # 壁（モニターの縁）：左右3ドットが縁
    wl = new(18, 18, W51)
    for x, c in ((0, W79), (1, W68), (2, W68), (15, W68), (16, W68), (17, W79)):
        vline(wl, x, 0, 17, c)
    for y in range(0, 18, 6):
        hline(wl, 4, 13, y, C("4a4a4a"))
    save(wl, "Stage/Tile_Wall.png")

    # すり抜け床（フォルダ）：左はタブ、まん中は横に並べる
    rows = [
        ".KKKKKK.................",
        "KTTTTTTKKKKKKKKKKKKKKKK.",
        "KYYYYYYYYYYYYYYYYYYYYYYK",
        "KYYYYYYYYYYYYYYYYYYYYYYK",
        "KOOOOOOOOOOOOOOOOOOOOOOK",
        ".KKKKKKKKKKKKKKKKKKKKKK.",
    ]
    # タブ（T）は、アプリの色で別の色に塗り替えられるように、体（Y）とわずかに違う色にしておく
    save(ascii_art(rows, {"K": K, "Y": CREAM, "O": OLIVE, "T": C("f6f29a")}), "Stage/Platform_Folder.png")


# ───────── ウィンドウ ─────────

def make_windows():
    # 9スライス用の枠（外から 515151×2、797979×2、白×2、中は dcdcdc）
    f = new(16, 16, WDC)
    for i, c in enumerate((W51, W51, W79, W79, WHITE, WHITE)):
        outline_rect(f, i, i, 15 - i, 15 - i, c)
    save(f, "UI/Win_Frame.png")

    # 閉じるボタン（小）
    rows = [
        "ROOOOOOOOOO",
        "ROOOOOOOOOO",
        "ROOWWOOOWWO",
        "ROOOWWOWWOO",
        "ROOOOWWWOOO",
        "ROOOOWWWOOO",
        "ROOOWWOWWOO",
        "ROOWWOOOWWO",
        "ROOOOOOOOOO",
        "RRRRRRRRRRR",
        "RRRRRRRRRRR",
    ]
    save(ascii_art(rows, {"R": DRED, "O": ORANGE, "W": WHITE}), "UI/Btn_Close.png")

    # ボタン（タイトル・リザルト）：通常・マウスが乗ったとき
    for name, face, edge in (("Ui_Button", WDC, W79), ("Ui_ButtonHover", WHITE, ORANGE)):
        b = new(16, 16, face)
        outline_rect(b, 0, 0, 15, 15, K)
        outline_rect(b, 1, 1, 14, 14, edge)
        hline(b, 2, 13, 2, WHITE)
        vline(b, 2, 2, 13, WHITE)
        hline(b, 2, 13, 13, WA2)
        vline(b, 13, 3, 13, WA2)
        save(b, f"UI/{name}.png")

    # 点線の枠（出現・瞬間移動の予告）。2ドットおきの点線なので、9スライスで並べても点線のまま
    t = new(12, 12)
    for i in range(12):
        on = (i // 2) % 2 == 0
        if on:
            put(t, i, 0, WHITE); put(t, i, 11, WHITE); put(t, 0, i, WHITE); put(t, 11, i, WHITE)
    for (x, y) in ((0, 0), (1, 0), (0, 1), (11, 0), (10, 0), (11, 1), (0, 11), (1, 11), (0, 10), (11, 11), (10, 11), (11, 10)):
        put(t, x, y, WHITE)
    save(t, "Attacks/Fx_TelegraphFrame.png")

    # 文字カーソル（VS Code が現れる場所の予告）
    cur = new(4, 12)
    vline(cur, 0, 0, 11, SKY); vline(cur, 3, 0, 11, SKY)
    vline(cur, 1, 0, 11, WHITE); vline(cur, 2, 0, 11, WHITE)
    save(cur, "Attacks/Fx_Cursor.png")


def window_base(w, h, title=None, frame=4, close=True):
    """小さめのウィンドウ（ポップアップ・エラー）。title はタイトルバーの色"""
    im = new(w, h, WDC)
    cols = (W51, W79, WHITE, WHITE)[:frame] if frame <= 4 else (W51, W51, W79, W79, WHITE, WHITE)
    for i, c in enumerate(cols):
        outline_rect(im, i, i, w - 1 - i, h - 1 - i, c)
    top = len(cols)
    if title is not None:
        rect(im, top, top, w - 1 - top, top + 5, title)
        hline(im, top + 2, top + 12, top + 2, WHITE)
        hline(im, top + 2, top + 8, top + 3, WHITE)
        if close:
            bx = w - 1 - top - 6
            rect(im, bx, top, bx + 6, top + 5, ORANGE)
            vline(im, bx, top, top + 5, DRED)
            for k in range(4):
                put(im, bx + 2 + k, top + 1 + k, WHITE)
                put(im, bx + 5 - k, top + 1 + k, WHITE)
    return im, top


def make_app_windows():
    # ポップアップ（Chrome のポップアップ出現。3×2マス＝54×36）
    im, top = window_base(54, 36, SKY)
    rect(im, 7, 12, 46, 25, ORANGE)
    outline_rect(im, 7, 12, 46, 25, DRED)
    # 星形のバッジ
    cx, cy = 15, 18
    for dy in range(-4, 5):
        for dx in range(-4, 5):
            if abs(dx) + abs(dy) <= 4 or (dx == 0 or dy == 0) and abs(dx) + abs(dy) <= 5:
                put(im, cx + dx, cy + dy, GOLD)
    put(im, cx, cy, WHITE)
    for y, (a, b) in zip((15, 18, 21), ((23, 43), (23, 39), (23, 41))):
        hline(im, a, b, y, WHITE)
        hline(im, a, b, y + 1, WHITE)
    rect(im, 18, 28, 35, 31, GREEN)
    outline_rect(im, 18, 28, 35, 31, C("1d7a12"))
    hline(im, 22, 31, 29, WHITE)
    save(im, "Attacks/Popup_Ad.png")

    # エラーウィンドウ（VS Code。3×2マス）
    im, top = window_base(54, 36, DRED)
    ellipse(im, 11.5, 18.5, 5.5, 5.5, DRED)
    ellipse(im, 11.5, 18.5, 4.5, 4.5, RED)
    for k in range(-2, 3):
        put(im, 11 + k, 18 + k, WHITE); put(im, 12 + k, 18 + k, WHITE)
        put(im, 11 + k, 18 - k, WHITE); put(im, 12 + k, 18 - k, WHITE)
    for y, b in ((14, 46), (17, 40), (20, 44)):
        hline(im, 20, b, y, W68)
    rect(im, 33, 26, 47, 31, WHITE)
    outline_rect(im, 33, 26, 47, 31, W79)
    hline(im, 37, 43, 28, W68)
    hline(im, 37, 43, 29, W68)
    save(im, "Attacks/Window_Error.png")

    # タブ増殖のタブ（1.5×0.6マス＝27×11）
    tab = new(27, 11)
    for y in range(11):
        inset = max(0, 3 - y)
        for x in range(inset, 27 - inset):
            put(tab, x, y, WHITE if y < 9 else WDC)
    for y in range(11):
        inset = max(0, 3 - y)
        put(tab, inset, y, K); put(tab, 26 - inset, y, K)
    for x in range(3, 24):
        put(tab, x, 0, K)
    for k in range(3):
        put(tab, k + 1, 3 - k, K); put(tab, 25 - k, 3 - k, K)
    hline(tab, 0, 26, 10, K)
    rect(tab, 4, 4, 6, 6, SKY)
    hline(tab, 9, 17, 5, WA2)
    for k in range(3):
        put(tab, 20 + k, 4 + k, W68); put(tab, 22 - k, 4 + k, W68)
    save(tab, "Attacks/Tab.png")

    # タブ手裏剣（0.6×0.4マス＝11×7）
    rows = [
        "..KKKKKKK..",
        ".KWWWWWWWK.",
        "KWBBWWWWWWK",
        "KWBBWEEEWWK",
        "KWWWWWWWWWK",
        "KDDDDDDDDDK",
        "KKKKKKKKKKK",
    ]
    save(ascii_art(rows, {"K": K, "W": WHITE, "B": SKY, "E": WA2, "D": WDC}), "Attacks/Bullet_Tab.png")


def make_boss_contents():
    """ボスのウィンドウの中身（4×3マスのウィンドウの内側 60×42）"""
    # ブラウザ（Chrome のボス）
    im = new(60, 42, WHITE)
    rect(im, 0, 0, 59, 6, WA2)
    # 開いているタブ
    for y in range(1, 7):
        inset = max(0, 2 - y)
        hline(im, 2 + inset, 21 - inset, y, WHITE)
    rect(im, 5, 3, 7, 5, SKY)
    hline(im, 10, 17, 4, WA2)
    # ほかのタブ
    for y in range(2, 7):
        inset = max(0, 3 - y)
        hline(im, 23 + inset, 38 - inset, y, WDC)
    rect(im, 26, 4, 27, 5, GREEN)
    hline(im, 30, 35, 4, W8B)
    vline(im, 42, 2, 6, W68); hline(im, 40, 44, 4, W68)
    # ツールバーとアドレスバー
    for k in range(3):
        put(im, 3 + k, 10 - k, W68); put(im, 3 + k, 10 + k, W68)
        put(im, 10 - k, 10 - k, W68); put(im, 10 - k, 10 + k, W68)
    rect(im, 14, 8, 56, 12, WDC)
    for y in (8, 12):
        put(im, 14, y, WHITE); put(im, 56, y, WHITE)
    rect(im, 16, 9, 17, 11, GREEN)
    hline(im, 20, 44, 10, W8B)
    hline(im, 0, 59, 14, WA2)
    # ページ：小さな壁紙のバナー、文字の行、ボタン
    rect(im, 3, 17, 56, 27, SKY)
    for x in range(3, 57):
        hy = 24 + int(round(1.6 * math.sin(x * 0.18)))
        for y in range(hy, 28):
            put(im, x, y, GREEN_L if y < hy + 1 else GREEN)
    for (cx, cy, r) in ((14, 20, 2.2), (17, 19.5, 2.6), (20, 20.5, 1.8), (40, 19, 1.8), (43, 18.6, 2.3)):
        ellipse(im, cx, cy, r, r * 0.8, WHITE)
    for y, b in ((30, 50), (33, 44), (36, 30)):
        hline(im, 3, b, y, WA2)
    rect(im, 40, 35, 56, 38, ORANGE)
    hline(im, 43, 53, 36, WHITE)
    save(im, "Bosses/Content_Browser.png")

    # コードエディタ（VS Code のボス）
    rnd = random.Random(3)
    im = new(60, 42, CODE_BG)
    rect(im, 0, 0, 59, 3, CODE_TITLE)
    for i, c in enumerate((C("ff5f57"), C("febc2e"), C("28c840"))):
        put(im, 2 + i * 3, 1, c); put(im, 3 + i * 3, 1, c)
    rect(im, 0, 4, 5, 38, CODE_ACT)
    for i, y in enumerate((6, 11, 16, 21)):
        c = WHITE if i == 0 else CODE_DIM
        rect(im, 1, y, 3, y + 2, c)
    vline(im, 0, 6, 8, CODE_STATUS)
    rect(im, 6, 4, 17, 38, CODE_SIDE)
    for i, y in enumerate(range(7, 33, 3)):
        indent = (0, 2, 2, 4, 4, 2, 0, 2, 2)[i % 9]
        width = rnd.randint(4, 8)
        if i == 3:
            rect(im, 6, y - 1, 17, y + 1, C("37373d"))
        hline(im, 8 + indent, min(16, 8 + indent + width), y, C("cccccc") if i == 3 else CODE_DIM)
    rect(im, 18, 4, 59, 7, CODE_TAB)
    rect(im, 18, 4, 33, 7, CODE_BG)
    hline(im, 21, 29, 5, C("cccccc"))
    put(im, 20, 5, CODE_STR)
    palette = (CODE_BLUE, CODE_LBLUE, CODE_STR, CODE_FN, CODE_PURP, CODE_CMT)
    y = 10
    line = 0
    while y < 37:
        put(im, 20, y, CODE_DIM)
        x = 23 + rnd.choice((0, 0, 3, 6))
        for _ in range(rnd.randint(1, 3)):
            ln = rnd.randint(3, 9)
            hline(im, x, min(57, x + ln), y, rnd.choice(palette))
            x += ln + 2
            if x > 55:
                break
        if line == 4:
            for xx in range(26, 44):
                put(im, xx, y + 1, CODE_ERR if (xx // 2) % 2 == 0 else CLEAR)
                put(im, xx, y + 2, CODE_ERR if (xx // 2) % 2 == 1 else CLEAR)
        y += 3
        line += 1
    rect(im, 0, 39, 59, 41, CODE_STATUS)
    hline(im, 2, 7, 40, WHITE)
    hline(im, 40, 50, 40, WHITE)
    save(im, "Bosses/Content_Editor.png")

    # 小さなアイコン（画面右上のゲージの横に出す）
    ib = new(12, 12, WHITE)
    rect(ib, 0, 0, 11, 2, WA2)
    rect(ib, 1, 1, 5, 2, WHITE)
    rect(ib, 2, 4, 9, 4, WDC)
    rect(ib, 1, 6, 10, 10, SKY)
    rect(ib, 1, 9, 10, 10, GREEN)
    outline_rect(ib, 0, 0, 11, 11, K)
    save(ib, "Bosses/Icon_Browser.png")
    ie = new(12, 12, CODE_BG)
    rect(ie, 1, 1, 2, 9, CODE_ACT)
    for y, (b, c) in zip((2, 4, 6, 8), ((8, CODE_BLUE), (9, CODE_STR), (6, CODE_FN), (9, CODE_CMT))):
        hline(ie, 4, b, y, c)
    rect(ie, 1, 10, 10, 10, CODE_STATUS)
    outline_rect(ie, 0, 0, 11, 11, K)
    save(ie, "Bosses/Icon_Editor.png")


# ───────── ウイルス（エフェクトの絵と同じ黒い煙） ─────────

def smoke_blob(w, h, cx, cy, rx, ry, seed, wisps=6, up_bias=0.8, density=1.0):
    rnd = random.Random(seed)
    im = new(w, h)
    phase = [rnd.uniform(0, math.tau) for _ in range(4)]

    def n(x, y):
        return (0.10 * math.sin(x * 1.3 + phase[0]) * math.cos(y * 1.1 + phase[1])
                + 0.08 * math.sin((x + y) * 0.9 + phase[2]) + rnd.uniform(-0.06, 0.06))

    for y in range(h):
        for x in range(w):
            d = math.sqrt(((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2) + n(x, y)
            if d < 0.70:
                put(im, x, y, S0)
            elif d < 0.88:
                put(im, x, y, S1)
            elif d < 1.02 and rnd.random() < 0.75 * density:
                put(im, x, y, S2 if rnd.random() < 0.55 else S1)
    # 外へ伸びる炎のような筋
    for _ in range(wisps):
        a = rnd.uniform(-math.pi, math.pi)
        a = a * (1 - up_bias) + (-math.pi / 2 + rnd.uniform(-1.1, 1.1)) * up_bias
        sx = cx + math.cos(a) * rx * 0.85
        sy = cy + math.sin(a) * ry * 0.85
        ln = rnd.randint(2, 4)
        for i in range(ln):
            x = int(round(sx + math.cos(a) * i + rnd.uniform(-0.4, 0.4)))
            y = int(round(sy + math.sin(a) * i))
            put(im, x, y, S2 if i > 0 else S1)
    return im


def eyes(im, positions, size=2, angry=False):
    for (x, y) in positions:
        rect(im, x, y, x + size - 1, y + size - 1, RED)
        put(im, x, y, WHITE)
        if angry:
            hline(im, x - 1, x + size, y - 1, S0)


def make_viruses():
    # 歩く雑魚：16×16（足元が下の端）。右向き
    frames = []
    for i in range(4):
        im = smoke_blob(16, 16, 8.0, 7.5, 6.6, 5.8, seed=10 + i, wisps=7)
        eyes(im, [(9, 6), (12, 6)])
        legs = ((5, 10), (6, 9), (5, 10), (4, 11))[i]
        for lx in legs:
            vline(im, lx, 13, 15, S0)
        hline(im, legs[0] - 1, legs[0], 15, S0)
        hline(im, legs[1], legs[1] + 1, 15, S0)
        frames.append(im)
    save(sheet(frames), "Enemies/Zako_Walker.png")

    # 撃つ雑魚：16×20。前に砲台
    frames = []
    for i in range(4):
        im = smoke_blob(16, 20, 7.5, 9.5, 6.0, 7.4, seed=40 + i, wisps=7)
        eyes(im, [(8, 6), (11, 6)])
        rect(im, 11, 11, 15, 13, S1)
        rect(im, 14, 10, 15, 14, S1)
        put(im, 15, 12, RED)
        hline(im, 4, 10, 18, S0)
        hline(im, 3, 11, 19, S0)
        frames.append(im)
    save(sheet(frames), "Enemies/Zako_Shooter.png")

    # 雑魚の弾（7×7）
    rows = [
        "..g.g..",
        ".gdddg.",
        "gdkkkdg",
        ".dkRkd.",
        "gdkkkdg",
        ".gdddg.",
        "..g.g..",
    ]
    save(ascii_art(rows, {"g": S2, "d": S1, "k": S0, "R": RED}), "Attacks/Bullet_Virus.png")


# ───────── 攻撃 ─────────

def make_attacks():
    # エラー弾（1.0×0.3マス＝18×5）。右向き
    rows = [
        "...........DDDDDD.",
        "..DD...DDDDRRRRRDD",
        "DD..DDRRRRRWWWWWRD",
        "..DD...DDDDRRRRRDD",
        "...........DDDDDD.",
    ]
    save(ascii_art(rows, {"D": DRED, "R": RED, "W": WHITE}), "Attacks/Bullet_Error.png")

    # ブレークポイントの赤丸（床の上の予告）
    m = new(27, 9)
    ellipse(m, 13.5, 4.5, 13.5, 4.5, DRED)
    ellipse(m, 13.5, 4.5, 12.0, 3.4, RED)
    hline(m, 7, 12, 2, C("ff8080"))
    save(m, "Attacks/Fx_BreakpointMark.png")

    # ブレークポイントの柱（縦に並べる。左右4ドットが縁）
    p = new(27, 18)
    for y in range(18):
        for x in range(27):
            e = min(x, 26 - x)
            if e <= 1:
                c = RED
            elif e <= 3:
                c = C("ff6060", 220)
            else:
                c = C("ff2020", 120 if y % 3 else 165)
            put(p, x, y, c)
    save(p, "Attacks/Fx_Pillar.png")

    # 能力の柱（白。実行中にアプリの色を掛けて使う。敵の柱は赤のまま）
    pn = new(27, 18)
    for y in range(18):
        for x in range(27):
            e = min(x, 26 - x)
            if e <= 1:
                c = WHITE
            elif e <= 3:
                c = C("ffffff", 220)
            else:
                c = C("ffffff", 120 if y % 3 else 165)
            put(pn, x, y, c)
    save(pn, "Attacks/Fx_PillarNeutral.png")

    # 赤い波線（横に並べる。左右1ドットが端）
    wv = new(12, 7)
    for x in range(12):
        y = (1, 2, 3, 4, 4, 3, 2, 1, 1, 2, 3, 4)[x]
        put(wv, x, y, RED)
        put(wv, x, y + 1, RED)
        put(wv, x, y + 2, DRED)
        put(wv, x, 6, C("ff0000", 70))
    save(wv, "Attacks/Fx_RedWave.png")

    # 影（落下物の予告）
    s = new(24, 6)
    ellipse(s, 12, 3, 12, 3, K)
    save(s, "Attacks/Fx_Shadow.png")

    # 「!」（撃つ雑魚の予備動作）
    rows = [
        ".KKK.",
        "KYYYK",
        "KYYYK",
        "KYYYK",
        ".KYK.",
        ".KYK.",
        "..K..",
        ".KKK.",
        "KYYYK",
        ".KKK.",
    ]
    save(ascii_art(rows, {"K": K, "Y": GOLD}), "Attacks/Fx_Alert.png")


# ───────── プレイヤーのエフェクト ─────────

def make_player_fx():
    # 近接の斬撃（3コマ、16×14）。右向き
    frames = []
    for i in range(3):
        im = new(16, 14)
        for y in range(14):
            for x in range(16):
                dx, dy = (x + 0.5 - 2.0) / 13.0, (y + 0.5 - 7.0) / 6.8
                d = math.sqrt(dx * dx + dy * dy)
                inner = math.sqrt(((x + 0.5 - 0.5) / 10.5) ** 2 + ((y + 0.5 - 7.0) / 5.2) ** 2)
                if d <= 1.0 and inner > 1.0:
                    ang = math.atan2(y + 0.5 - 7.0, x + 0.5 - 2.0)
                    if i == 0 and ang > 0.35:
                        continue
                    t = 1.0 - (inner - 1.0) * 2.2
                    c = WHITE if d > 0.86 else CREAM
                    if i == 2:
                        c = OLIVE if (x + y) % 2 == 0 else None
                    put(im, x, y, c)
        frames.append(im)
    save(sheet(frames), "Player/Fx_Slash.png")

    # 当たったときの火花（4コマ、11×11）
    frames = []
    pats = [
        [(5, 4), (5, 5), (5, 6), (4, 5), (6, 5)],
        [(5, 2), (5, 3), (5, 7), (5, 8), (2, 5), (3, 5), (7, 5), (8, 5), (4, 4), (6, 4), (4, 6), (6, 6), (5, 5), (5, 4), (5, 6), (4, 5), (6, 5)],
        [(5, 0), (5, 10), (0, 5), (10, 5), (2, 2), (8, 2), (2, 8), (8, 8), (5, 1), (1, 5), (9, 5), (5, 9)],
        [(5, 0), (0, 5), (10, 5), (5, 10), (1, 1), (9, 9)],
    ]
    cols = [WHITE, WHITE, CREAM, OLIVE]
    for i, pts in enumerate(pats):
        im = new(11, 11)
        for (x, y) in pts:
            put(im, x, y, cols[i] if not (i == 1 and (x, y) == (5, 5)) else CREAM)
        frames.append(im)
    save(sheet(frames), "Player/Fx_HitSpark.png")

    # 新しいタブ（瞬間移動）：小さなウィンドウが開く（4コマ、18×14）
    frames = []
    for i, (w, h) in enumerate(((4, 3), (8, 6), (13, 10), (18, 14))):
        im = new(18, 14)
        x0, y0 = (18 - w) // 2, (14 - h) // 2
        if i < 3:
            rect(im, x0, y0, x0 + w - 1, y0 + h - 1, WHITE if i == 0 else WDC)
            outline_rect(im, x0, y0, x0 + w - 1, y0 + h - 1, W51)
            if h >= 6:
                hline(im, x0 + 1, x0 + w - 2, y0 + 1, SKY)
        else:
            for x in range(18):
                if x % 2 == 0:
                    put(im, x, 0, WHITE); put(im, x, 13, WHITE)
            for y in range(14):
                if y % 2 == 0:
                    put(im, 0, y, WHITE); put(im, 17, y, WHITE)
        frames.append(im)
    save(sheet(frames), "Player/Fx_TabOpen.png")

    # 待機（デザイン担当の歩きの1コマ目から作った、頭が上下する2コマ）
    walk = Image.open(os.path.join(PROVIDED, "Player_Walk.png")).convert("RGBA")
    walk1 = walk.resize((walk.width // 3, walk.height // 3), Image.NEAREST)
    f0 = walk1.crop((0, 0, 13, 23))
    f1 = new(13, 23)
    head = f0.crop((0, 0, 13, 14))
    legs = f0.crop((0, 14, 13, 23))
    f1.paste(legs, (0, 14))
    f1.alpha_composite(head, (0, 1))
    save(sheet([f0, f1]), "Player/Player_Idle.png")

    # 小さい煙（デザイン担当の煙を半分にしたもの。雑魚が倒れたとき・ボスに当たったとき）
    smoke = Image.open(os.path.join(PROVIDED, "Fx_VirusSmoke.png")).convert("RGBA")
    s1 = smoke.resize((smoke.width // 3, smoke.height // 3), Image.NEAREST)  # 1コマ 49×52
    small = []
    for i in range(5):
        fr = s1.crop((i * 49, 0, i * 49 + 49, 52))
        small.append(fr.resize((25, 26), Image.NEAREST))
    save(sheet(small), "Effects/Fx_VirusSmokeSmall.png")


# ───────── 画面表示 ─────────

def make_hud():
    rows = [
        ".KK...KK.",
        "KRRK.KRRK",
        "KRWRKRRRK",
        "KRRRRRRRK",
        ".KRRRRRK.",
        "..KRRRK..",
        "...KRK...",
        "....K....",
    ]
    save(ascii_art(rows, {"K": K, "R": RED, "W": WHITE}), "UI/Hud_Heart.png")
    save(ascii_art(rows, {"K": K, "R": S1, "W": S1}), "UI/Hud_HeartEmpty.png")

    # 能力の枠（20×20）：ふつう・選択中
    for name, edge in (("Hud_Slot", W79), ("Hud_SlotSelected", ORANGE)):
        im = new(20, 20, WDC)
        outline_rect(im, 0, 0, 19, 19, K)
        outline_rect(im, 1, 1, 18, 18, edge)
        if name == "Hud_SlotSelected":
            outline_rect(im, 2, 2, 17, 17, ORANGE)
        else:
            hline(im, 2, 17, 2, WHITE)
            vline(im, 2, 2, 17, WHITE)
        save(im, f"UI/{name}.png")

    # 数字 1〜3（枠の下に出す）
    digits = {
        "1": [".W.", "WW.", ".W.", ".W.", "WWW"],
        "2": ["WW.", "..W", ".W.", "W..", "WWW"],
        "3": ["WW.", "..W", ".W.", "..W", "WW."],
    }
    for d, rows in digits.items():
        im = ascii_art(rows, {"W": WHITE})
        save(outline_alpha(im), f"UI/Hud_Digit{d}.png")

    # 白い1ドット（ゲージ・クールタイムの塗りに使う）と、ゲージの枠
    save(new(1, 1, WHITE), "UI/Hud_Pixel.png")
    g = new(12, 8, S0)
    outline_rect(g, 0, 0, 11, 7, K)
    outline_rect(g, 1, 1, 10, 6, W51)
    save(g, "UI/Hud_GaugeFrame.png")
    fill = new(1, 4, WHITE)
    put(fill, 0, 3, C("c8c8c8"))
    save(fill, "UI/Hud_GaugeFill.png")


def make_skill_icons():
    # 新しいタブ（16×16）
    im = new(16, 16)
    for y in range(1, 6):
        inset = max(0, 3 - y)
        hline(im, 1 + inset, 11 - inset, y, WHITE)
    for y in range(1, 6):
        inset = max(0, 3 - y)
        put(im, 1 + inset - 1 if inset else 1, y, K); put(im, 11 - inset + 1 if inset else 11, y, K)
    hline(im, 3, 9, 0, K)
    rect(im, 3, 3, 4, 4, SKY)
    rect(im, 0, 5, 15, 15, WHITE)
    outline_rect(im, 0, 5, 15, 15, K)
    hline(im, 1, 14, 14, WDC)
    rect(im, 7, 7, 8, 12, GREEN)
    rect(im, 5, 9, 10, 10, GREEN)
    save(im, "UI/Icon_NewTab.png")

    # ブレークポイント（16×16）
    im = new(16, 16, CODE_BG)
    outline_rect(im, 0, 0, 15, 15, K)
    ellipse(im, 5.0, 8.0, 3.6, 3.6, DRED)
    ellipse(im, 5.0, 8.0, 2.7, 2.7, RED)
    put(im, 4, 6, WHITE)
    for y, (b, c) in zip((3, 6, 9, 12), ((13, CODE_BLUE), (12, CODE_STR), (13, CODE_FN), (11, CODE_LBLUE))):
        hline(im, 10, b, y, c)
    save(im, "UI/Icon_Breakpoint.png")


# ───────── ボス B：ウイルスがアプリを着ている（2026-11-17 決定） ─────────
# 1枚の大きさは 92×80 ドット。ボスの当たり判定（4×3マス＝72×54）は、x 10〜81・y 20〜73 の位置（中心 (46, 47)）

BOSS_W, BOSS_H = 92, 80


def solid_blob(w, h, cx, cy, rx, ry, seed, wisps, down=False):
    """大きい煙：芯を広く、縁の帯を細くして、ざらつきすぎないようにする。down なら煙が下へたなびく"""
    rnd = random.Random(seed)
    im = new(w, h)
    ph = [rnd.uniform(0, math.tau) for _ in range(3)]
    for y in range(h):
        for x in range(w):
            ang = math.atan2(y + 0.5 - cy, x + 0.5 - cx)
            edge = 1.0 + 0.07 * math.sin(ang * 5 + ph[0]) + 0.05 * math.sin(ang * 9 + ph[1])
            d = math.hypot((x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry) / edge
            if d < 0.90:
                put(im, x, y, S0)
            elif d < 0.96:
                put(im, x, y, S1)
            elif d < 1.03 and rnd.random() < 0.5:
                put(im, x, y, S2 if rnd.random() < 0.5 else S1)
    for _ in range(wisps):  # 炎のような筋
        base = math.pi / 2 if down and rnd.random() < 0.5 else -math.pi / 2
        a = base + rnd.uniform(-1.2, 1.2)
        sx, sy = cx + math.cos(a) * rx * 0.95, cy + math.sin(a) * ry * 0.95
        for i in range(rnd.randint(3, 8)):
            x = int(round(sx + math.cos(a) * i + math.sin(i * 0.9 + ph[2]) * 0.6))
            y = int(round(sy + math.sin(a) * i))
            put(im, x, y, S1 if i < 2 else S2)
            if i < 2:
                put(im, x + 1, y, S0)
    return im


def arm(im, sx, sy, tx, ty, wobble):
    for t in range(16):
        x = int(round(sx + (tx - sx) * t / 15))
        y = int(round(sy + (ty - sy) * t / 15 + math.sin(t * 0.7 + wobble) * 1.4))
        rect(im, x, y, x + 1, y + 2, S0)
        put(im, x, y - 1, S1)


def make_boss_b():
    # 体（3コマで煙が揺れる）。浮く（Chrome）と、立つ（VS Code）
    for name, standing in (("BossB_Body_Float", False), ("BossB_Body_Stand", True)):
        frames = []
        for i in range(3):
            if standing:
                body = solid_blob(BOSS_W, BOSS_H, 46, 40, 37, 27, seed=500 + i, wisps=16)
                for lx in (30, 36, 56, 62):  # 短い煙の足（床＝y 73）
                    rect(body, lx, 64, lx + 3, 73, S0)
                    put(body, lx - 1, 72 - (i + lx) % 3, S1)
                    hline(body, lx - 1, lx + 4, 73, S0)
            else:
                body = solid_blob(BOSS_W, BOSS_H, 46, 42, 37, 29, seed=520 + i, wisps=20, down=True)
            arm(body, 12, 46, 22, 52, i)
            arm(body, 80, 46, 70, 54, i + 1)
            frames.append(body)
        save(sheet(frames), f"Bosses/{name}.png")

    # 手（ウィンドウの上に重ねる）
    hands = new(BOSS_W, BOSS_H)
    for (hx, hy) in ((19, 49), (68, 51)):
        ellipse(hands, hx + 2.5, hy + 2.5, 3.2, 3.0, S0)
        put(hands, hx + 1, hy, S1)
    save(hands, "Bosses/BossB_Hands.png")

    # 顔（30×12）：0 ふつう、1 まばたき、2 怒り、3 怒り（まばたき）
    frames = []
    for i in range(4):
        im = new(30, 12)
        angry, blink = i >= 2, i % 2 == 1
        for ex in (6, 19):
            if blink:
                hline(im, ex, ex + 4, 6, RED)
            else:
                rect(im, ex, 5, ex + 4, 8, RED)
                put(im, ex + (1 if not angry else 2), 5, WHITE)
                put(im, ex + (2 if not angry else 3), 5, WHITE if not angry else RED)
            if angry:  # 怒りの眉
                for k in range(6):
                    yy = 3 + (k // 2 if ex < 15 else (5 - k) // 2)
                    put(im, ex - 1 + k, yy, S1)
        if angry:  # 牙
            rect(im, 10, 10, 19, 11, S1)
            for x in range(10, 20, 2):
                put(im, x, 10, WHITE)
        frames.append(im)
    save(sheet(frames), "Bosses/BossB_Face.png")

    # 着ているウィンドウ（48×36）：0 きれい、1 小さなひび、2 大きなひびと欠け。タイトルバーはアプリの色
    def armor(content_file, title_colors, content_xy):
        content = Image.open(os.path.join(GEN, "Bosses", content_file)).convert("RGBA")
        frames = []
        for stage in range(3):
            w, h = 48, 36
            im = new(w, h, WDC)
            for i, c in enumerate((W51, W79, WHITE)):
                outline_rect(im, i, i, w - 1 - i, h - 1 - i, c)
            # タイトルバー（アイコンの色を左から並べる）
            for x in range(3, w - 3):
                seg = min(len(title_colors) - 1, (x - 3) * len(title_colors) // (w - 6))
                for y in range(3, 7):
                    put(im, x, y, title_colors[seg])
            rect(im, w - 9, 3, w - 4, 6, ORANGE)
            put(im, w - 7, 4, WHITE); put(im, w - 6, 5, WHITE); put(im, w - 6, 4, WHITE); put(im, w - 7, 5, WHITE)
            cx, cy = content_xy
            im.alpha_composite(content.crop((cx, cy, cx + w - 6, cy + h - 10)), (3, 7))
            if stage >= 1:  # ひび
                pts = [(31, 7), (28, 13), (32, 18), (27, 24), (30, 32)]
                for (a, b) in zip(pts, pts[1:]):
                    for t in range(10):
                        put(im, int(a[0] + (b[0] - a[0]) * t / 10), int(a[1] + (b[1] - a[1]) * t / 10), K)
                for (x, y) in ((9, 22), (11, 24), (13, 26), (10, 28)):
                    put(im, x, y, K); put(im, x + 1, y, K)
            if stage >= 2:  # 大きなひびと、欠けた角
                pts = [(5, 9), (10, 14), (8, 19), (14, 23)]
                for (a, b) in zip(pts, pts[1:]):
                    for t in range(8):
                        put(im, int(a[0] + (b[0] - a[0]) * t / 8), int(a[1] + (b[1] - a[1]) * t / 8), K)
                for y in range(h - 11, h):
                    for x in range(w - 14 + (h - y), w):
                        put(im, x, y, CLEAR)
                for y in range(h - 11, h):  # 欠けたふち
                    x = w - 14 + (h - y)
                    put(im, x - 1, y, W51)
            frames.append(im)
        return sheet(frames)

    save(armor("Content_Browser.png", [C("ea4335"), C("fbbc05"), C("34a853"), C("4285f4")], (0, 7)), "Bosses/BossB_Window_Browser.png")
    save(armor("Content_Editor.png", [C("007acc")], (4, 4)), "Bosses/BossB_Window_Editor.png")

    # 追い出されて逃げるウイルス（20×18、4コマ。足元が下の端）。怖がっている目
    frames = []
    for i in range(4):
        im = smoke_blob(20, 18, 10, 8.5, 7.2, 6.2, seed=600 + i, wisps=6)
        for ex in (8, 12):
            rect(im, ex, 6, ex + 2, 8, WHITE)
            put(im, ex + 1, 7, RED)
        hline(im, 9, 12, 11, WHITE)
        legs = ((7, 12), (8, 11), (7, 12), (6, 13))[i]
        for lx in legs:
            vline(im, lx, 15, 17, S0)
        frames.append(im)
    save(sheet(frames), "Enemies/Virus_Fleeing.png")

    # 破片（20×20、4コマ）：ウィンドウにひびが入ったとき
    frames = []
    rnd = random.Random(77)
    pieces = [(rnd.uniform(-1, 1), rnd.uniform(-1.2, 0.2)) for _ in range(6)]
    for i in range(4):
        im = new(20, 20)
        for k, (vx, vy) in enumerate(pieces):
            x = int(10 + vx * (2 + i * 2.2))
            y = int(10 + vy * (2 + i * 2.2) + i * i * 0.6)
            c = (WDC, W79, WHITE)[k % 3]
            rect(im, x, y, x + 1, y + (k % 2), c)
            put(im, x, y, W51)
        frames.append(im)
    save(sheet(frames), "Effects/Fx_Shards.png")

    # きらめき（9×9、4コマ）：きれいになったウィンドウのまわり
    frames = []
    for i, r in enumerate((1, 3, 4, 2)):
        im = new(9, 9)
        for d in range(-r, r + 1):
            put(im, 4 + d, 4, WHITE if abs(d) < r else CREAM)
            put(im, 4, 4 + d, WHITE if abs(d) < r else CREAM)
        frames.append(im)
    save(sheet(frames), "Effects/Fx_Sparkle.png")


# ───────── ステージのギミックと部屋（2026-11-17：ステージの種類） ─────────

DKEY = C("2a6fb0")  # タイトルバーの影（実行中にアプリの色の暗い色へ塗り替える）


def make_stage_gimmicks():
    # 開いたり閉じたりするウィンドウの足場（24×8、左右3ドットが縁。タイトルバー＝空色は、実行中にアプリの色へ塗り替える）
    im = new(24, 8, WDC)
    hline(im, 0, 23, 0, W51)
    for y in (1, 2):
        hline(im, 1, 22, y, SKY)
    rect(im, 20, 1, 21, 2, ORANGE)
    hline(im, 1, 22, 3, W79)
    hline(im, 0, 23, 7, W51)
    vline(im, 0, 0, 7, W51)
    vline(im, 23, 0, 7, W51)
    save(im, "Stage/Platform_Window.png")

    # エディタの部屋の中（36×18 を並べる。うすいコードの行）
    rnd = random.Random(11)
    room = new(36, 18, CODE_BG)
    for y in (3, 7, 11, 15):
        x = rnd.choice((2, 4, 6))
        while x < 34:
            ln = rnd.randint(3, 8)
            c = rnd.choice((C("262a31"), C("2a2f38"), C("233447")))
            hline(room, x, min(35, x + ln), y, c)
            x += ln + rnd.randint(2, 4)
    save(room, "Stage/Room_Editor.png")

    # 部屋のタイトルバー（18×6 を並べる。空色はアプリの色に塗り替える）
    tb = new(18, 6, SKY)
    hline(tb, 0, 17, 5, DKEY)
    save(tb, "Stage/Room_TitleBar.png")

    # 降ってくる通知（36×16）
    n = new(36, 16)
    rect(n, 1, 0, 34, 15, WHITE)
    rect(n, 0, 1, 35, 14, WHITE)
    for (x, y) in ((1, 0), (34, 0), (0, 1), (35, 1), (0, 14), (35, 14), (1, 15), (34, 15)):
        put(n, x, y, CLEAR)
    hline(n, 2, 33, 0, K); hline(n, 2, 33, 15, K); vline(n, 0, 2, 13, K); vline(n, 35, 2, 13, K)
    put(n, 1, 1, K); put(n, 34, 1, K); put(n, 1, 14, K); put(n, 34, 14, K)
    ellipse(n, 7.5, 8.0, 4.6, 4.6, DRED)
    ellipse(n, 7.5, 8.0, 3.8, 3.8, RED)
    vline(n, 7, 5, 8, WHITE); vline(n, 8, 5, 8, WHITE); put(n, 7, 10, WHITE); put(n, 8, 10, WHITE)
    hline(n, 14, 30, 5, W68); hline(n, 14, 26, 8, WA2); hline(n, 14, 28, 11, WA2)
    for k in range(3):
        put(n, 30 + k, 2 + k, W8B); put(n, 32 - k, 2 + k, W8B)
    save(n, "Hazards/Hazard_Notification.png")

    # ファイアウォール（炎の壁。18×18 を縦に並べる、3コマ）
    frames = []
    for f in range(3):
        im = new(18, 18)
        for y in range(18):
            for x in range(1, 17):
                cx = abs(x + 0.5 - 9) / 8.0
                heat = 1.0 - cx + 0.25 * math.sin(y * 0.9 + f * 2.1 + x * 0.7) + 0.15 * math.sin(y * 2.3 - f * 1.3)
                if heat > 0.9:
                    c = GOLD
                elif heat > 0.6:
                    c = C("ff8a00")
                elif heat > 0.35:
                    c = C("ff2a00")
                elif heat > 0.15:
                    c = C("b01000", 220)
                else:
                    continue
                put(im, x, y, c)
        frames.append(im)
    save(sheet(frames), "Hazards/Fx_Firewall.png")

    # ファイアウォールの発生装置（上下に置く。22×8）
    e = new(22, 8, C("333333"))
    outline_rect(e, 0, 0, 21, 7, K)
    hline(e, 1, 20, 1, C("4a4a4a"))
    for x in (4, 9, 14, 19):
        put(e, x - 1, 4, RED); put(e, x, 4, RED)
    save(e, "Hazards/Firewall_Emitter.png")


# ───────── カテゴリの能力（2026-11-17：ゲームの流れ） ─────────

def make_category_abilities():
    office, office_l = C("217346"), C("33a565")
    purple, pink = C("a259ff"), C("f24e1e")
    # 棒グラフ（Office・仕事）のアイコン
    im = new(16, 16, WHITE)
    outline_rect(im, 0, 0, 15, 15, K)
    hline(im, 2, 13, 13, W68)
    for x0, h in ((3, 4), (7, 7), (11, 10)):
        rect(im, x0, 13 - h, x0 + 2, 12, office)
        vline(im, x0, 13 - h, 12, office_l)
    save(im, "UI/Icon_BarGraph.png")
    # バケツ塗り（クリエイティブ）のアイコン
    im = new(16, 16, WHITE)
    outline_rect(im, 0, 0, 15, 15, K)
    for y in range(5, 13):  # バケツ（台形）
        inset = (y - 5) // 3
        hline(im, 3 + inset, 9 - inset, y, WA2)
        put(im, 3 + inset, y, W51); put(im, 9 - inset, y, W51)
    hline(im, 3, 9, 5, W51)
    rect(im, 9, 4, 12, 6, purple)  # こぼれる絵の具
    rect(im, 11, 7, 12, 10, purple)
    rect(im, 10, 11, 13, 13, purple)
    put(im, 12, 8, pink)
    save(im, "UI/Icon_Paint.png")
    # ボム（ゲーム）のアイコン
    im = new(16, 16, WHITE)
    outline_rect(im, 0, 0, 15, 15, K)
    ellipse(im, 7.0, 9.5, 4.6, 4.6, K)
    ellipse(im, 7.0, 9.5, 3.6, 3.6, C("2b2b33"))
    put(im, 5, 7, WHITE); put(im, 6, 7, C("8f8f9a"))
    rect(im, 9, 4, 10, 5, W68)
    put(im, 11, 3, ORANGE); put(im, 12, 2, GOLD); put(im, 12, 3, RED)
    save(im, "UI/Icon_Bomb.png")

    # ボムの弾（9×9）
    b = new(9, 9)
    ellipse(b, 4.5, 5.0, 3.6, 3.6, K)
    ellipse(b, 4.5, 5.0, 2.7, 2.7, C("2b2b33"))
    put(b, 3, 3, WHITE)
    put(b, 6, 1, ORANGE); put(b, 7, 0, GOLD)
    save(b, "Attacks/Bullet_Bomb.png")

    # 爆発（36×36。円の判定に合わせて伸び縮みさせる）
    e = new(36, 36)
    for y in range(36):
        for x in range(36):
            d = math.hypot(x + 0.5 - 18, y + 0.5 - 18) / 18.0
            n = 0.06 * math.sin(math.atan2(y - 18, x - 18) * 7)
            if d + n < 0.45:
                put(e, x, y, C("fff6c8", 230))
            elif d + n < 0.72:
                put(e, x, y, C("ffd23f", 220))
            elif d + n < 0.92:
                put(e, x, y, C("ff8a00", 200))
            elif d + n < 1.0:
                put(e, x, y, C("e04a00", 160))
    save(e, "Attacks/Fx_Blast.png")

    # バケツ塗り：塗りつぶした範囲（18×18 をタイルで並べる。上4ドットが表面、左右と下の2ドットが縁）
    pnt = new(18, 18)
    deep, light = C("7a3fd0"), C("c99bff")
    accents = (pink, C("1abcfe"), C("0acf83"))
    for y in range(18):
        for x in range(18):
            c = purple
            if x <= 1 or x >= 16 or y >= 16:
                c = deep
            put(pnt, x, y, c)
    for x in range(18):  # 表面（波打つ明るい線）
        top = 1 + (1 if (x // 3) % 2 else 0)
        for y in range(0, top):
            put(pnt, x, y, CLEAR)
        put(pnt, x, top, light)
        if x in (0, 17):
            for y in range(0, 4):
                put(pnt, x, y, CLEAR)
    for i, (x, y) in enumerate(((5, 7), (12, 10), (8, 13), (3, 11), (14, 6))):  # 混ざった絵の具の粒
        put(pnt, x, y, accents[i % 3])
    save(pnt, "Attacks/Fx_Paint.png")

    # 棒グラフの柱（縦に並べる。左右2ドットが縁）
    bar = new(18, 18)
    for y in range(18):
        for x in range(18):
            e2 = min(x, 17 - x)
            c = C("1a5c37") if e2 <= 1 else (office_l if e2 <= 3 else office)
            put(bar, x, y, c)
        if y % 6 == 0:
            hline(bar, 2, 15, y, C("2a8a52"))
    save(bar, "Attacks/Fx_Bar.png")

    # その場で作る能力の弾（8×8、灰色。ゲームの中でアプリの色に塗り替える）
    orb = new(8, 8)
    ellipse(orb, 4.0, 4.0, 3.6, 3.6, C("3a3a3a"))
    ellipse(orb, 4.0, 4.0, 2.7, 2.7, C("9a9a9a"))
    ellipse(orb, 3.6, 3.6, 1.4, 1.4, C("e6e6e6"))
    put(orb, 3, 2, WHITE)
    save(orb, "Attacks/Bullet_Orb.png")


# ───────── AI の能力「AIアシスタント」（2026-11-18） ─────────

AI_CYAN = C("5ff0ff")     # 目・レーザーの芯のまわり
AI_CYAN_D = C("3cc8e6")   # 羽の影・レーザーの外側
AI_WING = C("bff8ff")     # 羽
AI_NAVY = C("10162a")     # 画面


def line_points(x0, y0, x1, y1):
    """(x0, y0) から (x1, y1) までの点（ブレゼンハム）"""
    pts = []
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    while True:
        pts.append((x0, y0))
        if x0 == x1 and y0 == y1:
            break
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy
    return pts


def ai_wing(im, root_x, root_y, tip_x, tip_y):
    """羽：根元から先へ細くなる2枚重ね（上が明るい羽、下が影の羽）"""
    main = line_points(root_x, root_y, tip_x, tip_y)
    for i, (x, y) in enumerate(main):
        put(im, x, y, WHITE if i < len(main) - 1 else AI_WING)
        if i < len(main) - 1:
            put(im, x, y + 1, AI_WING)
        if i < len(main) - 2:
            put(im, x, y + 2, AI_CYAN_D)
    # 下の短い羽（羽根の重なり）
    mid_x = root_x + (tip_x - root_x) * 2 // 3
    mid_y = root_y + (tip_y - root_y) * 2 // 3 + 3
    for (x, y) in line_points(root_x, root_y + 2, mid_x, mid_y):
        put(im, x, y, AI_CYAN_D)


def ai_drone_frame(flap, light):
    """
    AI ロボット1コマ（線を付ける前は 24×11。右向き）。
    横長の長方形の体に画面（目）、左右に羽、頭にアンテナ、下に浮かぶための光。flap：羽の先の上下（-4〜2）
    """
    im = new(24, 11)
    # 羽（体の後ろ）
    ai_wing(im, 5, 5, 1, 5 + flap)
    ai_wing(im, 18, 5, 22, 5 + flap)
    # 体（横長の長方形）
    rect(im, 6, 3, 17, 9, WHITE)
    vline(im, 17, 4, 9, WA2)        # 右の影
    hline(im, 6, 17, 9, WA2)        # 下の影
    # 画面と目（右向きなので、目は少し右寄り）
    rect(im, 7, 4, 15, 8, AI_NAVY)
    for ex in (11, 14):
        put(im, ex, 5, AI_CYAN)
        put(im, ex, 6, AI_CYAN)
    # アンテナ（先が点滅する）
    vline(im, 11, 1, 2, W8B)
    put(im, 11, 0, AI_CYAN if light else ORANGE)
    # 浮かぶための光
    put(im, 11, 10, C("5ff0ff", 200))
    put(im, 12, 10, C("5ff0ff", 200))
    return outline_alpha(im)


def make_ai_assistant():
    # 羽ばたき4コマ（線込みで 26×13。上→水平→下→水平）
    flaps = (-4, -1, 2, -1)
    frames = [ai_drone_frame(f, i % 2 == 0) for i, f in enumerate(flaps)]
    save(sheet(frames), "Attacks/Summon_AiDrone.png")

    # レーザー（4×6。横のどの列も同じなので、横に伸ばしてもドットが崩れない。太さ6ドット＝1/3マス）
    beam = new(4, 6)
    for y, c in enumerate((C("3cc8e6", 120), C("5ff0ff", 230), WHITE, WHITE, C("5ff0ff", 230), C("3cc8e6", 120))):
        hline(beam, 0, 3, y, c)
    save(beam, "Attacks/Fx_Laser.png")

    # アイコン（16×16）：羽の生えたロボットが、下のウイルスへレーザーを撃っている
    im = new(16, 16, WHITE)
    outline_rect(im, 0, 0, 15, 15, K)
    for (x, y) in ((1, 3), (2, 4), (3, 5)):    # 左の羽
        put(im, x, y, AI_CYAN_D); put(im, x, y - 1, AI_WING)
    for (x, y) in ((14, 3), (13, 4), (12, 5)):  # 右の羽
        put(im, x, y, AI_CYAN_D); put(im, x, y - 1, AI_WING)
    rect(im, 4, 3, 11, 8, K)                    # 体（横長）
    rect(im, 5, 4, 10, 7, AI_NAVY)
    put(im, 7, 5, AI_CYAN); put(im, 9, 5, AI_CYAN)
    put(im, 7, 2, K); put(im, 7, 1, ORANGE)     # アンテナ
    for y in range(9, 12):                       # レーザー（真下へ）
        put(im, 7, y, WHITE); put(im, 6, y, AI_CYAN); put(im, 8, y, AI_CYAN)
    rect(im, 5, 12, 10, 14, S0)                  # ウイルス
    put(im, 6, 13, RED); put(im, 9, 13, RED)
    put(im, 4, 13, S1); put(im, 11, 13, S1)
    save(im, "UI/Icon_AiAssistant.png")


# ───────── AI チャットのボス（2026-11-19） ─────────
# 技の絵（レーザー・電流・ミニロボット）は、ゲームの中でアプリの色に塗り替える（ThemeRecolor.Effect：明るさを4段に分けて置き換える）。
# なので、色ではなく明るさの差で描く（暗い線・中間・明るい・ほぼ白）

PIX3 = {  # 3×5 の小さな文字（ERROR 用）
    "E": ["###", "#..", "##.", "#..", "###"],
    "R": ["##.", "#.#", "##.", "#.#", "#.#"],
    "O": [".#.", "#.#", "#.#", "#.#", ".#."],
}


def text3(im, x, y, s, c):
    for ch in s:
        rows = PIX3.get(ch)
        if rows:
            for yy, r in enumerate(rows):
                for xx, v in enumerate(r):
                    if v == "#":
                        put(im, x + xx, y + yy, c)
        x += 4


def ai_mini_bot(frame):
    """ミニロボット（12×10、線込みで 14×12）。四角い体に1つ目、小さな羽"""
    im = new(12, 10)
    up = frame % 2 == 0
    # 羽
    for x, y in ((0, 2 if up else 4), (1, 3 if up else 4)):
        put(im, x, y, C("cfcfcf"))
    for x, y in ((11, 2 if up else 4), (10, 3 if up else 4)):
        put(im, x, y, C("cfcfcf"))
    rect(im, 2, 2, 9, 8, C("9a9a9a"))
    hline(im, 3, 8, 2, WHITE)
    rect(im, 3, 4, 8, 6, C("3a3a3a"))
    rect(im, 5, 4, 6, 5, WHITE)      # 目
    put(im, 5, 9, C("cfcfcf")); put(im, 6, 9, C("cfcfcf"))  # 浮かぶ光
    put(im, 6, 0, WHITE); put(im, 6, 1, C("6a6a6a"))         # アンテナ
    return outline_alpha(im, C("1a1a1a"))


def make_ai_boss():
    # ミニロボット（2コマ）
    save(sheet([ai_mini_bot(i) for i in range(2)]), "Attacks/Summon_AiMini.png")

    # レーザー（4×16。横のどの列も同じ。外側ほど暗く、芯は白。太さは実行中に伸び縮みさせる）
    beam = new(4, 16)
    prof = [(0x50, 90), (0x70, 150), (0x90, 200), (0xa8, 230), (0xc8, 255), (0xe0, 255), (0xf4, 255), (0xff, 255)]
    for y in range(16):
        v, a = prof[min(y, 15 - y)]
        hline(beam, 0, 3, y, (v, v, v, a))
    save(beam, "Attacks/Fx_AiLaser.png")

    # 電流（3コマ、18×10。左右の端は真ん中の高さなので、横に並べるとつながる）
    rng = random.Random(1119)
    frames = []
    for f in range(3):
        im = new(18, 10)
        ys = [5] + [rng.randint(2, 7) for _ in range(5)] + [5]
        xs = [0, 3, 6, 9, 12, 15, 17]
        for i in range(len(xs) - 1):
            for (x, y) in line_points(xs[i], ys[i], xs[i + 1], ys[i + 1]):
                put(im, x, y, WHITE)
                if im.getpixel((x, max(0, y - 1)))[3] == 0:
                    put(im, x, y - 1, (0x9a, 0x9a, 0x9a, 150))
                if im.getpixel((x, min(9, y + 1)))[3] == 0:
                    put(im, x, y + 1, (0x9a, 0x9a, 0x9a, 150))
        # 枝分かれの小さな火花
        for _ in range(2):
            bx = rng.randint(2, 15)
            by = rng.choice([1, 8])
            put(im, bx, by, (0xcf, 0xcf, 0xcf, 220))
        frames.append(im)
    save(sheet(frames), "Attacks/Fx_AiSpark.png")

    # ERROR のウィンドウ（34×16）：赤いタイトルバー・閉じるボタン・白い中に「!」と赤い「ERROR」
    im = new(34, 16, WHITE)
    outline_rect(im, 0, 0, 33, 15, K)
    rect(im, 1, 1, 32, 4, C("d32f2f"))
    rect(im, 28, 1, 32, 4, ORANGE)
    put(im, 29, 2, WHITE); put(im, 31, 2, WHITE); put(im, 30, 3, WHITE)
    rect(im, 1, 5, 32, 14, C("fff3f3"))
    # 「!」のマーク（黄色の丸に黒い !）
    ellipse(im, 6.0, 10.0, 3.2, 3.2, C("ffd23f"))
    vline(im, 5, 8, 10, K); vline(im, 6, 8, 10, K)
    put(im, 5, 12, K); put(im, 6, 12, K)
    text3(im, 12, 8, "ERROR", C("c00000"))
    save(im, "Attacks/Fx_AiError.png")

    # アイコンが無いときの体（40×40）：角の丸い紺の四角に、白い吹き出しと考え中の点
    im = new(40, 40)
    for y in range(40):
        for x in range(40):
            dx = max(0, abs(x - 19.5) - 14.5)
            dy = max(0, abs(y - 19.5) - 14.5)
            if dx * dx + dy * dy <= 25:
                put(im, x, y, AI_NAVY)
    ellipse(im, 20, 18, 12.5, 9.5, WHITE)
    for (x, y) in line_points(12, 24, 9, 31):
        put(im, x, y, WHITE); put(im, x + 1, y, WHITE); put(im, x + 2, y - 1, WHITE)
    for cx in (14, 20, 26):
        rect(im, cx - 1, 17, cx + 1, 19, AI_CYAN_D)
    save(outline_alpha(im), "Bosses/Boss_AiDefaultIcon.png")


# ───────── チャットアプリの能力（2026-11-19：Slack・Discord・LINE） ─────────
# メッセージは1行分の厚み（中くらいの長さ）。中身の文字は描かない。
# 1つ目（Head）と、続けて送った2つ目以降（Body）の2枚。Slack・Discord は重ねて並べると1つにつながる（Body は上の線が無い）

LINE_G = C("7ddb4e")
LINE_GL = C("aaee86")
LINE_GD = C("2f7a1f")
SLACK_P = C("4a154b")
SLACK_PL = C("7c3085")
SLACK_COLORS = (C("e01e5a"), C("ecb22e"), C("2eb67d"), C("36c5f0"))
DISC_BG = C("1e2448")
DISC_LINE = C("0c0f26")
DISC_B = C("5865f2")
DISC_BL = C("a5adff")


def round_box(im, x0, y0, x1, y1, fill, line, r=2):
    """角の丸い四角（線あり）"""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            dx = max(x0 + r - x, 0, x - (x1 - r))
            dy = max(y0 + r - y, 0, y - (y1 - r))
            if dx * dx + dy * dy > r * r:
                continue
            edge = (x in (x0, x1) or y in (y0, y1) or
                    (dx > 0 and dy > 0 and (dx + 1) ** 2 + (dy + 1) ** 2 > r * r + 1))
            put(im, x, y, line if edge else fill)


def make_chat_apps():
    # 大きさ（2026-11-19：3つ積むと、3つとも高さ 26 ドット・幅 38 ドット。高さは最初の Slack・Discord の大きさにそろえた）
    W = 38
    LINE_H = 8              # LINE：吹き出し 8 ドット × 3 ＋ すき間 1 ドット × 2 ＝ 26
    HEAD_H, BODY_H = 10, 9  # Slack・Discord：10 ＋ 9 × 2 − 重ね 2 ＝ 26

    # ── LINE：自分が送った緑の吹き出し。1つ目だけ、プレイヤーの方（右向きのとき左上）にしっぽ ──
    #    （右上にしっぽを付けて描いてから、左右を反転する。左を向いたときは、ゲームの中でもう一度反転する）
    for tail in (True, False):
        im = new(W, LINE_H)
        round_box(im, 0, 0, W - 5, LINE_H - 1, LINE_G, LINE_GD, 2)
        hline(im, 3, W - 8, 1, LINE_GL)              # つや
        if tail:
            # 右上のしっぽ：吹き出しの右の端から、右上へとがった三角形
            for y in range(0, 4):
                x_end = W - 1 - y
                for x in range(W - 6, x_end + 1):
                    edge = y == 0 or x == x_end
                    put(im, x, y, LINE_GD if edge else LINE_G)
            for y in range(1, 4):
                put(im, W - 5, y, LINE_G)  # 吹き出しとつなげる（右の線を消す）
        save(im.transpose(Image.FLIP_LEFT_RIGHT), f"Attacks/Chat_Line_{'Head' if tail else 'Body'}.png")

    # ── Slack：白いメッセージに、ワンポイントの紫（アバター）。続けて送ると1つの枠につながる ──
    def slack_row(head):
        h = HEAD_H if head else BODY_H
        im = new(W, h, WHITE)
        vline(im, 0, 0, h - 1, C("3b3b3b")); vline(im, W - 1, 0, h - 1, C("3b3b3b"))
        hline(im, 0, W - 1, h - 1, C("3b3b3b"))
        if head:
            hline(im, 0, W - 1, 0, C("3b3b3b"))
            rect(im, 2, 2, 6, 6, SLACK_P)              # アバター（紫）
            put(im, 3, 3, SLACK_PL)
            rect(im, 9, 2, 18, 2, C("1d1c1d"))         # 名前（太字）
            rect(im, 20, 2, 22, 2, C("9a9a9a"))        # 時刻
            rect(im, 9, 5, W - 5, 5, C("c4c4c4"))      # 本文
        else:
            rect(im, 9, 4, W - 8, 4, C("c4c4c4"))      # 本文（行の真ん中）
        return im
    save(slack_row(True), "Attacks/Chat_Slack_Head.png")
    save(slack_row(False), "Attacks/Chat_Slack_Body.png")

    # ── Discord：深い青に、ワンポイントの Discord の青（アバター）。続けて送ると1つの枠につながる ──
    def disc_row(head):
        h = HEAD_H if head else BODY_H
        im = new(W, h, DISC_BG)
        vline(im, 0, 0, h - 1, DISC_LINE); vline(im, W - 1, 0, h - 1, DISC_LINE)
        hline(im, 0, W - 1, h - 1, DISC_LINE)
        if head:
            hline(im, 0, W - 1, 0, DISC_LINE)
            rect(im, 2, 2, 6, 6, DISC_B)               # アバター（丸。角を削る）
            for cx, cy in ((2, 2), (6, 2), (2, 6), (6, 6)):
                put(im, cx, cy, DISC_BG)
            put(im, 3, 4, WHITE); put(im, 5, 4, WHITE)
            rect(im, 9, 2, 18, 2, C("e6e8ff"))         # 名前
            rect(im, 20, 2, 22, 2, C("6b72a8"))        # 時刻
            rect(im, 9, 5, W - 5, 5, C("8e95c9"))      # 本文
        else:
            rect(im, 9, 4, W - 8, 4, C("8e95c9"))      # 本文（行の真ん中）
        vline(im, 1, 1 if head else 0, h - 2, DISC_B)  # 左の端に細い青の線
        return im
    save(disc_row(True), "Attacks/Chat_Discord_Head.png")
    save(disc_row(False), "Attacks/Chat_Discord_Body.png")

    # ── 当たったときのエフェクト（4コマ、16×16） ──
    def frames_of(draw):
        out = []
        for i in range(4):
            im = new(16, 16)
            draw(im, i)
            out.append(im)
        return sheet(out)

    def line_fx(im, i):  # 緑の輪が広がり、小さな吹き出しが飛ぶ
        r = 2.5 + i * 1.8
        for a in range(0, 360, 10):
            x = 7.5 + r * math.cos(math.radians(a)); y = 7.5 + r * math.sin(math.radians(a))
            put(im, int(round(x)), int(round(y)), LINE_G if i < 3 else LINE_GL)
        if i < 3:
            for k, (dx, dy) in enumerate(((1, -1), (-1, -1), (1, 1))):
                d = 3 + i * 2
                bx, by = 7 + dx * d, 7 + dy * d
                rect(im, bx - 1, by - 1, bx + 1, by, WHITE)
                put(im, bx + 1, by + 1, WHITE)
        if i == 0:
            rect(im, 6, 6, 9, 9, WHITE)

    def slack_fx(im, i):  # 4色の点が十字に飛び散る
        d = 1 + i * 2
        for k, (dx, dy) in enumerate(((0, -1), (1, 0), (0, 1), (-1, 0))):
            c = SLACK_COLORS[k]
            x, y = 7 + dx * d, 7 + dy * d
            size = 2 if i < 3 else 1
            rect(im, x, y, x + size - 1, y + size - 1, c)
            if i in (1, 2):  # 斜めの小さな点
                put(im, 7 + (dx - dy) * (d - 1), 7 + (dy + dx) * (d - 1), c)
        if i == 0:
            rect(im, 6, 6, 9, 9, SLACK_P)

    def disc_fx(im, i):  # 青い星がはじけ、きらめきが散る
        d = 2 + i * 2
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            for t in range(1, d + 1):
                c = WHITE if t < d - 1 else (DISC_BL if i < 2 else DISC_B)
                put(im, 7 + dx * t, 7 + dy * t, c)
        for dx, dy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            put(im, 7 + dx * (d - 1), 7 + dy * (d - 1), DISC_BL)
        if i < 2:
            rect(im, 6, 6, 8, 8, DISC_B if i else WHITE)

    save(frames_of(line_fx), "Attacks/Fx_ChatHit_Line.png")
    save(frames_of(slack_fx), "Attacks/Fx_ChatHit_Slack.png")
    save(frames_of(disc_fx), "Attacks/Fx_ChatHit_Discord.png")

    # ── 能力のアイコン（16×16）：吹き出しが3つ重なって飛んでいく ──
    def icon(bg, bubble, line, accent):
        im = new(16, 16, bg)
        outline_rect(im, 0, 0, 15, 15, K)
        for k, y in enumerate((3, 7, 11)):
            x0 = 3 + k
            round_box(im, x0, y - 1, x0 + 8, y + 1, bubble, line, 1)
            put(im, x0 + 9 + (1 if k else 0), y, accent)
        return im
    save(icon(WHITE, LINE_G, LINE_GD, LINE_GD), "UI/Icon_ChatLine.png")
    save(icon(WHITE, WHITE, C("3b3b3b"), SLACK_P), "UI/Icon_ChatSlack.png")
    save(icon(C("2b3160"), DISC_BG, DISC_LINE, DISC_B), "UI/Icon_ChatDiscord.png")


# ───────── チャットのボス（2026-11-19） ─────────
# ボスのメッセージは、プレイヤーの物より大きく（約1.5倍）、色を濁らせて、ウイルスに汚れたようなノイズを入れる

DIGIT3 = {
    "1": [".#.", "##.", ".#.", ".#.", "###"],
    "2": ["##.", "..#", ".#.", "#..", "###"],
    "3": ["##.", "..#", ".#.", "..#", "##."],
}


def corrupt(im, seed, colors, count):
    """ウイルスに汚れたようなノイズ：不透明な所に、暗い点と短い横線を散らす"""
    rng = random.Random(seed)
    w, h = im.size
    for _ in range(count):
        x, y = rng.randrange(2, w - 2), rng.randrange(1, h - 1)
        if im.getpixel((x, y))[3] == 0:
            continue
        c = rng.choice(colors)
        put(im, x, y, c)
        if rng.random() < 0.4 and im.getpixel((min(w - 1, x + 1), y))[3] > 0:
            put(im, x + 1, y, c)


def make_chat_boss():
    W = 56
    LINE_H = 13             # 13 × 3 ＋ すき間 1 × 2 ＝ 41
    HEAD_H, BODY_H = 16, 14  # 16 ＋ 14 × 2 − 重ね 2 ＝ 42
    GRIME = (C("1a1020"), C("2a1a30"), C("5a1020"))

    # ── LINE（濁った緑の吹き出し。1つ目だけ、ボスの方にしっぽ） ──
    lg, lgl, lgd = C("587a3e"), C("7d9a5e"), C("1f2c16")
    for tail in (True, False):
        im = new(W, LINE_H)
        round_box(im, 0, 0, W - 6, LINE_H - 1, lg, lgd, 3)
        hline(im, 4, W - 10, 1, lgl)
        if tail:
            for y in range(0, 5):
                x_end = W - 1 - y
                for x in range(W - 7, x_end + 1):
                    put(im, x, y, lgd if (y == 0 or x == x_end) else lg)
            for y in range(1, 5):
                put(im, W - 6, y, lg)
        corrupt(im, 11 if tail else 12, GRIME, 14)
        save(im.transpose(Image.FLIP_LEFT_RIGHT), f"Attacks/ChatBoss_Line_{'Head' if tail else 'Body'}.png")

    # ── Slack（くすんだ白に、濁った紫） ──
    def slack_row(head):
        h = HEAD_H if head else BODY_H
        im = new(W, h, C("b8b0a8"))
        out = C("1c1a1c")
        vline(im, 0, 0, h - 1, out); vline(im, W - 1, 0, h - 1, out)
        hline(im, 0, W - 1, h - 1, out)
        if head:
            hline(im, 0, W - 1, 0, out)
            rect(im, 3, 3, 11, 11, C("2e0d33"))
            put(im, 4, 4, C("5a2a62")); put(im, 5, 4, C("5a2a62"))
            rect(im, 14, 4, 28, 5, C("2a2628"))
            rect(im, 31, 5, 35, 5, C("7a726c"))
            rect(im, 14, 9, W - 7, 10, C("8a827c"))
        else:
            rect(im, 14, 6, W - 11, 7, C("8a827c"))
        corrupt(im, 21 if head else 22, GRIME, 16)
        return im
    save(slack_row(True), "Attacks/ChatBoss_Slack_Head.png")
    save(slack_row(False), "Attacks/ChatBoss_Slack_Body.png")

    # ── Discord（よどんだ深い青に、くすんだ Discord の青） ──
    def disc_row(head):
        h = HEAD_H if head else BODY_H
        im = new(W, h, C("12152c"))
        out = C("05060f")
        vline(im, 0, 0, h - 1, out); vline(im, W - 1, 0, h - 1, out)
        hline(im, 0, W - 1, h - 1, out)
        if head:
            hline(im, 0, W - 1, 0, out)
            ellipse(im, 7.5, 7.5, 4.6, 4.6, C("383f9a"))
            put(im, 6, 7, C("c0c4e8")); put(im, 9, 7, C("c0c4e8"))
            rect(im, 14, 4, 28, 5, C("aab0d8"))
            rect(im, 31, 5, 35, 5, C("4a5080"))
            rect(im, 14, 9, W - 7, 10, C("5e6496"))
        else:
            rect(im, 14, 6, W - 11, 7, C("5e6496"))
        vline(im, 1, 1 if head else 0, h - 2, C("383f9a"))
        corrupt(im, 31 if head else 32, (C("000000"), C("2a0a18"), C("5a1020")), 16)
        return im
    save(disc_row(True), "Attacks/ChatBoss_Discord_Head.png")
    save(disc_row(False), "Attacks/ChatBoss_Discord_Body.png")

    # ── そのほかのアプリ用（灰色。ゲームの中でアプリの色に塗り替える。吹き出しの形） ──
    for head in (True, False):
        im = new(W, LINE_H)
        round_box(im, 0, 0, W - 1, LINE_H - 1, C("9a9a9a"), C("2a2a2a"), 3)
        hline(im, 4, W - 5, 1, C("cfcfcf"))
        rect(im, 6, 5, W - 12 if head else W - 18, 6, C("6a6a6a"))
        corrupt(im, 41 if head else 42, (C("3a3a3a"), C("4a4a4a")), 14)
        save(im, f"Attacks/ChatBoss_Generic_{'Head' if head else 'Body'}.png")

    # ── チャットbot（歩く2コマ、16×18。原点は足元。灰色：ゲームの中でアプリの色に塗り替える） ──
    frames = []
    for f in range(2):
        im = new(16, 18)
        # 頭（吹き出しの形の画面）
        round_box(im, 1, 1, 14, 10, C("cfcfcf"), C("2a2a2a"), 2)
        rect(im, 3, 3, 12, 8, C("3a3a3a"))
        put(im, 5, 5, WHITE); put(im, 6, 5, WHITE); put(im, 9, 5, WHITE); put(im, 10, 5, WHITE)
        hline(im, 6, 9, 7, C("9a9a9a"))
        put(im, 3, 11, C("2a2a2a")); put(im, 4, 11, C("cfcfcf")); put(im, 4, 12, C("2a2a2a"))  # 吹き出しのしっぽ
        put(im, 8, 0, WHITE)  # アンテナ
        # 体と足（2コマで足を入れ替える）
        rect(im, 5, 11, 10, 14, C("9a9a9a"))
        outline_rect(im, 5, 11, 10, 14, C("2a2a2a"))
        if f == 0:
            vline(im, 6, 15, 17, C("2a2a2a")); vline(im, 9, 15, 16, C("2a2a2a")); put(im, 10, 16, C("2a2a2a"))
        else:
            vline(im, 9, 15, 17, C("2a2a2a")); vline(im, 6, 15, 16, C("2a2a2a")); put(im, 5, 16, C("2a2a2a"))
        frames.append(im)
    save(sheet(frames), "Enemies/Zako_Chatbot.png")

    # ── 通知のベル（4コマ、22×22。回収する物）：0 止まっている、1〜3 揺れる ──
    gold, gold_l, gold_d = C("ffd23f"), C("fff1a8"), C("b07d12")

    def bell(shift):
        im = new(22, 22)
        cx = 11 + shift
        rect(im, cx - 1, 1, cx, 2, gold_d)                   # 上のつまみ
        for y in range(3, 15):                                # 本体（下へ広がる）
            half = 3 + (y - 3) * 5 // 11
            hline(im, cx - half, cx + half - 1, y, gold)
            put(im, cx - half, y, gold_d); put(im, cx + half - 1, y, gold_d)
        hline(im, cx - 9, cx + 8, 15, gold_d)                 # ふち
        hline(im, cx - 8, cx + 7, 16, gold_d)
        vline(im, cx - 3, 5, 12, gold_l)                       # つや
        ellipse(im, cx, 18.5, 2.2, 2.2, gold_d)               # 舌
        return outline_alpha(im)
    save(sheet([bell(0), bell(-2), bell(2), bell(-1)]), "Attacks/Fx_Bell.png")

    # ── 残りの数のバッジ（3コマ、11×11）：赤い丸に白い数字（Mac の通知のように） ──
    badges = []
    for d in "123":
        im = new(11, 11)
        ellipse(im, 5.5, 5.5, 5.3, 5.3, C("ff3b30"))
        for yy, r in enumerate(DIGIT3[d]):
            for xx, v in enumerate(r):
                if v == "#":
                    put(im, 4 + xx, 3 + yy, WHITE)
        badges.append(im)
    save(sheet(badges), "Attacks/Fx_NotifBadge.png")


# ───────── Photoshop の能力（2026-11-19：筆とインク） ─────────

PS_NAVY = C("001e36")
PS_BLUE = C("31a8ff")
PS_BLUE_L = C("8fd3ff")
PS_BLUE_D = C("1473c2")


def make_photoshop():
    # 筆（56×8、線込みで 58×10＝約3.2マス。近接の約3倍の長さ。右向き。原点は持ち手の端＝左の真ん中）：
    # 紺の持ち手に青い帯、銀の口金、先に青いインクの付いた毛
    im = new(56, 8)
    rect(im, 0, 3, 28, 4, PS_NAVY)                 # 持ち手
    hline(im, 1, 27, 3, C("0d3556"))
    rect(im, 20, 3, 22, 4, PS_BLUE)                # 青い帯
    rect(im, 29, 2, 34, 5, C("c8ccd4"))            # 口金
    hline(im, 29, 34, 2, WHITE); hline(im, 29, 34, 5, C("8a8f99"))
    for x in range(35, 56):                        # 毛（先へ細くなる）
        t = (x - 35) / 20
        half = 3 - int(t * 2.6)
        for y in range(4 - half, 4 + half):
            c = C("e8d9b5") if t < 0.45 else (PS_BLUE if t < 0.85 else PS_BLUE_L)
            put(im, x, y, c)
    put(im, 55, 3, PS_BLUE_D); put(im, 54, 4, PS_BLUE_D)
    save(outline_alpha(im, C("0a0f1a")), "Attacks/Brush_Photoshop.png")

    # インク（20×20）：スプラトゥーンのインクのような、ふちが波打つしぶきと、まわりの小さな粒
    ink = new(20, 20)
    for y in range(20):
        for x in range(20):
            dx, dy = x + 0.5 - 10, y + 0.5 - 10.5
            ang = math.atan2(dy, dx)
            r = 6.6 + 0.7 * math.sin(ang * 5 + 0.6) + 0.5 * math.sin(ang * 3 - 1.1) + 0.3 * math.sin(ang * 8)
            d = math.hypot(dx, dy)
            if d <= r:
                c = PS_BLUE_D if d > r - 1.2 else PS_BLUE
                if dy < -2 and d < r - 2.2 and dx < 1:
                    c = PS_BLUE_L
                put(ink, x, y, c)
    for (x, y, rr) in ((2.5, 5.5, 1.5), (17.5, 4.5, 1.2), (16.5, 18.0, 1.3)):  # 粒
        ellipse(ink, x, y, rr, rr, PS_BLUE)
    put(ink, 7, 6, WHITE); put(ink, 8, 6, WHITE); put(ink, 7, 7, WHITE)  # つや
    save(outline_alpha(ink, C("0a2a4a")), "Attacks/Fx_PhotoshopInk.png")

    # アイコン（16×16）：紺の四角に、青い筆の跡としぶき
    ic = new(16, 16, PS_NAVY)
    outline_rect(ic, 0, 0, 15, 15, K)
    for i, (x, y) in enumerate(line_points(3, 12, 11, 4)):
        put(ic, x, y, C("e8d9b5") if i < 3 else PS_BLUE)
        put(ic, x + 1, y, PS_BLUE_D)
    ellipse(ic, 11.5, 11.5, 2.4, 2.4, PS_BLUE)
    put(ic, 13, 8, PS_BLUE_L); put(ic, 8, 13, PS_BLUE_L); put(ic, 11, 10, WHITE)
    save(ic, "UI/Icon_Photoshop.png")


# ───────── Photoshop のボス（2026-11-19） ─────────

def rough_splat(im, cx, cy, base, rng, colors, rough=0.9):
    """ふちが荒れたしぶき（colors：外側から内側の順）"""
    w, h = im.size
    for y in range(h):
        for x in range(w):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            ang = math.atan2(dy, dx)
            r = base + 1.1 * math.sin(ang * 5 + 0.4) + 0.7 * math.sin(ang * 9 - 1.3) + rng.uniform(-rough, rough)
            d = math.hypot(dx, dy)
            if d <= r:
                k = d / max(0.1, r)
                c = colors[0] if k > 0.82 else colors[1] if k > 0.45 else colors[2]
                put(im, x, y, c)


def make_photoshop_boss():
    rng = random.Random(2611)
    # ボスのインク（26×26。荒れていて、汚れの点が入る。灰色：ゲームの中でアプリの色に塗り替える）
    im = new(26, 26)
    rough_splat(im, 13, 13.5, 8.2, rng, (C("3a3a3a"), C("7a7a7a"), C("a8a8a8")))
    for (x, y, rr) in ((3.0, 5.0, 1.6), (22.5, 4.0, 1.3), (21.5, 22.0, 1.8), (4.5, 21.5, 1.2), (13.0, 1.8, 1.0)):
        ellipse(im, x, y, rr, rr, C("7a7a7a"))
    corrupt(im, 77, (C("1a1a1a"), C("2a2a2a"), C("cfcfcf")), 26)
    put(im, 9, 8, WHITE); put(im, 10, 8, WHITE); put(im, 9, 9, WHITE)
    save(outline_alpha(im, C("101010")), "Attacks/Fx_BossInk.png")

    # 攻撃の範囲（30×30、白。ゲームの中で赤く塗って点滅させる）：プレイヤーのインクと同じしぶきの形で、ふちが荒れている
    im = new(30, 30)
    rough_splat(im, 15, 15, 11.0, rng, (WHITE, WHITE, WHITE), rough=1.4)
    for _ in range(18):  # 欠け
        x, y = rng.randrange(4, 26), rng.randrange(4, 26)
        if math.hypot(x - 15, y - 15) > 7:
            put(im, x, y, CLEAR)
    for (x, y, rr) in ((2.5, 6.0, 1.4), (27.0, 8.0, 1.2), (25.5, 26.0, 1.6), (4.0, 25.0, 1.1)):
        ellipse(im, x, y, rr, rr, WHITE)
    save(im, "Attacks/Fx_InkDanger.png")

    # インクの生命体（歩く2コマ、28×30。原点は足元）：カラフルなインクでドロドロの人形
    mag, cya, yel, pur = C("ff3ea5"), C("29d3ff"), C("ffd23f"), C("8a4dff")
    frames = []
    for f in range(2):
        im = new(28, 30)
        ellipse(im, 14, 8.5, 7.5, 7.0, mag)                  # 頭
        ellipse(im, 11, 6.5, 3.5, 3.0, pur)
        rect(im, 8, 13, 19, 23, cya)                          # 体
        ellipse(im, 13.5, 18, 6.5, 6.0, cya)
        ellipse(im, 16, 20, 3.0, 3.0, yel)
        # 腕（だらんと垂れる。コマで少し揺れる）
        sway = 1 if f == 0 else -1
        for (x0, y0, x1, y1, c) in ((7, 14, 4, 21 + sway, mag), (20, 14, 23, 21 - sway, yel)):
            for (x, y) in line_points(x0, y0, x1, y1):
                put(im, x, y, c); put(im, x + 1, y, c)
        # 足（2コマで入れ替える）
        if f == 0:
            rect(im, 9, 24, 11, 29, pur); rect(im, 16, 24, 18, 27, mag)
        else:
            rect(im, 9, 24, 11, 27, pur); rect(im, 16, 24, 18, 29, mag)
        # 垂れるインク
        for (x, y0, ln, c) in ((6, 10, 3, mag), (21, 11, 4, mag), (12, 22, 3, cya), (19, 22, 2, yel), (4, 21, 3, mag)):
            vline(im, x, y0, y0 + ln + f, c)
            put(im, x, y0 + ln + f + 1, c)
        # 目
        rect(im, 10, 7, 12, 9, WHITE); rect(im, 15, 7, 17, 9, WHITE)
        put(im, 11, 8, K); put(im, 16, 8, K)
        hline(im, 11, 16, 11, C("5a1040"))
        frames.append(outline_alpha(im, C("2a1030")))
    save(sheet(frames), "Enemies/Zako_InkBlob.png")

    # マウスカーソル（12×18。原点は矢印の先＝左上）
    rows = [
        "K...........",
        "KK..........",
        "KWK.........",
        "KWWK........",
        "KWWWK.......",
        "KWWWWK......",
        "KWWWWWK.....",
        "KWWWWWWK....",
        "KWWWWWWWK...",
        "KWWWWWWWWK..",
        "KWWWWWWKKKK.",
        "KWWWKWWK....",
        "KWWKKWWK....",
        "KWK..KWWK...",
        "KK...KWWK...",
        "K.....KWWK..",
        "......KWWK..",
        ".......KK...",
    ]
    save(ascii_art(rows, {"K": K, "W": WHITE}), "Attacks/Fx_MouseCursor.png")

    # 選択範囲の点線（2コマ、8×2。横に並べる。コマを替えると点線が流れる）
    frames = []
    for f in range(2):
        im = new(8, 2)
        for x in range(8):
            c = K if ((x + f * 4) // 4) % 2 == 0 else WHITE
            vline(im, x, 0, 1, c)
        frames.append(im)
    save(sheet(frames), "Attacks/Fx_Marquee.png")

    # 状態異常の印（14×14）：速い（黄色の稲妻）・遅い（青い下向きの矢印）・クールタイムが長い（紫の時計に＋）・ジャンプが高い（緑の上向きの矢印）
    def status(draw):
        im = new(14, 14, C("1e1e2a"))
        draw(im)
        return im
    def fast(im):
        for (x, y) in line_points(8, 1, 4, 7) + line_points(4, 7, 9, 7) + line_points(9, 7, 5, 12):
            put(im, x, y, GOLD); put(im, x + 1, y, GOLD)
    def slow(im):
        for k in (0, 5):
            for i in range(4):
                hline(im, 3 + i, 10 - i, 3 + k + i, C("6ab0ff"))
    def cooldown(im):
        ellipse(im, 6.5, 7.5, 5.2, 5.2, C("b34dff"))
        ellipse(im, 6.5, 7.5, 4.0, 4.0, C("1e1e2a"))
        vline(im, 6, 4, 7, WHITE); hline(im, 6, 8, 7, WHITE)
        hline(im, 10, 13, 2, GOLD); vline(im, 11, 0, 4, GOLD); put(im, 12, 1, GOLD); put(im, 12, 3, GOLD)
    def jump(im):
        for i in range(5):
            hline(im, 6 - i, 7 + i, 2 + i, C("39cf16"))
        rect(im, 5, 7, 8, 12, C("39cf16"))
    for name, fn in (("Fast", fast), ("Slow", slow), ("Cooldown", cooldown), ("Jump", jump)):
        save(status(fn), f"UI/Status_{name}.png")


# ───────── ブラウザの能力「パケット」（2026-11-19） ─────────

def make_packet():
    blue, blue_l, blue_d = C("4285f4"), C("8ab4ff"), C("1a4fb0")
    green, green_l, green_d = C("34a853"), C("8ee6a4"), C("16692f")

    def bar(main, light, dark, plus):
        # 16×4（右向き）：先が明るく、データの白い点が並ぶ棒
        im = new(16, 4)
        rect(im, 0, 0, 15, 3, main)
        hline(im, 0, 15, 0, light)
        hline(im, 0, 15, 3, dark)
        for x in range(1, 12, 3):
            put(im, x, 1, WHITE); put(im, x + 1, 2, WHITE if plus else light)
        rect(im, 13, 0, 15, 3, WHITE)                  # 先（明るい）
        put(im, 0, 0, CLEAR); put(im, 0, 3, CLEAR)
        if plus:                                      # 応答：先に「＋」
            put(im, 14, 1, green_d); put(im, 14, 2, green_d); put(im, 13, 1, green_d); put(im, 15, 2, green_d)
        return im
    save(bar(blue, blue_l, blue_d, False), "Attacks/Fx_Packet.png")
    save(bar(green, green_l, green_d, True), "Attacks/Fx_PacketResponse.png")

    # アイコン（16×16）：地球（ブラウザ）から、パケットが3本飛んでいく
    im = new(16, 16, WHITE)
    outline_rect(im, 0, 0, 15, 15, K)
    ellipse(im, 5.5, 10.5, 4.2, 4.2, blue)
    vline(im, 5, 7, 14, blue_l); hline(im, 2, 9, 10, blue_l)
    ellipse(im, 5.5, 10.5, 2.0, 4.2, None)
    for (x, y) in ((8, 3), (10, 5), (9, 7)):          # パケット（枠の内側に収める）
        hline(im, x, x + 3, y, blue_d); put(im, x + 3, y, blue_l)
    put(im, 12, 9, green); put(im, 13, 9, green)        # 応答
    save(im, "UI/Icon_Packet.png")


# ───────── ブラウザのボス（2026-11-19） ─────────

def browser_window(content_bg, favicon):
    """ブラウザのウィンドウ（48×34）：枠・タブ1つ（左に小さなアイコン）・アドレスバー（鍵）・中身の色"""
    W, H = 48, 34
    im = new(W, H, content_bg)
    rect(im, 0, 0, W - 1, 8, C("dee1e6"))            # タブの並び
    rect(im, 3, 2, 20, 8, WHITE)                      # タブ
    rect(im, 5, 4, 6, 5, favicon)
    hline(im, 8, 17, 4, C("9aa0a6"))
    for (x, c) in ((W - 10, C("fbbc05")), (W - 7, C("34a853")), (W - 4, C("ea4335"))):  # ウィンドウのボタン
        rect(im, x, 3, x + 1, 4, c)
    rect(im, 0, 9, W - 1, 13, WHITE)                  # アドレスバーの帯
    rect(im, 3, 10, W - 4, 12, C("f1f3f4"))
    rect(im, 5, 10, 6, 12, C("5f6368"))               # 鍵
    hline(im, 9, 26, 11, C("9aa0a6"))
    outline_rect(im, 0, 0, W - 1, H - 1, C("3c4043"))
    return im


def make_browser_boss():
    # 爆弾のマークのサイト（下の3行は、ゲームの中で読み込みバーを描くので空けておく）
    im = browser_window(WHITE, C("ea4335"))
    ellipse(im, 24.0, 21.5, 6.2, 6.2, C("202124"))    # 爆弾
    ellipse(im, 24.0, 21.5, 5.0, 5.0, C("3c4043"))
    put(im, 21, 18, WHITE); put(im, 22, 18, WHITE); put(im, 21, 19, WHITE)
    rect(im, 27, 14, 29, 15, C("5f6368"))             # 口金
    put(im, 30, 13, C("b07d12")); put(im, 31, 12, ORANGE); put(im, 32, 11, GOLD); put(im, 31, 11, GOLD); put(im, 32, 12, RED)  # 導火線と火花
    hline(im, 9, 15, 17, C("dadce0")); hline(im, 33, 39, 17, C("dadce0"))  # 本文の線（飾り）
    hline(im, 9, 14, 24, C("dadce0")); hline(im, 34, 39, 24, C("dadce0"))
    save(im, "Attacks/Window_BombSite.png")

    # 雑魚を出し続けるウィンドウ：暗い赤の中に、ウイルスの顔と「!」
    im = browser_window(C("2a0a12"), C("ff3b30"))
    blob = smoke_blob(20, 16, 10, 9, 7, 6, seed=1119, wisps=5)
    paste(im, blob, 14, 15)
    for (x, y) in ((20, 20), (26, 20)):
        rect(im, x, y, x + 1, y + 1, RED)
    for x0 in (6, 38):                                 # 「!」
        rect(im, x0, 17, x0 + 1, 24, GOLD)
        rect(im, x0, 27, x0 + 1, 28, GOLD)
    save(im, "Enemies/Window_Spawner.png")


SECTIONS = {
    "background": lambda: make_background(),
    "terrain": lambda: make_terrain(),
    "windows": lambda: make_windows(),
    "app_windows": lambda: make_app_windows(),
    "boss_contents": lambda: make_boss_contents(),
    "viruses": lambda: make_viruses(),
    "attacks": lambda: make_attacks(),
    "player_fx": lambda: make_player_fx(),
    "hud": lambda: make_hud(),
    "skill_icons": lambda: make_skill_icons(),
    "boss_b": lambda: make_boss_b(),
    "stage_gimmicks": lambda: make_stage_gimmicks(),
    "category_abilities": lambda: make_category_abilities(),
    "ai_assistant": lambda: make_ai_assistant(),
    "ai_boss": lambda: make_ai_boss(),
    "chat_apps": lambda: make_chat_apps(),
    "chat_boss": lambda: make_chat_boss(),
    "photoshop": lambda: make_photoshop(),
    "photoshop_boss": lambda: make_photoshop_boss(),
    "packet": lambda: make_packet(),
    "browser_boss": lambda: make_browser_boss(),
}


def main():
    """引数なしなら全部。名前を渡すと、その部分だけ作る（例：python3 Tools/art/gen_sprites.py ai_assistant）"""
    import sys
    names = sys.argv[1:] or list(SECTIONS)
    for n in names:
        if n not in SECTIONS:
            raise SystemExit(f"unknown section: {n}（{', '.join(SECTIONS)}）")
        SECTIONS[n]()
    print("done:", GEN)


if __name__ == "__main__":
    main()
