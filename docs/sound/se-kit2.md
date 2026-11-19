# SE（kit-2 の 16 アプリ）

2026-11-19。レシピ：`Tools/audio/se/kit2.json`、割り当て：`Assets/Resources/Audio/Maps/30_kit2.json`。

- どのアプリも **本物の音はまねしていない**（音色とリズムの雰囲気だけ。メロディは全部自作）。
- ボスの音は低め・にごり、自分の能力の音は高め・明るい。第2形態は、同じ SE を `pitch` で少し低くしているだけ。
- **鳴る時について**：どの技も「ため」（Windup）が 0 なので、`start` は技が始まった瞬間。`release` は、コードが `NotifyReleased` を呼ぶたび（技によって「出した時」「当たる時」「1発ごと」がちがう）。出した時に1回しか呼ばれない技は、確実に鳴る `start` に音を置いて `release` を空にしてある。
- 弾・衝撃など、技の中で使う別の攻撃データ（`Attack_GitHub_PushCommit` など）が合図を出す所には、そのデータの名前で行を足してある。
- 赤い予告・床の範囲攻撃・落下物の着地は、共通の出来事の音（`telegraph`・`zone_fire`・`faller_land`）が別に鳴る。


## Unity

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_unity_spawn` | 「ポコッ」。四角い低めのブリップ＋小さいドン。物体が1つ出るたびに鳴る短い音 | Hierarchy に Cube が1つ増える・3D の物体がシーンに置かれる | Unity_Object3D_P1（release）、Unity_Object3D_P2（release・pitch 0.94） |
| `se_unity_ball` | 「ゴロゴロ…」。低い三角波のうなり＋こもったノイズ。重い球が転がり出す | Rigidbody の球が床を転がる物理 | Unity_Rigidbody_P1（release）、Unity_Rigidbody_P2（release・pitch 0.92） |
| `se_unity_shader_error` | 「ビビッ・ブー」。ザラついた増4度の往復＋下がるノコギリ波。エラーのブザー | Missing Shader のピンクと、Console の赤いエラー | Unity_MissingShader_P2（start） |
| `se_unity_cube_throw` | 「ピコッ・ヒュッ」。明るい2音のブリップ＋上がる風切り。軽くて気持ちいい投げ | + Cube（立方体を作ってポイと置く） | App_Unity_CubeThrow（start） |

## Blender

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_blender_grab` | 「カチ・ウィーン・カチ」。細かくふるえるノコギリ波が上がるサーボ音。前後にキーのクリック | G キーでつかんで軸にそって動かす（機械的な操作感） | Blender_Grab_P1（start）、Blender_Grab_P2（start・pitch 0.9） |
| `se_blender_scale` | 「ブゥーーン↑」。パルスの幅が細くなりながら2オクターブ上がる。ふくらむ音 | S キーで拡大（立方体が一気に大きくなる） | Blender_Scale_P1（start）、Blender_Scale_P2（start・pitch 0.92） |
| `se_blender_shard` | 「パラッ」。下がる速いアルペジオ＋短いノイズ。4つに割れて飛ぶ | スケールした立方体が4つに分かれる | Blender_ScaleShard_P2（release） |
| `se_blender_suzanne` | 「ボヨン・ウッホ」。三角波がはね上がって落ちる＋低い2音の鳴き声 | サルの頭 Suzanne がはずむ | Blender_Suzanne_P2（start） |
| `se_blender_scale_skill` | 「シュイッ・ポン！」。一気にかけ上がるパルス＋高い三角波のはじけ | S キーで拡大（自分の技。速くて明るい） | App_Blender_ScaleSkill（start） |

