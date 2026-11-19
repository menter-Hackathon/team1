"""
SE のレシピ（FORMAT.md 1章）から音を作る（2026-11-19）。

流れ：層を1つずつ作って足す → fx（ハイパス → ローパス → ビット落とし → エコー）→ 後ろの無音を切る
→ （reverse なら逆さに）→ 頭 1ms をフェードイン → 後ろの無音を切って短くフェードアウト → 一番大きい所を peak にそろえる。
"""
from __future__ import annotations

import re

import numpy as np

from . import core
from .core import SynthError, num, num_list

SOUND_KEYS = {"id", "desc", "peak", "layers", "fx"}
LAYER_KEYS = {"wave", "at", "notes", "freq", "freq_end", "dur", "curve", "duty", "duty_end", "vib", "arp",
              "arp_rate", "env", "vol", "repeat", "repeat_gap", "repeat_decay"}
FX_KEYS = {"lp", "hp", "crush", "echo", "reverse"}
SE_ID_RE = re.compile(r"^se_[a-z0-9_]+$")

SILENCE_DBFS = -60.0


class Segment:
    """層の中の1つの音（notes の1音、またはスイープ全体）。"""
    __slots__ = ("start", "gate", "f0", "f1", "curve", "vel")

    def __init__(self, start, gate, f0, f1=None, curve="exp", vel=1.0):
        self.start, self.gate, self.f0, self.f1, self.curve, self.vel = start, gate, f0, f1, curve, vel


def _parse_notes(notes, key="notes"):
    if not isinstance(notes, list) or not notes:
        raise SynthError("%s: [[音名, 長さ秒], ...] の配列にする" % key)
    segs = []
    t = 0.0
    for i, item in enumerate(notes):
        k = "%s[%d]" % (key, i)
        if not isinstance(item, list) or len(item) not in (2, 3):
            raise SynthError("%s: [音名, 長さ秒] にする（%r）" % (k, item))
        name, dur = item[0], item[1]
        if isinstance(dur, bool) or not isinstance(dur, (int, float)) or dur <= 0:
            raise SynthError("%s: 長さは 0 より大きい秒にする（%r）" % (k, dur))
        vel = 1.0
        if len(item) == 3:  # おまけ：3つ目に大きさ（倍率）を書いてもよい
            if isinstance(item[2], bool) or not isinstance(item[2], (int, float)) or item[2] < 0:
                raise SynthError("%s: 3つ目（大きさ）は 0 以上の数字（%r）" % (k, item[2]))
            vel = float(item[2])
        if isinstance(name, str) and name.strip().lower() in ("r", "rest", "-", "."):
            t += float(dur)
            continue
        f = core.parse_freq(name, k)
        segs.append(Segment(t, float(dur), f, vel=vel))
        t += float(dur)
    return segs, t


