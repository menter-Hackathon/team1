using UnityEngine;

/// <summary>
/// 召喚した味方：AI アシスタント（2026-11-18。能力「AIアシスタント」。数値は【仮】）。AttackRunner が AttackData（種類 Summon）から作る。
/// ・持ち主（プレイヤー）の後ろ上に、羽ばたきながら浮かんで付いていく
/// ・持ち主の体の中心から「敵を探す範囲」に入った敵のうち、一番近い敵へレーザーを撃つ（地形にさえぎられる敵は狙わない）
///   レーザーの判定は Hitbox。ヒット間隔のあいだ当て続けるたびに、判定を一瞬だけ出す（撃ち始め・狙い直しの直後は当たらない）。
///   途中にいる別の敵にも当たる
/// ・レーザーを撃った合計が「撃てる秒数」に達するか、「いられる秒数」が過ぎたら、点滅して消える。持ち主が倒れたときも消える
/// ・消えるまで、AttackRunner の攻撃は終わらない（能力のクールタイムは消えてから数える。PlayerSkillExecutor）
/// 見た目：体の絵は攻撃データの「召喚した味方の絵」、レーザーは「見た目」の絵（横に並べる）と色。下に、残りのレーザーの量を細い棒で出す
/// </summary>
public class AssistantDrone : MonoBehaviour
{
    /// <summary>敵を探し直す間隔（秒）</summary>
    const float RetargetInterval = 0.1f;
    /// <summary>消えるときの点滅の秒数</summary>
    const float VanishTime = 0.45f;
    /// <summary>残りがこの秒数を切ったら、点滅して知らせる</summary>
    const float WarnTime = 1.5f;

    AttackRunner _owner;
    AttackData _data;
    Transform _ownerBody;
    Health _ownerHealth;

    SpriteRenderer _body;
    SpriteRenderer _beam;
    Transform _beamRoot;
    BoxCollider2D _beamCollider;
    Hitbox _beamHitbox;
    SpriteRenderer _gaugeBack;
    SpriteRenderer _gaugeFill;
    Sprite[] _frames;

    float _bornAt;
    float _fired;          // 撃った合計の秒数
    float _nextRetarget;
    float _nextTick;
    float _hitUntil;       // 判定を出しておく時刻（ヒット間隔ごとに一瞬だけ出す）
    Collider2D _beamTarget; // 今のレーザーで当て続けている敵（変わったら数え直す）
    /// <summary>1回の判定を出しておく秒数（物理の更新が何回か入る長さ）</summary>
    const float HitPulse = 0.1f;
    float _vanishAt = -1f; // 消え始めた時刻（消えていなければ -1）
    Collider2D _target;
    int _facing = 1;

    readonly Collider2D[] _found = new Collider2D[24];
    readonly RaycastHit2D[] _rayHits = new RaycastHit2D[8];

    public AttackData Data => _data;
    /// <summary>今レーザーを撃っているか（テスト・演出用）</summary>
    public bool IsFiring => _target != null && _vanishAt < 0f;
    /// <summary>残りのレーザーの秒数</summary>
    public float FireRemaining => Mathf.Max(0f, _data.SummonFireTime - _fired);
    /// <summary>残りのいられる秒数</summary>
    public float LifeRemaining => Mathf.Max(0f, _data.SummonLifetime - (Time.time - _bornAt));
    /// <summary>今狙っている敵の被弾範囲。いなければ null</summary>
    public Collider2D CurrentTarget => _target;

    /// <summary>味方を出す。持ち主の後ろ上に現れる</summary>
    public static AssistantDrone Spawn(AttackRunner owner, AttackData data)
    {
        Vector2 start = HoverPoint(owner.transform, owner.Facing, data);
        GameObject go = owner.CreateSpawnedObject($"Summon_{data.name}", start);
        var drone = go.AddComponent<AssistantDrone>();
        drone.Init(owner, data);
        return drone;
    }

    static Vector2 HoverPoint(Transform owner, int facing, AttackData data)
    {
        Vector2 off = data.SummonHoverOffset;
        return (Vector2)owner.position + new Vector2(-facing * off.x, off.y);
    }