## Minecraft

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_minecraft_fuse` | 「ヒュッ…シューーー」。投げる下がり音のあと、だんだん強くなる導火線のノイズ（約1.5秒） | TNT の導火線（音は自作。本物の音は使わない） | Minecraft_Tnt_P1（start）、Minecraft_Tnt_P2（start） |
| `se_minecraft_sand` | 「ザッ・ザッ・ザザッ」。粗いノイズが4回＋低いドスン。砂が落ちて積もる | 落ちる砂のブロック | Minecraft_Sand_P1（start）、Minecraft_Sand_P2（start・pitch 0.9） |
| `se_minecraft_hiss` | 「スゥーーーッ↑」。だんだん高く強くなるノイズ＋低いうなり。いやな予感 | シューッと光って爆発する緑のやつ（雰囲気だけ） | Minecraft_GreenBlock_P2（start） |
| `se_minecraft_tnt_skill` | 「カチ…シューー・ドカン！」。置く音→1.2秒の導火線→太い爆発まで入り（爆発は 1.2秒の所） | TNT を置いて待つ（_fuse 1.2秒に合わせてある） | App_Minecraft_Tnt（start） |
| `se_minecraft_boom` | 「ドカーン」。下がるノイズ＋低い三角波の爆発だけ（0.5秒） | TNT・緑のブロックの爆発。※今は割り当てなし（爆発の瞬間に合図が出ないため。合図ができたら使う） | （割り当てなし） |

## QuickTime Player

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_quicktime_rewind` | 「ボッ・キュルルル↓↑」。撃つ低い音のあと、ふるえるノコギリ波が下がって上がるテープの巻き戻し | ⏪ 巻き戻し（弾が来た道を戻る） | QuickTime_Rewind_P1（start）、QuickTime_Rewind_P2（start・pitch 0.92） |
| `se_quicktime_trim` | 「カチ・カチ・シャキッ」。つまみ2つのクリック＋切る短いノイズ | 黄色いトリムのつまみで、外側を切り落とす | QuickTime_Trim_P1（release）、QuickTime_Trim_P2（release・pitch 0.93） |
| `se_quicktime_rec` | 「ピッ・ポー」。sine の2音（下→上）。録画が始まる合図 | 画面収録の赤い丸 REC | QuickTime_ScreenRecording_P2（start・pitch 0.85） |
| `se_quicktime_trim_skill` | 「シュッ→←・チャキン！」。上がる音と下がる音が1点に集まり、高い2音で切る | トリムのつまみが自分の所まで閉じる（0.2秒） | App_QuickTime_Trim（start） |

## GitHub Desktop

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_github_diff` | 「ピッ（＋）・ブーッ（−）」。高い明るい1音のあと、低いザラついたノコギリ波 | diff の緑の + と赤の − | GitHub_Diff_P1（start）、GitHub_Diff_P2（start・pitch 0.93） |
| `se_github_push` | 「タタタターン↑・シュー」。5度ずつ上がる4音＋上がる風。送り出す合図 | Push origin（↑3） | GitHub_Push_P1（start）、GitHub_Push_P2（start・pitch 0.93） |
| `se_github_commit` | 「ポッ」。丸い三角波の一瞬の下がり音。コミットの丸が1つ飛ぶたび | コミットの丸（1発ごと。とても短い） | GitHub_PushCommit（release） |
| `se_github_conflict` | 「ガガッ・ガガッ・ガーーッ」。半音でぶつかる2つのパルス（E と F）＋低いうなり。にごった衝突 | Merge conflict（<<<<<<< HEAD と >>>>>>> virus がぶつかる） | GitHub_MergeConflict_P2（start） |
| `se_github_revert` | 「シュワッ↑チッ」。逆再生で吸い込まれて、高い音で止まる（置く時も戻る時も同じ音） | Revert（コミットへ巻き戻る） | App_GitHubDesktop_Revert（start） |

## Xcode

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_xcode_build` | 「カ・カ・カッ」。上がる3つの短い四角波＋金属のチッ。ビルドが走り出す | Build 開始（ハンマーのアイコン・進みの表示） | Xcode_Hammer_P1（start）、Xcode_Hammer_P2（start・pitch 0.9） |
| `se_xcode_hammer` | 「ガキィン！」。金属っぽいノイズ＋下がる四角波と三角波の重いドン。ハンマーが床を打つ | Build のハンマーがふり下ろされる（衝撃が出る瞬間） | Xcode_HammerShock（release） |
| `se_xcode_sim` | 「ポ・ポ・ポーン」。やわらかい三角波の上がる3音（自作のメロディ）。端末が立ち上がる | iPhone の Simulator が起動する | Xcode_Simulator_P1（start）、Xcode_Simulator_P2（start・pitch 0.92） |
| `se_xcode_notify` | 「ピポッ」。下がる2音の短い通知。Simulator が1発撃つたび | Simulator の画面から飛ぶ通知（1発ごと。とても短い） | Xcode_SimNotification（release） |
| `se_xcode_warning` | 「ブ・ブ・ブーー↓」。下がる3音（増3和音）の細いパルス＋低いノコギリ波。ビルド失敗 | 999+ warnings・Build Failed | Xcode_Warnings_P2（start） |
| `se_xcode_hammer_skill` | 「ウィーン↑・ガキン！・ピロン♪」。0.35秒のため→金属の一撃→上がる2音（Build Succeeded） | ハンマーでビルド成功（ため 0.35秒に合わせてある） | App_Xcode_Hammer（start） |

