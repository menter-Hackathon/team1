#!/usr/bin/env python3
"""
音の割り当てと音のファイルが合っているか調べるスクリプト（2026-11-19）。

調べること
  (a) Assets/Resources/Audio/Maps/*.json に書いた SE の id（moves の start/release・events の se・fallbacks の start/release）が、
      レシピ（Tools/audio/se/*.json）と WAV（Assets/Resources/Audio/SE/<id>.wav）の両方にあるか            → 無ければ まちがい
  (b) Tools/audio/move_manifest.tsv の技（4列目。"(none)" は飛ばす）が全部 moves に書いてあるか（2列目ごとに表示） → 無ければ まちがい
  (c) FORMAT.md 3章の「出来事のキー」の表のうち、events に無いもの                                                → 注意
  (d) 割り当てのファイルどうしで、同じキーが2回出てくるもの                                                       → 注意
  (e) どこからも使われていないレシピ                                                                             → 注意
  (f) WAV ごとの長さ・一番大きい所・RMS（dBFS）。BGM のループは、つなぎ目の段差と、ちょうどの小節数か。Audio フォルダの合計の大きさ
終了コードは (a)(b) のまちがいがあるときだけ 1。

使い方（リポジトリの一番上で）
  python3 Tools/audio/check_audio.py            全部
  python3 Tools/audio/check_audio.py --brief    WAV ごとの行を出さない（目立つものだけ）
  --data DIR（se/・bgm/）・--maps DIR・--audio DIR（SE/・BGM/）・--manifest FILE で場所を変えられる（試し用）
"""
import sys

sys.dont_write_bytecode = True

import argparse  # noqa: E402
import glob  # noqa: E402
import json  # noqa: E402
import os  # noqa: E402
import re  # noqa: E402
from collections import OrderedDict, defaultdict  # noqa: E402

import numpy as np  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

from synth.core import read_wav  # noqa: E402

TEAMS = ("Player", "Enemy")
KINDS = ("Projectile", "Area", "Spawn", "Warp", "LockOn", "Custom")


def rel(p):
    r = os.path.relpath(p, ROOT)
    return p if r.startswith("..") else r


