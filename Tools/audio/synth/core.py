"""
音を作るための部品（2026-11-19）。

- 音名 → 周波数
- 波（矩形・パルス・三角・のこぎり・サイン・ファミコンのノイズ）
- ADSR（音の大きさの形）
- フィルター（ローパス・ハイパス）・ビット落とし・エコー
- WAV の書き出し

全部 numpy でまとめて計算する（1サンプルずつの Python のループはしない）。
同じデータからはいつも同じ WAV ができるように、乱数は使わない（ノイズの始まりの位置は id から決める）。
"""
from __future__ import annotations

import math
import re
import zlib

import numpy as np
from scipy.io import wavfile
from scipy.signal import lfilter

SE_RATE = 44100
BGM_RATE = 32000

WAVES = ("square", "pulse", "triangle", "saw", "sine", "noise", "noise_tonal")

DEFAULT_ENV = {"a": 0.002, "d": 0.05, "s": 0.7, "r": 0.03}
# レシピ・楽器の noise の freq に、この倍率をかけて LFSR を回す（1.0 = FORMAT のとおり「freq 回/秒」）。
# 聞いてみて全体に暗すぎるなら 2〜3 にする（ドラムの音は変わらない）。
NOISE_CLOCK_SCALE = 2.5  # 2026-11-19：レシピの freq（1〜5kHz で「シュッ」）が暗すぎたので 2.5 倍（ドラムは変わらない）
MIN_RELEASE = 0.002  # 余韻 0 でもプツッと鳴らないように、最低 2ms で消す


class SynthError(Exception):
    """データのまちがい。メッセージには、どのキーがおかしいかを入れる。"""


# ---------------------------------------------------------------- 音名

_NOTE_RE = re.compile(r"^([A-Ga-g])([#b]?)(-?\d)$")
_SEMI = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def note_to_midi(name: str) -> float:
    """'C4' → 60、'A4' → 69、'F#4'、'Bb3' など。"""
    m = _NOTE_RE.match(name.strip())
    if not m:
        raise SynthError("音名が読めない: %r（例 C5・F#4・Bb3）" % name)
    letter, acc, octv = m.group(1).upper(), m.group(2), int(m.group(3))
    semi = _SEMI[letter] + (1 if acc == "#" else -1 if acc == "b" else 0)
    return 12 * (octv + 1) + semi


def midi_to_hz(midi):
    return 440.0 * np.power(2.0, (np.asarray(midi, dtype=np.float64) - 69.0) / 12.0)


def parse_freq(v, key: str = "freq") -> float:
    """数字（Hz）か音名を Hz にする。"""
    if isinstance(v, bool):
        raise SynthError("%s: 数字か音名にする（%r）" % (key, v))
    if isinstance(v, (int, float)):
        if not (v > 0):
            raise SynthError("%s: 0 より大きくする（%r）" % (key, v))
        return float(v)
    if isinstance(v, str):
        try:
            return float(midi_to_hz(note_to_midi(v)))
        except SynthError as e:
            raise SynthError("%s: %s" % (key, e))
    raise SynthError("%s: 数字か音名にする（%r）" % (key, v))


def stable_seed(*parts) -> int:
    """毎回同じになる種（Python の hash() は起動ごとに変わるので使わない）。"""
    return zlib.crc32("/".join(str(p) for p in parts).encode("utf-8")) & 0x7FFFFFFF


# ---------------------------------------------------------------- ファミコンのノイズ（15bit LFSR）

def _lfsr_sequence(short: bool) -> np.ndarray:
    """LFSR を回して、出る値（+1/-1）の1周期ぶんを返す。
    長いモード：bit0 xor bit1（32767 で1周）。短いモード：bit0 xor bit6（93 で1周 → 金属っぽい音）。"""
    tap = 6 if short else 1
    state = 1
    if short:
        # 93 の周期に入るまで少し回しておく
        for _ in range(200):
            fb = (state ^ (state >> tap)) & 1
            state = (state >> 1) | (fb << 14)
    start = state
    out = []
    while True:
        out.append(1.0 if (state & 1) == 0 else -1.0)
        fb = (state ^ (state >> tap)) & 1
        state = (state >> 1) | (fb << 14)
        if state == start or len(out) > 40000:
            break
    seq = np.array(out, dtype=np.float64)
    return seq - seq.mean()


