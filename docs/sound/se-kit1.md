# SE：アプリのキット 1（チャット・仕事・AI の 17 アプリ）

2026-11-19。Slack・Discord・LINE・ChatGPT・Claude・Gemini・Notion・カレンダー・Excel・PowerPoint・Word・Keynote・メモ・メール・メッセージ・Teams・Zoom の、ボスの技と能力の音。

- レシピ：`Tools/audio/se/kit1.json`（86 個）　割り当て：`Assets/Resources/Audio/Maps/20_kit1.json`（115 行）
- 本物のアプリの通知音・着信音・送信音は、まねしていない。音色（木のポコポコ・紙・キラキラなど）で「そのアプリらしさ」を出し、メロディは全部自作。
- ボスの音は低め・にごり、能力の音は高め・明るい。第2形態は、同じ音を `pitch` 0.92 前後で低くして使う。
- 行の `vol` は start と release で共通なので、溜めの音（ハドルの入り・考え中・レーザーの予告）は、レシピの `peak` を 0.55〜0.6 に下げて小さくしてある。

## 鳴る瞬間の決まり（コードを読んで合わせた）

- `start`＝技が始まった瞬間。`release`＝コードが「出た」と知らせた瞬間（`NotifyReleased`）。
- 落ちてくる物・範囲の技は、**予告が出た瞬間**に `release` が来る（当たる瞬間ではない）。だから「現れる音」にしてある。着地・判定の音は、共通の出来事（`faller_land`・`zone_fire`）にまかせる。
- `start` と `release` が同じ瞬間に来る技は、片方だけに音を入れた。
- 中に別の技のデータを持つ技（`KitDressed`・`TalkTypingAttack`・`WallShot`・全方向の弾）は、`release` が**中の技の名前**で来る。その名前の行も足してある（表の「中の技」）。音が要らない中の技は、空の行（fallbacks の音が重ならないように）。
- 見た目の間隔に合わせて作った音（「間隔に合わせてある」と書いた行）は、`pitch` を 1 のままにする（pitch を変えると速さも変わるため）。
- ワープ（`Attack_VSCode_Warp`）は、ほかの担当の共通の技なので、ここでは割り当てていない。

## Slack

音の色：木を叩くような、丸いポコポコ

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_slack_typing` | 入力の欄に字が打たれる、木のキーのコトコト（0.7秒） | @channel を打ちこむ入力欄 | `Attack_Slack_AtChannel_P1`（start）<br>`Attack_Slack_AtChannel_P2`（start） |
| `se_slack_reaction_rain` | 絵文字の札が左から5つ、ポコポコと付いていく（0.15秒おき・だんだん高く） | メッセージに付くリアクションの数字が増える | `Attack_Slack_ReactionRain_P1`（release）<br>`Attack_Slack_ReactionRain_P2`（release） |
| `se_slack_huddle_join` | ヘッドホンを付ける、低めのやわらかい3音（自作のフレーズ） | ハドル（音声の集まり）を始める | `Attack_Slack_Huddle_P2`（start） |
| `se_slack_huddle_ring` | 音の輪が広がる「ボワン」。低い和音がふくらんで消える | ハドルの音の波 | `Attack_Slack_Huddle_P2`（release） |
| `se_slack_thumb` | 親指を投げる、明るい木のポコッ＋上がる2音 | 親指（いいね）のリアクション | `Attack_Ability_Slack_Reaction`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Slack_AtChannel_P1` | `se_slack_typing` | — | 0.55 | 1.0 | @channel（予告の 0.8秒に打つ音） |
| `Attack_Slack_AtChannel_P2` | `se_slack_typing` | — | 0.55 | 0.94 | @channel 第2形態 |
| `Attack_Slack_Mention_Breakpoint_P1`（中の技） | — | — | 1.0 | 1.0 | @channel の中の技（開始と同じ瞬間に出るので鳴らさない） |
| `Attack_Slack_Mention_Breakpoint_P2`（中の技） | — | — | 1.0 | 1.0 | 同上 |
| `Attack_Slack_ReactionRain_P1` | — | `se_slack_reaction_rain` | 0.6 | 1.0 | リアクションの雨（札が付く間隔に合わせてあるので pitch は 1） |
| `Attack_Slack_ReactionRain_P2` | — | `se_slack_reaction_rain` | 0.6 | 1.0 | 同上 |
| `Attack_Slack_Huddle_P2` | `se_slack_huddle_join` | `se_slack_huddle_ring` | 0.85 | 1.0 | ハドル（輪が出るたび＝2回） |
| `Attack_Ability_Slack_Reaction` | — | `se_slack_thumb` | 0.7 | 1.0 | 能力：リアクション |

## Discord

