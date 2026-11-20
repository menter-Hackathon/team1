# ウィンドウのステージ と スキルを拾って入れ替える流れ（2026-11-20）

まさぴ〜の依頼（2026-11-20）：

> いくつかのステージを、ウィンドウをゲームステージにして欲しい。ワームをウィンドウの周りを這わせたり、ウィンドウが蝕まれている感じを演出してほしい。
> それぞれのステージをクリアした時に相手が落としたスキルを拾うと、そのスキルの内容が見れる画面が開き、手持ちのスキルとその場で入れ替えれるようにして。そこでスキルを選択し終わるとブラックアウトしてデスクトップ画面に戻るようにしてほしい。で、次のステージのアプリをクリックして入っていく流れにしてほしい。
> （追加）写真は Windows のようなウィンドウだが、実装は Mac の UI で。

この文書は設計だけ。実装は 4 つの担当（A〜D）に分ける（5章）。数値はすべて【仮】。Unity は担当が動かさない（まとめ役がビルドして確かめる）。

---

## 0. 決めたこと

| # | こと | 印 |
|---|---|---|
| 1 | いくつかのアプリの 1画面のステージを「ウィンドウのステージ」にする：デスクトップの壁紙の上に、そのアプリのウィンドウが 4枚重なって出て、ウィンドウの上の端が足場になる | 【確定】 |
| 2 | ウィンドウは Mac の見た目（角が丸い・明るいタイトルバー・左に信号機・まん中に題名）。オレンジの ✕ は出さない。`MacUi` の部品で描く | 【確定】 |
| 3 | 後ろはゲームの Mac のデスクトップの壁紙（`Assets/Resources/DesktopWallpaper.png`）。緑の丘は使わない | 【確定】 |
| 4 | ワームがウィンドウのまわり（上・横・下）を這う。ウィンドウ 1枚＝`AppPlatform` 1つにするので、`ZakoWorm` は直さずにそのまま回る | 【確定】 |
| 5 | ウィンドウが蝕まれる：ふちから紫が食いこむ・穴があく・ワームが通った所にかじり跡・横にずれる乱れ・題名の字がくずれる。ステージが進むほど増え、ボスを正常化すると 1.2秒で元に戻る | 【確定】 |
| 6 | ボスを正常化すると、追い出したウイルスが倒れた所にスキルが落ちる。歩いて触ると拾う（自動では手に入らない） | 【確定】 |
| 7 | 拾うと、ゲームが止まって「入れ替えの画面」が開く：新しいスキルの中身と、今の 3枠。空いた枠に入れる・持っているスキルと入れ替える・入れない、を選ぶ | 【確定】 |
| 8 | 選び終わると暗転 → デスクトップの画面。次のアプリを自分でクリックして入る（Dock のそのアプリが跳ねて知らせる） | 【確定】 |
| 9 | どのアプリをウィンドウのステージにするか：Slack・Discord・LINE・Messages・Mail・Notes の 6つ（表は `WindowStage.Labels`。1行で変えられる） | 【提案】 |
| 10 | ラベルの無い代用ボス（`AppBackgrounds.LabelFor(plan) == null`。今は色を変えた壁紙とフォルダ 2つだけ）の 1画面のステージも、ウィンドウのステージにする（こわす物が無い）。切るときは `WindowStage.AlsoUnlisted = false` | 【提案】 |
| 11 | 赤い信号機は飾り（クリックで閉じない）。閉じられると、ワームのレールと巣の付く所がステージの途中で無くなるため | 【提案】 |
| 12 | 穴・かじり跡は見た目だけ（足場の当たり判定は変えない）。タイトルバー（乗る面）には穴をあけない | 【提案】 |
| 13 | ウィンドウのステージでは、歩く枠のワームを 30% → 45%、同時にいられる数を 3 → 4 にする | 【提案】【仮】 |
| 14 | 壁紙は黒に近いので、ステージ用に 320×180 に小さくして少し明るくした絵を作る（敵は黒い。背景の決まり 1）。それでも見にくければ `WindowStage.UseMacWallpaper = false` で今の壁紙に戻せる | 【提案】 |
| 15 | 落ちたスキルは 6秒 拾わないとプレイヤーへ飛んで来る。12秒で必ず画面が開く。拾うまでプレイヤーはダメージを受けない | 【提案】【仮】 |
| 16 | 最後のステージ（Terminal）と、報酬の無いボスは、今までどおり（落とさない。少し待ってリザルト・次へ） | 【提案】 |
| 17 | もう持っているスキルが落ちたとき：画面は開く（中身は見られる）。入れ替えは無く「OK」だけ | 【提案】 |
| 18 | デスクトップでクリックできるのは、次のアプリだけ（順番はランが決めた順）。キーボード・ゲームパッドは Z・Enter で、今までの「矢印が動いてクリック」が流れる | 【提案】 |
| 19 | ウィンドウの大きさ・位置・蝕む速さ・時間 | 【仮】 |

---

## 1. ウィンドウのステージ

### 1-1. どのステージか

`WindowStage.Wants(StagePlan plan)` が true のステージだけ。ほかのステージは今のまま（アプリの UI の背景と足場）。

- `WindowStage.Enabled`（既定 true）が true
- ラスボスではない（`!plan.IsFinal`）
- 1画面のステージ：`plan.App.StageScene == "Stage_Desktop"`（さらに `AppStagePlatforms.Build` の中で、フォルダの足場 `Folder_Left`・`Folder_Right` があるときだけ作る）
- ラベル（`AppBackgrounds.LabelFor(plan)`）が表にある、または（`AlsoUnlisted` で）ラベルが null。**ラベルがあって表に無いアプリ（Finder・Premiere Pro・Zoom など）は、足場の表が無くてもウィンドウにしない**
- 確認用：`WindowStage.ForceAll = true` で、1画面のステージを全部ウィンドウにする

表【提案】（`WindowStage.Labels`。1つの配列。書き足す・消すだけ）：

| ラベル | 題名（`<アプリ> — <部屋>`。4枚分。大きいウィンドウから順に使う） | 中身の形 |
|---|---|---|
| Slack | `Slack — スレッド`／`Slack — 通知`／`Slack — DM`／`Slack — 検索` | chat |
| Discord | `Discord — 通知`／`Discord — DM`／`Discord — 通話`／`Discord — 検索` | chat |
| LINE | `LINE — トーク`／`LINE — 通知`／`LINE — 友だち`／`LINE — ノート` | chat |
| Messages | `メッセージ — 新規`／`メッセージ — 通知`／`メッセージ — 写真`／`メッセージ — 検索` | chat |
| Mail | `メール — 受信`／`メール — 新規`／`メール — 下書き`／`メール — 迷惑` | list |
| Notes | `メモ — すべて`／`メモ — 新規`／`メモ — フォルダ`／`メモ — 最近` | note |
| ラベルが null（代用ボス） | `<アプリの名前> — 1`〜`4`（名前は `plan.App.DisplayName`） | list |

題名は 12字まで（1字 7.2ドット＝`MacUi.CharPx`。幅 6.5マスのウィンドウに入るのは `(117 − 30) ÷ 7.2 = 12字`）。上の表はどれも 12字以内。

えらんだ理由：この 6つのキットの決まり（`SlackRule`・`TalkKitUtil.Platforms`・`MailRule`・`GimmickA.SteadyPlatforms` など）は、足場を「動かない `AppPlatform`／すり抜け床」として一般に探すだけで、`Stage_<ラベル>.json` の id には頼っていない（コードで確かめた）。**入れない物**：Zoom（`ZoomJoinRushEvent` が点滅する足場を使う）・Premiere Pro（`PremiereTimelineStage`）・Finder・Terminal・Chrome・VS Code（専用のステージ）。

題名は一般的な言葉だけ（本物のメッセージ・ロゴは出さない）。

### 1-2. 画面の大きさ（確かめた数）

- カメラ：まん中 (0, 5.5)、縦 12マス・横 21.33マス。床の上の面 y=0。左右の壁の内側 x=±10。上の 1.6マスは HUD の帯（使える高さは y ≤ 9.9）
- ボス部屋（`BattleArena`）は Rect(-10, 0, 20, 11.5)＝画面と同じ。ボス戦も同じウィンドウの上で戦う
- プレイヤーのジャンプ 3.2マス（`PlayerController._jumpHeight`）。段の差は 2.6 までにする
- ウィンドウの絵は 1マス 18ドット（`MacUi.Px`）。壁紙は 1マス 15ドット（今の背景と同じ）

### 1-3. ウィンドウの置き方（型 3つ）

1ステージ 4枚。大きい 2枚＋小さい 2枚（型 2 は 上に 1枚）。`型 = (ラベルの文字コードの合計 + Run.CurrentIndex) % 3`、さらに `合計 % 2 == 1` なら左右を反転（cx → -cx）。数は「まん中 cx・幅 w・上の端 top・高さ h」（マス）。

