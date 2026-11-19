using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>【仮】行のブレークポイントの秒数（2026-11-19。docs/app-feedback-v2-design.md 3-1）。ボスの技とステージの出来事（3-4）で同じ物を使う</summary>
[Serializable]
public class VSCodeLineSettings
{
    [Tooltip("【仮】予告の秒数（赤丸が付いてから判定まで。矢印が赤丸に着くまで）")]
    public float Telegraph = 1f;
    [Tooltip("【仮】第2形態の、行と行のずれ（秒）")]
    public float Stagger = 0.7f;
    [Tooltip("【仮】判定の秒数（ジャンプで床から 1.0マスより上にいる時間は 約0.55秒なので 0.2秒）")]
    public float HitTime = 0.2f;
    [Tooltip("【仮】終わり（帯が左右から閉じる）の秒数")]
    public float CloseTime = 0.3f;
    [Tooltip("【仮】判定の前に帯が点滅する秒数")]
    public float BlinkTime = 0.3f;
    [Tooltip("【仮】デバッグの操作バーを出す秒数")]
    public float BarTime = 0.3f;
}

/// <summary>
/// 【仮】行のブレークポイントの進行（2026-11-19。docs/app-feedback-v2-design.md 3-1・3-4）。VSCodeBreakpointLine・VSCodeKit が出す。
/// 行ごとに「赤丸がポン → うすい赤の帯と点線・行番号 → 黄色い矢印が赤丸へ進む → 着いた瞬間に黄色く光って当たる（0.2秒）→ 左右から閉じる」。
/// 出した物は全部この GameObject の子（形態変化・正常化・出来事の終わりで、いっしょに消える）
/// </summary>
public class VSCodeBreakpointLineEffect : CustomEffect
{
    /// <summary>行1つ分</summary>
    public struct Row
    {
        /// <summary>行の下の端（ワールドの y）</summary>
        public float Bottom;
        /// <summary>行の高さ（マス）</summary>
        public float Height;
        /// <summary>判定の時刻（出してから何秒）</summary>
        public float HitAt;
        /// <summary>行番号（飾り）</summary>
        public int LineNo;
    }

    class Live
    {
        public Row Row;
        public Transform Root;
        public SpriteRenderer Band, Dot, Number, Arrow, EdgeTop, EdgeBottom;
        public SpriteRenderer DotTop, DotBottom;
        public Hitbox Hit;
        public bool Hitting, Done, BarShown;
        public float ArrowFrom, ArrowTo;
    }

    const int BandOrder = 6;     // 床の上の予告・場（キャラクターより後ろ）
    const int EdgeOrder = 7;
    const int DecoOrder = 41;    // 赤丸・行番号・矢印
    const int BarOrder = 43;

    AttackRunner _owner;
    AttackData _data;
    VSCodeLineSettings _cfg;
    Rect _room;
    bool _dotLeft;
    bool _showBar;
    float _start;
    readonly List<Live> _rows = new List<Live>();

    /// <summary>
    /// 行のブレークポイントを出す。room＝部屋（横いっぱいの範囲）、rows＝行（HitAt は出してからの秒）、
    /// dotLeft＝赤丸を左の壁ぎわに出す（false なら右。左右を逆にした絵）、showBar＝判定のときにデバッグの操作バーを出す
    /// </summary>
    public static VSCodeBreakpointLineEffect Create(AttackRunner owner, AttackData data, VSCodeLineSettings cfg, Rect room, List<Row> rows, bool dotLeft, bool showBar)
    {
        if (owner == null || data == null || cfg == null || rows == null || rows.Count == 0) return null;
        GameObject go = owner.CreateSpawnedObject("VSCodeBreakpointLine", new Vector2(room.center.x, room.yMin));
        var fx = go.AddComponent<VSCodeBreakpointLineEffect>();
        fx._owner = owner;
        fx._data = data;
        fx._cfg = cfg;
        fx._room = room;
        fx._dotLeft = dotLeft;
        fx._showBar = showBar;
        fx._start = Time.time;
        foreach (Row r in rows) fx._rows.Add(fx.Build(r));
        return fx;
    }