音の色：通話のブリップと、回線のザザッ

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_discord_voice_on` | 話し始めて、ふちが緑に光る。矩形波の短い3音＋回線のザザッ | ボイスチャットで話すと光る緑の枠 | `Attack_Discord_Voice_P1`（start）<br>`Attack_Discord_Voice_P2`（start） |
| `se_discord_voice_wave` | 声の波が飛ぶ「ブォン」。ゆれる低いノコギリ波 | ボイスの音の波 | `Attack_Discord_Voice_P1`（release）<br>`Attack_Discord_Voice_P2`（release） |
| `se_discord_server_list` | 壁に丸いアイコンが3つ並ぶ。丸いブリップ 低・中・高 | 左に並ぶサーバーのアイコン | `Attack_Discord_ServerList_P1`（start）<br>`Attack_Discord_ServerList_P2`（start） |
| `se_discord_server_shot` | 光ったアイコンが横へ飛ぶ。下がる矩形波のピュン（短い） | 選んだサーバーが角丸の四角に変わる | `Attack_Discord_ServerList_P1`（release）<br>`Attack_Discord_ServerList_P2`（release） |
| `se_discord_stamp` | 大きいスタンプが上に現れる、低いポコッ＋ザッ | スタンプの連打 | `Attack_Discord_StampRain_P2`（release） |
| `se_discord_mute` | マイクを切る。明るく下がる3音のあと、ノイズが「プツッ」と切れる | ミュートのボタン（斜線のマイク） | `Attack_App_Discord_Mute`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Discord_Voice_P1` | `se_discord_voice_on` | `se_discord_voice_wave` | 0.6 | 1.0 | ボイス（波を撃つたび） |
| `Attack_Discord_Voice_P2` | `se_discord_voice_on` | `se_discord_voice_wave` | 0.6 | 0.92 | ボイス 第2形態（3連） |
| `Attack_Discord_ServerList_P1` | `se_discord_server_list` | `se_discord_server_shot` | 0.55 | 1.0 | サーバー一覧（アイコンが飛ぶたび） |
| `Attack_Discord_ServerList_P2` | `se_discord_server_list` | `se_discord_server_shot` | 0.55 | 0.92 | サーバー一覧 第2形態 |
| `Attack_Discord_StampRain_P2` | — | `se_discord_stamp` | 0.55 | 1.0 | スタンプ連打（スタンプが上に出るたび） |
| `Attack_App_Discord_Mute` | — | `se_discord_mute` | 0.8 | 1.0 | 能力：ミュート |

## LINE

音の色：スタンプの、かわいい「ぽよん」

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_line_stamp_boing` | スタンプが上に現れる「ぽよっ」。上がって少し戻る丸い音（短い） | スタンプの連打 | `Attack_LINE_StampBarrage_P1`（release）<br>`Attack_LINE_StampBarrage_P2`（release） |
| `se_line_bubble` | 吹き出しが「ぷるん」と出る。下がって上がるやわらかい音 | トークの吹き出し | `Attack_LINE_Talk_PopupAppear_P1`（release）<br>`Attack_LINE_Talk_PopupAppear_P2`（release） |
| `se_line_angry` | 吹き出しが赤く怒り出す「ムカムカ…」。ふるえながら上がる低い矩形波 | 既読スルーに怒ったメッセージ | `Attack_LINE_Angry_P2`（release） |
| `se_line_stamp_press` | スタンプを「ぽよっ・ドン！」と押す。明るいぽよんのあと 0.22秒で重い判子の音 | スタンプを送る | `Attack_Ability_LINE_Stamp`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_LINE_StampBarrage_P1` | — | `se_line_stamp_boing` | 0.5 | 1.0 | スタンプ連打（スタンプが上に出るたび） |
| `Attack_LINE_StampBarrage_P2` | — | `se_line_stamp_boing` | 0.5 | 0.92 | スタンプ連打 第2形態（5個） |
| `Attack_LINE_Talk_PopupAppear_P1` | — | `se_line_bubble` | 0.55 | 1.0 | トークの吹き出し |
| `Attack_LINE_Talk_PopupAppear_P2` | — | `se_line_bubble` | 0.55 | 0.92 | トークの吹き出し 第2形態 |
| `Attack_LINE_Angry_P2` | — | `se_line_angry` | 0.85 | 1.0 | 怒りのメッセージ（3つ置いたとき） |
| `Attack_Ability_LINE_Stamp` | — | `se_line_stamp_press` | 0.8 | 1.0 | 能力：スタンプ（0.25秒後の判定に「ドン」を合わせてあるので pitch は 1） |

## ChatGPT

音の色：字が流れて出てくるチチチチ

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_chatgpt_stream` | 長い回答が下から上へ流れる。低めの細かいチチチチ（0.4秒） | 1文字ずつ出てくる回答（ストリーミング） | `Attack_ChatGPT_LongAnswer_Breakpoint_P1`（release）<br>`Attack_ChatGPT_Regenerate_P2`（release） |
| `se_chatgpt_regen` | 回る矢印。三角波のアルペジオが2周する | Regenerate（もう一度生成）のボタン | `Attack_ChatGPT_Regenerate_P2`（start） |
| `se_chatgpt_glitch_write` | ウソの分身が1行ずつ書かれて現れる。文字化けのザラザラしたチチチ（0.6秒） | ハルシネーション（もっともらしいウソ） | `Attack_ChatGPT_Hallucination_P1`（start）<br>`Attack_ChatGPT_Hallucination_P2`（start） |
| `se_chatgpt_word_shot` | 言葉の弾を扇に撃つ。低い矩形波の3連ブリップ | 回答の言葉が飛んでくる | `Attack_ChatGPT_Hallucination_P1`（release）<br>`Attack_ChatGPT_Hallucination_P2`（release） |
| `se_chatgpt_think` | 考え中の点が3つ「ポ・ポ・ポ」（0.5秒ちょうど） | 回答を待つ間の点（…） | `Attack_App_ChatGPT_Prompt`（start） |
| `se_chatgpt_prompt` | プロンプトの回答が一気に流れる。明るく駆け上がるチチチチ＋最後に「ピン」 | プロンプトを送ると長文が返ってくる | `Attack_App_ChatGPT_Prompt`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_ChatGPT_LongAnswer_Breakpoint_P1` | — | `se_chatgpt_stream` | 0.6 | 1.0 | 長文回答（第1・第2形態で同じ技） |
| `Attack_ChatGPT_Hallucination_P1` | `se_chatgpt_glitch_write` | `se_chatgpt_word_shot` | 0.6 | 1.0 | ハルシネーション（現れる → 撃つ） |
| `Attack_ChatGPT_Hallucination_P2` | `se_chatgpt_glitch_write` | `se_chatgpt_word_shot` | 0.6 | 0.92 | ハルシネーション 第2形態 |
| `Attack_ChatGPT_Regenerate_P2` | `se_chatgpt_regen` | `se_chatgpt_stream` | 0.6 | 0.9 | Regenerate（柱の予告が出るたび＝3回） |
| `Attack_App_ChatGPT_Prompt` | `se_chatgpt_think` | `se_chatgpt_prompt` | 0.85 | 1.0 | 能力：プロンプト（考え中 0.5秒 → 柱） |
| `Attack_App_ChatGPT_Prompt_Breakpoint`（中の技） | — | — | 1.0 | 1.0 | プロンプトの中の柱（上の release と同じ瞬間なので鳴らさない） |

