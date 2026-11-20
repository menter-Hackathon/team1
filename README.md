# VIRUS DESKTOP

あなたの Mac で毎日使っているアプリが、ウイルスに感染してボスになる。
**PC の使用時間と連動する、2D ドット絵アクションローグライト**です。

## 概要

- 起動すると、Mac のスクリーンタイム・アプリの使用時間を読み取り、普段作業している「あなたらしさ」をゲームが認識します。
- 使用時間の長いアプリ（Chrome・VS Code・Discord・Photoshop など）が、ウイルスに感染した**ボス**として登場します。遊ぶ人ごと・日ごとに中身が変わります。
- これらの情報を用いて、PCに触れるモチベーションに繋がるような実績システムもあります。
- ステージの背景と足場は、そのアプリの画面（UI）を模したものになります。アプリの「中」で戦うイメージです。
- ボスのゲージを満タンにすると、ウイルス本体がアプリから追い出されます。これを倒すとアプリが**正常化**し、そのアプリにちなんだ**能力**（Chrome のタブ、Photoshop のブラシなど）が手に入ります。
- 能力は 3 つまで持てます。ステージを進んでいき、最後にリザルト画面で、正常化したアプリと手に入れた能力を振り返ります。
- 手に入れたアプリはアプリ図鑑から確認できます。

### ゲームの流れ

```
タイトル → 能力えらび → ステージ 1 → ステージ 2 → ？？？ → リザルト
```

ステージは 3 種類です：1 画面（Stage_Desktop）、横スクロール（Stage_Scroll）、ウィンドウ移動（Stage_Windows）。

### 動作環境

- macOS（スクリーンタイムの読み取りは macOS 15.2 以降。Touch ID は macOS 12 以降の対応機種）
- Unity 2022.3.62f2（Built-in レンダーパイプライン）
- スクリーンタイムを読めない環境では、サンプルデータ（`Assets/Resources/SampleUsage.json`）で遊べます。

---

## 自作したもの・外部のもの（クレジット）

このリポジトリにあるものを「チームで自作したもの」と「外部の技術・素材」に分けてリスト化します。クレジット表記が必要かどうかに関係なく載せています。

### A. チームで自作したもの

#### プログラム

| 内容 | 場所 |
|---|---|
| ゲーム本体（プレイヤー、敵、ボス、能力、ステージ、UI、実績、図鑑など）の C# スクリプト | `Assets/Scripts/`、`Assets/_Project/*/Scripts/` |
| テスト（EditMode） | `Assets/Tests/` |
| スクリーンタイムの画面を撮影して文字を読み取る補助アプリ（Swift） | `Tools/ScreenTimeCapture/` |
| Touch ID でロックを解除するネイティブプラグイン（Objective-C）とビルド済みの dylib | `Tools/TouchID/`、`Assets/Plugins/macOS/libTeam1TouchIdEmbedded.dylib` |
| ドット絵を作るスクリプト（Python） | `Tools/art/` |
| 効果音・BGM を作るシンセサイザーと楽譜・レシピ（Python） | `Tools/audio/` |
| シーン、プレハブ、ScriptableObject、アニメーション | `Assets/Scenes/`、`Assets/Prefabs/`、`Assets/ScriptableObjects/`、`Assets/Animations/` |
| 設計メモ | `docs/` |

#### 画像

| 内容 | 場所 | 作り方 |
|---|---|---|
| プレイヤー、ウイルスの煙、壁紙、画面の見本 | `Assets/Sprites/Provided/` | チームのデザイン担当が手描き |
| アプリボス、スキルのエフェクト、雑魚ウイルス、最終ボス | `Assets/_Project/Art/VirusGameArt/` | チームのデザイン担当が手描き |
| 実績のアイコンとランク枠 | `Assets/_Project/AchievementSystem/` | チームのデザイン担当が手描き |
| 頭の上の HP バー | `Assets/_Project/PlayerHPBar/` | チームのデザイン担当が手描き |
| まだ手描きの絵がない物の仮の絵（UI、足場、攻撃、エフェクトなど） | `Assets/Sprites/Generated/` | `Tools/art/gen_sprites.py` で生成 |
| アプリの画面を模したステージ背景（38 種類） | `Assets/Resources/AppBackgrounds/` | `Tools/art/appbg/` で生成 |
| ウイルスの種類ごとの絵 | `Assets/Resources/Virus/` | `Tools/art/virus/` で生成 |

