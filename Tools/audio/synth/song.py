"""
BGM・ジングルの楽譜（FORMAT.md 2章）から曲を作る（2026-11-19）。

- ループの曲：イントロ → ループ → ループ と続けて作り、2回目のループを `<id>.wav` に書き出す
  （最後の音の余韻・エコーが頭に重なるので、最後から最初へ切れ目なくつながる）。イントロは `<id>_intro.wav`。
- ジングル：ループを1回だけ作り、1.5 秒の余韻をつけてフェードアウトする。
- ループの各回の頭は整数のサンプルにそろえ、ドラムのノイズは「その回の中のマスの位置」で決めるので、
  1回目と2回目のループはサンプル単位で同じになる。
"""
from __future__ import annotations

import math
import re

import numpy as np

from . import core, drums
from .core import SynthError, num, num_list

SONG_KEYS = {"id", "desc", "bpm", "steps_per_beat", "beats_per_bar", "swing", "instruments", "patterns",
             "sections", "intro", "loop", "jingle", "master"}
INST_KEYS = {"wave", "duty", "duty_end", "env", "vol", "vib", "octave", "detune", "arp_rate", "echo", "lp", "hp"}
KIT_KEYS = {"kit", "vol", "echo", "lp", "hp"}
MASTER_KEYS = {"lp", "hp", "gain"}
SONG_ID_RE = re.compile(r"^(bgm|jingle)_[a-z0-9_]+$")

SONG_PEAK = 0.9
JINGLE_TAIL = 1.5
DEFAULT_VOL = 0.5
DETUNE_GAIN = 0.7


# ---------------------------------------------------------------- パターンを読む

class Tok:
    """マス1つ。kind = 'hold'（-）・'off'（.）・'note'・'drum'。items = [(音の高さ midi か ドラムの文字, 強さ)]"""
    __slots__ = ("kind", "items")

    def __init__(self, kind, items=None):
        self.kind, self.items = kind, items or []


HOLD = Tok("hold")
OFF = Tok("off")
_NUM_RE = re.compile(r"^\d+(\.\d+)?$")


def _parse_element(el: str):
    vel = 1.0
    while el and el[-1] in "!'":
        vel *= 1.3 if el[-1] == "!" else 0.6
        el = el[:-1]
    if not el:
        raise SynthError("空の音")
    if len(el) == 1 and el in drums.DRUM_CHARS:
        return "drum", el, vel
    if _NUM_RE.match(el):
        hz = float(el)
        if hz <= 0:
            raise SynthError("周波数は 0 より大きく")
        return "note", 69.0 + 12.0 * math.log2(hz / 440.0), vel
    return "note", core.note_to_midi(el), vel


def parse_token(tok: str) -> Tok:
    base = tok.rstrip("!'")
    if base == "-":
        return HOLD
    if base == ".":
        return OFF
    kinds, items = set(), []
    for el in tok.split("+"):
        try:
            k, v, vel = _parse_element(el)
        except SynthError as e:
            raise SynthError("%r が読めない（%s）" % (tok, e))
        kinds.add(k)
        items.append((v, vel))
    if len(kinds) > 1:
        raise SynthError("%r: 音とドラムを + で混ぜられない" % tok)
    return Tok(kinds.pop(), items)


def parse_pattern(text, steps_per_bar: int):
    """パターンの文字列 → マスの列。`|` があれば、1小節のマスの数を調べる。
    戻り値 (toks, errors)。errors は 'N小節目: ...' の文字列。"""
    errs = []
    if not isinstance(text, str):
        return [], ["文字列にする"]
    raw = text.replace("|", " | ").split()
    has_bar = "|" in raw
    bars, cur = [], []
    for w in raw:
        if w == "|":
            bars.append(cur)
            cur = []
        else:
            cur.append(w)
    bars.append(cur)
    # 頭と最後の | の外側の空っぽは数えない
    while bars and not bars[0]:
        bars.pop(0)
    while bars and not bars[-1]:
        bars.pop()
    toks = []
    for bi, bar in enumerate(bars):
        if has_bar and len(bar) != steps_per_bar:
            errs.append("%d小節目のマスが %d 個（%d 個のはず）: %s" % (bi + 1, len(bar), steps_per_bar, " ".join(bar)))
        for w in bar:
            try:
                toks.append(parse_token(w))
            except SynthError as e:
                errs.append("%d小節目: %s" % (bi + 1, e))
                toks.append(OFF)
    return toks, errs


