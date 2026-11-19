using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Notionの技だけを試す独立した部屋。本編のカタログや装備は変更しない。</summary>
public sealed class NotionSkillDemo : MonoBehaviour, IWarpable
{
    [SerializeField] PlayerController _player;
    [SerializeField] VirusGauge _boss;
    [SerializeField] Collider2D _bossBody;
    [SerializeField] Camera _camera;
    [SerializeField] Texture2D _bossBodySheet;
    [SerializeField] Texture2D _bossFaceSheet;
    [SerializeField] Texture2D _bossHandsSheet;

    Sprite[] _bodyFrames, _faceFrames, _windowFrames;
    Sprite _hands, _notionIcon;
    readonly List<Sprite> _lookSprites = new List<Sprite>();
    readonly List<Texture2D> _lookTextures = new List<Texture2D>();
    float _restoredAt = -1f;
    SkillDefinition _hudAbility;
    bool _showControls;
    Transform _bossVisualRoot;
    SpriteRenderer _bodyVisual, _windowVisual, _handsVisual, _faceVisual;
    float _bossVisibility = 1f;
    bool _warped;
    Hitbox _bossContact;

    // Colliderを無効にしたワープ中も、見た目・予告に使う範囲は保持する。
    public Bounds BodyBounds
    {
        get
        {
            var box = _bossBody as BoxCollider2D;
            if (box == null) return _bossBody.bounds;
            Vector3 center = box.transform.TransformPoint(box.offset);
            Vector3 scale = box.transform.lossyScale;
            return new Bounds(center, new Vector3(box.size.x * Mathf.Abs(scale.x), box.size.y * Mathf.Abs(scale.y), 0f));
        }
    }

    public void SetVisibility(float alpha) => _bossVisibility = Mathf.Clamp01(alpha);
    public void SetContactEnabled(bool enabled)
    {
        bool on = enabled && !_boss.IsFull;
        _bossBody.enabled = on;
        if (_bossContact != null)
        {
            if (on) _bossContact.Activate(); else _bossContact.Deactivate();
        }
    }
    public void Teleport(Vector2 position)
    {
        _boss.transform.position = new Vector3(position.x, position.y, _boss.transform.position.z);
        Physics2D.SyncTransforms();
    }

    sealed class Shot
    {
        public Vector2 Position, Velocity;
        public float Life = 5f;
        public float Age;
        public float Delay;
        public bool Friendly, Homing;
        public Vector2 Size => Homing ? new Vector2(0.75f, 0.6f) : new Vector2(0.48f, 0.55f);
    }

    readonly List<Shot> _shots = new List<Shot>();
    readonly List<Vector2> _enemyToggles = new List<Vector2>();
    enum TaskKind { Jump, Move, Dash, Melee, Skill }
    sealed class TodoTask
    {
        public TaskKind Kind;
        public bool Done;
    }
    readonly List<TodoTask> _tasks = new List<TodoTask>();
    Health _health;
    PlayerMeleeAttack _melee;
    Collider2D _playerBody;
    Vector3 _start, _bossStart;
    Vector3 _bossMoveFrom, _bossMoveTo;
    int _attack; // 0:待機、1:ToDo、2:トグル、3:ブロック射出、4:ページ移動
    int _attackCycle, _bodyShots;
    float _attackAt, _nextAttack;
    bool _expanded;
    bool _auto = true, _invincible = true;
    float _moveStart;
    int _moveDirection;
    bool _playerToggle;
    Vector2 _togglePosition;
    int _toggleFacing;
    float _playerExpandedUntil, _readyAt, _playerToggleAt;
    int _playerPagesRemaining;
    float _nextPlayerPageAt;
    float _todoNextShot, _buffUntil;
    bool _todoRewardGranted;
    float _todoFailureAt = -10f;
    int _failedTaskMask;

    void Start()
    {
        _health = _player.GetComponent<Health>();
        _playerBody = _player.GetComponent<Collider2D>();
        _start = _player.transform.position;
        // VS Codeと同じ4×3の判定を基準に、体と判定を一緒に70%へ縮小。
        _boss.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
        if (_bossBody is BoxCollider2D box) box.size = new Vector2(4f, 3f);
        _bossStart = new Vector3(_boss.transform.position.x, 1.25f, _boss.transform.position.z);
        _bossContact = _bossBody.GetComponent<Hitbox>();
        if (_bossContact == null) _bossContact = _bossBody.gameObject.AddComponent<Hitbox>();
        _bossContact.SetArea(_bossBody);
        _bossContact.SetOwner(Team.Enemy, 1 << _player.gameObject.layer, false);
        _melee = _player.GetComponent<PlayerMeleeAttack>();
        _player.Jumped += OnJumped;
        _player.Dashed += OnDashed;
        if (_melee != null) _melee.Swung += OnSwung;
        _boss.Filled += OnRestored;
        BuildBossLook();
        var hud = _camera.GetComponentInChildren<PixelHud>();
        if (hud != null)
        {
            _hudAbility = SkillDefinition.CreateRuntime("トグル展開", _notionIcon, SkillCategory.Unknown, null, null);
            hud.BindTest(_boss, _notionIcon, _hudAbility, () => Mathf.Clamp01((_readyAt - Time.time) / 3f));
        }
        ResetDemo();
    }

