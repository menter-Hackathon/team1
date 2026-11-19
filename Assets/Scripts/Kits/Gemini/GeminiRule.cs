using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Gemini のステージのギミック「回答案を選ぶ」（2026-11-19。docs/app-kits-1-design.md 7章）。担当3 の物。
/// ・ボス戦は 20秒おき（最初は 10秒）、雑魚の時間は場面が変わるたびに、床の上に星の札が 3つ出る（KitProp。左・中・右）。5秒で消える
/// ・どれか 1つを叩くと、その案が選ばれる（ほかの 2つは消える）。中身は星の色で分かる（文字は出さない）
///   - 青い星＝能力のクールタイムを全部回復
///   - 紫の星＝10秒、与えるゲージ ×1.3（印が出る）
///   - ピンクの星＝目の前に星の柱（ゲージ +8）
/// ・叩かなくても罰は無い。ごほうびだけのギミック。置いた物は片づけの表（ボスの登場・50％・100％）で消える
/// </summary>
public class GeminiRule : StageRule
{
    const float BossInterval = 20f;
    const float FirstInBoss = 10f;
    const float Life = 5f;
    const float TagSize = 1.1f;
    const float GaugeBoost = 1.3f;
    const float GaugeSeconds = 10f;
    const int FxOrder = 34;

    class Tag
    {
        public KitProp Prop;
        public GeminiArt.StarKind Kind;
        public Vector2 Base;
        public float Born;
    }

    readonly List<Tag> _tags = new List<Tag>();
    float _timer;
    bool _zakoPending;
    bool _ended;
    string _gaugeKey;
    AttackData _pinkData;

    public override void Begin(StageRuleContext ctx)
    {
        _gaugeKey = ctx.Key("draft.gauge");
        _pinkData = ZoneStrike.MakeData("Attack_Stage_Gemini_PinkStar", 3, 8, new Vector2(4f, 3f), 0.2f, Color.white);
        _zakoPending = true; // 最初の場面（3秒たって、置いてよくなったら出す）
    }

    public override void OnPhase(PacePhase phase)
    {
        if (phase == PacePhase.Done) { ClearTags(); _zakoPending = false; return; }
        _zakoPending = true;
    }

    public override void OnBossAppeared(BossController boss)
    {
        ClearTags();
        _timer = BossInterval - FirstInBoss;
    }

    public override void OnBossPhase(int phase) => ClearTags();

    public override void Tick(float dt)
    {
        UpdateTags();
        if (_tags.Count > 0) return;
        if (Ctx.InBossFight)
        {
            _timer += dt;
            if (_timer >= BossInterval && Ctx.CanPlace)
            {
                _timer = 0f;
                SpawnTags();
            }
        }
        else if (_zakoPending && Ctx.CanPlace)
        {
            _zakoPending = false;
            SpawnTags();
        }
    }

    // ───────── 札 ─────────

    void SpawnTags()
    {
        Vector2[] spots = Spots();
        if (spots == null) return;
        var kinds = new List<GeminiArt.StarKind> { GeminiArt.StarKind.Blue, GeminiArt.StarKind.Purple, GeminiArt.StarKind.Pink };
        // 並びは毎回まぜる（色で選ぶ）
        for (int i = kinds.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
        }
        for (int i = 0; i < spots.Length && i < kinds.Count; i++)
        {
            GeminiArt.StarKind kind = kinds[i];
            KitProp prop = KitProp.Spawn(spots[i], GeminiArt.Tag(kind), new Vector2(TagSize, TagSize), null, 1, 0f, 16);
            if (prop == null) continue;
            var tag = new Tag { Prop = prop, Kind = kind, Base = spots[i], Born = Time.time };
            prop.Broken += _ => Pick(tag);
            Ctx.TrackPlaced(prop.gameObject);
            _tags.Add(tag);
            FxAnim.Sprite(GeminiArt.Star4(9), spots[i], FxOrder, 0.35f, Color.white, 1.2f, "GeminiTagIn").Pop().Spin(360f).FadeFrom(0.4f);
        }
        if (_tags.Count > 0) GameAudio.Event("gm_gemini_drafts");
    }

