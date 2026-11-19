using UnityEngine;

/// <summary>
/// 【仮】Arc の場面③の出来事「スペースのスワイプ」（2026-11-19。docs/stage-gimmick-variety-b.md 12章）。数値はすべて【仮】。
/// ・2本の指で横にスワイプして、スペースを切りかえる：床の全部が横に流れる（KitMotion.Conveyor・速さ 4・3.5秒）。
///   流れの先の壁ぎわ（幅 2）に、サイドバーの色の柱が床からせり上がる（KitMotion.Erupt・高さ 2.5。流れ始めて 1.2秒後から、流れが終わるまで）
/// ・左 → 右 → 左 の 3回（ステージで 2〜4回）。流れの予告 1.0秒（床の上を矢印が流れる）が、前の回の 1.0秒の休みに重なる
/// ・回ごとに、背景の色あいが少し変わる（ピンク → 青 → 緑。飾り）。床の少し上に、スペースの点「● ○ ○」が 1つ進む（飾り）
/// ・UI の足場（カード・サイドバーのタブ）に上がれば流れない。床にいるなら、流れに逆らって歩く（歩きの方が速い）。雑魚も一緒に流れる
/// ・出来事の間、ArcRule のウイルスの Space は新しく始まらない（ArcRule 側で場面③を見る）
/// </summary>
public class ArcSpaceSwipeEvent : KitTimedEvent
{
    const float FlowSpeed = 4f, FlowSeconds = 3.5f, Warn = 1f, Rest = 1f;
    const float PillarWidth = 2f, PillarHeight = 2.5f, PillarDelay = 1.2f, PillarRise = 0.2f;
    const float TintAlpha = 0.13f;
    static readonly Color[] Tints = { ArcArt.Pink, new Color32(0x6E, 0xA8, 0xFF, 0xFF), new Color32(0x6F, 0xD8, 0x9A, 0xFF) };

    readonly int _stage;
    int _rounds;
    SpriteRenderer _tint;
    Color _tintFrom, _tintTo;
    float _tintAt = -10f;
    SpriteRenderer[] _dots;

    public ArcSpaceSwipeEvent(int stage) : base("Spaces")
    {
        _stage = stage;
    }

    protected override void Build()
    {
        _rounds = Mathf.Clamp(GroupBMotion.Scaled(3, _stage), 1, 4);
        float step = FlowSeconds + Rest;
        for (int r = 0; r < _rounds; r++)
        {
            int n = r;
            At(r * step, () => Swipe(n));
        }
        EndAt((_rounds - 1) * step + Warn + FlowSeconds + 0.3f);
        MakeDecor();
    }

    /// <summary>1回分：床が流れて、流れの先の壁ぎわに柱</summary>
    void Swipe(int round)
    {
        bool toLeft = round % 2 == 0;
        Rect v = View;
        float floor = Floor;
        float dir = toLeft ? -1f : 1f;
        KitMotion.Conveyor(Owner, v.center.x, v.width, dir * FlowSpeed, FlowSeconds, Warn, ArcArt.Purple, floor);
        float x = toLeft ? v.xMin + PillarWidth * 0.5f : v.xMax - PillarWidth * 0.5f;
        float hold = Mathf.Max(0.3f, FlowSeconds - PillarDelay - PillarRise);
        KitMotion.Erupt(Owner, x, PillarWidth, PillarHeight, Warn + PillarDelay, PillarRise, hold, PillarSprite, Color.white, 1, floor);
        SetSpace(round);
        GameAudio.Event(round % 2 == 0 ? "gm_arc_space_virus" : "gm_arc_space_home");
    }

    // ───────── 飾り：背景の色あい・スペースの点 ─────────

    void MakeDecor()
    {
        Rect v = View;
        var root = new GameObject("Arc_SpaceDecor");
        Track(root);
        _tint = AttackRunner.AddVisual(root.transform, PlaceholderSprites.Square, new Color(1f, 1f, 1f, 0f), v.size, -92, "Tint");
        _tint.transform.position = v.center;
        _tintFrom = _tintTo = new Color(Tints[0].r, Tints[0].g, Tints[0].b, 0f);
        int n = Mathf.Max(3, _rounds);
        _dots = new SpriteRenderer[n];
        for (int i = 0; i < n; i++)
        {
            _dots[i] = AttackRunner.AddVisual(root.transform, KitMotionArt.Disc(7), new Color(1f, 1f, 1f, 0.45f), Vector2.one * 0.32f, KitMotionItem.OrderZone + 2, "SpaceDot" + i);
            _dots[i].transform.position = new Vector2(v.center.x + (i - (n - 1) * 0.5f) * 0.55f, Floor + 0.85f);
        }
    }

    void SetSpace(int round)
    {
        Color c = Tints[round % Tints.Length];
        _tintFrom = _tint != null ? _tint.color : c;
        _tintTo = new Color(c.r, c.g, c.b, TintAlpha);
        _tintAt = Time.time;
        if (_dots == null) return;
        for (int i = 0; i < _dots.Length; i++)
            if (_dots[i] != null) _dots[i].color = i == round ? Color.white : new Color(1f, 1f, 1f, 0.35f);
    }

    protected override void OnFrame(float clock)
    {
        if (_tint == null) return;
        float k = Mathf.Clamp01((Time.time - _tintAt) / 0.4f); // 色が横にすべる時間くらい
        _tint.color = Color.Lerp(_tintFrom, _tintTo, k);
    }

    /// <summary>サイドバーの色の柱（角の丸い色の帯。36×45 ドット＝2×2.5 マス。赤にしない）</summary>
    static Sprite PillarSprite => ArcArt.Gradient(36, 45, 6, ArcArt.Pink, ArcArt.Purple, ArcArt.Cyan, true);
}
