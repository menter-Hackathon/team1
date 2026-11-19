using UnityEngine;

// アプリのキット（2026-11-19：「このアプリといえば！」をボスの技・プレイヤーの能力・ステージのギミックにする）。
// アプリ1つ＝このインターフェースを実装したクラス1つ（Editor/AppKits/ の下に、アプリごとのファイルで）。
// AppKitsBuilder（メニュー「Tools/Team1/アプリのキットを用意」）が全部を集めて、
// ボスの設定（Boss_Kit_〇〇）・アプリ専用の能力（Skill_App_〇〇）を作り、対応アプリの表・能力の表に入れる。
// ステージのギミックは実行時のコードで登録する（AppStageEvents.Register、または StagePacer.Started の購読）。

/// <summary>アプリのキット1つ分（アセットを作る側）</summary>
public interface IAppKit
{
    /// <summary>表の上で見分ける名前（例："Slack"）。アセットの名前にも使う（英数字だけ）</summary>
    string Label { get; }
    /// <summary>アプリの名前（スクリーンタイムの名前。単語ごとに比べる。大文字小文字は区別しない）</summary>
    string[] AppNames { get; }
    /// <summary>バンドルIDの前方一致</summary>
    string[] BundleIds { get; }
    /// <summary>true：ボスは浮いて移動する（Chrome の体）。false：床に立ち、瞬間移動で移る（VS Code の体）</summary>
    bool HoverBoss { get; }
    /// <summary>ボスの第1形態の技（null・空なら、ボスは作らない＝今までの代用ボス）</summary>
    AttackData[] Phase1();
    /// <summary>ボスの第2形態の技（null・空なら、第1形態と同じ）</summary>
    AttackData[] Phase2();
    /// <summary>プレイヤーの能力の攻撃データ（null なら、能力は作らない＝今までのカテゴリの能力）</summary>
    AttackData PlayerSkill();
    /// <summary>能力の名前（カードと HUD に出る。例："メンション"）</summary>
    string SkillName { get; }
    /// <summary>能力のカテゴリ（カードの色分けなど）</summary>
    SkillCategory Category { get; }
}

/// <summary>
/// 今あるアセットを直す処理（2026-11-19：Chrome・VS Code など、キットの形でないボスの技の入れ替え）。
/// AppKitsBuilder が、キットを作ったあとに全部呼ぶ。何度呼ばれても同じ結果になるように作る
/// </summary>
public interface IAppKitFixup
{
    void Apply();
}

/// <summary>
/// 【仮】キットの足した欄（2026-11-19。担当0）。IAppKit に無い物（カードの文・能力のスキン・ゲージの範囲など）。
/// ふつうは KitAssetBuilder（Editor/Kits/Common/）を継げば、全部に既定の値が入っている。AppKitsBuilder が、あれば読む
/// </summary>
public interface IAppKitExtras
{
    /// <summary>能力えらびのカードの説明（null なら出さない）</summary>
    string SkillDescription { get; }
    /// <summary>カードのタグ（null なら出さない）</summary>
    string[] SkillTags { get; }
    /// <summary>能力の見た目のスキン（None なら能力のカテゴリから）</summary>
    SkinId SkillSkin { get; }
    /// <summary>能力のアイコン（null ならアプリのアイコン）</summary>
    Sprite SkillIcon { get; }
    /// <summary>ボスのゲージの最大値（使用時間で変える）</summary>
    IntRange GaugeMax { get; }
    /// <summary>第2形態の、攻撃のあとの待ち時間（秒。使用時間で変える）</summary>
    FloatRange Phase2Wait { get; }
    /// <summary>ボス・能力のアセットを作ったあとに呼ぶ（どちらも無ければ null）。設定を足したいときに使う</summary>
    void AfterBuild(BossProfile boss, SkillDefinition skill);
}
