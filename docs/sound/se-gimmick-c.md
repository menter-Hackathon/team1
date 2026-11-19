# ギミックの SE（グループ C：Unity・Blender・Minecraft・QuickTime・GitHub Desktop・Xcode・Cursor・Safari）

2026-11-19。アプリごとの「ステージの決まり」（StageRule）の瞬間に付ける専用の音。29 個。

- レシピ：`Tools/audio/se/gimmick_c.json`（id は `se_gm_<アプリ>_<何>`）
- 割り当て：`Assets/Resources/Audio/Maps/43_gimmick_c.json`（キーは `gm_<アプリ>_<瞬間>`。SE の id はキーの頭に `se_` を付けた物）
- 鳴らす所の指示書：`Tools/audio/hooks/gimmick_c.tsv`（コードにはまだ入れていない。Opus の担当が `GameAudio.Event("gm_…")` を入れる）
- 本物のアプリ・ゲーム・OS の音はまねしていない（音色とリズムの雰囲気だけ。メロディは自作）。アプリごとの音の色は、技の音（kit2.json）にそろえた。
- 考え方：プレイヤーに不利な物は「来る前」と「来た時」、ボスが強くなる物は「始まり」と「終わり」が分かるようにした。何度も鳴る物は小さく短く（vol 0.25〜0.4）。
- トースト・壊れる音など、今ある音と同じフレームになる物は、SE の頭を 0.06〜0.12 秒おくらせて、重なっても聞き分けられるようにした。

## Unity（ランダムな座標に 3D の物体）

| キー | いつ | 音 | 元ネタ |
|---|---|---|---|
| `gm_unity_object_warn` | 予告の枠と「!」が出た（7秒おき） | 「ポコッ・ピピッ」低い四角波のポコッ＋高い注意のピ2つ | Hierarchy に Cube (3) の行が増える |
| `gm_unity_object_drop` | 1.0秒後、物体が現れて落ち始める | 「ヒュゥン↓」三角波と四角波が一気に下がる | Rigidbody の物体が重力で落ちる |

着地は今ある `faller_land`。`gm_unity_object_drop` は共通のファイル（Kit3DFaller）に入れる必要がある。

## Blender（Default Cube）

| キー | いつ | 音 | 元ネタ |
|---|---|---|---|
| `gm_blender_frame` | Cube がまた現れる場所にオレンジの枠 | 「ウィッ・カチ」短く上がるノコギリ波＋キーのクリック | オレンジの選択の枠 |
| `gm_blender_cube` | Cube が現れた | 「ポン・カッ」三角波がふくらんで四角い2音で止まる | 新しいファイルに必ずある Default Cube |
| `gm_blender_delete` | 3回なぐって消した（見返り） | 「カチ・ピロリロン♪」メニューのクリック＋明るく上がる4音 | 最初に Default Cube を Delete する儀式 |

## Minecraft（昼と夜）

| キー | いつ | 音 | 元ネタ |
|---|---|---|---|
| `gm_minecraft_dusk` | 夜の2秒前（空が暗くなり始める） | 「ポー・ポー・ボー…」ゆっくり下がる三角波の3音＋うすい風 | 夕方に四角い太陽が沈む |
| `gm_minecraft_night` | 夜になった（雑魚が速く・ボスの待ち時間が短く） | 「ボォーーン…ゴゴ」低い短調の下がり＋低いうなり | 夜は敵が強くなる |
| `gm_minecraft_dawn` | 朝になった（強化の終わり） | 「ポ・ポ・ポ・ポーン↑」明るく上がる三角波の4音 | 朝日がのぼると安全になる |

## QuickTime Player（再生の速さ）

| キー | いつ | 音 | 元ネタ |
|---|---|---|---|
| `gm_quicktime_blink` | ボスの札が点滅（変わる1秒前） | 「ピ・ピ・ピ」sine の同じ高さの3回 | 速さを変える前のカウント |
| `gm_quicktime_fast` | ボスが 2x になった（強化の始まり） | 「キュルルッ↑・ピピッ」かけ上がる早送り＋速いピ2つ | 2倍速の早送り |
| `gm_quicktime_slow` | ボスが 0.5x になった（チャンス） | 「ギュゥーーン↓・ボー」だれて下がるスロー＋低い長い1音 | 0.5倍速のスロー再生 |
| `gm_quicktime_normal` | 1x に戻った（2x・0.5x の終わり） | 「カチ・ポッ」クリック＋まん中の高さの丸い1音 | 再生速度を 1x に戻す |
| `gm_quicktime_tag` | 雑魚に 2x・0.5x の札が付いた | 「チッ」とても短い2音（2x は高く・0.5x は低く） | 再生速度の札 |

