using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Safari の場面③の出来事「コンパス」（2026-11-19。docs/stage-gimmick-variety-b.md 10章）。数値はすべて【仮】。
/// ・部屋のまん中（床から 4）にコンパスの軸。そこから針が 2本、反対向きに伸びて回る：KitMotion.Orbit を同じ角度に 3個ずつ並べる
///   （半径 2／4／6・直径 1 で 1本の針。ステージで 1本の針の玉が 2〜4個）。1周 7秒（ゆっくり）、12秒。半分（6秒）で逆回り。
///   針は床まで届く：床にいるときは、針が来たら足場へ上がるか、針の進む向きへ一緒に歩く
/// ・針の色：白と、オレンジ寄りのピンク（本物は赤だが、予告の赤と見分けるため #FF8FB1）。軸から外の玉まで細い棒（飾り）
/// ・予告：軸に方位の印（N・E・S・W）と、針の通る円（半径 6）をうすい赤の点線で 1.0秒。針はその場所でうすく点滅＋細い赤い線。
///   逆回りの前に、軸が 0.5秒点滅
/// ・3秒・8秒に、ブックマークの星（青）が、針のいない方の端からゆっくり流れてくる（KitMotion.Catch・速さ 3・床から 3.6）。
///   取ると SafariRule の盾を 1枚（もう付いていれば、能力のクールタイムを全部戻す）
/// ・軸の近く（半径 1.5 より内）は安全。針の間は 180度あく
/// </summary>
public class SafariCompassEvent : KitTimedEvent
{
    const float CenterY = 4f, Period = 7f, Duration = 12f, FlipAt = 6f, OrbSize = 1f, FirstWarn = 1f;
    const float CatchY = 3.6f, CatchSpeed = 3f, CatchSize = 0.9f;
    static readonly float[] CatchTimes = { 3f, 8f };
    static readonly Color Pink = new Color32(0xFF, 0x8F, 0xB1, 0xFF);
    const float StartDeg = 90f;

    readonly int _stage;
    readonly List<KitMotionGroup> _orbits = new List<KitMotionGroup>();
    Vector2 _center;
    float _outer;
    float _spawnTime;
    SpriteRenderer _dial;
    readonly SpriteRenderer[] _bars = new SpriteRenderer[2];

    public SafariCompassEvent(int stage) : base("Compass")
    {
        _stage = stage;
    }

    protected override void Build()
    {
        At(0f, Spawn);
        foreach (float t in CatchTimes) At(t, Bookmark);
        EndAt(FirstWarn + Duration + 0.3f);
    }

    static float[] RadiiFor(int n) =>
        n <= 2 ? new[] { 3f, 5f } : n == 3 ? new[] { 2f, 4f, 6f } : new[] { 2f, 3.4f, 4.8f, 6.2f };

    void Spawn()
    {
        _center = new Vector2(View.center.x, Floor + CenterY);
        _spawnTime = Time.time;
        float speed = 360f / Period; // 反時計回り（＋）
        float[] radii = RadiiFor(GroupBMotion.Scaled(3, _stage));
        _outer = radii[radii.Length - 1];
        Sprite orb = KitMotionArt.Disc(18);
        for (int needle = 0; needle < 2; needle++)
        {
            float start = StartDeg + 180f * needle;
            Color c = needle == 0 ? Pink : Color.white;
            foreach (float r in radii)
            {
                KitMotionGroup g = KitMotion.Orbit(Owner, _center, r, speed, 1, Duration, FirstWarn, orb, c, 1, OrbSize, start, FlipAt);
                if (g != null) _orbits.Add(g);
            }
        }
        // 軸（コンパスの文字盤）と、軸から外の玉までの細い棒（飾り）
        var root = new GameObject("Safari_Compass");
        root.transform.position = _center;
        Track(root);
        _dial = RuntimeSprites.Show(root.transform, SafariArt.Dial(), Vector2.zero, KitMotionItem.OrderBody + 1, 1.4f, "Dial");
        for (int i = 0; i < 2; i++)
        {
            _bars[i] = AttackRunner.AddVisual(root.transform, KitMotionArt.Dot(2), Color.white, Vector2.one, KitMotionItem.OrderBody - 1, "NeedleBar" + i);
            PlaceBar(i, StartDeg + 180f * i, KitMotion.Red, 0.5f);
        }
        // 予告：方位の印と、針の通る円（うすい赤の点線）
        string[] dirs = { "E", "N", "W", "S" };
        for (int i = 0; i < 4; i++)
        {
            Vector2 at = _center + new Vector2(Mathf.Cos(i * 90f * Mathf.Deg2Rad), Mathf.Sin(i * 90f * Mathf.Deg2Rad)) * 1.35f;
            FxKit.Text(dirs[i], at, 0.4f, Color.white, KitMotionItem.OrderMark, FirstWarn, SafariArt.Blue);
        }
        KitMotionMark.Create(root.transform, KitMotionArt.Ring(64, true), _center, Vector2.one * _outer * 2f, 0f,
            new Color(KitMotion.Red.r, KitMotion.Red.g, KitMotion.Red.b, 0.5f), KitMotionItem.OrderWarn, FirstWarn, "NeedlePath");
    }