| 型 | id | cx | w | top | h | 下の端 | 役 |
|---|---|---|---|---|---|---|---|
| 1 ならび | A | -5.2 | 6.5 | 5.2 | 3.2 | 2.0 | 大 |
| | B | 4.6 | 7.0 | 5.6 | 3.6 | 2.0 | 大 |
| | C | -0.2 | 3.6 | 3.0 | 1.1 | 1.9 | 段 |
| | D | 7.6 | 3.2 | 3.0 | 1.1 | 1.9 | 段 |
| 2 かいだん（写真に近い） | A | -5.5 | 6.5 | 5.6 | 3.6 | 2.0 | 大 |
| | B | 4.8 | 7.0 | 5.0 | 3.0 | 2.0 | 大 |
| | C | -0.4 | 3.6 | 3.0 | 1.1 | 1.9 | 段 |
| | E | 0.2 | 4.5 | 7.6 | 2.0 | 5.6 | 上 |
| 3 まん中 | A | 0 | 7.0 | 5.4 | 3.4 | 2.0 | 大 |
| | C | -6.6 | 3.6 | 3.0 | 1.1 | 1.9 | 段 |
| | D | 6.6 | 3.6 | 3.0 | 1.1 | 1.9 | 段 |
| | E | -6.0 | 4.5 | 7.4 | 2.0 | 5.4 | 上 |

大きさはドットに合わせる：`wPx = Mathf.RoundToInt(w × 18)`、`hPx = Mathf.RoundToInt(h × 18)`、実際の大きさは `wPx ÷ 18`・`hPx ÷ 18`（3.6 → 65ドット＝3.61マス）。上の端 top はそのまま、まん中の y は `top − hPx ÷ 36`。当たり判定も、この合わせた大きさから作る。

守る決まり（数を変えるときも）：

1. 床 → 段（top 3.0）→ 大（top 5.0〜5.6）→ 上（top 7.4〜7.6）。どの段差も 2.6 以下。大きいウィンドウには、横にとなり合う段から登れること
2. 幅は 7.0 まで（`TalkKitUtil.Platforms` は、遊ぶ所の幅の半分＝約 10 以上の足場を数えない）
3. 下の端は床から 1.8 以上（下の面をワームが這う・巣が付く。プレイヤーが下をくぐれる）。巣が付くのは下の端が 1.8〜3.6・幅 2.5 以上（`VirusNests.FindUnderside`）＝ A・B・C・D。E には付かない
4. 左右の端は x=±9.3 より内側
5. 「まん中 x と 上の端 y」の組が、ほかのウィンドウと 0.3 以上ちがうこと（`AppPlatform.Covers` が見分ける）
6. ワームは床から 3.3 より上に 5秒いると落ちる（`ZakoWorm.HighLine`・`HighSeconds`）。`w + 2×(top − 3.3)` が 12 以下なら 1周できる（速さ 2.5マス/秒）。A・B は 1周する。E はずっと上なので、5秒 這ってから予告して落ちる（今の決まりのまま。`ZakoWorm.cs` は直さない）

重なり：リストの順に奥から描く（大 → 段 → 上）。大きい 2枚は少し重なってよい。いちばん手前の 1枚だけ信号機に色が付く（Mac の「前のウィンドウ」）。ほかは灰色（`active: false`）。赤い色は信号機の 3ドットだけ。

### 1-4. 足場としてのウィンドウ（`AppPlatform`）

ウィンドウ 1枚は、今のアプリの足場（`AppStagePlatforms.Spawn` の static）と同じ作り：

- GameObject `AppPlatform_Win<id>`、レイヤー `OneWayPlatform`、位置＝ウィンドウのまん中
- 子 `Art`（SpriteRenderer。**名前は必ず `Art`**。`ZakoWorm.AttachPlatformNow` と `VirusNests.FindUnderside` が `transform.Find("Art")` の bounds を読む）。`AttackRunner.AddVisual(go.transform, sprite, Color.white, size, order, "Art")`
- `BoxCollider2D`：幅 w、厚さ 0.3、上の端だけ（offset.y = h/2 − 0.15）。すり抜け床（下から上へ抜けられる）。横・下に当たりは無い
- `AppPlatform`（動かない物。`IsSteady` true）。`Setup(id, col, art, null, new AppStageParams())`

これで得られる物（コードで確かめた。直さない）：

- **ワーム**：歩く枠で出たワームがウィンドウの上に乗ると、`TryChooseRail` が `AppPlatform` を見つけて、`Art` の四角のまわり（上 → 横 → 下 → 横）を回る。巣から出たワームは下の面から回り始める
- **巣**：`VirusNests.FindSpot("Worm")` は、動かない足場の下の面を最初に探す → ウィンドウの下の面に巣が付く
- **雑魚の出る所**：`ZakoSpawner.TrySpawn` は上から下へ線を引いて、最初に当たった床・すり抜け床に出す → ウィンドウの上に出る。**ただし 4枚で横幅の 8〜9割をおおうので、そのままだと雑魚がほとんどウィンドウの上にしか出ない**（登らないと倒せず、ノルマに時間がかかる）。そこで `TrySpawn` の線の相手を、ウィンドウのステージでは 2回に 1回 床（Ground）だけにする（担当 A。5章）
- **拾える物・キットの決まり**：`AppStagePlatforms.RaycastSteady`・`IsSolidFor`・`GimmickA.SteadyPlatforms` が、動かない足場として数える
- フォルダの足場はかくす。シーンに置いてある最初の雑魚は `AppStagePlatforms.ReseatWave` が下の足場へ置き直す（今の仕組み）

描く順（sortingOrder）：ウィンドウ i（0〜3。奥から）は、影 `-8 + 3i`・`Art` `-7 + 3i`・題名の字 `-6 + 3i`。いちばん手前で 影 1・Art 2・字 3。感染のもや（-8）より前、巣（3）・キャラクター（11〜）より後ろ。

### 1-5. ウィンドウの絵（実行時に描く。Python は使わない）

1枚ごとに、自分だけの `Texture2D`（RGBA32・Point・ミップなし・w×18 × h×18 ドット）を作る。**`MacUi.WindowSprite` は使わない**（キャッシュされた絵を共有するので、蝕む係が書きこむとほかの所の絵まで変わる）。

1. `MacUi.StampWindow(put, wPx, hPx, look, null, 0, titleBar: 題名を字で出さないとき true, active: 手前の1枚だけ true)` で、ウィンドウを自分の格子に写す（`put` の y は上から。Texture2D は下からなので、y をひっくり返して入れる）
2. 中身（タイトルバーの下。`MacUi.HeaderH(hPx)` ドットより下）：
   - 左に、アプリの色の帯（幅＝ウィンドウの 22%。大きいウィンドウだけ）。色は `plan.App.Theme` の色を、白と 5:5 でまぜてくすませる（濃い面を作らない）
   - `chat`：行ごとに 3×3 の四角（顔の代わり。アプリの色・青・緑・だいだい を順に）＋ 灰色の棒（長さは乱数。高さ 2ドット、行の間 6ドット）。いちばん下に白い入力の枠（高さ 5ドット）
   - `list`：横の細い線で行を分け、行ごとに灰色の棒 2本（長い・短い）
   - `note`：左に細い行の一覧、右に長い棒が 4〜6本
   - 段（h 1.1＝20ドット）は中身 1行だけ（通知の板に見える）
   - 棒の色：`MacUi.Light4`（明るい見た目）。暗い見た目のアプリ（Discord。`MacLook.Dark`）は `MacUi.Gray2`
3. 題名：幅 80ドット以上のウィンドウは、`MacUi.Label(windowGo.transform, title, MacUi.DotToLocal(wPx, hPx, wPx*0.5f, 1f + th*0.5f), MacUi.TextH, MacUi.Ink(look), order, true)`（`th = MacUi.TitleHeightFor(hPx)`）。字が入らない（`!MacUi.FitsChars(title, wPx - 30)`）ときは「—」の後ろだけ（上のウィンドウ E は 81ドットなので 7字まで＝部屋の名前だけになる）、それも入らなければ字なし（StampWindow の灰色の棒）。段（65ドット）は 80ドットより小さいので、いつも字なし
4. 影：`MacUi.ShadowSprite(wPx, hPx)` を `MacUi.ShadowColor(look)` で、2ドット下へずらして置く（子 `Shadow`）
5. `Sprite.Create(tex, new Rect(0, 0, wPx, hPx), new Vector2(0.5f, 0.5f), 18f)`

明るい・暗い：Discord だけ `MacLook.Dark`、ほかは `MacLook.Light`（暗いウィンドウの面は `#1E1E1E` で敵の黒に近いので、Discord の中身の面は `MacUi.Gray4` で塗り直す）。

### 1-6. 壁紙

- 絵：`Assets/Resources/AppBackgrounds/Bg_MacDesktop.png`（320×180。1マス 15ドット。今の背景の絵と同じ大きさなので、`StageTheme.Apply(theme, appBackground)` がそのまま差し替える。`DesktopDecay` の感染ノイズも今までどおり重なる）
- 作り方：`Tools/art/appbg/mac_desktop.py`（PIL）。`Assets/Resources/DesktopWallpaper.png`（6000×3750）を、まん中で 16:9 に切る → 320×180 に小さく（LANCZOS）→ 明るさを上げる（各色 `c × 0.8 + 52`。元がほぼ黒なので、黒い敵が沈まないように）→ 保存
- `.meta`：`Bg_Slack.png.meta` を写して、`guid:` だけ新しい 32桁（`uuid.uuid4().hex`）にする（Read/Write 有効・Point・15ドット/マス・Sprite が要る。Unity に任せると既定の設定になって、`DesktopDecay` が動かない）。スクリプトが書く
- `WindowStage.Wallpaper()`＝`Resources.Load<Sprite>("AppBackgrounds/Bg_MacDesktop")`。無ければ null（今までの壁紙を色を変えて出す）
- `WindowStage.UseMacWallpaper = false` にすると null を返す（見にくかったときの逃げ道）
- 壁紙のまん中のステッカーの絵は、ほとんどウィンドウでかくれる。かくれない型（型 1 のまん中）でも、遊びのじゃまにはならない

