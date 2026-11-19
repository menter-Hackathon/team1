using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Photoshop のステージの決まり：レイヤー（2026-11-19。docs/app-kits-2-design.md 3-1）。
/// ・右下に小さいレイヤーのパネル（Enemies・Platforms・Background。左に目のアイコン）。ボス戦は Boss の行が増える
/// ・12秒おきに、どれか1行の目が閉じる（0.5秒点滅してから）。4秒で戻る（Boss は 3秒）
///   Enemies：雑魚が点線の輪かくだけ／Platforms：足場が点線の輪かくだけ（乗れるのは同じ）／Background：背景が市松もよう／Boss：ボスが点線の輪かくだけ
/// ・プレイヤー・危ない物・予告・弾は、絶対に隠さない。見た目だけで、当たり判定は変えない
/// ・片づけ：ボスの登場・形態変化（50％）・100％・やり直しで、全部元に戻す（ボスの色は BossController.SetVisibility で戻す）
/// </summary>
public class PhotoshopRule : StageRule
{
    /// <summary>【仮】目が閉じる間隔・閉じている秒数</summary>
    const float Interval = 12f, FirstDelay = 6f, HideTime = 4f, BossHideTime = 3f, BlinkTime = 0.5f;
    const float HiddenAlpha = 0.1f;

    internal enum Row { Boss, Enemies, Platforms, Background }

    enum State { Idle, Blink, Hidden }

    State _state = State.Idle;
    Row _row;
    float _timer, _until, _reapplyAt;
    PhotoshopLayersPanel _panel;
    BossController _boss;
    readonly Dictionary<SpriteRenderer, Color> _saved = new Dictionary<SpriteRenderer, Color>();
    readonly HashSet<Object> _outlined = new HashSet<Object>();
    readonly List<GameObject> _outlines = new List<GameObject>();
    GameObject _checker;
    bool _bossHidden;

    public override void Begin(StageRuleContext ctx)
    {
        _timer = FirstDelay;
        var go = new GameObject("PhotoshopLayers");
        _panel = go.AddComponent<PhotoshopLayersPanel>();
        _panel.Rule = this;
        ctx.TrackDecor(go);
    }

    internal bool InBossFight => Ctx != null && Ctx.InBossFight;
    internal bool IsClosed(Row r) => _state == State.Hidden && _row == r;
    internal bool IsBlinking(Row r) => _state == State.Blink && _row == r;

    public override void Tick(float dt)
    {
        float now = Time.time;
        switch (_state)
        {
            case State.Idle:
                _timer -= dt;
                if (_timer > 0f) return;
                _row = Pick();
                _state = State.Blink;
                _until = now + BlinkTime;
                GameAudio.Event("gm_photoshop_eye_blink");
                break;
            case State.Blink:
                if (now < _until) return;
                _state = State.Hidden;
                _until = now + (_row == Row.Boss ? BossHideTime : HideTime);
                _reapplyAt = 0f;
                GameAudio.Event("gm_photoshop_eye_close", _row == Row.Boss ? 0.8f : 1f);
                break;
            case State.Hidden:
                if (now >= _reapplyAt) { _reapplyAt = now + 0.25f; Apply(_row); }
                if (_row == Row.Background) FollowChecker();
                if (now >= _until) { RestoreAll(); GameAudio.Event("gm_photoshop_eye_open"); _state = State.Idle; _timer = Interval; }
                break;
        }
    }

    Row Pick()
    {
        var list = new List<Row> { Row.Enemies, Row.Platforms, Row.Background };
        if (InBossFight && _boss != null) { list.Remove(Row.Enemies); list.Add(Row.Boss); list.Add(Row.Boss); }
        return list[Random.Range(0, list.Count)];
    }

    public override void OnPhase(PacePhase phase)
    {
        if (phase == PacePhase.Done) ResetAll();
    }

    public override void OnBossAppeared(BossController boss)
    {
        ResetAll();
        _boss = boss;
        if (boss != null) boss.TransitionStarted += OnTransition;
    }

    void OnTransition() => ResetAll();

    public override void End()
    {
        ResetAll();
        if (_boss != null) _boss.TransitionStarted -= OnTransition;
        _boss = null;
    }

    void ResetAll()
    {
        RestoreAll();
        _state = State.Idle;
        _timer = Mathf.Max(_timer, 4f);
    }

    // ───────── 隠す ─────────