    /// <summary>針の棒を置く（軸から外の玉まで。deg の向き）</summary>
    void PlaceBar(int i, float deg, Color color, float alpha)
    {
        SpriteRenderer sr = _bars[i];
        if (sr == null) return;
        Vector2 dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
        sr.transform.position = _center + dir * (_outer * 0.5f);
        sr.transform.rotation = Quaternion.Euler(0f, 0f, deg);
        Vector2 native = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(_outer / Mathf.Max(0.001f, native.x), 0.14f / Mathf.Max(0.001f, native.y), 1f);
        color.a = alpha;
        sr.color = color;
    }

    /// <summary>針の今の角度（1本目。道の秒数 t から。逆回りも入れる）</summary>
    static float AngleAt(float t)
    {
        float tt = t > FlipAt ? 2f * FlipAt - t : t;
        return StartDeg + 360f / Period * tt;
    }

    protected override void OnFrame(float clock)
    {
        if (_dial == null) return;
        float t = Time.time - _spawnTime - FirstWarn; // 動き出してからの秒数（予告の間はマイナス）
        bool moving = t >= 0f;
        float deg = AngleAt(Mathf.Clamp(t, 0f, Duration));
        for (int i = 0; i < 2; i++) PlaceBar(i, deg + 180f * i, moving ? (i == 0 ? Pink : Color.white) : KitMotion.Red, moving ? 0.55f : 0.5f);
        if (moving && t > Duration) { foreach (var b in _bars) if (b != null) b.enabled = false; }
        // 逆回りの前の 0.5秒、軸が点滅
        bool blink = t >= FlipAt - 0.5f && t < FlipAt;
        _dial.enabled = !blink || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
    }

    /// <summary>ブックマークの星（青）：針のいない方の端から、ゆっくり流れてくる</summary>
    void Bookmark()
    {
        float t = Time.time - _spawnTime - FirstWarn;
        float deg = AngleAt(Mathf.Clamp(t, 0f, Duration));
        // ピンクの針の先が右なら、左から（白い針は反対なので、横向きのときは、針の進む向きの後ろ側から）
        float cos = Mathf.Cos(deg * Mathf.Deg2Rad);
        KitSide side = cos > 0f ? KitSide.Left : KitSide.Right;
        KitPath path = KitPath.Sweep(side, Floor + CatchY, CatchSpeed, 0f, 4f, 1f, View);
        KitMotion.Catch(Owner, path, OnBookmark, BookmarkStar, SafariArt.Blue, CatchSize);
    }

    void OnBookmark()
    {
        SafariRule rule = GroupBMotion.Rule<SafariRule>();
        if (rule != null && rule.GiveShield()) return;
        KitPlayer.RefillCooldowns();
        GameAudio.Event("gm_safari_count", 1.3f);
    }

    /// <summary>ブックマークの星（白い 5つの先の星。11ドット。色は SpriteRenderer で付ける）</summary>
    static Sprite BookmarkStar => KitArt.Paint("safari.event.bookmark", 11, 11, c => c.Ascii(0, 0, new[]
    {
        ".....w.....",
        "....www....",
        "....www....",
        "wwwwwwwwwww",
        ".wwwwwwwww.",
        "..wwwwwww..",
        "..wwwwwww..",
        ".wwww.wwww.",
        ".www...www.",
        "ww.......ww",
        "...........",
    }, new Dictionary<char, Color32> { { 'w', new Color32(255, 255, 255, 255) } }));
}