    void OnDestroy()
    {
        if (_player != null) _player.Jumped -= OnJumped;
        if (_player != null) _player.Dashed -= OnDashed;
        if (_melee != null) _melee.Swung -= OnSwung;
        if (_boss != null) _boss.Filled -= OnRestored;
        foreach (Sprite sprite in _lookSprites) if (sprite != null) Destroy(sprite);
        foreach (Texture2D texture in _lookTextures) if (texture != null) Destroy(texture);
        if (_hudAbility != null) Destroy(_hudAbility);
        if (_bossVisualRoot != null) Destroy(_bossVisualRoot.gameObject);
        if (_player != null) _player.ClearMoveSpeedBoost();
    }

    void OnJumped() => CompleteTask(TaskKind.Jump);
    void OnDashed() => CompleteTask(TaskKind.Dash);
    void OnSwung() => CompleteTask(TaskKind.Melee);

    void CompleteTask(TaskKind kind)
    {
        if (_attack != 1 || Time.time - _attackAt > 5f) return;
        foreach (TodoTask task in _tasks)
            if (task.Kind == kind) task.Done = true;
        if (_todoRewardGranted || _tasks.Count == 0) return;
        foreach (TodoTask task in _tasks) if (!task.Done) return;
        _todoRewardGranted = true;
        _buffUntil = Time.time + 5f;
        _player.GrantMoveSpeedBoost(1.25f, 5f);
        Vfx.Sparkle(_playerBody.bounds.center);
    }

    string TaskText(TaskKind kind)
    {
        switch (kind)
        {
            case TaskKind.Jump: return "ジャンプする";
            case TaskKind.Move: return _moveDirection > 0 ? "右へ2マス移動する" : "左へ2マス移動する";
            case TaskKind.Dash: return "ダッシュする";
            case TaskKind.Melee: return "近接攻撃する";
            default: return "スキルを使う";
        }
    }

    void OnRestored()
    {
        _todoFailureAt = -10f;
        _restoredAt = Time.time;
        Vfx.Smoke(_boss.transform.position);
        _shots.Clear();
        _enemyToggles.Clear();
        _attack = 0;
        SetContactEnabled(false);
        SetVisibility(1f);
        _playerToggle = false;
        _playerPagesRemaining = 0;
        _playerExpandedUntil = 0f;
    }

    void ResetDemo()
    {
        _todoFailureAt = -10f;
        _failedTaskMask = 0;
        _shots.Clear();
        _enemyToggles.Clear();
        _boss.ResetGauge();
        _restoredAt = -1f;
        _boss.transform.position = _bossStart;
        SetContactEnabled(true);
        SetVisibility(1f);
        _warped = false;
        _health.ResetHealth();
        _player.CanControl = true;
        _player.SetVelocity(Vector2.zero);
        _player.GetComponent<KinematicBody2D>().Warp(_start);
        _attack = 0;
        _nextAttack = Time.time + 2f;
        _attackCycle = 0;
        _playerToggle = false;
        _playerPagesRemaining = 0;
        _readyAt = _playerExpandedUntil = 0f;
        _buffUntil = 0f;
        _player.ClearMoveSpeedBoost();
    }

