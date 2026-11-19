using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Notion の場面③の出来事「ドラッグ＆ドロップ」（2026-11-19。docs/stage-gimmick-variety-b.md 4章）。数値はすべて【仮】。
/// ・白いブロック（横 4×縦 1。左に「⋮⋮」）が、左右の端から交互に、まっすぐ・ゆっくり横切る（速さ 5）。
///   段は 3つ（床から 0.5／3.5／5.5）のうち「プレイヤーが今いる段」。2.0秒おきに 6個（ステージで 5〜7個）。
///   画面に同時に 3個まで（予告中も数える。仕様は 2個だが、横切るのに 5秒かかるので 2.0秒おきだと 3個になる。罰の 2個同時のときは 4個まで）
/// ・予告 0.9秒（1個目は 1.0秒）：入る端に矢印と赤い線＋Notion の青のドロップの線（縦）。ブロックのうしろに、うすい青い影（ドラッグ中）
/// ・4個目の予告と同時に、まん中の上の段（床から 5.8。callout の足場の上）にトグル「▶」の的（2.2秒。仕様の 4秒から短くした：
///   叩かなかった罰を 5個目に間に合わせるため。的が出ている間は、次のブロックを待たせる）。
///   叩くと「▼」に回って、画面のブロックが全部たたまれて消え、残りは出ない（出来事が早く終わる）。
///   叩かないと、5・6個目は 2個同時（プレイヤーの段と、別の段）
/// ・ブロックは遅いので、段を 1つ変えればよけられる
/// </summary>
public class NotionDragEvent : KitTimedEvent
{
    /// <summary>段の高さ（床から。ブロックのまん中）</summary>
    static readonly float[] Lanes = { 0.5f, 3.5f, 5.5f };
    static readonly Vector2 BlockSize = new Vector2(4f, 1f);
    const float Speed = 5f, Step = 2f, FirstWarn = 1f, Warn = 0.9f;
    const int MaxLive = 3, MaxLivePair = 4;
    const int ToggleWith = 3; // 4個目（0 から数えて 3）
    const float ToggleLife = 2.2f, ToggleSize = 1f;
    static readonly Vector2 ToggleSpot = new Vector2(0f, 5.8f);

    readonly int _stage;
    readonly List<KitMotionMover> _blocks = new List<KitMotionMover>();
    int _total, _spawned;
    float _next;
    bool _fromLeft;
    bool _missed;
    bool _toggleShown;
    KitMotionTarget _toggle;

    public NotionDragEvent(int stage) : base("Drag & drop")
    {
        _stage = stage;
    }

    protected override void Build()
    {
        _total = GroupBMotion.Scaled(6, _stage);
        _fromLeft = KitMotion.PlayerPos.x >= View.center.x; // 1個目は、プレイヤーから遠い方から
        _next = 0f;
        EndAt(_total * Step + 6f); // 念のための上限（全部出して、画面からいなくなったら自分で終わる）
    }

    protected override void OnFrame(float clock)
    {
        _blocks.RemoveAll(b => b == null || b.IsDone);
        if (_spawned >= _total)
        {
            if (_blocks.Count == 0) Stop();
            return;
        }
        if (clock < _next) return;
        if (_toggle != null && !_toggle.IsDone) return; // トグルの結果が出るまで待つ
        int want = _missed && _spawned >= ToggleWith + 1 ? Mathf.Min(2, _total - _spawned) : 1;
        if (_blocks.Count + want > (want == 2 ? MaxLivePair : MaxLive))
        {
            _next = clock + 0.25f; // 画面のブロックが減るまで待つ
            return;
        }
        int lane = PlayerLane();
        Launch(lane, _spawned == 0 ? FirstWarn : Warn);
        if (want == 2) Launch(lane == 0 ? 2 : lane == 2 ? 0 : (Random.value < 0.5f ? 0 : 2), Warn);
        _next = clock + Step;
    }

    /// <summary>プレイヤーの足の高さに近い段</summary>
    int PlayerLane()
    {
        float h = PlayerPos.y - Floor;
        return h < 2f ? 0 : h < 4.6f ? 1 : 2;
    }

    void Launch(int lane, float warn)
    {
        int index = _spawned;
        _spawned++;
        KitSide side = _fromLeft ? KitSide.Left : KitSide.Right;
        _fromLeft = !_fromLeft;
        float y = Floor + Lanes[Mathf.Clamp(lane, 0, Lanes.Length - 1)];
        Sprite sprite = NotionArt.Block(index);
        KitMotionMover m = GroupBMotion.SweepRect(Owner, side, y, Speed, BlockSize, sprite, Color.white, warn, objectName: "Notion_DragBlock");
        if (m == null) return;
        _blocks.Add(m);
        // うしろに、うすい青い影（ドラッグ中の影）
        if (m.Body != null)
        {
            SpriteRenderer shadow = AttackRunner.AddVisual(m.Body, sprite, Blue(0.3f), BlockSize, KitMotionItem.OrderBody - 1, "DragShadow");
            shadow.transform.localPosition = new Vector3(side == KitSide.Left ? -0.2f : 0.2f, -0.18f, 0f);
        }
        // 入る端に、青いドロップの線（縦）
        float edge = side == KitSide.Left ? View.xMin + 0.15f : View.xMax - 0.15f;
        KitMotionMark.Create(m.transform, PlaceholderSprites.Square, new Vector2(edge, y), new Vector2(0.12f, BlockSize.y + 0.6f), 0f, Blue(1f),
            KitMotionItem.OrderMark, m.Warn, "DropLine");
        if (index == ToggleWith && !_toggleShown) ShowToggle();
    }

    void ShowToggle()
    {
        _toggleShown = true;
        Vector2 at = new Vector2(View.center.x + ToggleSpot.x, Floor + ToggleSpot.y);
        GameAudio.Event("gm_notion_toggle_warn");
        _toggle = KitMotion.HitTarget(Owner, at, ToggleLife, Fold, () => _missed = true, NotionArt.ToggleMark(false), Color.white, ToggleSize, 1, null, 0.4f);
    }

    /// <summary>トグルを叩いた：「▼」に回って、画面のブロックが全部たたまれて消え、残りは出ない</summary>
    void Fold()
    {
        GameAudio.Event("gm_notion_toggle_close");
        Vector2 at = new Vector2(View.center.x + ToggleSpot.x, Floor + ToggleSpot.y);
        FxAnim.Sprite(NotionArt.ToggleMark(true), at, 60, 0.5f, Color.white, 2f, "Notion_ToggleOpen").Pop();
        foreach (KitMotionMover b in _blocks)
        {
            if (b == null || b.IsDone) continue;
            if (!b.Warning && b.Body != null)
                FxAnim.Sprite(NotionArt.Block(0), b.Body.position, KitMotionItem.OrderBody, 0.25f, new Color(1f, 1f, 1f, 0.8f), 1f, "Notion_Fold").Grow(0.1f);
            b.Cancel();
        }
        _blocks.Clear();
        Stop();
    }

    static Color Blue(float a)
    {
        Color c = NotionArt.Blue;
        c.a = a;
        return c;
    }
}
