# 実績システム素材（VIRUS DESKTOP）

## 中身

```
AchievementSystem/
├── Icons/     解除済みアイコン 17枚（Ach_<ID>.png、48×48、元のドット絵は16×16）
├── Locked/    未解除アイコン 17枚（Ach_<ID>_Locked.png）
├── Frames/    ランク枠 3枚（AchFrame_Bronze / Silver / Gold.png、60×60）
└── Data/
    ├── achievements.csv     実績17件の定義（ScriptableObject 生成用）
    └── app_categories.csv   アプリ名とカテゴリの対応表（デトックス・週末エンジニア用）
```

## 置き場所

- 画像：`Assets/_Project/Art/VirusGameArt/AchievementIcons/`（Icons・Locked・Frames をそのまま）
- CSV：`Assets/_Project/Data/Achievements/`（エディタ拡張で ScriptableObject を作る元データ。ビルドには含めなくてよい）

## インポート設定（画像）

- Texture Type = Sprite (2D and UI) / Filter Mode = Point / Compression = None / Mip Maps オフ
- 3倍書き出し済み。他の素材と同じく PPU はプレイヤーのスプライトに合わせる
- UI で使う場合は Image の Preserve Aspect をオン

## achievements.csv の列

| 列 | 内容 |
|---|---|
| id | 実績ID。アイコンのファイル名と対応 |
| category | PCの使い方 / プレイ / アプリ図鑑 |
| name, description | 表示名と条件の説明 |
| rank | Bronze / Silver / Gold（一覧の枠の色にだけ使う。報酬はなし） |
| game_effect | ゲームへの影響（新入りのみ） |
| trigger | LaunchScan（起動時）/ InPlay（プレイ中）/ OnPurify（正常化したとき） |
| condition_type, target | 条件の種類と目標値 |
| icon, icon_locked | アイコンのファイル名 |

## app_categories.csv

- category が「エンタメ」「SNS」のアプリだけがデトックスの対象、「作業」が週末エンジニアの対象
- match_key は空欄。スクリーンタイムで取れるアプリ名（例：「Visual Studio Code」「Discord - 一緒にしゃべっ…」の正式名）を入れて照合に使う