    void Update()
    {
        if (_player == null || _boss == null) return;
        var kb = Keyboard.current;
        if (kb != null && kb.f1Key.wasPressedThisFrame) _showControls = !_showControls;
        if (kb != null && kb.rKey.wasPressedThisFrame) ResetDemo();
        if (_invincible && !_health.IsDead) _health.SetInvincible(0.1f);
        if (_health.IsDead || _boss.IsFull) return;
        if (kb != null)
        {
            if (kb.tKey.wasPressedThisFrame) BeginTodo();
            if (kb.yKey.wasPressedThisFrame) BeginToggle();
            if (kb.uKey.wasPressedThisFrame) BeginBodyAttack();
            if (kb.iKey.wasPressedThisFrame) BeginBossMove();
            if (kb.digit1Key.wasPressedThisFrame) UsePlayerToggle();
            if (kb.digit1Key.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame || kb.digit3Key.wasPressedThisFrame)
                CompleteTask(TaskKind.Skill);
        }
        if (_playerToggle)
        {
            FollowPlayerToggle();
            if (Time.time >= _playerToggleAt + 0.5f) ExpandPlayerToggle();
        }
        if (_playerPagesRemaining > 0 && Time.time >= _nextPlayerPageAt)
        {
            FollowPlayerToggle();
            FirePlayerPage(1);
            _playerPagesRemaining = 0;
        }

        if (_attack == 0 && _auto && Time.time >= _nextAttack)
        {
            switch (_attackCycle++ % 4)
            {
                case 0: BeginTodo(); break;
                case 1: BeginBodyAttack(); break;
                case 2: BeginBossMove(); break;
                default: BeginToggle(); break;
            }
        }
        if (_attack == 1)
        {
            if (Time.time - _attackAt < 5f && Time.time >= _todoNextShot)
            {
                FireBodyVolley(3, 4.5f);
                _todoNextShot = Time.time + 1.5f;
            }
            if ((_player.transform.position.x - _moveStart) * _moveDirection >= 2f) CompleteTask(TaskKind.Move);
            if (Time.time - _attackAt >= 5f)
            {
                int failed = 0;
                _failedTaskMask = 0;
                for (int row = 0; row < _tasks.Count; row++)
                {
                    if (_tasks[row].Done) continue;
                    failed++;
                    _failedTaskMask |= 1 << row;
                }
                if (failed > 0)
                {
                    _todoFailureAt = Time.time;
                    FireOverdueTasks();
                }
                FinishAttack();
            }
        }
        if (_attack == 2)
        {
            float elapsed = Time.time - _attackAt;
            if (!_expanded && elapsed >= 2f)
            {
                _expanded = true;
                foreach (Vector2 point in _enemyToggles)
                    FireFan(point, _playerBody.bounds.center, 5, 5.5f, false);
            }
            if (elapsed >= 3.2f) FinishAttack();
        }
        if (_attack == 3)
        {
            float elapsed = Time.time - _attackAt;
            if (_bodyShots < 3 && elapsed >= 0.8f + _bodyShots * 0.45f)
            {
                FireBodyVolley(3, 6f);
                _bodyShots++;
            }
            if (elapsed >= 2.3f) FinishAttack();
        }
        if (_attack == 4)
        {
            float elapsed = Time.time - _attackAt;
            // VS CodeのWarpと同様に、消える → 瞬間移動 → 現れる。移動経路には判定を出さない。
            if (elapsed >= 0.65f) SetContactEnabled(false);
            if (elapsed < 0.85f)
                SetVisibility(1f - Mathf.InverseLerp(0.65f, 0.85f, elapsed));
            else
            {
                if (!_warped)
                {
                    Teleport(_bossMoveTo);
                    _warped = true;
                }
                SetVisibility(Mathf.InverseLerp(1.15f, 1.4f, elapsed));
            }
            if (elapsed >= 1.4f) FinishAttack();
        }
        UpdateShots();
    }

    void FinishAttack()
    {
        SetVisibility(1f);
        SetContactEnabled(true);
        _attack = 0;
        _enemyToggles.Clear();
        _nextAttack = Time.time + 2.5f;
    }

    void BeginTodo()
    {
        if (_health.IsDead || _boss.IsFull) return;
        StartAttack(1);
        _tasks.Clear();
        _todoFailureAt = -10f;
        _todoRewardGranted = false;
        _todoNextShot = Time.time + 1f;
        // 同じ課題が重ならないよう、5種類から2種類を抽選。
        int first = Random.Range(0, 5);
        int second = Random.Range(0, 4);
        if (second >= first) second++;
        _tasks.Add(new TodoTask { Kind = (TaskKind)first });
        _tasks.Add(new TodoTask { Kind = (TaskKind)second });
        _moveStart = _player.transform.position.x;
        _moveDirection = _moveStart > 6f ? -1 : _moveStart < -6f ? 1 : Random.value < 0.5f ? -1 : 1;
    }

    void BeginToggle()
    {
        if (_health.IsDead || _boss.IsFull) return;
        StartAttack(2);
        _expanded = false;
        _enemyToggles.Add(new Vector2(-6f, 7f));
        _enemyToggles.Add(new Vector2(0f, 8f));
        _enemyToggles.Add(new Vector2(6f, 7f));
    }

    void StartAttack(int kind)
    {
        SetVisibility(1f);
        SetContactEnabled(true);
        _warped = false;
        _attack = kind;
        _attackAt = Time.time;
        _enemyToggles.Clear();
    }

