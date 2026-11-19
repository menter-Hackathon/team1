#!/usr/bin/env python3
"""
音の試聴ページを作る（2026-11-19）。
  python3 Tools/audio/make_preview.py  →  Tools/audio/preview.html
ブラウザで開くと、全部の BGM・SE を説明つきで聞ける（Assets/Resources/Audio の WAV をそのまま鳴らす）。
SE は、どの技・どの出来事で鳴るかも出す。音を作り直したら（build_audio.py）、これも作り直す。
"""
import glob
import html
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
AUDIO = os.path.join(ROOT, "Assets", "Resources", "Audio")
OUT = os.path.join(HERE, "preview.html")

# レシピのファイル → 見出し
GROUPS = [
    ("common.json", "共通（プレイヤー・UI・出来事）"),
    ("fix5.json", "Chrome・VS Code・Finder・Premiere Pro・Terminal・はじめの能力"),
    ("kit1.json", "チャット・仕事・AI（キット1）"),
    ("kit2.json", "開発・制作・ゲーム・音楽・ブラウザ（キット2）"),
    ("gimmick_a.json", "ギミック：Chrome・VS Code・Finder・Premiere Pro・Terminal・チャット・予定・Zoom"),
    ("gimmick_b.json", "ギミック：AI・Office・メモ"),
    ("gimmick_c.json", "ギミック：3D・動画・開発・ブラウザ"),
    ("gimmick_d.json", "ギミック：ゲーム・音楽・制作・Arc"),
]

BGM_PLACE = {
    "bgm_title": "タイトル", "bgm_dex": "アプリ図鑑", "bgm_loadout": "能力のカードを選ぶ画面",
    "bgm_desktop": "感染したデスクトップ（ステージの前）", "bgm_stage_desktop": "1画面のステージ",
    "bgm_stage_scroll": "横スクロールのステージ（Chrome）", "bgm_stage_windows": "ウィンドウのステージ（VS Code）",
    "bgm_stage_music": "ミュージックのステージ（BPM 100）", "bgm_boss": "ボス戦", "bgm_boss_final": "ラスボス Terminal",
    "bgm_result": "リザルト", "jingle_boss_appear": "ボスが現れる", "jingle_stage_clear": "ステージクリア",
    "jingle_gameover": "ゲームオーバー",
}


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def usage_index():
    """SE の id → 使っている所（技の名前・出来事のキー）"""
    use = {}
    for p in sorted(glob.glob(os.path.join(AUDIO, "Maps", "*.json"))):
        d = load(p)
        for r in d.get("moves", []) or []:
            for slot in ("start", "release"):
                sid = r.get(slot) or ""
                if sid:
                    use.setdefault(sid, []).append(f"{r['move']}（{'始め' if slot == 'start' else '出た瞬間'}）")
        for r in d.get("events", []) or []:
            if r.get("se"):
                use.setdefault(r["se"], []).append(f"出来事 {r['key']}")
        for r in d.get("fallbacks", []) or []:
            for slot in ("start", "release"):
                sid = r.get(slot) or ""
                if sid:
                    use.setdefault(sid, []).append(f"割り当てが無い技 {r['team']}×{r['kind']}")
    return use


def rel(path):
    return os.path.relpath(path, HERE).replace(os.sep, "/")


def app_of(sid):
    m = re.match(r"se_(?:gm_)?([a-z0-9]+)_", sid)
    return m.group(1) if m else "other"


