using UnityEngine;

/// <summary>
/// スピードデュレーションの本体（2026-11-18。docs/app-boss-skills-design.md 3-4）。PremiereSpeedDuration が出し、あとは自分で進む。
/// 場に残る技の効果（Lingers）。ダメージはない。
/// ・画面の上のほうに「速度・デュレーション」のウィンドウ（OnGUI）。数字が 100 % から 50 %／300 % へ回り、効果が始まると 0.4秒で閉じる
/// ・スローの場：出した瞬間のプレイヤーの位置に、半径3マスの円（予告は点線の枠だけ）。効果の間、中にいるプレイヤーの横の速さを 0.5倍にする
/// ・ボスの倍速：効果の間、ボスの移動の速さ 3倍・攻撃のあとの待ち時間 0.5倍。頭の上に ▶▶、後ろに赤っぽい残像
/// ・時間切れ・形態変化・正常化で消えるとき、Restore で倍率を 1 に戻す（変えた物だけ）
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
    const float WindowW = 150f, WindowH = 66f, TitleH = 14f, WindowTop = 58f, OpenTime = 0.12f;

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
        if (_ghosts != null)
        {
            foreach (var g in _ghosts) if (g != null) g.Visible = active;
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
    }

    void BuildBoost()
    {
        _mark = RuntimeSprites.Show(transform, PremiereArt.FastForwardMark, Vector2.zero, MarkOrder, 1f, "FastForward");
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
        float close = now < _effectAt ? 0f : Mathf.Clamp01((now - _effectAt) / Mathf.Max(0.01f, _config.CloseTime));
        float h = Mathf.Round(WindowH * Mathf.Min(open, 1f - close));
        if (h < 2f) return;
        float x = Mathf.Round(PixelGui.Width * 0.5f - WindowW * 0.5f);
        var r = new Rect(x, Mathf.Round(WindowTop + (WindowH - h) * 0.5f), WindowW, h);
        PixelGui.Fill(r, PremiereArt.WindowBorder);
        PixelGui.Fill(PixelGui.Inset(r, 1f), PremiereArt.WindowBg);
        if (h < WindowH) return; // 開く・閉じる途中は箱だけ

        var title = new Rect(r.x + 1f, r.y + 1f, r.width - 2f, TitleH);
        PixelGui.Fill(title, PremiereArt.WindowTitle);
        PixelGui.Text(new Rect(title.x + 5f, title.y, title.width - 10f, TitleH), "速度・デュレーション", 12, new Color(0.92f, 0.92f, 0.92f));

        // 「速度： 50 %」（数字だけ青）
        string label = "速度： ", num = Value(now).ToString(), unit = " %";
        GUIStyle style = PixelGui.Style(false, 12);
        float lw = Mathf.Ceil(PixelGui.Measure(label, style));
        float nw = Mathf.Ceil(PixelGui.Measure(num, style));
        float uw = Mathf.Ceil(PixelGui.Measure(unit, style));
        float bx = Mathf.Round(r.center.x - (lw + nw + uw) * 0.5f);
        float by = Mathf.Round(title.yMax + (r.yMax - title.yMax - 14f) * 0.5f);
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
