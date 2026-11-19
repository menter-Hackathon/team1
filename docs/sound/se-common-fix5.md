# SE の設計メモ（共通の音 ＋ 直した5アプリ）

2026-11-19。音は全部 Python で一から作る（`Tools/audio/FORMAT.md`）。このメモは、共通の SE と、Chrome・VS Code・Finder・Premiere Pro・Terminal の SE の説明。

- レシピ：`Tools/audio/se/common.json`（56 個）・`Tools/audio/se/fix5.json`（33 個）
- 割り当て：`Assets/Resources/Audio/Maps/00_common.json`（出来事 45・代わりの音 12）・`Assets/Resources/Audio/Maps/10_fix5.json`（技 44）
- まだ耳で聞いていない（音を作るプログラムと同時に書いたため）。聞いてから、大きさ（割り当ての `vol`）と高さ（`pitch`）を直す。

## 考え方

- プレイヤーの音は、高くて明るい（細いパルス波・三角波）。ボスの音は、低くてにごった音（矩形波・のこぎり波・ビットを落とす）。
- 何度も鳴る音（予告・当たり・弾）は、0.03〜0.08 秒で小さく。ダメージの音（`se_player_hurt`）だけは、いちばん大きくして、ほかの音にうもれないようにした。
- 本物のアプリ・OS の音はまねしていない。音色とリズムで「そのアプリっぽさ」を出した（キーボード・ファスナー・カミソリ・テープなど、物の音がもと）。
- 第1形態と第2形態は、同じ SE を `pitch` で変える（第2形態は少し低く、または少し高く）。
- **技の動きに合わせて作った音は `pitch` を 1.0 のままにする**（高さを変えると長さも変わって、ずれる）：`se_vscode_breakpoint_line`（1.0秒で止まる）・`se_premiere_razor`（0.9秒で切る）・`se_premiere_keyframe`（1.0秒で走り出す）・`se_premiere_speed`（0.8秒で決まる）・`se_term_chmod`（0.9秒で鍵）・`se_finder_type`（0.2秒ごとに1文字）・`se_abil_bargraph`（0.15秒ごとに1本）・`se_chrome_offline`（予告 1.0秒）。技の時間を変えたら、この音も直す。
- 専用の技（Custom）の多くは、技が始まった瞬間に「出た」の知らせも出す。その技は、音を `start` と `release` のどちらか1つにだけ入れた（2つ重ならないように）。

## 汎用（se_gen_*）

| SE の id | どんな音 | 元ネタ | 使う所 |
|---|---|---|---|
| `se_gen_telegraph` | とても小さく短い「ピ」。赤い予告が出た合図 | — | 出来事 `telegraph` |
| `se_gen_shot_small` | 敵の小さい弾。低めの「ピュ」 | — | 代わりの音：敵 の Projectile |
| `se_gen_shot_big` | 敵の大きい弾。太い「ドシュ」 | — | 代わりの音：敵 の Custom |
| `se_gen_laser` | レーザー。高い所から下がる「ビーッ」 | — | 代わりの音：プレイヤー の LockOn、代わりの音：敵 の LockOn |
| `se_gen_explosion_small` | 小さい爆発「ボン」 | — | （今は割り当てなし。ほかの担当が使ってよい） |
| `se_gen_explosion_big` | 大きい爆発「ドゴーン」。低くて長め | — | （今は割り当てなし。ほかの担当が使ってよい） |
| `se_gen_whoosh` | 風を切る「ヒュッ」。ワープ・すばやい動き | — | 代わりの音：プレイヤー の Warp、代わりの音：敵 の Warp |
| `se_gen_spawn_pop` | 物が現れる「ポン」 | — | 代わりの音：プレイヤー の Spawn、代わりの音：敵 の Spawn |
| `se_gen_land_thud` | 重い物が落ちた「ドスン」 | — | 出来事 `faller_land` |
| `se_gen_warn_beep` | 注意の「ピピッ」。2回の短いビープ | — | 代わりの音：敵 の Area |
| `se_gen_glass_break` | ガラスが割れる「パリーン」 | — | 出来事 `shield_break` |
| `se_gen_charge` | 力をためる「ウィーン」。だんだん高くなる | — | Chrome_TabShuriken_P1（始め）、Chrome_TabShuriken_P2（始め） |

