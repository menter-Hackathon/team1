using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【仮】プレイヤーとボスの強さの倍率（2026-11-19：アプリのキット。Steam の実績解除で強くなる、など）。
/// ・プレイヤー：攻撃力（雑魚へのダメージ）・ボスのゲージの増え方・能力のクールタイム・横の速さ
/// ・ボス：移動の速さ・攻撃のあとの待ち時間（BossController の倍率に掛ける）
/// シーンが変わると全部 1 に戻る（ステージごと）。変えた部品が、終わるときに戻してもよい
/// 2026-11-19：キットは StatMods（名前を付けて足し、名前で外す）を通す。StatMods は、前に入れた分との差だけをここに掛ける
/// （ここへ直接書く部品があっても消さない）。読む側はこのままでよい
/// </summary>
public static class GameBuffs
{
    /// <summary>プレイヤーの攻撃の、雑魚へのダメージの倍率</summary>
    public static float PlayerDamage = 1f;
    /// <summary>プレイヤーの攻撃の、ボスのゲージの増え方の倍率</summary>
    public static float PlayerGauge = 1f;
    /// <summary>プレイヤーの能力のクールタイムの倍率（0.5 で半分）</summary>
    public static float PlayerCooldown = 1f;
    /// <summary>プレイヤーの横の速さの倍率（PlayerController.MoveSpeedMultiplier に掛ける）</summary>
    public static float PlayerSpeed = 1f;
    /// <summary>ボスの移動の速さの倍率（BossController.MoveSpeedMultiplier に掛ける）</summary>
    public static float BossSpeed = 1f;
    /// <summary>ボスの攻撃のあとの待ち時間の倍率（BossController.WaitMultiplier に掛ける。0.5 で半分）</summary>
    public static float BossWait = 1f;
    /// <summary>【仮】ボスが受けるゲージの倍率（VirusGauge が読む。2026-11-19）</summary>
    public static float BossGaugeTaken = 1f;
    /// <summary>【仮】雑魚の移動の速さの倍率（2026-11-19。ZakoWalker・ZakoFlyer・ZakoCharger が読む）</summary>
    public static float ZakoSpeed = 1f;
    /// <summary>【仮】近接だけの、ボスのゲージの増え方の倍率（2026-11-19：Word の太字。VirusGauge が読む）</summary>
    public static float PlayerMeleeGauge = 1f;
    /// <summary>【仮】近接の届く長さの倍率（2026-11-19：Word の太字。KitMeleeReach が近接の判定の横幅に掛ける）</summary>
    public static float PlayerMeleeReach = 1f;

    /// <summary>全部 1 に戻す（StatMods の倍率も全部外す）</summary>
    public static void Reset()
    {
        PlayerDamage = PlayerGauge = PlayerCooldown = PlayerSpeed = 1f;
        BossSpeed = BossWait = 1f;
        BossGaugeTaken = ZakoSpeed = 1f;
        PlayerMeleeGauge = PlayerMeleeReach = 1f;
        StatMods.ResetFromGameBuffs();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Reset();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Reset();
}
