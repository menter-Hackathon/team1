# スキルエフェクト素材（Office / Unity / Steam）

- すべて PNG・透過・アンチエイリアスなし。**3倍書き出し**（プレイヤー素材と同じ密度）
- Unity Import 設定：Filter Mode = Point / Compression = None / Sprite Mode = Multiple → Sprite Editor の **Grid By Cell Size** で下表のセルサイズを指定
- `_Virus` 付きは **アプリボス用**（ウイルス配色）。プレイヤー版と同じセル・フレーム構成なので、同じ Animator / コードで差し替え可能
- 右向き基準。左向きは SpriteRenderer.flipX
- 文字は画像に焼き込んでいません（トーストの文言などは UI Text で）

## Office（グラフ攻撃）

| ファイル | セル | 構成 | FPS | ループ | Pivot | 当たり判定 |
|---|---|---|---|---|---|---|
| Office_Bar(_Virus) | 36×126 | 10F × 4行（行＝色：青/橙/灰/黄） | 12 | なし | Bottom Center | F2〜F5 |
| Office_Pie(_Virus) | 78×78 | 8F（出現→回転→光る→分裂） | 12 | なし | Center | F6 で PieSlice を4方向に発射 |
| Office_PieSlice(_Virus) | 36×36 | 4F × 4行（回転、行＝色） | 16 | あり | Center | 弾として常時 |
| Office_Line(_Virus) | 228×90 | 9F（線を描く6F→閃光2F→消滅1F） | 14 | なし | Left Center | F1〜F7（線に沿って貫通） |

- 棒グラフは1本ぶんのアニメーション。コード側で前方へ 0.1 秒ずつずらして4本（4色）生成する想定
- 折れ線の点の位置（3倍後の座標、セル左上基準）：(12,72) (51,39) (90,54) (129,18) (168,42) (210,9)。EdgeCollider2D の点に使える

## Unity（オブジェクト生成）

| ファイル | セル | 構成 | FPS | ループ | Pivot | 備考 |
|---|---|---|---|---|---|---|
| Unity_Cube / Sphere / Capsule (_Virus) | 60×60 | 1F | – | – | Center | Collider は Box / Circle / Capsule 2D |
| Unity_Primitives | 60×60 | 3列 × 2行（上：通常、下：ウイルス） | – | – | Center | まとめ版 |
| Unity_Spawn(_Virus) | 90×90 | 7F（選択枠→移動ツールの矢印→消える） | 14 | なし | Center | F2 でオブジェクト本体を出す |
| Unity_SpawnWarning_Virus | 90×90 | 4F（点滅） | 8 | あり | Center | ボス専用の生成予告。0.6〜0.8秒表示してから生成 |
| Unity_Impact(_Virus) | 108×42 | 6F（着地の砂煙） | 14 | なし | Bottom Center | 一定以上の落下速度で着地したときに出す |

## Steam（実績解除バフ）

| ファイル | セル | 構成 | FPS | ループ | Pivot | 備考 |
|---|---|---|---|---|---|---|
| Steam_Trophy(_Virus) | 48×48 | 6F（光が走る） | 10 | なし | Center | 通知のアイコン |
| Steam_ToastBG(_Virus) | 252×66 | 1枚 | – | – | Center | 通知の背景。右側の空きに「実績解除」と実績名を UI Text で |
| Steam_BuffBurst(_Virus) | 132×132 | 8F（リング＋上矢印） | 14 | なし | Center | 実績解除の瞬間にプレイヤーの中心へ |
| Steam_BuffAura(_Virus) | 78×108 | 6F | 10 | あり | Bottom Center | バフ中ずっとプレイヤーの足元に重ねる |