## UI（メニュー）

| SE の id | どんな音 | 元ネタ | 使う所 |
|---|---|---|---|
| `se_ui_move` | カーソルの「ピッ」。軽くて短い | — | 出来事 `ui_move` |
| `se_ui_decide` | 決定の「ピコン」。2つの音が上がる | — | 出来事 `ui_decide` |
| `se_ui_cancel` | 戻るの「ポコ」。2つの音が下がる | — | 出来事 `ui_cancel` |
| `se_ui_start` | ゲームを始める。明るく駆け上がる音とエコー | — | 出来事 `ui_start` |
| `se_ui_card` | カードをめくる「シャッ・ポン」 | — | 出来事 `ui_card` |

## プレイヤー

| SE の id | どんな音 | 元ネタ | 使う所 |
|---|---|---|---|
| `se_player_jump` | ジャンプの「ピョン」。明るく上がる | — | 出来事 `player_jump` |
| `se_player_land` | 着地の「トッ」 | — | 出来事 `player_land` |
| `se_player_dash` | ダッシュの「シュッ」。風＋上がる音 | — | 出来事 `player_dash` |
| `se_player_swing` | 近接を振る「スパッ」 | — | 出来事 `player_swing` |
| `se_player_swing_blocked` | 弾かれた「カキン」。金属っぽい | — | 出来事 `player_swing_blocked` |
| `se_player_hurt` | ダメージの「ビシャッ」。強く、はっきり聞こえる | — | 出来事 `player_hurt` |
| `se_player_die` | 倒れた。しょんぼり下がっていく音 | — | 出来事 `player_die` |
| `se_player_fall` | 穴に落ちた「ヒュ〜〜」 | — | 出来事 `player_fall` |
| `se_player_heal` | 回復の「キラリン」。やさしく上がる | — | 出来事 `player_heal` |
| `se_player_shot` | プレイヤーの弾「ピッ」。高くて軽い | — | 代わりの音：プレイヤー の Projectile |
| `se_player_skill` | 能力を出した「シャキン」。明るい | — | 代わりの音：プレイヤー の Area、代わりの音：プレイヤー の Custom |
| `se_skill_pickup` | 能力を拾った。うれしい上がる音 | — | 出来事 `skill_pickup` |
| `se_skill_ready` | 能力がまた使える「ポロン」。小さめ | — | 出来事 `skill_ready` |
| `se_skill_denied` | 使えない「ブブッ」。低い2回 | — | 出来事 `skill_denied` |

## 敵・ボス

| SE の id | どんな音 | 元ネタ | 使う所 |
|---|---|---|---|
| `se_enemy_hit` | 雑魚に当たった「ペシ」。短くて歯切れがいい | — | 出来事 `enemy_hit` |
| `se_enemy_die` | 雑魚が倒れた「ポフッ」 | — | 出来事 `enemy_die` |
| `se_boss_hit` | ボスに当たった「ドッ・チ」。重い当たり＋ゲージの小さい音 | — | 出来事 `boss_hit` |
| `se_boss_appear` | ボスが現れる。低いうなりがせり上がる | — | 出来事 `boss_appear` |
| `se_boss_phase2` | 第2形態。バグった音がせり上がって「ドン」 | — | 出来事 `boss_phase2` |
| `se_boss_purged` | ウイルスを取り除いた。はじけて、きれいな音が上がる | — | 出来事 `boss_purged` |

## 盾・タブ・ステージの物

