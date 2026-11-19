using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>連続する多色パス攻撃と、お絵かき召喚を試す部屋。</summary>
public sealed class IllustratorSkillDemo : MonoBehaviour, IWarpable
{
    [SerializeField] PlayerController _player;
    [SerializeField] VirusGauge _boss;
    [SerializeField] BoxCollider2D _bossBody;
    [SerializeField] Camera _camera;
    [SerializeField] Texture2D _bossBodySheet, _bossFaceSheet, _bossHandsSheet;

    static readonly Color Orange = new Color32(255, 155, 43, 255);
    static readonly Color Blue = new Color32(72, 207, 248, 255);

    static readonly Color[] InkColors =
    {
        new Color32(24, 207, 239, 255), new Color32(237, 51, 154, 255),
        new Color32(255, 216, 52, 255), new Color32(103, 223, 136, 255),
        new Color32(156, 110, 255, 255), new Color32(255, 133, 66, 255)
    };
    readonly List<IllustratorInkStroke> _strokes = new List<IllustratorInkStroke>();
    readonly List<IllustratorSketchSummon> _summons = new List<IllustratorSketchSummon>();
    DemoAppBossVisual _visual;
    Health _health;
    Collider2D _playerBody;
    Hitbox _contact;
    Sprite _icon;
    Texture2D _iconTexture;
    SkillDefinition _ability;
    Vector3 _playerStart, _bossStart, _warpTarget;
    float _readyAt, _nextInk, _nextWarp, _nextSketch, _warpAt = -10f;
    int _colorStep, _previousInkPattern = -1;
    float _nextManualAttack;
    bool Enraged => _boss.Ratio >= 0.5f;
    bool _auto = true, _invincible, _controls, _warped, _isWarping;
    bool CanAttack => _health != null && !_health.IsDead && !_boss.IsFull;

    public Bounds BodyBounds => new Bounds(_bossBody.transform.TransformPoint(_bossBody.offset),
        Vector2.Scale(_bossBody.size, _bossBody.transform.lossyScale));
    public void SetVisibility(float alpha) { if (_visual != null) _visual.Visibility = Mathf.Clamp01(alpha); }
    public void SetContactEnabled(bool enabled)
    {
        bool on = enabled && !_boss.IsFull;
        _bossBody.enabled = on;
        if (_contact != null) { if (on) _contact.Activate(); else _contact.Deactivate(); }
    }
    public void Teleport(Vector2 position)
    {
        _boss.transform.position = new Vector3(position.x, position.y, _boss.transform.position.z);
        Physics2D.SyncTransforms();
    }

    void Start()
    {
        _health = _player.GetComponent<Health>();
        _playerBody = _player.GetComponent<Collider2D>();
        _playerStart = _player.transform.position;
        _bossStart = _boss.transform.position;
        _contact = _boss.GetComponent<Hitbox>();
        if (_contact == null) _contact = _boss.gameObject.AddComponent<Hitbox>();
        _contact.SetArea(_bossBody);
        _contact.SetOwner(Team.Enemy, 1 << _player.gameObject.layer, false);
        CreateIcon();
        var art = new GameObject("Illustrator boss art");
        art.transform.SetParent(_boss.transform, false);
        _visual = art.AddComponent<DemoAppBossVisual>();
        _visual.Initialize(_boss, _bossBodySheet, _bossFaceSheet, _bossHandsSheet, _icon);
        _ability = SkillDefinition.CreateRuntime("お絵かき召喚", _icon, SkillCategory.Creative, null, null);
        var hud = _camera.GetComponentInChildren<PixelHud>();
        if (hud != null) hud.BindTest(_boss, _icon, _ability, () => Mathf.Clamp01((_readyAt - Time.time) / 3f));
        _boss.Filled += OnRestored;
        _health.Died += OnPlayerDied;
        ResetDemo();
    }

