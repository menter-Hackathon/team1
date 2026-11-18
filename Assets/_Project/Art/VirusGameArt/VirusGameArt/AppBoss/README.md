# アプリボス スプライト（Office / Unity / Steam）

- PNG・透過・アンチエイリアスなし・**3倍書き出し**（プレイヤー素材と同じ密度）
- Unity Import：Filter Mode = Point / Compression = None / Sprite Mode = Multiple → Sprite Editor の Grid By Cell Size
- アイコンは各サービスのロゴではないオリジナルのデザイン（Office＝グラフ入りの書類、Unity＝移動ツールの矢印付きの立方体、Steam＝ゲームコントローラー）

## ファイル構成（アプリごとに同じ）

| ファイル | セル | フレーム | FPS | ループ | Pivot | 備考 |
|---|---|---|---|---|---|---|
| AppBoss_<App>_Idle | 168×168 | 5 | 8 | あり | Center | 既存オーラの5フレームと同期 |
| AppBoss_<App>_Attack | 168×168 | 6 | 10 | なし | Center | **F3 でスキル発動**（光ってリングが出るフレーム） |
| AppBoss_<App>_Hit | 168×168 | 3 | 12 | なし | Center | 白く光る→グリッチ→元に戻る |
| AppBoss_<App>_Purify | 168×168 | 10 | 10 | なし | Center | 正常化：感染とオーラが消え、F6〜9できらめく |
| Body/AppBoss_<App>_<Anim>_Body | 96×96 | 同上 | 同上 | 同上 | Center | **オーラ抜きの本体だけ**。オーラを別オブジェクトで重ねる場合はこちら |
| AppIcon_<App>_Normal | 96×96 | 1 | – | – | Center | 正常化したあとのきれいなアイコン |

## 合成版と本体のみの使い分け

- **合成版（オーラ込み）**：1つの SpriteRenderer で済む。手軽
- **Body ＋ 既存の effect-Sheet**：オーラを子オブジェクトにすると、Purify でオーラだけ先にフェードさせるなどの制御がしやすい
- Body の中心は合成版の中心と一致。オーラは元の `effect-Sheet.png` のセル（3倍で147×156）の中心から、右に3px・下に約9pxずらした位置にアイコンの中心が来る