## Claude

音の色：あたたかい火花と、やわらかいチャイム（三角波の5度）

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_claude_charge` | 火花が集まる。あたたかい三角波が5度で重なって上がっていく（0.7秒） | 回答の前にまたたくスパークの印 | `Attack_Claude_Spark_P1`（start）<br>`Attack_Claude_Spark_P2`（start） |
| `se_claude_spark` | 火花が8方向にはじける。三角波の和音のひと突き＋パチッ | オレンジのスパーク（8本の線の星） | `Attack_Claude_Spark_P1`（release）<br>`Attack_Claude_Spark_P2`（release） |
| `se_claude_panel` | 回答のパネルが「フワッ」と開く。低めのやわらかい2音＋紙のような空気 | Artifacts のパネルが横に開く | `Attack_Claude_PopupAppear_P1`（release） |
| `se_claude_think` | じっくり考える。火花がゆっくり回る、あたたかい「コロ…コロ…」（1.4秒） | 拡張思考（じっくり考える） | `Attack_Claude_Thinking_P2`（start） |
| `se_claude_think_done` | 考え終わった。あたたかい和音の「ポーン」＋低い一打 | 考えがまとまって回答が始まる | `Attack_Claude_Thinking_P2`（release） |
| `se_claude_artifact` | アーティファクトのパネルが開く。明るくあたたかい上がる和音＋火花のチリッ | アーティファクト（作った物が横のパネルに出る） | `Attack_App_Claude_Artifact`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Claude_Spark_P1` | `se_claude_charge` | `se_claude_spark` | 0.65 | 1.0 | スパーク（撃つたび＝2回） |
| `Attack_Claude_Spark_P2` | `se_claude_charge` | `se_claude_spark` | 0.65 | 0.92 | スパーク 第2形態（3回） |
| `Attack_Claude_PopupAppear_P1` | — | `se_claude_panel` | 0.55 | 1.0 | Artifacts のパネル（第1・第2形態で同じ技） |
| `Attack_Claude_Thinking_P2` | `se_claude_think` | `se_claude_think_done` | 0.8 | 1.0 | じっくり考える（2秒考える → 考え終わり） |
| `Attack_App_Claude_Artifact` | — | `se_claude_artifact` | 0.75 | 1.0 | 能力：アーティファクト |

## Gemini

音の色：細いパルスのキラキラ（星）

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_gemini_shuffle` | 3つの回答案がチカチカ点滅する。細いパルスのキラキラが行ったり来たり（0.6秒） | 回答案 1・2・3 のどれかを選ぶ | `Attack_Gemini_Drafts_P1`（start）<br>`Attack_Gemini_Drafts_P2`（start） |
| `se_gemini_decide` | 安全な区画が決まる。星がまたたく3音＋低い一打 | 回答案が1つに決まる | `Attack_Gemini_Drafts_P1`（release）<br>`Attack_Gemini_Drafts_P2`（release） |
| `se_gemini_twin` | 反対側に「ふたご」が現れる。上がるキラキラと、鏡うつしに下がるキラキラ（0.6秒） | ふたご座（Gemini の名前の元） | `Attack_Gemini_Twin_P1`（start）<br>`Attack_Gemini_Twin_P2`（start） |
| `se_gemini_star_shot` | 星の弾。とても短い「チリッ」（予告と発射の2回鳴るので軽く） | 星の形の弾 | `Attack_Gemini_Twin_P1`（release）<br>`Attack_Gemini_Twin_P2`（release） |
| `se_gemini_image` | 絵が生成されて上に現れる。ざらざらが晴れていくノイズ＋上がるきらめき | 画像生成（ノイズから絵が出てくる） | `Attack_Gemini_Image_P2`（release） |
| `se_gemini_twinstar` | 自分と左右に星の柱。明るい2つの星が同時にきらめく（少しずれた2声） | ふたご星（2つ並んだ星） | `Attack_App_Gemini_TwinStar`（start） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Gemini_Drafts_P1` | `se_gemini_shuffle` | `se_gemini_decide` | 0.65 | 1.0 | 3つの回答案（点滅 → 決まる） |
| `Attack_Gemini_Drafts_P2` | `se_gemini_shuffle` | `se_gemini_decide` | 0.65 | 0.94 | 回答案 第2形態（2回決まる） |
| `Attack_Gemini_Twin_P1` | `se_gemini_twin` | `se_gemini_star_shot` | 0.45 | 1.0 | ふたご座（1発につき予告と発射の2回鳴る） |
| `Attack_Gemini_Twin_P2` | `se_gemini_twin` | `se_gemini_star_shot` | 0.45 | 0.92 | ふたご座 第2形態 |
| `Attack_Gemini_Image_P2` | — | `se_gemini_image` | 0.6 | 1.0 | 画像生成（絵が上に出るたび） |
| `Attack_App_Gemini_TwinStar` | `se_gemini_twinstar` | — | 0.75 | 1.0 | 能力：ふたご星（release は中の技の名前で来るので、start で鳴らす） |
| `Attack_App_Gemini_TwinStar_Inner`（中の技） | — | — | 1.0 | 1.0 | ふたご星の中の技 |

