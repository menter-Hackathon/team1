using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// 【仮】出来事「群れ」（2026-11-19。docs/stage-pacing-design.md 3-2・3-3・5章）。担当2 の物。
// 危ない物は出ない。小さい雑魚（担当3 の ZakoVariants.Small。まだ無ければ歩く雑魚）が、列で流れ込む。0.25秒おきに1体ずつ。
// 出すのは ZakoSpawner.SpawnNow（ゲージに数える）。渡された数（zakoBudget）より多く出さない。null が返ったら、もう出さない。
// 出し方
// ・FarEdge：プレイヤーから遠い方の端から。1列の上限（9体。重ねる群れは5体）より多いときは2列に分け、2列目は反対の端から（はさみうち）
// ・BothEdges：両端から同時に（Game の一斉ダウンロード）
// ・FromTop：画面の上にタブの列が並び、タブから1体ずつ落ちてくる（Browser のタブを開きすぎた）。プレイヤーの真上のタブからは落とさない
// 出し終えて、出した雑魚が全員倒れたら終わる（倒し切ったのに、何も無い時間が続かないように）。

/// <summary>出来事「群れ」（Browser・Game・Zoom、3ステージ目の重ねる群れ）</summary>
public class SwarmEvent : PaceEvent
{
    public enum Entry
    {
        FarEdge = 0,
        BothEdges = 1,
        FromTop = 2,
    }

    const float Step = 0.25f;         // 1体ずつの間
    const float TopStep = 0.4f;       // タブから落ちる間（1つずつ見えるように少し長く）
    const float LineGap = 1.2f;       // 1列目と2列目の間
    const int MaxLine = 9;            // 1列の数の上限
    const float EdgeInset = 1.2f;     // 端から列の先頭まで（マス）
    const float MinDistance = 4f;     // プレイヤーからこれより近くには出さない（今のまま）
    const float TabNear = 3f;         // プレイヤーの真上とみなす横の距離（マス）

    struct Order
    {
        public float At;
        public int Line;   // 0・1（FarEdge の列）／BothEdges：右 1・左 -1 を入れる
    }

    readonly Entry _entry;
    readonly List<Order> _orders = new List<Order>();
    readonly List<Health> _mine = new List<Health>();
    readonly List<Transform> _tabs = new List<Transform>();
    int _nextOrder;
    int _spawned;
    bool _exhausted;
    float _clock;
    int _lineSide;     // FarEdge の1列目の側（右 1／左 -1。最初に出すときに決める）

    public SwarmEvent(Entry entry)
    {
        _entry = entry;
    }

    /// <summary>1列の数の上限（FarEdge。こえたら2列に分け、2列目は反対の端から）。重ねる群れは少なくする</summary>
    public int MaxPerLine { get; set; } = MaxLine;

    public override bool SpawnsOwnZako => true;
    public override float MaxSeconds => 16f;
    protected override bool NeedsRunner => false;

    protected override void OnBegin()
    {
        int total = Budget;
        if (total <= 0) return;
        switch (_entry)
        {
            case Entry.BothEdges:
                for (int i = 0; i < total; i++) _orders.Add(new Order { At = (i / 2) * Step, Line = i % 2 == 0 ? 1 : -1 });
                break;
            case Entry.FromTop:
                for (int i = 0; i < total; i++) _orders.Add(new Order { At = i * TopStep, Line = 0 });
                BuildTabs(Mathf.Min(total, MaxLine));
                break;
            default:
            {
                int lines = total > Mathf.Max(1, MaxPerLine) ? 2 : 1;
                int first = lines == 2 ? (total + 1) / 2 : total;
                float t = 0f;
                for (int i = 0; i < total; i++)
                {
                    if (i == first) t += LineGap;
                    _orders.Add(new Order { At = t, Line = i < first ? 0 : 1 });
                    t += Step;
                }
                break;
            }
        }
    }

