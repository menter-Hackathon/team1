using UnityEngine;

/// <summary>
/// ボスに足す、そのボスだけの仕組み（2026-11-18：Terminal の再起動の演出・カーネル・パニックの背景など。docs/app-boss-skills-design.md 4-4）。
/// BossProfile の「ボスに足す仕組み」の欄に入れると、BossController がプロフィールを受け取ったときに Attach を1回呼ぶ。
/// ふつうは、ボスの GameObject に部品（MonoBehaviour）を足して、BossController のイベント（TransitionStarted・PhaseChanged など）を聞く。
/// 同じ部品を2回足さないように作る（プロフィールを入れ直したときにも呼ばれる）
/// </summary>
public abstract class BossExtension : ScriptableObject
{
    public abstract void Attach(BossController boss);
}

/// <summary>
/// ボスのゲージの棒の見せ方を変える（2026-11-18：Terminal は「本当の50%で満タン」に見せ、再起動で空・赤に戻す）。
/// ボスの GameObject に付いた部品がこれを持っていれば、画面表示（PixelHud）がこちらを使う
/// </summary>
public interface IBossGaugeDisplay
{
    /// <summary>棒に出す割合（0〜1）</summary>
    float DisplayRatio(BossController boss);
    /// <summary>棒の色を変えるなら、その色（変えないなら null）</summary>
    Color? FillColor(BossController boss);
    /// <summary>第2形態になる所の印を出すか</summary>
    bool ShowThresholdMark(BossController boss);
}
