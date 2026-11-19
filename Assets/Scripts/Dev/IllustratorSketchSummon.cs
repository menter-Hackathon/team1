using UnityEngine;

/// <summary>筆が描いた絵を実体化する。ロケット・雷・花を同じドット密度で描く。</summary>
public sealed class IllustratorSketchSummon : MonoBehaviour
{
    public enum DrawingKind { Rocket, Lightning, Flower }
    public bool Finished { get; private set; }
    public DrawingKind Kind { get; private set; }
    public bool Hostile { get; private set; }
    const float DrawTime = 0.75f;
    public const float EnemyRocketWarning = 0.95f;
    public const float EnemyLightningWarning = 0.9f;
    public static DrawingKind PickKind(float roll) => roll < 0.45f ? DrawingKind.Rocket
        : roll < 0.9f ? DrawingKind.Lightning : DrawingKind.Flower;
    public static DrawingKind PickEnemyKind(float roll) => roll < 0.5f ? DrawingKind.Rocket : DrawingKind.Lightning;
    IllustratorPixelCanvas _canvas, _boltCanvas, _warningCanvas;
    IllustratorBrushTip _brush;
    Vector3[] _outline, _posed, _bolt;
    Collider2D _playerBody, _bossBody;
    VirusGauge _boss;
    Health _health;
    Vector2 _velocity;
    Vector2 _castOrigin;
    Vector2 _launchDirection;
    Vector3[] _warningPoints;
    float _warningDistance;
    Vector2 _warningSide;
    float _strikeX;
    int _castFacing;
    Color32 _color, _dark = new Color32(34, 22, 46, 255);
    float _born;
    int _frame = -1, _visualPhase = -1;
    bool _resolved, _hit, _warningCleared;
    float RocketDelay => Hostile ? EnemyRocketWarning : 0f;
    float LightningDelay => Hostile ? EnemyLightningWarning : 0.25f;

    public void Initialize(DrawingKind kind, PlayerController player, VirusGauge boss, Collider2D bossBody, bool hostile = false)
    {
        Hostile = hostile;
        Kind = hostile && kind == DrawingKind.Flower ? DrawingKind.Rocket : kind;
        _boss = boss; _bossBody = bossBody;
        _playerBody = player.GetComponent<Collider2D>(); _health = player.GetComponent<Health>();
        _castOrigin = hostile ? (Vector2)bossBody.bounds.center : (Vector2)_playerBody.bounds.center;
        _castFacing = hostile ? (_playerBody.bounds.center.x >= _castOrigin.x ? 1 : -1) : player.FacingDirection;
        _strikeX = hostile ? Mathf.Clamp(_playerBody.bounds.center.x, -9.2f, 9.2f)
            : Mathf.Clamp(_castOrigin.x + _castFacing * 7f, -9.2f, 9.2f);
        _born = Time.time;
        _color = Kind == DrawingKind.Rocket ? hostile ? new Color32(244, 127, 65, 255) : new Color32(52, 207, 230, 255)
            : Kind == DrawingKind.Lightning ? new Color32(255, 213, 55, 255) : new Color32(237, 72, 157, 255);
        _outline = CreateOutline(Kind);
        _posed = new Vector3[_outline.Length];
        _canvas = new IllustratorPixelCanvas(transform, new[] { new Vector3(-0.85f, -0.85f), new Vector3(0.85f, 0.85f) }, 0.1f, 25);
        _brush = IllustratorBrushTip.Create(transform, _color, 28);
        // 描き始めた場所・向きを保持し、移動や振り向きで追従させない。
        transform.position = IllustratorPixelCanvas.Snap(_castOrigin + new Vector2(_castFacing * 1.5f, 0.6f));
        _launchDirection = hostile ? ((Vector2)_playerBody.bounds.center - (Vector2)transform.position).normalized
            : Vector2.right * _castFacing;
        if (_launchDirection.sqrMagnitude < 0.01f) _launchDirection = Vector2.right * _castFacing;
        if (hostile)
        {
            if (Kind == DrawingKind.Rocket) BuildRocketWarning();
            else BuildBolt();
        }
        Tick();
    }