デスクトップらしさ（余裕があれば）：右上のあいた所に `MacUi.Folder(14)` のフォルダを 2つ（order -9。字は無し）。

### 1-7. ウイルスの割り当て

`VirusRoster.Choose` の歩く枠で、`WindowStage.Active` のとき：W1 を「Worm」にして 45%、W2 は表のまま 15%（スス玉は 40%）。`Capped` のワームの上限を 4。表はさわらない。

### 1-8. 蝕まれるウィンドウ（`WindowCorrosion`）

色：紫 `#6A2A8C`・濃い紫 `#1E0830`・マゼンタ `#ED38FF`（感染のもや・デスクトップの画面と同じ）。赤は使わない。字の説明は出さない。

**蝕む強さ `level`（0〜1）**：目標へ 毎秒 0.05 で近づく（急に増やさない）。

| いつ | 目標 |
|---|---|
| 雑魚の時間 | `0.10 + 0.40 × EnemyWave.Progress`。`StagePacer.Current` があれば、場面の下限も見る：Intro 0.10／Build 0.22／Event 0.34／Rest 0.40／Climax 0.50 |
| ボス戦（`BossEntrance.IsTriggered`） | `0.50 + 0.45 × Boss.Gauge.Ratio` |
| ボスが正常化（`BossController.Normalized`） | 0 へ、1.2秒で（毎秒 0.05 ではなく、決まった時間で） |

**見た目**（ウィンドウごとの `Texture2D` を書きかえる。元の絵 `Clean` を持っているので、いつでも戻せる）：

| # | 何 | いつから | 作り方 |
|---|---|---|---|
| 1 | ふちから紫が食いこむ | 0.10 | 最初に 1回、ドットごとの「しきい値」を作る：`thr = ふちからの距離 ÷ 14ドット × 0.8 + 乱数 × 0.35`（乱数はウィンドウの id で種を決める）。`level > thr` のドットを紫に（3割は濃い紫、20ドットに 1つマゼンタ）。level が上がるほど内側へ進む |
| 2 | 穴があく | 0.35 | ウィンドウごとに 3〜5 個の中心（中身の面の中。乱数）。半径 `(level − 0.35) × 12` ドット（最大 7）。中は透明（壁紙が見える）、ふち 1ドットは濃い紫。**上の 9ドット（タイトルバー）には穴をあけない** |
| 3 | ワームのかじり跡 | いつも | 0.15秒ごとに、`ZakoWorm.Alive` のうち `RailKind == Rail.Platform` のワームについて、位置がウィンドウの四角のふちから 0.3マス以内なら、そのふちのドットに半円（半径 2〜3ドット）のかじり跡：透明にして、ふちを紫に。上の辺は透明にしないで紫にするだけ。1枚 60個まで。level が 0 に戻るまで残る |
| 4 | 横にずれる乱れ | 0.50 | 0.8〜2.0秒に 1回、どれか 1枚の、高さ 2〜4ドットの帯を、横へ 3〜6ドットずらして 0.08秒 見せて戻す（ずれた所のあいたドットはマゼンタ） |
| 5 | 題名の字がくずれる | 0.60 | 0.5秒ごとに、題名の 1〜3字を `# % & ?` のどれかに置きかえる（`StageWindow.Title.text`。太字の子 `Bold` の TextMesh も同じ字に）。色は `MacUi.SetLabelColor` で紫へ |
| 6 | 元に戻る | 正常化 | level が下がるにつれて、しきい値の大きい（内側の）ドットから `Clean` に戻る。戻った境目に白いドットを 0.1秒 光らせる。かじり跡・穴も消す。題名も元の字・元の色に |

書きかえは、level が 0.02 以上変わったとき・かじり跡が増えたとき・乱れのときだけ（`SetPixels32` + `Apply(false)`）。1枚 最大 126×65 ドットなので軽い。

時間が足りないときに切る順（後ろから切る）：1 → 3 → 6 → 2 → 5 → 4。1・3・6 があれば依頼（蝕まれる・ワームの跡・元に戻る）は満たす。

### 1-9. こわさない物

- ノルマ・`EnemyWave`・`StagePacer` の場面と出来事・キットの決まり（`StageRuleHost`）・ボス部屋：どれも足場を一般に探すだけなので、そのまま動く。足場を作るのは今と同じ `StageDirector.Awake` の中（ほかの部品の `Start` より先）
- シーン・エディターの作成ツールはさわらない（全部 実行時に作る）
- 表に無いアプリ・横スクロール（Chrome）・ウィンドウ移動（VS Code）・ラスボスは今のまま
- 感染したデスクトップの画面で開くウィンドウの中は、今のまま そのアプリの UI の絵（`AppBackgrounds.Find`）が出る。入ってからはデスクトップ＋ウィンドウ。気になるときは後で直す（`DesktopScreen` をさわるので、今回はしない）

---

## 2. スキルを拾う → 入れ替え → 暗転 → デスクトップ → クリック

### 2-1. 流れ（今との違い）

| | 今 | これから |
|---|---|---|
| 正常化の瞬間 | `BossController.Normalize` が `PlayerSkills.TryAcquire(reward)` を呼ぶ（すぐ手に入る。満杯なら入れ替え待ち） | 呼ばない（`AutoAcquireReward = false`）。スキルが落ちる |
| そのあと | 2.5秒 待って `GameSession.CompleteStage(reward)` | 拾う → 入れ替えの画面 → 決めたら `CompleteStage(reward)` |
| 暗転 | `GameSession.Transition` が暗転 → デスクトップ | 同じ（`GameSession.cs` は直さない） |
| デスクトップ | 矢印が自動で動いてクリック | 自分でクリック（Z・Enter でもよい） |

追い出したウイルスにとどめを刺す流れ（`Eject` → `OnEjectedVirusDied` → `Normalize`）は変えない。**そのウイルスが倒れた所にスキルが落ちる**（「相手が落とした」）。

落とさない場合（今までどおり 2.5秒 待って次へ）：報酬が null・ラスボス（`Plan.IsFinal`。次のステージが無い）・`RewardDrop.Enabled == false`。このときは `AutoAcquireReward` も true のまま。

図鑑・リザルト：`CompleteStage(reward)` には、入れても入れなくても、ボスの報酬をそのまま渡す（図鑑の「固有能力」と `ClearRecord.Reward` は「そのボスのスキル」なので）。

### 2-2. 落ちるスキル（`RewardDrop`）

- 出る所：`Boss.EjectedVirus` の位置（無ければ `Boss.BodyBounds.center`）。**位置は `Normalized` が来た瞬間（`Spawn` の中）に覚える**（そのあとウイルスは消えるかもしれない）。正常化の 0.5秒 後に、そこから上へはねて（速さ 6）、ボス部屋のまん中の方へ 1.5マス 寄った所の床・動かない足場の上へ落ちる（`AppStagePlatforms.RaycastSteady(…, Ground | OneWayPlatform)`。x は `BattleArena` の左右の端から 1.5 内側にとめる）。床から 0.7 の高さでふわふわ（上下 0.12・1.5回/秒）
- 見た目：白い角の丸い板（`MacUi.RoundRectSprite(22, 22, 3)`。ふちはアプリの色）に、スキルのアイコン（`reward.Icon`）。右下に、元のアプリの小さいアイコン（`Run.SourceIcon(reward)`）。まわりに白とアプリの色の小さい光が上へ流れる。描く順 14（`RewardKit.OrderPickup`）。赤は使わない
- 拾う：落ちてから 0.6秒 は拾えない（とどめの位置のまま、いきなり拾わないように）。そのあと、プレイヤーの体との距離が 0.7 以内で拾う（`Pickup` と同じく、毎フレーム自分で距離を見る）。音 `skill_pickup`
- 取りこぼさない：出てから 6秒 拾わなければ、プレイヤーへ飛んで来る（毎秒 8マス。地形は無視）。12秒 たったら、その場で拾ったことにする
- 拾うまでの間、プレイヤーはダメージを受けない：`Health.cs` に `public static bool RewardGrace { get; set; }` を足し、`TryTakeDamage` の `DebugInvincible` の行の次に `if (RewardGrace && _team == Team.Player) return false;`（点滅しない。`Health.DebugInvincible` は開発ビルドで「無敵（P）」の札が出るので使わない）。`RewardDrop` が出たとき true、画面を閉じた・`RewardDrop` が消えた（`OnDestroy`）・`Cancel` で必ず false。`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` でも false に。それでもライフ 0 になったら（落下など）、今までどおりゲームオーバー（`RewardDrop.Cancel()` で落ちたスキルを消す）
- 今ある `SkillPickup`（触るとすぐ手に入る。テスト用）と `Pickup`（回復・シールド。ボスの登場で消える）は使わない・直さない

### 2-3. 入れ替えの画面（`SkillSwapScreen`）

