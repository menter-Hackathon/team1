using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ハルシネーションの進行（2026-11-19。docs/app-kits-1-design.md 5章 P1-2）。ChatGptHallucinationAttack が出す。担当3 の物。
/// ウソ（ボスの絵の写し・7割の濃さ）が 1行ずつ書かれて現れる → 本物の赤い線・ウソの灰色の線 → 同時に撃つ → ウソが文字化けして消える。
/// ・ウソは KitProp（1回当てると消える。体当たりなし・ロックオンの相手にならない・ゲージは増えない）
/// ・形態変化（50％）・正常化でこの GameObject ごと消える（ウソ・灰色の弾も子か CustomEffect なので消える）
/// </summary>
public class ChatGptHallucinationEffect : CustomEffect
{
    const int LookOrderShift = -2;
    const int GlitchOrder = 32;
    const float GlitchStep = 0.15f;
    const int RevealSteps = 6;

    class Fake
    {
        public Transform Root;
        public Transform LookRoot;
        public List<SpriteRenderer> Look = new List<SpriteRenderer>();
        public SpriteMask Mask;
        public SpriteRenderer Cursor;
        public KitProp Prop;
        public Vector2 Origin;
        public Rect Body;
        public readonly List<GameObject> Lines = new List<GameObject>();
        public readonly List<Vector2> Dirs = new List<Vector2>();
        public bool Alive = true;
    }

    AttackRunner _owner;
    AttackData _data;
    ChatGptHallucinationAttack _cfg;
    float _start;
    bool _revealed;
    bool _linesShown;
    bool _fired;
    float _nextGlitch;
    int _glitchFrame;
    Vector2 _realOrigin;
    readonly List<Vector2> _realDirs = new List<Vector2>();
    readonly List<Fake> _fakes = new List<Fake>();

    public static ChatGptHallucinationEffect Spawn(AttackRunner owner, AttackData data, ChatGptHallucinationAttack cfg)
    {
        if (owner == null || data == null || cfg == null) return null;
        GameObject go = owner.CreateSpawnedObject("ChatGPTHallucination", owner.transform.position);
        var fx = go.AddComponent<ChatGptHallucinationEffect>();
        fx._owner = owner;
        fx._data = data;
        fx._cfg = cfg;
        fx._start = Time.time;
        fx.Build();
        return fx;
    }

    void Build()
    {
        Transform boss = _owner.transform;
        Bounds body = ChatGptArt.BodyOf(_owner);
        Vector2 bodyOff = (Vector2)(body.center - boss.position);
        Vector2 originOff = _owner.OriginPoint - (Vector2)boss.position;
        // 本物が一瞬だけ文字化けする（「どれが本物？」）
        ChatGptArt.Glitch(new Rect(body.min, body.size), 6, GlitchOrder);

        foreach (float x in PickXs(body))
        {
            var f = new Fake();
            var root = new GameObject("ChatGPTFake");
            root.layer = gameObject.layer;
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(x, boss.position.y, 0f);
            f.Root = root.transform;
            f.Origin = (Vector2)root.transform.position + originOff;
            Vector2 center = (Vector2)root.transform.position + bodyOff;
            f.Body = new Rect(center - (Vector2)body.extents, body.size);

            var look = new GameObject("Look");
            look.layer = root.layer;
            look.transform.SetParent(root.transform, false);
            f.LookRoot = look.transform;
            f.Look = ChatGptArt.CloneLook(boss, look.transform, new Color(1f, 1f, 1f, _cfg.FakeAlpha), false, LookOrderShift);
            foreach (var sr in f.Look) sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            // 1行ずつ書かれて現れる：上から下へ、見える範囲（マスク）を段ごとに広げる
            var maskGo = new GameObject("Reveal");
            maskGo.layer = root.layer;
            maskGo.transform.SetParent(root.transform, false);
            f.Mask = maskGo.AddComponent<SpriteMask>();
            f.Mask.sprite = PlaceholderSprites.Square;
            f.Cursor = FxKit.Box(root.transform, Vector2.zero, new Vector2(body.size.x * 0.6f, ChatGptArt.Px * 2f), ChatGptArt.Green, GlitchOrder - 1, "Cursor");

            // 叩ける置物（当たり判定だけ。絵は写しの方）
            f.Prop = KitProp.Spawn(_owner, center, PlaceholderSprites.Square, body.size * 0.85f, null, 1, 0f);
            if (f.Prop != null)
            {
                f.Prop.transform.SetParent(root.transform, true);
                if (f.Prop.Visual != null) f.Prop.Visual.enabled = false;
                Fake captured = f;
                f.Prop.Broken += _ => OnFakeBroken(captured);
            }
            _fakes.Add(f);
            ApplyReveal(f, 0f);
        }
    }