# ---------------------------------------------------------------- 楽譜を調べる

def _parse_instrument(name, I, warns):
    pre = "instruments.%s" % name
    if not isinstance(I, dict):
        raise SynthError("%s: {} の形にする" % pre)
    try:
        P = {"name": name}
        if "kit" in I:
            if I["kit"] != "chip":
                raise SynthError("kit: \"chip\" だけ（%r）" % I["kit"])
            P["drum"] = True
            for k in I:
                if k not in KIT_KEYS:
                    warns.append("%s.%s: ドラムでは使わないキー（無視する）" % (pre, k))
        else:
            P["drum"] = False
            for k in I:
                if k not in INST_KEYS:
                    warns.append("%s.%s: 知らないキー（無視する）" % (pre, k))
            wave = I.get("wave")
            if wave not in core.WAVES:
                raise SynthError("wave: %r は無い（%s。ドラムなら kit: chip）" % (wave, "・".join(core.WAVES)))
            P["wave"] = wave
            P["duty"] = num(I, "duty", 0.5, lo=0.01, hi=0.99)
            P["duty_end"] = num(I, "duty_end", P["duty"], lo=0.01, hi=0.99)
            if wave != "pulse":
                P["duty"] = P["duty_end"] = 0.5
            P["env"] = core.parse_env(I.get("env"))
            P["vib"] = num_list(I, "vib", 2, 3)
            P["octave"] = num(I, "octave", 0, lo=-4, hi=4, integer=True)
            P["detune"] = num(I, "detune", 0.0, lo=-100, hi=100)
            P["arp_rate"] = num(I, "arp_rate", 0.0, lo=0)
        P["vol"] = num(I, "vol", DEFAULT_VOL, lo=0)
        P["lp"] = num(I, "lp", 0.0, lo=0)
        P["hp"] = num(I, "hp", 0.0, lo=0)
        P["echo"] = num_list(I, "echo", 3, 3)
        if P["echo"]:
            d, fb, mix = P["echo"]
            if d <= 0 or not (0 <= fb <= 0.95) or not (0 <= mix <= 1.5):
                raise SynthError("echo: [遅れのマス数 >0, 戻り 0〜0.8, 混ぜる 0〜1]（%r）" % (P["echo"],))
        return P
    except SynthError as e:
        raise SynthError("%s.%s" % (pre, e))