    void BeginBodyAttack()
    {
        if (_health.IsDead || _boss.IsFull) return;
        StartAttack(3);
        _bodyShots = 0;
    }

    void BeginBossMove()
    {
        if (_health.IsDead || _boss.IsFull) return;
        StartAttack(4);
        _bossMoveFrom = _boss.transform.position;
        // 今いる場所とは違う列へ。下段・上段のどちらも近接で届く高さにする。
        int current = Mathf.Clamp(Mathf.RoundToInt((_bossMoveFrom.x + 6f) / 6f), 0, 2);
        int next = Random.Range(0, 2);
        if (next >= current) next++;
        _bossMoveTo = new Vector3(-6f + next * 6f, Random.value < 0.5f ? 1.25f : 4.2f, _bossMoveFrom.z);
    }

    void UsePlayerToggle()
    {
        if (_health.IsDead || _boss.IsFull || _playerToggle || Time.time < _readyAt) return;
        FollowPlayerToggle();
        _playerToggle = true;
        _playerToggleAt = Time.time;
        _readyAt = Time.time + 3f;
    }

    void FollowPlayerToggle()
    {
        _toggleFacing = _player.FacingDirection;
        Vector2 center = _playerBody.bounds.center;
        float x = center.x + _toggleFacing * 1.5f;
        // 壁際では内側に置き、プレイヤーと重なる位置へ押し戻さない。
        if (Mathf.Abs(x) > 9f)
        {
            _toggleFacing = -_toggleFacing;
            x = center.x + _toggleFacing * 1.5f;
        }
        _togglePosition = new Vector2(x, center.y + 0.2f);
    }

    void FireBodyVolley(int count, float speed)
    {
        Vector2 center = BodyBounds.center;
        Vector2 direction = ((Vector2)_playerBody.bounds.center - center).normalized;
        FireFan(center + direction * 1.5f, _playerBody.bounds.center, count, speed, false);
    }

    void ExpandPlayerToggle()
    {
        // トグルを開いて中のページを2枚だけ、順に取り出す。
        FirePlayerPage(0);
        _playerPagesRemaining = 1;
        _nextPlayerPageAt = Time.time + 0.18f;
        _playerToggle = false;
        _playerExpandedUntil = Time.time + 0.6f;
    }

    void FirePlayerPage(int index)
    {
        Vector2 direction = Quaternion.Euler(0f, 0f, index == 0 ? -4f : 4f) * (Vector2.right * _toggleFacing);
        _shots.Add(new Shot { Position = _togglePosition, Velocity = direction * 11f, Friendly = true });
    }