    public static Vector3[] CreateOutline(DrawingKind kind)
    {
        if (kind == DrawingKind.Rocket)
            return Grid(new[] { new Vector2(0,10), new Vector2(-5,4), new Vector2(-5,-4),
                new Vector2(-8,-8), new Vector2(-3,-6), new Vector2(0,-8), new Vector2(3,-6),
                new Vector2(8,-8), new Vector2(5,-4), new Vector2(5,4), new Vector2(0,10) });
        else if (kind == DrawingKind.Lightning)
            return Grid(new[] { new Vector2(1,11), new Vector2(-7,0), new Vector2(-1,0),
                new Vector2(-4,-11), new Vector2(8,3), new Vector2(2,3), new Vector2(1,11) });
        else
        {
            var outline = new Vector3[49];
            for (int i = 0; i < outline.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / (outline.Length - 1);
                float radius = 6f + 2f * Mathf.Cos(angle * 5f);
                outline[i] = new Vector3(Mathf.Cos(angle) * radius, 3f + Mathf.Sin(angle) * radius) / GameArt.PixelsPerUnit;
            }
            return outline;
        }
    }

    static Vector3[] Grid(Vector2[] points)
    {
        var result = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++) result[i] = (Vector3)points[i] / GameArt.PixelsPerUnit;
        return result;
    }

    public void Tick()
    {
        if (Finished) return;
        float elapsed = Time.time - _born, active = elapsed - DrawTime;
        if (active >= 0f)
        {
            if (!_resolved)
            {
                _resolved = true;
                _brush.Renderer.enabled = false;
                Vfx.Sparkle(transform.position);
                if (Kind == DrawingKind.Rocket)
                {
                    _velocity = _launchDirection * (Hostile ? 6f : 9f);
                }
                else if (Kind == DrawingKind.Lightning)
                {
                    if (_boltCanvas == null) BuildBolt();
                }
                else
                {
                    _health.Heal(1);
                    _health.SetInvincible(2f);
                    Vfx.Sparkle(_playerBody.bounds.center);
                }
            }
            if (Kind == DrawingKind.Rocket) UpdateRocket(active);
            else if (Kind == DrawingKind.Lightning) UpdateLightning(active);
            else Finished = active >= 1.7f;
        }
        int frame = Mathf.FloorToInt(elapsed * 12f);
        bool waiting = Kind == DrawingKind.Rocket && active < RocketDelay
            || Kind == DrawingKind.Lightning && active < LightningDelay;
        int phase = active < 0f ? 0 : waiting ? 1 : 2;
        if (frame != _frame || phase != _visualPhase)
        {
            _frame = frame; _visualPhase = phase;
            DrawPicture(Mathf.Clamp01(elapsed / DrawTime), active);
        }
    }

    void UpdateRocket(float active)
    {
        if (active < RocketDelay) return;
        if (!_warningCleared && _warningCanvas != null)
        {
            _warningCanvas.Clear(); _warningCanvas.Apply(); _warningCleared = true;
        }
        Vector2 old = transform.position;
        Vector2 next = old + _velocity * Time.deltaTime;
        transform.position = next;
        Bounds target = Hostile ? _playerBody.bounds : _bossBody.bounds;
        if ((Hostile || _bossBody.enabled) && IllustratorGeometry.SegmentHits(target, old, next, 0.2f))
        {
            var info = new DamageInfo { Amount = Hostile ? 1 : 3, GaugeGain = Hostile ? 0 : 24,
                SourceTeam = Hostile ? Team.Enemy : Team.Player, HitPoint = next,
                Knockback = Hostile ? new Vector2(Mathf.Sign(_velocity.x) * 4f, 3f) : Vector2.zero, HitStun = 0.2f };
            if (Hostile) _health.TryTakeDamage(info); else _boss.TryTakeDamage(info);
            Vfx.HitSpark(next); Vfx.SmokeSmall(next);
            Finished = true;
        }
        else Finished = active - RocketDelay >= 3f || Mathf.Abs(next.x) > 12f || next.y < -1f || next.y > 11f;
    }

    void BuildRocketWarning()
    {
        Vector3 origin = transform.position;
        _warningDistance = RocketWarningDistance(origin, _launchDirection);
        Vector3 end = origin + (Vector3)_launchDirection * _warningDistance;
        _warningSide = new Vector2(-_launchDirection.y, _launchDirection.x);
        _warningPoints = new[] { origin, end,
            origin + (Vector3)_warningSide * 0.55f, origin - (Vector3)_warningSide * 0.55f,
            end + (Vector3)_warningSide * 0.55f, end - (Vector3)_warningSide * 0.55f };
        _warningCanvas = new IllustratorPixelCanvas(transform, _warningPoints, 0.2f, 20);
    }

    public static float RocketWarningDistance(Vector2 origin, Vector2 direction)
    {
        // 矢印が床の下・画面外へ消えないよう、見える範囲まで進路を示す。
        float distance = 14f;
        if (direction.x > 0.001f) distance = Mathf.Min(distance, (9.6f - origin.x) / direction.x);
        if (direction.x < -0.001f) distance = Mathf.Min(distance, (-9.6f - origin.x) / direction.x);
        if (direction.y > 0.001f) distance = Mathf.Min(distance, (9f - origin.y) / direction.y);
        if (direction.y < -0.001f) distance = Mathf.Min(distance, (0.4f - origin.y) / direction.y);
        return Mathf.Max(0.2f, distance);
    }

    void DrawRocketWarning(float active)
    {
        _warningCanvas.Clear();
        Vector3 origin = _warningPoints[0];
        var warning = new Color32(255, 179, 58, 255);
        // 進路は細い点線で示し、先端の矢印で向きを伝える。
        for (float distance = 0f; distance < _warningDistance; distance += 0.75f)
        {
            Vector3 a = origin + (Vector3)_launchDirection * distance;
            Vector3 b = origin + (Vector3)_launchDirection * Mathf.Min(distance + 0.4f, _warningDistance);
            _warningCanvas.Line(a, b, 1, warning);
        }
        const int arrows = 1;
        float ready = Mathf.Clamp01(active / EnemyRocketWarning);
        for (int i = 0; i < arrows; i++)
        {
            float distance = _warningDistance * (i + 1f) / arrows;
            Vector3 tip = origin + (Vector3)_launchDirection * distance;
            float length = Mathf.Min(0.45f, _warningDistance * 0.55f);
            float width = Mathf.Min(0.24f, length * 0.55f);
            Vector3 a = tip - (Vector3)_launchDirection * length + (Vector3)_warningSide * width;
            Vector3 b = tip - (Vector3)_launchDirection * length - (Vector3)_warningSide * width;
            _warningCanvas.Line(tip, a, 3, _dark); _warningCanvas.Line(tip, b, 3, _dark);
            _warningCanvas.Line(tip, a, 1, warning); _warningCanvas.Line(tip, b, 1, warning);
            for (int row = 0; row <= 12; row++)
            {
                float t = row / 12f;
                if (t > ready) break;
                Vector3 center = tip - (Vector3)_launchDirection * length * (1f - t);
                Vector3 offset = (Vector3)_warningSide * width * (1f - t);
                _warningCanvas.Line(center - offset, center + offset, 1, warning);
            }
        }
        _warningCanvas.Apply();
    }

    void BuildBolt()
    {
        float x = _strikeX;
        RaycastHit2D floor = Physics2D.Raycast(new Vector2(x, 9f), Vector2.down, 12f, LayerMask.GetMask("Ground"));
        Vector2 target = new Vector2(x, floor.collider != null ? floor.point.y : 0f);
        _bolt = new Vector3[9];
        for (int i = 0; i < _bolt.Length; i++)
        {
            float t = i / (float)(_bolt.Length - 1);
            _bolt[i] = new Vector3(target.x + (i == 8 ? 0f : (i % 2 == 0 ? -0.35f : 0.35f)), Mathf.Lerp(9f, target.y, t));
        }
        _boltCanvas = new IllustratorPixelCanvas(transform, _bolt, 0.2f, 27);
    }

    void UpdateLightning(float active)
    {
        if (active >= LightningDelay && !_hit)
        {
            _hit = true;
            for (int i = 1; i < _bolt.Length; i++)
            {
                Bounds target = Hostile ? _playerBody.bounds : _bossBody.bounds;
                if ((!Hostile && !_bossBody.enabled) || !IllustratorGeometry.SegmentHits(target, _bolt[i - 1], _bolt[i], 0.1f)) continue;
                Vector3 hitPoint = target.center;
                var info = new DamageInfo { Amount = Hostile ? 1 : 2, GaugeGain = Hostile ? 0 : 20,
                    SourceTeam = Hostile ? Team.Enemy : Team.Player, HitPoint = hitPoint,
                    Knockback = Hostile ? new Vector2(0f, 5f) : Vector2.zero, HitStun = 0.2f };
                if (Hostile) _health.TryTakeDamage(info); else _boss.TryTakeDamage(info);
                Vfx.HitSpark(hitPoint);
                break;
            }
        }
        Finished = active >= LightningDelay + 0.55f;
    }

    void DrawPicture(float progress, float active)
    {
        _canvas.Clear();
        float angle = Kind == DrawingKind.Rocket && active >= 0f
            ? Mathf.Round((Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg - 90f) / 45f) * 45f * Mathf.Deg2Rad : 0f;
        float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
        for (int i = 0; i < _outline.Length; i++)
        {
            Vector3 point = _outline[i];
            _posed[i] = IllustratorPixelCanvas.Snap(new Vector3(point.x * cosine - point.y * sine, point.x * sine + point.y * cosine));
        }
        if (active < 0f)
        {
            float position = progress * (_posed.Length - 1);
            int segment = Mathf.Min(_posed.Length - 2, Mathf.FloorToInt(position));
            Vector3 tip = Vector3.Lerp(_posed[segment], _posed[segment + 1], position - segment);
            _canvas.Path(_posed, segment + 1, 1, _color);
            _canvas.Line(_posed[segment], tip, 1, _color);
            _brush.transform.localPosition = IllustratorPixelCanvas.Snap(tip);
            _brush.SetHeading(_posed[segment + 1] - _posed[segment]);
        }
        else
        {
            int coverage = Kind == DrawingKind.Flower ? Mathf.CeilToInt(Mathf.Clamp01((1.7f - active) / 0.3f) * 16f) : 16;
            for (int y = 0; y < _canvas.Height; y++)
                for (int x = 0; x < _canvas.Width; x++)
                {
                    Vector3 point = _canvas.World(x, y);
                    if (IllustratorGeometry.Contains(_posed, point))
                        _canvas.Dot(x, y, (x + y) % 5 == 0 ? (Color32)Color.Lerp(_color, _dark, 0.2f) : _color, coverage);
                }
            _canvas.Path(_posed, _posed.Length, 3, _dark, coverage, true);
            _canvas.Path(_posed, _posed.Length, 1, _color, coverage, true);
            if (Kind == DrawingKind.Flower)
            {
                Color32 green = new Color32(84, 188, 102, 255);
                _canvas.Line(new Vector3(0, -2f / 18f), new Vector3(0, -11f / 18f), 2, green, coverage);
                _canvas.Line(new Vector3(0, -8f / 18f), new Vector3(-5f / 18f, -5f / 18f), 2, green, coverage);
                _canvas.Box(new Vector3(0, 3f / 18f), 5, new Color32(255, 211, 63, 255));
            }
            else if (Kind == DrawingKind.Rocket)
            {
                Vector3 exhaustA = new Vector3(9f * sine, -9f * cosine) / 18f;
                Vector3 exhaustB = new Vector3(13f * sine, -13f * cosine) / 18f;
                _canvas.Line(exhaustA, exhaustB, 3, new Color32(245, 119, 42, 255));
                _canvas.Line(exhaustA, exhaustB, 1, new Color32(255, 218, 67, 255));
                _canvas.Box(Vector3.zero, 5, _dark);
                _canvas.Box(Vector3.zero, 3, new Color32(207, 239, 246, 255));
            }
        }
        _canvas.Apply();
        if (_warningCanvas != null && !_warningCleared)
            DrawRocketWarning(active);
        if (_boltCanvas != null)
        {
            _boltCanvas.Clear();
            if (active < LightningDelay)
            {
                if (Hostile) _boltCanvas.Path(_bolt, _bolt.Length, 3, _dark, 6);
                _boltCanvas.Path(_bolt, _bolt.Length, 1, _color, Hostile ? 12 : 5);
                if (Hostile)
                {
                    _boltCanvas.Box(_bolt[_bolt.Length - 1], 5, _dark);
                    _boltCanvas.Box(_bolt[_bolt.Length - 1], 3, _color);
                }
            }
            else
            {
                int coverage = Mathf.CeilToInt(Mathf.Clamp01((LightningDelay + 0.55f - active) / 0.3f) * 16f);
                _boltCanvas.Path(_bolt, _bolt.Length, 5, _dark, coverage);
                _boltCanvas.Path(_bolt, _bolt.Length, 3, _color, coverage);
            }
            _boltCanvas.Apply();
        }
    }

    void OnDestroy() { _canvas?.Dispose(); _boltCanvas?.Dispose(); _warningCanvas?.Dispose(); }
}
