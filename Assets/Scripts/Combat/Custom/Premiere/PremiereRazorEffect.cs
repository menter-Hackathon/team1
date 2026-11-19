using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// レーザーツールの本体（2026-11-18。docs/app-boss-skills-design.md 3-3）。PremiereRazor が出し、あとは自分で進む。場に残る技の効果（Lingers）。
/// ・予告（刃・点線・斜線）→ 切る（2本の線に判定）→ 削除（区間が黒くなって閉じる。区間の中の足場を消す。ボスが寄る）→ 足場が戻るまで残る
/// ・消す足場：AttackRunner の「上に乗れる地形」のうち、すり抜け床（OneWayPlatform）のレイヤーで、真ん中が区間の中にある物。
///   部屋の床（部屋の幅の半分以上ある物）・落下物の足場・開いたり閉じたりする足場（BlinkingPlatform）は消さない
/// ・消えた足場は当たり判定と絵を消し、点線の枠だけを残す。最後の1秒は点滅して戻る
/// ・形態変化・正常化で消されたとき、自分で終わったときは、Restore で足場をすぐ戻す。寄ったボスはそのまま
/// ・画面のゆれ：削除の瞬間に StageCamera.Shake で画面をゆらす。黒い帯と刃も少しゆらす【仮】
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
    const int GhostOrder = 6, HatchOrder = 7, BlackOrder = 40, DottedOrder = 41, FlashOrder = 42, BladeOrder = 43;

    AttackRunner _owner;
    PremiereRazor _config;
    Rect _arena;
    float _center;
    float _half;
    float _top;        // 刃の上の端
    float _start;
    float _cutAt;
    float _deleteAt;
    float _doneAt;
    float _returnAt = float.PositiveInfinity;
    bool _cut;
    bool _deleted;
    readonly List<Removed> _removed = new List<Removed>();

    readonly Transform[] _blades = new Transform[2];
    readonly SpriteRenderer[] _dotted = new SpriteRenderer[2];
    readonly SpriteRenderer[] _flashes = new SpriteRenderer[2];
    SpriteRenderer _hatch;
    SpriteRenderer _black;
    Transform _ghosts;

    BossController _boss;
    float _bossFromX;
    float _bossToX;
    bool _approach;

    public override bool Lingers => true;
    /// <summary>切って、ボスが寄り終えたら true（技はここまで待つ。足場が戻るまでは場に残る）</summary>
    public bool ActionDone { get; private set; }

    public static PremiereRazorEffect Spawn(AttackRunner owner, PremiereRazor config, float centerX)
    {
        Rect arena = BattleArena.GetBounds();
        GameObject go = owner.CreateSpawnedObject("PremiereRazor", new Vector2(centerX, arena.center.y));
        var e = go.AddComponent<PremiereRazorEffect>();
        e._owner = owner;
        e._config = config;
        e._arena = arena;
        e._center = centerX;
        e._half = config.SectionWidth * 0.5f;
        e._top = arena.yMax - config.TopInset;
        e._start = Time.time;
        e._cutAt = e._start + config.TelegraphTime;
        e._deleteAt = e._cutAt + config.CutTime;
        e._doneAt = e._deleteAt + config.DeleteTime;
        e._boss = owner.GetComponent<BossController>();
        e.BuildTelegraph();
        e.Tick(Time.time);
        return e;
    }

    void Update() => Tick(Time.time);

    void Tick(float now)
    {
        float floor = _arena.yMin;

        // ── 予告：刃・点線・斜線。最後の0.3秒は点線を点滅 ──
        if (now < _cutAt)
        {
            bool blink = _cutAt - now < 0.3f && Mathf.FloorToInt(now / 0.06f) % 2 == 1;
            foreach (var d in _dotted) d.enabled = !blink;
            return;
        }

        // ── 切る：刃が上から下へ落ち、白い光が走る。2本の線に判定 ──
        if (!_cut)
        {
            _cut = true;
            for (int i = 0; i < 2; i++)
            {
                float x = LineX(i);
                if (_owner != null && _config.Line != null) AreaStrike.Spawn(_owner, _config.Line, new Vector2(x, floor + 0.5f), Vector2.up, 0f);
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
            _blades[i].position = new Vector3(x + Shake(now, i), bladeY, 0f);
            float h = span * cutK;
            SetBox(_flashes[i], new Vector2(x, _top - h * 0.5f), new Vector2(FlashWidth, Mathf.Max(0.01f, h)));
            float fade = now < _deleteAt ? 1f : 1f - Mathf.Clamp01((now - _deleteAt) / FlashFade);
            _flashes[i].color = new Color(1f, 1f, 1f, fade);
        }
        if (now < _deleteAt) return;

        // ── 削除：区間が黒くなって左右から閉じる。足場を消し、ボスが寄る ──
        if (!_deleted)
        {
            _deleted = true;
            _hatch.enabled = false;
            foreach (var b in _blades) b.gameObject.SetActive(false);
            _black.enabled = true;
            RemovePlatforms();
            BeginApproach();
            StageCamera.Shake(0.12f, 0.3f); // 画面が一瞬、横にゆれる（2026-11-18：カメラのゆれの入口を足した）
            _returnAt = now + _config.RemoveTime;
        }
        float delK = _config.DeleteTime > 0f ? Mathf.Clamp01((now - _deleteAt) / _config.DeleteTime) : 1f;
        if (delK < 1f)
        {
            float w = _config.SectionWidth * (1f - delK);
            SetBox(_black, new Vector2(_center + Shake(now, 2), (floor + _top) * 0.5f), new Vector2(Mathf.Max(0.01f, w), span));
        }
        else _black.enabled = false;
        UpdateApproach(delK);
        if (now < _doneAt) return;

        // ── ここで技は終わり。足場が戻るまで残る ──
        if (!ActionDone)
        {
            ActionDone = true;
            foreach (var f in _flashes) f.enabled = false;
            if (_removed.Count == 0)
            {
                Destroy(gameObject);
                return;
            }
        }

        // 最後の1秒は点滅して戻る予告
        if (now >= _returnAt)
        {
            Destroy(gameObject); // Restore で足場を戻す
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

    float Shake(float now, int salt)
    {
        // 【仮】画面のゆれの代わり：削除の間だけ、刃と黒い帯を横に少しゆらす
        if (now < _deleteAt || now > _doneAt) return 0f;
        return (Mathf.PerlinNoise(now * 40f, salt * 3.7f) - 0.5f) * 2f * ShakePixels * PremiereArt.Pixel;
    }

    static void SetBox(SpriteRenderer sr, Vector2 worldCenter, Vector2 size)
    {
        sr.transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
        Vector2 native = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(size.x / Mathf.Max(native.x, 0.001f), size.y / Mathf.Max(native.y, 0.001f), 1f);
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

    protected override void Restore()
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
    }

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

        // 切られる区間：うすい紫の斜線（床から刃の下まで）
        _hatch = RuntimeSprites.Show(transform, PremiereArt.Hatch(_config.SectionWidth, dottedLen), Vector2.zero, HatchOrder, 1f, "Hatch");
        _hatch.transform.position = new Vector3(_center, floor + dottedLen * 0.5f, 0f);

        for (int i = 0; i < 2; i++)
        {
            float x = LineX(i);
            _blades[i] = RuntimeSprites.Show(transform, PremiereArt.RazorBlade, Vector2.zero, BladeOrder, 1f, "Blade").transform;
            _blades[i].position = new Vector3(x, _top - BladeHeight * 0.5f, 0f);
            _dotted[i] = RuntimeSprites.Show(transform, PremiereArt.DottedColumn(dottedLen), Vector2.zero, DottedOrder, 1f, "CutLine");
            _dotted[i].transform.position = new Vector3(x, floor + dottedLen * 0.5f, 0f);
            _flashes[i] = PremiereArt.Box(transform, Vector2.zero, new Vector2(FlashWidth, 0.01f), Color.white, FlashOrder, "CutFlash");
            _flashes[i].enabled = false;
        }

        _black = PremiereArt.Box(transform, Vector2.zero, new Vector2(_config.SectionWidth, 1f), Color.black, BlackOrder, "Delete");
        _black.enabled = false;
    }
}
