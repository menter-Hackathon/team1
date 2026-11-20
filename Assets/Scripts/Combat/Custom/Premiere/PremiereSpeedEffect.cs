using UnityEngine;

/// <summary>
/// スピードデュレーションの本体（2026-11-18。docs/app-boss-skills-design.md 3-4）。PremiereSpeedDuration が出し、あとは自分で進む。
/// 場に残る技の効果（Lingers）。ダメージはない。
/// ・画面の上のほうに「速度・デュレーション」のウィンドウ（OnGUI）。数字が 100 % から 50 %／300 % へ回り、効果が始まると 0.4秒で閉じる
/// ・スローの場：出した瞬間のプレイヤーの位置に、半径3マスの円（予告は点線の枠だけ）。効果の間、中にいるプレイヤーの横の速さを 0.5倍にする
/// ・ボスの倍速：効果の間、ボスの移動の速さ 3倍・攻撃のあとの待ち時間 0.5倍。頭の上に ▶▶、後ろに赤っぽい残像
/// ・時間切れ・形態変化・正常化で消えるとき、Restore で倍率を 1 に戻す（変えた物だけ）
/// 2026-11-20（docs/premiere-edit-screen-design.md 6-4）：プログラムモニター（PremiereMonitor）があれば、数字が回る間、映像の右上に同じ印（スローは時計、
/// 倍速は ▶▶）を出し、その印がモニターからスローの場のまん中／ボスの頭の上へ飛ぶ（当たらない。着く瞬間に効果が始まる）。効果の間、映像も 0.5倍／3倍で流れる。
/// 場の大きさ・倍率・秒数は今のまま
/// </summary>
public class PremiereSpeedEffect : CustomEffect
{
    // ───────── 見た目【仮】 ─────────
    const int RingDotCount = 24;
    const float RingDotSize = 2f * PremiereArt.Pixel;
    const float RingSpin = -25f;  // 点線のふちは、ゆっくり逆回り（度/秒）
    const float HandSpin = -120f; // 時計の針（度/秒）
    const float FillAlpha = 0.2f;
    const int FillOrder = 5, RingOrder = 6, ClockOrder = 7, MarkOrder = 45;
    static readonly Color GhostPurple = new Color(0.6f, 0.6f, 1f, 0.45f);
    static readonly Color GhostRed = new Color(1f, 0.3f, 0.3f, 0.4f);

    // ───────── ウィンドウ（OnGUI。PixelGui の座標）【仮】 ─────────
    // 2026-11-19：Mac の暗いウィンドウにした（docs/mac-look-design.md 2-4 の 38）。左上に信号機、題名はまん中、角を 3ドット欠く。
    // 幅は、題名が信号機にかからない幅まで広げる（WindowW より小さくはしない）。閉じるときは赤い丸がへこんで、まん中へ縮む
    const float WindowW = 150f, WindowH = 66f, TitleH = 14f, WindowTop = 58f, OpenTime = 0.12f, DentTime = 0.05f;
    const string WindowTitle = "速度・デュレーション";

    AttackRunner _owner;
    PremiereSpeedDuration _config;
    bool _slow;
    float _start;
    float _effectAt;   // 効果が始まる時刻（＝ウィンドウが閉じ始める）
    float _windowEnd;
    float _end;        // 効果が切れる時刻
    bool _applied;     // 倍率を変えたか（戻すのは変えた物だけ）
    Vector2 _center;

    PlayerController _player;
    BossController _boss;

    Transform _ring;
    SpriteRenderer[] _ringDots;
    SpriteRenderer _fill;
    Transform _clock;
    Transform _hand;
    SpriteRenderer _mark;
    PremiereGhost[] _ghosts;
    // 2026-11-18：スキルの見た目（docs/app-skill-visuals-design.md 12-4）【仮】
    GenericFrameCanvas _stripes;      // スローの場の中の横じま（白 1割、2ドットおき。ゆっくり下へ流れる）
    SpriteRenderer _stripesSr;
    int _stripeOffset = -1;
    TextMesh _percent;                // 場のふちの大きいうすい 50% ／ ▶▶ の右の 300%
    readonly SpriteRenderer[] _speedLines = new SpriteRenderer[3];
    float _lastBossX = float.NaN;
    float _trailDir = -1f;            // 速さの線を出す向き（ボスの後ろ）

    public override bool Lingers => true;

