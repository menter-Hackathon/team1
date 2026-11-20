# アプリボスの作り方（共通の基盤）

- 書いた日：2026-11-20
- 読む人：新しくアプリボスを足す人と AI エージェント。**アプリボスにさわる作業は、この文書を読んでから始める。**
- この文書は、今あるコード（`Assets/Scripts/Kits/`・`Assets/Scripts/Editor/Kits/`・`Assets/Scripts/Editor/AppKits/`）を読んで書いた。コードと食い違ったら、コードが正しい。気づいたらこの文書を直す。
- 細かい背景は設計メモにある：[app-kits-1-design.md](app-kits-1-design.md)（0章の決まり・1章の共通の入口）、[app-kits-2-design.md](app-kits-2-design.md)（1-2 の片づけの表）。

---

## 0. AI エージェントへ：始める前に

1. この文書を最後まで読む。
2. 見本のキットを 1つ読む。小さいのは **メモ（Notes）**：
   - [NotesKit.cs](../Assets/Scripts/Kits/Notes/NotesKit.cs)（実行時の登録）
   - [NotesKitAssets.cs](../Assets/Scripts/Editor/Kits/NotesKitAssets.cs)（技と能力の数値）
   - [NotesQuickMemo.cs](../Assets/Scripts/Kits/Notes/NotesQuickMemo.cs)（専用の技）
   - [NotesRule.cs](../Assets/Scripts/Kits/Notes/NotesRule.cs)（ステージのギミック）
   - [NotesArt.cs](../Assets/Scripts/Kits/Notes/NotesArt.cs)（コードでかくドット絵）
3. 作るアプリのキットが、もう無いか確かめる。`Assets/Scripts/Kits/` のフォルダ名と、[KitAliases.cs](../Assets/Scripts/Kits/Common/KitAliases.cs)（別のキットに名前だけ足しているアプリ）を見る。
4. 下の「2. 実装の型」で、どの型で作るかを決める。迷ったら **キット型**。
5. **共通の部品（`Kits/Common/`）と、今ある表やビルダーは変えない**（7章）。足りない物があれば、作る前に人に相談する。

---

## 1. アプリボスとは

遊ぶ人の Mac で使用時間の長いアプリが、ウイルスに感染したボスとして出てくる。アプリ 1つにつき、次の 4つをそろえたものを **キット** と呼ぶ。

| 部分 | 中身 | 必須か |
|---|---|---|
| ボスの技 | 第1形態 2つ、第2形態 +1つ（第2形態は第1形態の強い版も使う） | **必須**（これだけでもボスとして遊べる） |
| プレイヤーの能力 | そのボスを倒すと手に入る。能力えらびのカードにも出る | あるとよい |
| ステージのギミック（`StageRule`） | そのアプリのステージだけの決まり | あるとよい |
| 場面③の出来事（`StageEvent`） | ステージの途中で起きる出来事 | なくてよい |

- 元ネタは「**このアプリといえば！**」。アプリの機能やあるあるを、技・能力・ギミックにする（Slack のメンション、メモの紙、Chrome のタブなど）。
- キットの無いアプリは、代用ボス＋カテゴリの能力＋スキンの出来事で遊べる。キットの一部が無くても、その枠は今までの物が使われる。**いつでもボスとして遊べる状態を保つ。**

### 動く仕組み

```
【エディタ】
  Editor/Kits/<Label>KitAssets.cs（KitAssetBuilder を継ぐ）
     │  メニュー「Tools/Team1/アプリのキットを用意」（AppKitsBuilder.Build）
     ▼
  ScriptableObjects/Kits/<Label>/Attack_*.asset   … 技と能力の攻撃データ
  ScriptableObjects/Bosses/Boss_Kit_<Label>.asset … ボスの設定（Boss_Chrome か Boss_VSCode の複製）
  ScriptableObjects/Skills/Skill_App_<Label>.asset … 能力
  AppCatalog・AbilityCatalog の行                … アプリ名 → ボス・能力
  Resources/Kits/KitIndex.json・KitSkillTexts.json … 実行時の名前の表・カードの文

【実行時】
  Kits/<Label>/<Label>Kit.cs の Register（[RuntimeInitializeOnLoadMethod]）
     ├ AppKits.Register      … アプリ名 → ラベル
     ├ StageRules.Register   … ステージのギミック
     ├ StageEvents.Register  … 場面③の出来事（あれば）
     ├ KitDress.RegisterArt  … 技の絵
     └ SkillTexts.Register   … 能力カードの説明
```