_LFSR_LONG = _lfsr_sequence(False)
_LFSR_SHORT = _lfsr_sequence(True)
_LFSR_LONG_CS = np.concatenate([[0.0], np.cumsum(_LFSR_LONG)])
_LFSR_SHORT_CS = np.concatenate([[0.0], np.cumsum(_LFSR_SHORT)])


def _lfsr_run(seq: np.ndarray, cs: np.ndarray, clock: np.ndarray, sr: int, seed: int) -> np.ndarray:
    """LFSR を1秒に clock 回進めた値を、1サンプルの間で平均して返す（サンプル＆ホールドを少しなめらかにしたもの）。
    clock がサンプルレートより速いときも、間を平均するので折り返しのジャリジャリが減る。"""
    L = len(seq)
    total = cs[L]

    def integ(x):
        k = np.floor(x)
        i = k.astype(np.int64) % L
        full = np.floor_divide(k, L)
        return full * total + cs[i] + (x - k) * seq[i]

    inc = np.asarray(clock, dtype=np.float64) / sr
    ph1 = float(seed % L) + np.cumsum(inc)
    ph0 = ph1 - inc
    width = ph1 - ph0
    out = np.empty(len(inc))
    ok = width > 1e-9
    out[ok] = (integ(ph1[ok]) - integ(ph0[ok])) / width[ok]
    if (~ok).any():
        out[~ok] = seq[np.floor(ph0[~ok]).astype(np.int64) % L]
    return out


# ---------------------------------------------------------------- 波

def _polyblep(t: np.ndarray, dt: np.ndarray) -> np.ndarray:
    """PolyBLEP：波の段差の所だけ少しなめらかにして、高い音の折り返し（ジャリジャリ）を減らす。"""
    out = np.zeros_like(t)
    m = t < dt
    if m.any():
        x = t[m] / dt[m]
        out[m] = x + x - x * x - 1.0
    m = t > 1.0 - dt
    if m.any():
        x = (t[m] - 1.0) / dt[m]
        out[m] = x * x + x + x + 1.0
    return out


def oscillate(wave: str, freq: np.ndarray, sr: int, duty=0.5, phase0: float = 0.0, seed: int = 0) -> np.ndarray:
    """1サンプルごとの周波数 freq（配列）から波を作る。
    位相は周波数を足し上げて作るので、周波数が動いても（スイープ・ビブラート・アルペジオ）途切れない。
    noise / noise_tonal の freq は「LFSR を1秒に何回進めるか」（サンプル＆ホールド）。"""
    n = len(freq)
    if n == 0:
        return np.zeros(0)
    freq = np.asarray(freq, dtype=np.float64)
    if wave in ("noise", "noise_tonal"):
        if wave == "noise_tonal":
            # 短いモードは 93 ステップで1周する。freq（音名）をそのまま「金属っぽい音の高さ」にするため、93 倍の速さで回す
            return _lfsr_run(_LFSR_SHORT, _LFSR_SHORT_CS, freq * len(_LFSR_SHORT), sr, seed)
        return _lfsr_run(_LFSR_LONG, _LFSR_LONG_CS, freq, sr, seed)

    f = np.clip(freq, 0.0, sr * 0.45)
    dt = f / sr
    # 1サンプル目の位相が phase0 になるように足し上げる
    ph = np.cumsum(dt) - dt[0] + phase0
    p = ph - np.floor(ph)
    dtc = np.maximum(dt, 1e-9)
    if wave == "sine":
        return np.sin(2.0 * np.pi * p)
    if wave == "triangle":
        # 位相 0 で 0 から始まる三角波
        q = (p + 0.25) % 1.0
        return 2.0 * np.abs(2.0 * q - 1.0) - 1.0
    if wave == "saw":
        q = (p + 0.5) % 1.0  # 0 から始める
        return (2.0 * q - 1.0) - _polyblep(q, dtc)
    if wave in ("square", "pulse"):
        d = np.clip(np.broadcast_to(np.asarray(duty, dtype=np.float64), (n,)), 0.01, 0.99)
        naive = np.where(p < d, 1.0, -1.0)
        q = (p - d) % 1.0
        y = naive + _polyblep(p, dtc) - _polyblep(q, dtc)
        # 幅の狭いパルスは平均が 0 からずれる（直流）。引いておくと、音の始まり・終わりで「ボコッ」と鳴らない
        return y - (2.0 * d - 1.0)
    raise SynthError("wave: %r は無い（%s）" % (wave, "・".join(WAVES)))


