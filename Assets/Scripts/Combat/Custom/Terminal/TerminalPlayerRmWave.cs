using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】能力 rm -rf の「文字化けの波」（2026-11-19。docs/app-feedback-v2-design.md 6-3）。TerminalPlayerRm が出す。
/// ・横 2×高さ 3.5マスの判定が、毎秒 14マスで向いている方へ床の上を 10マス走る。貫通（同じ相手には1回だけ）。
///   足場から外れたら下の床へ落ちて続く。壁（弾が当たって止まる地形）で止まる
/// ・触れた敵の弾（Projectile）は消える。消えた弾・当たった敵は、緑の文字にくずれて上へ飛ぶ
/// ・見た目：緑 #33FF66 と白の小さい字（# / * rm）のかたまりが、うねりながら転がる。前の上に白い四角のカーソル。
///   通った床に、緑の字が 0.5秒残って消える（当たらない。飾り）。赤は使わない（赤い文字化けの床はボスの物）
/// 場に残る技・床の文字化けの印（Lingers・CorruptsFloor）は立てない（ボスの技えらびに効いてしまうため）
/// </summary>
public class TerminalPlayerRmWave : CustomEffect
{
    const float GlyphHeight = 0.4f;   // 【仮】字の高さ（マス）
    const float CellW = 0.4f;         // 字のマス目
    const float CellH = 0.4f;
    const float SwapInterval = 0.06f; // 字が入れ替わる間隔
    const float TrailStep = 0.4f;     // 床に字を残す間隔（マス）
    const float EndTime = 0.2f;       // 止まってから消えるまで
    const int BackOrder = 18, GlyphOrder = 22, TrailOrder = 17;
    static readonly Color Back = new Color(0.047f, 0.047f, 0.047f, 0.35f);

    AttackRunner _owner;
    TerminalPlayerRm _cfg;
    Rigidbody2D _rb;
    Hitbox _hitbox;
    Vector2 _size;
    int _dir = 1;
    float _traveled;
    float _lastTrailX;
    float _nextSwap;
    float _born;
    float _endAt = -1f;
    Transform _visual;
    SpriteRenderer _back;
    SpriteRenderer _cursor;
    TextMesh[,] _glyphs;
    int _cols, _rows;
    readonly Collider2D[] _results = new Collider2D[32];
    readonly RaycastHit2D[] _hits = new RaycastHit2D[4];
    readonly HashSet<GameObject> _erased = new HashSet<GameObject>();

    /// <summary>波を出す。ground は走り始める床の上（足元の x）、dir は向き（右 1／左 -1）</summary>
    public static TerminalPlayerRmWave Spawn(AttackRunner owner, AttackData data, TerminalPlayerRm cfg, Vector2 ground, int dir)
    {
        if (owner == null || data == null || cfg == null) return null;
        GameObject go = owner.CreateSpawnedObject("TerminalPlayerRmWave", ground);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        Vector2 size = new Vector2(Mathf.Max(0.2f, cfg.WaveSize.x), Mathf.Max(0.2f, cfg.WaveSize.y));
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;
        box.offset = new Vector2(0f, size.y * 0.5f); // 下の端が床の上

        var wave = go.AddComponent<TerminalPlayerRmWave>();
        wave._owner = owner;
        wave._cfg = cfg;
        wave._rb = rb;
        wave._size = size;
        wave._dir = dir >= 0 ? 1 : -1;
        wave._born = Time.time;
        wave._lastTrailX = ground.x;
        // 1回の波で、同じ相手には1回だけ当たる（貫通。ボスのゲージも1回だけ）
        wave._hitbox = owner.AddHitbox(go, data, box);
        wave._hitbox.SetErrorKind("PlayerRm");
        wave._hitbox.Hit += wave.OnHit;
        wave._hitbox.Activate();
        wave.BuildVisual();
        return wave;
    }