## Notion

音の色：ブロックの乾いた「カチャッ」

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_notion_block` | 白いブロックが上に現れる「カチャッ」。乾いたクリック＋低い矩形波（短い） | ブロックを積む・並べ替える | `Attack_Notion_BlockStack_P1`（release）<br>`Attack_Notion_BlockStack_P2`（release） |
| `se_notion_toggle` | ▶ が ▼ に開く。カチッのあと、中身が下から出てくる上がる音 | トグル（開くと中身が出る行） | `Attack_Notion_Toggle_P1`（start）<br>`Attack_Notion_Toggle_P2`（start） |
| `se_notion_view` | 上のタブが Table から Board に変わる。カチ・カチッ（0.4秒あけて2回） | ビューの切り替え（テーブル → ボード） | `Attack_Notion_ViewSwitch_P2`（start） |
| `se_notion_cards` | カードの束が列ごとに並ぶ。パラパラパラッと4枚 | ボードのカード | `Attack_Notion_ViewSwitch_P2`（release） |
| `se_notion_block_place` | ブロックを「カチャッ」と置く。明るいクリック＋上がる2音 | / で新しいブロックを足す | `Attack_Notion_Skill_Block`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Notion_BlockStack_P1` | — | `se_notion_block` | 0.5 | 1.0 | ブロックを積む（ブロックが上に出るたび） |
| `Attack_Notion_BlockStack_P2` | — | `se_notion_block` | 0.5 | 0.92 | ブロックを積む 第2形態 |
| `Attack_Notion_Toggle_P1` | `se_notion_toggle` | — | 0.6 | 1.0 | トグル（release は中の技の名前で来るので、start で鳴らす） |
| `Attack_Notion_Toggle_P2` | `se_notion_toggle` | — | 0.6 | 0.92 | トグル 第2形態 |
| `Attack_Notion_Toggle_Area_P1`（中の技） | — | — | 1.0 | 1.0 | トグルの中の技 |
| `Attack_Notion_Toggle_Area_P2`（中の技） | — | — | 1.0 | 1.0 | 同上 |
| `Attack_Notion_ViewSwitch_P2` | `se_notion_view` | `se_notion_cards` | 0.65 | 1.0 | ビューの切り替え（タブ 0.8秒 → 列の予告） |
| `Attack_Notion_Skill_Block` | — | `se_notion_block_place` | 0.75 | 1.0 | 能力：ブロック |

## カレンダー

音の色：ページをめくる音と、時計のチクタク

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_calendar_flip` | 予定のブロックが曜日の列に並ぶ。ページをめくる「パラッ」＋小さいチッチッ | カレンダーのページをめくる | `Attack_Calendar_Blocks_P1`（release）<br>`Attack_Calendar_Blocks_P2`（release） |
| `se_calendar_deadline` | 締め切りが近づく。同じ間で続く時計のチク・タク＋低い音がじわじわ上がる（1.4秒） | 今の時刻の赤い線が締め切りに届く | `Attack_Calendar_Deadline`（release） |
| `se_calendar_repeat` | 毎週の予定がまた出る。チッ・チッ＋低いベルの一打 | くり返しの予定（毎週） | `Attack_Calendar_Weekly_P2`（release） |
| `se_calendar_event_set` | 足元に予定を入れる。明るい「ピコッ」＋小さいベル2打（自作のリズム） | 予定を入れる（通知の前のリマインダー） | `Attack_App_Calendar_Event`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Calendar_Blocks_P1` | — | `se_calendar_flip` | 0.6 | 1.0 | 予定ブロック |
| `Attack_Calendar_Blocks_P2` | — | `se_calendar_flip` | 0.6 | 0.92 | 予定ブロック 第2形態 |
| `Attack_Calendar_Deadline` | — | `se_calendar_deadline` | 0.85 | 1.0 | 締め切りの線（予告 2.5秒の前半に鳴る） |
| `Attack_Calendar_Weekly_P2` | — | `se_calendar_repeat` | 0.6 | 1.0 | 毎週の予定（波ごと＝3回） |
| `Attack_App_Calendar_Event` | — | `se_calendar_event_set` | 0.7 | 1.0 | 能力：予定を入れる（置いた瞬間。3秒後の判定は別の音） |

## Excel