# ---------------------------------------------------------------- ADSR

def parse_env(env, key: str = "env") -> dict:
    out = dict(DEFAULT_ENV)
    if env is None:
        return out
    if not isinstance(env, dict):
        raise SynthError("%s: {a,d,s,r} の形にする" % key)
    for k, v in env.items():
        if k not in out:
            raise SynthError("%s.%s: 知らないキー（a・d・s・r だけ）" % (key, k))
        if isinstance(v, bool) or not isinstance(v, (int, float)) or v < 0:
            raise SynthError("%s.%s: 0 以上の数字にする（%r）" % (key, k, v))
        out[k] = float(v)
    if out["s"] > 1.0:
        raise SynthError("%s.s: 0〜1 にする（%r）" % (key, out["s"]))
    return out


def adsr(n_gate: int, sr: int, env: dict) -> np.ndarray:
    """ゲート（音を押している間）n_gate サンプル＋余韻の、大きさの形を返す。
    立ち上がりはまっすぐ、減り方と余韻は始めが速い曲線（チップっぽく歯切れよく）。
    ゲートが a+d より短いときは、その時の大きさから余韻に入る。"""
    a, d, s = env["a"], env["d"], env["s"]
    r = max(env["r"], MIN_RELEASE)
    n_rel = max(1, int(round(r * sr)))
    t = np.arange(n_gate, dtype=np.float64) / sr
    g = np.empty(n_gate)
    if a > 0:
        g[:] = np.minimum(t / a, 1.0)
    else:
        g[:] = 1.0
    if d > 0:
        x = np.clip((t - a) / d, 0.0, 1.0)
        dec = s + (1.0 - s) * (1.0 - x) ** 2
    else:
        dec = np.where(t >= a, s, 1.0)
    g = np.where(t < a, g, dec)
    # ゲートを離した時の大きさから余韻に入る
    lv = float(g[-1]) if n_gate > 0 else (0.0 if a > 0 else 1.0)
    xr = (np.arange(1, n_rel + 1, dtype=np.float64)) / n_rel
    rel = lv * (1.0 - xr) ** 2
    return np.concatenate([g, rel])


# ---------------------------------------------------------------- フィルター・エフェクト

def lowpass(x: np.ndarray, fc: float, sr: int) -> np.ndarray:
    """1次のローパス。"""
    if not fc or fc <= 0 or fc >= sr * 0.49:
        return x
    a = 1.0 - math.exp(-2.0 * math.pi * fc / sr)
    return lfilter([a], [1.0, -(1.0 - a)], x)


def highpass(x: np.ndarray, fc: float, sr: int) -> np.ndarray:
    """1次のハイパス。"""
    if not fc or fc <= 0:
        return x
    rc = 1.0 / (2.0 * math.pi * fc)
    k = rc / (rc + 1.0 / sr)
    return lfilter([k, -k], [1.0, -k], x)


def crush(x: np.ndarray, bits: float) -> np.ndarray:
    """ビットを落とす。今の一番大きい所を 1 として段々にする（音量に関係なく同じザラザラになる）。"""
    if not bits or bits <= 0:
        return x
    pk = float(np.max(np.abs(x))) if len(x) else 0.0
    if pk <= 0:
        return x
    q = 2.0 ** (float(bits) - 1.0)
    return np.round(x / pk * q) / q * pk


def echo_tail_seconds(delay: float, fb: float, mix: float, limit: float = 4.0) -> float:
    """エコーが -60dB まで小さくなるまでの長さ（秒）。"""
    if delay <= 0 or mix <= 0:
        return 0.0
    if fb <= 0:
        return delay
    k = math.ceil(math.log(1e-3 / max(mix, 1e-6)) / math.log(min(fb, 0.95))) + 1
    return min(delay * max(k, 1), limit)


