using UnityEngine;

/// <summary>
/// 敵のミニロボット（2026-11-19。AI チャットのボスが出す。数値は攻撃データ、見た目は【仮】）。AttackRunner が AttackData（種類 Summon、Chaser）から作る。
/// ・出てから少しの間は点滅していて、触れても当たらない（出た瞬間に当たらないように）
/// ・点滅が終わると、狙う相手（プレイヤー）へまっすぐ飛び出し、そのあとは少しずつ曲がって追いかける（地形は通り抜ける）。触れるとダメージ
/// ・攻撃を1回当てると壊れる（近接・能力・AI アシスタントのレーザー）。いられる秒数が過ぎても壊れる
/// ゲージは増えない（ボスの体ではないので）。正常化のときはボスの ClearSpawned で消える
/// </summary>
public class ChaserBot : MonoBehaviour, IDamageable
{
    /// <summary>出てから、触れると当たるようになるまでの秒数</summary>
    const float ArmTime = 0.6f;

    AttackRunner _owner;
    AttackData _data;
    Hitbox _contact;
    SpriteRenderer _body;
    Sprite[] _frames;
    Vector2 _velocity;
    float _bornAt;
    bool _armed;
    bool _dead;

    public Team Team => _owner != null ? _owner.Team : Team.Enemy;
    public bool IsArmed => _armed;

    public static ChaserBot Spawn(AttackRunner owner, AttackData data, Vector2 position)
    {
        GameObject go = owner.CreateSpawnedObject($"Chaser_{data.name}", position);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        // 被弾の窓口（IDamageable）を先に付ける。Hurtbox は付けたときに親から窓口を探すため
        var bot = go.AddComponent<ChaserBot>();
        bot._owner = owner;
        bot._data = data;
        bot._bornAt = Time.time;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.32f;
        go.AddComponent<Hurtbox>();
        // 触れるとダメージ（当たり続ける。連続ヒットはプレイヤーの無敵時間で防ぐ）
        bot._contact = owner.AddHitbox(go, data, col, hitOncePerActivation: false);

        bot._frames = data.SummonFrames;
        bot._body = new GameObject("Body").AddComponent<SpriteRenderer>();
        bot._body.gameObject.layer = go.layer;
        bot._body.transform.SetParent(go.transform, false);
        bot._body.sortingOrder = 16;
        if (bot._frames.Length > 0 && bot._frames[0] != null) bot._body.sprite = bot._frames[0];
        else
        {
            bot._body.sprite = PlaceholderSprites.Circle;
            bot._body.transform.localScale = Vector3.one * 0.6f;
            bot._body.color = data.Color;
        }

        // 最初は少し上へ飛び出す
        bot._velocity = new Vector2(Random.Range(-0.6f, 0.6f), 1f).normalized * data.SummonSpeed;
        Vfx.SmokeSmall(position);
        return bot;
    }

    void Update()
    {
        if (_dead) return;
        float age = Time.time - _bornAt;
        if (age >= _data.SummonLifetime)
        {
            Pop();
            return;
        }

        // 出てすぐは点滅（当たらない）。そのあと判定を出す
        if (!_armed && age >= ArmTime)
        {
            _armed = true;
            _contact.Activate();
            GameAudio.Event("gm_aichat_bot_launch");
            // プレイヤーへまっすぐ飛び出す（そのあとは、ゆるく曲がるだけ）
            if (_owner != null && _owner.TargetPoint is Vector2 aim)
            {
                Vector2 d = aim - (Vector2)transform.position;
                if (d.sqrMagnitude > 0.0001f) _velocity = d.normalized * _data.SummonSpeed;
            }
        }
        bool blinkEnd = _data.SummonLifetime - age < 1f; // 消える前の1秒も点滅して知らせる
        _body.enabled = (_armed && !blinkEnd) || Mathf.FloorToInt(Time.time / 0.07f) % 2 == 0;

        // ゆっくり曲がりながら追いかける
        Vector2? target = _owner != null ? _owner.TargetPoint : null;
        if (target is Vector2 t && _armed)
        {
            Vector2 want = t - (Vector2)transform.position;
            if (want.sqrMagnitude > 0.0001f)
            {
                float maxTurn = _data.SummonTurnRate * Time.deltaTime;
                float angle = Vector2.SignedAngle(_velocity, want);
                _velocity = Quaternion.Euler(0f, 0f, Mathf.Clamp(angle, -maxTurn, maxTurn)) * _velocity;
            }
        }
        else
        {
            _velocity = Vector2.Lerp(_velocity, Vector2.zero, Time.deltaTime * 2f); // 出てすぐは、ふわっと止まる
        }
        if (_armed) _velocity = _velocity.normalized * _data.SummonSpeed;
        transform.position += (Vector3)(_velocity * Time.deltaTime);

        // 部屋の外へは出ない
        Rect arena = BattleArena.GetBounds();
        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x, arena.xMin + 0.3f, arena.xMax - 0.3f);
        p.y = Mathf.Clamp(p.y, arena.yMin + 0.3f, arena.yMax - 0.3f);
        transform.position = p;

        if (_frames.Length > 1) _body.sprite = _frames[Mathf.FloorToInt(age * 10f) % _frames.Length];
        if (Mathf.Abs(_velocity.x) > 0.05f) _body.flipX = _velocity.x < 0f;
    }

    public bool TryTakeDamage(in DamageInfo info)
    {
        if (_dead || info.SourceTeam == Team || info.Amount <= 0) return false;
        Pop();
        return true;
    }

    void Pop()
    {
        if (_dead) return;
        _dead = true;
        _contact.Deactivate();
        Vfx.SmokeSmall(transform.position);
        Destroy(gameObject);
    }
}
