using System.Collections;
using UnityEngine;

/// <summary>
/// Spotlight・サーチ（2026-11-18。docs/app-boss-skills-design.md 2-1）の、場に出る物。FinderSpotlightAttack が出す。
/// 出したあとは自分で進む。見た目と値は【仮】。
///
/// 流れ（第1形態 2.3秒＋引っ込む 0.2秒）
/// 1. 検索バーが上から出る（0.3秒）
/// 2. PLAYER を1文字ずつ打つ（1.2秒）。検索バーからプレイヤーへ水色の点線、足元に水色の薄い円。プレイヤーを追う
/// 3. 検索完了（0.5秒）：検索バーが白く光り、下に結果の行（プレイヤーの絵）が1行出る。線と円が赤に変わり、その場に止まる
/// 4. 発射（0.3秒）：検索バーの下の端から円の中心へ光の帯。判定は帯（太さ 0.8）と円（直径 1.6）、0.15秒
/// 5. 検索バーが上へ引っ込む（0.2秒）
/// 第2形態は、4 のあと「水色で追う 0.4秒 → 赤で止まる 0.5秒 → 発射 0.3秒」をあと2回（結果の行が1行ずつ増える）。
///
/// ・検索バーは OnGUI（画面の上。GUI.depth＝5）。位置はゲームの世界で決めて、画面の位置に直す（ボス戦はカメラが動かない）
/// ・線・円・光の帯は、ゲームの世界に描く
/// ・撃つ前にボスの攻撃が止められたら（形態変化・正常化など）、撃たずに消える。地形やプレイヤーは変えないので、戻す物はない
/// </summary>
public class FinderSpotlightEffect : CustomEffect
{
    // ───────── 見た目【仮】 ─────────
    const int CircleOrder = 61, LineOrder = 62, BeamOrder = 64, FlashOrder = 65;
    const float BeamOuterDots = 14f;     // 光の帯の外側の太さ
    const float BeamCoreDots = 6f;       // 光の帯の芯の太さ
    const float SlideCells = 1.5f;       // 出る・引っ込むときに上下へ動く距離（マス）
    const float BarFlashTime = 0.25f;    // 検索完了で白く光る時間
    const float FloorFlashTime = 0.15f;  // 当たった床の白い光
    const float BlinkInterval = 0.1f;    // 止まった円の点滅
    const float RowHeightCells = 0.9f;   // 結果の行の高さ（マス）
    const float RowGap = 2f;             // 結果の行の間（画面のドット）
    const int TextSize = 16;             // 検索バーの文字（Press Start 2P は 8 の倍数）

    enum Step { Appear, Track, Lock, Fire, Retract }

    AttackRunner _owner;
    AttackData _data;
    FinderSpotlightAttack _attack;
    Step _step = Step.Appear;
    float _stepStart;
    float _typeStart = -1f;              // 打ち始めた時刻（まだなら -1）
    int _rows;                           // 結果の行の数
    float _barFlashUntil = float.NegativeInfinity;
    Vector2 _aim;                        // 円の中心
    FinderLine _line;
    Transform _circle;
    SpriteRenderer _circleFill, _circleRing;
    int _circleDots;
    // 2026-11-18：スキルの見た目（docs/app-skill-visuals-design.md 11-1）【仮】
    bool _hitThisShot;                   // 今の回の光の帯が当たったか
    float _noResultsUntil = float.NegativeInfinity; // 外れたとき、検索バーの字を No Results にする時刻まで
    const float NoResultsTime = 0.4f;
    const float CloseTime = 0.1f;        // 引っ込むとき、左右からまん中へ閉じる時間

    /// <summary>今撃っている回（0 から）。テスト用</summary>
    public int ShotIndex { get; private set; }
    /// <summary>狙いが止まっているか（赤）。テスト用</summary>
    public bool IsLocked => _step == Step.Lock;
    /// <summary>円の中心（ゲームの世界）。テスト用</summary>
    public Vector2 AimPoint => _aim;