拾った瞬間：`Time.timeScale = 0`、`MousePointer.ShowCursor()`、`PlayerController.CanControl = false`（`PlayerSkills` の X 長押しの「捨てる」は止まっていても数えるので、必ず切る）。OnGUI（`PixelGui.Begin(false)`・`GUI.depth = -400`＝HUD より手前、デスクトップ -500・暗転 -1000 より奥）。

画面（640×360 の仮想の画面。Mac のウィンドウ。図鑑のクイックルック `AppDexMenu.DrawQuickLook` と同じ部品）：

```
┌ ●●●            あたらしいスキル                    ┐
│ ┌────┐  スキルの名前（16）                          │
│ │ icon │  1行の説明（12・灰）  SkillBriefs.Line        │
│ │  48  │  [届く所の図] [絵文字の札…]                  │
│ └────┘  長い説明（12・2行まで） SkillTexts.Description │
│ ───────────────────────────── │
│ ┌ 1 ───┐ ┌ 2 ───┐ ┌ 3 ───┐ ┌───┐          │
│ │ icon 32 │ │ icon 32 │ │   ＋    │ │ ×  │          │
│ │ 名前    │ │ 名前    │ │ (あき)  │ │いれ │          │
│ │ 札      │ │ 札      │ │         │ │ない │  [決定]  │
│ └────┘ └────┘ └────┘ └───┘          │
│ いま：<選んでいる枠のスキルの名前> — <その1行の説明>    │
└─────────────────────────────┘
```

- 後ろ：画面全体を `PixelGui.Fill(…, new Color(0.05f, 0.07f, 0.12f, 0.45f))` で暗く
- ウィンドウ：`MacDesktopGui.CenteredWindow(W, H, 460, 264, false)` → `MacDesktopGui.Window(rect, "あたらしいスキル", out action)`（信号機を押しても何もしない）
- 上：`MacDesktopGui.Card` の枠にアイコン（`PixelGui.DrawSprite`）、右下に元のアプリのアイコン 16。名前 `PixelGui.TextFit(…, 16, MacDesktopGui.Ink)`、1行 `SkillBriefs.Line(reward)`（12・`MacDesktopGui.Muted`）、`PixelGui.ReachMap(x, y, SkillBriefs.Reach(reward.DisplayName))`、`PixelGui.PictoTags(x + 64, y + 2, SkillBriefs.Pictos(reward), 幅)`、長い説明 `PixelGui.Text(rect, SkillTexts.Description(reward), 12, PixelGui.BodyText, TextAnchor.UpperLeft, true)`（高さ 28 の枠で切る。空なら出さない）
- 下：えらぶ物は 4つ＝枠 1・2・3 と「いれない」。枠のカード（幅 100・高さ 86）：左上に番号、アイコン 32、名前（12）、札（`PictoTags`）。あいた枠は点線のふち（`PixelGui.DashedBorder`）と「＋」。えらんでいるカードは `MacDesktopGui.Card(r, true)`、その枠のアイコンを半分うすくして、右上に新しいスキルのアイコン（20）を重ねる（「これが入る」）。字は足さない
- いちばん下の 1行：えらんでいる枠にスキルがあれば「いま：名前 — 1行の説明」（比べるため）。あいた枠・「いれない」のときは出さない
- 最初にえらんでいる所：あいた枠があれば、いちばん前のあいた枠。無ければ枠 1

操作（`Keyboard.current`・`Gamepad.current`・マウスを直接読む。`PlayerInputReader` はさわらない。開いて 0.35秒 は受けつけない＝攻撃ボタンの連打で決まらないように。時間は unscaledTime）：

| 入力 | すること |
|---|---|
| ← →（A・D、十字キー、左スティック） | えらぶ所を動かす（端で止まる）。音 `ui_move` |
| 1・2・3 | その枠をえらぶ |
| Z・Enter・Space（ゲームパッド 下のボタン） | 決める。音 `ui_decide` |
| X・Esc（ゲームパッド 右のボタン） | 「いれない」をえらぶ（すぐには閉じない。もう一度 決めるボタンで閉じる）。音 `ui_cancel` |
| マウス | カードをクリック＝えらぶ。えらんでいるカードをもう一度クリック、または「決定」ボタン（`MacDesktopGui.Button(r, "決定", true, true)`）＝決める |

決めたとき（`SkillLoadout loadout = skills.Loadout`。これは `RunState.Loadout` そのもの＝次のステージに残る）：

```csharp
// 枠 i に入れる（あいていても、入っていても同じ。SkillLoadout に「i 番へ入れる」は無いので、写して戻す）
SkillDefinition[] s = loadout.Snapshot();
s[i] = reward;
loadout.Restore(s);          // 変わった枠だけ SlotChanged（Reason＝Loaded）が出る。HUD・パッシブはこれで直る
// いれない：何もしない
```

- `TryAcquire`・入れ替え待ち（`Pending`）は使わない（HUD の入れ替え待ちの点滅は出ない）
- もう持っているスキル（`loadout.Contains(reward)`）：カードは見せるが えらべない。持っている枠に ✓ の印。「OK」ボタンだけ（Z・Enter・クリックで閉じる）
- 次のステージに入るとき `GameSession.LoadCurrentStage` が `Run.StageStartSlots = Run.Loadout.Snapshot()` を取り直すので、リトライしても入れ替えた後の中身に戻る

閉じるとき：0.3秒、新しいアイコンがその枠へ入る動き（いれない：アイコンがうすくなって消える）→ `PlayerController.CanControl` は戻さない（もう暗転するので）→ 呼んだ側の `onClosed` → `StageDirector` が `GameSession.CompleteStage(reward)`。`CompleteStage` → `Transition` が `Time.timeScale = 1` に戻して暗転 → デスクトップ（今の仕組み）。

### 2-4. デスクトップで自分でクリック（`DesktopScreen`。足すだけ）

今：`TargetStart`（元に戻る所を見せた後）から、矢印が `CursorTime` で動き、`TargetStart + CursorTime` にクリック → 跳ねる → 開く。Z・クリックで早送り。

これから（`DesktopScreen.ManualClick == true` のとき。既定 true）：

1. `TargetStart` までは今と同じ（クリアしたアプリが元に戻る。ラスボスは Dock に現れる）。この間の Z・クリックは、元に戻る所の早送りだけ（今の `Skip` の前半）
2. `TargetStart` から「待ち」（始めに `MousePointer.ShowCursor()`）：次のアプリのアイコンが、1.6秒ごとに 2回 小さく跳ねる（高さ 6ドット。Mac の Dock の「見てほしい」の跳ね方）。矢印は描かない（本物のマウスの矢印を使う）。マウスがアイコンの上にあるときは、アイコンを 32 → 38 に大きくする（Dock の拡大）
3. クリック：マウスの左ボタンが、次のアプリのアイコン（元の位置の四角を 6ドット 広げた範囲）の上で押されたら、その時刻を「クリックした時刻」にする → 今と同じ：跳ねる（`BounceTime`）→ ウィンドウが開く（`OpenTime`）→ ステージへ
4. Z・Enter・Space・ゲームパッドの下のボタン：今までの自動の動き（画面のまん中から矢印が動いて `CursorTime` 後にクリック）を、その時から始める。「クリックした時刻」＝今＋`CursorTime`
5. ほかの所・ほかのアプリをクリックしても何もしない（次のアプリだけ。順番はランの順）
6. 最後のステージの後（`_target < 0`）は今のまま（全部戻ったデスクトップを見せて、自動でリザルトへ。Z・クリックで早送り）
7. 能力えらびの後の最初のステージに入るときも同じ（自分でクリック）

直し方（今の式を 1つのプロパティにまとめるだけ）：

```csharp
public static bool ManualClick = true;            // false＝今までどおり自動
float _clickAt = -1f;                             // クリックした時刻（T。-1＝まだ）
float _autoFrom = -1f;                            // Z で矢印が動き始めた時刻（T。-1＝矢印なし）
Rect _targetHome;                                 // 次のアプリのアイコンの元の位置（OnGUI の DrawDock が毎回入れる。GUI の座標）

float ClickAt => !ManualClick ? TargetStart + CursorTime : _clickAt >= 0f ? _clickAt : float.PositiveInfinity;
public bool IsWaitingForClick => _visible && !IsDone && ManualClick && _target >= 0 && _openAt < 0f && _clickAt < 0f && T >= TargetStart;
public void ClickTarget() { if (IsWaitingForClick) _clickAt = T; }   // 確認用・ゲームパッド用の口
```

- 今 `TargetStart + CursorTime` と書いてある 4か所（`Update` の開く判定・`PlaySounds` の `desktop_click`・`DrawDock` の `clickAt`・`DrawCursor` の `sinceClick`）を `ClickAt` に
- `DrawCursor` は `!ManualClick || _autoFrom >= 0f` のときだけ描く。動き始めは `ManualClick ? _autoFrom : TargetStart`
- `Update` の `if (T >= InputGuard && Pressed()) Skip();` を、`ManualClick` のときは `OnPress()` に：
  - `T < TargetStart`：`_restored >= 0 && T < RestoreAt` なら `_shownAt = Time.unscaledTime - RestoreAt`（今の早送りの前半と同じ）。ほかは何もしない
  - `_target < 0`：`IsDone = true`（今と同じ）
  - もう `_clickAt >= 0`：何もしない
  - キー・ゲームパッドで押された：`_autoFrom = T; _clickAt = T + CursorTime;`
  - マウスで押された：マウスの位置（`Mouse.current.position` を `x / PixelGui.Scale`、`(Screen.height − y) / PixelGui.Scale` に直す）が `_targetHome` を 6 広げた中なら `_clickAt = T`
