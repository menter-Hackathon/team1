#!/usr/bin/env python3
"""
ゲームの音を全部作り直すスクリプト（2026-11-19）。

Tools/audio/se/*.json（SE のレシピ）と Tools/audio/bgm/*.json（BGM・ジングルの楽譜）から
Assets/Resources/Audio/SE/<id>.wav（44100 Hz）と Assets/Resources/Audio/BGM/<id>.wav（32000 Hz）を書き出す。
決まりは Tools/audio/FORMAT.md。必要なもの：Python 3・numpy・scipy。

使い方（リポジトリの一番上で）
  python3 Tools/audio/build_audio.py                 全部作る
  python3 Tools/audio/build_audio.py --only se_a,bgm_title   この id だけ
  python3 Tools/audio/build_audio.py --se-only / --bgm-only
  python3 Tools/audio/build_audio.py --prune         レシピが無くなった se_*/bgm_*/jingle_* の WAV（と .meta）を消す
  python3 Tools/audio/build_audio.py --out DIR       DIR/SE・DIR/BGM に書く（試し用）
  python3 Tools/audio/build_audio.py --data DIR      DIR/se・DIR/bgm のデータを使う（試し用）
  python3 Tools/audio/build_audio.py --check         作らずにデータを調べるだけ

まちがいがあっても残りは作り続け、最後にまとめて表示する。1つでも失敗したら終了コードは 1。
"""
import sys

sys.dont_write_bytecode = True  # リポジトリに __pycache__ を作らない

import os  # noqa: E402

os.environ["PYTHONDONTWRITEBYTECODE"] = "1"  # 並べて作る子のプロセスも

import argparse  # noqa: E402
import glob  # noqa: E402
import json  # noqa: E402
import time  # noqa: E402
from concurrent.futures import ProcessPoolExecutor  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

import synth  # noqa: E402
from synth.core import SynthError  # noqa: E402

DEFAULT_OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")
PRUNE_PREFIXES = ("se_", "bgm_", "jingle_")


DATA_DIR = [HERE]


def rel(p):
    """表示用の短いパス（リポジトリの中ならリポジトリから、外ならデータの場所から）。"""
    r = os.path.relpath(p, ROOT)
    if not r.startswith(".."):
        return r
    r = os.path.relpath(p, DATA_DIR[0])
    return p if r.startswith("..") else r


