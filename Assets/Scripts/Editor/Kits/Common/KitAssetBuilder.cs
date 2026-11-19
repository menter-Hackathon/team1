using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】キットのアセットを作るクラスの土台（2026-11-19。docs/app-kits-1-design.md E1）。担当0 の物。
/// キットの担当は Editor/Kits/&lt;アプリ&gt;KitAssets.cs に、これを継いだクラスを1つ書くだけ。
/// メニュー「Tools/Team1/アプリのキットを用意」（AppKitsBuilder）が全部を集めて、ボス（Boss_Kit_〇〇）・能力（Skill_App_〇〇）を作り、表に入れる。
///
/// 書き方の例
///   public class SlackKitAssets : KitAssetBuilder
///   {
///       public override string Label => "Slack";
///       public override string[] AppNames => new[] { "Slack" };
///       public override string[] BundleIds => new[] { "com.tinyspeck.slackmacgap" };
///       public override bool HoverBoss => true;
///       public override AttackData[] Phase1() => new[] { Mention(), ReactionRain() };
///       AttackData Mention() => Data("Attack_Slack_Mention_Breakpoint_P1", AttackKind.Area, so => { so.FindProperty("_telegraphTime").floatValue = 0.8f; });
///   }
/// ・アセットは Assets/ScriptableObjects/Kits/&lt;Label&gt;/ に作る（Folder）
/// ・Data／Custom／CustomData／Dressed は、作るたびに init を入れ直す（キットの数値はコードが正。Unity で手で直した値は残らない）
/// ・攻撃データの名前に枠の語（breakpoint など。SkinTables.FrameOf）を入れると、カテゴリのスキンの絵が付く。入れなければ枠なし
/// ・実行時の登録（AppKits.Register・StageRules.Register・KitDress.RegisterArt）は、Kits/&lt;アプリ&gt;/&lt;アプリ&gt;Kit.cs に書く
/// </summary>
public abstract class KitAssetBuilder : IAppKit, IAppKitExtras
{
    // ───────── IAppKit ─────────

    /// <summary>表の上で見分ける名前（英数字だけ。例："Slack"）。AppKits.Register のラベルと同じにする</summary>
    public abstract string Label { get; }
    /// <summary>アプリの名前（スクリーンタイムの名前。単語ごとに比べる）</summary>
    public abstract string[] AppNames { get; }
    /// <summary>バンドルIDの前方一致（分からなければ空）</summary>
    public virtual string[] BundleIds => Array.Empty<string>();
    /// <summary>true：浮くボス（Chrome の体）。false：ワープのボス（VS Code の体）</summary>
    public virtual bool HoverBoss => true;
    /// <summary>第1形態の技（null なら、ボスは今までの代用ボス）</summary>
    public virtual AttackData[] Phase1() => null;
    /// <summary>第2形態の技（null なら第1形態と同じ）</summary>
    public virtual AttackData[] Phase2() => null;
    /// <summary>能力の攻撃データ（null なら、能力は今までのカテゴリの能力）</summary>
    public virtual AttackData PlayerSkill() => null;
    /// <summary>能力の名前（カードと HUD）</summary>
    public virtual string SkillName => Label;
    /// <summary>能力のカテゴリ</summary>
    public virtual SkillCategory Category => SkillCategory.Unknown;

    // ───────── 足した欄（IAppKitExtras） ─────────

    /// <summary>カードの説明（日本語の短い文）</summary>
    public virtual string SkillDescription => null;
    /// <summary>カードのタグ（2つくらい）</summary>
    public virtual string[] SkillTags => null;
    /// <summary>能力の見た目のスキン（None ならカテゴリから）</summary>
    public virtual SkinId SkillSkin => SkinId.None;
    /// <summary>能力のアイコン（null ならアプリのアイコン）</summary>
    public virtual Sprite SkillIcon => null;
    /// <summary>ボスのゲージの最大値（使用時間で変える）</summary>
    public virtual IntRange GaugeMax => new IntRange(80, 120);
    /// <summary>第2形態の、攻撃のあとの待ち時間（秒）</summary>
    public virtual FloatRange Phase2Wait => new FloatRange(0.8f, 0.6f);
    /// <summary>アセットを作ったあと（設定を足したいとき）</summary>
    public virtual void AfterBuild(BossProfile boss, SkillDefinition skill) { }

    // ───────── 道具 ─────────

    /// <summary>このキットのアセットの置き場所</summary>
    protected string Folder => $"{KitAssets.Root}/{Label}";

    /// <summary>データの技（弾・指定位置の判定・落下物・瞬間移動・ロックオン）を作る。init で数値を入れる（毎回入れ直す）</summary>
    protected AttackData Data(string name, AttackKind kind, Action<SerializedObject> init = null)
        => KitAssets.GetOrCreateData($"{Folder}/{name}.asset", kind, init);

    /// <summary>専用の技の中身（CustomAttack を継いだアセット）を作る。init で数値を入れる（毎回入れ直す）</summary>
    protected T Custom<T>(string name, Action<SerializedObject> init = null) where T : CustomAttack
        => KitAssets.GetOrCreateCustom<T>($"{Folder}/{name}.asset", init);

    /// <summary>専用の技の攻撃データ（種類＝Custom）を作る。init で共通の欄（ダメージ・クールタイムなど）を入れる（毎回入れ直す）</summary>
    protected AttackData CustomData(string name, CustomAttack custom, Action<SerializedObject> init = null)
        => KitAssets.GetOrCreateCustomData($"{Folder}/{name}.asset", custom, init);

    /// <summary>
    /// 包み（KitDressed）の攻撃データを作る：中の攻撃データを今の部品で出し、絵を artKey（KitDress.RegisterArt の名前）に差し替える。
    /// line：弾の通り道に、予告の間だけ赤い細い線。init は包みの攻撃データの共通の欄（クールタイム・ゲージなど）
    /// </summary>
    protected AttackData Dressed(string name, AttackData inner, string artKey, bool line = false, Action<SerializedObject> init = null)
        => KitAssets.Dressed($"{Folder}/{name}.asset", inner, artKey, line, init);

    /// <summary>今ある攻撃データを複製する（無ければ）。init は毎回入れ直す</summary>
    protected AttackData Copy(string sourcePath, string name, Action<SerializedObject> init = null)
        => KitAssets.Copy(sourcePath, $"{Folder}/{name}.asset", init);

    /// <summary>色を欄に入れる（16進。例："FF453A"）</summary>
    protected static void Color(SerializedObject so, string prop, string hex) => BossSkillAssets.Color(so, prop, hex);
}
