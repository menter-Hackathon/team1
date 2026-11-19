using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキル枠のボタン（1／2／3）が押されたら、そのスキルの攻撃データを実行する。クールタイムもここで数える。
/// ・クールタイムは攻撃データの「間隔」。使った瞬間から数える
/// ・クールタイムは能力ごとに覚える。捨てて拾い直しても、別の枠に入れ直しても、残り時間はそのまま
/// ・使えるかどうか（のけぞり中・入れ替え待ち中は使えない）は PlayerSkills が判断して SkillUseRequested を出す
/// ・召喚（AI アシスタント。2026-11-18）は、味方が消えてからクールタイムを数える。味方がいる間は使えない
/// ・チャット（Slack・Discord・LINE。2026-11-19）は、止まっている間は続けて押すとメッセージが増え（最大数まで、クールタイムなし）、
///   動くか最大数まで出したら、そこからクールタイムを数える
/// ・「順番に切り替わる攻撃」があるスキル（Office）は、使うたびに次の攻撃へ進む。次の攻撃は GetNextAttack と
///   NextAttackChanged で分かる（スロットのアイコンを変える用）。順番も能力ごとに覚える
/// ・攻撃データが無く「装備中ずっと効く効果」だけのスキル（Steam）は、ボタンを押しても何もしない
/// </summary>
[RequireComponent(typeof(PlayerSkills), typeof(AttackRunner))]
public class PlayerSkillExecutor : MonoBehaviour
{
    PlayerSkills _skills;
    AttackRunner _runner;

    /// <summary>クールタイムの倍率（状態異常「クールタイムが長い」。ふだんは1。使った瞬間の倍率で決まる。2026-11-19）</summary>
    public float CooldownMultiplier { get; set; } = 1f;

    /// <summary>能力を実行する AttackRunner（HUD が、専用の技の状態を聞くとき用）</summary>
    public AttackRunner Runner => _runner;
    // 能力ごとの、クールタイムの残り（倍率をかける前の秒数。毎フレーム、進み方の倍率で減らす）
    readonly Dictionary<SkillDefinition, float> _remain = new Dictionary<SkillDefinition, float>();
    readonly Dictionary<SkillDefinition, float> _cooldownTotal = new Dictionary<SkillDefinition, float>(); // 今のクールタイムの長さ（ゲージの割合用）
    readonly List<SkillDefinition> _keys = new List<SkillDefinition>();
    // 終わってからクールタイムを数える能力（召喚・チャット）。攻撃データ → 能力。攻撃が終わったら、そこからクールタイムを数える
    readonly Dictionary<AttackData, SkillDefinition> _waitingFinish = new Dictionary<AttackData, SkillDefinition>();
    // 終わるのを待っている能力の、念のための期限（Finished を受け取れなかったときは、この時刻を過ぎたら使える）
    readonly Dictionary<SkillDefinition, float> _waitUntil = new Dictionary<SkillDefinition, float>();
    // 順番に切り替わる攻撃（Office）の、次の番号
    readonly Dictionary<SkillDefinition, int> _turn = new Dictionary<SkillDefinition, int>();
    // 封印（2026-11-19）：全部の枠と、枠ごとの「使えるようになる時刻」
    float _sealAllUntil;
    readonly float[] _sealSlotUntil = new float[8];

    /// <summary>スキルを使ったとき（枠の番号, スキル）。SE・エフェクト・UI 用</summary>
    public event Action<int, SkillDefinition> SkillUsed;
    /// <summary>クールタイム中で使えなかったとき（枠の番号, スキル, 残り秒数）。UI の「まだ使えない」表示用</summary>
    public event Action<int, SkillDefinition, float> SkillOnCooldown;
    /// <summary>順番に切り替わるスキルの、次の攻撃が変わったとき（スキル, 次の攻撃データ）。スロットのアイコン用</summary>
    public event Action<SkillDefinition, AttackData> NextAttackChanged;