    public static PremiereSpeedEffect Spawn(AttackRunner owner, PremiereSpeedDuration config, bool slow)
    {
        Vector2 center = owner.TargetPoint ?? owner.OriginPoint;
        GameObject go = owner.CreateSpawnedObject(slow ? "PremiereSlowField" : "PremiereBossBoost", slow ? center : (Vector2)owner.transform.position);
        var e = go.AddComponent<PremiereSpeedEffect>();
        e._owner = owner;
        e._config = config;
        e._slow = slow;
        e._center = center;
        e._start = Time.time;
        e._effectAt = e._start + config.WindowTime;
        e._windowEnd = e._effectAt + config.CloseTime;
        e._end = e._effectAt + (slow ? config.SlowDuration : config.BoostDuration);
        e._player = owner.Target != null ? owner.Target.GetComponentInParent<PlayerController>() : null;
        e._boss = owner.GetComponent<BossController>();
        if (slow) e.BuildField();
        else e.BuildBoost();
        PremiereMonitor monitor = PremiereMonitor.Current;
        if (monitor != null)
        {
            // 2026-11-20：モニターの右上に印 → 効果の場所へ飛ぶ（数字が回る 0.8秒）。効果の間、映像の速さも変わる
            Sprite icon = slow ? PremiereArt.ClockFace : PremiereArt.FastForwardMark;
            monitor.ShowIcon(icon, config.WindowTime);
            BossController boss = e._boss;
            System.Func<Vector2> to = slow ? (System.Func<Vector2>)(() => center)
                : () => boss != null ? new Vector2(boss.BodyBounds.center.x, boss.BodyBounds.max.y + 0.4f) : center;
            PremiereMonitorFlyer.Icon(go.transform, icon, monitor.VideoCenter, to, e._start, e._effectAt, 16);
            monitor.Speed(slow ? config.SlowMultiplier : config.BoostMoveMultiplier, e._end - e._start);
        }
        e.Tick(Time.time);
        return e;
    }

    void Update() => Tick(Time.time);

    void Tick(float now)
    {
        if (now >= Mathf.Max(_end, _windowEnd))
        {
            Destroy(gameObject); // Restore で倍率を戻す
            return;
        }
        bool active = now >= _effectAt && now < _end;
        if (_slow) TickSlow(now, active);
        else TickBoost(now, active);
    }

    // ───────── スローの場 ─────────

    void TickSlow(float now, bool active)
    {
        _ring.localRotation = Quaternion.Euler(0f, 0f, RingSpin * (now - _start));
        // 予告はうすい枠だけ（最後の0.3秒は点滅）。効果の間は濃い枠と中の色・時計
        bool previewBlink = !active && _effectAt - now < 0.3f && Mathf.FloorToInt(now / 0.06f) % 2 == 1;
        Color ringColor = PremiereArt.WithAlpha(PremiereArt.DissolveColor, active ? 0.9f : previewBlink ? 0.15f : 0.6f);
        foreach (var d in _ringDots) d.color = ringColor;
        _fill.enabled = active;
        _clock.gameObject.SetActive(active);
        UpdateStripes(now, active);
        _hand.localRotation = Quaternion.Euler(0f, 0f, HandSpin * (now - _start));

        bool inside = false;
        if (active && _player != null)
        {
            Vector2 body = _owner != null && _owner.TargetPoint is Vector2 tp ? tp : (Vector2)_player.transform.position;
            inside = Vector2.Distance(body, _center) <= _config.SlowRadius;
            _player.MoveSpeedMultiplier = inside ? _config.SlowMultiplier : 1f;
            _applied = true;
        }
        if (_ghosts != null)
        {
            foreach (var g in _ghosts) if (g != null) g.Visible = inside;
        }
    }

    // ───────── ボスの倍速 ─────────

    void TickBoost(float now, bool active)
    {
        if (active && !_applied && _boss != null && _boss.State == BossState.Fighting)
        {
            _boss.MoveSpeedMultiplier = _config.BoostMoveMultiplier;
            _boss.WaitMultiplier = _config.BoostWaitMultiplier;
            _applied = true;
        }
        if (_boss != null)
        {
            Bounds b = _boss.BodyBounds;
            transform.position = new Vector3(b.center.x, b.max.y + 0.4f, 0f);
        }
        // ▶▶：予告の間は点滅、効果の間は出したまま。効果が切れたら消す
        _mark.enabled = active || (now < _effectAt && Mathf.FloorToInt(now / 0.1f) % 2 == 0);
        UpdateBoostDeco(now, active);
        if (_ghosts != null)
        {
            foreach (var g in _ghosts) if (g != null) g.Visible = active;
        }
    }

