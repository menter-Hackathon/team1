using UnityEngine;

/// <summary>輪の見た目（判定の瞬間）</summary>
public enum TalkBurst
{
    /// <summary>音の輪（Slack のハドル）。色の輪が広がる。判定の瞬間だけ白</summary>
    Sound = 0,
    /// <summary>花火（メッセージ）。色の点がまん中から外へ広がる</summary>
    Firework = 1,
    /// <summary>爆発（LINE の怒りのスタンプ）。白い光と、オレンジの輪と、怒った顔</summary>
    Explosion = 2,
}

/// <summary>
/// 【仮】円の判定（2026-11-19。担当1 のキットで使い回す）。予告の赤い輪（ふちが赤く点滅・中は赤 2割）→ 円の判定 → 消える。
/// ・予告の秒数が 0 なら予告なし（プレイヤーの能力）。プレイヤーの物には赤を使わない
/// ・攻撃する人の出した物（CreateSpawnedObject）で、CustomEffect なので、形態変化（50％）・正常化（100％）で消える
/// ・ダメージ・ゲージ・ノックバックは、渡した攻撃データの共通の欄（Hitbox）
/// </summary>
public class TalkRing : CustomEffect
{
    const int TelegraphOrder = 17, BurstOrder = 22;

    AttackRunner _owner;
    AttackData _data;
    float _diameter;
    float _telegraphAt, _hitAt, _endAt;
    TalkBurst _burst;
    Color _color;
    Transform _follow;
    Vector2 _followOffset;
    Hitbox _hitbox;
    SpriteRenderer _tgFill, _tgRing, _burstRing, _flash;
    Transform _burstRoot;
    bool _hitOn, _shown;

    /// <summary>今、判定が出ているか（テスト用）</summary>
    public bool IsHitActive => _hitOn;
    /// <summary>当たったとき（判定、当てた相手）。能力の飾りに使う</summary>
    public event System.Action<TalkRing, IDamageable> Hit;

    /// <summary>
    /// 円を1つ出す。center：真ん中、diameter：直径（マス）、delay：予告を始めるまで、telegraph：予告の秒数（0 で予告なし）、
    /// active：判定の秒数。follow を渡すと、その物について動く（予告の間だけ）
    /// </summary>
    public static TalkRing Spawn(AttackRunner owner, AttackData data, Vector2 center, float diameter, float delay, float telegraph, float active,
        TalkBurst burst, Color color, Transform follow = null)
    {
        if (owner == null || data == null) return null;
        GameObject go = owner.CreateSpawnedObject($"TalkRing_{data.name}", center);
        var r = go.AddComponent<TalkRing>();
        r._owner = owner;
        r._data = data;
        r._diameter = Mathf.Max(0.2f, diameter);
        float now = Time.time;
        r._telegraphAt = now + Mathf.Max(0f, delay);
        r._hitAt = r._telegraphAt + Mathf.Max(0f, telegraph);
        r._endAt = r._hitAt + Mathf.Max(0.05f, active);
        r._burst = burst;
        r._color = color;
        r._follow = follow;
        if (follow != null) r._followOffset = center - (Vector2)follow.position;
        r.Build(telegraph > 0f);
        return r;
    }

    void Build(bool telegraph)
    {
        var col = TalkKitUtil.AddTrigger<CircleCollider2D>(gameObject);
        col.radius = _diameter * 0.5f;
        _hitbox = _owner.AddHitbox(gameObject, _data, col);
        _hitbox.Hit += OnHit;

        int dots = Mathf.Clamp(Mathf.RoundToInt(_diameter * RuntimeSprites.PixelsPerUnit), 8, 240);
        Vector2 size = Vector2.one * _diameter;
        if (telegraph)
        {
            _tgFill = TalkArt.Show(transform, TalkArt.Disc(dots), Vector2.zero, size, TalkArt.Fade(TalkArt.Red, 0.2f), TelegraphOrder, "TelegraphFill");
            _tgRing = TalkArt.Show(transform, TalkArt.Ring(dots, 2), Vector2.zero, size, TalkArt.Red, TelegraphOrder + 1, "TelegraphRing");
            _tgFill.enabled = _tgRing.enabled = false;
        }
        _burstRoot = TalkArt.Child(transform, "Burst", transform.position);
        _burstRing = TalkArt.Show(_burstRoot, TalkArt.Ring(dots, Mathf.Max(3, dots / 14)), Vector2.zero, size, _color, BurstOrder, "Ring");
        _flash = TalkArt.Show(_burstRoot, TalkArt.Disc(dots), Vector2.zero, size, new Color(1f, 1f, 1f, 0.8f), BurstOrder + 1, "Flash");
        _burstRoot.gameObject.SetActive(false);
    }