    /// <summary>1回出す</summary>
    public static FinderSpotlightEffect Spawn(AttackRunner owner, AttackData data, FinderSpotlightAttack attack)
    {
        if (owner == null || data == null || attack == null) return null;
        GameObject go = owner.CreateSpawnedObject($"Finder_Spotlight_{data.name}", owner.OriginPoint);
        var e = go.AddComponent<FinderSpotlightEffect>();
        e._owner = owner;
        e._data = data;
        e._attack = attack;
        e.Build();
        e.StartCoroutine(e.Run());
        return e;
    }

    void Build()
    {
        _aim = CurrentAimPoint() ?? (Vector2)transform.position;
        _line = FinderLine.Create(transform, FinderArt.Cyan, true, 1, LineOrder, "AimLine");
        _line.Visible = false;

        _circleDots = Mathf.Max(4, Mathf.RoundToInt(_attack.CircleDiameter * RuntimeSprites.PixelsPerUnit));
        _circle = FinderArt.Child(transform, "AimCircle", _aim);
        Vector2 size = Vector2.one * _attack.CircleDiameter;
        _circleFill = AttackRunner.AddVisual(_circle, FinderArt.Disc(_circleDots), Fade(FinderArt.Cyan, 0.15f), size, CircleOrder, "Fill");
        _circleRing = AttackRunner.AddVisual(_circle, FinderArt.Ring(_circleDots, 1), Fade(FinderArt.Cyan, 0.8f), size, CircleOrder, "Ring");
        _circle.gameObject.SetActive(false);
    }

    // ───────── 進める ─────────

    IEnumerator Run()
    {
        // 1. 検索バーが出る
        SetStep(Step.Appear);
        while (Time.time < _stepStart + _attack.AppearTime)
        {
            if (!Tick()) yield break;
            yield return null;
        }

        int shots = 1 + _attack.ExtraShots;
        for (int i = 0; i < shots; i++)
        {
            ShotIndex = i;
            // 2. 打つ（1回目）／追う（2回目から）
            float trackTime = i == 0 ? _attack.Query.Length * _attack.TypeInterval : _attack.ExtraTrackTime;
            SetStep(Step.Track);
            if (i == 0) _typeStart = Time.time;
            ShowTracking();
            while (Time.time < _stepStart + trackTime)
            {
                if (!Tick()) yield break;
                yield return null;
            }

            // 3. 検索完了：赤で止まる
            if (!Tick()) yield break;
            SetStep(Step.Lock);
            ShowLocked();
            while (Time.time < _stepStart + _attack.LockTime)
            {
                if (!Tick()) yield break;
                yield return null;
            }

            // 4. 発射
            if (OwnerStopped())
            {
                Destroy(gameObject);
                yield break;
            }
            SetStep(Step.Fire);
            Fire();
            while (Time.time < _stepStart + _attack.FireTime) yield return null;
        }

        // 5. 引っ込む
        SetStep(Step.Retract);
        while (Time.time < _stepStart + _attack.RetractTime) yield return null;
        Destroy(gameObject);
    }

    void SetStep(Step step)
    {
        _step = step;
        _stepStart = Time.time;
    }

    /// <summary>撃つ前の1フレーム分。ボスの攻撃が止められていたら、撃たずに消えて false</summary>
    bool Tick()
    {
        if (OwnerStopped())
        {
            Destroy(gameObject);
            return false;
        }
        if (_step == Step.Track)
        {
            Vector2? p = CurrentAimPoint();
            if (p.HasValue) _aim = p.Value;
            PlaceAim();
        }
        else if (_step == Step.Lock)
        {
            // 止まった円は 0.1秒ごとに点滅
            bool on = Mathf.FloorToInt((Time.time - _stepStart) / BlinkInterval) % 2 == 0;
            _circleFill.enabled = on;
            _circleRing.enabled = on;
        }
        return true;
    }

    /// <summary>ボスがこの技をやめたか（形態変化・正常化・止められた）</summary>
    bool OwnerStopped() => _owner == null || !_owner.isActiveAndEnabled || _owner.Current != _data;

    /// <summary>プレイヤーの足元から、円の半径だけ上（円の下の端が足元）。相手がいなければ null</summary>
    Vector2? CurrentAimPoint()
    {
        if (!FinderArt.TryGetTargetBounds(_owner, out Bounds b)) return null;
        return new Vector2(b.center.x, b.min.y + _attack.CircleDiameter * 0.5f);
    }