def main():
    use = usage_index()
    parts = []
    # BGM
    rows = []
    for p in sorted(glob.glob(os.path.join(HERE, "bgm", "*.json")), key=lambda x: list(BGM_PLACE).index(os.path.basename(x)[:-5]) if os.path.basename(x)[:-5] in BGM_PLACE else 99):
        d = load(p)
        sid = d["id"]
        wav = os.path.join(AUDIO, "BGM", sid + ".wav")
        intro = os.path.join(AUDIO, "BGM", sid + "_intro.wav")
        if not os.path.exists(wav):
            continue
        players = ""
        if os.path.exists(intro):
            players += f'<div class="sub">イントロ（1回だけ）</div><audio controls preload="none" src="{html.escape(rel(intro))}"></audio><div class="sub">ループ</div>'
        players += f'<audio controls preload="none" {"loop" if not sid.startswith("jingle_") else ""} src="{html.escape(rel(wav))}"></audio>'
        rows.append(f'<div class="card bgm"><div class="head"><b>{html.escape(BGM_PLACE.get(sid, sid))}</b><code>{sid}</code><span class="tag">BPM {d.get("bpm")}</span></div>'
                    f'<p>{html.escape(d.get("desc", ""))}</p>{players}</div>')
    parts.append(f'<h2>BGM・ジングル（{len(rows)}）</h2><div class="grid">{"".join(rows)}</div>')

    # SE
    total = 0
    for fname, title in GROUPS:
        p = os.path.join(HERE, "se", fname)
        if not os.path.exists(p):
            continue
        sounds = load(p).get("sounds", [])
        by_app = {}
        for s in sounds:
            by_app.setdefault(app_of(s["id"]) if fname != "common.json" else "common", []).append(s)
        blocks = []
        for app, items in by_app.items():
            cards = []
            for s in items:
                sid = s["id"]
                wav = os.path.join(AUDIO, "SE", sid + ".wav")
                if not os.path.exists(wav):
                    continue
                total += 1
                u = use.get(sid, [])
                more = f'<details><summary>使う所 {len(u)}</summary><div class="use">{"<br>".join(html.escape(x) for x in u)}</div></details>' if u else '<div class="sub">（コードから鳴らす・または未使用）</div>'
                cards.append(f'<div class="card se" data-src="{html.escape(rel(wav))}"><button class="play" aria-label="再生">▶</button>'
                             f'<div class="body"><code>{sid}</code><p>{html.escape(s.get("desc", ""))}</p>{more}</div></div>')
            label = "" if app == "common" else f"<h3>{html.escape(app)}</h3>"
            blocks.append(f'{label}<div class="grid">{"".join(cards)}</div>')
        parts.append(f'<h2>SE：{html.escape(title)}（{len(sounds)}）</h2>{"".join(blocks)}')

    page = f"""<!doctype html>
<html lang="ja"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>音の試聴</title>
<style>
:root {{ --bg:#f6f4fa; --card:#fff; --ink:#1e0830; --sub:#6a5a7a; --line:#e2dcea; --accent:#8a2be2; }}
@media (prefers-color-scheme: dark) {{ :root {{ --bg:#16121c; --card:#211a2b; --ink:#f1ecf7; --sub:#a99bb8; --line:#3a2f47; --accent:#c38cff; }} }}
* {{ box-sizing:border-box; }}
body {{ margin:0; padding:24px 16px 64px; background:var(--bg); color:var(--ink); font:14px/1.6 -apple-system, "Hiragino Sans", sans-serif; }}
main {{ max-width:1100px; margin:0 auto; }}
h1 {{ font-size:22px; margin:0 0 4px; }} h2 {{ margin:36px 0 10px; font-size:18px; border-bottom:2px solid var(--line); padding-bottom:6px; }}
h3 {{ margin:18px 0 8px; font-size:14px; color:var(--sub); text-transform:uppercase; letter-spacing:.06em; }}
.lead {{ color:var(--sub); margin:0 0 8px; }}
.grid {{ display:grid; grid-template-columns:repeat(auto-fill, minmax(300px, 1fr)); gap:10px; }}
.card {{ background:var(--card); border:1px solid var(--line); border-radius:10px; padding:10px 12px; }}
.card p {{ margin:4px 0; }} .head {{ display:flex; gap:8px; align-items:baseline; flex-wrap:wrap; }}
code {{ font:12px ui-monospace, Menlo, monospace; color:var(--accent); }} .tag {{ font-size:12px; color:var(--sub); }}
.sub {{ font-size:12px; color:var(--sub); }} audio {{ width:100%; height:32px; margin-top:4px; }}
.se {{ display:flex; gap:10px; align-items:flex-start; }}
.play {{ flex:none; width:36px; height:36px; border-radius:50%; border:none; background:var(--accent); color:#fff; font-size:14px; cursor:pointer; }}
.play.on {{ filter:brightness(1.3); }} .body {{ min-width:0; }}
details summary {{ cursor:pointer; font-size:12px; color:var(--sub); }} .use {{ font-size:12px; color:var(--sub); word-break:break-all; }}
</style></head><body><main>
<h1>音の試聴</h1>
<p class="lead">このゲームの音は全部、Python で一から作った（Tools/audio）。SE {total} 個・曲 {len(rows)} 個。▶ を押すと鳴る。作り直すときは <code>python3 Tools/audio/build_audio.py</code> と <code>python3 Tools/audio/make_preview.py</code>。</p>
{"".join(parts)}
</main>
<script>
const cache = {{}};
document.querySelectorAll('.se').forEach(card => {{
  const btn = card.querySelector('.play');
  btn.addEventListener('click', () => {{
    const src = card.dataset.src;
    const a = cache[src] || (cache[src] = new Audio(src));
    a.currentTime = 0; a.play();
    btn.classList.add('on'); a.onended = () => btn.classList.remove('on');
  }});
}});
</script>
</body></html>
"""
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(page)
    print(f"作った: {OUT}（SE {total} 個・曲 {len(rows)} 個）")


if __name__ == "__main__":
    main()