| SE の id | どんな音 | 元ネタ | 使う所 |
|---|---|---|---|
| `se_shield_block` | 盾が弾を止めた「コン」 | — | 出来事 `shield_block` |
| `se_shield_restore` | 盾が戻った「フォン」。下から上へ | — | 出来事 `shield_restore` |
| `se_tab_close` | タブを閉じた「プチッ」 | ウィンドウの × ボタン | 出来事 `tab_close` |
| `se_popup_open` | ウィンドウが現れた「ポポン」 | ポップアップ | 出来事 `popup_open` |
| `se_prop_hit` | ステージの物をたたいた「コツ」 | — | 出来事 `prop_hit` |
| `se_prop_break` | ステージの物が壊れた「バキャ」 | — | 出来事 `prop_break` |
| `se_block_vanish` | 足場が消えた「ピュルッ」。下がって消える | — | 出来事 `block_vanish` |
| `se_zone_fire` | 床の範囲攻撃が出た「ズバッ」 | — | 出来事 `zone_fire` |

## ステージの流れ・デスクトップ・図鑑・結果

| SE の id | どんな音 | 元ネタ | 使う所 |
|---|---|---|---|
| `se_wave_clear` | 雑魚の波を倒しきった「タタタン」 | — | 出来事 `wave_clear` |
| `se_pace_phase` | ステージの出来事が始まる合図。2回上がるサイレン風 | — | 出来事 `pace_phase` |
| `se_toast` | 通知が出た「ポロ」。小さくやわらかい | 画面の角の通知（音は自作） | 出来事 `toast` |
| `se_stage_clear` | ステージクリアのきらめき（ジングルに重なる） | — | 出来事 `stage_clear` |
| `se_desktop_click` | マウスのクリック「カチッ」 | マウスのボタン | 出来事 `desktop_click` |
| `se_desktop_open` | ウィンドウが広がる「フワッ」 | ウィンドウが開く動き | 出来事 `desktop_open` |
| `se_desktop_cure` | ウイルスがはじけて元の色に戻る「パン・キラ」 | — | 出来事 `desktop_cure` |
| `se_desktop_glitch` | 画面が乱れる「ザザッ…ブツッ」。ラスボスの気配 | こわれた画面 | 出来事 `desktop_glitch` |
| `se_dex_unlock` | 図鑑に色がついた「キラリーン」 | — | 出来事 `dex_unlock` |
| `se_result_rank` | ランクが出た「ドン！ジャーン」。スタンプ | ハンコ | 出来事 `result_rank` |
| `se_gameover_tick` | 感染率が増える「ジッ」。低くてにごった刻み | — | 出来事 `gameover_tick` |

## Chrome

| SE の id | どんな音 | 元ネタ | 使う所 |
|---|---|---|---|
| `se_chrome_tab` | タブが回って飛ぶ「シュルッ」。短い | タブをすばやく切りかえる・投げる | Chrome_TabShuriken_P1（出た瞬間）、Chrome_TabShuriken_P2（出た瞬間） |
| `se_chrome_popup` | ポップアップ広告の「ポポッ」。少し安っぽく明るい | いきなり出る広告の窓 | Chrome_PopupAppear_P1（出た瞬間）、Chrome_PopupAppear_P2（出た瞬間）、Chrome_AdPopups_P1（始め）、Chrome_AdPopups_P2（始め） |
| `se_chrome_tab_multiply` | タブがどんどん増える「ポポポポポポ」。上がっていく | タブの開きすぎ | Chrome_TabMultiply（出た瞬間） |
| `se_chrome_offline` | ネットが切れた。3つの音が下がって、ザーッと消える（予告1秒に合わせる） | オフラインのページ | Chrome_Offline（始め） |
| `se_chrome_dino` | 恐竜が走り出す「ピョッ・トトトト」。8ビットの小さい足音 | オフラインの恐竜ゲーム（音は自作） | App_Chrome_DinoRun（出た瞬間） |

## VS Code