    void Apply(Row row)
    {
        switch (row)
        {
            case Row.Enemies: HideEnemies(); break;
            case Row.Platforms: HidePlatforms(); break;
            case Row.Background: ShowChecker(); break;
            case Row.Boss: HideBoss(); break;
        }
    }

    void HideEnemies()
    {
        int enemy = LayerMask.NameToLayer("Enemy");
        foreach (var h in Object.FindObjectsOfType<Health>())
        {
            if (h == null || h.IsDead || h.Team != Team.Enemy || h.gameObject.layer != enemy) continue;
            if (h.GetComponentInParent<BossController>() != null) continue;
            Fade(h.GetComponentsInChildren<SpriteRenderer>(), h.transform);
        }
    }

    void HidePlatforms()
    {
        Rect a = Ctx.PlayArea;
        var cols = Physics2D.OverlapAreaAll(a.min - Vector2.one, a.max + Vector2.one, LayerMask.GetMask("OneWayPlatform"));
        foreach (var c in cols)
        {
            if (c == null || c.isTrigger) continue;
            SpriteRenderer[] srs = c.GetComponentsInChildren<SpriteRenderer>();
            if (srs.Length == 0 && c.transform.parent != null) srs = c.transform.parent.GetComponentsInChildren<SpriteRenderer>();
            if (_outlined.Add(c))
            {
                var holder = new GameObject("PsPlatformAnts");
                holder.transform.position = Vector3.zero;
                Bounds b = c.bounds;
                PhotoshopArt.AntsRect(holder.transform, new Rect(b.min.x, b.min.y, b.size.x, Mathf.Max(0.2f, b.size.y)), 30);
                _outlines.Add(holder);
            }
            Fade(srs, null);
        }
    }

    void HideBoss()
    {
        if (_boss == null || _bossHidden) return;
        _bossHidden = true;
        _boss.SetVisibility(0.12f);
        var holder = new GameObject("PsBossAnts");
        holder.transform.position = Vector3.zero;
        Bounds b = BossVisualBounds(_boss);
        PhotoshopArt.AntsRect(holder.transform, new Rect(b.min.x, b.min.y, b.size.x, b.size.y), 30);
        holder.transform.SetParent(_boss.transform, true); // 大きさを保ったまま、ボスと一緒に動かす
        _outlines.Add(holder);
    }

    /// <summary>ボスの見た目の範囲（絵を全部合わせた物）。絵が無ければ被弾範囲</summary>
    static Bounds BossVisualBounds(BossController boss)
    {
        bool any = false;
        Bounds b = default;
        foreach (var sr in boss.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr == null || !sr.enabled || sr.sprite == null) continue;
            if (!any) { b = sr.bounds; any = true; }
            else b.Encapsulate(sr.bounds);
        }
        return any ? b : boss.BodyBounds;
    }

    /// <summary>絵をうすくして、まわりに流れる点線（outlineParent があれば、その子に付けて一緒に動かす）</summary>
    void Fade(SpriteRenderer[] srs, Transform outlineParent)
    {
        bool any = false;
        Bounds total = default;
        foreach (var sr in srs)
        {
            if (sr == null || !sr.enabled) continue;
            if (!_saved.ContainsKey(sr)) _saved[sr] = sr.color;
            Color c = _saved[sr];
            c.a *= HiddenAlpha;
            sr.color = c;
            if (!any) { total = sr.bounds; any = true; }
            else total.Encapsulate(sr.bounds);
        }
        if (!any || outlineParent == null || !_outlined.Add(outlineParent)) return;
        var holder = new GameObject("PsAnts");
        holder.transform.position = Vector3.zero;
        PhotoshopArt.AntsRect(holder.transform, new Rect(total.min.x, total.min.y, total.size.x, total.size.y), 30);
        holder.transform.SetParent(outlineParent, true);
        _outlines.Add(holder);
    }

    void ShowChecker()
    {
        if (_checker != null) return;
        _checker = new GameObject("PsCheckerBackground");
        var sr = _checker.AddComponent<SpriteRenderer>();
        sr.sprite = PhotoshopArt.CheckerTile;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.sortingOrder = -95;
        sr.color = new Color(1f, 1f, 1f, 0.92f);
        FollowChecker();
    }

    void FollowChecker()
    {
        if (_checker == null) return;
        Rect v = ScreenUi.ViewRect();
        _checker.transform.position = new Vector3(v.center.x, v.center.y, 0f);
        var sr = _checker.GetComponent<SpriteRenderer>();
        sr.size = v.size + new Vector2(2f, 2f);
    }

    void RestoreAll()
    {
        foreach (var kv in _saved) if (kv.Key != null) kv.Key.color = kv.Value;
        _saved.Clear();
        foreach (var o in _outlines) if (o != null) Object.Destroy(o);
        _outlines.Clear();
        _outlined.Clear();
        if (_checker != null) Object.Destroy(_checker);
        _checker = null;
        if (_bossHidden && _boss != null) _boss.SetVisibility(1f);
        _bossHidden = false;
    }
}

