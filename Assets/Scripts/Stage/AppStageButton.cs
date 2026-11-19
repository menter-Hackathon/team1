using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// button：乗れない。プレイヤーの攻撃（近接・能力の Hitbox）が当たると押せる（KitProp と同じ、重なりを自分で見るやり方）。
/// clear_bullets：画面の敵の弾を消す／stun_boss：戦っているボスを 1.5 秒ひるませる／refill：能力のクールタイムを全部戻す
/// 2026-11-19（遊んだ感想「押した時の効果がよくわからない」）：文字は使わず、絵で分かるようにした
/// ・どのアプリでも同じ印：右上の丸いバッジ（水色＝弾に斜線／黄色＝いなずま／緑＝回る矢印）
/// ・押せる間：ゆっくり大きく小さく・2秒おきにきらり。プレイヤーが近い（2.5マス）と白いふち（「たたける」）
/// ・押したとき（効果ごとに違う）：
///   弾を消す＝水色の輪が画面いっぱいに広がり、消えた弾の場所が水色にはじける／
///   ボスをひるませる＝ボタンからボスへ黄色いいなずま・ボスが光る・ボスの上を黄色い星3つが回る（ひるんでいる間）／
///   クールタイムを戻す＝緑の光がボタンからプレイヤーへ飛び、プレイヤーに緑の輪と skill_ready の音
/// ・待つ間：灰色＋時計のような影（時間がたつと減る）・バッジはうすく。戻るとき光って ui_button_ready
/// ・ボスがいない（戦っていない）ときのひるませるボタン：押しても使わない（小さく横にふるえるだけ）
/// </summary>
public class AppStageButton : MonoBehaviour
{
    const float StunSeconds = 1.5f;
    const float FlashTime = 0.15f;
    const float NearDistance = 2.5f;
    const float GlintEvery = 2f;
    const float NoShakeTime = 0.3f;
    const float RehitGuard = 0.4f;
    const int PieSteps = 16;
    static readonly Color Cooling = new Color(0.5f, 0.5f, 0.53f, 0.85f);
    public static readonly Color Cyan = RuntimeSprites.Hex("34D6F0");
    public static readonly Color Yellow = RuntimeSprites.Hex("FFD60A");
    public static readonly Color Green = RuntimeSprites.Hex("30D158");

    enum Effect { ClearBullets, StunBoss, Refill }

    BoxCollider2D _area;
    Transform _look;
    SpriteRenderer _art, _badge, _pie;
    readonly List<SpriteRenderer> _outline = new List<SpriteRenderer>();
    Color _baseColor = Color.white;
    Effect _effect;
    Vector2 _size;
    int _order, _pieSize;
    float _cooldown, _readyAt, _flashUntil, _noUntil, _nextGlint, _guardUntil, _findPlayerAt;
    bool _ready = true;
    Transform _player;
    readonly Collider2D[] _results = new Collider2D[16];
    static readonly List<Projectile> Bullets = new List<Projectile>();

    Color EffectColor => _effect == Effect.StunBoss ? Yellow : _effect == Effect.Refill ? Green : Cyan;