| SE の id | どんな音 | 元ネタ | 使う所 |
|---|---|---|---|
| `se_vscode_keys` | キーボードを打つ「カタカタカタ」（弾の前のため） | コードを書くキーボード | VSCode_ErrorBullet_P1（始め）、VSCode_ErrorBullet_P2（始め） |
| `se_vscode_error_bullet` | エラーの弾「ブッ」。低くにごった短いブザー | 赤い波線・エラー | VSCode_ErrorBullet_P1（出た瞬間）、VSCode_ErrorBullet_P2（出た瞬間） |
| `se_vscode_breakpoint` | 赤丸を置く「トン」＋小さい「ピ」 | 行番号の横の赤丸（ブレークポイント） | VSCode_Breakpoint_P1（出た瞬間）、VSCode_Breakpoint_P2（出た瞬間）、Ability_Breakpoint（出た瞬間） |
| `se_vscode_breakpoint_line` | 赤丸「トン」→ 矢印が進む刻みが速くなる → 1秒で止まる「ビタッ」（予告1秒に合わせる） | デバッグで1行ずつ進めて、止まる | VSCode_BreakpointLine_P1（始め）、VSCode_BreakpointLine_P2（始め） |
| `se_vscode_error_window` | エラーの窓が落ちてくる合図「ブブー」。半音でにごらせる | エラーのダイアログ | VSCode_ErrorWindowFall（出た瞬間） |
| `se_vscode_warp` | 行へジャンプ「ピロロッ」。下がってすぐ上がる | 定義へジャンプ・行の移動 | VSCode_Warp（出た瞬間） |

## Finder

| SE の id | どんな音 | 元ネタ | 使う所 |
|---|---|---|---|
| `se_finder_type` | 検索バーに6文字打つ「コッ コッ…」（0.2秒ごと。文字が出るのに合わせる） | Spotlight に名前を打つ | Finder_Spotlight_P1（始め）、Finder_Spotlight_P2（始め） |
| `se_finder_beam` | 光の帯「ピシャーッ」。明るく高い所から下がる | Spotlight の検索の光 | Finder_Spotlight_P1（出た瞬間）、Finder_Spotlight_P2（出た瞬間） |
| `se_finder_folder` | 書類をめくる「パサパサ」＋フォルダが開く「ポン」 | フォルダと書類 | Finder_SmartFolder_P1（始め）、Finder_SmartFolder_P2（始め）、Finder_PopupAppear_P2（出た瞬間） |
| `se_finder_tag` | タグ・トゲが飛ぶ「ピュッ」。紙をはじく感じ。短い | 色のタグ | Finder_SmartFolder_P1（出た瞬間）、Finder_SmartFolder_P2（出た瞬間） |
| `se_finder_zip` | ファスナー「ジィッ」＋ぎゅっと小さくなる「キュッ」。吸いこみにも発射にも使う | zip 圧縮（ファスナー） | App_Finder_Zip（出た瞬間） |

## Premiere Pro

| SE の id | どんな音 | 元ネタ | 使う所 |
|---|---|---|---|
| `se_premiere_keyframe` | ◆ を打つ「チッ チッ チッ」→ 1秒でクリップが走り出す「シュ」（予告1秒に合わせる） | キーフレームを打つ | Premiere_Keyframe_P1（始め）、Premiere_Keyframe_P2（始め） |
| `se_premiere_scrub` | テープを早送りする「キュルルル」。音程がゆれる | 再生ヘッドを動かす（スクラブ） | Premiere_Playhead_P1（始め）、Premiere_Playhead_P2（始め） |
| `se_premiere_razor` | 刃がかまえる「チャキ」→ 0.9秒で切る「ザシュッ」→ 列が閉じる「ゴトン」（技の流れに合わせる） | カミソリ（レーザーツール）で切って詰める | Premiere_Razor（始め） |
| `se_premiere_speed` | 数字が回る「チチチチ…」→ 0.8秒で決まる「ピポッ」 | 速度・デュレーションの数字 | Premiere_SpeedDuration（始め） |
| `se_premiere_ripple` | 切って「シャッ」、すき間が詰まる「シュルッ・カチ」。明るい（プレイヤーの能力） | リップル削除 | App_PremierePro_RippleDelete（出た瞬間） |

