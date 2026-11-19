using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】強さの倍率の入口（2026-11-19。docs/app-kits-1-design.md E2・app-kits-2-design.md 1-2）。担当0 の物。
// ・名前（鍵）を付けて足し、名前で外す。同じ種類は掛け算。掛けた結果を、最後に幅の中へ収める。
// ・結果は GameBuffs に入れる（GameBuffs を読む側は変えない）。GameBuffs に直接書く部品があっても消さないように、
//   「前に入れた分」との差だけを掛ける。
// ・外し忘れても、シーンが変わると全部外れる（GameBuffs.Reset が呼ぶ）。100％でも StageRuleHost が全部外す。
// ・鍵はキットの名前で始める（"slack.reaction"）。印（KitPips）に出すときは icon を渡す。
// ・クールタイムは「進み方の倍率」（1.5 ＝ 1.5倍の速さで進む）。「待つ時間 ×0.7」は 1÷0.7 で入れる。
// ・ボスの予告の長さと弾の速さは、どの倍率でも変えない（ここには種類が無い）。

/// <summary>倍率の種類1つ（例：プレイヤーの与えるゲージ）</summary>
public sealed class StatChannel
{
    struct Mod
    {
        public float Value;
        public float Until; // 0 ＝ずっと
        public Sprite Icon;
    }

    readonly Dictionary<string, Mod> _mods = new Dictionary<string, Mod>();
    readonly Action _apply;

    /// <summary>名前（デバッグ用）</summary>
    public string Name { get; }
    /// <summary>掛けた結果の下限・上限</summary>
    public float Min { get; }
    public float Max { get; }
    /// <summary>true：値が大きいほどプレイヤーに有利（印の矢印の色）</summary>
    public bool GoodWhenHigher { get; }
    /// <summary>true：敵（ボス・雑魚）の倍率（印はボスのゲージの横に出る）</summary>
    public bool EnemySide { get; }

    internal StatChannel(string name, float min, float max, bool goodWhenHigher, Action apply)
    {
        Name = name;
        Min = min;
        Max = max;
        GoodWhenHigher = goodWhenHigher;
        EnemySide = !name.StartsWith("Player.");
        _apply = apply;
    }

    /// <summary>今の値（全部を掛けて、幅の中へ収めた物。何も無ければ 1）</summary>
    public float Value
    {
        get
        {
            float v = 1f;
            foreach (var m in _mods.Values) v *= m.Value;
            return Mathf.Clamp(v, Min, Max);
        }
    }

    /// <summary>倍率を足す（同じ鍵は上書き）。icon を渡すと、HUD のライフの下に印が出る</summary>
    public void Set(string key, float multiplier, Sprite icon = null)
    {
        if (string.IsNullOrEmpty(key)) return;
        _mods[key] = new Mod { Value = Mathf.Max(0.01f, multiplier), Until = 0f, Icon = icon };
        _apply?.Invoke();
    }

    /// <summary>倍率を seconds 秒だけ足す（同じ鍵は、残りの時間を seconds に戻す）</summary>
    public void SetFor(string key, float multiplier, float seconds, Sprite icon = null)
    {
        if (string.IsNullOrEmpty(key)) return;
        _mods[key] = new Mod { Value = Mathf.Max(0.01f, multiplier), Until = Time.time + Mathf.Max(0.01f, seconds), Icon = icon };
        StatMods.EnsureTicker();
        _apply?.Invoke();
    }

    /// <summary>名前で外す（無くてもよい）</summary>
    public void Clear(string key)
    {
        if (string.IsNullOrEmpty(key) || !_mods.Remove(key)) return;
        _apply?.Invoke();
    }

    /// <summary>その鍵がかかっているか</summary>
    public bool Has(string key) => !string.IsNullOrEmpty(key) && _mods.ContainsKey(key);

    /// <summary>その鍵の残りの秒数（ずっとなら +∞、無ければ 0）</summary>
    public float Remaining(string key)
    {
        if (string.IsNullOrEmpty(key) || !_mods.TryGetValue(key, out Mod m)) return 0f;
        return m.Until <= 0f ? float.PositiveInfinity : Mathf.Max(0f, m.Until - Time.time);
    }

