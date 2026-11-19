"""
ゲームの音（SE・BGM）を JSON のデータから作るプログラム（2026-11-19）。
決まりは Tools/audio/FORMAT.md。使い方は Tools/audio/build_audio.py。
"""
from .core import SE_RATE, BGM_RATE, SynthError, write_wav, read_wav  # noqa: F401
from .se import render_sound, parse_sound  # noqa: F401
from .song import render_song, parse_song  # noqa: F401
