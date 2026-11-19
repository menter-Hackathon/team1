using UnityEngine;

/// <summary>
/// 【仮】Excel のステージのギミック「右肩上がり」（2026-11-19。docs/app-kits-1-design.md 10章）。数値はすべて【仮】。
/// ・ダメージを受けずに雑魚を 3体倒す（ボス戦は、近接を 8回当てる）ごとに、棒が1本伸びる（5本まで）
/// ・1本ごとに、与えるゲージと雑魚へのダメージ +8％（5本で +40％。StatMods）。HUD のライフの下に印とメーター
/// ・ダメージを受けると 0本に戻る（グラフが #DIV/0! になって崩れる）。倍率も戻る
/// ・ステージの最初からボス戦の終わり（100％）まで持ち越す。100％・やり直しで全部外れる（StageRuleHost）
/// </summary>
public class ExcelRule : StageRule
{
    /// <summary>【仮】棒の上限</summary>
    public const int MaxBars = 5;
    /// <summary>【仮】棒1本の倍率</summary>
    public const float PerBar = 0.08f;
    /// <summary>【仮】雑魚の時間：棒1本に要る数</summary>
    public const int ZakoPerBar = 3;
    /// <summary>【仮】ボス戦：棒1本に要る近接の数</summary>
    public const int MeleePerBar = 8;

    int _bars;
    int _count;
    bool _hooked;
    string _key;

    /// <summary>今の棒の数（テスト用）</summary>
    public int Bars => _bars;

    public override void Begin(StageRuleContext ctx)
    {
        _key = ctx.Key("rise");
        Health.AnyDied += OnAnyDied;
        Hitbox.AnyHit += OnAnyHit;
        if (ctx.PlayerHealth != null) ctx.PlayerHealth.Damaged += OnPlayerDamaged;
        _hooked = true;
        Refresh();
    }

    public override void OnBossAppeared(BossController boss)
    {
        _count = 0; // 数え方が変わる（雑魚 → 近接）。棒はそのまま
    }

    public override void End()
    {
        if (_hooked)
        {
            Health.AnyDied -= OnAnyDied;
            Hitbox.AnyHit -= OnAnyHit;
            if (Ctx != null && Ctx.PlayerHealth != null) Ctx.PlayerHealth.Damaged -= OnPlayerDamaged;
            _hooked = false;
        }
        if (_key != null)
        {
            StatMods.Player.GaugeGain.Clear(_key);
            KitMeter.Clear(_key);
        }
        _bars = 0;
        _count = 0;
    }

    void OnAnyDied(Health h)
    {
        if (h == null || h.Team != Team.Enemy || Ctx == null || Ctx.InBossFight) return;
        Count(ZakoPerBar);
    }

    void OnAnyHit(Hitbox hitbox, IDamageable target, DamageInfo info, bool first)
    {
        if (Ctx == null || !Ctx.InBossFight || hitbox == null || hitbox.Team != Team.Player) return;
        if (!(target is VirusGauge)) return;
        if (hitbox.GetComponentInParent<PlayerMeleeAttack>() == null) return; // 近接だけ
        Count(MeleePerBar);
    }

    void Count(int per)
    {
        if (_bars >= MaxBars) return;
        _count++;
        if (_count < per) return;
        _count = 0;
        _bars++;
        if (_bars >= MaxBars) GameAudio.Event("gm_excel_bar_max");
        else GameAudio.Event("gm_excel_bar_up", 1f + 0.07f * (_bars - 1));
        Refresh();
        // 伸びた：プレイヤーの上に、右肩上がりの小さい棒グラフ
        Vector2 at = PlayerHead();
        FxAnim.Sprite(ExcelArt.MiniChart(_bars), at, 60, 0.8f, null, 1.5f, "ExcelRise").Rise(0.7f).Pop();
    }

    void OnPlayerDamaged(DamageInfo info)
    {
        if (_bars == 0 && _count == 0) return;
        bool had = _bars > 0;
        _bars = 0;
        _count = 0;
        Refresh();
        if (!had) return;
        GameAudio.Event("gm_excel_crash");
        // 崩れる：#DIV/0! と、ばらける棒
        Vector2 at = PlayerHead();
        FxAnim.Sprite(OfficeArt.ErrorCell("#DIV/0!", 1), at, 60, 0.7f, null, 1.5f, "ExcelCrash").Rise(0.4f).Pop();
        for (int i = 0; i < 5; i++)
        {
            Color c = i % 3 == 0 ? ExcelArt.Blue : i % 3 == 1 ? ExcelArt.Orange : ExcelArt.Yellow;
            FxAnim.Sprite(OfficeArt.BarBit(c), at, 59, 0.6f, null, 1.5f, "ExcelBit")
                .Velocity(new Vector2(Random.Range(-3f, 3f), Random.Range(2f, 5f))).Gravity(18f).Spin(Random.Range(-360f, 360f));
        }
    }

    void Refresh()
    {
        if (_key == null) return;
        Sprite icon = Ctx != null ? Ctx.Icon : null;
        if (_bars > 0) StatMods.Player.GaugeGain.Set(_key, 1f + PerBar * _bars, icon);
        else StatMods.Player.GaugeGain.Clear(_key);
        KitMeter.Set(_key, (float)_bars / MaxBars, ExcelArt.Green, icon);
    }

    Vector2 PlayerHead()
    {
        Transform p = Ctx != null ? Ctx.Player : null;
        return p != null ? (Vector2)p.position + new Vector2(0f, 1.8f) : Vector2.zero;
    }
}