    protected override bool OnTick(float dt)
    {
        _mine.RemoveAll(h => h == null || h.IsDead);
        _clock += dt;
        ZakoSpawner spawner = Pacer != null ? Pacer.Spawner : null;
        if (spawner == null) _exhausted = true;
        while (!_exhausted && _nextOrder < _orders.Count && _clock >= _orders[_nextOrder].At && _spawned < Budget)
        {
            if (!SpawnOne(spawner, _orders[_nextOrder])) break; // 出せる場所が無い：次のフレームにもう一度
            _nextOrder++;
        }
        bool allOut = _exhausted || _nextOrder >= _orders.Count || _spawned >= Budget;
        return !(allOut && _mine.Count == 0);
    }

    /// <summary>1体出す。出せる場所が無ければ false（あとでもう一度）。SpawnNow が null なら、もう出さない</summary>
    bool SpawnOne(ZakoSpawner spawner, Order order)
    {
        Rect a = Area;
        Vector2 player = PlayerPos;
        Vector2 feet;
        Transform tab = null;
        switch (_entry)
        {
            case Entry.BothEdges:
                if (!TryEdge(a, order.Line, player, out feet) && !TryEdge(a, -order.Line, player, out feet)) return Postpone(); // その端にプレイヤーがいる：反対の端から
                break;
            case Entry.FromTop:
                tab = PickTab(player.x);
                if (tab == null) return Postpone();
                feet = (Vector2)tab.position + Vector2.down * 0.4f; // 空中に出す（落ちてくる）
                break;
            default:
            {
                if (_nextOrder == 0) _lineSide = player.x <= a.center.x ? 1 : -1; // 遠い方の端
                int side = order.Line == 0 ? _lineSide : -_lineSide;
                if (!TryEdge(a, side, player, out feet))
                {
                    if (!TryEdge(a, -side, player, out feet)) return Postpone(); // その端にプレイヤーがいる：反対の端から
                }
                break;
            }
        }

        int template = SmallTemplate();
        Health h = spawner.SpawnNow(template, feet);
        if (h == null && template != 0) h = spawner.SpawnNow(0, feet); // 小さい雑魚のひな形が使えない：歩く雑魚で
        if (h == null)
        {
            _exhausted = true; // ノルマを使い切った・ボスの登場が始まった
            return false;
        }
        _mine.Add(h);
        _spawned++;
        if (tab != null) DropTab(tab);
        return true;
    }

    /// <summary>出せる場所が無いとき：残りの注文を少し遅らせる（毎フレーム調べ直さない。ずっと出せなければ MaxSeconds で終わる）</summary>
    bool Postpone()
    {
        for (int i = _nextOrder; i < _orders.Count; i++)
        {
            Order o = _orders[i];
            o.At += 0.2f;
            _orders[i] = o;
        }
        return false;
    }

    /// <summary>端（右 1／左 -1）の近くの床の上。穴・壁の中・プレイヤーに近い所は、内側へずらす</summary>
    bool TryEdge(Rect a, int side, Vector2 player, out Vector2 feet)
    {
        float edge = side > 0 ? a.xMax - EdgeInset : a.xMin + EdgeInset;
        for (int i = 0; i < 8; i++)
        {
            float x = edge - side * i * 0.5f;
            if (TryFeet(a, x, player, out feet)) return true;
        }
        feet = default;
        return false;
    }

