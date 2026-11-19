"""
ファミコン風のドラム（kit "chip"）（2026-11-19）。

k キック・s スネア・h クローズドハイハット・o オープンハイハット・c クラッシュ・t ロータム・m ミッドタム・
p クラップ・b ベルのような金属音。三角波・ノイズ・矩形波で作る。
1つの打ち方につき数種類を作っておき、マスの位置で選ぶ（同じ位置ならいつも同じ音 → ループがきれいにつながる）。
"""
from __future__ import annotations

import numpy as np

from . import core

DRUM_CHARS = "kshoctmpb"
VARIANTS = 3

# 打ち方ごとの大きさ（キックを 1 とした目安）
_GAIN = {"k": 1.3, "s": 1.1, "h": 0.5, "o": 0.5, "c": 0.65, "t": 1.0, "m": 0.95, "p": 1.0, "b": 0.6}

_cache = {}


def _t(sec, sr):
    return np.arange(int(round(sec * sr)), dtype=np.float64) / sr


def _expenv(t, tau, attack=0.0005):
    e = np.exp(-t / tau)
    if attack > 0:
        e *= np.minimum(t / attack, 1.0)
    return e


def _end_fade(x, sr, sec=0.01):
    core.fade_out(x, int(sec * sr))
    return x


def _noise(n, clock, sr, seed, tonal=False):
    f = np.full(n, float(clock))
    return core.oscillate("noise_tonal" if tonal else "noise", f, sr, seed=seed)


def _kick(sr, v):
    t = _t(0.24, sr)
    f = 45.0 + 105.0 * np.exp(-t / 0.028)  # 150 Hz → 45 Hz に落ちる
    body = core.oscillate("triangle", f, sr) * 0.85 + core.oscillate("sine", f, sr) * 0.35
    body *= _expenv(t, 0.085)
    # 頭のカチッ（小さいスピーカーでも聞こえるように）
    click = _noise(len(t), sr * 0.9, sr, 101 + v) * _expenv(t, 0.0014, 0.0)
    click += core.oscillate("pulse", np.full(len(t), 1400.0), sr, duty=0.25) * _expenv(t, 0.002, 0.0) * 0.4
    return _end_fade(body + click * 0.7, sr)


def _snare(sr, v):
    t = _t(0.2, sr)
    nz = _noise(len(t), 15000.0, sr, 202 + v * 7)
    nz = core.highpass(nz, 600.0, sr) * _expenv(t, 0.075)
    f = 160.0 + 40.0 * np.exp(-t / 0.02)  # 200 → 160 Hz の短い音
    tone = core.oscillate("triangle", f, sr) * _expenv(t, 0.035)
    return _end_fade(nz * 0.9 + tone * 0.75, sr)


def _hat(sr, v, tau, dur):
    t = _t(dur, sr)
    nz = _noise(len(t), sr * 1.6, sr, 303 + v * 11)
    nz = core.highpass(core.highpass(nz, 5000.0, sr), 3000.0, sr)
    return _end_fade(nz * _expenv(t, tau, 0.0003), sr, min(0.01, dur / 4))


def _crash(sr, v):
    t = _t(1.5, sr)
    nz = _noise(len(t), sr * 1.2, sr, 404 + v * 13)
    shimmer = _noise(len(t), 3100.0, sr, 405 + v, tonal=True) * 0.18
    x = core.highpass(nz + shimmer, 2500.0, sr)
    env = 0.35 * _expenv(t, 0.04, 0.001) + 0.65 * _expenv(t, 0.5, 0.001)
    return _end_fade(x * env, sr, 0.05)


def _tom(sr, v, hi, lo):
    t = _t(0.32, sr)
    f = lo + (hi - lo) * np.exp(-t / 0.06)
    body = core.oscillate("triangle", f, sr) * _expenv(t, 0.12)
    click = _noise(len(t), 9000.0, sr, 505 + v) * _expenv(t, 0.003, 0.0) * 0.25
    return _end_fade(body + click, sr)


def _clap(sr, v):
    t = _t(0.22, sr)
    nz = _noise(len(t), 22000.0, sr, 606 + v * 17)
    nz = core.lowpass(core.highpass(nz, 900.0, sr), 4500.0, sr)
    env = np.zeros(len(t))
    for st in (0.0, 0.011, 0.022):  # 3回すばやく
        tt = t - st
        env += np.where(tt >= 0, np.exp(-np.maximum(tt, 0) / 0.0045), 0.0)
    tt = t - 0.03
    env += np.where(tt >= 0, 0.7 * np.exp(-np.maximum(tt, 0) / 0.06), 0.0)
    return _end_fade(nz * env, sr)


def _bell(sr, v):
    t = _t(0.4, sr)
    x = np.zeros(len(t))
    # 少しずらした高い矩形波を重ねる（金属っぽいうなり）
    for f, a in ((830.0, 1.0), (838.0, 0.6), (1245.0, 0.7), (1257.0, 0.4)):
        x += a * core.oscillate("square", np.full(len(t), f), sr, phase0=0.13 * a)
    x = core.highpass(x, 600.0, sr)
    env = _expenv(t, 0.07, 0.0008)
    return _end_fade(x * env * 0.5, sr)


_BUILDERS = {
    "k": _kick,
    "s": _snare,
    "h": lambda sr, v: _hat(sr, v, 0.02, 0.08),
    "o": lambda sr, v: _hat(sr, v, 0.12, 0.4),
    "c": _crash,
    "t": lambda sr, v: _tom(sr, v, 210.0, 90.0),
    "m": lambda sr, v: _tom(sr, v, 310.0, 140.0),
    "p": _clap,
    "b": _bell,
}


def hit(ch: str, sr: int, variant: int) -> np.ndarray:
    """打ち方 ch の音（一番大きい所 = 打ち方ごとの目安の大きさ）。"""
    key = (ch, sr, variant % VARIANTS)
    if key not in _cache:
        x = _BUILDERS[ch](sr, variant % VARIANTS)
        pk = float(np.max(np.abs(x)))
        _cache[key] = x * (_GAIN[ch] / pk if pk > 0 else 0.0)
    return _cache[key]


def max_len(sr: int) -> int:
    return int(1.6 * sr)