    void BuildVisual()
    {
        _visual = new GameObject("Visual").transform;
        _visual.gameObject.layer = gameObject.layer;
        _visual.SetParent(transform, false);
        _back = TerminalArt.Box(_visual, new Vector2(0f, _size.y * 0.5f), _size, Back, BackOrder, "Back");
        _cols = Mathf.Max(1, Mathf.RoundToInt(_size.x / CellW));
        _rows = Mathf.Max(1, Mathf.RoundToInt(_size.y / CellH));
        float cw = _size.x / _cols, ch = _size.y / _rows;
        _glyphs = new TextMesh[_cols, _rows];
        for (int c = 0; c < _cols; c++)
        {
            // c＝0 が前（進む向きの端）
            float x = _dir * (_size.x * 0.5f - cw * (c + 0.5f));
            for (int r = 0; r < _rows; r++)
            {
                TextMesh tm = WorldText.Create(_visual, Glyph(), GlyphHeight, GlyphColor(), GlyphOrder, true, TextAnchor.MiddleCenter, "Glyph");
                tm.richText = false;
                tm.transform.localPosition = new Vector3(x, ch * (r + 0.5f), 0f);
                _glyphs[c, r] = tm;
            }
        }
        // 前の上に白い四角のカーソル
        _cursor = TerminalArt.Box(_visual, Vector2.zero, new Vector2(GlyphHeight * 0.55f, GlyphHeight), TerminalArt.White, GlyphOrder, "Cursor");
        Animate(true);
    }

    static string Glyph() => TerminalArt.WaveGlyphs[Random.Range(0, TerminalArt.WaveGlyphs.Length)];
    static Color GlyphColor() => Random.value < 0.7f ? TerminalArt.Green : TerminalArt.White;

    void Update()
    {
        if (_endAt >= 0f)
        {
            // 止まった：うすくなって消える
            float k = Mathf.Clamp01(1f - (Time.time - _endAt) / EndTime);
            if (_back != null) _back.color = TerminalArt.WithAlpha(Back, Back.a * k);
            if (_cursor != null) _cursor.enabled = false;
            if (_glyphs != null)
                foreach (TextMesh g in _glyphs)
                    if (g != null) WorldText.SetColor(g, TerminalArt.WithAlpha(TerminalArt.Green, k));
            if (k <= 0f) Destroy(gameObject);
            return;
        }
        // 念のため（どこかで止まらなかったとき）
        if (Time.time - _born > _cfg.Distance / Mathf.Max(0.1f, _cfg.Speed) + 1.5f) BeginEnd(false);
        Animate(false);
        Trail();
    }