    bool TryFeet(Rect a, float x, Vector2 player, out Vector2 feet)
    {
        feet = default;
        if (Mathf.Abs(x - player.x) < MinDistance) return false;
        int mask = LayerMask.GetMask("Ground", "OneWayPlatform");
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, a.yMax - 0.5f), Vector2.down, a.height, mask);
        if (hit.collider == null || hit.distance < 0.2f) return false; // 床が無い（穴）・壁の中
        feet = hit.point;
        // ノルマの無いステージ（道中）では、ボス部屋の中に出さない
        if (Pacer != null && Pacer.Remaining < 0 && BattleArena.GetBounds().Contains(feet + Vector2.up * 0.5f)) return false;
        return Vector2.Distance(feet, player) >= MinDistance;
    }

    // ───────── Browser：画面の上のタブの列 ─────────

    void BuildTabs(int count)
    {
        Rect a = Area;
        Color main = FxKit.MainOf(Pacer != null ? Pacer.Theme : null, new Color(0.26f, 0.52f, 0.96f));
        Color light = Color.Lerp(main, Color.white, 0.5f);
        float y = a.yMax - 1.7f; // 上の表示（HUD）の下
        float w = Mathf.Min(1.4f, (a.width - 2f) / Mathf.Max(1, count) - 0.15f);
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(a.xMin + 1f + w * 0.5f, a.xMax - 1f - w * 0.5f, count == 1 ? 0.5f : i / (float)(count - 1));
            var tab = new GameObject("PaceTab").transform;
            tab.position = new Vector3(x, y, 0f);
            // タブの形：明るい縁（1ドット）＋アプリの色の地＋上の明るい線。文字は入れない
            FxKit.Box(tab, Vector2.zero, new Vector2(w, 0.5f), light, 17, "Edge");
            FxKit.Box(tab, new Vector2(0f, -FxKit.Pixel * 0.5f), new Vector2(w - FxKit.Pixel * 2f, 0.5f - FxKit.Pixel), main, 18, "Body");
            FxKit.Box(tab, new Vector2(0f, 0.25f - FxKit.Pixel * 2f), new Vector2(w - FxKit.Pixel * 4f, FxKit.Pixel), new Color(1f, 1f, 1f, 0.6f), 19, "Shine");
            tab.localScale = Vector3.zero;
            tab.gameObject.AddComponent<PaceTabPop>().Delay = i * 0.05f;
            _tabs.Add(tab);
            Track(tab.gameObject);
        }
    }

    /// <summary>落とすタブ（プレイヤーの真上のタブは避ける。無ければ null）。残りが1つずつ使われる</summary>
    Transform PickTab(float playerX)
    {
        _tabs.RemoveAll(t => t == null);
        Transform best = null;
        float bestScore = float.NegativeInfinity;
        foreach (var t in _tabs)
        {
            float d = Mathf.Abs(t.position.x - playerX);
            if (d < TabNear) continue;
            float score = d + UnityEngine.Random.value * 3f; // 遠いほど選ばれやすい（毎回同じ順にしない）
            if (score > bestScore)
            {
                bestScore = score;
                best = t;
            }
        }
        // タブの数より出す数が多いとき：使ったタブは消さずに残す（BuildTabs は最大9つ）
        return best;
    }

    void DropTab(Transform tab)
    {
        Vfx.SmokeSmall((Vector2)tab.position + Vector2.down * 0.3f);
        // 出す数がタブの数より多いときは、タブを残して使い回す
        int remaining = _orders.Count - (_nextOrder + 1);
        if (remaining >= _tabs.Count) return;
        _tabs.Remove(tab);
        if (tab.TryGetComponent(out PaceTabPop pop)) pop.Close();
        else UnityEngine.Object.Destroy(tab.gameObject);
    }

    // ───────── 小さい雑魚のひな形（担当3） ─────────

    static Type _variants;
    static bool _looked;

    /// <summary>
    /// 小さい雑魚のひな形の番号（ZakoVariants.Small）。ZakoVariants がまだ無くてもコンパイルが通るように、名前で探す。
    /// 無い・-1 なら 0（歩く雑魚）
    /// </summary>
    static int SmallTemplate()
    {
        if (!_looked)
        {
            _looked = true;
            _variants = Type.GetType("ZakoVariants");
        }
        if (_variants == null) return 0;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        try
        {
            FieldInfo f = _variants.GetField("Small", flags);
            if (f != null && f.FieldType == typeof(int)) return Mathf.Max(0, (int)f.GetValue(null));
            PropertyInfo p = _variants.GetProperty("Small", flags);
            if (p != null && p.PropertyType == typeof(int)) return Mathf.Max(0, (int)p.GetValue(null));
        }
        catch (Exception) { /* 読めなければ、歩く雑魚で出す */ }
        return 0;
    }
}