- `Show` で `_clickAt = -1f; _autoFrom = -1f;`
- 待ちの跳ね方・拡大は `DrawDock` の `isTarget` の所に足す（`IsWaitingForClick` のときだけ）
- `Skip` は残す（`ManualClick == false` のとき用）

---

## 3. 危ない所（コードを読んで見つけた物）

1. **壁紙が黒い**：`DesktopWallpaper.png` はほぼ黒で、敵（黒）が沈む。小さくした絵を明るくする（1-6）・敵はたいてい明るいウィンドウの前にいる、で和らげる。ビルドの絵で必ず見る。だめなら `UseMacWallpaper = false`
2. **`DesktopWallpaper.png` はそのままでは使えない**：テクスチャの種類が Default（Sprite ではない）・Read/Write 無効。`Resources.Load<Sprite>` は null、`DesktopDecay` は読めない。だから別の絵 `Bg_MacDesktop.png` と、写した `.meta` が要る
3. **`MacUi.WindowSprite` の絵は共有**：蝕む係が書きこむと、ほかの Mac のウィンドウ（技・通知）まで紫になる。ウィンドウごとに自分の `Texture2D` を作る（1-5）
4. **`Art` という名前**：ワームと巣は子の名前で探す。`MacUi.PlaceWindow`（子は `MacWindow/Body`）をそのまま使うと、ワームが乗らない
5. **拾う前に倒れる**：今は正常化で `StageDirector._ending = true` になり、そのあとのライフ 0 は無視される。落ちたスキルを取りに行く間は `_ending` を立てない（倒れたら今までどおりゲームオーバー）＋ダメージを止める（2-2）。これで止まらない
6. **X 長押し**：`PlayerSkills.Update` は止まっている間も長押しを数えて、えらんでいる枠を捨てる。入れ替えの画面の間は `CanControl = false`
7. **`BossController.Normalize` の自動で手に入る所**：テスト用の 3つのシーン（`BossTestDirector`・`StageTestDirector`・`OfficeUnitySteamDebugDirector`）は今のまま動くように、フラグ（既定 true）で止める。`StageDirector` だけが false にする
8. **確認用のプログラム**：今の確認は、デスクトップが自動で進むことに頼っている。`DesktopScreen.ManualClick = false` にするか、`IsWaitingForClick` を見て `ClickTarget()` を呼ぶ
9. **`Health.DebugInvincible` は使わない**：開発ビルドでは「無敵（P）」の札が画面に出る（`DebugInvincibility.OnGUI`）。`Health.RewardGrace` を 1つ足す（2-2）
10. **ヒットストップ**：`HitStop` は `Time.timeScale` が自分の値でなくなると手を引くので、入れ替えの画面の 0 とぶつからない（確かめた）
11. **ワームの上限**：上のウィンドウ（E）では 5秒で落ちる。大きいウィンドウは `w + 2×(top − 3.3) ≤ 12` を守れば 1周する。遅くなる効果（`GameBuffs.ZakoSpeed < 1`）の間は途中で落ちる（今の決まりのとおり）
12. **reki-su・hiroto-master のファイル**：`DesktopScreen.cs` は足すだけ（2-4）。`RunTitleMenu.cs`・`RunLoadoutMenu.cs`・`AppDexMenu.cs`・`MacDesktopFiles.cs`・`GameOverScreen.cs`・`RunResultScreen.cs`・`PlayerInputReader.cs`・`GameSession.cs`・シーン・エディターの作成ツールは、どの担当もさわらない
13. **雑魚がウィンドウの上にかたよる**：1-4 のとおり。`ZakoSpawner` の 1行で半分を床に出す。ビルドで、ノルマを倒し切る時間が今のステージと大きく変わらないか見る
14. **`Restore` で入れると `AcquireAttempted` は出ない**：聞いているのは確認用の `SkillSlotsDebugLabel` だけ（確かめた）。HUD・パッシブは `SlotChanged` で直る
15. **マウスの矢印**：ゲームは矢印をかくしていない（`Cursor.visible` を false にする所は無い）。念のため、入れ替えの画面とデスクトップの待ちの始めに `MousePointer.ShowCursor()` を呼ぶ
16. **描く順**：ウィンドウは -7〜2。キットの飾り・置く物（`KitProp` など）がウィンドウの後ろにかくれないか、ビルドの絵で見る（かくれたら、ウィンドウの順を全体に下げる。もやの -8 より前には保つ）

---

## 4. 担当どうしの約束（A が作り、B が使う）

B は A のコードを待たずに書けるように、形をここで決める。**名前・型を変えない**。

```csharp
// Assets/Scripts/Stage/WindowStage.cs（担当 A）
public static class WindowStage
{
    public static bool Enabled = true;
    public static bool AlsoUnlisted = true;       // ラベルが null のアプリ（代用ボス）もウィンドウにする
    public static bool UseMacWallpaper = true;
    public static bool ForceAll;                  // 確認用：1画面のステージを全部ウィンドウに
    public static int ForceTemplate = -1;         // 確認用：型（0〜2）
    public static readonly string[] Labels = { "Slack", "Discord", "LINE", "Messages", "Mail", "Notes" };

    public static bool Active { get; }            // 今のステージがウィンドウのステージか
    public static IReadOnlyList<StageWindow> Windows { get; }   // 奥から順。無ければ空
    public static event Action Built;             // ウィンドウを作り終えた直後（StageDirector.Awake の中）。ステージごとに 1回

    public static bool Wants(StagePlan plan);
    public static Sprite Wallpaper();
    internal static int Build(StagePlan plan, Transform parent);   // AppStagePlatforms.Build から呼ぶ。作った数
}

public sealed class StageWindow
{
    public string Id;                 // "A"〜"E"
    public AppPlatform Platform;
    public SpriteRenderer Art;        // 子 "Art"
    public Texture2D Texture;         // このウィンドウだけの絵（RGBA32・Point・読み書きできる）。B が書きかえる
    public Color32[] Clean;           // 元の絵（Texture.GetPixels32 と同じ並び＝下の行から。長さ W×H）。書きかえない
    public int W, H;                  // ドット（18ドット＝1マス）
    public int TitleH;                // 上から何ドットがタイトルバーか（ふち・下の線こみ＝MacUi.HeaderH(H)）。行 y >= H − TitleH がタイトルバー
    public Rect World;                // Art の四角（ワールド座標。動かない）
    public TextMesh Title;            // 題名の字（無ければ null）
    public string TitleText;          // 元の題名
    public Color TitleColor;          // 元の字の色
    public bool Front;                // いちばん手前か
}
```

- `Built` は静的なイベント。A は `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` で `Built = null`・`Windows` を空に・`Active = false` に戻す。`Build` の最初にも前のステージの分を空にする
- B は `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` で `WindowStage.Built += …` を付け、呼ばれたら `new GameObject("WindowCorrosion").AddComponent<WindowCorrosion>()`（シーンといっしょに消える）。`StageDirector`（`FindObjectOfType`）の `Boss`・`Wave`・`Entrance` は `Start` で取る（`Awake` の途中ではまだ無い）
- ワールドの点 → ドット：`px = (x − World.xMin) × 18`、`py = (y − World.yMin) × 18`（下から）

---

## 5. 担当の分け方（ファイルは重ならない）

### 担当 A：ウィンドウのステージ（足場・絵・壁紙・割り当て）

作る：
- `Assets/Scripts/Stage/WindowStage.cs`（4章の形。`StageWindow` も同じファイル）
- `Tools/art/appbg/mac_desktop.py`、それで作った `Assets/Resources/AppBackgrounds/Bg_MacDesktop.png` と `.meta`（1-6。スクリプトを実行して、絵を目で見ること）

直す：
- `Assets/Scripts/Stage/AppStagePlatforms.cs`：`Build` の中、`left`・`right` を見つけた後・`label` を決める前に

  ```csharp
  if (WindowStage.Wants(plan))
  {
      int made = WindowStage.Build(plan, left.transform.parent);
      if (made > 0)
      {
          left.SetActive(false); right.SetActive(false);
          Physics2D.SyncTransforms();
          try { ReseatWave(director.Wave); } catch (Exception e) { Debug.LogException(e); }
          Active = true; Label = AppBackgrounds.LabelFor(plan) ?? "Window";
          return made;
      }
  }
  ```
  と、足場 1つを作る口 `internal static AppPlatform SpawnWindow(string id, Vector2 center, Vector2 size, Sprite sprite, int order, Transform parent)`（1-4 のとおり。`Spawn` の static と同じ作り。絵の順番だけ引数）
- `Assets/Scripts/Visual/AppBackgrounds.cs`：`ForStage` の頭に

  ```csharp
  if (WindowStage.Wants(plan))
  {
      Sprite w = WindowStage.Wallpaper();
      if (w != null) { Current = w; CurrentLabel = LabelFor(plan); return w; }
  }
  ```
  （`Find` は変えない）
