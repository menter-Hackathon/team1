using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// レーザーツールの本体（2026-11-18。docs/app-boss-skills-design.md 3-3）。PremiereRazor が出し、あとは自分で進む。場に残る技の効果（Lingers）。
/// 2026-11-19（docs/app-feedback-v2-design.md 5-3）：区間の列を、まるごと消して閉じる。
/// ・予告（刃・赤い点線・赤むらさきの斜線）→ 切る（区間の全部に判定）→ 削除（区間の列が黒く抜ける。中は暗い市松。区間の中の足場を消す）→
///   詰め（黒い列が左右から閉じる。画面がゆれる。ボスが寄る）→ つなぎ目の白い線 → 足場が戻るまで残る
/// ・黒い列は絵だけ（当たり判定は無い）。重ねる順番は、ステージの絵・足場・タイムラインの飾り（8 まで）より前、キャラクター（11〜）と弾より後ろ（9）。
///   床の絵は床の上面より下へはみ出しているので、列は画面の下の端から上の端まで
/// ・消す足場：AttackRunner の「上に乗れる地形」のうち、すり抜け床（OneWayPlatform）のレイヤーで、真ん中が区間の中にある物。
///   部屋の床（部屋の幅の半分以上ある物）・落下物の足場・開いたり閉じたりする足場（BlinkingPlatform）は消さない
/// ・消えた足場は当たり判定と絵を消し、点線の枠だけを残す。最後の1秒は点滅して戻る
/// ・形態変化・正常化で消されたとき、自分で終わったときは、Restore で足場をすぐ戻す（黒い列は子なので一緒に消える）。寄ったボスはそのまま
/// </summary>
public class PremiereRazorEffect : CustomEffect
{
    class Removed
    {
        public Collider2D Collider;
        public readonly List<SpriteRenderer> Renderers = new List<SpriteRenderer>();
    }

    // ───────── 見た目【仮】 ─────────
    const float BladeHeight = 0.8f;
    const float FlashWidth = 6f * PremiereArt.Pixel;
    const float FlashFade = 0.2f;
    const float BlinkInterval = 0.1f;
    const float ShakePixels = 2f;
    const int GhostOrder = 6, HatchOrder = 7, ColumnOrder = 9, SeamOrder = 10, DottedOrder = 41, FlashOrder = 42, BladeOrder = 43;

    AttackRunner _owner;
    PremiereRazor _config;
    AttackData _hitData;
    Rect _arena;
    Rect _view;
    float _center;
    float _half;
    float _top;        // 刃の上の端
    float _start;
    float _cutAt;
    float _deleteAt;
    float _closeAt;
    float _doneAt;
    float _returnAt = float.PositiveInfinity;
    bool _cut;
    bool _deleted;
    bool _closing;
    readonly List<Removed> _removed = new List<Removed>();

    readonly Transform[] _blades = new Transform[2];
    readonly SpriteRenderer[] _dotted = new SpriteRenderer[2];
    readonly SpriteRenderer[] _flashes = new SpriteRenderer[2];
    SpriteRenderer _hatch;
    Transform _column;
    SpriteRenderer _checker;
    SpriteRenderer[] _columnEdges;
    SpriteRenderer _seam;
    Hitbox _hitbox;
    Transform _ghosts;
    // 画面の上の帯（タイムライン）の、紫のクリップ（左・まん中・右）。2026-11-18：スキルの見た目 12-3
    readonly Transform[] _clips = new Transform[3];
    readonly SpriteRenderer[] _cutTicks = new SpriteRenderer[2];
    const float ClipHeight = 0.35f, ClipMargin = 3f;

    BossController _boss;
    float _bossFromX;
    float _bossToX;
    bool _approach;

    public override bool Lingers => true;
    /// <summary>切って、閉じて、ボスが寄り終えたら true（技はここまで待つ。足場が戻るまでは場に残る）</summary>
    public bool ActionDone { get; private set; }

    public static PremiereRazorEffect Spawn(AttackRunner owner, PremiereRazor config, float centerX, AttackData hitData)
    {
        Rect arena = BattleArena.GetBounds();
        GameObject go = owner.CreateSpawnedObject("PremiereRazor", new Vector2(centerX, arena.center.y));
        var e = go.AddComponent<PremiereRazorEffect>();
        e._owner = owner;
        e._config = config;
        e._hitData = hitData;
        e._arena = arena;
        Rect view = ScreenUi.ViewRect();
        e._view = view.height > 1f ? view : new Rect(arena.xMin, arena.yMin - 2f, arena.width, arena.height + 4f);
        e._center = centerX;
        e._half = config.SectionWidth * 0.5f;
        e._top = arena.yMax - config.TopInset;
        e._start = Time.time;
        e._cutAt = e._start + config.TelegraphTime;
        e._deleteAt = e._cutAt + config.CutTime;
        e._closeAt = e._deleteAt + config.BlackTime;
        e._doneAt = e._closeAt + config.CloseTime;
        e._boss = owner.GetComponent<BossController>();
        e.BuildTelegraph();
        e.BuildHitbox();
        e.Tick(Time.time);
        return e;
    }

