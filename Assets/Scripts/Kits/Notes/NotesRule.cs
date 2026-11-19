using UnityEngine;

/// <summary>
/// 【仮】メモのステージのギミック「チェックリスト」（2026-11-19。docs/app-kits-1-design.md 14章。担当0）。
/// ・ステージの最初に、画面の左上（HUD の下）に小さいメモが 1枚ピンで留まる。中身は絵のお題が 3行（文字なし。飾りの数字だけ）
///   ○ 近接の絵 ×10（近接を当てる）／○ 能力の絵 ×3（能力を当てる）／○ ハートの絵（ダメージを受けずに 20秒。細い棒で見せる）
/// ・できた行に ✓。3行とも ✓ で、ライフが 1 回復（1ステージに 1回だけ）【要相談④：決まるまでは入れる】
/// ・ボス戦に入ると、新しいメモが 1枚（同じ形で、数が 1.5倍）。ごほうびは、クールタイムの回復と、10秒 与えるゲージ ×1.3
/// ・危ない物は出さない（ごほうびだけ）。メモは OnGUI（NotesChecklistView）
/// </summary>
public class NotesRule : StageRule
{
    /// <summary>お題の数【仮】</summary>
    const int Melee = 10, Skill = 3;
    const float Safe = 20f;
    const float BossScale = 1.5f;
    const float BuffSeconds = 10f;
    const float BuffGauge = 1.3f;

    // 今のメモ
    internal int MeleeNeed, SkillNeed, MeleeDone, SkillDone;
    internal float SafeNeed, SafeTimer;
    internal bool IsBossMemo, Rewarded;
    /// <summary>メモが変わった時刻（出てくる動きに使う）</summary>
    internal float ShownAt;
    bool _healed;
    NotesChecklistView _view;

    internal bool MeleeOk => MeleeDone >= MeleeNeed;
    internal bool SkillOk => SkillDone >= SkillNeed;
    internal bool SafeOk => SafeTimer >= SafeNeed;

    public override void Begin(StageRuleContext ctx)
    {
        NewMemo(false);
        Hitbox.AnyHit += OnAnyHit;
        Health.AnyDamaged += OnAnyDamaged;
        var go = new GameObject("NotesChecklist");
        _view = go.AddComponent<NotesChecklistView>();
        _view.Rule = this;
        ctx.TrackDecor(go);
    }

    public override void OnBossAppeared(BossController boss) => NewMemo(true);

    public override void Tick(float dt)
    {
        if (Rewarded) return;
        if (!SafeOk)
        {
            SafeTimer = Mathf.Min(SafeNeed, SafeTimer + dt);
            if (SafeOk) CheckSound(MeleeOk, SkillOk);
        }
        if (MeleeOk && SkillOk && SafeOk) Reward();
    }

    public override void End()
    {
        Hitbox.AnyHit -= OnAnyHit;
        Health.AnyDamaged -= OnAnyDamaged;
        if (_view != null) Object.Destroy(_view.gameObject);
        _view = null;
    }

    void NewMemo(bool boss)
    {
        float k = boss ? BossScale : 1f;
        MeleeNeed = Mathf.CeilToInt(Melee * k);
        SkillNeed = Mathf.CeilToInt(Skill * k);
        SafeNeed = Safe * k;
        MeleeDone = SkillDone = 0;
        SafeTimer = 0f;
        IsBossMemo = boss;
        Rewarded = false;
        ShownAt = Time.time;
        GameAudio.Event("gm_notes_memo_new");
    }

    void OnAnyHit(Hitbox hitbox, IDamageable target, DamageInfo info, bool first)
    {
        if (Rewarded || hitbox == null || hitbox.Team != Team.Player || target == null || target.Team == Team.Player) return;
        bool melee = hitbox.GetComponentInParent<PlayerMeleeAttack>() != null;
        if (melee) { if (!MeleeOk) { MeleeDone++; if (MeleeOk) CheckSound(SkillOk, SafeOk); } }
        else if (!SkillOk) { SkillDone++; if (SkillOk) CheckSound(MeleeOk, SafeOk); }
    }

    // 行が ✓ になった音。ほかに ✓ の行が多いほど高く。3行目はすぐ「そろった」の音が鳴るので鳴らさない
    static void CheckSound(bool otherA, bool otherB)
    {
        int n = (otherA ? 1 : 0) + (otherB ? 1 : 0);
        if (n < 2) GameAudio.Event("gm_notes_check", 1f + 0.12f * n);
    }