## Cursor

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_cursor_ghost` | 「ポロロロン…」。やわらかい三角波の上がるアルペジオにエコー。うっすら現れる気配 | Tab 補完の灰色のゴーストテキスト | Cursor_Tab_P1（start）、Cursor_Tab_P2（start・pitch 0.94） |
| `se_cursor_diff` | 「ピロッ（Accept）・ブブーッ（Reject）」。明るい2音のあと、低いノコギリ波が2回 | Accept / Reject（緑と赤の diff） | Cursor_AcceptReject_P1（start）、Cursor_AcceptReject_P2（start・pitch 0.93） |
| `se_cursor_generating` | 「カタカタカタカタ…」。細いパルスの速い打鍵が12回、だんだん高くなる（0.8秒） | Generating...（コードが1文字ずつ打たれていく） | Cursor_Generating_P2（start） |
| `se_cursor_cmdk` | 「カチッ・キラララン♪」。キーのクリック＋かけ上がる明るい5音にエコー | ⌘K（AI が何か作ってくれる） | App_Cursor_CmdK（start） |

## Safari

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_safari_compass` | 「ピポピポピポ…ピーン」。低い音と、だんだん上がる音が行き来する。針がゆれて止まる | コンパスの針が回ってねらいを定める | Safari_Compass_P1（start）、Safari_Compass_P2（start・pitch 0.93） |
| `se_safari_needle` | 「シュピッ」。細いパルスが一気に下がる＋短いノイズ。針が1本飛ぶたび | コンパスの針が飛ぶ（1発ごと。短い） | Safari_CompassNeedle（release）、Safari_PrivateShot（release・pitch 0.8） |
| `se_safari_tabs` | 「パ・パ・パ・パタン」。下がる4つの四角波。タブが格子に並ぶ | タブ一覧（サムネイルの格子） | Safari_TabGrid_P1（start）、Safari_TabGrid_P2（start・pitch 0.93） |
| `se_safari_private` | 「ヒュゥーーン↓」。こもったノコギリ波と三角波が深く沈む。画面が暗くなる | プライベートブラウズ（暗い画面） | Safari_Private_P2（start） |
| `se_safari_reader` | 「サラッ・キラリーン」。紙をめくるノイズ＋澄んだ上がる3音にエコー。すっきり消える | リーダー表示（余計な物が全部消えて白い紙になる） | App_Safari_Reader（start） |