音の色：セルのカチカチと、電卓のピッピッ

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_excel_fill` | 右下の ■ を引っぱる。セルが1つずつ埋まるカチカチが上がっていく（0.45秒） | オートフィル（1・2・3…と続きが入る） | `Attack_Excel_Autofill_P1`（start）<br>`Attack_Excel_Autofill_P2`（start） |
| `se_excel_ref_error` | セルが赤い #REF! に変わる。にごった低い矩形波の「ブブッ」（自作。OS の警告音ではない） | #REF! のエラー | `Attack_Excel_Autofill_P1`（release）<br>`Attack_Excel_Autofill_P2`（release） |
| `se_excel_bars` | 棒グラフが予告される。低い矩形波が階段のように上がる4音 | 棒グラフ | `Attack_Excel_BarGraph_P1`（release）<br>`Attack_Excel_BarGraph_P2`（release） |
| `se_excel_pie_cut` | 円グラフが6つに切り分けられる。金属っぽい「シャキ・シャキ・シャキ」 | 円グラフ | `Attack_Excel_Pie_P2`（start） |
| `se_excel_pie_shot` | 切れ端が6方向へ飛ぶ。低い矩形波の「バラッ」 | 円グラフの切れ端 | `Attack_Excel_Pie_TabShuriken_Slice`（release） |
| `se_excel_autofill_skill` | セルが8マス伸びる。電卓のような明るいピッが0.05秒おきに8つ上がり、最後に「チーン」 | オートフィル（遠いほど大きい数字） | `Attack_Excel_AutofillSkill`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Excel_Autofill_P1` | `se_excel_fill` | `se_excel_ref_error` | 0.6 | 1.0 | オートフィル（引っぱる → 赤い #REF! に変わるたび） |
| `Attack_Excel_Autofill_P2` | `se_excel_fill` | `se_excel_ref_error` | 0.6 | 0.92 | オートフィル 第2形態 |
| `Attack_Excel_BarGraph_P1` | — | `se_excel_bars` | 0.65 | 1.0 | 棒グラフ |
| `Attack_Excel_BarGraph_P2` | — | `se_excel_bars` | 0.65 | 0.92 | 棒グラフ 第2形態（6本） |
| `Attack_Excel_Pie_P2` | `se_excel_pie_cut` | — | 0.6 | 1.0 | 円グラフ（切り分ける。release は中の弾の名前で来る） |
| `Attack_Excel_Pie_TabShuriken_Slice`（中の技） | — | `se_excel_pie_shot` | 0.65 | 1.0 | 円グラフの中の弾（飛ぶたび＝2回） |
| `Attack_Excel_AutofillSkill` | — | `se_excel_autofill_skill` | 0.75 | 1.0 | 能力：オートフィル（1マス 0.05秒に合わせてあるので pitch は 1） |

## PowerPoint

音の色：スライドが切り替わる「シュッ」

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_ppt_bullet_dots` | 「•」が上から3つ出る。ポッ・ポッ・ポッ（0.25秒おき・高→中→低） | 箇条書き | `Attack_PowerPoint_Bullets_P1`（start）<br>`Attack_PowerPoint_Bullets_P2`（start） |
| `se_ppt_line_shot` | 文の行が横へ飛ぶ。短い「シュッ」＋低いブリップ | 箇条書きの行がアニメーションで入ってくる | `Attack_PowerPoint_BulletLine`（release） |
| `se_ppt_slide` | スライドが上に現れる。画面が切り替わる「シュワッ」＋低い「トン」 | スライドの切り替え（トランジション） | `Attack_PowerPoint_SlideDrop_P1`（release）<br>`Attack_PowerPoint_SlideDrop_P2`（release） |
| `se_ppt_flyin` | 大きい矢印が壁から壁へ横切る。太い「ブォーッ」（音程が下がりながら通りすぎる） | フライイン（横から飛んでくるアニメーション） | `Attack_PowerPoint_FlyInArrow`（release） |
| `se_ppt_flyin_skill` | 後ろの壁から矢印が飛んでくる。明るく速い「シュバッ」＋上がるきらめき | フライイン | `Attack_PowerPoint_FlyInSkill`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_PowerPoint_Bullets_P1` | `se_ppt_bullet_dots` | — | 0.55 | 1.0 | 箇条書き（「•」が 0.25秒おきに出るのに合わせてあるので pitch は 1） |
| `Attack_PowerPoint_Bullets_P2` | `se_ppt_bullet_dots` | — | 0.55 | 1.0 | 同上 |
| `Attack_PowerPoint_BulletLine`（中の技） | — | `se_ppt_line_shot` | 0.55 | 1.0 | 箇条書きの中の弾（行が飛ぶたび） |
| `Attack_PowerPoint_SlideDrop_P1` | — | `se_ppt_slide` | 0.6 | 1.0 | スライド落下（スライドが上に出るたび） |
| `Attack_PowerPoint_SlideDrop_P2` | — | `se_ppt_slide` | 0.6 | 0.92 | スライド落下 第2形態 |
| `Attack_PowerPoint_FlyIn_P2` | `se_gen_warn_beep` | — | 0.5 | 1.0 | フライイン（始まりは共通の警告音。release は中の弾の名前で来る） |
| `Attack_PowerPoint_FlyInArrow`（中の技） | — | `se_ppt_flyin` | 0.85 | 1.0 | フライインの中の弾（矢印が飛ぶたび＝4回） |
| `Attack_PowerPoint_FlyInSkill` | — | `se_ppt_flyin_skill` | 0.8 | 1.0 | 能力：フライイン |

## Word

