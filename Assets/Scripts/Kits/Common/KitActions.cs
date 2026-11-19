using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】プレイヤー・ボス・弾に足す小さい入口（2026-11-19。docs/app-kits-1-design.md E5）。担当0 の物。
// ・能力の封印・クールタイムを進める・回復（KitPlayer）
// ・ボスをひるませる・ゲージを戻す・技の途中か（KitBoss）
// ・敵の弾を消す（KitBullets）
// 中身は PlayerSkillExecutor・BossController・VirusGauge にある（ここは呼ぶだけの窓口）。

/// <summary>プレイヤーに足す入口</summary>
public static class KitPlayer
{
    /// <summary>プレイヤー（無ければ null）</summary>
    public static PlayerController Controller => UnityEngine.Object.FindObjectOfType<PlayerController>();
    /// <summary>能力を実行する係（無ければ null）</summary>
    public static PlayerSkillExecutor Executor => UnityEngine.Object.FindObjectOfType<PlayerSkillExecutor>();
    /// <summary>プレイヤーの位置（無ければ null）</summary>
    public static Transform Transform { get { var c = Controller; return c != null ? c.transform : null; } }

    /// <summary>能力の枠を seconds 秒使えなくする（HUD の枠に鍵と斜線）。selectedOnly：選んでいる枠1つだけ。重ねたら長い方</summary>
    public static void Seal(float seconds, bool selectedOnly = false)
    {
        PlayerSkillExecutor ex = Executor;
        if (ex == null) return;
        int slot = -1;
        if (selectedOnly)
        {
            var skills = ex.GetComponent<PlayerSkills>();
            slot = skills != null ? skills.SelectedSlot : -1;
        }
        ex.Seal(seconds, slot);
    }

    /// <summary>封印を全部とく</summary>
    public static void Unseal()
    {
        PlayerSkillExecutor ex = Executor;
        if (ex != null) ex.Unseal();
    }

    /// <summary>その枠が封印されているか</summary>
    public static bool IsSealed(int slot)
    {
        PlayerSkillExecutor ex = Executor;
        return ex != null && ex.IsSealed(slot);
    }

    /// <summary>全部の能力のクールタイムを seconds 秒進める</summary>
    public static void AdvanceCooldowns(float seconds)
    {
        PlayerSkillExecutor ex = Executor;
        if (ex != null) ex.AdvanceCooldowns(seconds);
    }

    /// <summary>全部の能力のクールタイムを回復する（すぐ使える）</summary>
    public static void RefillCooldowns()
    {
        PlayerSkillExecutor ex = Executor;
        if (ex != null) ex.RefillCooldowns();
    }

    /// <summary>
    /// 【仮】プレイヤーの被弾を seconds 秒だけ見張る（2026-11-19：Firefox の広告ブロックなど）。
    /// 敵の攻撃の判定が当たる直前に onHit(info) を呼び、true を返したらその当たりを止める（ダメージなし）。止めたあと invincibleAfter 秒は無敵。
    /// 「1回だけ防ぐ」なら、onHit の中で返した KitGuard の Release を呼ぶ。プレイヤーがいなければ null。
    /// 秒数が切れたとき・シーンが変わるとき・100％で必ず外れる。弾を消す・返すのは呼んだ側（info.Source・info.HitPoint）
    /// </summary>
    public static KitGuard Guard(float seconds, Func<DamageInfo, bool> onHit, float invincibleAfter = 0.5f)
    {
        var c = Controller;
        Health h = c != null ? c.GetComponent<Health>() : null;
        return KitGuards.Add(h, seconds, onHit, invincibleAfter);
    }

    /// <summary>ライフを amount 回復する（最大より上にはしない）。回復できたら true</summary>
    public static bool Heal(int amount = 1)
    {
        var c = Controller;
        Health h = c != null ? c.GetComponent<Health>() : null;
        if (h == null || h.IsDead || h.Current >= h.Max) return false;
        h.Heal(amount);
        return true;
    }
}

/// <summary>ボスに足す入口</summary>
public static class KitBoss
{
    /// <summary>今戦っているボス（ステージの係が知っているボス → シーンの中の、動いているボス。無ければ null）</summary>
    public static BossController Current
    {
        get
        {
            StageRuleHost h = StageRuleHost.Current;
            if (h != null && h.Context != null && h.Context.Boss != null) return h.Context.Boss;
            foreach (var b in UnityEngine.Object.FindObjectsOfType<BossController>())
                if (b.isActiveAndEnabled && b.State != BossState.Normalized) return b;
            return null;
        }
    }

    /// <summary>
    /// seconds 秒、次の技を始めない・移動しない（技の途中は止めない）。
    /// 続けてかけても伸びない（残りの時間が seconds に戻るだけ）
    /// </summary>
    public static void Stun(BossController boss, float seconds)
    {
        if (boss != null) boss.Stun(seconds);
    }

    /// <summary>ひるんでいるか</summary>
    public static bool IsStunned(BossController boss) => boss != null && boss.IsStunned;

    /// <summary>ゲージを amount 戻す（0 より下にしない。第2形態は第2形態のまま）。戻した量を返す</summary>
    public static int RewindGauge(BossController boss, int amount)
    {
        if (boss == null || boss.Gauge == null) return 0;
        return boss.Gauge.Remove(amount);
    }

    /// <summary>技の途中か（予告も含む）</summary>
    public static bool InMove(BossController boss) => boss != null && boss.Runner != null && boss.Runner.IsRunning;

    /// <summary>第2形態か</summary>
    public static bool IsPhase2(BossController boss) => boss != null && boss.Phase >= 2;

    /// <summary>戦っている（形態変化・正常化・追い出しの間でない）か</summary>
    public static bool IsFighting(BossController boss) => boss != null && boss.State == BossState.Fighting;
}

/// <summary>弾に足す入口</summary>
public static class KitBullets
{
    static readonly List<Projectile> Buffer = new List<Projectile>();

    /// <summary>円の中の、敵の弾（Projectile）を消す。消した数を返す</summary>
    public static int ClearEnemyProjectiles(Vector2 center, float radius)
    {
        Buffer.Clear();
        Buffer.AddRange(UnityEngine.Object.FindObjectsOfType<Projectile>());
        float r2 = radius * radius;
        int n = 0;
        foreach (var p in Buffer)
        {
            if (p == null || p.Owner == null || p.Owner.Team != Team.Enemy) continue;
            if (((Vector2)p.transform.position - center).sqrMagnitude > r2) continue;
            UnityEngine.Object.Destroy(p.gameObject);
            n++;
        }
        Buffer.Clear();
        return n;
    }
}
