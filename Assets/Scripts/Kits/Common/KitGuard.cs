using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 【仮】プレイヤーの被弾を止める口（2026-11-19。担当C の頼み：Firefox の広告ブロック）。担当0 の物。
// ・KitPlayer.Guard(秒数, onHit) で見張りを足す。敵の攻撃の判定がプレイヤーに当たる直前に onHit(info) が呼ばれ、
//   true を返すとその当たりを止める（ダメージなし）。止めたあとは少しだけ無敵（同じ当たりが続けて来ないように）
// ・止めるのはプレイヤーの陣営の Health だけ。ボス・雑魚の当たりは止めない。落下などのダメージ以外の減り方も止めない（Health.Guard の決まり）
// ・必ず外れる：秒数が切れたとき・Release・シーンが変わるとき・100％（StageRuleHost）・守るプレイヤーが消えたとき
// ・弾を消す・弾を返すのは、呼んだ側でする（info.Source が当てた物、info.HitPoint が当たった所）

/// <summary>プレイヤーの被弾の見張り1つ（KitPlayer.Guard が返す）</summary>
public sealed class KitGuard
{
    internal Health Target;
    internal Func<DamageInfo, bool> OnHit;
    internal float Until;
    internal float InvincibleAfter;
    internal bool Released;

    /// <summary>まだ効いているか</summary>
    public bool Active => !Released && Target != null && Time.time < Until;
    /// <summary>止めた回数</summary>
    public int Blocked { get; internal set; }

    /// <summary>外す（何回呼んでもよい）</summary>
    public void Release()
    {
        Released = true;
        KitGuards.Prune();
    }
}

/// <summary>見張りの一覧（Health.Guard につなぐ係）</summary>
public static class KitGuards
{
    static readonly List<KitGuard> Guards = new List<KitGuard>();
    static readonly Func<Health, DamageInfo, bool> Hook = Check;

    internal static KitGuard Add(Health target, float seconds, Func<DamageInfo, bool> onHit, float invincibleAfter)
    {
        if (target == null || onHit == null || seconds <= 0f) return null;
        var g = new KitGuard { Target = target, OnHit = onHit, Until = Time.time + seconds, InvincibleAfter = Mathf.Max(0f, invincibleAfter) };
        Guards.Add(g);
        Health.Guard = Hook;
        return g;
    }

    /// <summary>全部外す（シーンの切り替え・100％）</summary>
    public static void ClearAll()
    {
        foreach (var g in Guards) g.Released = true;
        Guards.Clear();
        Unhook();
    }

    /// <summary>切れた見張りを外す。1つも無くなったら Health.Guard を外す</summary>
    internal static void Prune()
    {
        Guards.RemoveAll(g => !g.Active);
        if (Guards.Count == 0) Unhook();
    }

    static void Unhook()
    {
        if (Health.Guard == Hook) Health.Guard = null;
    }

    static bool Check(Health h, DamageInfo info)
    {
        Prune();
        if (h == null || h.Team != Team.Player || info.SourceTeam == Team.Player) return false;
        for (int i = 0; i < Guards.Count; i++)
        {
            KitGuard g = Guards[i];
            if (!g.Active || g.Target != h) continue;
            bool block;
            try { block = g.OnHit(info); }
            catch (Exception e) { Debug.LogException(e); block = false; }
            if (!block) continue;
            g.Blocked++;
            if (g.InvincibleAfter > 0f) h.SetInvincible(g.InvincibleAfter);
            return true;
        }
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        ClearAll();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ClearAll();
}