    void CreateIcon()
    {
        _iconTexture = new Texture2D(24, 24, TextureFormat.RGBA32, false)
            { name = "Illustrator pixel icon", filterMode = FilterMode.Point };
        var pixels = new Color[24 * 24];
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
            {
                bool border = x < 2 || x > 21 || y < 2 || y > 21;
                pixels[y * 24 + x] = border ? Orange : new Color32(48, 16, 0, 255);
            }
        string[] glyph = { "00110000000", "01111000110", "11001100000", "11001100110", "11111100110", "11001100110", "11001100110" };
        for (int row = 0; row < glyph.Length; row++)
            for (int col = 0; col < glyph[row].Length; col++)
                if (glyph[row][col] == '1')
                    for (int dy = 0; dy < 2; dy++) pixels[(18 - row * 2 - dy) * 24 + 6 + col] = Orange;
        _iconTexture.SetPixels(pixels);
        _iconTexture.Apply();
        _icon = Sprite.Create(_iconTexture, new Rect(0, 0, 24, 24), Vector2.one * 0.5f, GameArt.PixelsPerUnit);
    }

    void ResetDemo()
    {
        ClearAttacks();
        _boss.ResetGauge();
        Teleport(_bossStart);
        SetVisibility(1f);
        SetContactEnabled(true);
        _health.ResetHealth();
        _player.CanControl = true;
        _player.SetVelocity(Vector2.zero);
        _player.GetComponent<KinematicBody2D>().Warp(_playerStart);
        _readyAt = 0f;
        _warpAt = -10f;
        _isWarping = false;
        _colorStep = 0;
        _previousInkPattern = -1;
        _nextManualAttack = 0f;
        _nextInk = Time.time + 1.4f;
        _nextWarp = Time.time + 8f;
        _nextSketch = Time.time + 2.5f;
    }

    void OnRestored()
    {
        ClearAttacks();
        SetContactEnabled(false);
        SetVisibility(1f);
        _warpAt = -10f;
        _isWarping = false;
        Vfx.Smoke(_boss.transform.position);
    }