    /// <summary>字のかたまりを、うねらせて入れ替える（列ごとに高さが変わる波）</summary>
    void Animate(bool force)
    {
        if (_glyphs == null) return;
        bool swap = force || Time.time >= _nextSwap;
        if (swap) _nextSwap = Time.time + SwapInterval;
        float t = Time.time - _born;
        int frontTop = _rows;
        for (int c = 0; c < _cols; c++)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(t * 14f - c * 1.1f);
            float frac = c == 0 ? 0.75f + 0.25f * wave : 0.4f + 0.6f * wave; // 前の列は高く
            int shown = Mathf.Clamp(Mathf.CeilToInt(frac * _rows), 1, _rows);
            if (c == 0) frontTop = shown;
            for (int r = 0; r < _rows; r++)
            {
                TextMesh g = _glyphs[c, r];
                if (g == null) continue;
                bool on = r < shown;
                g.gameObject.SetActive(on);
                if (on && swap)
                {
                    g.text = Glyph();
                    WorldText.SetColor(g, r == shown - 1 ? TerminalArt.White : GlyphColor()); // てっぺんは白（波がしら）
                }
            }
        }
        if (_cursor != null)
        {
            float cw = _size.x / _cols, ch = _size.y / _rows;
            _cursor.transform.localPosition = new Vector3(_dir * (_size.x * 0.5f + cw * 0.3f), ch * (frontTop - 0.5f), 0f);
            _cursor.enabled = Mathf.FloorToInt(Time.time / 0.12f) % 2 == 0;
        }
    }

    /// <summary>通った床に、緑の字を残す（0.5秒で消える。当たらない）</summary>
    void Trail()
    {
        if (_cfg.TrailLife <= 0f) return;
        Vector2 p = transform.position;
        // 2026-11-19：波が進む向きと逆に離れていると、_lastTrailX が遠ざかり続けて抜けられなかった（ゲームが固まる）。
        // 進む向きに離れた分だけ字を置き、逆向き・離れすぎのときは今の位置から数え直す
        float ahead = (p.x - _lastTrailX) * _dir;
        if (_dir == 0 || ahead < 0f || ahead > TrailStep * 40f) { _lastTrailX = p.x; return; }
        int guard = 0;
        while ((p.x - _lastTrailX) * _dir >= TrailStep && guard++ < 40)
        {
            _lastTrailX += _dir * TrailStep;
            FxKit.Text(Glyph(), new Vector2(_lastTrailX - _dir * _size.x * 0.5f, p.y + 0.22f), GlyphHeight, TerminalArt.Green, TrailOrder,
                _cfg.TrailLife, new Color(0f, 0f, 0f, 0f)).FadeFrom(0.3f);
        }
    }

    void FixedUpdate()
    {
        if (_owner == null) return;
        if (_endAt >= 0f)
        {
            EraseBullets(_rb.position); // 止まってうすくなる間も、弾は消す（判定も残す。同じ相手には1回だけ）
            return;
        }
        float step = _cfg.Speed * Time.fixedDeltaTime;
        if (_traveled + step > _cfg.Distance) step = Mathf.Max(0f, _cfg.Distance - _traveled);
        Vector2 pos = _rb.position;

        // 壁：真ん中から、進む向きに調べる（足場・すり抜け床は止めない）。壁ぎわで使っても、前の端が壁に着いた所で止まる
        bool wall = false;
        float half = _size.x * 0.5f;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(_owner.SolidMask);
        Vector2 from = new Vector2(pos.x, pos.y + 0.5f);
        int n = Physics2D.Raycast(from, new Vector2(_dir, 0f), filter, _hits, half + step + 0.02f);
        for (int i = 0; i < n; i++)
        {
            if (_hits[i].collider == null) continue;
            step = Mathf.Clamp(_hits[i].distance - half - 0.02f, 0f, step);
            wall = true;
            break;
        }

        float nx = pos.x + _dir * step;
        float ny = pos.y;
        // 床に沿う：足場から外れたら下の床へ落ちる（少しの段差は上がる）
        if (_owner.TryFindGround(nx, pos.y + 0.6f, out float g) && g <= pos.y + 0.6f) ny = g;
        _rb.MovePosition(new Vector2(nx, ny));
        _traveled += step;

        EraseBullets(new Vector2(nx, ny));
        if (wall || _traveled >= _cfg.Distance - 0.001f) BeginEnd(wall);
    }

    /// <summary>触れた敵の弾を消す（緑の字にくずれて上へ飛ぶ）</summary>
    void EraseBullets(Vector2 feet)
    {
        var filter = new ContactFilter2D { useTriggers = true }; // レイヤーは問わない（弾は撃った人のレイヤー）
        int n = Physics2D.OverlapBox(feet + new Vector2(0f, _size.y * 0.5f), _size, 0f, filter, _results);
        for (int i = 0; i < n; i++)
        {
            Collider2D col = _results[i];
            if (col == null || !col.enabled) continue;
            Projectile p = col.GetComponentInParent<Projectile>();
            if (p == null || !p.isActiveAndEnabled || !IsEnemy(p) || !_erased.Add(p.gameObject)) continue;
            Crumble(p.transform.position, 4, 0.5f);
            p.gameObject.SetActive(false); // このフレームのうちに当たらないように、すぐ止める
            Destroy(p.gameObject);
        }
    }

    bool IsEnemy(Projectile p)
    {
        Hitbox hb = p.GetComponent<Hitbox>();
        if (hb != null) return hb.Team != _owner.Team;
        return p.Owner != null && p.Owner.Team != _owner.Team;
    }

    void OnHit(IDamageable target, DamageInfo info)
    {
        bool dead = target is Health h && h.IsDead;
        Crumble(info.HitPoint, dead ? 9 : 3, dead ? 1f : 0.6f);
    }

    /// <summary>緑の字にくずれて、上へ飛ぶ</summary>
    void Crumble(Vector2 at, int count, float area)
    {
        if (_owner == null) return;
        TerminalGlyphBurst.Spawn(_owner, at, new Vector2(area, area), count, TerminalArt.Green, TerminalArt.WaveGlyphs, GlyphHeight, 0.6f, GlyphOrder + 1);
    }

    /// <summary>止まる（うすくなって消える。判定は消えるまで残す）。壁に当たったら、前で字がはじける</summary>
    void BeginEnd(bool wall)
    {
        if (_endAt >= 0f) return;
        _endAt = Time.time;
        if (wall) Crumble((Vector2)transform.position + new Vector2(_dir * _size.x * 0.5f, _size.y * 0.4f), 6, 0.8f);
    }

    protected override void Restore()
    {
        if (_hitbox != null)
        {
            _hitbox.Hit -= OnHit;
            _hitbox.Deactivate();
        }
    }
}
