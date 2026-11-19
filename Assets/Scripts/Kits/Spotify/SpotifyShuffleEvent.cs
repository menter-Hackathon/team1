using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Spotify のステージの出来事「シャッフル」（場面③。2026-11-19。docs/stage-gimmick-variety-c.md 9章）。
/// ・Bounce（主）：跳ねるレコード（灰色の円盤に緑のまん中）。2／3／3個を 4.0秒おきに 1個ずつ。左右の壁の高さ 5 から交互に、
///   ななめ下 35°・速さ 7・壁と床で 5回跳ねて消える。足場は通りぬける（足場では跳ねない）。同じ時に 2個まで（3個目は 1個目が消えてから）
///   予告 1.0秒：出る所で赤く点滅＋最初の 2回の跳ね返りまでの道の赤い折れ線。跳ねるたびに、次の道の線が 0.3秒だけ出る
///   （KitMotion.Bounce は最初の道の線だけなので、2本目と跳ねるたびの線は、この出来事が同じ道すじ KitPath.Bounce を作って引く）
/// ・Catch：3.0秒は左、9.0秒は右の端から Premium の緑の札が波打って横切る（SpotifyRule.SendTicket。決まりの札と同じ物・同じ効き）
/// </summary>
public class SpotifyShuffleEvent : KitTimedEvent
{
    const float RecordEvery = 4f, RecordFirst = 1f, RecordHeight = 5f, RecordAngle = 35f, RecordSpeed = 7f, RecordWarn = 1f, RecordSize = 0.9f;
    const int Bounces = 5, MaxRecords = 2;
    const float NextLineTime = 0.3f;

    class Record
    {
        public KitMotionMover Mover;
        public List<Vector2> Points;
        public List<float> Times; // 動き出してから、その点に着く秒数
        public int NextCorner;
    }

    readonly List<Record> _records = new List<Record>();
    GameObject _root;
    int _spawned;
    int _pending; // 2個いて出せなかった数

    public SpotifyShuffleEvent() : base("Shuffle") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(SpotifyKit.Label, stage => new SpotifyShuffleEvent());

    protected override void Build()
    {
        _root = new GameObject("SpotifyShuffle");
        Track(_root);
        int n = Level >= 2 ? 3 : 2;
        Every(RecordFirst, RecordEvery, n, i => _pending++);
        At(3f, () => Ticket(KitSide.Left));
        At(9f, () => Ticket(KitSide.Right));
        EndAt(14f);
    }

    void Ticket(KitSide side)
    {
        var rule = StageRules.Current as SpotifyRule;
        if (rule != null)
        {
            rule.SendTicket(side, SpotifyRule.TicketSpeed, KitOwner.Of(go => rule.Ctx.TrackPlaced(go)));
            GameAudio.Event("gm_spotify_ticket_drop");
            return;
        }
        // 決まりが無いとき：同じ流れ方で、取るとクールタイムを少し戻す
        Rect a = KitMotion.Area;
        KitPath path = KitPath.Sweep(side, KitMotion.FloorY() + SpotifyRule.TicketHeight, SpotifyRule.TicketSpeed, SpotifyRule.TicketWave, 4f, 1f, a);
        KitMotion.Catch(Owner, path, () => KitPlayer.AdvanceCooldowns(2f), SpotifyArt.Premium, null, SpotifyRule.TicketSize);
    }

    void SpawnRecord()
    {
        Rect a = KitMotion.Area;
        bool fromLeft = _spawned % 2 == 0;
        _spawned++;
        float rad = RecordAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2((fromLeft ? 1f : -1f) * Mathf.Cos(rad), -Mathf.Sin(rad));
        Vector2 from = new Vector2(fromLeft ? a.xMin + 0.6f : a.xMax - 0.6f, KitMotion.FloorY() + RecordHeight);
        KitMotionMover m = KitMotion.Bounce(Owner, from, dir, RecordSpeed, Bounces, RecordWarn, RecordSprite, null, 1, RecordSize);
        if (m == null) return;
        var r = new Record { Mover = m, Points = BouncePoints(from, dir, Bounces, KitMotion.BoxFor(RecordSize)), Times = new List<float>(), NextCorner = 1 };
        float acc = 0f;
        r.Times.Add(0f);
        for (int i = 1; i < r.Points.Count; i++)
        {
            acc += (r.Points[i] - r.Points[i - 1]).magnitude;
            r.Times.Add(acc / RecordSpeed);
        }
        // 予告：2本目の道（1本目は KitMotion.Bounce が引く）
        if (r.Points.Count >= 3) KitMotion.WarnLine(m.transform, r.Points[1], r.Points[2], m.Warn, KitMotion.Red);
        _records.Add(r);
    }

