using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Steam のステージの決まり：実績解除（2026-11-19。docs/app-kits-2-design.md 2-3。まさぴ〜の例）。
/// 条件を満たすと実績の通知が出て、8秒間強くなる。プレイヤーの実績は右下（金）、ボスの実績は左下（紫）。
/// ・同じ実績は1ステージに1回。通知は同時に1つ（続いたら順番に）。効果は重ねず、残りの時間をのばす（16秒まで）
/// ・プレイヤー：与えるゲージ ×1.3・速さ ×1.15（HUD に剣と靴の印）
///   First Blood（雑魚を初めて倒す）／Combo x5（連続キル 5）／Speedrunner（雑魚の時間を 40秒以内）／
///   First Strike（ボスに初めて当てる）／Full Loadout（能力を 2種類ボスに当てる）／Untouchable（ボス戦で 20秒当たらない）
/// ・ボス：移動 ×1.3・待ち時間 ×0.8（予告の長さ・弾の速さは変えない）。ボスのゲージの下に紫の上向きの矢印（KitPips のボスの印）
///   20 sec survived（ボスが動き出して 20秒）／Gotcha（プレイヤーに初めて当てる）／Hat Trick（3回当てる）／Second Wind（50％）
/// ・字は通知（Steam の UI のパロディ）の中だけ。説明の帯は出さない
/// ・100％・やり直しで全部外す（StatMods は StageRuleHost も外す）
/// </summary>
public class SteamRule : StageRule
{
    /// <summary>【仮】強くなる秒数と、のばせる上限</summary>
    public const float BuffSeconds = 8f, BuffMax = 16f;
    /// <summary>【仮】プレイヤーの倍率</summary>
    public const float PlayerGauge = 1.3f, PlayerSpeed = 1.15f;
    /// <summary>【仮】ボスの倍率</summary>
    public const float BossSpeed = 1.3f, BossWait = 0.8f;
    /// <summary>【仮】条件の数</summary>
    public const float SpeedrunSeconds = 40f, UntouchableSeconds = 20f, SurviveSeconds = 20f;
    public const int ComboCount = 5, HatTrickHits = 3, LoadoutKinds = 2;

    public const string KeyPower = "steam.ach.power", KeySpeed = "steam.ach.speed";
    public const string KeyBossSpeed = "steam.boss.speed", KeyBossWait = "steam.boss.wait";

    readonly HashSet<string> _done = new HashSet<string>();
    readonly HashSet<string> _skillsOnBoss = new HashSet<string>();
    int _kills;
    int _bossHitsOnPlayer;
    float _lastPlayerHitAt;
    bool _hooked;
    bool _bossBuffOn; // ボスの強化が切れた瞬間の音のため
    Health _player;

    public override void Begin(StageRuleContext ctx)
    {
        _player = ctx.PlayerHealth;
        Health.AnyDied += OnAnyDied;
        Hitbox.AnyHit += OnAnyHit;
        if (_player != null) _player.Damaged += OnPlayerDamaged;
        _hooked = true;
        _lastPlayerHitAt = Time.time;
    }

    public override void Tick(float dt)
    {
        KillChain chain = KillChain.Current;
        if (chain != null && chain.Count >= ComboCount) Unlock("Combo x5", false);
        if (!Ctx.InBossFight) return;
        bool on = StatMods.Boss.Wait.Has(KeyBossWait);
        if (_bossBuffOn && !on) GameAudio.Event("gm_steam_boss_buff_end");
        _bossBuffOn = on;
        if (Ctx.BossFightSeconds >= SurviveSeconds) Unlock("20 sec survived", true);
        if (Time.time - _lastPlayerHitAt >= UntouchableSeconds) Unlock("Untouchable", false);
    }

    public override void OnPhase(PacePhase phase)
    {
        // 雑魚の時間が終わった（ボスの登場）
        if (phase == PacePhase.Done && _kills > 0 && Ctx.Elapsed <= SpeedrunSeconds) Unlock("Speedrunner", false);
    }

    public override void OnBossAppeared(BossController boss)
    {
        _lastPlayerHitAt = Time.time;
        _bossHitsOnPlayer = 0;
        _bossBuffOn = false;
    }

    public override void OnBossPhase(int phase)
    {
        if (phase >= 2) Unlock("Second Wind", true);
    }

    public override void End()
    {
        if (_hooked)
        {
            Health.AnyDied -= OnAnyDied;
            Hitbox.AnyHit -= OnAnyHit;
            if (_player != null) _player.Damaged -= OnPlayerDamaged;
            _hooked = false;
        }
        StatMods.ClearPrefix("steam.ach.");
        StatMods.ClearPrefix("steam.boss.");
        SteamToast.ClearAll();
        _bossBuffOn = false;
    }

    // ───────── 条件 ─────────

    void OnAnyDied(Health h)
    {
        if (!RewardKit.IsZako(h)) return;
        _kills++;
        Unlock("First Blood", false);
    }

    void OnAnyHit(Hitbox hb, IDamageable target, DamageInfo info, bool first)
    {
        if (hb == null || hb.Team != Team.Player) return;
        var c = target as Component;
        if (c == null || HitFx.BossOf(c.gameObject) == null) return;
        Unlock("First Strike", false);
        AttackData d = info.Data;
        if (d != null && d.Kind != AttackKind.Melee && hb.GetComponentInParent<PlayerMeleeAttack>() == null)
        {
            _skillsOnBoss.Add(d.name);
            if (_skillsOnBoss.Count >= LoadoutKinds) Unlock("Full Loadout", false);
        }
    }

    void OnPlayerDamaged(DamageInfo info)
    {
        _lastPlayerHitAt = Time.time;
        if (!Ctx.InBossFight) return;
        _bossHitsOnPlayer++;
        Unlock("Gotcha", true);
        if (_bossHitsOnPlayer >= HatTrickHits) Unlock("Hat Trick", true);
    }

    // ───────── 実績 ─────────

    void Unlock(string name, bool boss)
    {
        if (!_done.Add(name)) return;
        if (boss)
        {
            SteamToast.Show(name, true, new (Sprite, string)[] { (SteamArt.Boot, "+30%"), (SteamArt.BossUp, "+25%") }, ApplyBoss);
        }
        else
        {
            SteamToast.Show(name, false, new (Sprite, string)[] { (SteamArt.Sword, "+30%"), (SteamArt.Boot, "+15%") }, ApplyPlayer);
        }
    }

    static float Extend(StatChannel ch, string key)
    {
        float rem = ch.Remaining(key);
        if (float.IsInfinity(rem)) rem = 0f;
        return Mathf.Min(BuffMax, rem + BuffSeconds);
    }

    static void ApplyPlayer()
    {
        float t = Extend(StatMods.Player.GaugeGain, KeyPower);
        StatMods.Player.GaugeGain.SetFor(KeyPower, PlayerGauge, t, SteamArt.Sword);
        StatMods.Player.Speed.SetFor(KeySpeed, PlayerSpeed, t, SteamArt.Boot);
        GameAudio.Event("gm_steam_ach_player"); // 通知が出た瞬間（SteamToast の onShown）
    }

    static void ApplyBoss()
    {
        float t = Extend(StatMods.Boss.Wait, KeyBossWait);
        StatMods.Boss.Speed.SetFor(KeyBossSpeed, BossSpeed, t, SteamArt.BossUp); // ボスのゲージの下に紫の上向きの矢印
        StatMods.Boss.Wait.SetFor(KeyBossWait, BossWait, t);
        GameAudio.Event("gm_steam_ach_boss");
    }
}