    /// <summary>鍵が prefix で始まる物を全部外す（"slack." など）</summary>
    public void ClearPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return;
        var remove = new List<string>();
        foreach (var k in _mods.Keys) if (k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) remove.Add(k);
        if (remove.Count == 0) return;
        foreach (var k in remove) _mods.Remove(k);
        _apply?.Invoke();
    }

    /// <summary>全部外す</summary>
    public void ClearAll()
    {
        if (_mods.Count == 0) return;
        _mods.Clear();
        _apply?.Invoke();
    }

    internal void ClearSilently() => _mods.Clear();

    /// <summary>時間切れの物を外す（外したら true）</summary>
    internal bool Expire(float now)
    {
        List<string> remove = null;
        foreach (var kv in _mods)
        {
            if (kv.Value.Until > 0f && now >= kv.Value.Until) (remove ??= new List<string>()).Add(kv.Key);
        }
        if (remove == null) return false;
        foreach (var k in remove) _mods.Remove(k);
        _apply?.Invoke();
        return true;
    }

    internal bool AnyTimed()
    {
        foreach (var m in _mods.Values) if (m.Until > 0f) return true;
        return false;
    }

    /// <summary>印に出す物（鍵・アイコン・倍率）</summary>
    internal void CollectPips(List<StatMods.Pip> into)
    {
        foreach (var kv in _mods)
        {
            if (kv.Value.Icon == null) continue;
            bool up = kv.Value.Value >= 1f;
            into.Add(new StatMods.Pip { Key = kv.Key, Icon = kv.Value.Icon, Up = up, Good = up == GoodWhenHigher });
        }
    }
}

/// <summary>強さの倍率（プレイヤー・ボス・雑魚）。例：StatMods.Player.GaugeGain.Set("slack.reaction", 1.5f)</summary>
public static class StatMods
{
    /// <summary>プレイヤーの倍率</summary>
    public static class Player
    {
        /// <summary>与えるゲージ（と雑魚へのダメージ）の倍率。幅 0.5〜1.6</summary>
        public static readonly StatChannel GaugeGain = new StatChannel("Player.GaugeGain", 0.5f, 1.6f, true, Apply);
        /// <summary>雑魚へのダメージだけの倍率。幅 0.5〜1.6</summary>
        public static readonly StatChannel Damage = new StatChannel("Player.Damage", 0.5f, 1.6f, true, Apply);
        /// <summary>クールタイムの進み方の倍率（1.5 ＝ 1.5倍の速さで進む）。幅 0.6〜1.7</summary>
        public static readonly StatChannel CooldownRate = new StatChannel("Player.CooldownRate", 0.6f, 1.7f, true, Apply);
        /// <summary>横の速さの倍率。幅 0.6〜1.4</summary>
        public static readonly StatChannel Speed = new StatChannel("Player.Speed", 0.6f, 1.4f, true, Apply);
        /// <summary>近接だけの、与えるゲージの倍率（GaugeGain にさらに掛かる）。幅 0.5〜1.6（2026-11-19：Word の太字）</summary>
        public static readonly StatChannel MeleeGauge = new StatChannel("Player.MeleeGauge", 0.5f, 1.6f, true, Apply);
        /// <summary>近接の届く長さ（判定の横幅）の倍率。幅 0.6〜1.6（2026-11-19：Word の太字）</summary>
        public static readonly StatChannel MeleeReach = new StatChannel("Player.MeleeReach", 0.6f, 1.6f, true, Apply);
    }

    /// <summary>ボスの倍率</summary>
    public static class Boss
    {
        /// <summary>攻撃のあとの待ち時間の倍率（0.8 ＝ 短い＝強い）。幅 0.7〜1.5</summary>
        public static readonly StatChannel Wait = new StatChannel("Boss.Wait", 0.7f, 1.5f, true, Apply);
        /// <summary>移動の速さの倍率。幅 0.5〜1.6</summary>
        public static readonly StatChannel Speed = new StatChannel("Boss.Speed", 0.5f, 1.6f, false, Apply);
        /// <summary>受けるゲージの倍率（1.2 ＝ ゲージが早くたまる）。幅 0.5〜1.6</summary>
        public static readonly StatChannel GaugeTaken = new StatChannel("Boss.GaugeTaken", 0.5f, 1.6f, true, Apply);
    }

    /// <summary>雑魚の倍率</summary>
    public static class Zako
    {
        /// <summary>移動の速さの倍率。幅 0.5〜1.6（読む側は GameBuffs.ZakoSpeed）</summary>
        public static readonly StatChannel Speed = new StatChannel("Zako.Speed", 0.5f, 1.6f, false, Apply);
    }

    /// <summary>HUD の印1つ</summary>
    public struct Pip
    {
        public string Key;
        public Sprite Icon;
        /// <summary>倍率が 1 以上か（矢印の向き）</summary>
        public bool Up;
        /// <summary>プレイヤーに有利か（矢印の色）</summary>
        public bool Good;
    }