    void ShowTracking()
    {
        _line.SetStyle(FinderArt.Cyan, true, 1);
        _line.Visible = true;
        _circleFill.sprite = FinderArt.Disc(_circleDots);
        _circleRing.sprite = FinderArt.Ring(_circleDots, 1);
        _circleFill.color = Fade(FinderArt.Cyan, 0.15f);
        _circleRing.color = Fade(FinderArt.Cyan, 0.8f);
        _circleFill.enabled = _circleRing.enabled = true;
        _circle.gameObject.SetActive(true);
        PlaceAim();
    }

    void ShowLocked()
    {
        _line.SetStyle(FinderArt.Red, false, 2);
        _circleRing.sprite = FinderArt.Ring(_circleDots, 2);
        _circleFill.color = Fade(FinderArt.Red, 0.25f);
        _circleRing.color = FinderArt.Red;
        _rows++;
        _barFlashUntil = Time.time + BarFlashTime;
        PlaceAim();
    }

    void PlaceAim()
    {
        _circle.position = _aim;
        _line.Set(BeamOrigin(), _aim);
    }

    void Fire()
    {
        _line.Visible = false;
        _circle.gameObject.SetActive(false);
        Vector2 from = BeamOrigin();
        Vector2 to = _aim;
        StartCoroutine(Beam(from, to));
        if (_owner == null) return;
        _owner.NotifyReleased(_data);

        // 判定：光の帯（回した箱）と、足元の円。どちらも短い時間だけ（ダメージ・ノックバックは Hitbox に任せる）
        Vector2 d = to - from;
        float length = Mathf.Max(d.magnitude, 0.01f);
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        var beam = FinderArt.Child(transform, "BeamHit", (from + to) * 0.5f);
        beam.rotation = Quaternion.Euler(0f, 0f, angle);
        var box = AddTrigger<BoxCollider2D>(beam.gameObject);
        box.size = new Vector2(length, _attack.BeamWidth);
        Hitbox beamHit = _owner.AddHitbox(beam.gameObject, _data, box);
        beamHit.Activate();
        Destroy(beam.gameObject, _attack.HitWindow);

        var circle = FinderArt.Child(transform, "CircleHit", to);
        var col = AddTrigger<CircleCollider2D>(circle.gameObject);
        col.radius = _attack.CircleDiameter * 0.5f;
        Hitbox circleHit = _owner.AddHitbox(circle.gameObject, _data, col);
        circleHit.Activate();
        Destroy(circle.gameObject, _attack.HitWindow);

        // 当たったかを見て、外れたら検索バーに No Results（11-1）。当たった絵は FinderSkin.Error（種類 Spotlight）
        _hitThisShot = false;
        beamHit.Hit += OnBeamHit;
        circleHit.Hit += OnBeamHit;
        StartCoroutine(CheckMiss(_attack.HitWindow + 0.02f));
    }

    void OnBeamHit(IDamageable target, DamageInfo info) => _hitThisShot = true;

    IEnumerator CheckMiss(float wait)
    {
        yield return new WaitForSeconds(wait);
        if (!_hitThisShot) _noResultsUntil = Time.time + NoResultsTime;
    }

    static T AddTrigger<T>(GameObject go) where T : Collider2D
    {
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<T>();
        col.isTrigger = true;
        return col;
    }