- `Assets/Scripts/Enemy/Virus/VirusRoster.cs`：1-7
- `Assets/Scripts/Stage/ZakoSpawner.cs`：`TrySpawn` の `Physics2D.Raycast(from, Vector2.down, view.height, _groundMask)` の相手を、`WindowStage.Active && Random.value < 0.5f` のとき `LayerMask.GetMask("Ground")` に（1行。ほかは変えない）

確かめる（ビルドの絵で）：
1. Slack のステージ：壁紙が Mac のデスクトップ（明るくした物）。Mac のウィンドウが 4枚（丸い角・左に信号機・まん中に題名「Slack — …」）。オレンジの ✕ は無い。フォルダの足場は無い
2. プレイヤーが 床 → 段 → 大きいウィンドウ → （型 2・3 は）上のウィンドウ へ登れる
3. ワームがウィンドウの上 → 横 → 下 → 横 と 1周する。ワームの巣がウィンドウの下の面に付く
4. 黒い敵が、壁紙の前でも見える
5. Zoom・Chrome・VS Code・ラスボスは今までどおり
6. ノルマを倒すとボスが出る。Slack の決まり（未読の板）が今までどおり出て、ウィンドウの後ろにかくれない
7. 雑魚が床にもウィンドウの上にも出る（床にいる雑魚が 3〜6割）

### 担当 B：蝕まれるウィンドウ

作る：`Assets/Scripts/Visual/WindowCorrosion.cs` だけ（1-8。4章の形を使う）。ほかのファイルは直さない。

外から見える物：

```csharp
public class WindowCorrosion : MonoBehaviour
{
    public static bool Enabled = true;
    public static float ForceLevel = -1f;          // 確認用：0〜1 を入れると、その強さに固定
    public static WindowCorrosion Current { get; }
    public float Level { get; }                    // 今の強さ
    public int BiteCount { get; }                  // かじり跡の数（全部のウィンドウの合計）
}
```

確かめる：
1. 始まってすぐ：ふちに少しだけ紫（level 0.10）
2. 雑魚を倒すほど紫が内側へ進む。level 0.35 から穴があいて、壁紙が見える。タイトルバーには穴が無い
3. ワームが這った所に、かじり跡が並ぶ
4. ボス戦：ゲージが上がるほどひどくなる。乱れの帯・題名の字のくずれが出る
5. 正常化：1.2秒で全部 元の絵に戻る（`Level == 0`、絵が `Clean` と同じ）
6. `ForceLevel = 0.2 / 0.5 / 0.9` の絵を 1枚ずつ。赤い色が無いこと。ほかの Mac のウィンドウ（ボスの技の通知など）が紫になっていないこと
7. 例外が出ても、ステージは止まらない（`Update` を try で囲み、1回ログを出して自分を止める）

### 担当 C：スキルが落ちる・拾う・入れ替えの画面

作る：
- `Assets/Scripts/Skills/RewardDrop.cs`（2-2）
- `Assets/Scripts/Dev/SkillSwapScreen.cs`（2-3）

直す：
- `Assets/Scripts/Core/Damage/Health.cs`：`RewardGrace`（2-2。足すのは 2行）
- `Assets/Scripts/Boss/BossController.cs`：`public bool AutoAcquireReward { get; set; } = true;` を足し、`Normalize` の `if (reward != null)` を `if (reward != null && AutoAcquireReward)` に。ほかは変えない
- `Assets/Scripts/Run/StageDirector.cs`：
  - `Awake`：ボスを作った後 `Boss.AutoAcquireReward = !UsesDrop;`（`bool UsesDrop => RewardDrop.Enabled && !Plan.IsFinal && Plan.Profile != null && Plan.Profile.Reward != null && _player != null;`）
  - `OnBossNormalized(reward)`：`UsesDrop && reward != null` なら、`_ending` は立てずに `RewardDrop.Spawn(Boss, _player, reward, _session.Run.SourceIcon(reward), Plan.App.Theme, onDone)` を 1回だけ。`onDone`＝`if (_ending) return; _ending = true; _session.CompleteStage(reward);`。ちがえば今のまま（2.5秒 待って `CompleteStage`）
  - `OnPlayerDied`：落ちたスキルがあれば `RewardDrop.Cancel()` してから今のまま

外から見える物：

```csharp
public class RewardDrop : MonoBehaviour
{
    public static bool Enabled = true;
    public static RewardDrop Current { get; }      // 落ちている間だけ
    public SkillDefinition Skill { get; }
    public bool CanPick { get; }                   // 拾える（0.6秒 たった）
    public static RewardDrop Spawn(BossController boss, PlayerSkills player, SkillDefinition reward, Sprite appIcon, AppTheme theme, Action onDone);
    public void PickNow();                         // 確認用：触ったことにする（画面が開く）
    public static void Cancel();
}

public class SkillSwapScreen : MonoBehaviour
{
    public static SkillSwapScreen Current { get; } // 開いている間だけ
    public static SkillSwapScreen Open(SkillDefinition reward, PlayerSkills skills, Sprite appIcon, Action onClosed);
    public int Selected { get; }                   // 0〜2＝枠、3＝いれない
    public void Select(int index);                 // 確認用
    public void Confirm();                         // 確認用：決める（0.35秒 の待ちは無視）
}
```

確かめる：
1. 追い出したウイルスを倒す → その所からスキルの板が出て、床の上でふわふわする。HUD の枠はまだ変わらない
2. 6秒 放っておくと、プレイヤーへ飛んで来て画面が開く
3. 触る → ゲームが止まり、画面が開く：アイコン・名前・1行の説明・届く所の図・札・長い説明、下に 3枠と「いれない」
4. あいた枠をえらんで決める → `Run.Loadout.Get(i) == reward`。入っている枠 → 入れ替わる。「いれない」→ 変わらない
5. 決めた後：暗転 → デスクトップ。`Run.Cleared` の最後の `Reward` と、図鑑のそのアプリのスキルは、ボスの報酬（入れなくても）
6. 次のステージで、えらんだ中身のまま。そこでリトライしても同じ
7. ラスボス：スキルは落ちず、今までどおりリザルトへ
8. もう持っているスキル：画面は「OK」だけ
9. `BossTest`・`StageTest` のシーンは、今までどおりすぐ手に入る

### 担当 D：デスクトップで自分でクリック

直す：`Assets/Scripts/Dev/DesktopScreen.cs` だけ（2-4。足すだけ。reki-su の描き方・並びは変えない）。

確かめる：
1. クリアの後：アプリが元に戻る → 次のアプリが跳ねて待つ。何もしなければ、ずっと待つ（`IsWaitingForClick == true`）
2. 次のアプリをクリック → 跳ねる → ウィンドウが開く → ステージ
3. ほかの所をクリックしても何も起きない
4. Z・Enter → 矢印が動いてクリック → 開く
5. 元に戻る途中のクリック・Z は、早送りだけ（開かない）
6. ラスボス：Dock に現れてから待つ
7. 最後のクリアの後：今までどおり自動でリザルトへ
8. `ManualClick = false`：今までと同じ動き

### 順番

A・B・C・D は同時に始められる（B は 4章の形だけで書ける。コンパイルは A が入ってから）。まとめるときは A → B、C、D の順でコンパイルを見る。

---

## 6. 確認用のプログラムがすること（ビルドしたアプリで。OnGUI はバッチでは写らない）

前じたく：`~/Library/Application Support/DefaultCompany/Team1/app_dex.json` を別の所へ写しておき、終わったら戻す（`CompleteStage` が図鑑を書く）。`Assets/__ProbeTests` は終わったら消す。

1. **ウィンドウのステージ**：`GameSession.StageFilter`（`BossSelectDebug` と同じ入口）で Slack と もう 1つのアプリにしぼってランを始める（`SampleUsageProvider`・`AutoPickLoadout`。次のステージへ進む所まで見るので 2つ以上）。しぼれないときは `WindowStage.ForceAll = true`。デスクトップは `ClickTarget()` で進む。ステージで：`WindowStage.Active`・`Windows.Count == 4`・全部の `Platform.IsSolidNow`・`Art` があるか。絵 1枚
2. **型**：`ForceTemplate = 0 / 1 / 2` で 1枚ずつ
3. **ワーム**：`VirusRoster.ForceType = "Worm"`。10秒 回して、`ZakoWorm.Alive` の中に `RailKind == Rail.Platform` で `SurfaceNormal.y < -0.5`（下の面）の物が 1回でもいたか。`VirusNest.All` に、ウィンドウの下の面の巣があるか。絵 1枚
4. **蝕む**：`WindowCorrosion.ForceLevel = 0.2 / 0.5 / 0.9` の絵。`BiteCount > 0`。`ForceLevel = -1` に戻す
5. **ボスまで**：雑魚を全部倒す（`Health` を直接 0 に）→ ボスのゲージを 100% に → 追い出したウイルスを倒す
6. **落ちる**：`RewardDrop.Current != null`・`Run.Loadout` がまだ変わっていない・`WindowCorrosion.Current.Level` が 1.5秒 後に 0。絵 1枚
7. **拾う**：`RewardDrop.Current.PickNow()` → `SkillSwapScreen.Current != null`・`Time.timeScale == 0`。絵 1枚（画面）
8. **えらぶ**：`Select(1)` の絵 → `Confirm()` → `Run.Loadout.Get(1) == reward`。別の回で `Select(3)`（いれない）→ 変わらない
9. **デスクトップ**：暗転の後 `GameSession.IsOnDesktop`。3秒 待っても `IsWaitingForClick` のまま（自動で進まない）。絵 1枚 → `ClickTarget()` → 次のステージが始まる（`GameSession.StageStarted`）。次のステージの `PlayerSkills.GetSkill(1) == reward`
10. **放っておく**：6 の後 何もしないで 7秒 → 画面が開いている
11. **ラスボス**：最後のステージで正常化 → `RewardDrop.Current == null` のままリザルトへ
12. **今までのステージ**：`WindowStage.Enabled = false` で Slack → 今までのアプリの UI の背景と足場