## Steam

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_steam_sale` | 「チャ・チーン」。チッというノイズ＋低い1音＋高い2音。レジっぽい | SALE の値札（-90%） | Steam_Sale_P1（start・pitch 0.85）、Steam_Sale_P2（start・pitch 0.8） |
| `se_steam_tag` | 「パタッ」。短いノイズ＋低い三角波。値札がめくれる・落ちる軽い音 | 値札が出る瞬間と、大きい -95% の衝撃（同じ合図が2回来るので短い音） | Steam_Sale_P1（release・pitch 0.85）、Steam_Sale_P2（release・pitch 0.8） |
| `se_steam_download` | 「ポ・ポ・ポ・ポ…↑」。1.4秒かけて1音ずつ上がる14段。進みのバーがのびる | ダウンロードの進みのバー（0%→100%） | Steam_Download_P1（start）、Steam_Download_P2（start） |
| `se_steam_blip` | 「ピポッ」。上がる2音＋低いトン。バーが出る時と 100% の瞬間 | ダウンロード開始・100%（同じ合図が2回来るので短い音） | Steam_Download_P1（release）、Steam_Download_P2（release） |
| `se_steam_friend` | 「ポ・ポ・ポン」。下がって上がる三角波の3音（自作）。通知が1枚流れてくるたび | フレンドがオンラインの通知（本物の通知音とは別のメロディ） | Steam_FriendCard（release） |
| `se_steam_achievement` | 「ヒュッ・タララララーン♪」。投げる風のあと、かけ上がる5音のファンファーレにエコー（自作） | 実績解除のトロフィー（本物の通知音とは別のメロディ） | App_Steam_Trophy（start） |

- 音なしの行（中の弾の行で鳴らすので、二重にしない）：Steam_Friends_P2

## ミュージック

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_music_eq` | 「ド・ド・ド・ドン↑」。低い四角波が段々に上がる4音（Em の和音）。棒がせり上がる | イコライザーの棒 | Music_Equalizer_P1（release）、Music_Equalizer_P2（release・pitch 0.94） |
| `se_music_wave` | 「ブォン」。三角波が深く下がる低音。0.6秒おき（ステージのビートと同じ間隔）で鳴る | スピーカーから出る音の波（1発ごと） | Music_WaveShot（release） |
| `se_music_notes` | 「タララッ」。上がる3音（G の和音）。音符が扇に飛ぶ | 音符の弾 ♪（扇に3発） | Music_Shuffle_P2（release）、App_Music_Repeat（release） |
| `se_music_shuffle` | 「シュルッ」。上がる音と下がる音が交差する。交差した矢印 | シャッフル（交差した矢印の印） | Music_Shuffle_P2（start） |
| `se_music_repeat` | 「タラリ・タラリー♪」。同じ3音（A の和音）を2回くり返す明るい音 | リピート（回る矢印。同じ物をもう1回） | App_Music_Repeat（start） |

- 音なしの行（中の弾の行で鳴らすので、二重にしない）：Music_SoundWave_P1、Music_SoundWave_P2

