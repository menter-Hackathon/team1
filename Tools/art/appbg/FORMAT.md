# アプリの UI の背景（2026-11-19）

ステージに入ったら、背景をそのアプリの画面（UI）にする（まさぴ〜の依頼：「ウィンドウの中なので、ステージの中はそのアプリの UI に。アプリの中で戦っている感じ」）。
レイアウトを JSON で書く → `python3 Tools/art/appbg/render.py` → `Assets/Resources/AppBackgrounds/Bg_<ラベル>.png`（320×180 ドット。1ドット = 1/15 マス。カメラに付いて画面いっぱいに出る）。

```
Tools/art/appbg/
  FORMAT.md        この文書
  render.py        描くプログラム（Opus）
  apps/<ラベル>.json  アプリごとのレイアウト（Fable）
```

ラベル（38）：Chrome, VSCode, Finder, PremierePro, Terminal, Slack, LINE, Messages, Teams, Discord, Calendar, Mail, Zoom, ChatGPT, Claude, Gemini, Notion, Notes, Excel, PowerPoint, Word, Keynote, Unity, Blender, Minecraft, QuickTime, GitHubDesktop, Xcode, Cursor, Safari, Steam, Music, Spotify, Firefox, Figma, Photoshop, Illustrator, Arc

## 画面のどこに何がかぶるか（320×180 の座標。左上が 0,0）

- **左右の壁**：x < 10 と x ≥ 310 は、ステージの壁（暗い灰色）でほぼ隠れる。
- **床**：y ≥ 170 は床（アプリの色）で隠れる。
- **HUD**：左上（x 0〜150, y 0〜22）に相棒・HP・能力の枠、右上（x 240〜320, y 0〜28）にステージの札とボスのゲージ。背景の上に重なる（背景は見えてよいが、細かい物は読めない）。
- **遊ぶ所**：x 10〜310, y 22〜170。足場（細い横の板）・敵（**黒いもじゃもじゃ＋赤い目**）・プレイヤー（黄色）・弾（いろいろな色）・予告（**赤い線・赤い枠**）が出る。

## 見やすさの決まり（いちばん大事）

1. **敵は黒い**。背景の大きな面は、真っ黒・ほぼ黒（明るさ 20% より暗い）にしない。ダークモードのアプリ（Discord・Spotify・VS Code・Photoshop など）も、少し明るい灰色・紺にする（例 `#3a3f4b` くらいより明るく）。
2. **予告は赤い**。大きな面に赤（`#d00`〜`#f44` あたり）を使わない。アプリの色が赤でも、くすんだ色・ピンク・オレンジ寄りにする。
3. **遊ぶ所（まん中）は静かに**：細かい物・強いコントラストは周り（上のバー・左右のサイドバー・下のバー）に置き、まん中は大きな面と薄い線・薄い文字の行（灰色の棒で文字のふり）くらいにする。
4. **全体を少し暗く・薄く**：本物の UI より彩度とコントラストを落とす（ゲームの物が前に出るように）。
5. 本物のロゴ・アイコンの絵はそのまま描かない（形と色の雰囲気だけ）。文字は UI のまね（英語の短い言葉）だけ。説明の文字は入れない。

## JSON

```json
{
  "id": "Slack",
  "desc": "何の画面か（日本語で短く）",
  "base": "#e8e4ec",
  "elements": [
    {"type": "rect", "x": 10, "y": 0, "w": 60, "h": 180, "color": "#4a2a55"},
    {"type": "text", "x": 16, "y": 30, "text": "# general", "color": "#d8c8e0"},
    {"type": "bars", "x": 80, "y": 40, "w": 200, "rows": 12, "row_h": 8, "bar_h": 2, "color": "#c9c3d1", "seed": 3}
  ],
  "veil": {"x": 70, "y": 22, "w": 240, "h": 148, "color": "#ffffff", "alpha": 0.15}
}
```

- 色は `#rgb`・`#rrggbb`・`#rrggbbaa`。`alpha`（0〜1）を書くと半透明で重ねる。
- 描く順は上から（後の物が上）。
- `veil`（省略可）：最後に、遊ぶ所などへ半透明の色をかぶせて静かにする。配列にしてもよい。

### 部品（type）

| type | キー | 何 |
|---|---|---|
| `rect` | x, y, w, h, color | 四角（塗り） |
| `rrect` | x, y, w, h, color, r | 角の丸い四角（r は 1〜4） |
| `frame` | x, y, w, h, color, width | 四角のふち（width 省略 1） |
| `line` | x1, y1, x2, y2, color, width | 線 |
| `hlines` / `vlines` | x, y, w, h, step, color | 横線・縦線を step ごとに（表の罫線など） |
| `grid` | x, y, w, h, cell_w, cell_h, color | 格子 |
| `checker` | x, y, w, h, size, c1, c2 | 市松もよう（透明の印） |
| `gradient` | x, y, w, h, c1, c2, dir（"v" か "h"） | なめらかでなく、4〜8段の帯で色を変える（ドット絵らしく） |
| `circle` | cx, cy, r, color, fill（true/false） | 丸 |
| `text` | x, y, text, color, scale（1 か 2） | 3×5 ドットの英数字（小文字は大文字の形）。使える記号：`# @ . , : ; - + / % ! ? ( ) [ ] < > _ = ' " * & $ ~ |`。1文字 = 4ドット幅（scale 2 で 8。`#` だけ 6ドット幅） |
| `bars` | x, y, w, rows, row_h, bar_h, color, seed, min（0〜1、省略 0.35）, indent（省略 0） | 文字の行のふり：灰色の棒を rows 行。長さは seed で決まる（同じ seed なら同じ） |
| `icon` | name, x, y, size（5〜16）, color | 簡単な印：`avatar`（丸い顔）・`hash`・`bell`・`search`・`plus`・`play`・`pause`・`next`・`prev`・`heart`・`star`・`check`・`gear`・`mic`・`camera`・`folder`・`file`・`chat`・`home`・`user`・`close`・`menu`・`lock`・`cloud`・`music`・`grid`・`list`・`pen`・`brush`・`cube`・`sun`・`moon`・`leaf`・`bolt`・`flag`・`trophy`・`cart`・`download`・`upload`・`shield`・`puzzle`・`globe`・`compass`・`terminal`・`code`・`branch` |
| `repeat` | count, dx, dy, items | items（部品の配列。座標は1回目の位置）を、dx・dy ずつずらして count 回描く。items の text・bars の中の `{i}` は回数（0から）に置きかわる |
| `noise` | x, y, w, h, color, density（0〜1）, seed | ぽつぽつ点を打つ（ざらつき・星など） |