def parse_song(D: dict):
    """楽譜を調べる。戻り値 (song, warnings)。まちがいは全部まとめて SynthError（1行に1つ）。"""
    warns, errs = [], []
    if not isinstance(D, dict):
        raise SynthError("曲は {} の形にする")
    sid = D.get("id")
    if not isinstance(sid, str) or not SONG_ID_RE.match(sid):
        raise SynthError("id: bgm_ か jingle_ で始まる英小文字・数字・_ だけにする（%r）" % (sid,))
    for k in D:
        if k not in SONG_KEYS:
            warns.append("%s: 知らないキー（無視する）" % k)
    S = {"id": sid}
    S["bpm"] = num(D, "bpm", required=True, lo=20, hi=400)
    S["spb"] = num(D, "steps_per_beat", 4, lo=1, hi=16, integer=True)
    S["bpb"] = num(D, "beats_per_bar", 4, lo=1, hi=16, integer=True)
    S["swing"] = num(D, "swing", 0.0, lo=0, hi=0.5)
    S["jingle"] = bool(D.get("jingle", False))
    if S["jingle"] != sid.startswith("jingle_"):
        warns.append("jingle: id が %s なのに jingle が %s" % (sid.split("_")[0] + "_", S["jingle"]))
    S["steps_per_bar"] = S["spb"] * S["bpb"]
    S["step_dur"] = 60.0 / S["bpm"] / S["spb"]
    master = D.get("master") or {}
    if not isinstance(master, dict):
        raise SynthError("master: {} の形にする")
    for k in master:
        if k not in MASTER_KEYS:
            warns.append("master.%s: 知らないキー（無視する）" % k)
    try:
        S["master"] = {"lp": num(master, "lp", 0.0, lo=0), "hp": num(master, "hp", 0.0, lo=0),
                       "gain": num(master, "gain", 1.0, lo=0, hi=1.0)}
    except SynthError as e:
        raise SynthError("master.%s" % e)

    insts = D.get("instruments")
    if not isinstance(insts, dict) or not insts:
        raise SynthError("instruments: 1つ以上の楽器を書く")
    S["instruments"] = {}
    for name, I in insts.items():
        try:
            S["instruments"][name] = _parse_instrument(name, I, warns)
        except SynthError as e:
            errs.append(str(e))

    pats = D.get("patterns")
    if not isinstance(pats, dict):
        raise SynthError("patterns: {名前: 文字列} の形にする")
    parsed = {}
    for pname, text in pats.items():
        toks, perrs = parse_pattern(text, S["steps_per_bar"])
        for e in perrs:
            errs.append("patterns.%s: %s" % (pname, e))
        parsed[pname] = toks

    secs = D.get("sections")
    if not isinstance(secs, dict) or not secs:
        raise SynthError("sections: 1つ以上の区間を書く")
    S["sections"] = {}
    for sname, sec in secs.items():
        pre = "sections.%s" % sname
        if not isinstance(sec, dict):
            errs.append("%s: {} の形にする" % pre)
            continue
        tracks = {}
        for k, v in sec.items():
            if k == "bars":
                continue
            if k not in S["instruments"]:
                if k not in insts:
                    errs.append("%s.%s: そんな楽器は instruments に無い" % (pre, k))
                continue
            if v is None or v == "":
                continue
            if not isinstance(v, str) or v not in parsed:
                errs.append("%s.%s: パターン %r が patterns に無い" % (pre, k, v))
                continue
            inst = S["instruments"][k]
            toks = parsed[v]
            bad = [t for t in toks if (t.kind == "drum" and not inst["drum"]) or (t.kind == "note" and inst["drum"])]
            if bad:
                what = "音" if inst["drum"] else "ドラムの記号"
                errs.append("%s.%s: 楽器 %s に%sが書いてある（パターン %s）" % (pre, k, k, what, v))
                continue
            tracks[k] = (v, toks)
        bars = sec.get("bars")
        if bars is None:
            longest = max([len(t) for _, t in tracks.values()] or [S["steps_per_bar"]])
            bars = max(1, math.ceil(longest / S["steps_per_bar"]))
            warns.append("%s.bars: 無いのでパターンの長さから %d 小節にした" % (pre, bars))
        if isinstance(bars, bool) or not isinstance(bars, (int, float)) or bars != int(bars) or bars < 1:
            errs.append("%s.bars: 1 以上の整数にする（%r）" % (pre, bars))
            continue
        S["sections"][sname] = {"bars": int(bars), "tracks": tracks}

    for key in ("intro", "loop"):
        v = D.get(key)
        if v is None:
            S[key] = []
            continue
        if not isinstance(v, list) or any(not isinstance(e, str) for e in v):
            errs.append("%s: 区間の名前の配列にする（%r）" % (key, v))
            S[key] = []
            continue
        for e in v:
            if e not in secs:
                errs.append("%s: 区間 %r が sections に無い" % (key, e))
        S[key] = [e for e in v if e in S["sections"]]
    if not D.get("loop"):
        errs.append("loop: 区間の並びが必要（ジングルも loop に書く）")
    if errs:
        raise SynthError("\n".join(errs))
    return S, warns


# ---------------------------------------------------------------- 並べる

def _block_steps(S, names):
    return sum(S["sections"][n]["bars"] for n in names) * S["steps_per_bar"]