    Live Build(Row row)
    {
        float d = BrowserDevFx.Dot;
        float w = _room.width;
        float h = row.Height;
        var root = new GameObject($"Line{row.LineNo}").transform;
        root.gameObject.layer = gameObject.layer;
        root.SetParent(transform, false);
        root.position = new Vector3(_room.center.x, row.Bottom + h * 0.5f, 0f);
        var live = new Live { Row = row, Root = root };

        // 帯（うすい赤）と、上下のふちの赤い点線
        live.Band = BrowserDevFx.Rect(root, Vector2.zero, new Vector2(w, h), BrowserDevFx.WithAlpha(DevArt.ErrorRed, 0.15f), BandOrder, "Band");
        live.DotTop = BrowserDevFx.DottedLine(root, new Vector2(0f, h * 0.5f - d * 0.5f), w, 1, 0f, DevArt.ErrorRed, EdgeOrder);
        live.DotBottom = BrowserDevFx.DottedLine(root, new Vector2(0f, -h * 0.5f + d * 0.5f), w, 1, 0f, DevArt.ErrorRed, EdgeOrder);
        // 判定のときの、上下の赤いふち（実線）
        live.EdgeTop = BrowserDevFx.Rect(root, new Vector2(0f, h * 0.5f - d), new Vector2(w, 2f * d), DevArt.ErrorRed, EdgeOrder, "EdgeTop");
        live.EdgeBottom = BrowserDevFx.Rect(root, new Vector2(0f, -h * 0.5f + d), new Vector2(w, 2f * d), DevArt.ErrorRed, EdgeOrder, "EdgeBottom");
        live.EdgeTop.enabled = live.EdgeBottom.enabled = false;

        // 赤丸（壁ぎわ）と、その内側に灰色の行番号
        float side = _dotLeft ? -1f : 1f;
        float dotX = side * (w * 0.5f - 0.45f);
        live.Dot = BrowserDevFx.Show(root, DevArt.BreakpointDotBig(), new Vector2(dotX, 0f), DecoOrder, 1f, "Dot");
        live.Dot.transform.localScale = Vector3.zero;
        live.Number = BrowserDevFx.Show(root, BrowserDevFx.Text(row.LineNo.ToString(), DevArt.LineNo, true), new Vector2(dotX - side * 0.6f, 0f), DecoOrder, 1f, "LineNo");
        // 黄色い矢印：反対の端から、赤丸へ
        live.ArrowFrom = -dotX;
        live.ArrowTo = dotX - side * 0.05f;
        live.Arrow = BrowserDevFx.Show(root, DevArt.RightArrow(9), new Vector2(live.ArrowFrom, 0f), DecoOrder + 1, 1f, "Arrow");
        live.Arrow.flipX = _dotLeft; // 進む向き（赤丸のほう）を指す

        // 判定（行の全部）
        var hit = new GameObject("Hit");
        hit.layer = gameObject.layer;
        hit.transform.SetParent(root, false);
        var rb = hit.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var box = hit.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(w, h);
        live.Hit = _owner.AddHitbox(hit, _data, box, true);

        SetVisible(live, false);
        return live;
    }

    static void SetVisible(Live l, bool on)
    {
        l.Band.enabled = on;
        l.DotTop.enabled = l.DotBottom.enabled = on;
        l.Dot.enabled = l.Number.enabled = l.Arrow.enabled = on;
    }