---

## 2. 実装の型（どれで作るか）

| 型 | 使う場面 | 例 |
|---|---|---|
| **A. キット型（標準）** | 新しいアプリボスは、まずこれ。絵はコードでかくドット絵 | Slack・Notes・Excel など 33 アプリ（`Assets/Scripts/Kits/`） |
| B. キット型＋手描きの見た目 | デザイン担当の手描き素材（コマ送り）がある | Office・Unity・Steam（[OfficeUnitySteamBuilder.cs](../Assets/Scripts/Editor/OfficeUnitySteamBuilder.cs)・[AppBossAnimator.cs](../Assets/Scripts/Boss/AppBossAnimator.cs)） |
| C. 専用ボス（古い型） | **新しくは作らない** | Chrome・VS Code・Finder・Premiere Pro・Terminal（ラスボス）など（`Run/AppKits/`・`Combat/Custom/`） |

- B は「A で技・能力・ギミックを作り、見た目だけ手描き素材に差し替える」と考える。手描き素材の取り込み設定は [VirusGameArt の README](../Assets/_Project/Art/VirusGameArt/README.md) に従う（PPU 54・Point・圧縮なし）。
- C のボスを直すときは、そのボスのファイルと設計メモ（[app-boss-skills-design.md](app-boss-skills-design.md) など）を読む。

以下は A（キット型）の説明。

---

## 3. 作るファイル

`<Label>` は英数字だけのアプリの名前（例：`Notes`・`GitHubDesktop`）。アセットの名前にも使う。

| ファイル | 中身 | 必須か |
|---|---|---|
| `Assets/Scripts/Kits/<Label>/<Label>Kit.cs` | 実行時の登録。ラベル・アプリ名・バンドルID・能力の名前と説明・絵のキー | **必須** |
| `Assets/Scripts/Editor/Kits/<Label>KitAssets.cs` | `KitAssetBuilder` を継ぐ。技と能力の数値 | **必須** |
| `Assets/Scripts/Kits/<Label>/<Label>Art.cs` | 技の絵（`KitArt.Paint` でコードの中にかく） | 絵を差し替える技があれば |
| `Assets/Scripts/Kits/<Label>/<Label><技>.cs` | 専用の技（`CustomAttack`）と、自分で進む部品（`CustomEffect`） | データで作れない技があれば |
| `Assets/Scripts/Kits/<Label>/<Label>Rule.cs` | ステージのギミック（`StageRule`） | ギミックを作るなら |
| `Assets/Scripts/Kits/<Label>/<Label><名前>Event.cs` | 場面③の出来事（`StageEvent`。`KitTimedEvent` を使うと速い） | 作るなら |

**自分のアプリのフォルダの中だけをさわる。** 生成されるアセット（`ScriptableObjects/Kits/<Label>/`・`Boss_Kit_<Label>`・`Skill_App_<Label>`・`Resources/Kits/*.json`・表の行）は、手で作らない。ビルダーに作らせる。

---

## 4. 手順

どのキットも、作る順番は同じ。①だけで「そのアプリのボス」になる。

1. **第1形態の技 2つ**
2. **能力**
3. **ギミック**
4. **第2形態の技**

### 4-1. 登録（`<Label>Kit.cs`）

```csharp
public static class FooKit
{
    public const string Label = "Foo";
    public static string[] AppNames => new[] { "Foo", "フー" };       // スクリーンタイムの名前（単語ごと・大文字小文字は区別しない）
    public static string[] BundleIds => new[] { "com.example.foo" }; // 前方一致
    public const string SkillName = "〇〇";
    public const string SkillDescription = "能力カードの説明（1〜2文）。";
    public static string[] SkillTags => new[] { "遠距離", "足止め" };
    public const string ArtShot = "foo.shot";                        // 絵のキー（"<label小文字>.<名前>"）

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, AppNames, BundleIds);
        StageRules.Register(Label, () => new FooRule());   // ギミックがあれば
        KitDress.RegisterArt(ArtShot, i => FooArt.Shot()); // 絵があれば
        SkillTexts.Register(SkillName, SkillDescription, SkillTags);
    }
}
```

### 4-2. 技と能力（`<Label>KitAssets.cs`）