## GitHub Desktop（マージの衝突）

| キー | いつ | 音 | 元ネタ |
|---|---|---|---|
| `gm_github_merge_warn` | 合流の柱の予告（赤い枠と <<<<<<<） | 「ウィーーン→←・ガガガッ」上下から来た2つの音が半音でぶつかる | 2本のブランチが合流して衝突の印が出る |
| `gm_github_merge_fire` | 1.2秒後、柱が赤く光る | 「ガシャーン！」低いにごった和音＋ノイズのはじけ | マージの衝突（Conflict） |
| `gm_github_commit` | 雑魚を倒して main にコミットの丸が増えた | 「ポッ」とても短い丸い三角波 | 履歴にコミットが1つ積まれる |

## Xcode（Build）

| キー | いつ | 音 | 元ネタ |
|---|---|---|---|
| `gm_xcode_building` | Building... が始まった（5秒で3体） | 「カ・カ・カチャッ↑」金属のチッ3回＋5度上がる四角波 | Indexing が終わって Building... に変わる |
| `gm_xcode_count` | 数が1つ進んだ（丸が緑に） | 「カチン」金属のチッ＋短い四角波（進むたびに高く） | ビルドの進みのバー |
| `gm_xcode_succeeded` | Build Succeeded（クールタイムが速く） | 「チッ・ポ・ポ・ポーン♪」やわらかく上がる3音（自作） | 緑の Build Succeeded |
| `gm_xcode_failed` | Build Failed（8秒、敵が強く） | 「ガチャン・ブ・ブ・ブーー↓」金属の外れる音＋増4度で落ちる | 赤い Build Failed |
| `gm_xcode_failed_end` | Build Failed の8秒が終わった | 「コ・ピッ」小さい四角波が5度上がる | エラーが消えて Indexing に戻る |

## Cursor（Tab の候補）

| キー | いつ | 音 | 元ネタ |
|---|---|---|---|
| `gm_cursor_tab_appear` | Tab の候補の箱が現れた | 「カチ・ポワン…」小さいクリック＋やわらかい2音にエコー | 灰色のゴーストテキストがふっと出る |
| `gm_cursor_tab_hurry` | 箱が点滅（消える1秒前） | 「ピピピピピ」三角波の細かい5連 | 候補は放っておくと消える |
| `gm_cursor_accept` | プレイヤーが箱に触れた（クールタイム全部回復） | 「カチッ・キラリーン♪」Tab のクリック＋かけ上がる4音 | Tab で候補を Accept |
| `gm_cursor_boss_take` | ボス戦で取りそこねた箱が紫になってボスへ飛ぶ | 「ブブッ・ヒュイーン↑」低いブザー2回＋上がるノコギリ波 | AI が勝手に続きを書いてしまう |
| `gm_cursor_boss_accept` | ボスが Accept（次の待ち時間が半分） | 「ガチッ・ブ・ブ・ブーン↑」重いキー＋増4度で上がる低いパルス | Accepted（ボスの側） |

ボスの強化の終わり（次の技の始まりで外れる）は、技の始まりの音と重なるので付けていない。

## Safari（トラッカーをブロック）

| キー | いつ | 音 | 元ネタ |
|---|---|---|---|
| `gm_safari_count` | トラッカーを1つブロック（メーターが進む） | 「ピッ」澄んだとても短い2音（数が増えるたびに高く） | 「トラッカー N 個をブロック」の数字 |
| `gm_safari_shield_on` | 5つたまって盾が付いた | 「シュワ・ポロロローン♪」きらめき＋上がる4音にエコー | プライバシーレポートの盾 |
| `gm_safari_shield_block` | 盾が当たりを1回止めて割れた | 「パキーン！」明るいノイズのはじけ＋下がる sine の2音 | トラッカーをブロックした |

## 入れるときの注意（指示書の要点）

- 共通のファイルに入れる物：`gm_unity_object_drop`（`Kits/Common/Kit3DFaller.cs` の StartFall。`_opt.Group == "unity.rule"` のときだけ）。
- 境目の文が無くて、見張り（bool・タイマー）を足す物：`gm_minecraft_dusk`・`gm_cursor_tab_hurry`・`gm_xcode_failed_end`。
- QuickTime の 2x・0.5x・1x の音は、Tick の「変わった所」に入れる（ApplyBoss の中・ボスの登場には入れない）。
- 倍率を付ける物：`gm_xcode_count`（数で高く）・`gm_safari_count`（数で高く）・`gm_quicktime_tag`（2x 高く・0.5x 低く）。