    void Update() => Tick(Time.time);

    void Tick(float now)
    {
        float floor = _arena.yMin;

        // ── 予告：刃・赤い点線・斜線。最後の0.3秒は点線を点滅 ──
        if (now < _cutAt)
        {
            bool blink = _cutAt - now < 0.3f && Mathf.FloorToInt(now / 0.06f) % 2 == 1;
            foreach (var d in _dotted) d.enabled = !blink;
            foreach (var t in _cutTicks) if (t != null) t.enabled = !blink;
            return;
        }
        UpdateClips(now);

        // ── 切る：刃が上から下へ落ち、白い光が走る。区間の全部に判定 ──
        if (!_cut)
        {
            _cut = true;
            if (_hitbox != null) _hitbox.Activate();
            for (int i = 0; i < 2; i++)
            {
                _dotted[i].enabled = false;
                _flashes[i].enabled = true;
            }
        }
        float cutK = _config.CutTime > 0f ? Mathf.Clamp01((now - _cutAt) / _config.CutTime) : 1f;
        float span = Mathf.Max(0.1f, _top - floor);
        for (int i = 0; i < 2; i++)
        {
            float x = LineX(i);
            float bladeY = Mathf.Lerp(_top - BladeHeight * 0.5f, floor + BladeHeight * 0.5f, cutK);
            _blades[i].position = new Vector3(x, bladeY, 0f);
            float h = span * cutK;
            SetBox(_flashes[i], new Vector2(x, _top - h * 0.5f), new Vector2(FlashWidth, Mathf.Max(0.01f, h)));
            float fade = now < _deleteAt ? 1f : 1f - Mathf.Clamp01((now - _deleteAt) / FlashFade);
            _flashes[i].color = new Color(1f, 1f, 1f, fade);
        }
        if (now < _deleteAt) return;

        // ── 削除：区間の列が黒く抜ける（暗い市松）。足場を消す ──
        if (!_deleted)
        {
            _deleted = true;
            if (_hitbox != null) _hitbox.Deactivate();
            _hatch.enabled = false;
            foreach (var b in _blades) b.gameObject.SetActive(false);
            _column.gameObject.SetActive(true);
            SetColumnWidth(_config.SectionWidth, 0f);
            RemovePlatforms();
            _returnAt = now + _config.RemoveTime;
        }
        if (now < _closeAt) { UpdateApproach(0f); return; }

        // ── 詰め：黒い列が左右からまん中へ閉じる。画面が横に1回ゆれる。ボスが寄る ──
        if (!_closing)
        {
            _closing = true;
            BeginApproach();
            StageCamera.Shake(0.12f, 0.3f);
        }
        float closeK = _config.CloseTime > 0f ? Mathf.Clamp01((now - _closeAt) / _config.CloseTime) : 1f;
        if (closeK < 1f)
        {
            float ease = closeK * closeK * (3f - 2f * closeK);
            SetColumnWidth(_config.SectionWidth * (1f - ease), Shake(now));
        }
        else if (_column.gameObject.activeSelf)
        {
            _column.gameObject.SetActive(false);
            ShowSeam(now);
        }
        UpdateApproach(closeK);
        UpdateSeam(now);
        if (now < _doneAt) return;

        // ── ここで技は終わり。足場が戻るまで残る ──
        if (!ActionDone)
        {
            ActionDone = true;
            foreach (var f in _flashes) f.enabled = false;
        }
        if (_removed.Count == 0 && (_seam == null || !_seam.enabled))
        {
            Destroy(gameObject);
            return;
        }

        // 最後の1秒は点滅して戻る予告
        if (now >= _returnAt)
        {
            if (_seam == null || !_seam.enabled)
            {
                Destroy(gameObject); // Restore で足場を戻す
                return;
            }
            RestorePlatforms();
            return;
        }
        bool blinking = _returnAt - now <= _config.BlinkTime;
        bool visible = blinking && Mathf.FloorToInt(now / BlinkInterval) % 2 == 0;
        foreach (var r in _removed)
        {
            foreach (var sr in r.Renderers)
            {
                if (sr != null && sr.enabled != visible) sr.enabled = visible;
            }
        }
    }