## 描く・見る

```
python3 Tools/art/appbg/render.py                      # 全部 → Assets/Resources/AppBackgrounds/
python3 Tools/art/appbg/render.py --only Slack,LINE --out <dir>   # 試し
python3 Tools/art/appbg/render.py --only Slack --out <dir> --preview  # 見本：3倍に拡大し、HUD・壁・床・足場・敵・プレイヤー・赤い予告の線を重ねた絵も出す（<dir>/preview/）
python3 Tools/art/appbg/render.py --sheet <file.png>    # 全部を並べた一覧の絵
```

## 足場とギミック（`stage`。2026-11-19 追加）

まさぴ〜の依頼：「デザインだけじゃなく、アプリのウィンドウの UI を足場やステージのギミックに使って」。
UI の部品（メッセージの行・セル・タイル・再生バー・ボタンなど）を **乗れる足場** にして、アプリらしく **動かす・消す・叩ける** ようにする。1画面のステージ（Chrome・VS Code 以外の36アプリ。ラスボス Terminal も）だけ。Chrome（横スクロール）と VS Code（ウィンドウ移動）は今のステージの作りのまま。

```json
"stage": {
  "hide_folders": true,
  "platforms": [
    {"id": "msg1", "x": 88, "y": 127, "w": 44, "h": 9,
     "skin": [ {"type": "rrect", "x": 0, "y": 0, "w": 44, "h": 9, "r": 2, "color": "#f4f0f6"} ],
     "behavior": {"type": "static"}}
  ]
}
```

- `x, y, w, h`：足場の絵の四角（背景と同じ 320×180 の座標）。**乗る面は上の辺（y）**。当たり判定は上の辺から少しだけ（すり抜け床：下からジャンプで抜けて、上に乗れる）。
- `skin`：足場の絵（部品は上の表と同じ。座標は足場の左上が 0,0）。省略すると白っぽい板。足場は背景とは別の絵になる（動く・消えるため）ので、**背景には同じ部品を描かない**（同じ場所に描くと二重に見える）。
- `hide_folders`：今のフォルダの足場2枚を消す（ふつうは true）。
- 書き出し：`Assets/Resources/AppBackgrounds/Parts/<ラベル>__<id>.png`（足場の絵）と `Stage_<ラベル>.json`（Unity が読む表。ワールド座標）。

### behavior（動き）

| type | キー | 何 |
|---|---|---|
| `static` | — | 動かない |
| `move` | dx, dy（ドット）, period（秒）, offset（秒） | 今の場所と (dx, dy) ずらした場所を往復（乗っているプレイヤーも一緒に動く）。再生バーのつまみ・スクロールバーなど |
| `blink` | on, off, warn（秒）, offset | 出ている on 秒 → 消える前に warn 秒点滅 → off 秒消える（消えている間はうすい形だけ）。カメラのオン・オフ、トグルの開閉など |
| `scroll` | speed（ドット/秒）, range（ドット）, dir（"up" か "down"）, offset | range だけ上（下）へ流れて、フェードして元の場所へ戻る（乗っているプレイヤーも動く）。チャットのメッセージが流れる・通知が積まれるなど |
| `fall` | delay（秒）, respawn（秒） | 乗って delay 秒でゆれて落ちる（消える）→ respawn 秒で戻る。消されるファイル・閉じるタブなど |
| `button` | effect, cooldown（秒） | **乗る足場ではなく、叩けるボタン**（プレイヤーの攻撃が当たると押せる）。effect：`clear_bullets`（画面の敵の弾を消す）・`stun_boss`（ボスを 1.5 秒ひるませる）・`refill`（能力のクールタイムを全部戻す）。押すと cooldown 秒は灰色で押せない。1アプリ1つまで |

### 決まり

- 足場は **8枚まで**（ボタンも数える）。上の面は y 60〜160、横は x 12〜308。幅 12 ドット以上。
- **フォルダの場所**：x 100〜115 と x 205〜220 の上、y 127 付近（±3）に、乗れる足場（ボタン以外）を置く（雑魚が最初に乗る場所。上下に動く・消える足場でもよいが、なるべく static か blink）。
- **届くこと**：ジャンプは約 48 ドット。床（y 172）かほかの足場から、46 ドット以内の高さに置く（render.py が調べて「注意」を出す）。
- ボス戦もこの足場で戦う。ボスの技・アプリのギミック（キットの StageRule が出す足場・置く物）とぶつからないように、そのアプリのコード（`Assets/Scripts/Kits/<アプリ>/`）を見て、置く場所・数を決める。とくに上の方（y 60〜90）はボスが浮く所なので、置きすぎない。
- 動く・消える足場は、遊ぶ人が分かる速さに（move・scroll はゆっくり。blink は warn を必ず付ける）。