def load_json(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def table_keys(md_text, heading_word):
    """FORMAT.md の、heading_word を含む行のすぐ後の表から、1列目の `キー` を全部取り出す。"""
    lines = md_text.splitlines()
    keys = []
    start = None
    for i, ln in enumerate(lines):
        if heading_word in ln and not ln.startswith("|"):
            start = i + 1
            break
    if start is None:
        return keys
    in_table = False
    for ln in lines[start:]:
        s = ln.strip()
        if s.startswith("|"):
            in_table = True
            first = s.strip("|").split("|")[0]
            if set(first.strip()) <= set("-: "):
                continue
            keys += re.findall(r"`([a-z0-9_]+)`", first)
        elif in_table:
            break
    return keys


def load_recipes(data_dir):
    se_ids = OrderedDict()
    songs = {}
    for path in sorted(glob.glob(os.path.join(data_dir, "se", "*.json"))):
        try:
            D = load_json(path)
            for S in D.get("sounds", []):
                if isinstance(S, dict) and isinstance(S.get("id"), str):
                    se_ids.setdefault(S["id"], path)
        except Exception as e:
            print("  注意: %s が読めない（%s）" % (rel(path), e))
    for path in sorted(glob.glob(os.path.join(data_dir, "bgm", "*.json"))):
        try:
            D = load_json(path)
            if isinstance(D, dict) and isinstance(D.get("id"), str):
                songs[D["id"]] = D
        except Exception as e:
            print("  注意: %s が読めない（%s）" % (rel(path), e))
    return se_ids, songs


def load_manifest(path):
    rows = []
    if not os.path.exists(path):
        return rows
    with open(path, encoding="utf-8") as f:
        for ln in f:
            cols = ln.rstrip("\n").split("\t")
            if len(cols) < 4:
                continue
            move = cols[3].strip()
            if not move or move == "(none)":
                continue
            rows.append((cols[0].strip(), cols[1].strip(), cols[2].strip(), move))
    return rows


def song_bars(D, which):
    """楽譜からループ（またはイントロ）の小節数と、1小節のサンプル数（32000 Hz）を出す。"""
    try:
        spb = int(D.get("steps_per_beat", 4))
        bpb = int(D.get("beats_per_bar", 4))
        bpm = float(D["bpm"])
        secs = D.get("sections", {})
        names = D.get(which) or []
        bars = 0
        for n in names:
            b = secs[n].get("bars")
            if b is None:
                return None, None
            bars += int(b)
        return bars, bpb * 60.0 / bpm * 32000.0
    except Exception:
        return None, None


def main():
    ap = argparse.ArgumentParser(description="音の割り当てと WAV を調べる")
    ap.add_argument("--data", default=HERE)
    ap.add_argument("--maps", default=os.path.join(ROOT, "Assets", "Resources", "Audio", "Maps"))
    ap.add_argument("--audio", default=os.path.join(ROOT, "Assets", "Resources", "Audio"))
    ap.add_argument("--manifest", default=os.path.join(HERE, "move_manifest.tsv"))
    ap.add_argument("--format", default=os.path.join(HERE, "FORMAT.md"))
    ap.add_argument("--brief", action="store_true", help="WAV ごとの行を出さない")
    a = ap.parse_args()

    errors = 0
    se_ids, songs = load_recipes(a.data)
    se_dir = os.path.join(a.audio, "SE")
    bgm_dir = os.path.join(a.audio, "BGM")
    wavs_se = {os.path.splitext(os.path.basename(p))[0] for p in glob.glob(os.path.join(se_dir, "*.wav"))}

    # ---------------------------------------------------------------- 割り当てを読む
    refs = []  # (se_id, file, where)
    moves = defaultdict(list)  # move -> [file]
    events = defaultdict(list)
    fallbacks = defaultdict(list)
    map_files = sorted(glob.glob(os.path.join(a.maps, "*.json")))
    print("割り当て: %s（%d ファイル）" % (rel(a.maps), len(map_files)))
    if not os.path.isdir(a.maps):
        print("  注意: フォルダが無い")
    warn_lines = []

    def check_range(fn, where, row, key, lo, hi):
        v = row.get(key)
        if v is None:
            return
        if isinstance(v, bool) or not isinstance(v, (int, float)) or not (lo <= v <= hi):
            warn_lines.append("%s: %s.%s = %r（%s〜%s）" % (fn, where, key, v, lo, hi))

    for path in map_files:
        fn = os.path.basename(path)
        try:
            M = load_json(path)
        except Exception as e:
            print("  まちがい: %s が JSON として読めない（%s）" % (fn, e))
            errors += 1
            continue
        if not isinstance(M, dict):
            print("  まちがい: %s は {moves, events, fallbacks} の形にする" % fn)
            errors += 1
            continue
        for k in M:
            if k not in ("moves", "events", "fallbacks"):
                warn_lines.append("%s: 知らないキー %s（Unity は読まない）" % (fn, k))
        for i, row in enumerate(M.get("moves") or []):
            where = "moves[%d]" % i
            if not isinstance(row, dict) or not isinstance(row.get("move"), str) or not row.get("move"):
                print("  まちがい: %s: %s に move（技の名前）が無い" % (fn, where))
                errors += 1
                continue
            moves[row["move"]].append(fn)
            for key in ("start", "release"):
                v = row.get(key, "")
                if v:
                    refs.append((v, fn, "%s(%s).%s" % (where, row["move"], key)))
            check_range(fn, where, row, "vol", 0, 1.5)
            check_range(fn, where, row, "pitch", 0.5, 2)
        for i, row in enumerate(M.get("events") or []):
            where = "events[%d]" % i
            if not isinstance(row, dict) or not isinstance(row.get("key"), str) or not row.get("key"):
                print("  まちがい: %s: %s に key が無い" % (fn, where))
                errors += 1
                continue
            events[row["key"]].append(fn)
            v = row.get("se", "")
            if v:
                refs.append((v, fn, "%s(%s).se" % (where, row["key"])))
            check_range(fn, where, row, "vol", 0, 1.5)
            check_range(fn, where, row, "pitch", 0.5, 2)
        for i, row in enumerate(M.get("fallbacks") or []):
            where = "fallbacks[%d]" % i
            if not isinstance(row, dict):
                continue
            team, kind = row.get("team"), row.get("kind")
            if team not in TEAMS or kind not in KINDS:
                warn_lines.append("%s: %s の team/kind が %r/%r（%s × %s）" % (fn, where, team, kind, "/".join(TEAMS), "/".join(KINDS)))
            fallbacks["%s×%s" % (team, kind)].append(fn)
            for key in ("start", "release"):
                v = row.get(key, "")
                if v:
                    refs.append((v, fn, "%s(%s×%s).%s" % (where, team, kind, key)))
            check_range(fn, where, row, "vol", 0, 1.5)

    # ---------------------------------------------------------------- (a)
    print("\n(a) 割り当ての SE がレシピと WAV にあるか（%d 個の参照）" % len(refs))
    na = 0
    for sid, fn, where in refs:
        miss = []
        if not isinstance(sid, str):
            miss.append("文字列ではない")
        else:
            if sid not in se_ids:
                miss.append("レシピが無い")
            if sid not in wavs_se:
                miss.append("WAV が無い")
        if miss:
            print("  まちがい: %s: %s = %r → %s" % (fn, where, sid, "・".join(miss)))
            na += 1
    errors += na
    if na == 0:
        print("  OK")

    # ---------------------------------------------------------------- (b)
    man = load_manifest(a.manifest)
    print("\n(b) move_manifest.tsv の技が moves にあるか（%d 行）" % len(man))
    if not man:
        print("  注意: %s が無いか空" % rel(a.manifest))
    missing = OrderedDict()
    man_moves = set()
    for kind, owner, phase, move in man:
        man_moves.add(move)
        if move not in moves:
            missing.setdefault(owner, [])
            if move not in missing[owner]:
                missing[owner].append(move)
    nb = sum(len(v) for v in missing.values())
    for owner, ms in missing.items():
        print("  まちがい: %s: %s" % (owner, ", ".join(ms)))
    errors += nb
    if nb == 0:
        print("  OK")
    else:
        print("  → 無い技 %d 個（%d のボス・能力）" % (nb, len(missing)))
    extra = sorted(m for m in moves if man_moves and m not in man_moves)
    if extra:
        print("  参考: 表に無い技の行 %d 個（雑魚の技か、名前のまちがい）: %s" % (len(extra), ", ".join(extra)))

    # ---------------------------------------------------------------- (c)
    fmt_text = open(a.format, encoding="utf-8").read() if os.path.exists(a.format) else ""
    ev_keys = table_keys(fmt_text, "出来事のキー")
    print("\n(c) 出来事のキー（FORMAT.md の表 %d 個）が events にあるか" % len(ev_keys))
    nc = [k for k in ev_keys if k not in events]
    if nc:
        print("  注意: events に無い: %s" % ", ".join(nc))
    else:
        print("  OK")
    unknown = [k for k in events if ev_keys and k not in ev_keys and not k.startswith("gm_")]
    if unknown:
        print("  注意: 表に無いキー（コードから GameAudio.Event で呼ばないと鳴らない）: %s" % ", ".join(unknown))

    # ---------------------------------------------------------------- (c2) ギミックの音（FORMAT.md 4章）
    # gm_ のキーは、コードの GameAudio.Event("gm_…") で鳴らす。割り当てとコードの両方にあるかを見る
    gm_map = sorted(k for k in events if k.startswith("gm_"))
    code_keys = {}
    scripts = os.path.join(ROOT, "Assets", "Scripts")
    for dp, _dn, fn in os.walk(scripts):
        for f in fn:
            if not f.endswith(".cs"):
                continue
            p = os.path.join(dp, f)
            with open(p, encoding="utf-8", errors="ignore") as fh:
                text = fh.read()
                if "GameAudio.Event" not in text:
                    continue
                # 文字列の "gm_…" を全部数える（呼び出しの中でキーを選ぶ書き方もあるので）
                for m in re.finditer(r'"(gm_[a-z0-9_]+)"', text):
                    code_keys.setdefault(m.group(1), os.path.relpath(p, scripts))
    print("\n(c2) ギミックの音（gm_）：割り当て %d 個・コードの呼び出し %d 個" % (len(gm_map), len(code_keys)))
    no_code = [k for k in gm_map if k not in code_keys]
    no_map = sorted(k for k in code_keys if k not in events)
    if no_code:
        print("  注意: コードから呼んでいない（鳴らない）: %s" % ", ".join(no_code))
    if no_map:
        print("  注意: コードで呼んでいるのに割り当てが無い: %s" % ", ".join("%s（%s）" % (k, code_keys[k]) for k in no_map))
    if not no_code and not no_map:
        print("  OK")

    # ---------------------------------------------------------------- (d)
    print("\n(d) 同じキーが2回")
    nd = 0
    for label, table in (("moves", moves), ("events", events), ("fallbacks", fallbacks)):
        for k, fs in table.items():
            if len(fs) > 1:
                print("  注意: %s %s が %s（後に読む方が使われる）" % (label, k, " と ".join(fs)))
                nd += 1
    if nd == 0:
        print("  OK")
    for w in warn_lines:
        print("  注意: " + w)

    # ---------------------------------------------------------------- (e)
    used = {r[0] for r in refs if isinstance(r[0], str)}
    print("\n(e) 使われていないレシピ（%d 個中）" % len(se_ids))
    ne = [s for s in se_ids if s not in used]
    if ne:
        by_file = OrderedDict()
        for s in ne:
            by_file.setdefault(os.path.basename(se_ids[s]), []).append(s)
        for f, ss in by_file.items():
            print("  注意: %s: %s" % (f, ", ".join(ss)))
    else:
        print("  OK")
    orphan_wavs = sorted(w for w in wavs_se if w not in se_ids)
    if orphan_wavs:
        print("  注意: レシピの無い SE の WAV（build_audio.py --prune で消せる）: %s" % ", ".join(orphan_wavs))

    # ---------------------------------------------------------------- (f)
    print("\n(f) WAV")
    se_stats = []
    for p in sorted(glob.glob(os.path.join(se_dir, "*.wav"))):
        try:
            sr, x = read_wav(p)
        except Exception as e:
            print("  注意: %s が読めない（%s）" % (rel(p), e))
            continue
        pk = float(np.max(np.abs(x))) if len(x) else 0.0
        rms = 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12) if len(x) else -120
        se_stats.append((os.path.basename(p)[:-4], sr, len(x) / sr, pk, rms))
    if se_stats:
        durs = [s[2] for s in se_stats]
        print("  SE %d 個：長さ %.3f〜%.3f 秒（中央 %.3f）・RMS %.1f〜%.1f dBFS" % (
            len(se_stats), min(durs), max(durs), float(np.median(durs)), min(s[4] for s in se_stats), max(s[4] for s in se_stats)))
        for name, sr, dur, pk, rms in se_stats:
            odd = []
            if sr != 44100:
                odd.append("%d Hz" % sr)
            if dur > 1.6:
                odd.append("長い")
            if pk >= 0.999:
                odd.append("割れているかも")
            if not a.brief or odd:
                print("    %-34s %6.3fs  peak %.3f  RMS %6.1f dB%s" % (name, dur, pk, rms, ("  ← " + "・".join(odd)) if odd else ""))
    bgm_files = sorted(glob.glob(os.path.join(bgm_dir, "*.wav")))
    if bgm_files:
        print("  BGM %d 個" % len(bgm_files))
    for p in bgm_files:
        name = os.path.basename(p)[:-4]
        try:
            sr, x = read_wav(p)
        except Exception as e:
            print("  注意: %s が読めない（%s）" % (rel(p), e))
            continue
        pk = float(np.max(np.abs(x))) if len(x) else 0.0
        rms = 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12) if len(x) else -120
        line = "    %-28s %6.2fs  peak %.3f  RMS %6.1f dB" % (name, len(x) / sr, pk, rms)
        odd = []
        if sr != 32000:
            odd.append("%d Hz" % sr)
        is_intro = name.endswith("_intro")
        sid = name[:-6] if is_intro else name
        D = songs.get(sid)
        if D is None:
            odd.append("楽譜が無い")
        elif D.get("jingle"):
            line += "  ジングル"
        else:
            bars, spbar = song_bars(D, "intro" if is_intro else "loop")
            if bars:
                exact = bars * spbar
                diff = len(x) - exact
                ok = abs(diff) <= 1.0
                line += "  %d小節%s（%+.1f サンプル）" % (bars, "ちょうど" if ok else "ではない", diff)
                if not ok:
                    odd.append("小節数とずれている")
            if not is_intro and len(x) > 3:
                jump = abs(x[-1] - x[0])
                slope = abs((x[-1] - x[-2]) - (x[1] - x[0]))
                typ = float(np.percentile(np.abs(np.diff(x)), 99.9))
                line += "  つなぎ目 段差 %.4f・傾きの差 %.4f（ふだんの大きい段差 %.4f）" % (jump, slope, typ)
                if jump > max(typ * 1.5, 0.05):
                    odd.append("つなぎ目が目立つかも")
        if not a.brief or odd:
            print(line + (("  ← " + "・".join(odd)) if odd else ""))
    bgm_ids = table_keys(fmt_text, "BGM の id")
    if bgm_ids:
        have = {os.path.basename(p)[:-4] for p in bgm_files}
        miss = [b for b in bgm_ids if b not in have]
        if miss:
            print("  注意: FORMAT.md の表にある曲で WAV が無い: %s" % ", ".join(miss))
    total = 0
    n_files = 0
    for dp, _dn, fns in os.walk(a.audio):
        for f in fns:
            total += os.path.getsize(os.path.join(dp, f))
            n_files += 1
    print("  %s の合計：%.2f MB（%d ファイル）" % (rel(a.audio), total / 1048576.0, n_files))

    print("\n%s" % ("まちがい %d 個（(a)(b)）" % errors if errors else "まちがい無し"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