アプリの画面を模した背景は、形と色の雰囲気だけを似せています。本物のロゴや画面の画像は使っていません。

#### 音

| 内容 | 場所 |
|---|---|
| 効果音（SE） | `Assets/Resources/Audio/SE/` |
| BGM・ジングル | `Assets/Resources/Audio/BGM/` |

音はすべて Python（numpy・scipy）で波形から自作しました（`Tools/audio/`）。フリー素材や、実在するアプリ・ゲームの音は使っていません。

### B. 外部の技術・素材

#### ゲームエンジンと Unity 公式パッケージ

| 名前 | 用途 | ライセンス |
|---|---|---|
| [Unity](https://unity.com/) 2022.3.62f2 | ゲームエンジン | Unity のライセンス |
| Input System 1.14.0 | キーボード・ゲームパッドの入力 | Unity Companion License |
| uGUI（Unity UI）1.0.0 | 画面の UI | Unity Companion License |
| TextMeshPro 3.0.7 | 文字の表示 | Unity Companion License |
| Timeline 1.7.7 | （導入のみ） | Unity Companion License |
| Visual Scripting 1.9.4 | （導入のみ） | Unity Companion License |
| Engineering feature（Test Framework ほか）1.0.1 | テストの実行 | Unity Companion License |

#### ライブラリ

| 名前 | 用途 | ライセンス |
|---|---|---|
| [UniRx](https://github.com/neuecc/UniRx)（neuecc） | 実績まわりのイベント通知 | MIT License |
| [UniTask](https://github.com/Cysharp/UniTask)（Cysharp） | 非同期処理 | MIT License |
| [DOTween](http://dotween.demigiant.com/)（Demigiant） | トゥイーン | DOTween のライセンス（無料版） |

#### フォント

| 名前 | 用途 | ライセンス |
|---|---|---|
| [DotGothic16](https://github.com/fontworks-fonts/DotGothic16)（Copyright 2020 The DotGothic16 Project Authors） | 日本語の文字 | SIL Open Font License 1.1（`Assets/Fonts/DotGothic16-OFL.txt`） |
| [Press Start 2P](https://fonts.google.com/specimen/Press+Start+2P)（Copyright 2012 The Press Start 2P Project Authors） | 英数字の文字 | SIL Open Font License 1.1（`Assets/Fonts/PressStart2P-OFL.txt`） |

#### OS の機能（macOS / Apple のフレームワーク）

| 名前 | 用途 |
|---|---|
| スクリーンタイム（システム設定） | アプリの使用時間のもとになるデータ |
| ScreenCaptureKit | スクリーンタイムの画面を撮影する |
| Vision | 撮影した画面から文字（アプリ名と時間）を読み取る |
| LocalAuthentication（`LAContext`・`LAAuthenticationView`） | ゲーム内ロック画面の Touch ID 認証。指紋やパスワードはゲーム側で受け取らない |
| AppKit・ImageIO・UniformTypeIdentifiers | 補助アプリの画面と画像の書き出し |
| osascript（JavaScript for Automation） | 遊ぶ人の Mac に入っているアプリのアイコンを、実行時に取り出して表示する（アイコン画像はリポジトリに入れていない） |

#### 開発ツール

| 名前 | 用途 |
|---|---|
| Python 3 | 画像と音を作るスクリプト |
| [Pillow](https://python-pillow.org/) | ドット絵の生成（HPND License） |
| [NumPy](https://numpy.org/) | 音の波形の生成（BSD License） |
| [SciPy](https://scipy.org/) | 音のフィルタと WAV の書き出し（BSD License） |
| Xcode のコマンドラインツール（swiftc・clang） | 補助アプリと Touch ID プラグインのビルド |
| Visual Studio Code | コードエディタ |
| Git・GitHub | バージョン管理 |
| [Claude Code](https://claude.com/claude-code)（Anthropic。Claude Opus 5.5 / Sonnet 5.5） | AI によるコーディング支援。設計メモ、C# スクリプト、画像・音を作るスクリプトの多くを、チームの指示のもとで生成した（コミットに `Co-Authored-By: Claude` と記載） |

#### 商標について

ゲームに登場するアプリ名（Chrome、VS Code、Discord、Photoshop など）は、各社の商標です。本作はそれらの会社とは関係ありません。アプリのアイコンは遊ぶ人の Mac から実行時に読み込むだけで、配布物には含めていません。

---

## チーム

- tsuzuki-seita
- masapii
- reki-su（rekki）
- hiroto-master
