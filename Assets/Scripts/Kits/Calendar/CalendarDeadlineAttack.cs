using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】カレンダーのボスの技「締め切りの線」（2026-11-19。docs/app-kits-1-design.md 9章 P1-2）。場に残る技。
/// 床の全部に赤い帯の予告（2.5秒。0-3「足場の上だけ安全」）。足場には白い枠（ここが安全）。
/// 予告の間、赤い横の線（カレンダーの「今」の線。左の端に赤い丸）が、床から高さ 2.5 までゆっくり上がる →
/// 上がりきったら、線より下が判定 0.8秒。
/// ・判定は部屋の床の高さから決める（プレイヤーが足場の上にいても、足場の上に出さない）
/// ・避け方：足場に乗る（足場は高さ 3）
/// ・出ている間（CalendarDeadlineEffect）は、ほかの場に残る技を選ばない・ギミックの時間の帯が止まる
/// </summary>
[CreateAssetMenu(fileName = "Calendar_Deadline", menuName = "Team1/Kits/Calendar Deadline")]
public class CalendarDeadlineAttack : CustomAttack
{
    [Tooltip("予告（秒）。足場の上だけ安全なので 2.5")]
    [SerializeField, Min(0.8f)] float _telegraph = 2.5f;
    [Tooltip("判定の時間（秒）")]
    [SerializeField, Min(0.05f)] float _active = 0.8f;
    [Tooltip("判定の高さ（床から。マス）。足場の上の面（3）より低く")]
    [SerializeField, Min(0.5f)] float _height = 2.5f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        CalendarDeadlineEffect e = CalendarDeadlineEffect.Spawn(owner, data, _telegraph, _active, _height);
        if (e == null) yield break;
        owner.NotifyReleased(data);
        float timeout = Time.time + _telegraph + _active + 1f;
        while (e != null && Time.time < timeout) yield return null;
    }
}

/// <summary>締め切りの線の、場に出る物（予告の帯・足場の白い枠・上がる今の線・判定）。形態変化でも消える（場に残る技）</summary>
public class CalendarDeadlineEffect : CustomEffect
{
    const int FrameOrder = 17, LineOrder = 21;
    static readonly List<CalendarDeadlineEffect> Alive = new List<CalendarDeadlineEffect>();

    /// <summary>締め切りの線が出ているか（カレンダーのギミックが時間の帯を止める）</summary>
    public static bool AnyActive
    {
        get
        {
            Alive.RemoveAll(e => e == null);
            return Alive.Count > 0;
        }
    }

    public override bool Lingers => true;

    float _born, _telegraph, _active, _height, _floor;
    Rect _arena;
    Transform _line;
    readonly List<SpriteRenderer> _frames = new List<SpriteRenderer>();
    PaceZoneMark _mark;

    public static CalendarDeadlineEffect Spawn(AttackRunner owner, AttackData data, float telegraph, float active, float height)
    {
        Rect arena = BattleArena.GetBounds();
        float floor = CallKitUtil.FloorY(arena, arena.center.x);
        GameObject go = owner.CreateSpawnedObject("Calendar_Deadline", new Vector2(arena.center.x, floor));
        var e = go.AddComponent<CalendarDeadlineEffect>();
        e._born = Time.time;
        e._telegraph = telegraph;
        e._active = active;
        e._height = height;
        e._floor = floor;
        e._arena = arena;
        e.Build(owner, data);
        return e;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!Alive.Contains(this)) Alive.Add(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Alive.Remove(this);
    }

    void Build(AttackRunner owner, AttackData data)
    {
        // 赤い帯の予告（PaceZoneMark。自分の子にして、一緒に消す）
        _mark = PaceZoneMark.Show(null, _arena.xMin, _arena.xMax, _floor, _height, _telegraph, _active);
        if (_mark != null) _mark.transform.SetParent(transform, true);

        // 足場の白い枠（ここが安全）
        foreach (Bounds b in CallKitUtil.FindPlatforms(_arena))
        {
            Vector2 size = new Vector2(b.size.x + 0.3f, Mathf.Max(0.6f, b.size.y + 0.3f));
            int wp = Mathf.Clamp(Mathf.RoundToInt(size.x * CallArt.Ppu), 4, 200);
            int hp = Mathf.Clamp(Mathf.RoundToInt(size.y * CallArt.Ppu), 4, 60);
            SpriteRenderer f = CalendarArt.Put(transform, CallArt.Frame(wp, hp, 2), Vector2.zero, size, Color.white, FrameOrder, "SafeFrame");
            f.transform.position = new Vector3(b.center.x, b.center.y, 0f);
            _frames.Add(f);
        }

        // 今の線（赤い横の線と、左の端の赤い丸）
        _line = new GameObject("NowLine").transform;
        _line.SetParent(transform, false);
        CalendarArt.Put(_line, PlaceholderSprites.Square, Vector2.zero, new Vector2(_arena.width, 2f * CallArt.Px), CalendarArt.Red, LineOrder, "Line");
        CalendarArt.Put(_line, CalendarArt.NowDot, new Vector2(-_arena.width * 0.5f + 0.25f, 0f), Vector2.one * 7f * CallArt.Px, CalendarArt.Red, LineOrder + 1, "Dot");
        PlaceLine(0f);

        // 判定：部屋の床から高さ height、部屋の幅の全部（予告のあと）
        Vector2 center = new Vector2(_arena.center.x, _floor + _height * 0.5f);
        CallTimedHit hit = CallKitUtil.TimedHit(owner, data, center, new Vector2(_arena.width + 1f, _height), false, _telegraph, _active, "Deadline");
        if (hit != null) hit.transform.SetParent(transform, true);
    }

    void PlaceLine(float k)
    {
        if (_line == null) return;
        _line.position = new Vector3(_arena.center.x, _floor + _height * Mathf.Clamp01(k), 0f);
    }

    void Update()
    {
        float t = Time.time - _born;
        if (t >= _telegraph + _active)
        {
            Destroy(gameObject);
            return;
        }
        // ゆっくり上がる（最後に少し速く）
        float k = Mathf.Clamp01(t / _telegraph);
        PlaceLine(k * k * (3f - 2f * k));
        foreach (var f in _frames)
        {
            if (f == null) continue;
            f.color = CallKitUtil.Alpha(Color.white, t < _telegraph ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 4f)) : 1f);
        }
    }
}