    /// <summary>光の帯の見た目（外側うすい黄色・芯は白）。細くなりながら消える。当たった床に白い光</summary>
    IEnumerator Beam(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        float length = Mathf.Max(d.magnitude, 0.01f);
        Transform root = FinderArt.Child(transform, "Beam", (from + to) * 0.5f);
        root.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        var outer = AttackRunner.AddVisual(root, PlaceholderSprites.Square, FinderArt.BeamOuter, new Vector2(length, BeamOuterDots * FinderArt.Dot), BeamOrder, "Outer");
        var core = AttackRunner.AddVisual(root, PlaceholderSprites.Square, Color.white, new Vector2(length, BeamCoreDots * FinderArt.Dot), BeamOrder + 1, "Core");
        // 帯の両側に、うすい黄色の線を 1本ずつ（判定の外。3割）【仮】2026-11-18（11-1）
        float side = _attack.BeamWidth * 0.5f + 3f * FinderArt.Dot;
        var sideA = AttackRunner.AddVisual(root, PlaceholderSprites.Square, new Color(1f, 0.92f, 0.45f, 0.3f), new Vector2(length, FinderArt.Dot), BeamOrder, "SideA");
        var sideB = AttackRunner.AddVisual(root, PlaceholderSprites.Square, new Color(1f, 0.92f, 0.45f, 0.3f), new Vector2(length, FinderArt.Dot), BeamOrder, "SideB");
        sideA.transform.localPosition = new Vector3(0f, side, 0f);
        sideB.transform.localPosition = new Vector3(0f, -side, 0f);
        // 帯の根元（検索バーの虫めがね）が、白く 2倍にふくらむ
        Transform glowRoot = FinderArt.Child(transform, "BeamRoot", from);
        var glow = RuntimeSprites.Show(glowRoot, FinderArt.Disc(14), Vector2.zero, FlashOrder, 1f, "Glow");

        Vector2 floor = to - new Vector2(0f, _attack.CircleDiameter * 0.5f);
        Transform flashRoot = FinderArt.Child(transform, "FloorFlash", floor);
        var flash = AttackRunner.AddVisual(flashRoot, FinderArt.Disc(16), Color.white, new Vector2(_attack.CircleDiameter * 1.2f, 0.45f), FlashOrder, "Flash");

        Vector3 outerScale = outer.transform.localScale, coreScale = core.transform.localScale;
        float start = Time.time;
        float life = Mathf.Max(_attack.FireTime, 0.01f);
        while (Time.time - start < Mathf.Max(life, FloorFlashTime))
        {
            float k = 1f - Mathf.Clamp01((Time.time - start) / life);
            outer.transform.localScale = new Vector3(outerScale.x, outerScale.y * k, 1f);
            core.transform.localScale = new Vector3(coreScale.x, coreScale.y * k, 1f);
            flash.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((Time.time - start) / FloorFlashTime));
            sideA.color = sideB.color = new Color(1f, 0.92f, 0.45f, 0.3f * k);
            float g = Mathf.Clamp01((Time.time - start) / 0.15f);
            glowRoot.localScale = Vector3.one * Mathf.Lerp(1f, 2f, 1f - (1f - g) * (1f - g));
            glow.color = new Color(1f, 1f, 1f, 1f - g);
            yield return null;
        }
        Destroy(root.gameObject);
        Destroy(flashRoot.gameObject);
        Destroy(glowRoot.gameObject);
    }

    // ───────── 検索バー（ゲームの世界の位置） ─────────

    /// <summary>検索バーの範囲（ゲームの世界）。slide は上へずらす距離（マス）</summary>
    Rect BarRect(float slide)
    {
        Rect view = ScreenUi.ViewRect();
        Vector2 size = _attack.BarSize;
        float top = view.yMax - _attack.BarTopMargin + slide;
        return new Rect(view.center.x - size.x * 0.5f, top - size.y, size.x, size.y);
    }

    /// <summary>光の帯・狙いの線が出る所（検索バーの下の端の真ん中）</summary>
    Vector2 BeamOrigin()
    {
        Rect bar = BarRect(0f);
        return new Vector2(bar.center.x, bar.yMin);
    }

    /// <summary>出る・引っ込む動き（上へずらす距離と、濃さ。width は横の幅の割合＝左右からまん中へ閉じる）</summary>
    void BarMotion(out float slide, out float alpha, out float width)
    {
        slide = 0f;
        alpha = 1f;
        width = 1f;
        if (_step == Step.Appear)
        {
            float k = _attack.AppearTime > 0f ? Mathf.Clamp01((Time.time - _stepStart) / _attack.AppearTime) : 1f;
            float e = 1f - (1f - k) * (1f - k);
            slide = (1f - e) * SlideCells;
            alpha = k;
        }
        else if (_step == Step.Retract)
        {
            // 消えるとき：検索バーが、左右からまん中へ閉じる（11-1。2026-11-18）
            // 引っ込む時間の最後の 0.1秒で閉じる（それまでは、外れたときの No Results を見せる）
            float close = Mathf.Max(0.01f, Mathf.Min(CloseTime, _attack.RetractTime));
            float k = Mathf.Clamp01((Time.time - _stepStart - (_attack.RetractTime - close)) / close);
            width = 1f - k * k;
            alpha = width > 0.02f ? 1f : 0f;
        }
    }

    /// <summary>今見えている文字</summary>
    string TypedText()
    {
        string q = _attack.Query;
        if (_typeStart < 0f) return "";
        if (ShotIndex > 0 || _step != Step.Track) return q;
        int n = Mathf.FloorToInt((Time.time - _typeStart) / _attack.TypeInterval) + 1;
        return q.Substring(0, Mathf.Clamp(n, 0, q.Length));
    }

    // ───────── 画面の上の表示（OnGUI） ─────────

    void OnGUI()
    {
        if (_attack == null) return;
        GUI.depth = 5; // 能力の枠・ゲージ（10）より手前
        if (Event.current.type != EventType.Repaint) return;
        GameSession session = GameSession.Instance;
        if (session != null && (session.IsTransitioning || session.IsGameOver)) return;

        PixelGui.Begin(false);
        DrawBar();
        PixelGui.End();
    }

    void DrawBar()
    {
        BarMotion(out float slide, out float alpha, out float widthK);
        if (alpha <= 0f) return;
        Rect w = BarRect(slide);
        if (widthK < 1f)
        {
            float cx = w.center.x;
            w = new Rect(cx - w.width * widthK * 0.5f, w.y, w.width * widthK, w.height);
        }
        Vector2? a = ScreenUi.WorldToGui(new Vector3(w.xMin, w.yMax, 0f));
        Vector2? b = ScreenUi.WorldToGui(new Vector3(w.xMax, w.yMin, 0f));
        if (!a.HasValue || !b.HasValue) return;
        Rect r = Rect.MinMaxRect(Mathf.Round(a.Value.x), Mathf.Round(a.Value.y), Mathf.Round(b.Value.x), Mathf.Round(b.Value.y));
        if (r.width < 8f || r.height < 4f) return;
        float cell = r.height / Mathf.Max(0.1f, _attack.BarSize.y); // 1マスが画面の何ドットか
        float dot = cell / RuntimeSprites.PixelsPerUnit;

        // 地とふち（角の丸い濃い灰色。少し透ける）
        RoundedBox(r, Fade(FinderArt.BarFill, 0.92f * alpha), Fade(FinderArt.BarEdge, alpha));
        // 検索完了：白く光る
        if (Time.time < _barFlashUntil)
        {
            float k = (_barFlashUntil - Time.time) / BarFlashTime;
            RoundedBox(r, new Color(1f, 1f, 1f, 0.85f * k * alpha), new Color(1f, 1f, 1f, k * alpha));
        }

        // 虫めがね（12ドット）
        float m = Mathf.Round(12f * dot);
        var icon = new Rect(Mathf.Round(r.x + cell * 0.35f), Mathf.Round(r.center.y - m * 0.5f), m, m);
        PixelGui.DrawSprite(icon, FinderArt.Magnifier, new Color(1f, 1f, 1f, alpha));

        if (widthK < 0.6f) return; // 閉じる途中は、地とふちだけ

        // 打った文字と、点滅するカーソル。外れたときは、灰色の No Results（11-1。2026-11-18）
        bool noResults = Time.time < _noResultsUntil;
        string text = noResults ? "No Results" : TypedText();
        float tx = Mathf.Round(icon.xMax + cell * 0.3f);
        var textRect = new Rect(tx, Mathf.Round(r.center.y - TextSize * 0.5f), Mathf.Max(0f, r.xMax - tx - 4f), TextSize);
        PixelGui.TextEn(textRect, text, TextSize, noResults ? new Color(0.56f, 0.56f, 0.58f, alpha) : new Color(1f, 1f, 1f, alpha));
        bool typing = _step == Step.Track && ShotIndex == 0;
        if (!noResults && (typing || Mathf.FloorToInt(Time.time / 0.4f) % 2 == 0))
        {
            float width = string.IsNullOrEmpty(text) ? 0f : PixelGui.Measure(text, PixelGui.Style(true, TextSize));
            PixelGui.Fill(new Rect(Mathf.Round(tx + width + 2f), Mathf.Round(r.center.y - TextSize * 0.5f - 1f), 2f, TextSize + 2f), new Color(1f, 1f, 1f, alpha));
        }

        // 結果の行（プレイヤーの小さい絵）。追っている間は、まだ選ばれていない行を1行足す。
        // 固定（赤）になった瞬間、その行が青で選ばれ、右の端に ⏎ の印（11-1。2026-11-18）
        float rowH = Mathf.Round(cell * RowHeightCells);
        bool pending = _step == Step.Track;
        int shown = _rows + (pending ? 1 : 0);
        for (int i = 0; i < shown; i++)
        {
            var row = new Rect(r.x, r.yMax + RowGap + i * (rowH + RowGap), r.width, rowH);
            bool selected = !pending && i == _rows - 1;
            Color fill = selected ? FinderArt.RowSelect : FinderArt.BarFill;
            RoundedBox(row, Fade(fill, 0.92f * alpha), Fade(FinderArt.BarEdge, alpha));
            DrawPlayerIcon(new Rect(row.x + cell * 0.35f, row.y + 3f, rowH - 6f, rowH - 6f), alpha);
            if (selected)
            {
                float rm = Mathf.Round(6f * dot * 1.5f);
                PixelGui.DrawSprite(new Rect(Mathf.Round(row.xMax - rm - cell * 0.35f), Mathf.Round(row.center.y - rm * 0.5f), rm, rm), FinderSkinArt.Return6, new Color(1f, 1f, 1f, alpha));
            }
        }
    }

    /// <summary>プレイヤーの絵を、縦横の比を保って枠に収めて描く</summary>
    static void DrawPlayerIcon(Rect box, float alpha)
    {
        Sprite[] walk = GameArt.Instance != null ? GameArt.Instance.PlayerWalk : null;
        Sprite s = walk != null && walk.Length > 0 ? walk[0] : null;
        if (s == null)
        {
            PixelGui.Fill(new Rect(box.center.x - 4f, box.y + 2f, 8f, box.height - 4f), new Color(1f, 1f, 1f, alpha));
            return;
        }
        Rect tr = s.textureRect;
        float k = Mathf.Min(box.width / Mathf.Max(1f, tr.width), box.height / Mathf.Max(1f, tr.height));
        float w = Mathf.Round(tr.width * k), h = Mathf.Round(tr.height * k);
        PixelGui.DrawSprite(new Rect(Mathf.Round(box.x), Mathf.Round(box.center.y - h * 0.5f), w, h), s, new Color(1f, 1f, 1f, alpha));
    }

    /// <summary>角を丸めた四角（角は2ドット。塗りとふちは重ねない＝半透明でも濃さがそろう）</summary>
    static void RoundedBox(Rect r, Color fill, Color edge)
    {
        float x = r.x, y = r.y, w = r.width, h = r.height;
        // 塗り
        PixelGui.Fill(new Rect(x + 1f, y + 2f, w - 2f, h - 4f), fill);
        PixelGui.Fill(new Rect(x + 2f, y + 1f, w - 4f, 1f), fill);
        PixelGui.Fill(new Rect(x + 2f, y + h - 2f, w - 4f, 1f), fill);
        // ふち
        PixelGui.Fill(new Rect(x + 2f, y, w - 4f, 1f), edge);
        PixelGui.Fill(new Rect(x + 2f, y + h - 1f, w - 4f, 1f), edge);
        PixelGui.Fill(new Rect(x, y + 2f, 1f, h - 4f), edge);
        PixelGui.Fill(new Rect(x + w - 1f, y + 2f, 1f, h - 4f), edge);
        PixelGui.Fill(new Rect(x + 1f, y + 1f, 1f, 1f), edge);
        PixelGui.Fill(new Rect(x + w - 2f, y + 1f, 1f, 1f), edge);
        PixelGui.Fill(new Rect(x + 1f, y + h - 2f, 1f, 1f), edge);
        PixelGui.Fill(new Rect(x + w - 2f, y + h - 2f, 1f, 1f), edge);
    }

    static Color Fade(Color c, float a)
    {
        c.a *= a;
        return c;
    }
}