    void Init(AttackRunner owner, AttackData data)
    {
        _owner = owner;
        _data = data;
        _ownerBody = owner.transform;
        _ownerHealth = owner.GetComponent<Health>();
        _bornAt = Time.time;
        _facing = owner.Facing;

        // 体（コマ送り）。絵が無ければ白い四角
        _frames = data.SummonFrames;
        Sprite first = _frames.Length > 0 ? _frames[0] : null;
        _body = new GameObject("Body").AddComponent<SpriteRenderer>();
        _body.gameObject.layer = gameObject.layer;
        _body.transform.SetParent(transform, false);
        _body.sortingOrder = 14; // プレイヤー（15）の後ろ
        if (first != null) _body.sprite = first;
        else
        {
            _body.sprite = PlaceholderSprites.Square;
            _body.transform.localScale = new Vector3(0.8f, 0.5f, 1f);
            _body.color = new Color(0.6f, 0.95f, 1f);
        }

        // レーザー：根元に置いた入れ物を回し、x 方向に伸ばす。判定も同じ形
        _beamRoot = new GameObject("Beam").transform;
        _beamRoot.gameObject.layer = gameObject.layer;
        _beamRoot.SetParent(transform, false);
        var rb = _beamRoot.gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        _beamCollider = _beamRoot.gameObject.AddComponent<BoxCollider2D>();
        _beamCollider.isTrigger = true;
        _beamCollider.size = new Vector2(0.1f, data.BeamWidth);
        _beamHitbox = owner.AddHitbox(_beamRoot.gameObject, data, _beamCollider);
        _beamHitbox.Hit += OnBeamHit;

        // 絵は「横のどの列も同じ」レーザーなので、横に伸ばしてもドットは崩れない。縦は絵の高さ＝レーザーの太さにしておく
        _beam = AttackRunner.AddVisual(_beamRoot, data.Sprite, data.Color, new Vector2(0.1f, data.BeamWidth), 19, "BeamVisual");
        _beam.enabled = false;

        // 残りのレーザーの量（体の下の細い棒）
        Sprite pixel = GameArt.Instance != null && GameArt.Instance.Pixel != null ? GameArt.Instance.Pixel : PlaceholderSprites.Square;
        float barY = -(first != null ? first.bounds.extents.y : 0.25f) - 0.12f;
        _gaugeBack = MakeBar(pixel, new Vector3(0f, barY, 0f), new Color(0f, 0f, 0f, 0.7f), 14);
        _gaugeFill = MakeBar(pixel, new Vector3(0f, barY, 0f), GaugeColor * data.Color, 15);
        SetBarSize(_gaugeBack, GaugeWidth + 2f / GameArt.PixelsPerUnit, 3f / GameArt.PixelsPerUnit);
        UpdateGauge();

        Vfx.Sparkle(transform.position);
    }

    const float GaugeWidth = 14f / GameArt.PixelsPerUnit;
    /// <summary>残りのレーザーの棒の色（ロボットの目・レーザーと同じ水色）</summary>
    static readonly Color GaugeColor = new Color32(0x5f, 0xf0, 0xff, 0xff);