    /// <summary>ウソの x を選ぶ（部屋に入る所・ボスとほかのウソから離れた所）</summary>
    List<float> PickXs(Bounds body)
    {
        Rect arena = BattleArena.GetBounds();
        float bossX = _owner.transform.position.x;
        float half = body.extents.x;
        var xs = new List<float>();
        float[] offs = _cfg.Offsets ?? new float[0];
        foreach (float off in offs)
        {
            if (xs.Count >= _cfg.FakeCount) break;
            float x = bossX + off;
            if (x - half < arena.xMin + 0.3f || x + half > arena.xMax - 0.3f) continue;
            if (Mathf.Abs(x - bossX) < _cfg.MinGap) continue;
            bool near = false;
            foreach (float t in xs) if (Mathf.Abs(t - x) < _cfg.MinGap) near = true;
            if (near) continue;
            xs.Add(x);
        }
        return xs;
    }

    void Update()
    {
        if (_owner == null)
        {
            Destroy(gameObject);
            return;
        }
        float t = Time.time - _start;
        float appear = _cfg.AppearTime;

        // 現れる
        if (!_revealed)
        {
            float r = Mathf.Clamp01(t / appear);
            foreach (var f in _fakes) if (f.Alive) ApplyReveal(f, r);
            if (t >= appear)
            {
                _revealed = true;
                foreach (var f in _fakes) FinishReveal(f);
            }
        }

        // 予告の線
        if (!_linesShown && t >= appear)
        {
            _linesShown = true;
            ShowLines();
        }

        // 撃つ
        if (!_fired && t >= appear + _cfg.Telegraph)
        {
            _fired = true;
            Fire();
        }

        // 文字化け（0.15秒ごと）
        if (Time.time >= _nextGlitch)
        {
            _nextGlitch = Time.time + GlitchStep;
            _glitchFrame++;
            foreach (var f in _fakes) if (f.Alive) GlitchFake(f);
        }

        if (_fired && t >= appear + _cfg.Telegraph + _cfg.VanishDelay)
        {
            foreach (var f in _fakes) if (f.Alive) Vanish(f);
            Destroy(gameObject);
        }
    }

    void ApplyReveal(Fake f, float ratio)
    {
        if (f.Mask == null) return;
        float step = Mathf.Ceil(ratio * RevealSteps) / RevealSteps;
        float h = Mathf.Max(0.001f, f.Body.height * step + 0.2f);
        float w = f.Body.width + 1f;
        Vector2 native = f.Mask.sprite != null ? (Vector2)f.Mask.sprite.bounds.size : Vector2.one;
        float top = f.Body.yMax + 0.1f;
        f.Mask.transform.position = new Vector3(f.Body.center.x, top - h * 0.5f, 0f);
        f.Mask.transform.localScale = new Vector3(w / Mathf.Max(0.001f, native.x), h / Mathf.Max(0.001f, native.y), 1f);
        if (f.Cursor != null)
        {
            f.Cursor.transform.position = new Vector3(f.Body.center.x, top - h, 0f);
            f.Cursor.enabled = step < 1f;
        }
    }

    void FinishReveal(Fake f)
    {
        foreach (var sr in f.Look) if (sr != null) sr.maskInteraction = SpriteMaskInteraction.None;
        if (f.Mask != null) Destroy(f.Mask.gameObject);
        if (f.Cursor != null) Destroy(f.Cursor.gameObject);
        f.Mask = null;
        f.Cursor = null;
    }