音の色：タイプライターのカタカタ

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_word_type` | 文の行が打たれる。タイプライターのカタカタ＋行の終わりの小さいチン（0.5秒） | 文を打つ | `Attack_Word_Strikethrough_P1`（start）<br>`Attack_Word_Strikethrough_P2`（start） |
| `se_word_strike` | 太い赤い取り消し線が「シュッ」と引かれる。鋭く短い | 取り消し線 | `Attack_Word_Strikethrough_P1`（release）<br>`Attack_Word_Strikethrough_P2`（release） |
| `se_word_comment` | 黄色いコメントが余白から「ペタッ」と出る。付せんの音＋低い2音 | 余白のコメント（校閲） | `Attack_Word_Comment_P1`（start）<br>`Attack_Word_Comment_P2`（start） |
| `se_word_layout` | 大きい画像が入って文がくずれる。ガガッ・ガッと引っかかる低い音 | 画像を入れたらレイアウトがくずれる | `Attack_Word_Layout_P2`（release） |
| `se_word_bold` | 太字にする。太く明るい和音の「ドン！」＋タイプライターのチン | 太字（B のボタン） | `Attack_Word_BoldSkill`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Word_Strikethrough_P1` | `se_word_type` | `se_word_strike` | 0.65 | 1.0 | 取り消し線（線が引かれるたび＝3回） |
| `Attack_Word_Strikethrough_P2` | `se_word_type` | `se_word_strike` | 0.65 | 0.94 | 取り消し線 第2形態（5回） |
| `Attack_Word_Comment_P1` | `se_word_comment` | — | 0.55 | 1.0 | コメント（release は中の技の名前で来るので、start で鳴らす） |
| `Attack_Word_Comment_P2` | `se_word_comment` | — | 0.55 | 0.92 | コメント 第2形態 |
| `Attack_Word_Comment_Inner_P1`（中の技） | — | — | 1.0 | 1.0 | コメントの中の技 |
| `Attack_Word_Comment_Inner_P2`（中の技） | — | — | 1.0 | 1.0 | 同上 |
| `Attack_Word_Layout_P2` | — | `se_word_layout` | 0.8 | 1.0 | レイアウトくずれ（画像が上に出たとき） |
| `Attack_Word_BoldSkill` | — | `se_word_bold` | 0.85 | 1.0 | 能力：太字 |

## Keynote

音の色：なめらかなサイン波の移動と、拍手のようなノイズ

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_keynote_pop` | 大きい丸がボスの横に「ポワッ」と出る。低いやわらかいサイン波 | スライドに図形が現れる | `Attack_Keynote_MagicMove_P1`（start）<br>`Attack_Keynote_MagicMove_P2`（start） |
| `se_keynote_glide` | 丸がなめらかに行き先へ動く。ゆっくり入ってゆっくり止まる「スゥーン」（0.35秒） | マジックムーブ（形がなめらかに移る） | `Attack_Keynote_MagicMove_P1`（release）<br>`Attack_Keynote_MagicMove_P2`（release） |
| `se_keynote_number` | 大きい数字が上に現れる「デン」。太い低いパルス | 発表で出す大きい数字 | `Attack_Keynote_BigNumbers_P1`（release）<br>`Attack_Keynote_BigNumbers_P2`（release） |
| `se_keynote_hush` | 会場が暗くなって静まる。低い音がふくらむ＋ざわめきが引いていく（0.8秒） | 「One more thing...」の前の間 | `Attack_Keynote_OneMoreThing_P2`（start） |
| `se_keynote_stars` | 星が12方向へ飛ぶ。低い和音の一打＋拍手のようなパラパラ | 発表の拍手 | `Attack_Keynote_Star`（release） |
| `se_keynote_dash` | なめらかに 5マス動く。明るい「スゥィーン」＋着いた所のきらめき | マジックムーブ | `Attack_Keynote_DashSkill`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Keynote_MagicMove_P1` | `se_keynote_pop` | `se_keynote_glide` | 0.7 | 1.0 | マジックムーブ（丸が動くたび。動く 0.35秒に合わせてあるので pitch は 1） |
| `Attack_Keynote_MagicMove_P2` | `se_keynote_pop` | `se_keynote_glide` | 0.7 | 1.0 | 同上（5回） |
| `Attack_Keynote_BigNumbers_P1` | — | `se_keynote_number` | 0.6 | 1.0 | 大きい数字（数字が上に出るたび） |
| `Attack_Keynote_BigNumbers_P2` | — | `se_keynote_number` | 0.6 | 0.92 | 大きい数字 第2形態 |
| `Attack_Keynote_OneMoreThing_P2` | `se_keynote_hush` | — | 0.8 | 1.0 | One more thing...（暗くなる 0.8秒。release は中の弾の名前で来る） |
| `Attack_Keynote_Star`（中の技） | — | `se_keynote_stars` | 0.85 | 1.0 | One more thing... の中の弾（12発が飛ぶたび＝2回） |
| `Attack_Keynote_DashSkill` | — | `se_keynote_dash` | 0.75 | 1.0 | 能力：マジックムーブ |

## メモ