    void Awake()
    {
        _skills = GetComponent<PlayerSkills>();
        _runner = GetComponent<AttackRunner>();
    }

    void OnEnable()
    {
        _skills.SkillUseRequested += OnSkillUseRequested;
        _runner.Finished += OnAttackFinished;
        SkillUsed += ShowSkillIcon;
    }

    void OnDisable()
    {
        _skills.SkillUseRequested -= OnSkillUseRequested;
        _runner.Finished -= OnAttackFinished;
        SkillUsed -= ShowSkillIcon;
        if (_usedIcon != null) _usedIcon.enabled = false;
    }

    SpriteRenderer _usedIcon;
    float _usedIconAge;
    float _usedIconScale;

    void ShowSkillIcon(int slot, SkillDefinition skill)
    {
        Sprite icon = null;
        var session = GameSession.Instance;
        if (session != null && session.Run != null) icon = session.Run.SourceIcon(skill);
        if (icon == null) icon = skill.Icon;
        if (icon == null) return;
        if (_usedIcon == null)
        {
            var go = new GameObject("UsedAppIcon");
            go.transform.SetParent(transform, false);
            _usedIcon = go.AddComponent<SpriteRenderer>();
            _usedIcon.sortingOrder = RewardKit.OrderBadge + 3;
        }
        _usedIcon.sprite = icon;
        _usedIconScale = RewardKit.IconScale(icon, 18f);
        _usedIconAge = 0f;
        _usedIcon.enabled = true;
        UpdateSkillIcon();
    }

    void LateUpdate()
    {
        if (_usedIcon == null || !_usedIcon.enabled) return;
        _usedIconAge += Time.deltaTime;
        if (_usedIconAge >= 0.65f) { _usedIcon.enabled = false; return; }
        UpdateSkillIcon();
    }

