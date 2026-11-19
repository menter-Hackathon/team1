using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Figma のステージの決まり：共同編集のカーソル（2026-11-19。docs/app-kits-2-design.md 2-4）。
/// ・ステージの間ずっと、ほかの人のカーソルが 2つ、画面をうろうろする（当たらない。飾り）
/// ・9秒おきに、1つが四角を描く：床の上に青い選択の枠（3×2マス。1.0秒。プレイヤーから 4マス以上。当たらないので赤にしない）
///   → 灰色の四角 Rectangle 1 が置かれる（KitBlock。乗れる・壁・弾を止める。当たらない）→ 8秒で、別のカーソルが消す。同時に 2つまで
/// ・四角が出る所にいた雑魚は、上に乗る（KitBlock が押し上げる）。ボス戦も続く。危ない物は出さない
/// ・片づけ：枠・四角は ctx.TrackPlaced（ボスの登場・50％・100％で消える）、カーソルは ctx.TrackDecor
/// </summary>
public class FigmaRule : StageRule
{
    /// <summary>【仮】四角を描く間隔（秒）</summary>
    const float Interval = 9f;
    /// <summary>【仮】最初の四角までの秒数</summary>
    const float FirstDelay = 5f;
    const float Life = 8f;
    const int MaxRects = 2;
    static readonly Vector2 RectSize = new Vector2(3f, 2f);

    readonly List<FigmaWanderCursor> _cursors = new List<FigmaWanderCursor>();
    readonly List<FigmaDrawJob> _jobs = new List<FigmaDrawJob>();
    float _next;

    public override void Begin(StageRuleContext ctx)
    {
        _next = FirstDelay;
        for (int i = 0; i < 2; i++)
        {
            Rect a = ctx.PlayArea;
            var root = FigmaArt.CursorWithTag(FigmaArt.NameOf(i), FigmaArt.CursorColor(i), new Vector2(Mathf.Lerp(a.xMin, a.xMax, 0.3f + 0.4f * i), a.yMax - 2f), 41);
            var w = root.gameObject.AddComponent<FigmaWanderCursor>();
            w.Area = () => Ctx != null ? Ctx.PlayArea : a;
            ctx.TrackDecor(root.gameObject);
            _cursors.Add(w);
        }
    }

    public override void Tick(float dt)
    {
        _jobs.RemoveAll(j => j == null);
        _next -= dt;
        if (_next > 0f) return;
        if (!Ctx.CanPlace || _jobs.Count >= MaxRects) { _next = 1f; return; }
        if (!KitBlock.TryFindSpot(Ctx.PlayArea, RectSize, 4f, out Vector2 pos)) { _next = 1.5f; return; }
        FigmaWanderCursor drawer = Free(null);
        if (drawer == null) { _next = 1f; return; }
        var go = new GameObject("FigmaDrawJob");
        var job = go.AddComponent<FigmaDrawJob>();
        Ctx.TrackPlaced(go);
        job.Begin(this, drawer, pos, RectSize, Life);
        _jobs.Add(job);
        _next = Interval;
    }

    /// <summary>手のあいているカーソル（but 以外）</summary>
    internal FigmaWanderCursor Free(FigmaWanderCursor but)
    {
        _cursors.RemoveAll(c => c == null);
        foreach (var c in _cursors) if (c != but && !c.Busy) return c;
        return null;
    }

    public override void End()
    {
        foreach (var c in _cursors) if (c != null) c.Busy = false;
    }
}

/// <summary>うろうろするカーソル（当たらない飾り）。Busy の間は、描く係が動かす</summary>
public class FigmaWanderCursor : MonoBehaviour
{
    internal System.Func<Rect> Area;
    internal bool Busy;
    Vector2 _target;
    float _retargetAt;

    void Update()
    {
        if (Busy) return;
        Rect a = Area != null ? Area() : ScreenUi.ViewRect();
        if (Time.time >= _retargetAt || !a.Contains(_target))
        {
            _target = new Vector2(Random.Range(a.xMin + 1.5f, a.xMax - 2.5f), Random.Range(a.yMin + 2.5f, a.yMax - 1.5f));
            _retargetAt = Time.time + Random.Range(1.2f, 2.6f);
        }
        transform.position = Vector2.MoveTowards(transform.position, _target, 4.5f * Time.deltaTime);
    }

    /// <summary>pos へ speed マス/秒で動く（コルーチンの中で）</summary>
    internal IEnumerator MoveTo(Vector2 pos, float speed)
    {
        while (this != null && ((Vector2)transform.position - pos).sqrMagnitude > 0.0004f)
        {
            transform.position = Vector2.MoveTowards(transform.position, pos, speed * Time.deltaTime);
            yield return null;
        }
    }
}

/// <summary>四角を1つ描いて、置いて、時間で消す係（FigmaRule が作る。ctx.TrackPlaced で片づく）</summary>
public class FigmaDrawJob : MonoBehaviour
{
    FigmaRule _rule;
    FigmaWanderCursor _drawer;
    Vector2 _pos, _size;
    float _life;
    KitBlock _block;