音の色：えんぴつのカリカリと紙の音

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_notes_scribble` | 黄色い紙が上に並ぶ。えんぴつのカリカリ（行ったり来たり）＋紙のパサッ（0.45秒） | メモを走り書きする | `Attack_Notes_Paper_P1`（start）<br>`Attack_Notes_Paper_P2`（start） |
| `se_notes_memo` | 隅からメモの窓がせり出す。紙がすべる「スッ」＋えんぴつのトン | クイックメモ（画面の隅から出すメモ） | `Attack_Notes_QuickMemo`（release） |
| `se_notes_lock` | 鍵がカチャカチャ揺れて、0.9秒で「ガチャン」と閉まる | メモに鍵をかける（ロック） | `Attack_Notes_Lock_P2`（release） |
| `se_notes_pin` | 画びょうを投げる。明るい「ピッ」＋紙に刺さる「トッ」 | メモをピンで留める | `Attack_Notes_Skill_Pin`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Notes_Paper_P1` | `se_notes_scribble` | — | 0.55 | 1.0 | メモの紙（release は中の技の名前で来るので、start で鳴らす） |
| `Attack_Notes_Paper_P2` | `se_notes_scribble` | — | 0.55 | 0.94 | メモの紙 第2形態（7枚） |
| `Attack_Notes_Paper_Fall_P1`（中の技） | — | — | 1.0 | 1.0 | メモの紙の中の技 |
| `Attack_Notes_Paper_Fall_P2`（中の技） | — | — | 1.0 | 1.0 | 同上 |
| `Attack_Notes_QuickMemo` | — | `se_notes_memo` | 0.6 | 1.0 | クイックメモ（第1・第2形態で同じ技） |
| `Attack_Notes_Lock_P2` | — | `se_notes_lock` | 0.85 | 1.0 | 鍵をかける（予告 0.9秒の終わりに「ガチャン」を合わせてあるので pitch は 1） |
| `Attack_Notes_Skill_Pin` | — | `se_notes_pin` | 0.7 | 1.0 | 能力：ピン留め |

## メール

音の色：紙飛行機（紙のはためき）

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_mail_fold` | 紙飛行機が3つ並ぶ。紙を折る「カサ・カサ・カサ」 | 送信の印の紙飛行機 | `Attack_Mail_Send_P1`（start）<br>`Attack_Mail_Send_P2`（start） |
| `se_mail_plane` | 紙飛行機が飛ぶ。紙がはためきながら、ふわっと上がって下がる短い音 | 紙飛行機 | `Attack_Mail_Send_P1`（release）<br>`Attack_Mail_Send_P2`（release） |
| `se_mail_spam_drop` | 迷惑メールの封筒が3通ポトポト落ちてくる＋にごった低い2音 | 迷惑メール | `Attack_Mail_Spam_P1`（release）<br>`Attack_Mail_Spam_P2`（release） |
| `se_mail_reply_all` | 全部の封筒がいっせいに返信する。紙のはためきが重なった「バサバサッ」（予告と発射の2回鳴る） | 全員に返信 | `Attack_Mail_ReplyAll_P2`（release） |
| `se_mail_send_skill` | 紙飛行機を投げる。明るく伸びていく「ヒュゥーッ」＋小さいきらめき | 送信 | `Attack_App_Mail_Send`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Mail_Send_P1` | `se_mail_fold` | `se_mail_plane` | 0.5 | 1.0 | 送信（紙飛行機が飛ぶたび＝3回） |
| `Attack_Mail_Send_P2` | `se_mail_fold` | `se_mail_plane` | 0.5 | 0.92 | 送信 第2形態 |
| `Attack_Mail_Spam_P1` | — | `se_mail_spam_drop` | 0.6 | 1.0 | 迷惑メール |
| `Attack_Mail_Spam_P2` | — | `se_mail_spam_drop` | 0.6 | 0.92 | 迷惑メール 第2形態（「全員に返信」が先に出すときも鳴る） |
| `Attack_Mail_ReplyAll_P2` | — | `se_mail_reply_all` | 0.7 | 1.0 | 全員に返信（予告の線が出たとき・撃ったときの2回） |
| `Attack_App_Mail_Send` | — | `se_mail_send_skill` | 0.7 | 1.0 | 能力：送信 |

## メッセージ

音の色：吹き出しのふくらみ・花火・レーザー

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_messages_slam` | 大きい吹き出しが上でふくらむ「ブゥン」。低い音が下がりながら力をためる | スラム（ドンと落ちるメッセージのエフェクト） | `Attack_Messages_Slam_P1`（release）<br>`Attack_Messages_Slam_P2`（release） |
| `se_messages_firework` | 花火が開く。低い「ドン」＋パチパチと散る火の粉 | 花火のエフェクト | `Attack_Messages_Fireworks_P1`（release）<br>`Attack_Messages_Fireworks_P2`（release） |
| `se_messages_firework_skill` | 花火が開く（プレイヤー）。明るい「ポン」＋上がるきらめきとパチパチ | 花火のエフェクト | `Attack_Ability_Messages_Firework`（release） |
| `se_messages_laser_charge` | レーザーの予告。2つの音が少しずつ上がりながらうなる（0.8秒） | レーザーのエフェクト | `Attack_Messages_Laser_P2`（start） |
| `se_messages_laser` | 緑と紫の光の帯が床をなぞる。2色のうなるビーム（1.4秒・だんだん弱く） | レーザーのエフェクト | `Attack_Messages_Laser_P2`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Messages_Slam_P1` | — | `se_messages_slam` | 0.8 | 1.0 | スラム（吹き出しが上に出るたび。着地は共通の音） |
| `Attack_Messages_Slam_P2` | — | `se_messages_slam` | 0.8 | 0.92 | スラム 第2形態（2連続） |
| `Attack_Messages_Fireworks_P1` | — | `se_messages_firework` | 0.7 | 1.0 | 花火（輪を置いたとき1回＋開くたび） |
| `Attack_Messages_Fireworks_P2` | — | `se_messages_firework` | 0.55 | 0.92 | 花火 第2形態（5か所） |
| `Attack_Messages_Laser_P2` | `se_messages_laser_charge` | `se_messages_laser` | 0.9 | 1.0 | レーザー（予告 0.8秒 → 帯が出る） |
| `Attack_Ability_Messages_Firework` | — | `se_messages_firework_skill` | 0.7 | 1.0 | 能力：花火（開くたび＝3回） |