def load_json(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def collect(data_dir, errors, warnings):
    """データを全部読む。戻り値 (se_list, song_list, parse_failed)。
    se_list = [(file, sound_dict)]、song_list = [(file, song_dict)]。同じ id が2つあれば2つ目はまちがい。"""
    se_list, songs = [], []
    seen = {}
    parse_failed = False
    for path in sorted(glob.glob(os.path.join(data_dir, "se", "*.json"))):
        try:
            D = load_json(path)
        except Exception as e:  # JSON の書きまちがい
            errors.append("%s: JSON が読めない: %s" % (rel(path), e))
            parse_failed = True
            continue
        sounds = D.get("sounds") if isinstance(D, dict) else None
        if not isinstance(sounds, list):
            errors.append("%s: {\"sounds\": [...]} の形にする" % rel(path))
            parse_failed = True
            continue
        for i, S in enumerate(sounds):
            sid = S.get("id") if isinstance(S, dict) else None
            label = sid if isinstance(sid, str) else "sounds[%d]" % i
            if isinstance(sid, str):
                if sid in seen:
                    errors.append("%s: %s: id が %s と同じ（2つ目は作らない）" % (rel(path), sid, rel(seen[sid])))
                    continue
                seen[sid] = path
            se_list.append((path, label, S))
    for path in sorted(glob.glob(os.path.join(data_dir, "bgm", "*.json"))):
        try:
            D = load_json(path)
        except Exception as e:
            errors.append("%s: JSON が読めない: %s" % (rel(path), e))
            parse_failed = True
            continue
        items = D.get("songs") if isinstance(D, dict) and isinstance(D.get("songs"), list) else [D]
        for i, S in enumerate(items):
            sid = S.get("id") if isinstance(S, dict) else None
            label = sid if isinstance(sid, str) else os.path.basename(path)
            if isinstance(sid, str):
                if sid in seen:
                    errors.append("%s: %s: id が %s と同じ（2つ目は作らない）" % (rel(path), sid, rel(seen[sid])))
                    continue
                seen[sid] = path
                base = os.path.splitext(os.path.basename(path))[0]
                if base != sid and len(items) == 1:
                    warnings.append("%s: %s: ファイル名と id がちがう" % (rel(path), sid))
            songs.append((path, label, S))
    return se_list, songs, parse_failed


def expected_files(se_list, songs):
    """今のレシピで出来るはずのファイル名（拡張子なし）。"""
    se_names, bgm_names = set(), set()
    for _, label, _S in se_list:
        se_names.add(label)
    for _, label, S in songs:
        bgm_names.add(label)
        if isinstance(S, dict) and S.get("intro") and not S.get("jingle"):
            bgm_names.add(label + "_intro")
    return se_names, bgm_names


def _job_se(args):
    path, label, S, out_dir, check_only = args
    try:
        x, warns = synth.render_sound(S)
        if not check_only:
            synth.write_wav(os.path.join(out_dir, label + ".wav"), x, synth.SE_RATE)
        return (label, path, None, warns, len(x) / synth.SE_RATE)
    except SynthError as e:
        return (label, path, str(e), [], 0.0)
    except Exception as e:  # プログラムのまちがい（データのせいではないかも）
        return (label, path, "作れなかった（%s: %s）" % (type(e).__name__, e), [], 0.0)


def _job_song(args):
    path, label, S, out_dir, check_only = args
    try:
        outs, info, warns = synth.render_song(S)
        if not check_only:
            for name, x in outs.items():
                synth.write_wav(os.path.join(out_dir, name + ".wav"), x, synth.BGM_RATE)
        durs = {name: len(x) / synth.BGM_RATE for name, x in outs.items()}
        return (label, path, None, warns, durs)
    except SynthError as e:
        return (label, path, str(e), [], {})
    except Exception as e:
        return (label, path, "作れなかった（%s: %s）" % (type(e).__name__, e), [], {})


def prune(folder, keep, report):
    if not os.path.isdir(folder):
        return
    for path in sorted(glob.glob(os.path.join(folder, "*.wav"))):
        name = os.path.splitext(os.path.basename(path))[0]
        if not name.startswith(PRUNE_PREFIXES) or name in keep:
            continue
        os.remove(path)
        meta = path + ".meta"
        if os.path.exists(meta):
            os.remove(meta)  # Unity の .meta も一緒に（残すと Unity が警告を出す）
        report.append(rel(path))


def main():
    ap = argparse.ArgumentParser(description="ゲームの音（SE・BGM）を JSON から作る")
    ap.add_argument("--out", help="書き出す先（DIR/SE・DIR/BGM）。省略で Assets/Resources/Audio")
    ap.add_argument("--data", default=HERE, help="データの場所（DIR/se・DIR/bgm）。省略で Tools/audio")
    ap.add_argument("--only", help="この id だけ作る（カンマ区切り）")
    ap.add_argument("--se-only", action="store_true", help="SE だけ")
    ap.add_argument("--bgm-only", action="store_true", help="BGM・ジングルだけ")
    ap.add_argument("--prune", action="store_true", help="レシピが無い se_*/bgm_*/jingle_* の WAV を消す")
    ap.add_argument("--check", action="store_true", help="作らずに調べるだけ（WAV は書かない）")
    ap.add_argument("--jobs", type=int, default=1, help="同時に作る数（省略で 1。全部でも数秒なので、ふつうは 1 で十分）")
    ap.add_argument("-q", "--quiet", action="store_true", help="うまくいった音の行を出さない")
    a = ap.parse_args()
    if a.se_only and a.bgm_only:
        ap.error("--se-only と --bgm-only は一緒に使えない")

    t0 = time.time()
    out_root = os.path.abspath(a.out) if a.out else DEFAULT_OUT
    se_dir, bgm_dir = os.path.join(out_root, "SE"), os.path.join(out_root, "BGM")
    errors, warnings = [], []
    DATA_DIR[0] = os.path.abspath(a.data)
    se_list, songs, parse_failed = collect(DATA_DIR[0], errors, warnings)

    only = None
    if a.only:
        only = set(s.strip() for s in a.only.split(",") if s.strip())
        known = set(l for _, l, _ in se_list) | set(l for _, l, _ in songs)
        for s in sorted(only - known):
            errors.append("--only: %s というレシピは無い" % s)
    do_se = not a.bgm_only
    do_bgm = not a.se_only
    se_jobs = [(p, l, S, se_dir, a.check) for p, l, S in se_list if do_se and (only is None or l in only)]
    song_jobs = [(p, l, S, bgm_dir, a.check) for p, l, S in songs if do_bgm and (only is None or l in only)]
    if not a.check:
        if se_jobs:
            os.makedirs(se_dir, exist_ok=True)
        if song_jobs:
            os.makedirs(bgm_dir, exist_ok=True)

    n_ok = 0
    jobs = a.jobs if a.jobs > 0 else 1
    results = []
    if jobs > 1 and len(se_jobs) + len(song_jobs) > 4:
        with ProcessPoolExecutor(max_workers=jobs) as ex:
            fs = [ex.submit(_job_song, j) for j in song_jobs]  # 曲は重いので先に
            fs += [ex.submit(_job_se, j) for j in se_jobs]
            results = [f.result() for f in fs]
    else:
        results = [_job_song(j) for j in song_jobs] + [_job_se(j) for j in se_jobs]

    for label, path, err, warns, dur in results:
        for w in warns:
            warnings.append("%s: %s: %s" % (rel(path), label, w))
        if err:
            for line in err.split("\n"):
                errors.append("%s: %s: %s" % (rel(path), label, line))
            continue
        n_ok += 1
        if not a.quiet:
            if isinstance(dur, dict):
                print("  ok  %-28s %s" % (label, "  ".join("%s %.2fs" % (k, v) for k, v in dur.items())))
            else:
                print("  ok  %-28s %.3fs" % (label, dur))

    pruned = []
    if a.prune:
        if parse_failed:
            errors.append("--prune: 読めないデータのファイルがあるので、消すのをやめた")
        elif a.check:
            pass
        else:
            # 残すものは --only に関係なく、全部のレシピ（作るのに失敗したものも）から決める
            se_keep, bgm_keep = expected_files(se_list, songs)
            for on, folder, keep, what in ((do_se, se_dir, se_keep, "SE"), (do_bgm, bgm_dir, bgm_keep, "BGM")):
                if not on:
                    continue
                if not keep:
                    # レシピが1つも無い = データの場所のまちがいかもしれないので、全部消したりしない
                    warnings.append("--prune: %s のレシピが1つも無いので、%s は消さなかった" % (what, rel(folder)))
                    continue
                prune(folder, keep, pruned)
    for p in pruned:
        print("  消した %s" % p)

    if warnings:
        print("\n注意（%d）" % len(warnings))
        for w in warnings:
            print("  " + w)
    if errors:
        print("\nまちがい（%d）" % len(errors))
        for e in errors:
            print("  " + e)
    n_fail = sum(1 for r in results if r[2])
    where = "" if a.check else " → %s" % rel(out_root)
    print("\n%s %d 個・失敗 %d 個（%.1f 秒）%s" % ("調べた" if a.check else "作った", n_ok, n_fail, time.time() - t0, where))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