    void GlitchFake(Fake f)
    {
        // 濃さを揺らし、1ドット横にずらし、小さい四角を散らす
        bool odd = _glitchFrame % 2 == 0;
        float a = _cfg.FakeAlpha * (odd ? 0.75f : 1f);
        foreach (var sr in f.Look)
        {
            if (sr == null) continue;
            Color c = sr.color;
            c.a = a;
            sr.color = c;
        }
        if (f.LookRoot != null) f.LookRoot.localPosition = new Vector3(odd ? ChatGptArt.Px : -ChatGptArt.Px, 0f, 0f);
        ChatGptArt.Glitch(f.Body, 2, GlitchOrder);
    }

    // ───────── 線と弾 ─────────

    void ShowLines()
    {
        Rect arena = BattleArena.GetBounds();
        _realOrigin = _owner.OriginPoint;
        Aim(_realOrigin, _realDirs);
        foreach (var d in _realDirs) KitDress.Line(_owner, _realOrigin, _realOrigin + d * RayLength(arena, _realOrigin, d), _cfg.Telegraph);
        Color gray = new Color(ChatGptArt.Gray.r, ChatGptArt.Gray.g, ChatGptArt.Gray.b, 0.8f);
        foreach (var f in _fakes)
        {
            if (!f.Alive) continue;
            Aim(f.Origin, f.Dirs);
            foreach (var d in f.Dirs)
            {
                GameObject line = KitDress.Line(_owner, f.Origin, f.Origin + d * RayLength(arena, f.Origin, d), _cfg.Telegraph, 2f, gray);
                if (line != null) f.Lines.Add(line);
            }
        }
    }

    void Aim(Vector2 origin, List<Vector2> into)
    {
        into.Clear();
        Vector2 baseDir = new Vector2(_owner.Facing, 0f);
        if (_owner.TargetPoint is Vector2 target)
        {
            Vector2 d = target - origin;
            if (d.sqrMagnitude > 0.0001f) baseDir = d.normalized;
        }
        int n = Mathf.Max(1, _cfg.Shots);
        for (int i = 0; i < n; i++)
        {
            float ang = n > 1 ? -_cfg.Spread * 0.5f + _cfg.Spread * i / (n - 1) : 0f;
            into.Add(Quaternion.Euler(0f, 0f, ang) * baseDir);
        }
    }

    void Fire()
    {
        for (int i = 0; i < _realDirs.Count; i++)
        {
            Projectile p = Projectile.Spawn(_owner, _data, _realOrigin, _realDirs[i], i);
            if (p != null) KitDress.Swap(p.gameObject, ChatGptArt.Token);
        }
        foreach (var f in _fakes)
        {
            if (!f.Alive) continue;
            foreach (var d in f.Dirs) ChatGptFakeBullet.Spawn(_owner, f.Origin, d, _data.Speed, _data.Lifetime, _data.HitSize);
        }
    }

    /// <summary>origin から dir へ、部屋の端までの長さ（最大 30）</summary>
    static float RayLength(Rect r, Vector2 o, Vector2 d)
    {
        float t = 30f;
        if (d.x > 0.0001f) t = Mathf.Min(t, (r.xMax - o.x) / d.x);
        if (d.x < -0.0001f) t = Mathf.Min(t, (r.xMin - o.x) / d.x);
        if (d.y > 0.0001f) t = Mathf.Min(t, (r.yMax - o.y) / d.y);
        if (d.y < -0.0001f) t = Mathf.Min(t, (r.yMin - o.y) / d.y);
        return Mathf.Max(1f, t);
    }

    // ───────── 消える ─────────

    void OnFakeBroken(Fake f)
    {
        if (!f.Alive) return;
        Vanish(f);
    }

    void Vanish(Fake f)
    {
        f.Alive = false;
        ChatGptArt.Glitch(f.Body, 10, GlitchOrder);
        foreach (var l in f.Lines) if (l != null) Destroy(l);
        f.Lines.Clear();
        if (f.Root != null) Destroy(f.Root.gameObject);
    }
}