def build_timeline(S, blocks, sr):
    """blocks = [(種類 'intro'/'loop', [区間名...]), ...]。
    戻り値：楽器ごとのマスの列、マスの頭のサンプル位置（最後に終わりの位置を1つ足したもの）、
    マスの「回の中の位置」、回の種類、各回の頭のサンプル。"""
    step_dur = S["step_dur"]
    swing = S["swing"]
    pos, local, kinds, starts = [], [], [], []
    tracks = {name: [] for name in S["instruments"]}
    cur = 0
    for kind, names in blocks:
        n = _block_steps(S, names)
        starts.append(cur)
        s = np.arange(n)
        t = s * step_dur + np.where(s % 2 == 1, swing * step_dur, 0.0)
        pos.append(cur + np.round(t * sr).astype(np.int64))
        local.append(s)
        kinds += [kind] * n
        for name in S["instruments"]:
            arr = tracks[name]
            for sec_name in names:
                sec = S["sections"][sec_name]
                ln = sec["bars"] * S["steps_per_bar"]
                if name in sec["tracks"]:
                    toks = sec["tracks"][name][1]
                    if toks:
                        reps = -(-ln // len(toks))
                        arr.extend((toks * reps)[:ln])  # 短ければくり返す・長ければ切る
                        continue
                arr.extend([OFF] * ln)  # 区間に書かない楽器は鳴らない
        cur += int(round(n * step_dur * sr))
    starts.append(cur)
    pos.append(np.array([cur], dtype=np.int64))
    return tracks, np.concatenate(pos), np.concatenate(local + [np.array([0])]), kinds, starts


def _render_melodic(S, P, toks, pos, local, kinds, sr, buf):
    """音の楽器を1本作って buf に足す。"""
    sid = S["id"]
    events = []
    cur = None
    for i, tk in enumerate(toks):
        if tk.kind == "note":
            if cur:
                events.append((cur[0], i, cur[1], cur[2]))
            cur = (i, tk.items, core.stable_seed(sid, P["name"], kinds[i], int(local[i])))
        elif tk.kind == "off":
            if cur:
                events.append((cur[0], i, cur[1], cur[2]))
                cur = None
    if cur:
        events.append((cur[0], len(toks), cur[1], cur[2]))
    oct_shift = 12 * P["octave"]
    nbuf = len(buf)
    for si, ei, items, seed in events:
        st = int(pos[si])
        n_gate = max(1, int(pos[ei]) - st)
        env = core.adsr(n_gate, sr, P["env"])
        n = len(env)
        midis = [m + oct_shift for m, _ in items]
        vels = [v for _, v in items]
        if P["arp_rate"] > 0 and len(midis) > 1:
            voices = [(midis[0], [m - midis[0] for m in midis], float(np.mean(vels)))]
        else:
            g = 1.0 / math.sqrt(len(midis))
            voices = [(m, None, v * g) for m, v in zip(midis, vels)]
        duty = P["duty"]
        if P["duty_end"] != P["duty"]:
            duty = np.linspace(P["duty"], P["duty_end"], n)
        y = np.zeros(n)
        for vi, (m, arp, vel) in enumerate(voices):
            f = float(core.midi_to_hz(m)) * core.freq_mods(n, sr, P["vib"], arp, P["arp_rate"])
            if P["wave"] == "noise":
                f = f * core.NOISE_CLOCK_SCALE
            ph = 0.29 * vi
            w = core.oscillate(P["wave"], f, sr, duty=duty, phase0=ph, seed=seed + vi)
            if P["detune"]:
                f2 = f * 2.0 ** (P["detune"] / 1200.0)
                w = DETUNE_GAIN * (w + core.oscillate(P["wave"], f2, sr, duty=duty, phase0=ph + 0.25, seed=seed + 50 + vi))
            y += vel * w
        y *= env
        e = min(nbuf, st + n)
        if e > st:
            buf[st:e] += y[:e - st]


def _render_drums(S, P, toks, pos, local, kinds, sr, buf):
    nbuf = len(buf)
    for i, tk in enumerate(toks):
        if tk.kind != "drum":
            continue
        st = int(pos[i])
        for ch, vel in tk.items:
            var = core.stable_seed(kinds[i], int(local[i]), ch)
            h = drums.hit(ch, sr, var)
            e = min(nbuf, st + len(h))
            if e > st:
                buf[st:e] += vel * h[:e - st]


def render_mix(S, blocks, sr, tail_sec):
    """blocks を続けて作り、全部の楽器を足した音（ノーマライズ前）を返す。"""
    tracks, pos, local, kinds, starts = build_timeline(S, blocks, sr)
    end = starts[-1]
    total = end + int(round(tail_sec * sr))
    mix = np.zeros(total)
    for name, P in S["instruments"].items():
        toks = tracks[name]
        if not any(t.kind in ("note", "drum") for t in toks):
            continue
        buf = np.zeros(total)
        if P["drum"]:
            _render_drums(S, P, toks, pos, local, kinds, sr, buf)
        else:
            _render_melodic(S, P, toks, pos, local, kinds, sr, buf)
        buf = core.highpass(buf, P["hp"], sr)
        buf = core.lowpass(buf, P["lp"], sr)
        if P["echo"]:
            d, fb, mix_amt = P["echo"]
            buf = core.echo(buf, int(round(d * S["step_dur"] * sr)), fb, mix_amt)
        mix += P["vol"] * buf
    M = S["master"]
    mix = core.highpass(mix, M["hp"], sr)
    mix = core.lowpass(mix, M["lp"], sr)
    mix = core.highpass(mix, 12.0, sr)  # 直流を取る
    return mix, starts


def _tail_seconds(S):
    t = 0.0
    for P in S["instruments"].values():
        r = 1.6 if P["drum"] else max(P["env"]["r"], core.MIN_RELEASE)
        if P["echo"]:
            d, fb, mix = P["echo"]
            r += core.echo_tail_seconds(d * S["step_dur"], fb, mix, limit=6.0)
        t = max(t, r)
    return t + 0.05


def render_song(D: dict, sr: int = core.BGM_RATE, passes: int = 2):
    """曲を作る。戻り値 (outputs, info, warnings)。outputs = {ファイル名(拡張子なし): float 配列}。
    passes はテスト用（3 にすると、ループの2回目と3回目が同じか調べられる）。"""
    S, warns = parse_song(D)
    sid = S["id"]
    gain = S["master"]["gain"]
    intro = [("intro", S["intro"])] if S["intro"] else []
    info = {"bpm": S["bpm"], "steps_per_bar": S["steps_per_bar"], "step_dur": S["step_dur"],
            "loop_bars": sum(S["sections"][n]["bars"] for n in S["loop"]),
            "intro_bars": sum(S["sections"][n]["bars"] for n in S["intro"]), "jingle": S["jingle"]}
    if S["jingle"]:
        mix, starts = render_mix(S, intro + [("loop", S["loop"])], sr, max(_tail_seconds(S), JINGLE_TAIL))
        end = starts[-1]
        n_tail = int(round(JINGLE_TAIL * sr))
        x = mix[:end + n_tail].copy()
        tt = np.arange(n_tail) / max(1, n_tail - 1)
        x[end:] *= np.cos(0.5 * np.pi * tt) ** 2  # 余韻をフェードアウト
        pk = float(np.max(np.abs(x)))
        if pk <= 1e-9:
            raise SynthError("音が無い（全部休み）")
        thr = 1e-3 * pk / SONG_PEAK
        above = np.nonzero(np.abs(x) > thr)[0]
        x = x[:int(above[-1]) + int(0.01 * sr)]
        core.fade_out(x, int(0.01 * sr))
        x *= SONG_PEAK * gain / pk
        info["samples"] = len(x)
        return {sid: x}, info, warns

    blocks = intro + [("loop", S["loop"])] * passes
    # ループの外には余韻が要らない（2回目の終わりで切る）ので、短い余白だけ
    mix, starts = render_mix(S, blocks, sr, 0.05)
    i0 = 1 if intro else 0
    a, b = starts[i0 + 1], starts[i0 + 2]
    loop = mix[a:b].copy()
    outs = {}
    if intro:
        outs[sid + "_intro"] = mix[starts[0]:starts[1]].copy()
    outs[sid] = loop
    pk = max(float(np.max(np.abs(v))) for v in outs.values())
    if pk <= 1e-9:
        raise SynthError("音が無い（全部休み）")
    k = SONG_PEAK * gain / pk  # イントロとループは同じ倍率（音の大きさがそろう）
    for key in outs:
        outs[key] = outs[key] * k
    info["loop_samples"] = b - a
    info["loop_exact"] = info["loop_bars"] * S["steps_per_bar"] * S["step_dur"] * sr
    info["intro_samples"] = (starts[1] - starts[0]) if intro else 0
    if passes >= 3:
        info["pass3"] = mix[starts[i0 + 2]:starts[i0 + 3]] * k
    return outs, info, warns
