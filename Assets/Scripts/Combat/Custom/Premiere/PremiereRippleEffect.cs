using System.Collections.Generic;
using UnityEngine;
using K = GenericFrameKit;

/// <summary>
/// 【仮】リップル削除の本体（2026-11-19。docs/app-feedback-v2-design.md 5-5）。PremiereRippleDelete が出し、あとは自分で進む。
/// 枠と斜線（0.2秒）→ 削除（判定 0.1秒・敵の弾を消す・黒く抜ける）→ 詰め（黒い区間がプレイヤーの側へ閉じる 0.15秒・先の雑魚が手前へすべる）→
/// つなぎ目の白い線（0.3秒）。
/// 雑魚をすべらせる間は、雑魚の絵を少しつぶし、後ろに残像を2枚。消えるとき（Restore）に、つぶした絵を必ず戻す
/// </summary>
public class PremiereRippleEffect : CustomEffect
{
    class Pulled
    {
        public Health Health;
        public Transform Root;
        public KinematicBody2D Body;
        public Rigidbody2D Rb;
        public Vector2 From, To;
        public SpriteRenderer Art;
        public Vector3 ArtScale;
        public int Ghosts;
    }

    const int HatchOrder = 27, FrameOrder = 28, BladeOrder = 29, ColumnOrder = 9, SeamOrder = 10;
    const float SeamTime = 0.3f;

    AttackRunner _owner;
    PremiereRippleDelete _config;
    Rect _rect;
    int _facing;
    float _start, _deleteAt, _closeAt, _endAt;
    bool _deleted, _closing;
    Hitbox _hitbox;
    SpriteRenderer _hatch, _frame, _checker, _seam;
    Transform _blade;
    readonly List<Pulled> _pulled = new List<Pulled>();

    public static PremiereRippleEffect Spawn(AttackRunner owner, AttackData data, PremiereRippleDelete config, Rect rect, int facing)
    {
        GameObject go = owner.CreateSpawnedObject("PremiereRipple", rect.center);
        var e = go.AddComponent<PremiereRippleEffect>();
        e._owner = owner;
        e._config = config;
        e._rect = rect;
        e._facing = facing >= 0 ? 1 : -1;
        e._start = Time.time;
        e._deleteAt = e._start + config.MarkTime;
        e._closeAt = e._deleteAt + config.DeleteTime;
        e._endAt = e._closeAt + config.CloseTime;
        e.Build(data);
        return e;
    }

    float Near => _facing > 0 ? _rect.xMin : _rect.xMax;
    float Far => _facing > 0 ? _rect.xMax : _rect.xMin;

    void Build(AttackData data)
    {
        // 枠と斜線（プレイヤーの物なので、白と紫。赤は使わない）
        _hatch = PremiereTimelineArt.Tiled(transform, PremiereTimelineArt.PurpleHatch, Vector2.zero, _rect.size, Color.white, HatchOrder, "Hatch");
        _frame = RuntimeSprites.Show(transform, PremiereArt.DashedFrame(_rect.width, _rect.height), Vector2.zero, FrameOrder, 1f, "Frame");
        _blade = RuntimeSprites.Show(transform, PremiereArt.RazorBlade, new Vector2(0f, _rect.height * 0.5f), BladeOrder, 0.6f, "Blade").transform;

        // 黒く抜ける区間（暗い市松）
        _checker = PremiereTimelineArt.Tiled(transform, PremiereTimelineArt.Checker, Vector2.zero, _rect.size, Color.white, ColumnOrder, "Deleted");
        _checker.enabled = false;

        // 判定（区間の全部。攻撃データのダメージ・ゲージ）
        if (_owner != null && data != null)
        {
            var hitGo = new GameObject("RippleHit");
            hitGo.layer = gameObject.layer;
            hitGo.transform.SetParent(transform, false);
            var rb = hitGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
            var box = hitGo.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = _rect.size;
            _hitbox = _owner.AddHitbox(hitGo, data, box);
        }
    }

