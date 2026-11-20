#!/usr/bin/env python3
"""
ウィンドウのステージの壁紙を作る（2026-11-20。docs/window-stage-and-pickup-design.md 1-6）。
  python3 Tools/art/appbg/mac_desktop.py                 → Assets/Resources/AppBackgrounds/Bg_MacDesktop.png
  python3 Tools/art/appbg/mac_desktop.py --out FILE.png  （確かめる用。別の所へ書く）

・元：Assets/Resources/DesktopWallpaper.png（ゲームの Mac のデスクトップの壁紙。6000×3750。ほぼ黒）
・まん中で 16:9 に切る → 320×180 に小さく（LANCZOS。1マス 15ドット＝今の背景と同じ大きさ）
・明るくする（各色 c × GAIN + LIFT）。元がほぼ黒で、黒い敵が沈むため（FORMAT.md「見やすさの決まり」1）
・少しだけ色を薄くする（SAT。まん中のステッカーの赤が、予告の赤とまぎれないように。決まり 2・4）
・.meta は書かない（Unity が作る。取り込みの設定は Assets/Scripts/Editor/Art/AppBackgroundImport.cs がフォルダで入れる）
数はすべて【仮】
"""
import argparse
import os

from PIL import Image, ImageEnhance

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
SRC = os.path.join(ROOT, "Assets", "Resources", "DesktopWallpaper.png")
OUT = os.path.join(ROOT, "Assets", "Resources", "AppBackgrounds", "Bg_MacDesktop.png")
W, H = 320, 180

GAIN = 0.8   # 【仮】c × GAIN + LIFT（doc 1-6）
LIFT = 60   # doc は 52。感染のもや（DesktopDecay）が重なっても黒い敵が沈まないよう、少し上げた
SAT = 0.8    # 【仮】彩度（1＝そのまま）


def build(src_path):
    im = Image.open(src_path).convert("RGB")
    sw, sh = im.size
    # まん中で 16:9 に切る
    if sw * 9 >= sh * 16:
        cw, ch = sh * 16 // 9, sh
    else:
        cw, ch = sw, sw * 9 // 16
    left, top = (sw - cw) // 2, (sh - ch) // 2
    im = im.crop((left, top, left + cw, top + ch))
    im = im.resize((W, H), Image.LANCZOS)
    if SAT != 1.0:
        im = ImageEnhance.Color(im).enhance(SAT)
    im = im.point(lambda c: max(0, min(255, int(round(c * GAIN + LIFT)))))
    return im.convert("RGBA")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--src", default=SRC)
    ap.add_argument("--out", default=OUT)
    args = ap.parse_args()
    im = build(args.src)
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    im.save(args.out)
    print(f"wrote {args.out} {im.size}")


if __name__ == "__main__":
    main()