    float LineX(int i) => i == 0 ? _center - _half : _center + _half;

    float Shake(float now)
    {
        // 【仮】閉じる間だけ、黒い列を横に少しゆらす
        return (Mathf.PerlinNoise(now * 40f, 2.7f) - 0.5f) * 2f * ShakePixels * PremiereArt.Pixel;
    }

    static void SetBox(SpriteRenderer sr, Vector2 worldCenter, Vector2 size)
    {
        sr.transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
        Vector2 native = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(size.x / Mathf.Max(native.x, 0.001f), size.y / Mathf.Max(native.y, 0.001f), 1f);
    }

    // ───────── 判定 ─────────

    /// <summary>区間の全部（幅 × 床から天井まで）の判定。攻撃データはダメージ・ノックバックだけ使う</summary>
    void BuildHitbox()
    {
        if (_owner == null || _hitData == null) return;
        var go = new GameObject("RazorSection");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        float h = Mathf.Max(1f, _arena.height);
        go.transform.position = new Vector3(_center, _arena.yMin + h * 0.5f, 0f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(_config.SectionWidth, h);
        _hitbox = _owner.AddHitbox(go, _hitData, box);
        _hitbox.SetErrorKind("Razor");
    }

    // ───────── 足場 ─────────

    void RemovePlatforms()
    {
        if (_owner == null) return;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(_owner.GroundMask);
        var hits = new List<Collider2D>();
        Physics2D.OverlapArea(new Vector2(_center - _half, _arena.yMin + 0.1f), new Vector2(_center + _half, _arena.yMax), filter, hits);
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        var seen = new HashSet<GameObject>();
        _ghosts = PremiereArt.Child(transform, "RemovedPlatforms", Vector2.zero);
        foreach (Collider2D col in hits)
        {
            if (col == null || !col.enabled) continue;
            if (oneWay >= 0 && col.gameObject.layer != oneWay) continue;
            Bounds b = col.bounds;
            if (b.center.x < _center - _half || b.center.x > _center + _half) continue;
            if (b.size.x >= _arena.width * 0.5f) continue;                       // 部屋の床
            if (col.GetComponentInParent<FallingObject>() != null) continue;     // 落下物の足場
            if (col.GetComponentInParent<BlinkingPlatform>() != null) continue;  // 開いたり閉じたりする足場
            if (col.GetComponentInParent<CustomEffect>() != null) continue;      // 技が出した物

            var r = new Removed { Collider = col };
            col.enabled = false;
            if (seen.Add(col.gameObject))
            {
                foreach (var sr in col.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (sr == null || !sr.enabled) continue;
                    sr.enabled = false;
                    r.Renderers.Add(sr);
                }
            }
            _removed.Add(r);

            // 跡：点線の枠だけ残す（戻る場所が分かるように）
            var ghost = RuntimeSprites.Show(_ghosts, PremiereArt.DashedFrame(b.size.x, Mathf.Max(b.size.y, 2f * PremiereArt.Pixel)), Vector2.zero, GhostOrder, 1f, "Ghost");
            ghost.transform.position = new Vector3(b.center.x, b.center.y, 0f);
            ghost.color = new Color(1f, 1f, 1f, 0.6f);
        }
    }

    void RestorePlatforms()
    {
        foreach (var r in _removed)
        {
            if (r.Collider != null) r.Collider.enabled = true;
            foreach (var sr in r.Renderers)
            {
                if (sr != null) sr.enabled = true;
            }
        }
        _removed.Clear();
        if (_ghosts != null) Destroy(_ghosts.gameObject);
    }

    protected override void Restore() => RestorePlatforms();

    // ───────── ボスの寄り ─────────

    void BeginApproach()
    {
        if (_boss == null || _owner == null || _boss.State != BossState.Fighting) return;
        Vector2? target = _owner.TargetPoint;
        if (target == null) return;
        float bx = _boss.transform.position.x;
        float px = target.Value.x;
        float dist = Mathf.Abs(px - bx);
        float move = Mathf.Clamp(dist - _config.MinBossDistance, 0f, _config.Approach);
        if (move <= 0f) return;
        float ext = _boss.BodyBounds.extents.x;
        float lo = _arena.xMin + ext, hi = _arena.xMax - ext;
        float to = bx + Mathf.Sign(px - bx) * move;
        _bossFromX = bx;
        _bossToX = hi >= lo ? Mathf.Clamp(to, lo, hi) : bx;
        _approach = true;
    }

    void UpdateApproach(float k)
    {
        if (!_approach) return;
        if (_boss == null || _boss.State != BossState.Fighting)
        {
            _approach = false;
            return;
        }
        float ease = 1f - (1f - k) * (1f - k) * (1f - k); // 一気に寄って、最後はゆっくり
        Vector3 p = _boss.transform.position;
        _boss.Teleport(new Vector2(Mathf.Lerp(_bossFromX, _bossToX, ease), p.y));
        if (k >= 1f) _approach = false;
    }

    // ───────── 見た目を作る ─────────

    void BuildTelegraph()
    {
        float floor = _arena.yMin;
        float dottedTop = _top - BladeHeight;
        float dottedLen = Mathf.Max(0.1f, dottedTop - floor);

        // 切られる区間：赤むらさきの斜線（床から刃の下まで）
        _hatch = PremiereTimelineArt.Tiled(transform, PremiereTimelineArt.RedPurpleHatch, Vector2.zero, new Vector2(_config.SectionWidth, dottedLen), Color.white, HatchOrder, "Hatch");
        _hatch.transform.position = new Vector3(_center, floor + dottedLen * 0.5f, 0f);

        for (int i = 0; i < 2; i++)
        {
            float x = LineX(i);
            _blades[i] = RuntimeSprites.Show(transform, PremiereArt.RazorBlade, Vector2.zero, BladeOrder, 1f, "Blade").transform;
            _blades[i].position = new Vector3(x, _top - BladeHeight * 0.5f, 0f);
            // 切る線：赤い点線（太さ 2ドット）
            _dotted[i] = RuntimeSprites.Show(transform, PremiereArt.DottedColumn(dottedLen), Vector2.zero, DottedOrder, 1f, "CutLine");
            _dotted[i].transform.position = new Vector3(x, floor + dottedLen * 0.5f, 0f);
            _dotted[i].color = PremiereTimelineArt.EnemyRed;
            _flashes[i] = PremiereArt.Box(transform, Vector2.zero, new Vector2(FlashWidth, 0.01f), Color.white, FlashOrder, "CutFlash");
            _flashes[i].enabled = false;
        }

        // 黒く抜ける列（画面の下の端から上の端まで。中は暗い市松。左右のふちは黒 1ドット）
        _column = PremiereArt.Child(transform, "DeletedColumn", Vector2.zero);
        _column.position = new Vector3(_center, _view.center.y, 0f);
        _checker = PremiereTimelineArt.Tiled(_column, PremiereTimelineArt.Checker, Vector2.zero, new Vector2(_config.SectionWidth, _view.height + 2f), Color.white, ColumnOrder, "Checker");
        _columnEdges = new SpriteRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            _columnEdges[i] = PremiereArt.Box(_column, Vector2.zero, new Vector2(PremiereArt.Pixel, _view.height + 2f), Color.black, ColumnOrder, "Edge");
        }
        _column.gameObject.SetActive(false);
        BuildClips();
    }