    void Update()
    {
        float now = Time.time;
        // ── 枠と斜線。刃が上から落ちる ──
        if (now < _deleteAt)
        {
            float k = _config.MarkTime > 0f ? Mathf.Clamp01((now - _start) / _config.MarkTime) : 1f;
            _blade.localPosition = new Vector3(0f, Mathf.Lerp(_rect.height * 0.5f, -_rect.height * 0.5f + 0.3f, k * k), 0f);
            return;
        }

        // ── 削除：判定・弾を消す・黒く抜ける ──
        if (!_deleted)
        {
            _deleted = true;
            _hatch.enabled = false;
            _frame.enabled = false;
            _blade.gameObject.SetActive(false);
            _checker.enabled = true;
            if (_hitbox != null) _hitbox.Activate();
            ClearBullets();
            PickPulled();
        }
        if (now < _closeAt) return;

        // ── 詰め：黒い区間がプレイヤーの側へ閉じる。先の雑魚が手前へすべる ──
        if (!_closing)
        {
            _closing = true;
            if (_hitbox != null) _hitbox.Deactivate();
        }
        float ck = _config.CloseTime > 0f ? Mathf.Clamp01((now - _closeAt) / _config.CloseTime) : 1f;
        float ease = 1f - (1f - ck) * (1f - ck);
        float w = _rect.width * (1f - ease);
        PremiereTimelineArt.SetTiledSize(_checker, new Vector2(Mathf.Max(0.001f, w), _rect.height));
        _checker.transform.position = new Vector3(Near + _facing * w * 0.5f, _rect.center.y, 0f);
        MovePulled(ease);
        if (ck < 1f) return;

        // ── つなぎ目の白い線 ──
        if (_seam == null)
        {
            _checker.enabled = false;
            EndPull();
            _seam = PremiereArt.Box(transform, Vector2.zero, new Vector2(PremiereArt.Pixel, _rect.height), Color.white, SeamOrder, "Seam");
            _seam.transform.position = new Vector3(Near, _rect.center.y, 0f);
        }
        float left = _endAt + SeamTime - now;
        K.SetAlpha(_seam, Mathf.Clamp01(left / 0.15f));
        if (left <= 0f) Destroy(gameObject);
    }

    // ───────── 弾を消す ─────────

    static readonly Collider2D[] Buffer = new Collider2D[48];

    void ClearBullets()
    {
        if (_owner == null) return;
        var filter = new ContactFilter2D { useTriggers = true };
        int n = Physics2D.OverlapBox(_rect.center, _rect.size, 0f, filter, Buffer);
        var done = new HashSet<GameObject>();
        for (int i = 0; i < n; i++)
        {
            Collider2D col = Buffer[i];
            if (col == null) continue;
            GameObject target = null;
            Projectile p = col.GetComponentInParent<Projectile>();
            if (p != null)
            {
                Hitbox hb = p.GetComponent<Hitbox>();
                bool enemy = hb != null ? hb.Team != _owner.Team : p.Owner != null && p.Owner.Team != _owner.Team;
                if (enemy) target = p.gameObject;
            }
            else
            {
                FinderZippable z = col.GetComponentInParent<FinderZippable>();
                if (z != null && z.isActiveAndEnabled) target = z.gameObject;
            }
            if (target == null || !done.Add(target)) continue;
            K.Shards(target.transform.position, Vector2.up, 3, j => PremiereSkinArt.Film8, j => Color.white, 60f, 0.8f);
            target.SetActive(false);
            Destroy(target);
        }
    }

    // ───────── 引き寄せ ─────────

    void PickPulled()
    {
        if (_owner == null) return;
        float x0 = Far, x1 = Far + _facing * _config.PullRange;
        var a = new Vector2(Mathf.Min(x0, x1), _rect.yMin - 1f);
        var b = new Vector2(Mathf.Max(x0, x1), _rect.yMax + 1f);
        var filter = new ContactFilter2D { useTriggers = true };
        var hits = new List<Collider2D>();
        Physics2D.OverlapArea(a, b, filter, hits);
        var seen = new HashSet<Health>();
        BossController[] bosses = FindObjectsOfType<BossController>();
        foreach (Collider2D col in hits)
        {
            if (col == null || !col.TryGetComponent(out Hurtbox hurt)) continue;
            if (!(hurt.Owner is Health h) || h == null || h.IsDead || h.Team == _owner.Team || !seen.Add(h)) continue;
            if (h.GetComponentInParent<BossController>() != null) continue; // ボスの体
            bool virus = false;
            foreach (var boss in bosses) if (boss != null && boss.EjectedVirus == h) virus = true;
            if (virus) continue;
            if (h.GetComponent<PlayerController>() != null) continue;
            AddPulled(h, hurt);
        }
    }