    internal void Begin(FigmaRule rule, FigmaWanderCursor drawer, Vector2 pos, Vector2 size, float life)
    {
        _rule = rule;
        _drawer = drawer;
        _pos = pos;
        _size = size;
        _life = life;
        transform.position = pos;
        StartCoroutine(Run());
    }

    void OnDestroy()
    {
        if (_drawer != null) _drawer.Busy = false;
        if (_block != null) Destroy(_block.gameObject);
    }

    IEnumerator Run()
    {
        _drawer.Busy = true;
        Vector2 h = _size * 0.5f;
        Vector2 c0 = _pos + new Vector2(-h.x, h.y), c1 = _pos + new Vector2(h.x, -h.y);
        // 左上の角へ動く
        yield return _drawer.MoveTo(c0, 16f);
        GameAudio.Event("gm_figma_draw");
        // ドラッグ：青い枠が広がる（1.0秒。当たらないので青）
        var frame = new GameObject("Frame").transform;
        frame.SetParent(transform, false);
        frame.position = _pos;
        var parts = new List<SpriteRenderer>();
        const float drag = 0.7f, hold = 0.3f;
        float start = Time.time;
        while (Time.time - start < drag)
        {
            float k = Mathf.Clamp01((Time.time - start) / drag);
            Vector2 cur = Vector2.Lerp(c0, c1, 1f - (1f - k) * (1f - k));
            if (_drawer != null) _drawer.transform.position = cur;
            foreach (var p in parts) if (p != null) Destroy(p.gameObject);
            parts.Clear();
            Vector2 mid = (c0 + cur) * 0.5f;
            frame.position = mid;
            parts.AddRange(FigmaArt.SelectionFrame(frame, new Vector2(Mathf.Abs(cur.x - c0.x), Mathf.Abs(cur.y - c0.y)), 40));
            yield return null;
        }
        foreach (var p in parts) if (p != null) Destroy(p.gameObject);
        parts.Clear();
        frame.position = _pos;
        parts.AddRange(FigmaArt.SelectionFrame(frame, _size, 40));
        TextMesh dims = WorldText.Create(frame, $"{Mathf.RoundToInt(_size.x * 18f)} x {Mathf.RoundToInt(_size.y * 18f)}", 0.28f, Color.white, 43, true, TextAnchor.MiddleCenter, "Dims");
        dims.transform.localPosition = new Vector3(0f, -h.y - 0.4f, 0f);
        var pill = RuntimeSprites.Show(frame, FigmaArt.RoundRect(42, 9), new Vector2(0f, -h.y - 0.4f), 42, 1f, "Pill");
        pill.color = FigmaArt.Blue;
        yield return new WaitForSeconds(hold);

        // 置く（その瞬間にもう一度調べる。プレイヤーが入っていたら、やめる）
        _block = KitBlock.Place(_pos, _size, PlaceholderSprites.Square, _life);
        if (_block == null)
        {
            Destroy(frame.gameObject);
            if (_drawer != null) _drawer.Busy = false;
            Destroy(gameObject, 0.05f);
            yield break;
        }
        _block.SetColor(FigmaArt.RectGray); // 四角は、この係が消えると一緒に消える（係は ctx.TrackPlaced 済み）
        GameAudio.Event("gm_figma_rect_place");
        TextMesh label = WorldText.Create(_block.transform, "Rectangle 1", 0.28f, MacUi.Gray2, 7, true, TextAnchor.LowerLeft, "Label");
        label.transform.localPosition = new Vector3(-h.x, h.y + 0.08f, 0f);
        FxKit.Ring(_pos, 2.2f, 0.25f, FigmaArt.Blue);
        // 選んだままを少し見せてから、枠を消す
        yield return new WaitForSeconds(0.6f);
        Destroy(frame.gameObject);
        if (_drawer != null) _drawer.Busy = false;

        // 消す前に、別のカーソルが来る（来られなければ、そのまま時間で消える）
        float deleteAt = Time.time + Mathf.Max(0.5f, _life - 1.6f - 0.6f - hold);
        while (Time.time < deleteAt && _block != null) yield return null;
        if (_block == null) { Destroy(gameObject); yield break; }
        FigmaWanderCursor eraser = _rule.Free(null);
        if (eraser != null)
        {
            eraser.Busy = true;
            yield return eraser.MoveTo(_pos + new Vector2(h.x * 0.3f, h.y * 0.2f), 14f);
            if (_block != null)
            {
                var f2 = new GameObject("Frame2").transform;
                f2.SetParent(transform, false);
                f2.position = _pos;
                FigmaArt.SelectionFrame(f2, _size, 40);
                GameAudio.Event("gm_figma_delete_warn");
                yield return new WaitForSeconds(0.35f);
                if (_block != null) FxKit.Text("Delete", _pos + new Vector2(0f, h.y + 0.5f), 0.34f, MacUi.Gray6, 47, 0.5f, Color.white);
                if (_block != null) _block.Vanish(0.2f);
                Destroy(f2.gameObject, 0.2f);
            }
            if (eraser != null) eraser.Busy = false;
        }
        while (_block != null) yield return null;
        Destroy(gameObject);
    }
}