    void OnPlayerDied()
    {
        ClearAttacks();
        SetContactEnabled(false);
        SetVisibility(1f);
        _isWarping = false;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f1Key.wasPressedThisFrame) _controls = !_controls;
        if (kb != null && kb.rKey.wasPressedThisFrame) ResetDemo();
        if (_invincible && !_health.IsDead) _health.SetInvincible(0.1f);
        if (_health.IsDead || _boss.IsFull) return;
        if (kb != null)
        {
            if (kb.tKey.wasPressedThisFrame) PenAttack();
            if (kb.uKey.wasPressedThisFrame) RotationAttack();
            if (kb.iKey.wasPressedThisFrame) BeginWarp();
            if (kb.oKey.wasPressedThisFrame) BossSketch();
            if (kb.digit1Key.wasPressedThisFrame) UseSketch();
        }
        if (_auto && !_isWarping)
        {
            if (Time.time >= _nextWarp) BeginWarp();
            if (!_isWarping && Time.time >= _nextSketch) BossSketch();
            if (!_isWarping && Time.time >= _nextInk)
            {
                // 同じ並びを繰り返さず、描画は短い間隔でつなぐ。
                bool rotate = Random.value < 0.3f && _previousInkPattern != 1;
                if (rotate) RotationAttack(); else PenAttack();
                _previousInkPattern = rotate ? 1 : 0;
            }
        }
        UpdateWarp();
        UpdateStrokes();
        if (CanAttack) UpdateSummons();
    }

    bool BeginDrawing()
    {
        if (!CanAttack || _isWarping || Time.time < _nextManualAttack) return false;
        _nextManualAttack = Time.time + 0.35f;
        return true;
    }

    void PenAttack()
    {
        if (!BeginDrawing()) return;
        Vector2 target = _playerBody.bounds.center;
        const int count = 1;
        int color = _colorStep++;
        for (int i = 0; i < count; i++)
        {
            Vector2 point = new Vector2(Mathf.Clamp(target.x, -8.5f, 8.5f), Mathf.Clamp(target.y + (Random.value < 0.35f ? Random.Range(1.3f, 3f) : 0f), 0.55f, 7.5f));
            float bend = (Random.value < 0.5f ? 1f : -1f) * Random.Range(1.6f, 3f);
            bool reverse = Random.value < 0.5f;
            Vector3[] path = Random.value < 0.45f
                ? DiagonalThrough(point, (Random.value < 0.5f ? 1f : -1f) * Random.Range(0.35f, 0.75f), reverse)
                : Through(point, bend, reverse);
            AddInk(path, InkColors[(color + i) % InkColors.Length],
                false, 1.4f, 0.6f, 0.15f);
        }
        _nextInk = Time.time + (Enraged ? 1.9f : 2.2f);
    }

    static Vector3[] Through(Vector2 point, float bend, bool reverse)
    {
        // 両端を持ち上げ、床付近の制御点が一直線に並ぶのを防ぐ。
        // 狙う点の前後だけ短い水平接線を使い、滑らかにつながる谷型の曲線にする。
        Vector2 a = new Vector2(-10f, Mathf.Clamp(point.y + 2.5f + bend * 0.7f, 1.8f, 8.5f));
        Vector2 d = new Vector2(10f, Mathf.Clamp(point.y + 2.5f - bend * 0.7f, 1.8f, 8.5f));
        Vector2 b = new Vector2(-6f, Mathf.Clamp(a.y + 1.5f, 1.8f, 8.5f));
        Vector2 c = point + Vector2.left * 1.2f;
        Vector3[] left = IllustratorGeometry.Curve(a, b, c, point, 40);
        b = point + Vector2.right * 1.2f;
        c = new Vector2(6f, Mathf.Clamp(d.y + 1.5f, 1.8f, 8.5f));
        Vector3[] right = IllustratorGeometry.Curve(point, b, c, d, 40);
        var points = new Vector3[81];
        System.Array.Copy(left, points, left.Length);
        System.Array.Copy(right, 1, points, left.Length, right.Length - 1);
        if (reverse) System.Array.Reverse(points);
        return points;
    }

    static Vector3[] DiagonalThrough(Vector2 point, float slope, bool reverse)
    {
        Vector2 a = new Vector2(-10f, point.y + (-10f - point.x) * slope);
        Vector2 d = new Vector2(10f, point.y + (10f - point.x) * slope);
        if (a.y < 0.35f || a.y > 8.5f)
        {
            a.y = Mathf.Clamp(a.y, 0.35f, 8.5f);
            a.x = point.x + (a.y - point.y) / slope;
        }
        if (d.y < 0.35f || d.y > 8.5f)
        {
            d.y = Mathf.Clamp(d.y, 0.35f, 8.5f);
            d.x = point.x + (d.y - point.y) / slope;
        }
        Vector3[] points = IllustratorGeometry.Curve(a, Vector2.Lerp(a, d, 1f / 3f), Vector2.Lerp(a, d, 2f / 3f), d);
        if (reverse) System.Array.Reverse(points);
        return points;
    }

    void RotationAttack()
    {
        if (!BeginDrawing()) return;
        Vector2 center = BodyBounds.center;
        Vector2 direction = ((Vector2)_playerBody.bounds.center - center).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = Vector2.left;
        int count = Enraged ? 3 : 2;
        int color = _colorStep++;
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Lerp(-75f, 75f, i / (float)(count - 1));
            Vector2 ray = Quaternion.Euler(0, 0, angle) * direction;
            Vector2 tangent = new Vector2(-ray.y, ray.x);
            Vector2 end = center + ray * 14f;
            end = new Vector2(Mathf.Clamp(end.x, -10f, 10f), Mathf.Clamp(end.y, 1.1f, 8.5f));
            Vector2 controlA = center + ray * 3f + tangent;
            Vector2 controlB = end - ray * 2f + tangent * 2.2f;
            controlA.y = Mathf.Clamp(controlA.y, 1.2f, 8.5f);
            controlB.y = Mathf.Clamp(controlB.y, 1.2f, 8.5f);
            if (Mathf.Abs(center.y - end.y) < 1f && Mathf.Abs(controlA.y - controlB.y) < 0.6f)
                controlA.y = Mathf.Min(8.5f, Mathf.Max(controlA.y, center.y + 2.2f));
            Vector3[] curve = IllustratorGeometry.Curve(center + ray * 0.9f,
                controlA, controlB, end);
            AddInk(curve, InkColors[(color + i) % InkColors.Length], false,
                1.4f + i * 0.17f, 0.6f, 0.15f);
        }
        _nextInk = Time.time + (Enraged ? 2.3f : 2.7f);
    }

    void UseSketch()
    {
        if (!CanAttack || Time.time < _readyAt || !_player.IsControllable || CountSketches(false) >= 3) return;
        var root = new GameObject("筆で描いて実体化");
        var summon = root.AddComponent<IllustratorSketchSummon>();
        summon.Initialize(IllustratorSketchSummon.PickKind(Random.value), _player, _boss, _bossBody);
        _summons.Add(summon);
        _readyAt = Time.time + 3f;
    }

    int CountSketches(bool hostile)
    {
        int count = 0;
        foreach (IllustratorSketchSummon summon in _summons)
            if (summon.Hostile == hostile && !summon.Finished) count++;
        return count;
    }

    void BossSketch()
    {
        if (CountSketches(true) >= 2 || !BeginDrawing()) return;
        var root = new GameObject("ボスが描くロケット・雷");
        var summon = root.AddComponent<IllustratorSketchSummon>();
        summon.Initialize(IllustratorSketchSummon.PickEnemyKind(Random.value), _player, _boss, _bossBody, true);
        _summons.Add(summon);
        _nextSketch = Time.time + (Enraged ? 3.8f : 4.3f);
    }

    void UpdateSummons()
    {
        for (int i = _summons.Count - 1; i >= 0; i--)
        {
            IllustratorSketchSummon summon = _summons[i];
            summon.Tick();
            if (!CanAttack) return; // 正常化通知でリストがクリアされる。
            if (summon.Finished) { Destroy(summon.gameObject); _summons.RemoveAt(i); }
        }
    }

    void AddInk(Vector3[] points, Color color, bool friendly, float warning, float draw, float hold,
        IllustratorInkStroke.DamageGroup group = null)
    {
        if (_strokes.Count >= 32) return;
        var root = new GameObject(friendly ? "CMY パス斬撃" : "ベクターストローク");
        var stroke = root.AddComponent<IllustratorInkStroke>();
        stroke.Initialize(points, color, friendly, warning, draw, hold, group);
        _strokes.Add(stroke);
    }

    void UpdateStrokes()
    {
        for (int i = _strokes.Count - 1; i >= 0; i--)
        {
            IllustratorInkStroke stroke = _strokes[i];
            stroke.Tick();
            if (stroke.Finished)
            {
                Destroy(stroke.gameObject); _strokes.RemoveAt(i); continue;
            }
            if (!stroke.Dangerous || stroke.Group.Hit || (stroke.Friendly && !_bossBody.enabled)) continue;
            Bounds target = stroke.Friendly ? BodyBounds : _playerBody.bounds;
            for (int n = 1; n < stroke.VisiblePoints; n++)
            {
                if (!IllustratorGeometry.SegmentHits(target, stroke.Points[n - 1], stroke.Points[n], stroke.Radius)) continue;
                var info = new DamageInfo { Amount = 2, GaugeGain = 18,
                    SourceTeam = stroke.Friendly ? Team.Player : Team.Enemy, HitPoint = target.center,
                    Knockback = new Vector2(Mathf.Sign(target.center.x - stroke.Points[0].x) * 4f, 3f), HitStun = 0.2f };
                bool hit = stroke.Friendly ? _boss.TryTakeDamage(info) : _health.TryTakeDamage(info);
                if (hit)
                {
                    stroke.Group.Hit = true;
                    Vfx.HitSpark(target.center);
                    if (_boss.IsFull || _health.IsDead) return; // 通知で攻撃リストが破棄される。
                }
                break;
            }
        }
    }

    void BeginWarp()
    {
        if (!CanAttack || _isWarping) return;
        _isWarping = true;
        _warpAt = Time.time;
        _warped = false;
        float x = _boss.transform.position.x > 0f ? -6f : 6f;
        _warpTarget = new Vector3(x, Random.value < 0.5f ? 1.25f : 4.2f, 0);
        _nextWarp = Time.time + Random.Range(7.5f, 10f);
    }

    void UpdateWarp()
    {
        if (!_isWarping) return;
        float t = Time.time - _warpAt;
        if (t >= 1.4f)
        {
            SetVisibility(1f);
            SetContactEnabled(true);
            _isWarping = false;
            return;
        }
        if (t >= 0.65f) SetContactEnabled(false);
        if (t < 0.85f) SetVisibility(1f - Mathf.InverseLerp(0.65f, 0.85f, t));
        else
        {
            if (!_warped) { Teleport(_warpTarget); _warped = true; }
            SetVisibility(Mathf.InverseLerp(1.1f, 1.35f, t));
        }
        if (t >= 1.35f) { SetVisibility(1f); SetContactEnabled(true); }
    }

    void ClearAttacks()
    {
        foreach (IllustratorInkStroke stroke in _strokes) if (stroke != null) Destroy(stroke.gameObject);
        foreach (IllustratorSketchSummon summon in _summons) if (summon != null) Destroy(summon.gameObject);
        _strokes.Clear(); _summons.Clear();
    }

    void OnGUI()
    {
        if (_health == null) return;
        PixelGui.Begin(false);
        if (_isWarping)
        {
            Vector3 p = _camera.WorldToScreenPoint(_warpTarget);
            Vector2 size = (Vector2)BodyBounds.size * (Screen.height / (_camera.orthographicSize * 2f)) / PixelGui.Scale;
            var rect = new Rect(p.x / PixelGui.Scale - size.x / 2, PixelGui.Height - p.y / PixelGui.Scale - size.y / 2, size.x, size.y);
            PixelGui.Border(rect, Blue, 1f);
        }
        if (_health.IsDead || _boss.IsFull)
            PixelGui.Text(new Rect(10, PixelGui.Height - 40, 180, 30), "R：リセット", 12, PixelGui.White);
        if (_controls)
        {
            float y = PixelGui.Height - 78;
            PixelGui.Fill(new Rect(0, y, PixelGui.Width, 78), PixelGui.Ink);
            PixelGui.Text(new Rect(12, y + 3, PixelGui.Width - 24, 20), "A/D 移動　Space ジャンプ　Shift ダッシュ　J 攻撃　1 お絵かき召喚　R リセット", 12, PixelGui.White);
            if (GUI.Button(new Rect(12, y + 27, 120, 25), "T：ペンツール")) PenAttack();
            if (GUI.Button(new Rect(140, y + 27, 120, 25), "U：回転コピー")) RotationAttack();
            if (GUI.Button(new Rect(268, y + 27, 110, 25), "I：ワープ")) BeginWarp();
            if (GUI.Button(new Rect(386, y + 27, 110, 25), "O：敵の召喚")) BossSketch();
            _auto = GUI.Toggle(new Rect(12, y + 54, 110, 24), _auto, "自動行動");
            _invincible = GUI.Toggle(new Rect(140, y + 54, 140, 24), _invincible, "無敵で試す");
        }
        PixelGui.End();
    }

    void OnDestroy()
    {
        if (_boss != null) _boss.Filled -= OnRestored;
        if (_health != null) _health.Died -= OnPlayerDied;
        ClearAttacks();
        if (_visual != null) Destroy(_visual.gameObject);
        if (_ability != null) Destroy(_ability);
        if (_icon != null) Destroy(_icon);
        if (_iconTexture != null) Destroy(_iconTexture);
    }
}