    void Update()
    {
        float now = Time.time;
        float t = now - _start;
        bool blink = Mathf.FloorToInt(now / 0.06f) % 2 == 0;
        bool allDone = true;
        foreach (Live l in _rows)
        {
            if (l.Done) continue;
            allDone = false;
            float teleAt = l.Row.HitAt - _cfg.Telegraph;
            if (t < teleAt) continue;
            float hitEnd = l.Row.HitAt + _cfg.HitTime;
            float closeEnd = hitEnd + _cfg.CloseTime;

            if (t < l.Row.HitAt)
            {
                // 予告：赤丸がポン、矢印が赤丸へ進む。最後の 0.3秒は帯が点滅
                SetVisible(l, true);
                float e = t - teleAt;
                float s = BrowserDevFx.Pop(e, 0.1f);
                l.Dot.transform.localScale = new Vector3(s, s, 1f);
                float k = Mathf.Clamp01(e / Mathf.Max(0.05f, _cfg.Telegraph));
                l.Arrow.transform.localPosition = new Vector3(Mathf.Lerp(l.ArrowFrom, l.ArrowTo, k), 0f, 0f);
                bool lastBlink = l.Row.HitAt - t <= _cfg.BlinkTime;
                float a = lastBlink ? (blink ? 0.3f : 0.08f) : 0.15f;
                l.Band.color = BrowserDevFx.WithAlpha(DevArt.ErrorRed, a);
                continue;
            }
            if (t < hitEnd)
            {
                // 判定：行の全部が止まった行の黄色（6割）に光る。上下のふちは赤
                if (!l.Hitting)
                {
                    l.Hitting = true;
                    SetVisible(l, true);
                    l.Dot.transform.localScale = Vector3.one;
                    l.Arrow.transform.localPosition = new Vector3(l.ArrowTo, 0f, 0f);
                    l.DotTop.enabled = l.DotBottom.enabled = false;
                    l.EdgeTop.enabled = l.EdgeBottom.enabled = true;
                    l.Band.color = BrowserDevFx.WithAlpha(DevArt.Stopped, 0.6f);
                    l.Hit.Activate();
                    if (_showBar && !l.BarShown)
                    {
                        l.BarShown = true;
                        ShowBar();
                    }
                }
                continue;
            }
            if (l.Hitting)
            {
                l.Hitting = false;
                l.Hit.Deactivate();
                l.EdgeTop.enabled = l.EdgeBottom.enabled = false;
                l.Arrow.enabled = false;
                l.Number.enabled = false;
            }
            if (t < closeEnd)
            {
                // 終わり：帯が左右から閉じる。赤丸が消える
                float k = Mathf.Clamp01((t - hitEnd) / Mathf.Max(0.05f, _cfg.CloseTime));
                l.Band.transform.localScale = new Vector3(_room.width * (1f - k) / BrowserDevFx.Dot, l.Row.Height / BrowserDevFx.Dot, 1f);
                l.Band.color = BrowserDevFx.WithAlpha(DevArt.Stopped, 0.6f * (1f - k * 0.5f));
                float ds = 1f - k;
                l.Dot.transform.localScale = new Vector3(ds, ds, 1f);
                continue;
            }
            l.Done = true;
            if (l.Root != null) l.Root.gameObject.SetActive(false);
        }
        if (allDone) Destroy(gameObject);
    }

    /// <summary>部屋の上のまん中に、デバッグの操作バー（今ある絵）を 0.3秒</summary>
    void ShowBar()
    {
        Rect view = BrowserDevFx.View();
        float top = Mathf.Min(_room.yMax, view.yMax - 1.9f); // 上の HUD の下
        var go = new GameObject("DebugBar");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector2(_room.center.x, top - 0.35f);
        SpriteRenderer bar = BrowserDevFx.Show(go.transform, DevArt.DebugBar(), Vector2.zero, BarOrder, 1f, "Bar");
        BrowserDevFx.Animate(go, _cfg.BarTime, (e, k) =>
        {
            float s = BrowserDevFx.Pop(e, 0.08f);
            go.transform.localScale = new Vector3(s, s, 1f);
            bar.color = new Color(1f, 1f, 1f, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
        });
    }
}