    void UpdateBoostDeco(float now, bool active)
    {
        if (_percent != null && _percent.gameObject.activeSelf != active) _percent.gameObject.SetActive(active);
        if (_boss == null) return;
        Bounds b = _boss.BodyBounds;
        // 動いた向きの反対（後ろ）に線を出す
        if (!float.IsNaN(_lastBossX) && Mathf.Abs(b.center.x - _lastBossX) > 0.002f) _trailDir = b.center.x > _lastBossX ? -1f : 1f;
        _lastBossX = b.center.x;
        for (int i = 0; i < _speedLines.Length; i++)
        {
            SpriteRenderer l = _speedLines[i];
            if (l == null) continue;
            l.enabled = active;
            if (!active) continue;
            float len = 1f + i * 0.4f;
            float slide = Mathf.Repeat(now * 6f + i * 0.37f, 1f) * 0.5f;
            float x = b.center.x + _trailDir * (b.extents.x + 0.3f + len * 0.5f + slide);
            float y = b.center.y + (i - 1) * 0.45f;
            l.transform.position = new Vector3(x, y, 0f);
            Color c = l.color;
            c.a = 0.6f * (1f - slide * 1.6f);
            l.color = c;
        }
    }

    protected override void Restore()
    {
        if (!_applied) return;
        if (_slow)
        {
            if (_player != null) _player.MoveSpeedMultiplier = 1f;
        }
        else if (_boss != null)
        {
            _boss.MoveSpeedMultiplier = 1f;
            _boss.WaitMultiplier = 1f;
        }
    }

    // ───────── 見た目を作る ─────────

    void BuildField()
    {
        float r = _config.SlowRadius;
        _fill = AttackRunner.AddVisual(transform, PlaceholderSprites.Circle, PremiereArt.WithAlpha(PremiereArt.DissolveColor, FillAlpha), new Vector2(r * 2f, r * 2f), FillOrder, "Fill");
        _fill.enabled = false;

        _ring = PremiereArt.Child(transform, "Ring", Vector2.zero);
        _ringDots = new SpriteRenderer[RingDotCount];
        for (int i = 0; i < RingDotCount; i++)
        {
            float a = i * Mathf.PI * 2f / RingDotCount;
            _ringDots[i] = PremiereArt.Box(_ring, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, new Vector2(RingDotSize, RingDotSize), PremiereArt.DissolveColor, RingOrder, "Dot");
        }

        _clock = PremiereArt.Child(transform, "Clock", Vector2.zero);
        RuntimeSprites.Show(_clock, PremiereArt.ClockFace, Vector2.zero, ClockOrder, 1f, "Face");
        _hand = PremiereArt.Child(_clock, "Hand", Vector2.zero);
        PremiereArt.Box(_hand, new Vector2(0f, 2f * PremiereArt.Pixel), new Vector2(PremiereArt.Pixel, 4f * PremiereArt.Pixel), PremiereArt.DissolveLight, ClockOrder + 1, "Needle");
        _clock.gameObject.SetActive(false);

        // 中のプレイヤーに紫の残像を1枚
        if (_player != null) _ghosts = new[] { PremiereGhost.Create(transform, _player.transform, GhostPurple, 0.12f) };

        // 横じまと、大きいうすい 50%（12-4。2026-11-18）
        int d = Mathf.Clamp(Mathf.RoundToInt(r * 2f * 18f), 8, 160);
        _stripes = new GenericFrameCanvas(d, d, "SlowStripes");
        _stripesSr = _stripes.Show(transform, Vector2.zero, FillOrder, "Stripes");
        _stripesSr.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        _stripesSr.enabled = false;
        _percent = WorldText.Create(transform, "50%", 0.8f, new Color(1f, 1f, 1f, 0.2f), ClockOrder, true, TextAnchor.MiddleCenter, "Percent");
        _percent.transform.localPosition = new Vector3(r * 0.42f, -r * 0.62f, 0f);
        _percent.gameObject.SetActive(false);
    }

    void UpdateStripes(float now, bool active)
    {
        if (_stripes == null) return;
        _stripesSr.enabled = active;
        if (_percent != null && _percent.gameObject.activeSelf != active) _percent.gameObject.SetActive(active);
        if (!active) return;
        int offset = Mathf.FloorToInt((now - _effectAt) / 0.12f);
        if (offset == _stripeOffset) return;
        _stripeOffset = offset;
        _stripes.Clear();
        int d = _stripes.W;
        float rr = d * 0.5f;
        var c = new Color32(255, 255, 255, 26);
        for (int y = 0; y < d; y++)
        {
            if (((y - offset) % 3 + 3) % 3 != 0) continue; // 2ドットあけて1本
            float dy = y + 0.5f - rr;
            float half = Mathf.Sqrt(Mathf.Max(0f, rr * rr - dy * dy));
            for (int x = Mathf.CeilToInt(rr - half); x < Mathf.FloorToInt(rr + half); x++) _stripes.Set(x, y, c);
        }
        _stripes.Apply();
    }

