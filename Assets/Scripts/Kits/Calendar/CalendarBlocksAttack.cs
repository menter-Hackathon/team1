using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】カレンダーのボスの技「予定ブロック」（2026-11-19。docs/app-kits-1-design.md 9章 P1-1）。
/// 画面の上に曜日の 7列（うすい縦の線つき）。そのうち filled 列に、色の付いた予定のブロック（2.5×1.5）が並ぶ（0.2秒おき）→
/// 0.6秒待つ → 左から 0.3秒おきに落ちる。予告は床の影と赤い「！」（並んだ瞬間から）。
/// ・プレイヤーのいる列は、かならず埋まる（動かないと当たる）。空いている列（幅 約2.8マス）に立てば当たらない
/// ・ブロックの数値（大きさ・落ちる速さ）はこの技の攻撃データの「落下物」の欄。絵は予定の板（KitDress.Swap）
/// 第2形態：5列（空きは 2列）。埋まった列の1つに、2個続けて落ちる（ダブルブッキング）
/// </summary>
[CreateAssetMenu(fileName = "Calendar_Blocks", menuName = "Team1/Kits/Calendar Blocks")]
public class CalendarBlocksAttack : CustomAttack
{
    [Tooltip("埋める列の数（7列のうち）")]
    [SerializeField, Range(1, 6)] int _filled = 4;
    [Tooltip("並べる間隔（秒）")]
    [SerializeField, Min(0f)] float _placeInterval = 0.2f;
    [Tooltip("並べ終えてから落ち始めるまで（秒）")]
    [SerializeField, Min(0f)] float _hold = 0.6f;
    [Tooltip("落とす間隔（秒。左から）")]
    [SerializeField, Min(0f)] float _dropInterval = 0.3f;
    [Tooltip("ダブルブッキング（埋まった列の1つに、もう1個続けて落とす）")]
    [SerializeField] bool _doubleBook;
    [Tooltip("ダブルブッキングの2個目が落ちるまでの遅れ（秒）")]
    [SerializeField, Min(0.1f)] float _doubleDelay = 0.45f;

    public const int Columns = 7;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        Rect arena = BattleArena.GetBounds();
        float colW = arena.width / Columns;
        Vector2 target = owner.TargetPoint ?? arena.center;
        int playerCol = Mathf.Clamp(Mathf.FloorToInt((target.x - arena.xMin) / colW), 0, Columns - 1);

        // 埋める列：プレイヤーの列＋ランダム
        var cols = new List<int> { playerCol };
        var rest = new List<int>();
        for (int i = 0; i < Columns; i++) if (i != playerCol) rest.Add(i);
        while (cols.Count < _filled && rest.Count > 0)
        {
            int k = Random.Range(0, rest.Count);
            cols.Add(rest[k]);
            rest.RemoveAt(k);
        }
        cols.Sort();

        float hangY = arena.yMax - 1.7f - data.ObjectSize.y * 0.5f;
        float now = Time.time;
        int n = cols.Count;
        float lastPlace = now + (n - 1) * _placeInterval;
        float lastFall = lastPlace + _hold + (n - 1) * _dropInterval;
        int doubleCol = _doubleBook ? cols[Random.Range(0, n)] : -1;
        float headerLife = lastFall - now + (_doubleBook ? _doubleDelay : 0f) + 1.2f;
        CalendarWeekHeader.Spawn(owner, arena, cols, headerLife);

        var objects = new List<FallingObject>();
        int colorStart = Random.Range(0, CalendarArt.EventColors.Length);
        for (int i = 0; i < n; i++)
        {
            float x = arena.xMin + colW * (cols[i] + 0.5f);
            float appearAt = now + i * _placeInterval;
            float fallAt = lastPlace + _hold + i * _dropInterval;
            objects.Add(Spawn(owner, data, new Vector2(x, hangY), appearAt, fallAt, colorStart + i, i));
            if (cols[i] == doubleCol)
            {
                // 2個目：上に重ねて並べ、少し遅れて同じ列に落とす（同じ時刻に別の予定）
                objects.Add(Spawn(owner, data, new Vector2(x, hangY + data.ObjectSize.y + 0.15f), appearAt, fallAt + _doubleDelay, colorStart + i + 1, n + i));
            }
        }
        owner.NotifyReleased(data);

