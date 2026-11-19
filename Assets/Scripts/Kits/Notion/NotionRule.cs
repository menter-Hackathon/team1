using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Notion のステージのギミック「トグルの足場」（2026-11-19。docs/app-kits-1-design.md 8章。担当0）。
/// ・部屋の足場（左右に1つずつ）がトグルになる。左の端に ▶／▼ の印
/// ・8秒おきに、片方が閉じる（▶。足場がうすくなり、乗れない）。もう片方は開いている（▼）。いつも、どちらか1つは開いている
/// ・閉じる 1.5秒前から、閉じる方の足場が点滅する。上にいたら落ちるだけ（痛くない）。ボス戦も同じ
/// ・【要相談③】足場を変えてよいか：決まるまでは入れる。100％・ステージの終わりで、足場は2つとも必ず戻す
/// ・足場の見つけ方は Premiere のレーザーツール（PremiereRazorEffect）と同じ（すり抜け床のうち、部屋の床・落下物・技が出した物を除く）
/// </summary>
public class NotionRule : StageRule
{
    /// <summary>【仮】閉じる足場が変わる間隔</summary>
    const float Period = 8f;
    /// <summary>【仮】閉じる前に点滅する秒数</summary>
    const float BlinkTime = 1.5f;
    /// <summary>【仮】閉じた足場のうすさ</summary>
    const float ClosedAlpha = 0.25f;

    class Platform
    {
        public Collider2D Collider;
        public readonly List<SpriteRenderer> Renderers = new List<SpriteRenderer>();
        public readonly List<Color> Colors = new List<Color>();
        public SpriteRenderer Mark;
        public Bounds Bounds;
        public bool Closed;
    }

    readonly List<Platform> _platforms = new List<Platform>(); // [0]＝左、[1]＝右
    float _timer;
    int _next;       // 次に閉じる足場（0 か 1）
    bool _warned;    // 今回の点滅の音を鳴らしたか
    Rect _foundIn;

    public override void Begin(StageRuleContext ctx) => Find(ctx.PlayArea);

    public override void OnBossAppeared(BossController boss)
    {
        // ボス部屋が別の所にあれば、そこの足場で始め直す（Notion は1画面のステージなので、ふつうは同じ足場）
        Rect arena = Ctx.Arena;
        if (_platforms.Count == 2 && arena.Overlaps(_foundIn)) return;
        RestoreAll();
        Find(arena);
    }

    public override void Tick(float dt)
    {
        if (_platforms.Count < 2) return;
        _timer += dt;
        int closing = _next;
        float untilClose = Period - _timer;
        // 閉じる前の点滅（開いている方だけ）
        Platform p = _platforms[closing];
        if (!p.Closed && untilClose <= BlinkTime) SetAlpha(p, Mathf.FloorToInt(Time.time / 0.12f) % 2 == 0 ? 1f : 0.45f);
        if (!p.Closed && untilClose <= BlinkTime && !_warned) { _warned = true; GameAudio.Event("gm_notion_toggle_warn"); }
        if (untilClose > 0f) return;
        _timer = 0f;
        _warned = false;
        // 閉じる方を閉じて、もう片方を開く（いつも1つは開いている）
        SetClosed(_platforms[closing], true);
        SetClosed(_platforms[1 - closing], false);
        GameAudio.Event("gm_notion_toggle_close");
        _next = 1 - closing;
    }

    public override void End() => RestoreAll();

    // ───────── 足場 ─────────

    void Find(Rect area)
    {
        _platforms.Clear();
        _foundIn = area;
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        if (oneWay < 0) return;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(1 << oneWay);
        var hits = new List<Collider2D>();
        Physics2D.OverlapArea(new Vector2(area.xMin, area.yMin + 0.1f), new Vector2(area.xMax, area.yMax), filter, hits);
        Collider2D left = null, right = null;
        float cx = area.center.x;
        foreach (Collider2D col in hits)
        {
            if (col == null || !col.enabled || col.gameObject.layer != oneWay) continue;
            Bounds b = col.bounds;
            if (b.size.x >= area.width * 0.5f) continue;                       // 部屋の床
            if (col.GetComponentInParent<FallingObject>() != null) continue;     // 落下物の足場
            if (col.GetComponentInParent<BlinkingPlatform>() != null) continue;  // 開いたり閉じたりする足場
            if (col.GetComponentInParent<CustomEffect>() != null) continue;      // 技が出した物
            // 左右それぞれ、まん中から 3.5マスに近い物
            if (b.center.x < cx) { if (left == null || Mathf.Abs(b.center.x - (cx - 3.5f)) < Mathf.Abs(left.bounds.center.x - (cx - 3.5f))) left = col; }
            else { if (right == null || Mathf.Abs(b.center.x - (cx + 3.5f)) < Mathf.Abs(right.bounds.center.x - (cx + 3.5f))) right = col; }
        }
        if (left == null || right == null) return; // 足場が2つ無いステージでは何もしない
        _platforms.Add(Make(left));
        _platforms.Add(Make(right));
        _timer = 0f;
        _warned = false;
        _next = 0;
    }

    Platform Make(Collider2D col)
    {
        var p = new Platform { Collider = col, Bounds = col.bounds };
        foreach (var sr in col.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr == null || !sr.enabled) continue;
            p.Renderers.Add(sr); // 色は最初に変えるときに覚える（ステージの色の塗り替えが終わったあと）
        }
        // 左の端の印（▼＝開いている）
        var go = new GameObject("NotionToggleMark");
        go.transform.position = new Vector3(p.Bounds.min.x + 0.3f, p.Bounds.max.y + 0.3f, 0f);
        p.Mark = go.AddComponent<SpriteRenderer>();
        p.Mark.sprite = NotionArt.ToggleMark(true);
        p.Mark.sortingOrder = 25;
        Ctx.TrackDecor(go);
        return p;
    }

    void SetClosed(Platform p, bool closed)
    {
        p.Closed = closed;
        if (p.Collider != null) p.Collider.enabled = !closed;
        SetAlpha(p, closed ? ClosedAlpha : 1f);
        if (p.Mark != null) p.Mark.sprite = NotionArt.ToggleMark(!closed);
    }

    static void SetAlpha(Platform p, float a)
    {
        if (p.Colors.Count != p.Renderers.Count)
        {
            if (a >= 1f) return; // まだ変えていない
            p.Colors.Clear();
            foreach (var sr in p.Renderers) p.Colors.Add(sr != null ? sr.color : Color.white);
        }
        for (int i = 0; i < p.Renderers.Count; i++)
        {
            SpriteRenderer sr = p.Renderers[i];
            if (sr == null) continue;
            Color c = p.Colors[i];
            c.a *= a;
            sr.color = c;
        }
    }

    void RestoreAll()
    {
        foreach (var p in _platforms)
        {
            if (p.Collider != null) p.Collider.enabled = true;
            SetAlpha(p, 1f);
            p.Closed = false;
            if (p.Mark != null) Object.Destroy(p.Mark.gameObject);
        }
        _platforms.Clear();
    }
}