## Terminal（ラスボス）

| SE の id | どんな音 | 元ネタ | 使う所 |
|---|---|---|---|
| `se_term_type` | 暗いキーボード「ガガガガ…」＋低いうなり。コマンドを打つ（約0.9秒） | 黒い画面にコマンドを打つ | Terminal_RmRf_P1（始め）、Terminal_RmRf_P2（始め） |
| `se_term_type_short` | 暗いキーボードの短い版「ガガガガッ」（約0.45秒） | 短いコマンドを打つ | Terminal_StringRain（始め）、Terminal_Fork（始め） |
| `se_term_rmrf` | 床がくずれる。低くにごったうなりが下がり、ザラザラくずれる（長め） | rm -rf（全部消すコマンド） | Terminal_RmRf_P1（出た瞬間）、Terminal_RmRf_P2（出た瞬間） |
| `se_term_chmod` | コマンドを打つ → 0.9秒で鎖が床に着いて、鍵がかかる「ガチャン」（技の流れに合わせる） | chmod 000（使えなくするコマンド） | Terminal_Chmod_P1（始め）、Terminal_Chmod_P2（始め） |
| `se_term_deny` | アクセス拒否「ブッブー」。低くてきつい2回のブザー | Permission denied | Terminal_DenyPillar_P1（出た瞬間）、Terminal_DenyPillar_P2（出た瞬間） |
| `se_term_rain` | 文字が降る「ピロピロピロ…」。細い音がばらばらに下がる | 流れ落ちる文字列 | Terminal_StringRain（出た瞬間） |
| `se_term_fork_shot` | 分身が出る・撃つ「ブツッ」。暗くて短い | fork（プロセスが増える） | Terminal_Fork（出た瞬間） |
| `se_term_rm_player` | 自分の rm -rf。軽い打鍵「タタタッ」→ 波が走る「シュワー」。明るい色（プレイヤーの能力） | rm -rf を自分が使う | App_Terminal（出た瞬間） |

## プレイヤーの能力

| SE の id | どんな音 | 元ネタ | 使う所 |
|---|---|---|---|
| `se_abil_bargraph` | 棒が3本立つ「ド・ミ・ソ」（0.15秒ごと）。1本ごとに小さい「トン」 | 棒グラフ | Ability_BarGraph（出た瞬間） |
| `se_abil_bomb` | ボムを投げる「ポイッ」＋導火線の「シュー」 | ボム | Ability_Bomb（出た瞬間） |
| `se_abil_newtab` | 新しいタブへ飛ぶ「シュッ・ポン」。明るい | 新しいタブを開く | Ability_NewTab（出た瞬間） |
| `se_abil_paint` | ペンキを流す「バシャ・トプン」 | バケツ塗り | Ability_PaintBucket（出た瞬間） |

## 分かっている弱い所

- ブレークポイント（行）の第2形態は、行が3つ（1.0・1.7・2.4秒）。音は1つ目の行にだけ合う（SE は 1.5秒までなので）。2つ目・3つ目は、予告の音（`telegraph`）と床の発動の音（`zone_fire`）にまかせる。
- 文字列の雨は3回降るが、「出た」の知らせは1回目だけ。2回目・3回目は音がない（Unity 側で知らせを足せば鳴る）。
- zip 圧縮は、吸いこみと発射で同じ知らせが来るので、同じ音（`se_finder_zip`）。分けたいときは、Unity 側で出来事を分ける。
- fork() は、分身が出る瞬間と、分身が1発撃つたびに同じ音（`se_term_fork_shot`）。
- ボムの爆発・弾が当たった瞬間は、技の知らせが来ないので、ここでは鳴らない（`enemy_hit`・`boss_hit` が鳴る）。
- chmod のあとの柱は、別の攻撃データ（`Attack_Terminal_DenyPillar_P1`・`_P2`）で知らせが来るので、その名前の行を足した。