## Spotify

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_spotify_wrapped` | 「タラララララーン↑」。F の和音をかけ上がる6音＋低いノコギリ波＋上がるノイズ。派手な発表 | 年末の Wrapped（大きい数字のカード） | Spotify_Wrapped_P1（start）、Spotify_Wrapped_P2（start・pitch 0.94） |
| `se_spotify_card` | 「ダンッ」。短いノイズ＋下がる四角波。カードがささる | Wrapped のカードが上からささる（出す時と、ささる時に鳴る） | Spotify_Wrapped_P1（release）、Spotify_Wrapped_P2（release・pitch 0.94） |
| `se_spotify_skip` | 「キュッ・ドン」。一瞬かけ上がる音のあと、輪が広がる低い音 | ⏭ スキップ（次へ飛ぶ） | Spotify_Skip_P1（release）、Spotify_Skip_P2（release・pitch 0.94） |
| `se_spotify_shuffle` | 「ピコパコピコパコ…ピン」。高さがばらばらの8音が速く鳴って、最後に1音で止まる | シャッフル（再生キューが混ざる 0.5秒） | Spotify_Queue_P2（start・pitch 0.8） |
| `se_spotify_note` | 「ポン」。三角波の短い1音＋1オクターブ上の細い音。音符が1つ飛ぶたび | 再生キューの音符（1発ごと。短い。ボスは低く、自分のは高く） | Spotify_Queue_P2（release・pitch 0.8）、Spotify_QueueLane（release・pitch 0.8）、App_Spotify_Note（release・pitch 1.5） |
| `se_spotify_queue_add` | 「タ・タ・ターン↑」。C の和音を上がる明るい3音。キューに3つ並ぶ | 再生キューに追加（音符が頭の上に3つ並ぶ） | App_Spotify_NoteQueue（start） |

## Firefox

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_firefox_flame` | 「ボワァッ」。ノイズがふくらんでしぼむ＋低いノコギリ波。火の玉を吐く | 炎のしっぽ | Firefox_TailFlame_P1（start）、Firefox_TailFlame_P2（start・pitch 0.92） |
| `se_firefox_pillar` | 「ゴォッ！」。太く上がるノイズ＋低い三角波。火の柱が立つ | コンテナタブの色の区画に炎の柱（色が出る時と、柱が立つ時に鳴る） | Firefox_Containers_P1（release）、Firefox_Containers_P2（release・pitch 0.93） |
| `se_firefox_pip_pop` | 「プッ・パン」。一瞬上がる音のあと、短い破裂。小さい窓がはがれる・はじける | ピクチャー・イン・ピクチャーの小さい窓（はがれる時と、はじける時） | Firefox_Pip_P2（release） |
| `se_firefox_shield` | 「シュワーン・キラン」。上がるパルス＋高い2音にエコー。盾が張られる | 広告ブロック（盾のアドオン） | App_Firefox_AdBlock（start） |
| `se_firefox_counter` | 「ボシュッ！」。明るい上がるノイズ＋下がるパルス。防いだ弾を火の玉で返す | 広告ブロックの反撃（火の玉） | App_Firefox_Counter（release） |

## Figma

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_figma_join` | 「ポ・ポ・ポッ」。上がる3つの丸い三角波。だれかのカーソルが入ってくる | 共同編集のカーソルが3つ入ってくる | Figma_Cursor_P1（start）、Figma_Cursor_P2（start・pitch 0.94） |
| `se_figma_cursor_dash` | 「カチッ・シュッ」。クリックのノイズ＋細いパルスが一気に下がる。カーソルが突っ込む | カーソルのクリック（1つ突っ込むたび。短い） | Figma_Cursor_P1（release）、Figma_Cursor_P2（release・pitch 0.94） |
| `se_figma_frame` | 「カッ・ニューン・カチ」。クリック→幅が細くなりながら上がるパルス→止めるクリック。ドラッグで枠を広げる | Frame（選択の枠をドラッグで広げる 0.3秒） | Figma_Frame_P1（start）、Figma_Frame_P2（start・pitch 0.93） |
| `se_figma_delete` | 「パシュッ」。下がる2音＋ザラついたノイズ。枠の中が消える | Delete（選択した枠の中を消す） | Figma_Frame_P1（release）、Figma_Frame_P2（release・pitch 0.93） |
| `se_figma_duplicate` | 「ポッ・ポポン」。同じ音が1回→2回と増えて上がる。複製される | ⌘D 複製（1 → 2 → 4 と増える） | Figma_Duplicate_P2（release） |
| `se_figma_pen` | 「カ・カ・カッ・シュイーン」。上がる3つのクリック（アンカー）→線が走る明るい上がり音 | ペンツール（アンカーポイントを3つ打って線で結ぶ） | App_Figma_PenTool（start） |

## Photoshop

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_photoshop_lasso` | 「チ・チ・チ・チ…」。高低が交互の細かい刻みが8回＋ゆっくり上がる sine。点線が流れる | なげなわ選択の、流れる点線 | Photoshop_Lasso_P1（start）、Photoshop_Lasso_P2（start・pitch 0.93） |
| `se_photoshop_delete` | 「ザシュッ」。粗く落ちるノイズ＋下がる2音。市松もようになって消える | 選択範囲を Delete（透明の市松もよう） | Photoshop_Lasso_P1（release）、Photoshop_Lasso_P2（release・pitch 0.93） |
| `se_photoshop_brush` | 「シュワァーッ」。こもったノイズがふくらんでしぼむ（0.5秒）。太いブラシでひと塗り | ブラシで線をなぞる（0.5秒） | Photoshop_Brush_P1（release）、Photoshop_Brush_P2（release・pitch 0.93） |
| `se_photoshop_stamp` | 「チッ・ポコン」。照準のクリック→木のハンコのような低いポン。ピッチで使い分け（ボスは低く、自分は高く） | コピースタンプ（⌥クリックで照準 → ポンと押す） | Photoshop_Clone_P2（start・pitch 0.8）、App_Photoshop_Stamp（start・pitch 1.25） |

