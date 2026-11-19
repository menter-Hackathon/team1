using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】QuickTime Player のステージの出来事「巻き戻し」（場面③。2026-11-19。docs/stage-gimmick-variety-c.md 4章）。
/// ・はじめ：上のまん中に ">> 2x" の札（1.0秒）
/// ・EdgeSweep（主）・行き：左の端から右へ、フィルムのコマ（灰色の四角に白い枠。黄色はプレイヤーの色なので使わない）。
///   3／4／5本を 1.1秒おき（5本は 1.0秒おき）。高さは決め打ちの順（0.8・3.8・2.2・5.6・0.8＝床・低い足場・その間・まん中の足場）。速さ 8・まっすぐ
///   通ったあとに、うすい残像の線（白 20％）が残る（戻ってくる道の印。EdgeSweep に無いので、この出来事が線を置く）
/// ・止まる：札が "||"（0.3秒）→ "<< 2x"（1.0秒）。残像の線が、最後に通った物から順に赤く光る
/// ・EdgeSweep・戻り：右の端から左へ、同じ高さを逆の順番で。速さ 10・1.0秒おき。それぞれの予告の間、その線が赤く点滅する
/// ・終わり：札が "> 1x"。線が消える
/// ・戻りの始まりは、行きの最後のコマが画面を出た時（設計の 7.0秒より遅くなることがある。強さ 3 は約 9秒）。
///   同じ高さのコマが行きと戻りですれちがわないため（行きの最後と戻りの最初は同じ高さ）
/// </summary>
public class QuickTimeRewindEvent : KitTimedEvent
{
    const float GoSpeed = 8f, BackSpeed = 10f, GoGap = 1.1f, BackGap = 1f, Warn = 0.8f, FrameSize = 0.9f;
    static readonly float[] Lanes = { 0.8f, 3.8f, 2.2f, 5.6f, 0.8f };
    static readonly Color FrameGray = new Color32(0x8E, 0x8E, 0x93, 0xFF);

    class Lane
    {
        public float Y;
        public QuickTimeTrail Trail;
        public KitMotionGroup Go;
        public KitMotionGroup Back;
    }

    readonly List<Lane> _lanes = new List<Lane>();
    GameObject _root;
    GameObject _tag;
    int _count;
    float _backAt; // 戻りの最初のコマが飛び出す時刻（表の時計）

    public QuickTimeRewindEvent() : base("Rewind") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(QuickTimeKit.Label, stage => new QuickTimeRewindEvent());

    protected override void Build()
    {
        _root = new GameObject("QuickTimeRewind");
        Track(_root);
        _count = Level >= 3 ? 5 : Level == 2 ? 4 : 3;
        Rect a = KitMotion.Area;
        float goCross = (a.width + FrameSize * 2f) / GoSpeed;
        float backCross = (a.width + FrameSize * 2f) / BackSpeed;
        float goGap = _count >= 5 ? 1f : GoGap; // 強さ 3 は少しつめる（終わりが 20秒の打ち切りに近づかないように）
        float lastGoExit = 1f + goGap * (_count - 1) + Warn + goCross;
        _backAt = Mathf.Max(7.8f, lastGoExit);

        At(0f, () => ShowTag(">> 2x", 1f));
        Every(1f, goGap, _count, Go);
        At(_backAt - 1.8f, () => { ShowTag("||", 0.3f); FlashTrails(); });
        At(_backAt - 1.5f, () => ShowTag("<< 2x", 1f));
        for (int k = 0; k < _count; k++)
        {
            int lane = _count - 1 - k; // 逆の順番
            At(_backAt - Warn + BackGap * k, () => Back(lane));
        }
        float end = _backAt + BackGap * (_count - 1) + backCross;
        At(end, () => { ShowTag("> 1x", 0.8f); ClearTrails(); });
        EndAt(end + 0.6f);
    }

    void Go(int i)
    {
        float y = Lanes[i % Lanes.Length];
        KitMotionGroup g = KitMotion.EdgeSweep(Owner, KitSide.Left, y, GoSpeed, 1, 0f, 0f, Warn, FilmFrame, FrameGray, 1, FrameSize);
        var lane = new Lane { Y = y, Go = g };
        Rect a = KitMotion.Area;
        var go = new GameObject("Trail");
        go.transform.SetParent(_root.transform, false);
        lane.Trail = go.AddComponent<QuickTimeTrail>();
        lane.Trail.Setup(KitMotion.FloorY() + y, a.xMin, a.xMax);
        _lanes.Add(lane);
    }

    void Back(int index)
    {
        if (index < 0 || index >= _lanes.Count) return;
        Lane lane = _lanes[index];
        if (lane.Trail != null) lane.Trail.Blink(Warn);
        lane.Back = KitMotion.EdgeSweep(Owner, KitSide.Right, lane.Y, BackSpeed, 1, 0f, 0f, Warn, FilmFrame, FrameGray, 1, FrameSize);
    }

    protected override void OnFrame(float clock)
    {
        foreach (var lane in _lanes)
        {
            if (lane.Trail == null) continue;
            // 行き：コマのうしろに線をのばす。戻り：コマが通った所から線を消す
            Transform goBody = FirstBody(lane.Go);
            if (goBody != null) lane.Trail.GrowTo(goBody.position.x);
            Transform backBody = FirstBody(lane.Back);
            if (backBody != null) lane.Trail.EatFrom(backBody.position.x);
        }
    }