## 7. 2回目の直し：大きく重なるウィンドウ と ウイルスから広がる汚染（2026-11-20）

【確定】（まさぴ〜さん）
- ウィンドウをもっと大きくする。ウィンドウどうしは重なってよい
- 蝕まれるのは、ワーム・マイナー・ルートキットなどが**いる場所から**、ウィンドウの内側へ**だんだん**広がる
- 見た目は macOS のまま（角丸・左に信号機・まん中に題名）

この章が置きかえる物：1-3 の表と決まり 2・6、1-8 の表の 1（ふちから紫）・2（決まった穴）・6（戻り方）と「切る順」、6章の 2・3・4。ほかは今のまま（7-4）。数は全部【提案】。

### 7-1. 置き方（型 3つ。4枚のまま）

数は「まん中 cx・幅 w・上の端 top・高さ h」（マス）。i は描く順（0＝いちばん奥）。左右反転（cx → -cx）は今のまま。

| 型 | i | id | cx | w | top | h | 下の端 | ドット | 役（Big） |
|---|---|---|---|---|---|---|---|---|---|
| 0 カスケード | 0 | A | -3.4 | 9.0 | 7.6 | 5.0 | 2.6 | 162×90 | 大・奥（true） |
| | 1 | B | 3.6 | 9.0 | 5.6 | 3.8 | 1.82 | 162×68 | 大（true） |
| | 2 | C | -5.6 | 5.4 | 3.0 | 1.1 | 1.89 | 97×20 | 段・わき道 |
| | 3 | D | 1.0 | 4.5 | 3.0 | 1.0 | 2.0 | 81×18 | 段・登り口 |
| 1 山 | 0 | A | 0 | 9.0 | 7.4 | 4.8 | 2.6 | 162×86 | 大・奥（true） |
| | 1 | B | -5.6 | 7.0 | 5.2 | 3.4 | 1.81 | 126×61 | 中（true） |
| | 2 | C | 5.8 | 6.6 | 5.6 | 3.6 | 2.0 | 119×65 | 中（true） |
| | 3 | D | 0.2 | 6.6 | 3.0 | 1.1 | 1.89 | 119×20 | 段・登り口 |
| 2 ななめ | 0 | A | -4.6 | 8.4 | 7.6 | 4.6 | 3.0 | 151×83 | 大・奥（true） |
| | 1 | B | 0.6 | 9.0 | 5.4 | 3.4 | 2.0 | 162×61 | 大（true） |
| | 2 | C | 5.4 | 7.0 | 3.0 | 1.1 | 1.89 | 126×20 | 段・登り口 |
| | 3 | D | -5.4 | 5.0 | 2.9 | 1.0 | 1.9 | 90×18 | 段・登り口 |

守る決まり（数を変えるときも。スクリプトで全部の型を確かめた）：

1. **重なりの決まり**：重なる 2枚では、手前の top が奥の top より **0.6 以上 低い**（奥のタイトルバー 9ドット＝0.5マスが全部見える。上の端の当たり判定は欠けない）。→ 描く順は「top の高い順に奥」。重なる組の top の差は 1.8〜4.6 で全部 OK
2. その結果、**いちばん手前（色つきの信号機）は、いつも低い段**になる。大きいウィンドウは灰色の信号機（Mac の「後ろのウィンドウ」）。これでよい
3. 幅は **9.0 まで**（`TalkKitUtil.Platforms` は `幅 >= 遊ぶ所の幅 × 0.5` の足場を数えない。遊ぶ所が 18.6 なら 9.3。9.6 は危ない）
4. 登り方：床 → 段（top 2.9〜3.0）→ 大（5.2〜5.6）→ 奥（7.4〜7.6）。段差は全部 **2.6 以下**。登る 2枚は横に **1.0 以上 重なる**（真上へとべば届く）
   - 型 0：床 → D → B（横の重なり 4.1）→ A（2.0）。C は低いわき道（C から B へは行けない）
   - 型 1：床 → D → B（1.0）または C（1.0）→ A（2.4 / 2.0）
   - 型 2：床 → C → B（3.2）→ A（3.5）。床 → D → B（1.0）
5. 下の端は床から 1.8 以上（**ドットに合わせた後で**。h 1.2 は 22ドット＝1.22 になり、top 3.0 だと下の端 1.78 で巣が付かない。だから段は h 1.0〜1.1）。左右の端は x=±9.3 の内側（最大 ±9.1）。上の端は 7.6 まで（フォルダの飾り・HUD に当たらない）
6. 「まん中 x と top」の組は全部 0.3 以上ちがう（`AppPlatform.Covers`）
7. 枚数は 4枚のまま（5枚目は Art の順が 5 になり、巣（3）より手前になる。6章も `Count == 4`）
8. 段（h 1.0〜1.1）は h < 30ドットなので、今の「通知の板」の絵のまま。幅は 80ドット（4.5マス）以上にする（それより細いと題名の字が出ない）。大・中は `Big = true`（左の色の帯）

### 7-2. 重なったウィンドウの ワーム と 巣

コードで分かったこと：ワーム（11〜）と巣（3）は**どのウィンドウよりも手前に描く**。だから「かくれる」のではなく、**手前のウィンドウの顔の上を這う・浮く**ように見える。直す所は 2つ。

**ワーム（`ZakoWorm`）**

1. かくれ判定：レールの点 p が、自分より手前のウィンドウの四角（`StageWindow.World` を **0.3 ふくらませた**物）の中なら「かくれている」
2. 次の点がかくれているなら**折り返す**（今の壁ぎわと同じ書き方：`_s = before; _dir = -_dir`）。上の端は決まり 1 で かくれない
3. **1.0秒の間に 2回 折り返したら**、予告して落ちる（短い所で行ったり来たりしない）
4. 高さの決まり：なくさない。ウィンドウのレールだけ `HighSeconds` を **5 → 9秒**。理由：高い所の道のりは最大で 型 0 の A の 7.0秒（`(w + 2×(top − max(下の端, 3.3))) ÷ 2.5`。ほかは 4.3〜6.9秒）。9秒なら 1回は通り切れる。型 1 の A のように全部が高い所に閉じこめられたワームは、9秒で落ちる
5. 「残り 3体以下 → 床だけ」は今のまま（`last && !onFloor → StartWarn`）。これが「手の届かない敵を残さない」の最後の守り
6. `ZakoWorm` に、今乗っている足場を返す読み取り口を足す（`public AppPlatform RailPlatform`）。重なると、ふちが近いウィンドウ（例：型 0 の B と D の下の端 1.8 / 2.0）を `ScanWorms` が取りちがえるため

見えるレール（確かめた）：上の端は全部。
- 奥の A：上＋横の上の方だけ（下の端はほぼ全部かくれる）。全部が高い所なので、**9秒で予告して落ちる**（3つの型とも）
- B（型 1 は B・C）：上 → 横 → 下の端の見える所 → 段の手前で折り返す。折り返す所がちょうど 3.3 の高さなので、行って戻る間に 9秒をこえて落ちることがある。それでよい（落ちて床に来る＝倒せる）
- 段（C・D）：全部 3.3 より下。1周する（いちばん手前でない段は、重なる所で折り返す）

**巣（`VirusNests.FindUnderside`）**

1. 今は「下の端のまん中」だけを見る → 重なると手前のウィンドウの上に巣が浮く。**下の端の、見えている いちばん長い区間のまん中**に変える
2. 区間は **2.0マス以上**（手前の四角は 0.3 ふくらませて切る）。高さの帯 1.8〜3.6・幅 2.5 以上は今のまま
3. 付く所（確かめた）：型 0＝B（x 3.5〜8.1）・C・D／型 1＝B（-9.1〜-3.4）・C（3.8〜9.1）・D／型 2＝B（-2.6〜1.6）・C・D。**奥の A には付かない**（どの型も 3か所ある）

### 7-3. ウイルスから広がる汚染（`WindowCorrosion` の作り直し）

**しくみ**