        float timeout = Time.time + 8f;
        while (Time.time < timeout && objects.Exists(o => o != null && !o.HasLanded)) yield return null;
    }

    static FallingObject Spawn(AttackRunner owner, AttackData data, Vector2 hang, float appearAt, float fallAt, int color, int index)
    {
        FallingObject f = FallingObject.Spawn(owner, data, hang, appearAt, fallAt, true, false, index);
        if (f == null) return null;
        KitDress.Swap(f.gameObject, CalendarArt.Block(color));
        CallKitUtil.AddExclaim(f);
        return f;
    }
}

/// <summary>画面の上の曜日の 7列とうすい縦の線（飾り。列の境目が読める。今日の曜日に赤い丸）。形態変化でも消える</summary>
public class CalendarWeekHeader : CustomEffect
{
    const int Order = 12;
    float _born, _endAt;
    readonly List<SpriteRenderer> _srs = new List<SpriteRenderer>();
    readonly List<float> _alpha = new List<float>();
    readonly List<TextMesh> _texts = new List<TextMesh>();

    public static CalendarWeekHeader Spawn(AttackRunner owner, Rect arena, List<int> filled, float seconds)
    {
        if (owner == null) return null;
        GameObject go = owner.CreateSpawnedObject("Calendar_WeekHeader", new Vector2(arena.xMin, arena.yMin));
        var h = go.AddComponent<CalendarWeekHeader>();
        h._born = Time.time;
        h._endAt = Time.time + seconds;
        h.Build(arena, filled);
        return h;
    }

    void Build(Rect arena, List<int> filled)
    {
        float colW = arena.width / CalendarBlocksAttack.Columns;
        float top = arena.height;
        // 曜日の帯（白いうすい地）
        Add(CalendarArt.Put(transform, PlaceholderSprites.Square, new Vector2(arena.width * 0.5f, top - 0.55f), new Vector2(arena.width, 1.1f), new Color(1f, 1f, 1f, 0.18f), Order, "Band"), 0.18f);
        int today = Random.Range(0, CalendarBlocksAttack.Columns);
        for (int i = 0; i < CalendarBlocksAttack.Columns; i++)
        {
            float cx = colW * (i + 0.5f);
            if (i > 0) Add(CalendarArt.Put(transform, PlaceholderSprites.Square, new Vector2(colW * i, top * 0.5f), new Vector2(CallArt.Px, top), CalendarArt.Grid, Order, "Grid"), CalendarArt.Grid.a);
            if (i == today) Add(CalendarArt.Put(transform, CallArt.Disc(14), new Vector2(cx, top - 0.55f), Vector2.one * 0.78f, CalendarArt.Red, Order + 1, "Today"), 1f);
            TextMesh tm = WorldText.Create(transform, CalendarArt.Weekdays[i], 0.4f, Color.white, Order + 2, true, TextAnchor.MiddleCenter, "Day");
            tm.transform.localPosition = new Vector3(cx, top - 0.55f, 0f);
            _texts.Add(tm);
        }
        Apply(0f);
    }

    void Add(SpriteRenderer sr, float a)
    {
        _srs.Add(sr);
        _alpha.Add(a);
    }

    void Update()
    {
        if (Time.time >= _endAt)
        {
            Destroy(gameObject);
            return;
        }
        float k = Mathf.Clamp01((Time.time - _born) / 0.2f) * Mathf.Clamp01((_endAt - Time.time) / 0.3f);
        Apply(k);
    }

    void Apply(float k)
    {
        for (int i = 0; i < _srs.Count; i++)
        {
            if (_srs[i] == null) continue;
            Color c = _srs[i].color;
            c.a = _alpha[i] * k;
            _srs[i].color = c;
        }
        foreach (var t in _texts) if (t != null) WorldText.SetColor(t, CallKitUtil.Alpha(Color.white, k));
    }
}
