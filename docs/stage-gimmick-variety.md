# ステージのギミックの動きを増やす（2026-11-19）

まさぴ〜の依頼：「ステージギミックが上から落ちてくるやつばっかり。アプリに合わせて、下から出てきたり、いろんな方角から飛んできたり、プレイヤーに行動を促したり、見た目と動きのバリエーションをもっと増やして」。

今の様子
- 場面③の出来事は、アプリのキットのほとんど（Teams 以外）が自分の物を持っておらず、スキンの出来事（Rain＝上から降る・Sweep＝床の柱・Swarm＝上のタブから雑魚）になっている。
- キットのステージの決まり（StageRule）の危ない物も、上から落とす物が多い（FallingObject・Kit3DFaller・DropQuake など）。

やること
1. **共通の動きの部品 `KitMotion`**（下の表。Opus が `Assets/Scripts/Kits/Common/KitMotion*.cs` に作る）。
2. **アプリごとに、自分の場面③の出来事**（`StageEvents.Register(label, factory)`）を1つ作る。Chrome・VS Code・Premiere・Terminal は、今の出来事（AppStageEvents）があるので、足りない所だけ。
3. **ステージの決まりの危ない物の向き**：上から落ちる物ばかりのアプリは、アプリらしい別の向き・動きに変える（全部は変えない。重さで落ちるのが自然な物――Minecraft の砂・Unity の物体など――は残してよい）。
4. 見た目は各アプリの絵（`Kits/<App>/<App>Art.cs` など）と、ステージの背景（アプリの UI）に合わせる。

## 1. 動きの部品（KitMotion）

どれも：予告（赤・アプリの色の印）→ 動く → 消える。予告の長さは 0.6 秒以上（ボス戦は 0.8 秒以上）。`owner`（AttackRunner）を渡すとボスの出した物として 50％・100％で消える。出来事・決まりから使うときは `ctx.CanSpawnHazard` を見る（StageRule の決まり）。時間はスケールあり（Time.time）。見た目は `Sprite`（無ければ四角）と色を渡す。当たると 1 ダメージ（`damage` で変えられる）。

| 名前 | 何 | おもな値 |
|---|---|---|
| `Erupt` | **下から**：床（か足場の上）の x の範囲に、光る割れ目の予告 → 物（柱・アイコン・手など）が下からせり上がる → 少し止まる → 引っこむ | x, width, height, warn, rise, hold, sprite |
| `EdgeSweep` | **横から**：画面の左か右の端に矢印の予告（高さ y）→ 物が横切る（まっすぐ／波打つ）。count 個を gap 秒おき | side（L/R/Both）, y, speed, count, gap, wave（振れ幅） |
| `Diagonal` | **斜め・角から**：画面の角か端の点から、予告の線（その時のプレイヤーの場所へ）→ 飛んでくる | from（corner/edge の点）, speed, count, spread |
| `Orbit` | **回る**：点（ボス・足場・置いた物）の周りを、count 個が回る。すき間をくぐる | center, radius, speed, count, duration |
| `Bounce` | **跳ねる**：壁と床で跳ね返りながら進む（画面の中を何回か跳ねて消える） | from, dir, speed, bounces |
| `FloorWave` | **床を走る**：床の端から波（低い壁）が走ってくる → ジャンプで越える | side, speed, height |
| `WallCrawl` | **壁を這う**：壁・床・天井のふちに沿って進む | start（角）, dir（cw/ccw）, speed |
| `Pop` | **その場に出る**：予告の印の所に、奥から大きくなって出る（下から・横からではない「出現」）→ 少しして消える。弾をばらまく（ring）もできる | point, warn, life, ringCount |
| `Conveyor` | **床が動く**：床の一部が左右に流れて、上に立つ物を運ぶ（予告：矢印が流れる） | x, width, speed, duration |
| `HitTarget` | **行動を促す・叩く**：的が出る → 時間内に叩くと良いこと（ボスがひるむ・弾が消える・回復など）、叩かないと悪いこと（危ない物が出る・ボスが強くなる）。的は動いてもよい（Orbit・EdgeSweep で運ぶ） | point, life, onHit, onMiss |
| `Catch` | **行動を促す・取る**：良い物が飛んでくる（どの向きからでも）→ 触ると良いこと | from, path, speed, onCatch |
| `HoldZone` | **行動を促す・立つ**：場所の印 → その中に seconds 秒いると良いこと（出ると戻る） | x, width, seconds, onDone |
| `SafeSpot` | **行動を促す・逃げる**：ここだけ安全の印 → 少しして、それ以外に危ない物 | x, width, warn, then（Erupt/EdgeSweep など） |

組み合わせてよい（例：`Orbit` で回る `HitTarget`、`EdgeSweep` で流れてくる `Catch`）。

## 2. アプリごとの案

設計の担当（Fable）が、`docs/stage-gimmick-variety-a.md`・`-b.md`・`-c.md` に書く。1アプリごとに：
- **場面③の出来事**：名前・アプリの元ネタ・使う部品（上の表）と並べ方・秒数・数・予告・プレイヤーにしてほしい行動・見た目（どの絵・色）
- **ステージの決まりの直し**（あれば）：今の危ない物（どのファイルの何）→ どの部品・向きに変えるか
- 公平さ：雑魚の時間とボス戦での違い（ボス戦は小さく・逃げ場をふさがない）