1. ウィンドウごとに、ドットごとの「届いた時刻」`Arrive[]`（まだ＝∞）と「穴になる時刻」`Hole[]` を持つ
2. ウイルスがいる所に**種**を置く：`(x, y, 生まれた時刻, 速さ, 最大の半径, 種類)`。半径 r ＝ 速さ × たった秒数（最大まで）
3. ドットへの距離 d に、にじみ `n`（0.65〜1.5。6ドットの大きさのなめらかな乱数＋ドットごとの小さい乱数。最初に 1回作る）を掛ける。`d × n ≤ r` になった時刻が `Arrive`（小さい方を残す）。n が小さい筋にそって、根のように伸びる
4. 色は「届いてからの秒数」で決める：**0.4秒まで マゼンタ**（広がっている先だけ 1〜2ドット光る）→ **紫** → **6秒から 濃い紫**（3割だけ。まだら）。止まったシミは光らない
5. 穴：強い種（巣・ルートキット）だけ。届いて **8秒後**から、種の半径の **35%** まで透明に（ふち 1ドットは濃い紫）。**タイトルバーの行には穴をあけない**（今のまま。紫にはなる）
6. 種を置くウィンドウ：ふち・上に立つ物は**その足場のウィンドウ**。とぶ物は、その点で**いちばん手前**のウィンドウ
7. 元の絵 `Clean` から塗るのは今のまま。色は紫・濃い紫・マゼンタ・戻るときの白だけ

**何が種になるか**（1マス＝18ドット。大きいウィンドウは 162×90 ＝ 約 14,600ドット）

| もと | いつ・どこに | 速さ（ドット/秒） | 最大の半径 | 育つ間 | 1枚の上限 | 穴 |
|---|---|---|---|---|---|---|
| ワーム | 這っているふちに、前の種から **10ドット**ごと（0.15秒ごとに見る）。かじり跡は今のまま | 3 | 9 | 3秒 | 48 | × |
| ワームの巣 | 付いた下の端の点。上へ広がる | 1.2 | 46 | 巣がある間（こわす・しぼむと止まる） | 2 | ○ |
| ルートキット | 出た時（Rising）。ウィンドウの上なら上の端の点。**床から出たとき**は、真上 2.5マス以内にウィンドウの下の端が見えていれば、その下の端の点。濃い紫を多めに（ひび） | 5 | 12 | 2.4秒 | 12 | ○ |
| マイナー | ウィンドウの上にいる間、1.0秒ごとに足元。**下へ細長く**（たての距離を 0.5倍で数える＝深さ 20・幅 10 の「ほった穴」） | 4 | 10 | 2.5秒 | 12 | × |
| ほかの雑魚 | ウィンドウの上に立っている間、1.5秒ごとに足元（ほかの弱い種から 12ドット以上あける） | 2.5 | 6 | 2.4秒 | 16 | × |
| とぶ雑魚（スパイウェア） | ウィンドウの四角の中に 1.0秒いたら、その点 | 2 | 6 | 3秒 | 8 | × |

- 床の雑魚は種にならない（ウィンドウの下の端は 1.8 以上なので、重ならない）
- コードの事実：**マイナーは歩く雑魚の見た目だけ**（`VirusSkins`：`("Miner", Base.Walker)`）。自分の動きは無い。ウィンドウのステージで出るのは Discord だけ。**ルートキットは 6つのアプリの歩く枠に無い**（巣からだけ）。ルートキットの巣は床に付き、出たルートキットも床にもぐる（`FindFloor`・`AcquireSurface`）ので、**ウィンドウの上から出ることは ほぼ無い** → 上の表の「床から出たとき」の決まりで、真上のウィンドウの下の端を汚す。だから主役は「ワーム・巣・上に立つ雑魚」。マイナーの「下へ細長く」は、種類の名前を見て形を変えるだけにする
- 【提案・あとで】ウィンドウのステージだけ、W2 が無いアプリ（LINE・Messages）の W2 を Miner にすると、ほり跡が見える

**広がる量の目安**（大きいウィンドウ 1枚）

| いつ | 目安 | 上限（こえたら新しいドットを足さない） |
|---|---|---|
| 雑魚の時間（60〜90秒）の終わり | 20〜35%（ワーム 1周のふち約 15%＋巣 20秒で約 6%＋立つ雑魚 約 3%） | **45%** |
| ボス戦 | ゲージで広がる（下） | **70%** |

- 巣を放っておくと 38秒で半径 46（半円で約 23%）。こわせば止まる → 「早くこわす」が見た目で伝わる
- 段（22ドットの高さ）は小さいので、上限の割合は同じでも早く紫になる。それでよい

**ボス戦**（雑魚も巣ももういないので、ゲージで動かす）

1. 今ある全部の種の半径を `最大の半径 × (1 + 0.6 × ゲージの割合)` まで広げる。ゲージが 0.03 変わるごとに 1回、1フレームに 1枚ずつ。届いた時刻は「今」
2. 種が 0 のウィンドウには、ボスの登場のとき、**ボスにいちばん近いふちの点**に 1つ（速さ 2・最大 30・ゲージで育つ）
3. 乱れ（0.50 から）・題名くずれ（0.60 から）は今の `level` のまま（場面とゲージ）。`level` はもう紫の量には使わない

**元に戻る**（正常化。1.2秒。今と同じ長さ）

1. 全部のウィンドウの `Arrive` の いちばん古い時刻 tMin と いちばん新しい時刻 tMax を取る
2. 切る時刻 c を **tMax → tMin へ 1.2秒で**動かす。`Arrive > c` のドットは `Clean` に戻す（新しい所から消えて、種へちぢんで帰る）
3. 境目（`c 〜 c +（tMax − tMin）× 0.08`）は白。0.1秒
4. 終わったら 種・`Arrive`・`Hole`・かじり跡を全部消す。題名も元に

**重さの決まり**

1. 塗り直しは 1枚 **10回/秒まで**（戻る間だけ 20回/秒）。変わった四角（育っている種の箱を合わせた物）だけ回す
2. `Arrive` を書くのは**育っている種だけ**。育ち終わった種は軽いリスト（x・y・最大の半径・種類）に移す（ボス戦の 1 で使う）
3. 種は 1枚 **合計 96 まで**。種の箱は 半径 ÷ 0.65（にじみの いちばん小さい値。半径 9 なら 29×29ドット）。育っている種は多くて 60（ワーム 4体 × 14）× 29×29 ＝ 約 5万ドット/回 → 軽い
4. `float` の配列は 1枚 2本（最大 14,600 × 2）。毎フレームの確保はしない

**時間が足りないときに切る順**（上から切る）

1. とぶ雑魚の種
2. マイナーの細長い形（ほかの雑魚と同じ丸に）
3. 穴（`Hole[]` ごと）
4. ルートキットの「濃い紫 多め」
5. にじみのなめらかな乱数（ドットごとの乱数だけに）
6. ボス戦のゲージで広がる分（ボス戦は 乱れ・題名だけに）
7. 上に立つ雑魚の種

のこす物（これで依頼を満たす）：**ワームと巣の種・だんだん広がる・マゼンタの先・元に戻る**。

### 7-4. 今のままの物

- macOS の見た目（`MacUi.StampWindow`）・題名の表・中身の絵・壁紙・フォルダの飾り・左右反転・型の選び方
- 足場の作り（`SpawnWindow`：上の端だけのすり抜け床・子 `Art`）・描く順（影 -8+3i・Art -7+3i・字 -6+3i）
- 雑魚の出る所（2回に 1回 床だけ）・ウイルスの割り当て（1-7）・ワームの上限 4
- 色（紫 `#6A2A8C`・濃い紫 `#1E0830`・マゼンタ `#ED38FF`・白）。赤なし・字の説明なし
- かじり跡・乱れ・題名くずれ・1.2秒で戻る・例外で止める・`MacUi.WindowSprite` にさわらない
- `StageWindow` の約束（4章）。足すのは読み取り口だけ：`ZakoWorm.RailPlatform`、`WindowCorrosion.Coverage(i)`（0〜1）・`SeedCount`・`DebugSeed(i, 点, 種類)`

### 7-5. 確認用のプログラムが見ること（6章の 2・3・4 を置きかえる）

1. **型**：`ForceTemplate = 0 / 1 / 2` の絵。4枚・全部 `IsSolidNow`。重なる組で「手前の `World.yMax` ≤ 奥の `World.yMax` − 0.6」。幅 ≤ 9.0
2. **登れる**：プレイヤーを床に置いて、型ごとの道（7-1 の 4）の順に真上へとばせて、A の上（y ≥ top）に立てるか
3. **ワーム**：`ForceType = "Worm"` で 20秒。毎フレーム、レールにいるワームの位置が、手前のウィンドウの四角（ふくらませない）の中に**入っていない**こと。床から 3.3 より上に 9.7秒 以上 続けていたワームが 0。残り 3体で全部 床
4. **巣**：`VirusNest.All` の、ウィンドウの下の巣が、手前のウィンドウの四角の中に無い。A に付いていない
5. **広がる**：`SeedCount > 0`。`Coverage` が 5秒・15秒・30秒 と**増える**（絵 3枚。種のまわりから内側へ伸びて、先がマゼンタ）。巣をこわした後、そのまわりが 3秒 増えない。雑魚の時間は どの 1枚も 0.45 以下
6. **ボス戦**：ゲージ 0% → 100% で `Coverage` が増える・0.70 以下。乱れと題名くずれが出る（絵 1枚）
7. **戻る**：正常化の 0.6秒後の絵（新しい所から消えて、白い境目）。1.5秒後に全部の `Coverage == 0`・`SeedCount == 0`・`BiteCount == 0`・題名が元の字
8. **色**：汚れたドットの色は 紫・濃い紫・マゼンタ・白・透明だけ。タイトルバーの行に透明が無い
9. `ForceLevel = 0.2 / 0.5 / 0.9` は、もう紫を塗らない（乱れ・題名だけ）。紫の絵は `DebugSeed` で撮る