    /// <summary>黒い列の幅を変える（まん中は区間のまん中のまま）</summary>
    void SetColumnWidth(float w, float shake)
    {
        if (_column == null) return;
        w = Mathf.Max(0.001f, w);
        _column.position = new Vector3(_center + shake, _view.center.y, 0f);
        PremiereTimelineArt.SetTiledSize(_checker, new Vector2(w, _view.height + 2f));
        if (_columnEdges == null) return;
        _columnEdges[0].transform.localPosition = new Vector3(-w * 0.5f, 0f, 0f);
        _columnEdges[1].transform.localPosition = new Vector3(w * 0.5f, 0f, 0f);
    }

    /// <summary>閉じた所に、白い たての線（1ドット）が残る</summary>
    void ShowSeam(float now)
    {
        _seam = PremiereArt.Box(transform, Vector2.zero, new Vector2(PremiereArt.Pixel, _view.height + 2f), Color.white, SeamOrder, "Seam");
        _seam.transform.position = new Vector3(_center, _view.center.y, 0f);
        _seamUntil = now + _config.SeamTime;
        GenericFrameKit.Shards(new Vector2(_center, _arena.yMin + (_top - _arena.yMin) * 0.4f), Vector2.up, 6, i => PremiereSkinArt.Film8, i => Color.white, 70f, 1.3f);
    }

    float _seamUntil = float.NegativeInfinity;