    void UpdateSkillIcon()
    {
        float t = Mathf.Clamp01(_usedIconAge / 0.65f);
        Vector2 head = FxKit.HeadOf(gameObject, (Vector2)transform.position + Vector2.up);
        _usedIcon.transform.position = FxKit.ClampToView(head + Vector2.up * (0.65f + t * 0.22f), 0.7f);
        float pop = 1f + Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI) * 0.18f;
        // 親の左右反転や拡大縮小をアイコンに伝えない。
        Vector3 parentScale = transform.lossyScale;
        _usedIcon.transform.localScale = new Vector3(
            _usedIconScale * pop / (Mathf.Abs(parentScale.x) > 0.001f ? parentScale.x : 1f),
            _usedIconScale * pop / (Mathf.Abs(parentScale.y) > 0.001f ? parentScale.y : 1f), 1f);
        _usedIcon.color = new Color(1f, 1f, 1f, 1f - Mathf.InverseLerp(0.65f, 1f, t));
    }

    void OnAttackFinished(AttackData attack)
    {
        if (attack == null || !_waitingFinish.TryGetValue(attack, out SkillDefinition skill)) return;
        _waitingFinish.Remove(attack);
        _waitUntil.Remove(skill);
        StartCooldown(skill, attack.Cooldown);
    }

    void OnSkillUseRequested(int index, SkillDefinition skill) => TryUse(index, skill);

    /// <summary>クールタイムの進み方の倍率（1.5 ＝ 1.5倍の速さで進む。GameBuffs.PlayerCooldown の逆数）</summary>
    static float Rate => 1f / Mathf.Max(0.1f, GameBuffs.PlayerCooldown);

    void Update()
    {
        if (_remain.Count == 0) return;
        float step = Time.deltaTime * Rate;
        if (step <= 0f) return;
        _keys.Clear();
        _keys.AddRange(_remain.Keys);
        foreach (var k in _keys)
        {
            float r = _remain[k] - step;
            if (r <= 0f || k == null)
            {
                _remain.Remove(k);
                if (k != null) GameAudio.Event("skill_ready");
            }
            else _remain[k] = r;
        }
    }

    /// <summary>次に使ったときに出る攻撃データ（順番に切り替わらないスキルは、いつも攻撃データ）</summary>
    public AttackData GetNextAttack(SkillDefinition skill)
    {
        if (skill == null) return null;
        _turn.TryGetValue(skill, out int turn);
        return skill.GetAttack(turn);
    }

    /// <summary>スキルを使う。使えたら true</summary>
    public bool TryUse(int index, SkillDefinition skill)
    {
        if (skill == null) return false;
        AttackData attack = GetNextAttack(skill);
        if (attack == null)
        {
            // 常に効く効果だけのスキル（Steam）は、押しても何もしない
            if (skill.Passive == null) Debug.LogWarning($"{skill.DisplayName}: 攻撃データが設定されていないので使えません", skill);
            return false;
        }

        // チャット（Slack・Discord・LINE）：ためが続いている間（止まっていて最大数まで）は、クールタイムなしでメッセージを足す
        if (attack.Kind == AttackKind.Chat && ChatVolley.TryAppend(_runner, attack))
        {
            SkillUsed?.Invoke(index, skill);
            return true;
        }

        float remain = GetCooldownRemaining(skill);
        if (remain > 0f)
        {
            SkillOnCooldown?.Invoke(index, skill, remain);
            GameAudio.Event("skill_denied");
            return false;
        }
        float sealed_ = SealRemaining(index);
        if (sealed_ > 0f)
        {
            SkillOnCooldown?.Invoke(index, skill, sealed_); // 封印中（2026-11-19）
            GameAudio.Event("skill_denied");
            return false;
        }
        DressFor(skill, attack);
        if (!_runner.Run(attack))
        {
            GameAudio.Event("skill_denied"); // 相手がいない・使えない
            return false;
        }

        if (attack.Kind == AttackKind.Summon)
        {
            // 味方が消えるまでは使えない。消えたこと（Finished）を受け取れなかったときのために、いられる最大の秒数の後には使えるようにしておく
            _waitUntil[skill] = Time.time + attack.SummonLifetime + attack.Cooldown;
            _waitingFinish[attack] = skill;
        }
        else if (attack.Kind == AttackKind.Chat)
        {
            // ためが終わる（動く・最大数まで出す）まではクールタイムを数えない。終わったら Finished から数える。
            // 受け取れなかったときのために、待つ秒数×最大数の後には使えるようにしておく
            _waitUntil[skill] = Time.time + attack.ChatFireDelay * attack.ChatMaxMessages + attack.Cooldown;
            _waitingFinish[attack] = skill;
        }
        else
        {
            // 2回押しの能力（zip圧縮）は、1回目のあとはクールタイムを始めない（2026-11-18）
            bool defer = attack.Kind == AttackKind.Custom && attack.Custom != null && attack.Custom.DefersCooldown(_runner);
            if (!defer) StartCooldown(skill, attack.Cooldown);
        }
        if (skill.HasAttackCycle)
        {
            _turn.TryGetValue(skill, out int turn);
            _turn[skill] = turn + 1;
            NextAttackChanged?.Invoke(skill, GetNextAttack(skill));
        }
        SkillUsed?.Invoke(index, skill);
        return true;
    }

    // クールタイムを数え始める（倍率をかける前の秒数で覚える。進み方の倍率は Update で）
    void StartCooldown(SkillDefinition skill, float seconds)
    {
        float cd = seconds * CooldownMultiplier;
        if (skill == null || cd <= 0f) return;
        _remain[skill] = cd;
        _cooldownTotal[skill] = cd;
    }

    /// <summary>
    /// 能力の見た目（スキン・アプリの色・アプリのアイコン）を入れる（2026-11-18：スキルの見た目 1-1）。
    /// 能力は同時に使えるので、攻撃データごとに覚えさせる（SkinDresser.Remember）。AttackRunner.Skin は最後に使った能力
    /// </summary>
    void DressFor(SkillDefinition skill, AttackData attack)
    {
        SkinId skin = SkinTables.ForSkill(skill);
        Sprite icon = null;
        GameSession session = GameSession.Instance;
        if (session != null && session.Run != null) icon = session.Run.SourceIcon(skill);
        if (icon == null) icon = skill.Icon;
        SkinDresser.Remember(attack, skin, skill.Theme, icon);
        _runner.Skin = skin;
        _runner.SkinTheme = skill.Theme;
        _runner.SkinIcon = icon;
    }

    /// <summary>クールタイムの残り秒数（今の進み方の倍率での秒数。使えるなら 0）</summary>
    public float GetCooldownRemaining(SkillDefinition skill)
    {
        if (skill == null) return 0f;
        // チャットのためが続いている間は、まだ押せるので「使える」扱い
        if (skill.Attack != null && skill.Attack.Kind == AttackKind.Chat && ChatVolley.IsOpenFor(_runner, skill.Attack)) return 0f;
        float remain = _remain.TryGetValue(skill, out float r) ? r / Rate : 0f;
        if (_waitUntil.TryGetValue(skill, out float until)) remain = Mathf.Max(remain, until - Time.time); // 召喚・チャットが終わるのを待っている
        return Mathf.Max(0f, remain);
    }

    /// <summary>クールタイムの残りの割合（1＝使った直後、0＝使える）。UI のゲージ用</summary>
    public float GetCooldownRatio(SkillDefinition skill)
    {
        if (skill == null) return 0f;
        if (_waitUntil.TryGetValue(skill, out float until) && until > Time.time) return 1f; // 終わるのを待っている間は、いっぱいのまま
        if (!_remain.TryGetValue(skill, out float r)) return 0f;
        float total = _cooldownTotal.TryGetValue(skill, out float t) && t > 0f ? t : r;
        return Mathf.Clamp01(r / total);
    }

    // ───────── 2026-11-19：アプリのキットの入口（E5。KitPlayer から呼ぶ） ─────────

    /// <summary>全部の能力のクールタイムを seconds 秒進める（倍率をかける前の秒数）</summary>
    public void AdvanceCooldowns(float seconds)
    {
        if (seconds <= 0f || _remain.Count == 0) return;
        _keys.Clear();
        _keys.AddRange(_remain.Keys);
        foreach (var k in _keys)
        {
            float r = _remain[k] - seconds;
            if (r <= 0f) _remain.Remove(k);
            else _remain[k] = r;
        }
    }

    /// <summary>全部の能力のクールタイムを回復する（すぐ使える）</summary>
    public void RefillCooldowns() => _remain.Clear();

    /// <summary>能力の枠を seconds 秒使えなくする。slot が負なら全部の枠。重ねたら長い方</summary>
    public void Seal(float seconds, int slot = -1)
    {
        float until = Time.time + Mathf.Max(0f, seconds);
        if (slot < 0) _sealAllUntil = Mathf.Max(_sealAllUntil, until);
        else if (slot < _sealSlotUntil.Length) _sealSlotUntil[slot] = Mathf.Max(_sealSlotUntil[slot], until);
    }

    /// <summary>封印を全部とく</summary>
    public void Unseal()
    {
        _sealAllUntil = 0f;
        for (int i = 0; i < _sealSlotUntil.Length; i++) _sealSlotUntil[i] = 0f;
    }

    /// <summary>その枠が封印されているか</summary>
    public bool IsSealed(int slot) => SealRemaining(slot) > 0f;

    /// <summary>その枠の封印の残り秒数（無ければ 0）</summary>
    public float SealRemaining(int slot)
    {
        float until = _sealAllUntil;
        if (slot >= 0 && slot < _sealSlotUntil.Length) until = Mathf.Max(until, _sealSlotUntil[slot]);
        return Mathf.Max(0f, until - Time.time);
    }
}