```csharp
public class FooKitAssets : KitAssetBuilder
{
    public override string Label => FooKit.Label;
    public override string[] AppNames => FooKit.AppNames;
    public override string[] BundleIds => FooKit.BundleIds;
    public override bool HoverBoss => true;                  // true＝浮いて x=-7/0/7 を移動（Chrome の体）／false＝床に立ちワープ（VS Code の体）
    public override string SkillName => FooKit.SkillName;
    public override SkillCategory Category => SkillCategory.Office;
    public override string SkillDescription => FooKit.SkillDescription;
    public override string[] SkillTags => FooKit.SkillTags;

    public override AttackData[] Phase1() => new[] { A("P1"), B("P1") };
    public override AttackData[] Phase2() => new[] { A("P2"), B("P2"), C() };
    public override AttackData PlayerSkill() => Skill();
    // GaugeMax（既定 80〜120）・Phase2Wait（既定 0.8〜0.6秒）・AfterBuild も上書きできる
}
```

技を作る道具（`KitAssetBuilder` の protected メソッド。アセットは `ScriptableObjects/Kits/<Label>/<名前>.asset` にできる）

| 道具 | 使い道 |
|---|---|
| `Data(name, AttackKind, so => …)` | データだけで作る技（弾・指定位置の判定・落下物・瞬間移動など）。欄は `so.FindProperty("_speed")` のように入れる |
| `Custom<T>(name, so => …)` | 専用の技（`CustomAttack` を継いだクラス）のアセット |
| `CustomData(name, custom, so => …)` | 専用の技を指す攻撃データ |
| `Dressed(name, inner, artKey)` | 今ある攻撃データに、キットの絵を着せる |
| `Copy(sourcePath, name, …)` | 今ある攻撃データを複製して直す |
| `Color(so, "_color", "FFD60A")` | 色を入れる |

- アセットは「無ければ作る」。**すでにあれば数値は上書きしない**（調整した値を消さないため）。数値を入れ直すときは、そのアセットを消してからビルダーを回す。
- 攻撃データの名前は `Attack_<Label>_<技>_<P1|P2>`。名前にスキンの枠の語（`Breakpoint`・`tabmultiply`・`popupappear` など）を入れると、そのスキンの絵が自動で付く（`SkinTables.FrameOf`）。キットの絵にしたいときは、枠の語を入れずに `Dressed` で着せる。
- **データで作れる形を先に考える。** 入らない動き（壁から出す・高さを決めて出す・隅に出すなど）だけを `CustomAttack` にする。

### 4-3. 専用の技（`CustomAttack`・`CustomEffect`）

- `CustomAttack`（ScriptableObject）の `Run(owner, data)` に技の流れを書く。ボスは `Run` が終わるまで次の技を始めない。
- 場に出す物は `owner.CreateSpawnedObject` で作る（片づけで消えるように）。自分で動く物は `CustomEffect` を継いだ部品に任せる。
- 地形・プレイヤー・ボスの状態を変える技は、`CustomEffect.Restore` で**必ず元に戻す**（形態変化・正常化・途中で止められたとき）。
- 場に残る技は `AttackData` の `_lingering` を true にする（重ねて出さない）。
- 能力用には `CanRun`・`DefersCooldown`（2回押し）・`DisplayIcon`・`IsCharged` がある。

### 4-4. 絵（`<Label>Art.cs`）

- `KitArt.Paint("<キー>", 幅, 高さ, c => { c.Fill(…); c.HLine(…); })` でドット絵をかく。色は `KitArt.Hex("FFD60A")`。
- 1マス＝18ドット。2×2マスの物なら 36×36。
- **絵のファイルは足さない**（手描き素材がある B 型を除く）。

### 4-5. ステージのギミック（`StageRule`）

- `Begin`・`Tick`・`OnPhase`・`OnBossAppeared`・`OnBossPhase`・`End` を上書きする。`End` は何回呼ばれてもよいように作る。
- 出した物は必ず `ctx.TrackHazard`（危ない物）・`ctx.TrackPlaced`（置いた物）・`ctx.TrackDecor`（飾り）に渡す。`StageRuleHost` が片づけの表どおりに消す。
- 危ない物を出す前に `ctx.CanSpawnHazard`、置く前に `ctx.CanPlace` を見る（最初の 3秒・形態変化のあと・ボスの技の途中などは false になる）。
- 強さの倍率は `StatMods`（例：`StatMods.Boss.Wait.Set(key, 0.7f, icon)`）。キーは `ctx.Key("名前")`。100% で全部外れる。
- Terminal のステージ（ラスボス）には付かない。

### 4-6. アセットを作る