    /// <summary>倍率が変わったとき</summary>
    public static event Action Changed;

    static IEnumerable<StatChannel> All()
    {
        yield return Player.GaugeGain;
        yield return Player.Damage;
        yield return Player.CooldownRate;
        yield return Player.Speed;
        yield return Player.MeleeGauge;
        yield return Player.MeleeReach;
        yield return Boss.Wait;
        yield return Boss.Speed;
        yield return Boss.GaugeTaken;
        yield return Zako.Speed;
    }

    /// <summary>全部外す（100％・ステージの終わり）</summary>
    public static void ClearAll()
    {
        foreach (var c in All()) c.ClearSilently();
        Apply();
    }

    /// <summary>鍵が prefix で始まる物を、全部の種類から外す（"slack." など）</summary>
    public static void ClearPrefix(string prefix)
    {
        foreach (var c in All()) c.ClearPrefix(prefix);
    }

    /// <summary>その鍵を、全部の種類から外す</summary>
    public static void Clear(string key)
    {
        foreach (var c in All()) c.Clear(key);
    }

    /// <summary>HUD の印に出す物（アイコンを渡して足した物）。enemySide：false＝プレイヤーの倍率（ライフの下）、true＝ボス・雑魚の倍率（ボスのゲージの横）</summary>
    public static void CollectPips(List<Pip> into, bool enemySide = false)
    {
        foreach (var c in All()) if (c.EnemySide == enemySide) c.CollectPips(into);
    }

    // ───────── GameBuffs に入れる ─────────

    // 前に GameBuffs に掛けた分（差だけを掛けるため）
    static float _gauge = 1f, _damage = 1f, _cooldown = 1f, _pSpeed = 1f, _bWait = 1f, _bSpeed = 1f, _bGauge = 1f, _zSpeed = 1f, _mGauge = 1f, _mReach = 1f;

    static void Apply()
    {
        float gauge = Player.GaugeGain.Value;
        float damage = Mathf.Clamp(Player.GaugeGain.Value * Player.Damage.Value, 0.5f, 1.6f);
        float cooldown = 1f / Mathf.Max(0.01f, Player.CooldownRate.Value); // GameBuffs は「待つ時間の倍率」
        float pSpeed = Player.Speed.Value;
        float bWait = Boss.Wait.Value;
        float bSpeed = Boss.Speed.Value;
        float bGauge = Boss.GaugeTaken.Value;
        float zSpeed = Zako.Speed.Value;

        GameBuffs.PlayerGauge *= gauge / _gauge; _gauge = gauge;
        GameBuffs.PlayerDamage *= damage / _damage; _damage = damage;
        GameBuffs.PlayerCooldown *= cooldown / _cooldown; _cooldown = cooldown;
        GameBuffs.PlayerSpeed *= pSpeed / _pSpeed; _pSpeed = pSpeed;
        GameBuffs.BossWait *= bWait / _bWait; _bWait = bWait;
        GameBuffs.BossSpeed *= bSpeed / _bSpeed; _bSpeed = bSpeed;
        GameBuffs.BossGaugeTaken *= bGauge / _bGauge; _bGauge = bGauge;
        GameBuffs.ZakoSpeed *= zSpeed / _zSpeed; _zSpeed = zSpeed;
        float mGauge = Player.MeleeGauge.Value, mReach = Player.MeleeReach.Value;
        GameBuffs.PlayerMeleeGauge *= mGauge / _mGauge; _mGauge = mGauge;
        GameBuffs.PlayerMeleeReach *= mReach / _mReach; _mReach = mReach;
        KitMeleeReach.Apply();
        try { Changed?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    /// <summary>GameBuffs.Reset から呼ぶ（全部 1 に戻した直後。ここでは GameBuffs に掛けない）</summary>
    internal static void ResetFromGameBuffs()
    {
        foreach (var c in All()) c.ClearSilently();
        _gauge = _damage = _cooldown = _pSpeed = _bWait = _bSpeed = _bGauge = _zSpeed = _mGauge = _mReach = 1f;
    }

    // ───────── 時間で外す ─────────

    static StatModsTicker _ticker;

    internal static void EnsureTicker()
    {
        if (_ticker != null) return;
        var go = new GameObject("StatModsTicker");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        _ticker = go.AddComponent<StatModsTicker>();
    }

    internal static void Tick()
    {
        float now = Time.time;
        foreach (var c in All()) c.Expire(now);
    }
}

/// <summary>時間で外す倍率を見張る（StatMods が作る。見えない）</summary>
public class StatModsTicker : MonoBehaviour
{
    void Update() => StatMods.Tick();
}