    /// <summary>札を置く 3か所（左・中・右）。ボス戦は部屋の床、雑魚の時間はプレイヤーのまわりの床</summary>
    Vector2[] Spots()
    {
        AttackRunner probe = Ctx.Hazard;
        PlayerSkillExecutor ex = KitPlayer.Executor;
        if (ex != null && ex.Runner != null) probe = ex.Runner;
        if (Ctx.InBossFight)
        {
            Rect a = Ctx.Arena;
            float y = a.yMin + TagSize * 0.5f + 0.35f;
            return new[] { new Vector2(a.center.x - 6f, y), new Vector2(a.center.x, y), new Vector2(a.center.x + 6f, y) };
        }
        if (Ctx.Player == null) return null;
        Rect view = Ctx.PlayArea;
        Vector2 p = Ctx.Player.position;
        float[] offs = { -4.5f, 0f, 4.5f };
        var list = new Vector2[offs.Length];
        for (int i = 0; i < offs.Length; i++)
        {
            float x = Mathf.Clamp(p.x + offs[i] + 1.5f, view.xMin + 1f, view.xMax - 1f);
            float ground = p.y;
            if (probe != null && probe.TryFindGround(x, p.y + 2.5f, out float g) && g > p.y - 4f) ground = g;
            list[i] = new Vector2(x, ground + TagSize * 0.5f + 0.35f);
        }
        return list;
    }

    void UpdateTags()
    {
        bool expired = false;
        for (int i = _tags.Count - 1; i >= 0; i--)
        {
            Tag t = _tags[i];
            if (t.Prop == null || t.Prop.IsBroken) { _tags.RemoveAt(i); continue; }
            float age = Time.time - t.Born;
            if (age >= Life)
            {
                expired = true;
                Object.Destroy(t.Prop.gameObject);
                _tags.RemoveAt(i);
                continue;
            }
            // ふわふわ浮く。消える 1秒前から点滅
            t.Prop.transform.position = t.Base + new Vector2(0f, Mathf.Sin(age * 4f + i) * 0.08f);
            if (t.Prop.Visual != null) t.Prop.Visual.enabled = Life - age > 1f || Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
        }
        // 3枚いっしょに消えるので、音は1回だけ
        if (expired) GameAudio.Event("gm_gemini_expire");
    }

    void Pick(Tag chosen)
    {
        if (_ended) return;
        Vector2 at = chosen.Prop != null ? (Vector2)chosen.Prop.transform.position : chosen.Base;
        FxAnim.Sprite(GeminiArt.Star4(13), at, FxOrder, 0.45f, Color.white, 1.6f, "GeminiChosen").Pop().Spin(420f).Grow(1.5f).FadeFrom(0.5f);
        FxKit.Ring(at, 2.2f, 0.3f, GeminiArt.ColorOf(chosen.Kind));
        // ほかの 2つは消える
        foreach (var t in _tags)
        {
            if (t == chosen || t.Prop == null) continue;
            FxAnim.Sprite(GeminiArt.Tag(t.Kind), t.Prop.transform.position, FxOrder - 1, 0.25f, new Color(1f, 1f, 1f, 0.8f), TagSize * 18f / 17f, "GeminiDrop").Grow(0.2f).FadeFrom(0.1f);
            Object.Destroy(t.Prop.gameObject);
        }
        _tags.Clear();
        Apply(chosen.Kind);
    }

    void Apply(GeminiArt.StarKind kind)
    {
        Transform player = Ctx.Player;
        switch (kind)
        {
            case GeminiArt.StarKind.Blue:
                KitPlayer.RefillCooldowns();
                GameAudio.Event("gm_gemini_pick_blue");
                if (player != null) FxKit.Ring((Vector2)player.position + Vector2.up * 0.5f, 2.6f, 0.35f, GeminiArt.Blue);
                break;
            case GeminiArt.StarKind.Purple:
                StatMods.Player.GaugeGain.SetFor(_gaugeKey, GaugeBoost, GaugeSeconds, GeminiArt.Icon);
                GameAudio.Event("gm_gemini_pick_purple");
                if (player != null) FxKit.Ring((Vector2)player.position + Vector2.up * 0.5f, 2.6f, 0.35f, GeminiArt.Purple);
                break;
            default:
                GameAudio.Event("gm_gemini_pick_pink");
                PinkPillar();
                break;
        }
    }

    /// <summary>ピンクの星：プレイヤーの目の前に星の柱（プレイヤーの攻撃。ゲージ +8）</summary>
    void PinkPillar()
    {
        PlayerSkillExecutor ex = KitPlayer.Executor;
        AttackRunner runner = ex != null ? ex.Runner : null;
        if (runner == null || _pinkData == null) return;
        Vector2 feet = runner.OriginPoint;
        float x = feet.x + runner.Facing * 1.6f;
        runner.TryFindGround(x, feet.y + 0.5f, out float ground);
        ZoneStrike.Strike(runner, _pinkData, new Rect(x - 0.75f, ground, 1.5f, 5f), 0f, 0f, 0.3f, GeminiArt.Beam, Color.white);
    }

    void ClearTags()
    {
        foreach (var t in _tags) if (t.Prop != null) Object.Destroy(t.Prop.gameObject);
        _tags.Clear();
    }

    public override void End()
    {
        _ended = true;
        ClearTags();
        StatMods.Player.GaugeGain.Clear(_gaugeKey);
    }
}