    static Transform FirstBody(KitMotionGroup g)
    {
        if (g == null || g.IsDone) return null;
        foreach (var item in g.Items) if (item is KitMotionMover m && m.Body != null && m.MoveTime > 0f) return m.transform;
        return null;
    }

    /// <summary>止まった時：残像の線が、最後に通った物から順に赤く光る（0.15秒ずつ）</summary>
    void FlashTrails()
    {
        for (int i = _lanes.Count - 1, n = 0; i >= 0; i--, n++)
            if (_lanes[i].Trail != null) _lanes[i].Trail.Flash(n * 0.15f, 0.3f);
    }

    void ClearTrails()
    {
        foreach (var lane in _lanes) if (lane.Trail != null) Object.Destroy(lane.Trail.gameObject);
    }

    /// <summary>上のまん中の札（seconds 秒）。前の札は消す</summary>
    void ShowTag(string text, float seconds)
    {
        if (_tag != null) Object.Destroy(_tag);
        Rect a = KitMotion.Area;
        _tag = new GameObject("QuickTimeRewindTag");
        _tag.transform.SetParent(_root.transform, false);
        _tag.transform.position = new Vector2(a.center.x, a.yMax - 2.4f);
        QuickTimeArt.Tag(_tag.transform, text, Vector2.zero, KitMotionItem.OrderMark);
        Object.Destroy(_tag, seconds);
        GameAudio.Event("gm_quicktime_tag");
    }

    static readonly Dictionary<char, Color32> FramePal = new Dictionary<char, Color32>
    {
        { 'W', new Color32(255, 255, 255, 255) }, // 枠（灰色で塗る）
        { 'K', new Color32(40, 40, 44, 255) },    // 穴
        { 'S', new Color32(190, 190, 196, 255) }, // 中の絵
    };

    /// <summary>フィルムのコマ（14×14。白い枠と、上下の小さい穴。Diagonal・EdgeSweep の色で灰色に塗る）</summary>
    static Sprite FilmFrame => RuntimeSprites.FromAscii("QuickTimeEvent_Frame", new[]
    {
        "WWWWWWWWWWWWWW",
        "WKWWKWWKWWKWKW",
        "WWWWWWWWWWWWWW",
        "WWSSSSSSSSSSWW",
        "WWSWWWWWWWWSWW",
        "WWSWSSSSSSWSWW",
        "WWSWSWWWWSWSWW",
        "WWSWSWWWWSWSWW",
        "WWSWSSSSSSWSWW",
        "WWSWWWWWWWWSWW",
        "WWSSSSSSSSSSWW",
        "WWWWWWWWWWWWWW",
        "WKWWKWWKWWKWKW",
        "WWWWWWWWWWWWWW",
    }, FramePal);
}

/// <summary>
/// 【仮】QuickTime の出来事の残像の線（白 20％。高さ y の、左の端から GrowTo の x まで）。2026-11-19。
/// Blink：その秒数だけ赤く点滅（戻りの予告）。EatFrom：x より右を消す（戻りのコマが通った所）。当たらない飾り
/// </summary>
public class QuickTimeTrail : MonoBehaviour
{
    SpriteRenderer _line;
    Vector2 _native = Vector2.one;
    float _y, _xMin, _xMax, _left, _right;
    float _blinkFrom = -1f, _blinkUntil = -1f;

    public void Setup(float y, float xMin, float xMax)
    {
        _y = y;
        _xMin = xMin;
        _xMax = xMax;
        _left = xMin;
        _right = xMin;
        _line = AttackRunner.AddVisual(transform, PlaceholderSprites.Square, new Color(1f, 1f, 1f, 0.2f), Vector2.one, KitMotionItem.OrderZone, "Line");
        _native = _line.sprite != null ? (Vector2)_line.sprite.bounds.size : Vector2.one;
        Layout();
    }

    /// <summary>線を x までのばす（行きのコマのうしろ）</summary>
    public void GrowTo(float x) { _right = Mathf.Clamp(Mathf.Max(_right, x), _xMin, _xMax); Layout(); }

    /// <summary>x より右の線を消す（戻りのコマが通った所）</summary>
    public void EatFrom(float x) { _right = Mathf.Clamp(Mathf.Min(_right, x), _xMin, _xMax); Layout(); }

    /// <summary>seconds 秒、赤く点滅</summary>
    public void Blink(float seconds) { _blinkFrom = Time.time; _blinkUntil = Time.time + seconds; }

    /// <summary>delay 秒後から seconds 秒、赤く光る</summary>
    public void Flash(float delay, float seconds) { _blinkFrom = Time.time + delay; _blinkUntil = _blinkFrom + seconds; }

    void Layout()
    {
        if (_line == null) return;
        float w = Mathf.Max(0f, _right - _left);
        _line.enabled = w > 0.05f;
        Transform t = _line.transform;
        t.position = new Vector3((_left + _right) * 0.5f, _y, 0f);
        t.localScale = new Vector3(Mathf.Max(0.001f, w) / Mathf.Max(0.001f, _native.x), 2f * KitDress.Px / Mathf.Max(0.001f, _native.y), 1f);
    }

    void Update()
    {
        if (_line == null) return;
        bool red = Time.time >= _blinkFrom && Time.time < _blinkUntil && Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
        _line.color = red ? new Color(KitMotion.Red.r, KitMotion.Red.g, KitMotion.Red.b, 0.85f) : new Color(1f, 1f, 1f, 0.2f);
    }
}