    protected override void OnFrame(float clock)
    {
        _records.RemoveAll(r => r.Mover == null || r.Mover.IsDone);
        if (_pending > 0 && _records.Count < MaxRecords && !Quiet)
        {
            _pending--;
            SpawnRecord();
        }
        // 跳ねるたびに、次の道の線を 0.3秒
        foreach (var r in _records)
        {
            float t = r.Mover.MoveTime;
            while (r.NextCorner < r.Points.Count - 1 && t >= r.Times[r.NextCorner])
            {
                int k = r.NextCorner++;
                if (k + 1 < r.Points.Count - 1) // 最後の「外へ出る」道には引かない
                    KitMotion.WarnLine(_root.transform, r.Points[k], r.Points[k + 1], NextLineTime, new Color(KitMotion.Red.r, KitMotion.Red.g, KitMotion.Red.b, 0.7f));
            }
        }
    }

    /// <summary>KitPath.Bounce と同じ跳ね返りの点（はじめ・跳ねた所・外へ出る先）</summary>
    static List<Vector2> BouncePoints(Vector2 from, Vector2 dir, int bounces, Rect box)
    {
        var pts = new List<Vector2>();
        Vector2 p = new Vector2(Mathf.Clamp(from.x, box.xMin, box.xMax), Mathf.Clamp(from.y, box.yMin, box.yMax));
        Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : new Vector2(1f, 1f).normalized;
        pts.Add(p);
        for (int i = 0; i < bounces; i++)
        {
            float tx = float.PositiveInfinity, ty = float.PositiveInfinity;
            if (d.x > 1e-4f) tx = (box.xMax - p.x) / d.x;
            else if (d.x < -1e-4f) tx = (box.xMin - p.x) / d.x;
            if (d.y > 1e-4f) ty = (box.yMax - p.y) / d.y;
            else if (d.y < -1e-4f) ty = (box.yMin - p.y) / d.y;
            float t = Mathf.Max(0.001f, Mathf.Min(tx, ty));
            if (float.IsInfinity(t)) break;
            p += d * t;
            pts.Add(p);
            if (tx <= ty + 1e-4f) d.x = -d.x;
            if (ty <= tx + 1e-4f) d.y = -d.y;
        }
        pts.Add(p + d * 30f); // 外へ出る先（線は引かない）
        return pts;
    }

    static readonly Dictionary<char, Color32> RecordPal = new Dictionary<char, Color32>
    {
        { 'G', RuntimeSprites.Hex("868C96") }, // 円盤
        { 'D', RuntimeSprites.Hex("6B7079") }, // みぞ
        { 'S', SpotifyArt.Green },             // まん中のラベル
        { 'K', RuntimeSprites.Hex("2A2D33") }, // 穴
    };

    /// <summary>レコード（14×14。灰色の円盤に緑のまん中）</summary>
    static Sprite RecordSprite => RuntimeSprites.FromAscii("SpotifyEvent_Record", new[]
    {
        ".....GGGG.....",
        "...GGGGGGGG...",
        "..GGDDDDDDGG..",
        ".GGDGGGGGGDGG.",
        ".GDGGSSSSGGDG.",
        "GGDGSSSSSSGDGG",
        "GGDGSSKKSSGDGG",
        "GGDGSSKKSSGDGG",
        "GGDGSSSSSSGDGG",
        ".GDGGSSSSGGDG.",
        ".GGDGGGGGGDGG.",
        "..GGDDDDDDGG..",
        "...GGGGGGGG...",
        ".....GGGG.....",
    }, RecordPal);
}