    void OnHit(IDamageable target, DamageInfo info)
    {
        try { Hit?.Invoke(this, target); }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    void Update()
    {
        float now = Time.time;
        if (now < _hitAt && _follow != null) transform.position = (Vector2)_follow.position + _followOffset;

        if (now < _telegraphAt) return;
        if (now < _hitAt)
        {
            if (_tgRing != null)
            {
                bool on = _hitAt - now > 0.3f || Mathf.FloorToInt(now / 0.08f) % 2 == 0;
                _tgRing.enabled = on;
                _tgFill.enabled = true;
            }
            return;
        }
        if (!_shown)
        {
            _shown = true;
            if (_tgRing != null) { _tgRing.enabled = false; _tgFill.enabled = false; }
            _burstRoot.gameObject.SetActive(true);
            _hitbox.Activate();
            _hitOn = true;
            if (_owner != null) _owner.NotifyReleased(_data);
            SpawnBurstFx();
        }
        float t = Mathf.Clamp01((now - _hitAt) / Mathf.Max(0.05f, _endAt - _hitAt));
        // 輪：0.7 → 1.0 に広がって、うすくなる。光は最初の 0.1秒だけ
        float s = Mathf.Lerp(0.7f, 1.05f, 1f - (1f - t) * (1f - t));
        _burstRoot.localScale = new Vector3(s, s, 1f);
        _burstRing.color = TalkArt.Fade(now - _hitAt < 0.06f ? Color.white : _color, 1f - t * 0.7f);
        _flash.color = new Color(1f, 1f, 1f, Mathf.Max(0f, 0.7f - (now - _hitAt) / 0.12f));
        if (now >= _endAt)
        {
            _hitOn = false;
            _hitbox.Deactivate();
            Destroy(gameObject);
        }
    }

    // 判定の瞬間の飾り（当たらない。消えても戻す物はない）
    void SpawnBurstFx()
    {
        Vector2 c = transform.position;
        float r = _diameter * 0.5f;
        switch (_burst)
        {
            case TalkBurst.Firework:
            {
                Color[] cols = { _color, Color.white, TalkArt.Yellow, TalkArt.Heart };
                int n = 14;
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    FxAnim.Sprite(TalkArt.Disc(3), c + dir * r * 0.2f, BurstOrder + 2, 0.5f, cols[i % cols.Length], 1f, "Spark")
                        .Velocity(dir * r * 2.2f).Gravity(3f).FadeFrom(0.4f);
                }
                break;
            }
            case TalkBurst.Explosion:
            {
                FxAnim.Sprite(TalkArt.Angry, c, BurstOrder + 3, 0.45f, null, 2.4f, "Angry").Pop().FadeFrom(0.6f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 2f / 8 + 0.3f;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    FxAnim.Sprite(TalkArt.Disc(4), c, BurstOrder + 2, 0.35f, i % 2 == 0 ? (Color)TalkArt.Heart : Color.white, 1f, "Bit")
                        .Velocity(dir * r * 3f).FadeFrom(0.3f);
                }
                StageCamera.Shake(0.12f, 0.15f);
                break;
            }
            default:
            {
                // 音の輪：うすい輪がもう1つ、外へ
                FxAnim.Sprite(TalkArt.Ring(48, 2), c, BurstOrder - 1, 0.4f, TalkArt.Fade(_color, 0.6f), _diameter / (48f / 18f) * 0.8f, "Echo").Grow(1.4f).FadeFrom(0.2f);
                break;
            }
        }
    }

    protected override void Restore()
    {
        if (_hitbox != null) _hitbox.Hit -= OnHit;
    }
}