    internal static AppStageButton Create(Transform parent, string name, Vector2 center, Vector2 size, Sprite sprite, Color color, int order, AppStageParams p)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = Vector2.Max(size, new Vector2(0.6f, 0.6f)); // 小さいボタンでも当てやすく
        var b = go.AddComponent<AppStageButton>();
        b._area = col;
        b._size = size;
        b._order = order;
        b._look = new GameObject("Look").transform;
        b._look.SetParent(go.transform, false);
        b._art = AttackRunner.AddVisual(b._look, sprite, color, size, order, "Art");
        b._baseColor = color;
        b._effect = ParseEffect(p != null ? p.effect : null);
        b._cooldown = p != null ? AppStageParams.Pos(p.cooldown, 8f, 0.5f) : 8f;
        b.BuildLook();
        return b;
    }

    static Effect ParseEffect(string s)
    {
        s = string.IsNullOrEmpty(s) ? "" : s.Trim().ToLowerInvariant();
        if (s == "stun_boss") return Effect.StunBoss;
        if (s == "refill") return Effect.Refill;
        return Effect.ClearBullets;
    }

    void BuildLook()
    {
        // 右上の丸いバッジ（9ドット）
        _badge = RuntimeSprites.Show(_look, BadgeSprite(_effect), new Vector2(_size.x * 0.5f - 0.05f, _size.y * 0.5f - 0.05f), _order + 2, 1f, "Badge");
        // 近いときの白いふち（4本の線）
        const float t = 0.07f, gap = 0.06f;
        float w = _size.x + gap * 2f, h = _size.y + gap * 2f;
        _outline.Add(FxKit.Box(_look, new Vector2(0f, h * 0.5f + t * 0.5f), new Vector2(w + t * 2f, t), Color.white, _order + 1, "Outline"));
        _outline.Add(FxKit.Box(_look, new Vector2(0f, -h * 0.5f - t * 0.5f), new Vector2(w + t * 2f, t), Color.white, _order + 1, "Outline"));
        _outline.Add(FxKit.Box(_look, new Vector2(-w * 0.5f - t * 0.5f, 0f), new Vector2(t, h), Color.white, _order + 1, "Outline"));
        _outline.Add(FxKit.Box(_look, new Vector2(w * 0.5f + t * 0.5f, 0f), new Vector2(t, h), Color.white, _order + 1, "Outline"));
        foreach (var o in _outline) o.enabled = false;
        // 待つ間の時計の影
        _pieSize = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(_size.x, _size.y) * RuntimeSprites.PixelsPerUnit * 0.85f), 10, 24);
        _pie = RuntimeSprites.Show(transform, PieSprite(_pieSize, PieSteps), Vector2.zero, _order + 3, 1f, "Clock");
        _pie.enabled = false;
        _nextGlint = Time.time + 0.6f;
    }

    // ───────── 押す ─────────

    void FixedUpdate()
    {
        if (!_ready || _area == null || !_area.enabled || Time.time < _guardUntil) return;
        var filter = new ContactFilter2D { useTriggers = true };
        filter.NoFilter();
        int n = _area.OverlapCollider(filter, _results);
        for (int i = 0; i < n; i++)
        {
            Collider2D c = _results[i];
            if (c == null) continue;
            Hitbox h = c.GetComponent<Hitbox>();
            if (h == null) h = c.GetComponentInParent<Hitbox>();
            if (h == null || h.Team != Team.Player || !h.IsActive) continue;
            TryPress();
            return;
        }
    }

    void TryPress()
    {
        float now = Time.time;
        _guardUntil = now + RehitGuard;
        BossController boss = null;
        if (_effect == Effect.StunBoss)
        {
            boss = KitBoss.Current;
            if (!KitBoss.IsFighting(boss))
            {
                _noUntil = now + NoShakeTime; // ボスがいない：使わない。小さくふるえるだけ
                return;
            }
        }
        _ready = false;
        _readyAt = now + _cooldown;
        _flashUntil = now + FlashTime;
        GameAudio.Event("ui_button");
        try
        {
            switch (_effect)
            {
                case Effect.StunBoss: StunBoss(boss); break;
                case Effect.Refill: Refill(); break;
                default: ClearBullets(); break;
            }
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    // 弾を消す：消える弾の場所を先に覚えてから消し、そこを水色にはじけさせる。ボタンから水色の輪が画面いっぱいに広がる
    void ClearBullets()
    {
        Rect v = ScreenUi.ViewRect();
        float radius = Mathf.Max(v.width, v.height);
        var points = new List<Vector2>();
        Bullets.Clear();
        Bullets.AddRange(FindObjectsOfType<Projectile>());
        foreach (Projectile p in Bullets)
        {
            if (p == null || p.Owner == null || p.Owner.Team != Team.Enemy) continue;
            if (((Vector2)p.transform.position - v.center).sqrMagnitude > radius * radius) continue;
            points.Add(p.transform.position);
        }
        Bullets.Clear();
        KitBullets.ClearEnemyProjectiles(v.center, radius);
        foreach (Vector2 pt in points)
        {
            FxKit.Ring(pt, 0.9f, 0.25f, Cyan);
            FxAnim.Sprite(FxArt.Twinkle, pt, FxKit.OrderRing, 0.25f, Cyan, 1f, "BulletPop").Pop();
        }
        Vector2 from = transform.position;
        float far = 0f;
        far = Mathf.Max(far, Vector2.Distance(from, new Vector2(v.xMin, v.yMin)));
        far = Mathf.Max(far, Vector2.Distance(from, new Vector2(v.xMax, v.yMin)));
        far = Mathf.Max(far, Vector2.Distance(from, new Vector2(v.xMin, v.yMax)));
        far = Mathf.Max(far, Vector2.Distance(from, new Vector2(v.xMax, v.yMax)));
        const int ringPx = 40;
        Sprite[] frames = FxArt.RingFrames(ringPx);
        float ringRadius = ringPx / RuntimeSprites.PixelsPerUnit;
        const float startScale = 0.2f;
        float endScale = (far + 0.5f) / ringRadius;
        FxAnim.Sprite(frames[frames.Length - 1], from, FxKit.OrderRing, 0.55f, Cyan, startScale, "Shockwave")
            .Grow(endScale / startScale).FadeFrom(0.5f);
        FxKit.Ring(from, 2.2f, 0.3f, Color.white);
        ScreenFx.WhiteFlash(0.15f, 0.06f);
    }

    // ボスをひるませる：ボタンからボスへ黄色いいなずま、ボスが光り、ボスの上を黄色い星が回る
    void StunBoss(BossController boss)
    {
        KitBoss.Stun(boss, StunSeconds);
        Vector2 from = transform.position;
        Bounds body = boss.BodyBounds;
        Vector2 to = body.center;
        Lightning(from, to);
        FlashOverlay.Play(boss.gameObject, Yellow, Color.white, 0.3f);
        FxKit.Ring(to, 2.6f, 0.3f, Yellow);
        FxKit.Ring(from, 1.6f, 0.25f, Yellow);
        AppStunStars.Attach(boss, StunSeconds);
    }

    void Lightning(Vector2 from, Vector2 to)
    {
        FxAnim bolt = FxAnim.Create("ButtonLightning", from, 0.3f);
        Vector2 d = to - from;
        float len = d.magnitude;
        if (len < 0.1f) return;
        Vector2 perp = new Vector2(-d.y, d.x) / len;
        int n = Mathf.Clamp(Mathf.RoundToInt(len / 0.7f), 4, 24);
        var pts = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float k = i / (float)n;
            float jitter = i == 0 || i == n ? 0f : UnityEngine.Random.Range(-0.35f, 0.35f);
            pts[i] = d * k + perp * jitter; // bolt の中の位置
        }
        for (int i = 0; i < n; i++)
        {
            Vector2 a = pts[i], b = pts[i + 1], mid = (a + b) * 0.5f;
            float segLen = Vector2.Distance(a, b) + 0.06f;
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            SpriteRenderer outer = FxKit.Box(bolt.transform, mid, new Vector2(segLen, 0.14f), Yellow, FxKit.OrderRing, "Bolt");
            outer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            SpriteRenderer core = FxKit.Box(bolt.transform, mid, new Vector2(segLen, 0.05f), Color.white, FxKit.OrderRing + 1, "Core");
            core.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
        bolt.FadeFrom(0.4f).Refresh();
    }

    // クールタイムを戻す：緑の光がボタンからプレイヤーへ飛び、着いたらプレイヤーに緑の輪と音
    void Refill()
    {
        KitPlayer.RefillCooldowns();
        Transform pl = FindPlayer();
        Vector2 from = transform.position;
        FxKit.Ring(from, 1.6f, 0.25f, Green);
        if (pl == null) { GameAudio.Event("skill_ready"); return; }
        const int count = 7;
        const float fly = 0.35f;
        for (int i = 0; i < count; i++) AppFlyToTarget.Spawn(FxArt.Twinkle, from, pl, Green, i * 0.035f, fly, UnityEngine.Random.Range(0.8f, 2f) * (i % 2 == 0 ? 1f : -1f));
        StartCoroutine(RefillArrive(pl, fly + count * 0.035f));
    }

    IEnumerator RefillArrive(Transform pl, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (pl == null) yield break;
        Vector2 c = (Vector2)pl.position + Vector2.up * 0.5f;
        FxKit.Ring(c, 2f, 0.35f, Green);
        FxKit.Ring(c, 1.1f, 0.25f, Color.white, 0.05f);
        GameAudio.Event("skill_ready");
    }

    // ───────── 見た目 ─────────

    void Update()
    {
        float now = Time.time;
        if (!_ready && now >= _readyAt)
        {
            _ready = true;
            _flashUntil = now + FlashTime;
            _nextGlint = now + 0.5f;
            GameAudio.Event("ui_button_ready");
            FxKit.Ring(transform.position, Mathf.Max(_size.x, _size.y) + 0.6f, 0.3f, EffectColor);
        }
        if (_look != null)
        {
            float s = _ready ? 1f + 0.03f * (1f + Mathf.Sin(now * 4.5f)) : 1f; // 1.0 ↔ 1.06
            float shake = now < _noUntil ? Mathf.Sin(now * 70f) * 0.06f : 0f;
            _look.localScale = new Vector3(s, s, 1f);
            _look.localPosition = new Vector3(shake, 0f, 0f);
        }
        if (_art != null)
        {
            Color target = now < _flashUntil ? Color.white : _ready ? _baseColor : _baseColor * Cooling;
            if (_art.color != target) _art.color = target;
        }
        if (_badge != null)
        {
            Color bc = _ready ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            if (_badge.color != bc) _badge.color = bc;
        }
        if (_pie != null)
        {
            int k = _ready ? 0 : Mathf.CeilToInt(Mathf.Clamp01((_readyAt - now) / _cooldown) * PieSteps);
            _pie.enabled = k > 0;
            if (k > 0) _pie.sprite = PieSprite(_pieSize, k);
        }
        if (_ready && now >= _nextGlint)
        {
            _nextGlint = now + GlintEvery;
            Vector2 corner = (Vector2)transform.position + new Vector2(-_size.x * 0.5f + 0.1f, _size.y * 0.5f - 0.1f);
            FxAnim.Sprite(FxArt.Star4, corner, _order + 4, 0.4f, Color.white, 0.7f, "Glint").Pop().Spin(120f);
        }
        bool near = _ready && PlayerNear();
        float a = 0.6f + 0.4f * Mathf.Sin(now * 8f);
        foreach (SpriteRenderer o in _outline)
        {
            if (o == null) continue;
            o.enabled = near;
            if (near) o.color = new Color(1f, 1f, 1f, a);
        }
    }

    Transform FindPlayer()
    {
        if (_player == null && Time.time >= _findPlayerAt)
        {
            _findPlayerAt = Time.time + 0.5f;
            _player = KitPlayer.Transform;
        }
        return _player;
    }

    bool PlayerNear()
    {
        Transform pl = FindPlayer();
        if (pl == null) return false;
        Vector2 body = (Vector2)pl.position + Vector2.up * 0.5f;
        return Vector2.Distance(body, transform.position) <= NearDistance;
    }

    // ───────── 絵（実行時に作る小さいドット絵） ─────────

    static readonly Color32 BadgeInk = new Color32(0x1c, 0x1c, 0x1e, 255);

    static Sprite BadgeSprite(Effect e)
    {
        string[] rows;
        Color32 fill;
        switch (e)
        {
            case Effect.StunBoss: // 黄色にいなずま
                fill = Yellow;
                rows = new[]
                {
                    "..KKKKK..",
                    ".KBBBBBK.",
                    "KBBBKKBBK",
                    "KBBKKBBBK",
                    "KBKKKKKBK",
                    "KBBBKKBBK",
                    "KBBKKBBBK",
                    ".KBKBBBK.",
                    "..KKKKK..",
                };
                break;
            case Effect.Refill: // 緑に回る矢印
                fill = Green;
                rows = new[]
                {
                    "..KKKKK..",
                    ".KBBBBBK.",
                    "KBBWWWBBK",
                    "KBWBBBWBK",
                    "KBWBBWWWK",
                    "KBWBBBWBK",
                    "KBBWWBBBK",
                    ".KBBBBBK.",
                    "..KKKKK..",
                };
                break;
            default: // 水色に、斜線の入った弾
                fill = Cyan;
                rows = new[]
                {
                    "..KKKKK..",
                    ".KBBBBBK.",
                    "KBBBBBKBK",
                    "KBWWWKBBK",
                    "KBWWKWWBK",
                    "KBWKWWBBK",
                    "KBKBBBBBK",
                    ".KBBBBBK.",
                    "..KKKKK..",
                };
                break;
        }
        var pal = new Dictionary<char, Color32> { { 'K', BadgeInk }, { 'B', fill }, { 'W', new Color32(255, 255, 255, 255) } };
        return RuntimeSprites.FromAscii($"AppButtonBadge_{e}", rows, pal);
    }

    // 時計のような影：上から時計回りに、残りの割合（k / PieSteps）だけ塗った円
    static Sprite PieSprite(int d, int k)
    {
        string key = $"AppButtonPie_{d}_{k}";
        float frac = k / (float)PieSteps;
        var rows = new string[d];
        float r = d * 0.5f;
        for (int y = 0; y < d; y++)
        {
            var line = new char[d];
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = r - (y + 0.5f);
                bool inside = dx * dx + dy * dy <= r * r;
                float ang = Mathf.Atan2(dx, dy); // 上が 0、時計回りに +
                if (ang < 0f) ang += Mathf.PI * 2f;
                bool filled = ang / (Mathf.PI * 2f) <= frac;
                line[x] = !inside ? '.' : filled ? 'D' : '.';
            }
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii(key, rows, new Dictionary<char, Color32> { { 'D', new Color32(0x10, 0x10, 0x14, 150) } });
    }
}

/// <summary>ボスの上を回る黄色い星3つ（ひるんでいる間。ボタンの stun_boss）</summary>
public class AppStunStars : MonoBehaviour
{
    BossController _boss;
    float _until;
    readonly SpriteRenderer[] _stars = new SpriteRenderer[3];

    public static void Attach(BossController boss, float seconds)
    {
        if (boss == null) return;
        var go = new GameObject("StunStars");
        if (go.scene != boss.gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, boss.gameObject.scene);
        var s = go.AddComponent<AppStunStars>();
        s._boss = boss;
        s._until = Time.time + seconds;
        for (int i = 0; i < 3; i++)
        {
            s._stars[i] = RuntimeSprites.Show(go.transform, FxArt.Star4, Vector2.zero, FxKit.OrderRing, 0.8f, "Star");
            s._stars[i].color = AppStageButton.Yellow;
        }
        s.Place();
    }

    void Update()
    {
        if (_boss == null || Time.time >= _until || !_boss.isActiveAndEnabled)
        {
            Destroy(gameObject);
            return;
        }
        Place();
    }

    void Place()
    {
        Bounds b = _boss.BodyBounds;
        transform.position = new Vector3(b.center.x, b.max.y + 0.45f, 0f);
        float rx = Mathf.Max(0.6f, b.extents.x * 0.6f);
        for (int i = 0; i < _stars.Length; i++)
        {
            if (_stars[i] == null) continue;
            float ang = Time.time * 5f + i * Mathf.PI * 2f / _stars.Length;
            _stars[i].transform.localPosition = new Vector3(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * 0.22f, 0f);
        }
    }
}

/// <summary>ボタンからプレイヤーへ飛ぶ光（ゆるい弧をえがいて、動くプレイヤーを追う）</summary>
public class AppFlyToTarget : MonoBehaviour
{
    Vector2 _from;
    Transform _target;
    float _delay, _time, _age, _bend;
    SpriteRenderer _sr;

    public static void Spawn(Sprite sprite, Vector2 from, Transform target, Color color, float delay, float time, float bend)
    {
        var go = new GameObject("FlyToPlayer");
        go.transform.position = from;
        var f = go.AddComponent<AppFlyToTarget>();
        f._from = from;
        f._target = target;
        f._delay = delay;
        f._time = Mathf.Max(0.05f, time);
        f._bend = bend;
        f._sr = go.AddComponent<SpriteRenderer>();
        f._sr.sprite = sprite;
        f._sr.color = color;
        f._sr.sortingOrder = FxKit.OrderRing;
        f._sr.enabled = delay <= 0f;
    }

    void Update()
    {
        if (_target == null) { Destroy(gameObject); return; }
        if (_delay > 0f) { _delay -= Time.deltaTime; return; }
        _sr.enabled = true;
        _age += Time.deltaTime;
        float t = Mathf.Clamp01(_age / _time);
        Vector2 to = (Vector2)_target.position + Vector2.up * 0.5f;
        Vector2 d = to - _from;
        Vector2 perp = d.sqrMagnitude > 0.0001f ? new Vector2(-d.y, d.x).normalized : Vector2.up;
        Vector2 mid = (_from + to) * 0.5f + perp * _bend;
        float e = t * t * (3f - 2f * t);
        Vector2 p = Vector2.Lerp(Vector2.Lerp(_from, mid, e), Vector2.Lerp(mid, to, e), e);
        transform.position = p;
        float s = 1f - 0.4f * t;
        transform.localScale = new Vector3(s, s, 1f);
        if (t >= 1f) Destroy(gameObject);
    }
}