## Microsoft Teams

音の色：手を挙げる「ピョコ」とベル、木琴の「ポロン」

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_teams_share` | 画面共有の枠が出る。枠がはまる「カシャ」＋低い矩形波の3音（自作。着信・参加の音ではない） | 画面共有（赤い枠） | `Attack_Teams_Share_P1`（release）<br>`Attack_Teams_Share_P2`（release） |
| `se_teams_hand` | 床から手が挙がる予告。低い「ニョキッ」と上がる音＋にごった小さいベル | 挙手（手を挙げるボタン） | `Attack_Teams_RaiseHand_P1`（start）<br>`Attack_Teams_RaiseHand_P2`（start） |
| `se_teams_joined` | 参加者のタイルが6枚並ぶ。木琴のような「ポロン」が0.15秒おきに6つ（自作の並び） | 「〜さんが参加しました」 | `Attack_Teams_Joined_TabMultiply_P2`（release） |
| `se_teams_hand_skill` | 真上に大きい手を挙げる。明るい「ピョコッ」＋澄んだベルの一打 | 挙手 | `Attack_Ability_Teams_RaiseHand`（start） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Teams_Share_P1` | — | `se_teams_share` | 0.7 | 1.0 | 画面共有（枠が出るたび） |
| `Attack_Teams_Share_P2` | — | `se_teams_share` | 0.7 | 0.92 | 画面共有 第2形態（2回） |
| `Attack_Teams_RaiseHand_P1` | `se_teams_hand` | — | 0.6 | 1.0 | 挙手（release は中の技の名前で来るので、start で鳴らす） |
| `Attack_Teams_RaiseHand_P2` | `se_teams_hand` | — | 0.6 | 0.92 | 挙手 第2形態（5本） |
| `Attack_Teams_HandColumn_P1`（中の技） | — | — | 1.0 | 1.0 | 挙手の中の技 |
| `Attack_Teams_HandColumn_P2`（中の技） | — | — | 1.0 | 1.0 | 同上 |
| `Attack_Teams_Joined_TabMultiply_P2` | — | `se_teams_joined` | 0.7 | 1.0 | 参加しました（タイルが 0.15秒おきに並ぶのに合わせてあるので pitch は 1） |
| `Attack_Ability_Teams_RaiseHand` | `se_teams_hand_skill` | — | 0.75 | 1.0 | 能力：挙手（release は中の技の名前で来るので、start で鳴らす） |
| `Attack_Ability_Teams_HandColumn`（中の技） | — | — | 1.0 | 1.0 | 能力の挙手の中の技 |

## Zoom

音の色：カメラの「カシャ」と、退出の「ブッ」

| SE | どんな音 | 元ネタ | 使う技 |
|---|---|---|---|
| `se_zoom_gallery` | 部屋が6つのタイルに分かれる。線が走る「ジッ」＋カメラのシャッター「カシャ」 | ギャラリービュー（参加者のタイル） | `Attack_Zoom_Gallery_P1`（release）<br>`Attack_Zoom_Gallery_P2`（release） |
| `se_zoom_leave` | 赤い「退出」ボタンが上に現れる。低くにごった「ブブッ」（短い） | 退出ボタン（ミーティングを抜ける） | `Attack_Zoom_Leave_P1`（release）<br>`Attack_Zoom_Leave_P2`（release） |
| `se_zoom_clap` | 拍手の絵文字が5つ並ぶ。拍手のパチパチパチ（ばらばらの間で7回） | リアクションの拍手 | `Attack_Zoom_ClapStorm_P2`（release） |
| `se_zoom_lockon` | 画面の敵にピントを合わせて撃つ。明るいピピピのあと、0.45秒で「カシャッ」とレーザー | ギャラリービュー（話している人に緑の枠） | `Attack_App_Zoom`（release） |

| 技 | start | release | vol | pitch | メモ |
|---|---|---|---|---|---|
| `Attack_Zoom_Gallery_P1` | — | `se_zoom_gallery` | 0.7 | 1.0 | ギャラリービュー（タイルに分かれたとき） |
| `Attack_Zoom_Gallery_P2` | — | `se_zoom_gallery` | 0.7 | 0.92 | ギャラリービュー 第2形態 |
| `Attack_Zoom_Leave_P1` | — | `se_zoom_leave` | 0.6 | 1.0 | 退出ボタン（ボタンが上に出るたび） |
| `Attack_Zoom_Leave_P2` | — | `se_zoom_leave` | 0.6 | 0.92 | 退出ボタン 第2形態 |
| `Attack_Zoom_ClapStorm_P2` | — | `se_zoom_clap` | 0.7 | 1.0 | リアクションの嵐 |
| `Attack_App_Zoom` | — | `se_zoom_lockon` | 0.8 | 1.0 | 能力：ギャラリービュー（予告 0.45秒に合わせてあるので pitch は 1） |