    void AddPulled(Health h, Hurtbox hurt)
    {
        var p = new Pulled { Health = h, Root = h.transform, Body = h.GetComponent<KinematicBody2D>(), Rb = h.GetComponent<Rigidbody2D>() };
        p.From = p.Root.position;
        Bounds body = hurt.Collider != null ? hurt.Collider.bounds : new Bounds(p.From + Vector2.up * 0.5f, Vector3.one);
        float dist = SafeDistance(p, body);
        if (dist <= 0.05f) return;
        p.To = p.From + new Vector2(-_facing * dist, 0f);
        p.Art = MainArt(h.gameObject);
        if (p.Art != null) p.ArtScale = p.Art.transform.localScale;
        _pulled.Add(p);
    }

    /// <summary>壁と穴を越えない距離（引き寄せる向き＝プレイヤーの側）</summary>
    float SafeDistance(Pulled p, Bounds body)
    {
        float want = _config.PullDistance;
        Vector2 dir = new Vector2(-_facing, 0f);
        float half = body.extents.x;
        // 壁
        RaycastHit2D wall = Physics2D.Raycast(body.center, dir, want + half, _owner.SolidMask);
        if (wall.collider != null) want = Mathf.Min(want, Mathf.Max(0f, wall.distance - half - 0.05f));
        // 穴（床を歩く雑魚だけ）
        if (p.Body != null)
        {
            float feet = body.min.y;
            float safe = 0f;
            for (float d = 0.2f; d <= want + 0.001f; d += 0.2f)
            {
                Vector2 probe = new Vector2(body.center.x + dir.x * d, feet + 0.1f);
                RaycastHit2D ground = Physics2D.Raycast(probe, Vector2.down, 1f, _owner.GroundMask);
                if (ground.collider == null) break;
                safe = d;
            }
            want = Mathf.Min(want, safe);
        }
        return want;
    }

    void MovePulled(float k)
    {
        foreach (var p in _pulled)
        {
            if (p.Health == null || p.Root == null || p.Health.IsDead) continue;
            Vector2 pos = Vector2.Lerp(p.From, p.To, k);
            Vector2 now = p.Root.position;
            pos.y = now.y; // 縦はそのまま（落ちる・浮くのはその雑魚の動き）
            if (p.Body != null) p.Body.Warp(pos);
            else
            {
                if (p.Rb != null) p.Rb.position = pos;
                p.Root.position = pos;
            }
            // つぶれる・残像
            if (p.Art != null) p.Art.transform.localScale = new Vector3(p.ArtScale.x * 1.12f, p.ArtScale.y * 0.88f, p.ArtScale.z);
            if (p.Art != null && p.Ghosts < 2 && k >= p.Ghosts * 0.4f)
            {
                p.Ghosts++;
                Ghost(p.Art);
            }
        }
    }

    void EndPull()
    {
        foreach (var p in _pulled)
        {
            if (p.Art != null) p.Art.transform.localScale = p.ArtScale;
        }
        _pulled.Clear();
    }

    protected override void Restore() => EndPull();

    static SpriteRenderer MainArt(GameObject go)
    {
        SpriteRenderer best = null;
        float area = 0f;
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr == null || !sr.enabled || sr.sprite == null || sr.transform == go.transform) continue;
            Vector3 s = sr.bounds.size;
            if (best == null || s.x * s.y > area)
            {
                best = sr;
                area = s.x * s.y;
            }
        }
        return best;
    }

    static void Ghost(SpriteRenderer src)
    {
        Transform t = K.Loose("Gen4_RippleGhost", src.transform.position);
        t.localScale = src.transform.lossyScale;
        var sr = t.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = src.sprite;
        sr.flipX = src.flipX;
        sr.sortingOrder = src.sortingOrder - 1;
        sr.color = new Color(0.75f, 0.7f, 1f, 0.45f);
        GenericFrameTweens.Of(t.gameObject).Alpha(sr, 0.45f, 0f, 0.15f).Kill(0.16f);
    }
}