1. Unity で、メニュー「**Tools/Team1/アプリのキットを用意**」を押す。
   - Unity を開いていなければ、バッチモードでもよい：
     `/Applications/Unity/Hub/Editor/2022.3.62f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -executeMethod AppKitsBuilder.Build -logFile -`
   - 同じプロジェクトを Unity で開いているときは、バッチモードは使えない。
2. コンソールに `[AppKits] キット N 個：…` と出て、自分のラベルが入っていることを確かめる。エラーが無いことも確かめる。
3. できたアセット・表の差分（`AppCatalog.asset`・`AbilityCatalog.asset`・`Resources/Kits/*.json`）をコミットに含める。

---

## 5. 守る決まり

### 数値（変えない前提）

- 長さはマス（1マス＝18ドット）、時間は秒。
- プレイヤー：毎秒 7マス、ジャンプ 3.2マス、体 0.7×1.0。
- 部屋：横 20マス（x＝-10〜10）、床から天井まで 11.5マス。足場は x＝±3.5、幅 2.4、高さ 3。
- ボスの攻撃は、どれもライフが 1減る。ノックバック (6,5)、のけぞり 0.25秒、無敵 1.0秒。
- 攻撃のあとの待ち時間：第1形態 1.2秒、第2形態 0.8秒。
- 能力：使えばボスのゲージが増える（移動だけ・強化だけの能力は作らない）。クールタイムは 2.5〜8秒が目安。

### 予告と公平さ

- **予告は 0.8秒が基本。第2形態でも短くしない。** 難しくするときは、数と密度を増やす。
- 「足場の上だけ安全」の技とギミックは、予告を 2.5秒にする。
- **危ない物は赤い予告**（赤いふち・赤い線・赤い「！」）。当たらない物に赤は使わない。
- ギミックの罰は「ライフが減る」にしない。守れなかったときは、ボスか雑魚が 8〜10秒だけ少し強くなる。
- ギミックがボス戦で出す危ない物は、小さくて逃げ場をふさがない物だけ（直径 4マスまで）。
- 倍率の幅：プレイヤーの速さ 〜1.4、与えるゲージ 〜1.6、クールタイムの進み 0.6〜1.7、ボスの待ち時間 0.7〜1.5。**ボスの予告の長さと弾の速さは、どの倍率でも変えない。**

### 片づけ

- 50%（形態変化）で `CustomEffect` を消し、100%（正常化）で全部消す。変えた地形・速さ・封印は戻す。
- ギミックの片づけは [app-kits-2-design.md](app-kits-2-design.md) 1-2 の表のとおり（`StageRuleHost` がやる。出した物を Track していれば任せてよい）。

### 見た目・文字・音

- 説明の帯は出さない。出す文字は、UI をまねた**英語の短い言葉**（`@channel`・`Regenerate` など）と飾りの数字だけ。
- 見た目は Mac 風（`MacUi` のウィンドウ・通知・ダイアログ）。
- **本物のロゴ・アイコン・キャラクターの絵を描かない。** 形と色の雰囲気だけ似せる。
- **本物のアプリの音（通知音・起動音）をまねない。** 音は [Tools/audio/FORMAT.md](../Tools/audio/FORMAT.md) のとおり、自作のチップチューンにする。
- **個人の情報は読まない・出さない。** 本物のメッセージ・予定・連絡先・アカウント名は使わない。出す文字は決まった作り物だけ（`#general`・`PLAYER` など）。

---

## 6. 使える部品

### 攻撃データの種類（`AttackKind`。[AttackEnums.cs](../Assets/Scripts/Combat/AttackEnums.cs)）

| 種類 | 中身 |
|---|---|
| `Projectile` | 弾。狙い方は「狙う相手」「正面」。数と扇の角度 |
| `Area` | 指定位置の判定。持続時間を入れると、残るダメージ床 |
| `Spawn` | 落下物・設置物（上の列・狙う相手の真上・出現）。足場にもできる |
| `Warp` | 瞬間移動 |
| `Custom` | 専用の技（`CustomAttack`） |

欄の名前は、今あるキット（`Editor/Kits/*KitAssets.cs`）で使っている物をまねる。

### 共通の部品（`Assets/Scripts/Kits/Common/`。使うだけ、変えない）

