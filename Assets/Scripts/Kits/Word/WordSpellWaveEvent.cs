using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Word の場面③の出来事「スペルチェックの波」（2026-11-19。docs/stage-gimmick-variety-b.md 8章）。数値はすべて【仮】。
/// ・まちがった字の下の赤い波線（高さ 0.9）が、床の端から走ってくる（KitMotion.FloorWave・速さ 9）
///   1本目：左から → 2本目（2.4秒後）：右から → 3本目：左から、すぐあと（0.7秒）にもう 1本（2回続けてジャンプ）→
///   4本目：左右から同時（まん中で出会って消える）。ステージ 1 は 3本目の 2回目なし、ステージ 3 は最後に右からもう 1本
/// ・予告：床の赤い帯と端の矢印（FloorWave）＋出る端の床に赤い「ABC」（0.9秒）。左右同時のときは、まん中の床に「×」
/// ・ジャンプで越える、または UI の足場（文字の行）に乗る。波を近接・能力で叩くと「直す」（緑の輪で消える。
///   WordRule の直しと同じ数え方：3つ直すとクールタイムが戻る）
/// ・波が通ったあとの床に、青い二重線（文法の線）が 0.3秒残る（飾り）
/// ・出来事の間、WordRule の波線は出ない（③ の間は ctx.CanSpawnHazard が false のため。直しは要らない）
/// </summary>
public class WordSpellWaveEvent : KitTimedEvent
{
    const float Speed = 9f, Height = 0.9f, Width = 1.6f, FirstWarn = 1f, Warn = 0.9f;
    const float TrailEvery = 0.06f, TrailLife = 0.3f;

    struct Wave
    {
        public float At;
        public KitSide Side;
        public Wave(float at, KitSide side) { At = at; Side = side; }
    }

    readonly int _stage;
    readonly List<KitMotionGroup> _groups = new List<KitMotionGroup>();
    readonly HashSet<KitMotionItem> _hooked = new HashSet<KitMotionItem>();
    int _localFixes;
    float _trailAt;

    public WordSpellWaveEvent(int stage) : base("Spelling")
    {
        _stage = stage;
    }

    protected override void Build()
    {
        var plan = new List<Wave> { new Wave(0f, KitSide.Left), new Wave(2.4f, KitSide.Right), new Wave(4.8f, KitSide.Left) };
        if (_stage >= 2) plan.Add(new Wave(5.5f, KitSide.Left)); // すぐあとにもう 1本（2回続けてジャンプ）
        plan.Add(new Wave(8f, KitSide.Both));                       // 左右から同時（まん中で出会う）
        if (_stage >= 3) plan.Add(new Wave(10.4f, KitSide.Right));
        float last = 0f;
        for (int i = 0; i < plan.Count; i++)
        {
            Wave w = plan[i];
            float warn = i == 0 ? FirstWarn : Warn;
            At(w.At, () => Run(w.Side, warn));
            last = Mathf.Max(last, w.At);
        }
        float cross = (View.width + Width * 2f) / Speed;
        EndAt(last + Warn + cross + 0.3f);
    }

    void Run(KitSide side, float warn)
    {
        bool meet = side == KitSide.Both;
        Sprite wave = OfficeArt.SpellWave(Mathf.RoundToInt(Width * 18f), Mathf.RoundToInt(Height * 18f), 0, false);
        KitMotionGroup g = KitMotion.FloorWave(Owner, side, Speed, Height, warn, wave, null, 1, Width, meet);
        if (g != null) _groups.Add(g);
        // 出る端の床に、赤い「ABC」（まちがった字）。左右同時なら、まん中に「×」
        Rect v = View;
        float y = Floor + Height + 0.9f;
        if (side != KitSide.Right) FxKit.Text("ABC", new Vector2(v.xMin + 1.4f, y), 0.42f, WordArt.Red, 40, warn, Color.white);
        if (side != KitSide.Left) FxKit.Text("ABC", new Vector2(v.xMax - 1.4f, y), 0.42f, WordArt.Red, 40, warn, Color.white);
        if (meet) FxKit.Text("×", new Vector2(v.center.x, Floor + 0.5f), 0.6f, WordArt.Red, 40, warn + 1f, Color.white, false);
        GameAudio.Event("gm_word_wave");
    }

    protected override void OnFrame(float clock)
    {
        _groups.RemoveAll(g => g == null || g.IsDone);
        bool trail = Time.time >= _trailAt;
        if (trail) _trailAt = Time.time + TrailEvery;
        foreach (KitMotionGroup g in _groups)
        {
            foreach (KitMotionItem it in g.Items)
            {
                if (it == null || it.IsDone || it.Body == null) continue;
                if (_hooked.Add(it)) Hook(it);
                if (trail) FxAnim.Sprite(GrammarLine, new Vector2(it.Body.position.x, Floor + 0.06f), 3, TrailLife, Color.white, 1f, "Word_Grammar").FadeFrom(0f);
            }
        }
    }

    /// <summary>走る波に、叩ける置物（見えない）を重ねる。近接・能力が当たると「直す」</summary>
    void Hook(KitMotionItem wave)
    {
        KitProp prop = KitProp.Spawn(wave.Body.position, null, new Vector2(Width + 0.4f, Height + 0.6f), p => Fix(wave));
        if (prop == null) return;
        prop.transform.SetParent(wave.Body, true);
        if (prop.Visual != null) prop.Visual.enabled = false;
    }

    void Fix(KitMotionItem wave)
    {
        if (wave == null || wave.IsDone) return;
        Vector2 at = wave.Body != null ? (Vector2)wave.Body.position : (Vector2)wave.transform.position;
        FxKit.Ring(at, 2.2f, 0.2f, OfficeArt.Green);
        FxAnim.Sprite(OfficeArt.SpellWave(Mathf.RoundToInt(Width * 18f), Mathf.RoundToInt(Height * 18f), 0, false, true), at, 30, 0.4f, Color.white, 1f, "Word_Fixed").FadeFrom(0.3f);
        wave.Cancel();
        WordRule rule = GroupBMotion.Rule<WordRule>();
        if (rule != null) { rule.CountFix(); return; }
        // 決まりが無いとき：3つ直すと、クールタイムを全部戻す
        _localFixes++;
        GameAudio.Event("gm_word_fix", 1f + 0.12f * (_localFixes - 1));
        if (_localFixes < WordRule.FixesForRefill) return;
        _localFixes = 0;
        KitPlayer.RefillCooldowns();
        GameAudio.Event("gm_word_refill");
    }

    /// <summary>青い二重線（文法の線。18×4 ドット）</summary>
    static Sprite GrammarLine => KitArt.Paint("word.event.grammar", 18, 4, c =>
    {
        Color32 blue = WordArt.BlueLight;
        c.HLine(0, 0, 18, blue);
        c.HLine(0, 3, 18, blue);
    });
}