    void FireFan(Vector2 from, Vector2 target, int count, float speed, bool friendly)
    {
        Vector2 direction = (target - from).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = Vector2.left;
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Lerp(-22f, 22f, i / (float)(count - 1));
            Vector2 velocity = Quaternion.Euler(0f, 0f, angle) * direction;
            _shots.Add(new Shot { Position = from, Velocity = velocity * speed, Friendly = friendly });
        }
    }

    public static Vector2 OverdueSpawnPoint(Vector2 target, int index)
    {
        float centerX = Mathf.Clamp(target.x, -4.3f, 4.3f);
        Vector2 point = index == 0 ? new Vector2(centerX - 4.5f, target.y + 1.2f)
            : index == 1 ? new Vector2(centerX + 4.5f, target.y + 1.8f)
            : new Vector2(centerX, target.y + 5f);
        point.y = Mathf.Clamp(point.y, 0.8f, 8.5f);
        // 壁際・高所でもプレイヤーのすぐ隣には召喚しない。
        if (Vector2.Distance(point, target) < 3f) point.y = target.y > 4.5f ? 1.2f : 8.5f;
        return point;
    }

    void FireOverdueTasks()
    {
        Vector2 target = _playerBody.bounds.center;
        for (int i = 0; i < 3; i++)
        {
            Vector2 from = OverdueSpawnPoint(target, i);
            _shots.Add(new Shot { Position = from, Velocity = (target - from).normalized * 4f,
                Homing = true, Life = 5.5f, Delay = 0.55f + i * 0.25f });
        }
    }

    void UpdateShots()
    {
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            Shot shot = _shots[i];
            if (shot.Delay > 0f)
            {
                shot.Delay -= Time.deltaTime;
                continue;
            }
            Vector2 previous = shot.Position;
            shot.Age += Time.deltaTime;
            if (shot.Homing && shot.Age < 2.2f)
            {
                Vector2 desired = ((Vector2)_playerBody.bounds.center - shot.Position).normalized;
                Vector3 heading = Vector3.RotateTowards(shot.Velocity.normalized, desired,
                    Mathf.Deg2Rad * 75f * Time.deltaTime, 0f);
                shot.Velocity = (Vector2)heading * Mathf.Lerp(4f, 6f, Mathf.Clamp01(shot.Age / 2f));
            }
            shot.Position += shot.Velocity * Time.deltaTime;
            shot.Life -= Time.deltaTime;
            Bounds target = shot.Friendly ? BodyBounds : _playerBody.bounds;
            target.Expand(new Vector3(shot.Size.x, shot.Size.y, 0f));
            if ((!shot.Friendly || _bossBody.enabled) && SegmentHits(target, previous, shot.Position))
            {
                if (shot.Friendly)
                {
                    // Filledでリストが消える前に取り除く。
                    _shots.RemoveAt(i);
                    _boss.Add(6);
                    if (_boss.IsFull) return;
                }
                else
                {
                    _shots.RemoveAt(i);
                    _health.TryTakeDamage(new DamageInfo { Amount = 1, SourceTeam = Team.Enemy,
                        HitPoint = shot.Position, HitStun = 0.15f,
                        Knockback = new Vector2(Mathf.Sign(shot.Velocity.x) * 3f, 2f) });
                }
                continue;
            }
            if (shot.Life <= 0f || shot.Position.y < 0f || shot.Position.y > 12f || Mathf.Abs(shot.Position.x) > 12f)
                _shots.RemoveAt(i);
        }
    }

    static bool SegmentHits(Bounds bounds, Vector2 from, Vector2 to)
    {
        if (bounds.Contains(new Vector3(from.x, from.y, bounds.center.z))) return true;
        Vector2 delta = to - from;
        return delta.sqrMagnitude > 0f && bounds.IntersectRay(
            new Ray(new Vector3(from.x, from.y, bounds.center.z), delta.normalized), out float distance)
            && distance <= delta.magnitude;
    }

    Rect WorldRect(Vector2 center, Vector2 size)
    {
        Vector3 a = _camera.WorldToScreenPoint(center - size * 0.5f);
        Vector3 b = _camera.WorldToScreenPoint(center + size * 0.5f);
        return new Rect(a.x / PixelGui.Scale, PixelGui.Height - b.y / PixelGui.Scale,
            (b.x - a.x) / PixelGui.Scale, (b.y - a.y) / PixelGui.Scale);
    }

    static void Page(Rect rect, bool infected)
    {
        PixelGui.Fill(rect, infected ? new Color32(235, 220, 241, 255) : PixelGui.Card);
        PixelGui.Border(rect, infected ? PixelGui.Magenta : PixelGui.Ink, 2f);
    }

    static void TogglePage(Rect rect, bool expanded, bool infected)
    {
        Page(rect, infected);
        float unit = Mathf.Min(1f, rect.height / 18f);
        float x = rect.x + 6 * unit, y = rect.center.y - 4 * unit;
        for (int i = 0; i < 5; i++)
        {
            Rect dot = expanded ? new Rect(x + i * unit, y + i * unit, (9 - i * 2) * unit, 1)
                : new Rect(x + i * unit, y + i * unit, 1, (9 - i * 2) * unit);
            PixelGui.Fill(dot, PixelGui.Ink);
        }
        PixelGui.Fill(new Rect(x + 16 * unit, rect.center.y - 2 * unit, Mathf.Max(1, rect.width - 32 * unit), 2), PixelGui.FrameDark);
        PixelGui.Fill(new Rect(x + 16 * unit, rect.center.y + 2 * unit, Mathf.Max(1, rect.width - 44 * unit), 1), PixelGui.Frame);
    }

    void BuildBossLook()
    {
        // Chrome / VS Codeと同じ体・手・顔をそのまま使う。
        _bodyFrames = Slice(_bossBodySheet, 3);
        _faceFrames = Slice(_bossFaceSheet, 4);
        Sprite[] hands = Slice(_bossHandsSheet, 1);
        _hands = hands.Length > 0 ? hands[0] : null;

        // Notionの白黒のNを既存のウィンドウ生成処理へ渡す。
        var texture = new Texture2D(24, 24, TextureFormat.RGBA32, false)
            { name = "Notion pixel icon", filterMode = FilterMode.Point };
        var pixels = new Color32[24 * 24];
        for (int y = 2; y < 22; y++)
            for (int x = 2; x < 22; x++)
            {
                bool border = x == 2 || x == 21 || y == 2 || y == 21;
                bool n = y >= 6 && y <= 17 && ((x >= 6 && x <= 8) || (x >= 15 && x <= 17)
                    || Mathf.Abs(x - (8f + (17 - y) * 7f / 11f)) < 1.4f);
                pixels[y * 24 + x] = border || n ? new Color32(22, 22, 22, 255) : new Color32(255, 255, 255, 255);
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        _lookTextures.Add(texture);
        Sprite icon = Sprite.Create(texture, new Rect(0, 0, 24, 24), Vector2.one * 0.5f, GameArt.PixelsPerUnit);
        _notionIcon = icon;
        _lookSprites.Add(icon);
        _windowFrames = BossWindowArt.Create(icon, null, "Notion window");
        foreach (Sprite sprite in _windowFrames)
        {
            _lookSprites.Add(sprite);
            _lookTextures.Add(sprite.texture);
        }
        _bossVisualRoot = new GameObject("Notion boss art").transform;
        _bossVisualRoot.SetParent(_boss.transform, false);
        _bodyVisual = CreateBossRenderer("Body", 10);
        _windowVisual = CreateBossRenderer("Window", 12);
        _handsVisual = CreateBossRenderer("Hands", 13);
        _faceVisual = CreateBossRenderer("Face", 13);
    }

    Sprite[] Slice(Texture2D sheet, int count)
    {
        if (sheet == null) return new Sprite[0];
        var frames = new Sprite[count];
        int width = sheet.width / count;
        for (int i = 0; i < count; i++)
        {
            frames[i] = Sprite.Create(sheet, new Rect(i * width, 0, width, sheet.height),
                Vector2.one * 0.5f, GameArt.PixelsPerUnit);
            _lookSprites.Add(frames[i]);
        }
        return frames;
    }

    SpriteRenderer CreateBossRenderer(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_bossVisualRoot, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = order;
        return renderer;
    }

    void SetBossSprite(SpriteRenderer renderer, Sprite sprite, Vector2 offset, Color color)
    {
        renderer.sprite = sprite;
        renderer.transform.localPosition = offset;
        color.a *= _bossVisibility;
        renderer.color = color;
        renderer.enabled = sprite != null && color.a > 0.01f;
    }

    void LateUpdate()
    {
        if (_playerToggle || Time.time < _playerExpandedUntil) FollowPlayerToggle();
        if (_bossVisualRoot == null) return;
        bool restored = _boss.IsFull;
        _bodyVisual.enabled = _handsVisual.enabled = _faceVisual.enabled = false;
        if (!restored && _bodyFrames.Length > 0)
        {
            int frame = Mathf.FloorToInt(Time.time * 6f) % _bodyFrames.Length;
            SetBossSprite(_bodyVisual, _bodyFrames[frame], Vector2.up * (7f / 18f), Color.white);
        }
        if (_windowFrames.Length > 0)
        {
            int stage = restored ? 0 : _boss.Ratio >= 2f / 3f ? 2 : _boss.Ratio >= 1f / 3f ? 1 : 0;
            float t = restored && _restoredAt >= 0f ? Mathf.Clamp01((Time.time - _restoredAt) / 1.6f) : 0f;
            var tint = new Color(1f, 1f, 1f, restored ? 1f - t : 1f);
            if (!restored && _attack == 3 && _bodyShots == 0)
                tint = new Color(1f, 0.8f, 0.85f, _bossVisibility);
            Vector2 offset = Vector2.up * (-5f / 18f + t * 1.2f);
            SetBossSprite(_windowVisual, _windowFrames[stage], offset, tint);
        }
        if (!restored)
        {
            SetBossSprite(_handsVisual, _hands, Vector2.up * (7f / 18f), Color.white);
            if (_faceFrames.Length >= 4)
            {
                int frame = (_boss.Ratio >= 0.5f ? 2 : 0) + (Mathf.Repeat(Time.time, 3.4f) < 0.12f ? 1 : 0);
                SetBossSprite(_faceVisual, _faceFrames[frame], Vector2.up * (25f / 18f), Color.white);
            }
        }
    }

    void DrawTodoPage(float width)
    {
        float sinceFailure = Time.time - _todoFailureAt;
        bool failed = sinceFailure >= 0f && sinceFailure < 0.5f;
        var page = new Rect((width - 236f) * 0.5f, 58, 236, 86);
        if (failed) page.x += Mathf.Round(Mathf.Sin(sinceFailure * 48f) * 3f * (1f - sinceFailure / 0.5f));
        var ink = new Color32(55, 53, 47, 255);
        var blue = new Color32(45, 139, 209, 255);
        PixelGui.Fill(new Rect(page.x + 2, page.y + 2, page.width, page.height), new Color(0, 0, 0, 0.18f));
        PixelGui.Fill(page, failed ? new Color32(255, 232, 229, 255) : new Color32(255, 254, 250, 255));
        PixelGui.Border(page, failed ? PixelGui.DarkRed : PixelGui.Frame, 1f);
        PixelGui.DrawSprite(new Rect(page.x + 9, page.y + 7, 14, 14), _notionIcon);
        PixelGui.Text(new Rect(page.x + 29, page.y + 5, 100, 20), "To-do", 14, ink);
        float remaining = failed ? 0f : Mathf.Clamp01(1f - (Time.time - _attackAt) / 5f);
        PixelGui.Text(new Rect(page.xMax - 51, page.y + 6, 41, 18), $"{remaining * 5f:0.0}s", 10,
            failed ? PixelGui.DarkRed : PixelGui.SubText, TextAnchor.MiddleRight);
        PixelGui.Fill(new Rect(page.x + 10, page.y + 27, page.width - 20, 1), PixelGui.Muted);
        PixelGui.Fill(new Rect(page.x + 10, page.y + 27, (page.width - 20) * remaining, 1), blue);
        for (int i = 0; i < _tasks.Count; i++)
        {
            TodoTask task = _tasks[i];
            float y = page.y + 35 + i * 22;
            var box = new Rect(page.x + 11, y + 3, 9, 9);
            bool overdue = failed && !task.Done;
            PixelGui.Border(box, overdue ? PixelGui.DarkRed : task.Done ? blue : PixelGui.Frame, 1f);
            if (task.Done)
            {
                PixelGui.Fill(box, blue);
                PixelGui.Fill(new Rect(box.x + 2, box.y + 4, 3, 2), PixelGui.White);
                PixelGui.Fill(new Rect(box.x + 4, box.y + 2, 2, 4), PixelGui.White);
                PixelGui.Fill(new Rect(box.x + 6, box.y + 1, 1, 3), PixelGui.White);
            }
            if (overdue)
                for (int dot = 1; dot < 8; dot++)
                {
                    PixelGui.Fill(new Rect(box.x + dot, box.y + dot, 1, 1), PixelGui.DarkRed);
                    PixelGui.Fill(new Rect(box.x + 8 - dot, box.y + dot, 1, 1), PixelGui.DarkRed);
                }
            var line = new Rect(page.x + 29, y - 1, page.width - 39, 18);
            PixelGui.TextFit(line, TaskText(task.Kind), 12,
                overdue ? PixelGui.DarkRed : task.Done ? PixelGui.Frame : ink);
            if (task.Done)
                PixelGui.Fill(new Rect(line.x, line.center.y, Mathf.Min(line.width,
                    PixelGui.Measure(TaskText(task.Kind), PixelGui.Style(false, 12))), 1), PixelGui.Frame);
        }
    }

    void DrawTodoFailure(float width)
    {
        float t = (Time.time - _todoFailureAt) / 0.8f;
        if (t < 0f || t >= 1f) return;
        for (int row = 0; row < 2; row++)
        {
            if ((_failedTaskMask & (1 << row)) == 0) continue;
            Vector2 center = new Vector2((width - 236f) * 0.5f + 15f, 100f + row * 22f);
            for (int shard = 0; shard < 9; shard++)
            {
                float angle = shard * Mathf.PI * 2f / 9f + row * 0.3f;
                float distance = (12f + shard % 3 * 8f) * t;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance + Vector2.up * t * t * 12f;
                Color tint = shard % 3 == 0 ? PixelGui.Magenta : PixelGui.DarkRed;
                tint.a = 1f - t;
                PixelGui.Fill(new Rect(Mathf.Round(center.x + offset.x), Mathf.Round(center.y + offset.y),
                    shard % 2 == 0 ? 3 : 2, 2), tint);
            }
        }
    }

    void OnGUI()
    {
        if (_player == null || _camera == null || _health == null) return;
        GUI.depth = 20; // 共通HUD（10）の後ろに技の見た目を描く。
        PixelGui.Begin(false);
        float w = PixelGui.Width, h = PixelGui.Height;
        if (_attack == 4)
        {
            Vector3 offset = BodyBounds.center - _boss.transform.position;
            Rect destination = WorldRect(_bossMoveTo + offset, BodyBounds.size);
            PixelGui.Fill(destination, new Color(0.8f, 0.6f, 0.95f, 0.18f));
            PixelGui.Border(destination, PixelGui.LabelPurple, 1f);
        }
        foreach (Vector2 point in _enemyToggles)
            TogglePage(WorldRect(point, new Vector2(3.6f, 0.65f)), _expanded, true);
        if (_playerToggle || Time.time < _playerExpandedUntil)
            TogglePage(WorldRect(_togglePosition, new Vector2(1.6f, 0.42f)), !_playerToggle, false);
        foreach (Shot shot in _shots)
        {
            if (shot.Delay > 0f)
            {
                Rect warning = WorldRect(shot.Position, shot.Size * 1.5f);
                PixelGui.Border(warning, PixelGui.DarkRed, 1f);
                PixelGui.Fill(new Rect(warning.x + 2, warning.yMax - 3,
                    (warning.width - 4) * Mathf.Clamp01(1f - shot.Delay / 1.05f), 1), PixelGui.DarkRed);
                continue;
            }
            if (shot.Homing)
            {
                for (int i = 1; i <= 3; i++)
                {
                    Vector2 tail = shot.Position - shot.Velocity.normalized * i * 0.28f;
                    PixelGui.Fill(WorldRect(tail, Vector2.one * 0.12f),
                        new Color(0.9f, 0.25f, 0.5f, 0.4f / i));
                }
            }
            Rect r = WorldRect(shot.Position, shot.Size);
            Color ink = shot.Homing ? PixelGui.DarkRed : PixelGui.Ink;
            PixelGui.Fill(r, shot.Homing ? new Color32(255, 227, 225, 255) : PixelGui.Card);
            PixelGui.Border(r, ink, 1f);
            // Notionのページ：折れた右上角、チェックボックス、本文の罫線。
            PixelGui.Fill(new Rect(r.xMax - 4, r.y, 4, 4), ink);
            PixelGui.Fill(new Rect(r.xMax - 3, r.y + 1, 2, 2), PixelGui.Panel);
            if (shot.Homing) PixelGui.Fill(new Rect(r.x + 1, r.y + 1, r.width - 5, 2), PixelGui.DarkRed);
            PixelGui.Border(new Rect(r.x + 3, r.y + 5, 4, 4), ink, 1f);
            if (shot.Friendly) PixelGui.Fill(new Rect(r.x + 4, r.y + 6, 2, 2), PixelGui.LabelGreen);
            PixelGui.Fill(new Rect(r.x + 9, r.y + 6, Mathf.Max(1, r.width - 12), 1), ink);
            PixelGui.Fill(new Rect(r.x + 3, r.y + 11, Mathf.Max(1, r.width - 6), 1), ink);
        }

        if (Time.time < _buffUntil && !_health.IsDead)
        {
            Vector2 center = _playerBody.bounds.center;
            for (int i = 0; i < 3; i++)
                PixelGui.Fill(WorldRect(center + new Vector2(-_player.FacingDirection * (0.55f + i * 0.18f),
                    0.24f - i * 0.2f), new Vector2(0.3f, 0.045f)), PixelGui.HpGreen);
            PixelGui.Text(new Rect(10, 51, 24, 18), "↑", 14, PixelGui.HpGreen);
            PixelGui.Fill(new Rect(31, 58, 50 * Mathf.Clamp01((_buffUntil - Time.time) / 5f), 3), PixelGui.HpGreen);
        }
        if (_attack == 1 || Time.time - _todoFailureAt < 0.5f) DrawTodoPage(w);
        DrawTodoFailure(w);
        if (_health.IsDead || _boss.IsFull)
        {
            PixelGui.Fill(new Rect(10, h - 120, w - 20, 30), PixelGui.Ink);
            PixelGui.Text(new Rect(20, h - 120, w - 40, 30), "R：リセット", 14, PixelGui.White);
        }
        if (!_showControls)
        {
            PixelGui.End();
            return;
        }
        PixelGui.Fill(new Rect(0, h - 83, w, 83), PixelGui.Ink);
        PixelGui.Text(new Rect(12, h - 81, w - 24, 20), "A/D 移動　Space ジャンプ　Shift ダッシュ　J 攻撃　1 トグル　R リセット", 12, PixelGui.White);
        if (GUI.Button(new Rect(12, h - 58, 142, 26), "T：ToDo強制執行")) BeginTodo();
        if (GUI.Button(new Rect(162, h - 58, 115, 26), "Y：トグル展開")) BeginToggle();
        if (GUI.Button(new Rect(285, h - 58, 115, 26), "U：本体攻撃")) BeginBodyAttack();
        if (GUI.Button(new Rect(408, h - 58, 115, 26), "I：ページ移動")) BeginBossMove();
        _auto = GUI.Toggle(new Rect(12, h - 27, 110, 24), _auto, "自動行動");
        _invincible = GUI.Toggle(new Rect(135, h - 27, 150, 24), _invincible, "無敵で試す");
        PixelGui.End();
    }
}