def parse_layer(L: dict, warns: list, idx: int):
    """層を調べて、作るのに必要な値をまとめる。"""
    pre = "layers[%d]" % idx
    if not isinstance(L, dict):
        raise SynthError("%s: {} の形にする" % pre)
    for k in L:
        if k not in LAYER_KEYS:
            warns.append("%s.%s: 知らないキー（無視する）" % (pre, k))
    try:
        wave = L.get("wave")
        if wave not in core.WAVES:
            raise SynthError("wave: %r は無い（%s）" % (wave, "・".join(core.WAVES)))
        P = {"wave": wave}
        P["at"] = num(L, "at", 0.0, lo=0)
        P["vol"] = num(L, "vol", 0.7, lo=0)
        P["duty"] = num(L, "duty", 0.5, lo=0.01, hi=0.99)
        P["duty_end"] = num(L, "duty_end", P["duty"], lo=0.01, hi=0.99)
        if wave != "pulse" and ("duty" in L or "duty_end" in L):
            warns.append("%s.duty: pulse の時だけ効く（%s では無視）" % (pre, wave))
        if wave != "pulse":
            P["duty"] = P["duty_end"] = 0.5
        P["vib"] = num_list(L, "vib", 2, 3)
        P["arp"] = num_list(L, "arp", 1, 64)
        P["arp_rate"] = num(L, "arp_rate", 0.0, lo=0)
        if P["arp"] and not P["arp_rate"]:
            raise SynthError("arp_rate: arp を使う時は 0 より大きい秒を書く")
        P["env"] = core.parse_env(L.get("env"))
        P["repeat"] = num(L, "repeat", 1, lo=1, hi=64, integer=True)
        P["repeat_gap"] = num(L, "repeat_gap", 0.0, lo=0)
        P["repeat_decay"] = num(L, "repeat_decay", 0.0, lo=0, hi=1)
        has_notes = "notes" in L and L["notes"] is not None
        has_freq = "freq" in L and L["freq"] is not None
        if has_notes and has_freq:
            raise SynthError("notes と freq は片方だけ書く")
        if has_notes:
            for k in ("freq_end", "dur", "curve"):
                if k in L:
                    warns.append("%s.%s: notes の時は使わない（無視する）" % (pre, k))
            segs, gate_total = _parse_notes(L["notes"])
        elif has_freq:
            f0 = core.parse_freq(L["freq"], "freq")
            f1 = core.parse_freq(L["freq_end"], "freq_end") if L.get("freq_end") is not None else f0
            dur = num(L, "dur", required=True, lo=0)
            if dur <= 0:
                raise SynthError("dur: 0 より大きい秒にする")
            curve = L.get("curve", "exp")
            if curve not in ("exp", "lin"):
                raise SynthError("curve: exp か lin（%r）" % curve)
            segs, gate_total = [Segment(0.0, dur, f0, f1, curve)], dur
        else:
            raise SynthError("notes か freq（＋dur）のどちらかが必要")
        P["segs"], P["gate_total"] = segs, gate_total
        return P
    except SynthError as e:
        raise SynthError("%s.%s" % (pre, e))


def render_segment(P: dict, seg: Segment, sr: int, seed: int, t_layer0: float) -> np.ndarray:
    """1つの音を作る（ゲート＋余韻）。t_layer0 はこの音が層の頭から何秒目か（duty の動きに使う）。"""
    n_gate = max(1, int(round(seg.gate * sr)))
    env = core.adsr(n_gate, sr, P["env"])
    n = len(env)
    t = np.arange(n, dtype=np.float64) / sr
    if seg.f1 is not None and seg.f1 != seg.f0:
        x = np.clip(t / seg.gate, 0.0, 1.0)
        if seg.curve == "exp":
            f = seg.f0 * np.power(seg.f1 / seg.f0, x)
        else:
            f = seg.f0 + (seg.f1 - seg.f0) * x
    else:
        f = np.full(n, seg.f0)
    f = f * core.freq_mods(n, sr, P["vib"], P["arp"], P["arp_rate"])
    if P["wave"] == "noise":
        f = f * core.NOISE_CLOCK_SCALE
    duty = 0.5
    if P["wave"] == "pulse":
        if P["duty_end"] != P["duty"]:
            # duty は層の全体（notes の合計の長さ、またはスイープの dur）で動かす
            g = max(P["gate_total"], 1e-6)
            x = np.clip((t_layer0 + t) / g, 0.0, 1.0)
            duty = P["duty"] + (P["duty_end"] - P["duty"]) * x
        else:
            duty = P["duty"]
    y = core.oscillate(P["wave"], f, sr, duty=duty, seed=seed)
    return y * env * seg.vel