## Illustrator

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_illustrator_anchor` | 「カッ…カッ…カッ」。0.2秒おきに上がる3つのクリック（A の和音）。点を1つずつ打つ | ペンツールのアンカーポイント（0.2秒ずつ3つ） | Illustrator_Pen_P1（start）、Illustrator_Pen_P2（start・pitch 0.94） |
| `se_illustrator_stroke` | 「シュイーン」。なめらかに上がる sine＋うすいノコギリ波。きれいな曲線が引かれる | ベジェ曲線のパスがオレンジの線になる | Illustrator_Pen_P1（release）、Illustrator_Pen_P2（release・pitch 0.94） |
| `se_illustrator_fill` | 「ポン」。一瞬上がる三角波＋短い四角波。図形が塗られるたび | スターツールなどの図形の塗り（1つごと。短い） | Illustrator_Shape_P1（release）、Illustrator_Shape_P2（release・pitch 0.94） |
| `se_illustrator_artboard` | 「ブッ・ブーーッ」。増4度下がるノコギリ波の2音＋金属のノイズ。外側が切り捨てられる | アートボードの外は書き出されない | Illustrator_Artboard_P2（start） |
| `se_illustrator_star` | 「キラキラキラーン☆」。E の和音を高くかけ上がる6音にエコー。星がきらめく | スターツール（星を描く） | App_Illustrator_Star（start） |

## Arc

| SE | 音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_arc_swipe` | 「フワァーン↑」。sine と三角波がなめらかに上がる＋やわらかいノイズ。角のない音 | Space の切り替え（グラデーションを横にスワイプ） | Arc_Space_P1（release）、Arc_Space_P2（release・pitch 0.94） |
| `se_arc_window` | 「ポワッ」。sine の上がる2音。小さい窓がふわっと開く | Little Arc（小さい窓が開く） | Arc_LittleArc_P1（start）、Arc_LittleArc_P2（start・pitch 0.94） |
| `se_arc_orb` | 「ピュン」。sine が丸く下がる短い音。窓から1発撃つたび | Little Arc の窓から飛ぶ玉（1発ごと。短い） | Arc_LittleArc_P1（release）、Arc_LittleArc_P2（release・pitch 0.94） |
| `se_arc_archive` | 「シュゥン↓」。三角波がしぼみながら下がる＋うすいノイズ。すっと消される | 使わないタブの自動アーカイブ | Arc_Archive_P2（release） |
| `se_arc_zap` | 「バチッ・ビュイッ！」。金属っぽいノイズ＋細いパルスが一気に下がる＋高い音のまたたき | Zap（ページの物を稲妻で消す） | App_Arc_Zap（start） |