    void OnAnyDamaged(Health health, int amount)
    {
        if (Rewarded || health == null || health != Ctx.PlayerHealth || SafeOk) return;
        if (SafeTimer >= 3f) GameAudio.Event("gm_notes_safe_reset");
        SafeTimer = 0f; // ダメージを受けたら、ハートの行は始めから
    }

    void Reward()
    {
        Rewarded = true;
        GameAudio.Event("gm_notes_complete");
        Vector2 at = Ctx.Player != null ? (Vector2)Ctx.Player.position + new Vector2(0f, 0.6f) : Ctx.PlayArea.center;
        if (!IsBossMemo)
        {
            if (!_healed && KitPlayer.Heal(1)) FxKit.Ring(at, 1.6f, 0.35f, NotesArt.Green);
            _healed = true; // 1ステージに 1回だけ
            return;
        }
        KitPlayer.RefillCooldowns();
        StatMods.Player.GaugeGain.SetFor(Ctx.Key("checklist"), BuffGauge, BuffSeconds, NotesArt.IconMemo());
        FxKit.Ring(at, 1.6f, 0.35f, NotesArt.Yellow);
    }
}

/// <summary>チェックリストのメモ（画面の左上。OnGUI。文字なし、飾りの数字だけ）</summary>
public class NotesChecklistView : MonoBehaviour
{
    internal NotesRule Rule;

    const float X = 6f, Y = 50f, W = 54f;
    const float RowH = 11f;

    void OnGUI()
    {
        if (Rule == null) return;
        GameSession session = GameSession.Instance;
        if (session != null && (session.IsTransitioning || session.IsGameOver)) return;
        GUI.depth = 10;
        PixelGui.Begin(false);
        // 出てくるとき、上から少しすべり込む
        float t = Mathf.Clamp01((Time.time - Rule.ShownAt) / 0.2f);
        float y = Y - (1f - t) * 12f;
        float h = 6f + RowH * 3f + 3f;
        var r = new Rect(X, y, W, h);
        PixelGui.Fill(new Rect(r.x + 1f, r.y + 2f, r.width, r.height), new Color(0f, 0f, 0f, 0.3f)); // 影
        PixelGui.Fill(r, (Color)NotesArt.Edge);
        PixelGui.Fill(PixelGui.Inset(r, 1f), (Color)NotesArt.Paper);
        PixelGui.Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, 4f), (Color)NotesArt.Yellow);
        // ピン
        PixelGui.Fill(new Rect(r.center.x - 2f, r.y - 2f, 5f, 5f), (Color)NotesArt.PinRed);
        float ry = r.y + 7f;
        Row(r.x, ry, NotesArt.IconMelee(), Rule.MeleeOk, Rule.MeleeNeed - Rule.MeleeDone, -1f);
        Row(r.x, ry + RowH, NotesArt.IconSkill(), Rule.SkillOk, Rule.SkillNeed - Rule.SkillDone, -1f);
        Row(r.x, ry + RowH * 2f, NotesArt.IconHeart(), Rule.SafeOk, 0, Rule.SafeNeed > 0f ? Rule.SafeTimer / Rule.SafeNeed : 1f);
        PixelGui.End();
    }

    // 1行：○／✓・お題の絵・残りの数（数字）か、細い棒
    static void Row(float x, float y, Sprite icon, bool done, int left, float bar)
    {
        PixelGui.DrawSprite(new Rect(x + 3f, y + 1f, 7f, 7f), done ? NotesArt.Check() : NotesArt.Circle());
        PixelGui.DrawSprite(new Rect(x + 12f, y, 8f, 8f), icon);
        if (bar >= 0f)
        {
            var b = new Rect(x + 23f, y + 3f, 26f, 3f);
            PixelGui.Fill(b, (Color)NotesArt.Rule);
            PixelGui.Fill(new Rect(b.x, b.y, Mathf.Round(b.width * Mathf.Clamp01(bar)), b.height), done ? (Color)NotesArt.Green : (Color)NotesArt.PinRed);
        }
        else if (!done) PixelGui.TextEn(new Rect(x + 24f, y, 28f, 8f), Mathf.Max(0, left).ToString(), 8, (Color)NotesArt.Ink);
        PixelGui.Fill(new Rect(x + 3f, y + 9f, 48f, 1f), (Color)NotesArt.Rule);
    }
}