def parse_sound(S: dict):
    """音を調べる。戻り値 (params, warnings)。まちがいは SynthError（キーの名前つき）。"""
    warns = []
    if not isinstance(S, dict):
        raise SynthError("音は {} の形にする")
    sid = S.get("id")
    if not isinstance(sid, str) or not SE_ID_RE.match(sid):
        raise SynthError("id: se_ で始まる英小文字・数字・_ だけにする（%r）" % (sid,))
    for k in S:
        if k not in SOUND_KEYS:
            warns.append("%s: 知らないキー（無視する）" % k)
    if not isinstance(S.get("desc"), str) or not S.get("desc").strip():
        warns.append("desc: 説明が無い")
    peak = num(S, "peak", 0.85, lo=0.01, hi=1.0)
    layers = S.get("layers")
    if not isinstance(layers, list) or not layers:
        raise SynthError("layers: 1つ以上の層の配列にする")
    LP = [parse_layer(L, warns, i) for i, L in enumerate(layers)]
    fx = S.get("fx") or {}
    if not isinstance(fx, dict):
        raise SynthError("fx: {} の形にする")
    for k in fx:
        if k not in FX_KEYS:
            warns.append("fx.%s: 知らないキー（無視する）" % k)
    try:
        F = {
            "lp": num(fx, "lp", 0.0, lo=0),
            "hp": num(fx, "hp", 0.0, lo=0),
            "crush": num(fx, "crush", 0.0, lo=0, hi=16),
            "echo": num_list(fx, "echo", 3, 3),
            "reverse": bool(fx.get("reverse", False)),
        }
        if F["echo"]:
            d, fb, mix = F["echo"]
            if d <= 0 or not (0 <= fb <= 0.95) or not (0 <= mix <= 1.5):
                raise SynthError("echo: [遅れ秒 >0, 戻り 0〜0.8, 混ぜる 0〜1]（%r）" % (F["echo"],))
    except SynthError as e:
        raise SynthError("fx.%s" % e)
    return {"id": sid, "peak": peak, "layers": LP, "fx": F}, warns


def render_sound(S: dict, sr: int = core.SE_RATE):
    """音を作る。戻り値 (float 配列, warnings)。"""
    P, warns = parse_sound(S)
    sid = P["id"]
    # 置く場所を全部決めてから、長さを決める
    items = []  # (開始サンプル, 層, 音, 大きさ, 種)
    end = 0
    for li, L in enumerate(P["layers"]):
        for k in range(L["repeat"]):
            # くり返しの次の回は「前の回のゲートが終わってから repeat_gap 秒あけて」始まる
            off = L["at"] + k * (L["gate_total"] + L["repeat_gap"])
            gain = L["vol"] * (1.0 - L["repeat_decay"]) ** k
            for si, seg in enumerate(L["segs"]):
                st = int(round((off + seg.start) * sr))
                ln = max(1, int(round(seg.gate * sr))) + max(1, int(round(max(L["env"]["r"], core.MIN_RELEASE) * sr)))
                items.append((st, li, seg, gain, core.stable_seed(sid, li, k, si)))
                end = max(end, st + ln)
    F = P["fx"]
    tail = 0
    if F["echo"]:
        tail = int(round(core.echo_tail_seconds(*F["echo"]) * sr))
    x = np.zeros(end + tail + 1)
    for st, li, seg, gain, seed in items:
        if gain <= 0:
            continue
        L = P["layers"][li]
        y = render_segment(L, seg, sr, seed, seg.start)
        x[st:st + len(y)] += gain * y
    # fx
    x = core.highpass(x, F["hp"], sr)
    x = core.lowpass(x, F["lp"], sr)
    x = core.crush(x, F["crush"])
    if F["echo"]:
        d, fb, mix = F["echo"]
        x = core.echo(x, int(round(d * sr)), fb, mix)
    x = core.highpass(x, 12.0, sr)  # 直流を取る（聞こえない低さ）
    pk = float(np.max(np.abs(x))) if len(x) else 0.0
    if not np.isfinite(pk):
        raise SynthError("音に NaN・無限大が入った")
    if pk <= 1e-9:
        raise SynthError("音が無い（全部の層の vol が 0、または全部休み）")
    thr = 10 ** (SILENCE_DBFS / 20.0) * pk / P["peak"]  # そろえた後で -60dBFS になる大きさ
    x = _trim_tail(x, thr, sr)
    if F["reverse"]:
        x = x[::-1].copy()
        x = _trim_tail(x, thr, sr)
    core.fade_in(x, int(0.001 * sr))
    core.fade_out(x, min(int(0.006 * sr), max(1, len(x) // 10)))
    pk = float(np.max(np.abs(x)))
    if pk <= 1e-9:
        raise SynthError("音が無い")
    x *= P["peak"] / pk
    return x, warns


def _trim_tail(x: np.ndarray, thr: float, sr: int) -> np.ndarray:
    above = np.nonzero(np.abs(x) > thr)[0]
    if len(above) == 0:
        return x[:1].copy()
    last = int(above[-1]) + int(0.004 * sr)
    return x[:min(len(x), last + 1)].copy()