    SpriteRenderer MakeBar(Sprite pixel, Vector3 localPos, Color color, int order)
    {
        var sr = new GameObject("Gauge").AddComponent<SpriteRenderer>();
        sr.gameObject.layer = gameObject.layer;
        sr.transform.SetParent(transform, false);
        sr.transform.localPosition = localPos;
        sr.sprite = pixel;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>棒の大きさ（マス）。絵の元の大きさ（1ドットの絵は 1/18 マス）に関係なく、指定した大きさにする</summary>
    static void SetBarSize(SpriteRenderer sr, float width, float height)
    {
        Vector2 native = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(width / Mathf.Max(native.x, 0.0001f), height / Mathf.Max(native.y, 0.0001f), 1f);
    }

    void Update()
    {
        if (_owner == null || _ownerBody == null || (_ownerHealth != null && _ownerHealth.IsDead))
        {
            BeginVanish();
        }

        if (_vanishAt >= 0f)
        {
            UpdateVanish();
            return;
        }

        if (_owner != null) _facing = _owner.Facing;
        Follow();
        Animate();

        // 今の敵が狙えなくなったら外す。新しい敵は、決まった間隔で探す（今の敵がいる間は、そのまま撃ち続ける）
        if (_target != null && !IsValidTarget(_target)) _target = null;
        if (_target == null && Time.time >= _nextRetarget)
        {
            _nextRetarget = Time.time + RetargetInterval;
            _target = FindTarget();
        }

        if (_target != null) Fire();
        else StopBeam();

        UpdateGauge();
        UpdateWarnBlink();

        if (_fired >= _data.SummonFireTime || Time.time - _bornAt >= _data.SummonLifetime) BeginVanish();
    }

    // ───────── 動き ─────────

    void Follow()
    {
        Vector2 goal = HoverPoint(_ownerBody, _facing, _data);
        goal.y += Mathf.Sin((Time.time - _bornAt) * 3.2f) * 0.12f; // ふわふわ上下する
        float k = 1f - Mathf.Exp(-_data.SummonFollowSharpness * Time.deltaTime);
        transform.position = Vector2.Lerp(transform.position, goal, k);
    }

    void Animate()
    {
        if (_frames.Length > 1)
        {
            int i = Mathf.FloorToInt((Time.time - _bornAt) * _data.SummonFps) % _frames.Length;
            _body.sprite = _frames[i];
        }
        // 狙っている敵の方を向く（いなければ持ち主と同じ向き）
        int face = _target != null ? (_target.bounds.center.x >= transform.position.x ? 1 : -1) : _facing;
        _body.flipX = face < 0;
    }

    // ───────── 敵を探す ─────────

    Vector2 OwnerCenter
    {
        get
        {
            foreach (var col in _ownerBody.GetComponents<Collider2D>())
            {
                if (!col.isTrigger && col.enabled) return col.bounds.center;
            }
            return _ownerBody.position;
        }
    }

    /// <summary>レーザーを出す位置（体の、向いている側の目の少し前）</summary>
    Vector2 Muzzle
    {
        get
        {
            int face = _body.flipX ? -1 : 1;
            return (Vector2)transform.position + new Vector2(face * 0.25f, -0.05f);
        }
    }

    /// <summary>範囲の中で、ロボットに一番近い敵の被弾範囲。いなければ null</summary>
    Collider2D FindTarget()
    {
        var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
        filter.SetLayerMask(_owner.TargetLayers);
        int count = Physics2D.OverlapCircle(OwnerCenter, _data.SummonRange, filter, _found);
        Collider2D best = null;
        float bestDist = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider2D c = _found[i];
            if (!IsValidTarget(c)) continue;
            float d = ((Vector2)c.bounds.center - (Vector2)transform.position).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }
        return best;
    }