    void BuildBoost()
    {
        _mark = RuntimeSprites.Show(transform, PremiereArt.FastForwardMark, Vector2.zero, MarkOrder, 1f, "FastForward");
        // ▶▶ の右に 300%、ボスの後ろに横の速さの線 3本（12-4。2026-11-18）
        _percent = WorldText.Create(transform, "300%", 0.4f, PremiereArt.FastForward, MarkOrder, true, TextAnchor.MiddleLeft, "Percent");
        _percent.transform.localPosition = new Vector3(0.35f, 0f, 0f);
        for (int i = 0; i < 3; i++)
        {
            _speedLines[i] = PremiereArt.Box(transform, Vector2.zero, new Vector2(1f + i * 0.4f, PremiereArt.Pixel), new Color(1f, 1f, 1f, 0.6f), MarkOrder - 1, "SpeedLine");
            _speedLines[i].enabled = false;
        }
        // ボスの後ろに、赤っぽい残像を2枚
        if (_boss != null)
        {
            Color far = GhostRed;
            far.a *= 0.55f;
            _ghosts = new[]
            {
                PremiereGhost.Create(transform, _boss.transform, GhostRed, 0.08f),
                PremiereGhost.Create(transform, _boss.transform, far, 0.16f),
            };
        }
    }

    // ───────── ウィンドウ ─────────

    void OnGUI()
    {
        GUI.depth = 5; // 技の表示（HUD より手前）
        if (Event.current.type != EventType.Repaint) return;
        float now = Time.time;
        if (now >= _windowEnd || _config == null) return;
        GameSession session = GameSession.Instance;
        if (session != null && (session.IsTransitioning || session.IsGameOver)) return;
        PixelGui.Begin(false);
        DrawWindow(now);
        PixelGui.End();
    }

    void DrawWindow(float now)
    {
        float open = Mathf.Clamp01((now - _start) / OpenTime);
        // 閉じる：はじめの 0.05秒は赤い丸がへこむだけ。そのあと、まん中へ縮む（閉じる秒数の合計は今のまま）
        bool closing = now >= _effectAt;
        float shrinkTime = Mathf.Max(0.01f, _config.CloseTime - DentTime);
        float close = closing ? Mathf.Clamp01((now - _effectAt - DentTime) / shrinkTime) : 0f;
        // 題名（左右に信号機の分をあける）が入る幅
        float width = Mathf.Max(WindowW, GenericMac.Gui.TitleWidth(WindowTitle, true) + 64f);
        float sx = 1f - close, sy = Mathf.Min(open, 1f - close);
        float w = Mathf.Round(width * sx), h = Mathf.Round(WindowH * sy);
        if (h < 2f || w < 4f) return;
        float cx = PixelGui.Width * 0.5f, cy = WindowTop + WindowH * 0.5f;
        var r = new Rect(Mathf.Round(cx - w * 0.5f), Mathf.Round(cy - h * 0.5f), w, h);
        if (h < WindowH || w < width)
        {
            // 開く・閉じる途中は、角の丸い箱だけ
            GenericMac.Gui.RoundFill(r, 3, 3, GenericMac.DarkEdge);
            GenericMac.Gui.RoundFill(PixelGui.Inset(r, 1f), 2, 2, GenericMac.DarkBg);
            return;
        }

        Rect body = GenericMac.Gui.Window(r, WindowTitle, true, TitleH, 3, true, closing ? 0 : -1);

        // 「速度： 50 %」（数字だけ青）
        string label = "速度： ", num = Value(now).ToString(), unit = " %";
        GUIStyle style = PixelGui.Style(false, 12);
        float lw = Mathf.Ceil(PixelGui.Measure(label, style));
        float nw = Mathf.Ceil(PixelGui.Measure(num, style));
        float uw = Mathf.Ceil(PixelGui.Measure(unit, style));
        float bx = Mathf.Round(r.center.x - (lw + nw + uw) * 0.5f);
        float by = Mathf.Round(body.y + (body.height - 14f) * 0.5f);
        PixelGui.Text(new Rect(bx, by, lw + 2f, 14f), label, 12, Color.white);
        PixelGui.Text(new Rect(bx + lw, by, nw + 2f, 14f), num, 12, PremiereArt.PlayheadBlue);
        PixelGui.Text(new Rect(bx + lw + nw, by, uw + 2f, 14f), unit, 12, Color.white);
    }

    /// <summary>ウィンドウの数字。100 % から、50 %（スロー）か 300 %（倍速）へ回る</summary>
    int Value(float now)
    {
        int target = _slow ? 50 : 300;
        float t = _config.WindowTime > 0f ? Mathf.Clamp01((now - _start) / _config.WindowTime) : 1f;
        float ease = 1f - (1f - t) * (1f - t);
        return Mathf.RoundToInt(Mathf.Lerp(100f, target, ease));
    }
}