/// <summary>右下のレイヤーのパネル（OnGUI）。PhotoshopRule の状態を描くだけ</summary>
public class PhotoshopLayersPanel : MonoBehaviour
{
    internal PhotoshopRule Rule;

    void OnGUI()
    {
        if (Rule == null || Event.current.type != EventType.Repaint) return;
        GameSession session = GameSession.Instance;
        if (session != null && (session.IsTransitioning || session.IsGameOver)) return;
        GUI.depth = 6;
        PixelGui.Begin(false);
        Draw();
        PixelGui.End();
    }

    void Draw()
    {
        var rows = new List<(PhotoshopRule.Row row, string name)>();
        if (Rule.InBossFight) rows.Add((PhotoshopRule.Row.Boss, "Boss"));
        rows.Add((PhotoshopRule.Row.Enemies, "Enemies"));
        rows.Add((PhotoshopRule.Row.Platforms, "Platforms"));
        rows.Add((PhotoshopRule.Row.Background, "Background"));
        const float w = 124f, header = 11f, rowH = 13f, margin = 6f;
        float h = header + rows.Count * rowH + 2f;
        var r = new Rect(Mathf.Round(PixelGui.Width - w - margin), Mathf.Round(PixelGui.Height - h - margin), w, h);
        MacUi.Gui.RoundFill(r, 2, new Color(PhotoshopArt.Panel.r, PhotoshopArt.Panel.g, PhotoshopArt.Panel.b, 0.94f));
        MacUi.Gui.RoundEdge(r, 2, PhotoshopArt.PanelLine);
        PixelGui.TextEn(new Rect(r.x + 5f, r.y + 2f, w - 10f, 8f), "Layers", 8, new Color(0.85f, 0.85f, 0.85f, 1f));
        PixelGui.Fill(new Rect(r.x + 1f, r.y + header, w - 2f, 1f), PhotoshopArt.PanelLine);
        for (int i = 0; i < rows.Count; i++)
        {
            var (row, name) = rows[i];
            var rr = new Rect(r.x + 1f, r.y + header + 1f + i * rowH, w - 2f, rowH - 1f);
            bool closed = Rule.IsClosed(row), blink = Rule.IsBlinking(row);
            if (closed || blink) PixelGui.Fill(rr, new Color(PhotoshopArt.Blue.r * 0.55f, PhotoshopArt.Blue.g * 0.55f, PhotoshopArt.Blue.b * 0.6f, 1f));
            else PixelGui.Fill(rr, PhotoshopArt.PanelRow);
            PixelGui.Fill(new Rect(rr.x, rr.yMax, rr.width, 1f), PhotoshopArt.PanelLine);
            // 目
            bool shut = closed || (blink && Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0);
            Sprite eye = shut ? CreativeArt.EyeClosed : CreativeArt.EyeOpen;
            PixelGui.DrawSprite(new Rect(rr.x + 3f, rr.y + 3f, 7f, 5f), eye, shut ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white);
            PixelGui.Fill(new Rect(rr.x + 13f, rr.y, 1f, rr.height), PhotoshopArt.PanelLine);
            // 小さい絵（レイヤーのサムネイル）
            var th = new Rect(rr.x + 16f, rr.y + 2f, 11f, rr.height - 4f);
            PixelGui.Fill(th, row == PhotoshopRule.Row.Background ? new Color(0.8f, 0.8f, 0.8f, 1f) : Color.white);
            PixelGui.Fill(new Rect(th.x + 3f, th.y + 2f, 5f, th.height - 4f), row == PhotoshopRule.Row.Boss ? MacUi.Red : row == PhotoshopRule.Row.Enemies ? MacUi.Gray3 : row == PhotoshopRule.Row.Platforms ? PhotoshopArt.Blue : new Color(0.65f, 0.65f, 0.65f, 1f));
            PixelGui.TextEn(new Rect(th.xMax + 4f, rr.y + 2f, rr.xMax - th.xMax - 6f, 8f), name, 8, closed ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white);
        }
    }
}
