using UnityEngine;

/// <summary>
/// 【仮】Claude のステージのギミック「使用上限」（2026-11-19。docs/app-kits-1-design.md 6章）。担当3 の物。
/// 【要相談：不利になる「あるある」を入れてよいか】→ 仕様書のかっこ内のとおり入れる。
/// ・HUD のライフの下に、細いオレンジのメーター（KitMeter）。能力を 1回使うと 1/6 減る。
///   能力を使わずに 4秒たつごとに 1/6 戻る（使うと数え直し。続けて 6回使うと、ちょうど 0 になる）。近接を当てると 1/12 戻る
/// ・半分以上あるとき：クールタイムが 1.2倍の速さで進む（StatMods。印も出る）
/// ・0 になると：4秒、能力が全部使えない（KitPlayer.Seal。メーターは灰色で、左から戻っていく）。戻ったら満タン
/// ・雑魚の時間もボス戦も同じ。100％・ステージの終わりで、メーター・倍率・封印が全部戻る
/// ・文字は出さない（封印は HUD の枠の鍵と、灰色のメーターで分かる）
/// </summary>
public class ClaudeRule : StageRule
{
    const int Uses = 6;
    const float RegenSeconds = 4f;     // 能力を使わずにこの秒数たつと 1/6 戻る
    const float MeleeGain = 1f / 12f;
    const float SealSeconds = 4f;
    const float BoostRate = 1.2f;

    static readonly Color SealGray = RuntimeSprites.Hex("8E8E93");

    float _value = 1f;
    bool _sealed;
    float _sealUntil;
    bool _boost;
    float _idle;
    PlayerSkillExecutor _exec;
    bool _hitHooked;
    bool _ended;
    string _meterKey;
    string _boostKey;
    Sprite _icon;

    public override void Begin(StageRuleContext ctx)
    {
        _meterKey = ctx.Key("usage");
        _boostKey = ctx.Key("usage.boost");
        _icon = ctx.Icon != null ? ctx.Icon : ClaudeArt.Icon8;
        Hook();
        UpdateBoost();
        Draw();
    }

    void Hook()
    {
        if (_ended) return;
        if (_exec == null)
        {
            _exec = KitPlayer.Executor;
            if (_exec != null) _exec.SkillUsed += OnSkillUsed;
        }
        if (!_hitHooked)
        {
            Hitbox.AnyHit += OnAnyHit;
            _hitHooked = true;
        }
    }

    public override void Tick(float dt)
    {
        if (_exec == null) Hook();
        if (_sealed)
        {
            if (Time.time >= _sealUntil)
            {
                _sealed = false;
                _value = 1f;
                GameAudio.Event("gm_claude_reset");
            }
        }
        else if (_value < 1f)
        {
            _idle += dt;
            if (_idle >= RegenSeconds)
            {
                _idle -= RegenSeconds;
                _value = Mathf.Min(1f, _value + 1f / Uses);
            }
        }
        else _idle = 0f;
        UpdateBoost();
        Draw();
    }

    void OnSkillUsed(int slot, SkillDefinition skill)
    {
        if (_ended || _sealed) return;
        _idle = 0f;
        _value -= 1f / Uses;
        if (_value <= 0.001f)
        {
            _value = 0f;
            SealNow();
        }
        else if (_value <= 1f / Uses + 0.001f) GameAudio.Event("gm_claude_low"); // あと1回で上限（メーターの点滅と同じ条件）
        else GameAudio.Event("gm_claude_use", 0.85f + 0.3f * _value);
        UpdateBoost();
        Draw();
    }

    void OnAnyHit(Hitbox hitbox, IDamageable target, DamageInfo info, bool first)
    {
        // first は「この判定がこの相手に初めて当てたか」（近接の判定は使い回すので、見ない）
        if (_ended || _sealed || hitbox == null || hitbox.Team != Team.Player) return;
        if (hitbox.GetComponentInParent<PlayerMeleeAttack>() == null) return; // 近接だけ
        _value = Mathf.Min(1f, _value + MeleeGain);
        UpdateBoost();
        Draw();
    }

    void SealNow()
    {
        _sealed = true;
        _sealUntil = Time.time + SealSeconds;
        _idle = 0f;
        KitPlayer.Seal(SealSeconds);
        GameAudio.Event("gm_claude_limit");
    }

    void UpdateBoost()
    {
        bool want = !_ended && !_sealed && _value >= 0.5f;
        if (want == _boost) return;
        _boost = want;
        if (want) StatMods.Player.CooldownRate.Set(_boostKey, BoostRate, _icon);
        else StatMods.Player.CooldownRate.Clear(_boostKey);
    }

    void Draw()
    {
        if (_ended) return;
        if (_sealed)
        {
            float r = 1f - Mathf.Clamp01((_sealUntil - Time.time) / SealSeconds);
            KitMeter.Set(_meterKey, r, SealGray, _icon, false);
        }
        else KitMeter.Set(_meterKey, _value, ClaudeArt.Orange, _icon, _value <= 1f / Uses + 0.001f);
    }

    public override void End()
    {
        if (_ended) return;
        _ended = true;
        if (_exec != null) _exec.SkillUsed -= OnSkillUsed;
        _exec = null;
        if (_hitHooked) Hitbox.AnyHit -= OnAnyHit;
        _hitHooked = false;
        KitMeter.Clear(_meterKey);
        StatMods.Player.CooldownRate.Clear(_boostKey);
        _boost = false;
        if (_sealed) KitPlayer.Unseal();
        _sealed = false;
    }
}