def echo(x: np.ndarray, delay_samples: int, fb: float, mix: float) -> np.ndarray:
    """戻りのある遅れ線のエコー。出力 = 元の音 + mix × エコー。
    w[n] = x[n-D] + fb × w[n-D] を D サンプルずつのかたまりで計算する（速い）。"""
    D = int(delay_samples)
    if D <= 0 or mix <= 0:
        return x
    n = len(x)
    if D < 64:
        b = np.zeros(D + 1)
        b[D] = 1.0
        a = np.zeros(D + 1)
        a[0] = 1.0
        a[D] = -fb
        w = lfilter(b, a, x)
    else:
        w = np.zeros(n)
        for s in range(D, n, D):
            e = min(s + D, n)
            w[s:e] = x[s - D:e - D] + fb * w[s - D:e - D]
    return x + mix * w


def fade_in(x: np.ndarray, n: int) -> None:
    n = min(n, len(x))
    if n > 0:
        x[:n] *= np.linspace(0.0, 1.0, n, endpoint=False)


def fade_out(x: np.ndarray, n: int) -> None:
    n = min(n, len(x))
    if n > 0:
        x[len(x) - n:] *= np.linspace(1.0, 0.0, n)


def write_wav(path: str, x: np.ndarray, sr: int) -> None:
    """16bit・モノラルの WAV を書く。"""
    if not np.all(np.isfinite(x)):
        raise SynthError("音に NaN・無限大が入った（作り方のまちがい）")
    data = np.clip(np.round(x * 32767.0), -32768, 32767).astype(np.int16)
    wavfile.write(path, sr, data)


def read_wav(path: str):
    """WAV を読んで (サンプルレート, -1〜1 の float 配列) を返す。"""
    sr, data = wavfile.read(path)
    if data.ndim > 1:
        data = data.mean(axis=1)
    if data.dtype == np.int16:
        x = data.astype(np.float64) / 32768.0
    elif data.dtype == np.int32:
        x = data.astype(np.float64) / 2147483648.0
    elif data.dtype == np.uint8:
        x = (data.astype(np.float64) - 128.0) / 128.0
    else:
        x = data.astype(np.float64)
    return sr, x


# ---------------------------------------------------------------- 値を調べる小さな関数

def num(d: dict, key: str, default=None, lo=None, hi=None, required=False, integer=False):
    """d[key] を数字として取り出す。範囲外・型ちがいは SynthError。"""
    if key not in d or d[key] is None:
        if required:
            raise SynthError("%s: 必須" % key)
        return default
    v = d[key]
    if isinstance(v, bool) or not isinstance(v, (int, float)):
        raise SynthError("%s: 数字にする（%r）" % (key, v))
    if integer and float(v) != int(v):
        raise SynthError("%s: 整数にする（%r）" % (key, v))
    if lo is not None and v < lo:
        raise SynthError("%s: %s 以上にする（%r）" % (key, lo, v))
    if hi is not None and v > hi:
        raise SynthError("%s: %s 以下にする（%r）" % (key, hi, v))
    return int(v) if integer else float(v)


def num_list(d: dict, key: str, n_min: int, n_max: int):
    if key not in d or d[key] is None:
        return None
    v = d[key]
    if not isinstance(v, list) or not (n_min <= len(v) <= n_max) or any(
            isinstance(e, bool) or not isinstance(e, (int, float)) for e in v):
        raise SynthError("%s: 数字 %d〜%d 個の配列にする（%r）" % (key, n_min, n_max, v))
    return [float(e) for e in v]


def freq_mods(n: int, sr: int, vib=None, arp=None, arp_rate=0.0) -> np.ndarray:
    """ビブラート・アルペジオの、周波数にかける倍率（配列）。t は音の頭から。
    vib = [深さ半音, 速さHz, かかり始める秒]。始める秒があるときは 0.08 秒かけて深くする。"""
    t = np.arange(n, dtype=np.float64) / sr
    semi = np.zeros(n)
    if vib:
        depth, rate = vib[0], vib[1]
        delay = vib[2] if len(vib) > 2 else 0.0
        if depth and rate:
            tv = t - delay
            amt = np.clip(tv / 0.08, 0.0, 1.0) if delay > 0 else (tv >= 0).astype(np.float64)
            semi += depth * amt * np.sin(2.0 * np.pi * rate * np.maximum(tv, 0.0))
    if arp and arp_rate and arp_rate > 0:
        k = np.floor(t / arp_rate).astype(np.int64) % len(arp)
        semi += np.asarray(arp, dtype=np.float64)[k]
    if not semi.any():
        return np.ones(n)
    return np.power(2.0, semi / 12.0)