| 部品 | 使い道 |
|---|---|
| `KitArt` | コードでドット絵をかく |
| `KitDress`・`KitDressed` | 今ある部品にキットの絵を着せる。`KitDress.Line` で予告の線 |
| `StatMods` | 強さの倍率（プレイヤー・ボス） |
| `KitHud` | HUD のライフの下の小さい印とメーター |
| `KitProp`・`KitBlock` | 叩ける・乗れる置き物 |
| `KitMotion*`・`KitPath` | ギミックの動きの部品（下から出る・横から来る・回る・跳ねる・床が動く など）。メニュー「Tools/Team1/KitMotion の試し」で、再生中に見本を出せる |
| `KitTimedEvent` | 場面③の出来事を、時間の表で手早く書く土台 |
| `WallShot` | 高さの決まった横の弾 |
| `ZoneStrike` | 区画の攻撃 |
| `DropQuake` | 落下＋左右の波 |
| `Kit3D`・`Kit3DFaller` | 3D の物体（Unity・Blender・Minecraft） |
| `KitBossWarp` | ボスを瞬間移動させる |
| `KitGuard` | プレイヤーの被弾を止める |
| `KitMeleeReach` | 近接の届く距離を変える |
| `KitActions` | プレイヤー・ボス・弾に足す小さい入口 |

---

## 7. さわってよい所・いけない所

**さわってよい**：自分のアプリの `Kits/<Label>/` と `Editor/Kits/<Label>KitAssets.cs`。

**変えない**（変えたいときは人に相談する）

- 共通の部品：`Assets/Scripts/Kits/Common/`
- 今ある表とビルダー：`AppCatalog`・`AbilityCatalog`・`AppKitsBuilder`・`KitAssetBuilder`・`BossController`・`VirusGauge`・`PlayerSkillExecutor`・`SkillTexts`
- スキン：`Assets/Scripts/Visual/Skins/`（`SkinTables` を含む）
- ほかのアプリのキット、シーン、今あるアセット
- 生成されるアセットを手で直すこと（ビルダーで作り直す）

**任意で足す所**（アプリごとの表。足すときは 1行だけ足す）

| 足す物 | 場所 |
|---|---|
| ステージの背景と足場（アプリの画面風） | `Tools/art/appbg/apps/<Label>.json` → `python3 Tools/art/appbg/render.py`。決まりは [FORMAT.md](../Tools/art/appbg/FORMAT.md) |
| 技の音・ギミックの音 | `Tools/audio/se/*.json`・`Assets/Resources/Audio/Maps/*.json`。決まりは [Tools/audio/FORMAT.md](../Tools/audio/FORMAT.md) |
| 出てくるウイルスの種類 | [VirusRoster.cs](../Assets/Scripts/Enemy/Virus/VirusRoster.cs) の `Add(...)` |
| 実績のアプリのカテゴリ | [app_categories.csv](../Assets/_Project/AchievementSystem/Data/app_categories.csv) |
| 日本語のアプリ名 → 英語名 | [AppIcons.cs](../Assets/Scripts/Run/AppIcons.cs) の `JapaneseNames` |
| 似たアプリに、このキットを名前だけ使わせる | [KitAliases.cs](../Assets/Scripts/Kits/Common/KitAliases.cs) の `Table` |

---

## 8. 確かめ方

1. **コンパイル**：Unity のコンソールにエラーが無い。
2. **アセット**：4-6 のとおりビルダーを回し、`Boss_Kit_<Label>`・`Skill_App_<Label>`・`ScriptableObjects/Kits/<Label>/` ができている。
3. **遊んで確かめる**：タイトルのメニューの「ずかん」（アプリ図鑑）から、そのアプリを選んで戦う（[AppDexBattle.cs](../Assets/Scripts/Run/AppDexBattle.cs)。練習のランなので、図鑑と実績には書かない）。
4. 見る物
   - 第1形態の技 2つ、第2形態の技 3つが出る。
   - 予告が赤で 0.8秒以上ある。
   - 50% と 100% で、出した物が全部消え、倍率が戻る。
   - ギミックが、雑魚の時間とボス戦の両方で動く。
   - 能力を、ほかのステージ（Terminal を含む）へ持っていっても動く。
   - `AppKits.Enabled = false` で、代用ボスに戻る。
5. 人に頼むこと：AI エージェントが Unity を操作できないときは、1〜3 を人に頼み、何を見てほしいかを 4 の形で伝える。

---

## 9. 終わったら

- 設計メモ（このアプリの技・能力・ギミックの中身と数値）を `docs/` に足すか、今の設計メモの章に足す。数値は【仮】と書く。
- [implementation-status.md](implementation-status.md) に、足したアプリを 1行足す。
- この文書と食い違う作り方をしたら、この文書も直す。
- コミットのメッセージは日本語で、今までの形に合わせる（`feat: 〇〇のキットを足す` など）。main に直接入れず、ブランチを切って PR にする。