    bool IsValidTarget(Collider2D c)
    {
        if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) return false;
        if (!c.TryGetComponent(out Hurtbox hurtbox) || hurtbox.Owner == null) return false;
        IDamageable owner = hurtbox.Owner;
        if (owner.Team == _owner.Team) return false;
        if (owner is Health h && (h.IsDead || !h.isActiveAndEnabled)) return false;
        if (owner is VirusGauge g && (g.IsFull || !g.isActiveAndEnabled)) return false; // 正常化したボスは撃たない
        if (Vector2.Distance(c.ClosestPoint(OwnerCenter), OwnerCenter) > _data.SummonRange) return false;
        return HasLineOfSight(c);
    }

    /// <summary>レーザーの通り道に、壁・床が無いか（すり抜け床は通す）</summary>
    bool HasLineOfSight(Collider2D target)
    {
        Vector2 from = transform.position;
        Vector2 to = target.bounds.center;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(_owner.SolidMask);
        int n = Physics2D.Linecast(from, to, filter, _rayHits);
        for (int i = 0; i < n; i++)
        {
            if (_rayHits[i].collider == null) continue;
            if (_rayHits[i].collider.attachedRigidbody != null && _rayHits[i].collider.attachedRigidbody == target.attachedRigidbody) continue;
            return false;
        }
        return true;
    }

    // ───────── レーザー ─────────

    void Fire()
    {
        Vector2 from = Muzzle;
        Vector2 to = _target.bounds.center;
        Vector2 d = to - from;
        float length = Mathf.Max(0.1f, d.magnitude);

        _beamRoot.position = from;
        _beamRoot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        SetBeamShape(length, _data.BeamWidth);
        // ゆらめき：0.05秒ごとに明るさを2段階で切り替える
        Color c = _data.Color;
        if (Mathf.FloorToInt(Time.time / 0.05f) % 2 == 1) c = new Color(c.r * 0.8f, c.g * 0.9f, c.b, c.a * 0.85f);
        _beam.color = c;

        if (!_beam.enabled || _beamTarget != _target)
        {
            // 撃ち始め・狙う敵が変わったとき：出ている判定を消し、ヒット間隔のあいだ当て続けて、はじめて1回当たる
            // （前の敵を倒した直後の判定が、次の敵にそのまま当たらないようにする）
            _beam.enabled = true;
            _beamTarget = _target;
            _beamHitbox.Deactivate();
            _nextTick = Time.time + _data.TickInterval;
        }
        else if (Time.time >= _nextTick)
        {
            // 当て続けてヒット間隔がたつたびに、判定を一瞬だけ出す（「当てた相手」の記録も消えるので、同じ敵にもう一度当たる）
            _beamHitbox.Activate();
            _hitUntil = Time.time + HitPulse;
            _nextTick = Time.time + _data.TickInterval;
        }
        if (_beamHitbox.IsActive && Time.time >= _hitUntil) _beamHitbox.Deactivate();
        _fired += Time.deltaTime;
    }

    void SetBeamShape(float length, float width)
    {
        // 判定は根元から先へ伸ばす（判定の太さは決まった太さのまま）
        _beamCollider.size = new Vector2(length, _data.BeamWidth);
        _beamCollider.offset = new Vector2(length * 0.5f, 0f);
        Transform v = _beam.transform;
        v.localPosition = new Vector3(length * 0.5f, 0f, 0f);
        if (_beam.drawMode != SpriteDrawMode.Simple) _beam.size = new Vector2(length, width);
        else
        {
            Vector2 native = _beam.sprite.bounds.size;
            v.localScale = new Vector3(length / Mathf.Max(native.x, 0.001f), width / Mathf.Max(native.y, 0.001f), 1f);
        }
    }

    void OnBeamHit(IDamageable target, DamageInfo info) => Vfx.HitSpark(info.HitPoint);

    void StopBeam()
    {
        if (!_beam.enabled) return;
        _beam.enabled = false;
        _beamTarget = null;
        _beamHitbox.Deactivate();
    }

    // ───────── 残り・消える ─────────

    void UpdateGauge()
    {
        float r = Mathf.Clamp01(FireRemaining / Mathf.Max(0.01f, _data.SummonFireTime));
        SetBarSize(_gaugeFill, GaugeWidth * r, 1f / GameArt.PixelsPerUnit);
        // 左端をそろえる
        Vector3 p = _gaugeFill.transform.localPosition;
        p.x = -GaugeWidth * 0.5f * (1f - r);
        _gaugeFill.transform.localPosition = p;
    }

    void UpdateWarnBlink()
    {
        float remain = Mathf.Min(FireRemaining, LifeRemaining);
        bool on = remain > WarnTime || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
        _body.enabled = on;
    }

    void BeginVanish()
    {
        if (_vanishAt >= 0f) return;
        _vanishAt = Time.time;
        _target = null;
        if (_beam != null) StopBeam();
        Vfx.Sparkle(transform.position);
    }

    void UpdateVanish()
    {
        float t = (Time.time - _vanishAt) / VanishTime;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        // 点滅しながら上へ少し浮いて消える
        _body.enabled = Mathf.FloorToInt(Time.time / 0.05f) % 2 == 0;
        transform.position += Vector3.up * (Time.deltaTime * 1.2f);
        Color c = _body.color;
        c.a = 1f - t;
        _body.color = c;
        _gaugeBack.enabled = _gaugeFill.enabled = false;
    }

    void OnDisable()
    {
        if (_beamHitbox != null) _beamHitbox.Deactivate();
    }
}