    void UpdateSeam(float now)
    {
        if (_seam == null || !_seam.enabled) return;
        float left = _seamUntil - now;
        if (left <= 0f)
        {
            _seam.enabled = false;
            return;
        }
        float a = Mathf.Clamp01(left / 0.2f);
        _seam.color = new Color(1f, 1f, 1f, a);
    }

    /// <summary>画面の上の帯（タイムライン）に、紫のクリップの帯。切る所に白い印（12-3。2026-11-18）</summary>
    void BuildClips()
    {
        float y = _top + ClipHeight * 0.5f + 2f * PremiereArt.Pixel;
        float left = Mathf.Max(_arena.xMin + 0.2f, _center - _half - ClipMargin);
        float right = Mathf.Min(_arena.xMax - 0.2f, _center + _half + ClipMargin);
        float[] xs = { left, LineX(0), LineX(1), right };
        for (int i = 0; i < 3; i++)
        {
            float w = Mathf.Max(0.05f, xs[i + 1] - xs[i]);
            Transform c = PremiereArt.Child(transform, "Clip", Vector2.zero);
            c.position = new Vector3((xs[i] + xs[i + 1]) * 0.5f, y, 0f);
            PremiereArt.Box(c, Vector2.zero, new Vector2(w, ClipHeight), PremiereSkinArt.ClipDark, BladeOrder - 2, "Edge");
            PremiereArt.Box(c, Vector2.zero, new Vector2(w - 2f * PremiereArt.Pixel, ClipHeight - 2f * PremiereArt.Pixel), PremiereTimelineArt.ClipPurple, BladeOrder - 1, "Fill");
            PremiereArt.Box(c, new Vector2(-w * 0.5f + Mathf.Min(0.6f, w * 0.3f), 1.5f * PremiereArt.Pixel), new Vector2(Mathf.Min(0.9f, w * 0.5f), PremiereArt.Pixel), Color.white, BladeOrder, "Name");
            PremiereArt.Box(c, new Vector2(0f, -2f * PremiereArt.Pixel), new Vector2(w - 4f * PremiereArt.Pixel, PremiereArt.Pixel), PremiereSkinArt.ClipDark, BladeOrder, "Wave");
            _clips[i] = c;
        }
        for (int i = 0; i < 2; i++)
        {
            _cutTicks[i] = PremiereArt.Box(transform, Vector2.zero, new Vector2(PremiereArt.Pixel, ClipHeight + 4f * PremiereArt.Pixel), PremiereTimelineArt.EnemyRed, BladeOrder, "CutMark");
            _cutTicks[i].transform.position = new Vector3(LineX(i), y, 0f);
        }
    }

    /// <summary>切った所で2つに分かれて、左右へ 2ドット離れる。詰めで、まん中のクリップは閉じる。技が終わったら消える</summary>
    void UpdateClips(float now)
    {
        if (_clips[0] == null) return;
        foreach (var t in _cutTicks) if (t != null) t.enabled = false;
        float k = Mathf.Clamp01((now - _cutAt) / 0.06f);
        float gap = 2f * PremiereArt.Pixel * k;
        float baseY = _clips[1].position.y;
        _clips[0].position = new Vector3((Mathf.Max(_arena.xMin + 0.2f, _center - _half - ClipMargin) + LineX(0)) * 0.5f - gap, baseY, 0f);
        _clips[2].position = new Vector3((LineX(1) + Mathf.Min(_arena.xMax - 0.2f, _center + _half + ClipMargin)) * 0.5f + gap, baseY, 0f);
        if (now >= _deleteAt)
        {
            float closeK = now < _closeAt ? 0f : (_config.CloseTime > 0f ? Mathf.Clamp01((now - _closeAt) / _config.CloseTime) : 1f);
            _clips[1].localScale = new Vector3(Mathf.Max(0.001f, 1f - closeK), 1f, 1f);
            foreach (var sr in _clips[1].GetComponentsInChildren<SpriteRenderer>()) sr.color = Color.Lerp(sr.color, Color.black, 0.25f);
        }
        // 技が終わったら（足場が戻るのを待つ間は）、帯はうすくなって消える
        float fade = now < _doneAt ? 1f : 1f - Mathf.Clamp01((now - _doneAt) / 0.25f);
        for (int i = 0; i < 3; i++)
        {
            if (_clips[i] == null) continue;
            bool on = fade > 0f;
            if (_clips[i].gameObject.activeSelf != on) _clips[i].gameObject.SetActive(on);
            if (!on) continue;
            foreach (var sr in _clips[i].GetComponentsInChildren<SpriteRenderer>())
            {
                Color c = sr.color;
                c.a = fade;
                sr.color = c;
            }
        }
    }
}
