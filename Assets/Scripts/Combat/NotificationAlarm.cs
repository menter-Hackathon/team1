using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通知のアラーム（2026-11-19。チャットのボスの大技。数値は攻撃データ、見た目は【仮】）。AttackRunner が AttackData（種類 Alarm）から作る。
/// ・部屋のあちこち（いろいろな高さの床・足場の上）に、通知のベルを置く（数は「出す数」）。触れると回収できる
/// ・どのベルにも、右上の赤い丸に同じ数（残りのベルの数。Mac の通知のバッジのように）。回収するたびに、全部のベルの数が減る
/// ・部屋全体に、薄い赤の危険エリアが点滅する（鳴るたびに、点滅が速く・濃くなる）
/// ・ベルは決まった間隔で鳴る（残りのベルが全部揺れる）。鳴るたびに、それぞれのベルから赤いもやが出る（何をすればよいか分かるように）
/// ・決まった回数鳴ったときにベルが残っていると、部屋全体が真っ赤になって攻撃する。それまでに全部回収すれば、何も起きずに終わる
/// ・終わるまで（攻撃が終わるか、全部回収するまで）、AttackRunner の攻撃は終わらない（ボスは動かず、ほかの技も出さない）
/// </summary>
public class NotificationAlarm : MonoBehaviour
{
    static readonly Color DangerColor = new Color(1f, 0.12f, 0.12f, 1f);
    static readonly Color MistColor = new Color(1f, 0.15f, 0.2f, 1f);
    const float BellShakeTime = 0.6f;
    const float ImpactTime = 0.35f;
    const float PickupRadius = 0.6f;

    /// <summary>回収するベル1つ（ベルの絵と、右上の数のバッジ）</summary>
    class Bell
    {
        public SpriteRenderer Body;
        public SpriteRenderer Badge;
        public float Phase;
    }

    AttackRunner _owner;
    AttackData _data;
    readonly List<Bell> _bells = new List<Bell>();
    SpriteRenderer _overlay;
    Hitbox _hitbox;
    Rect _arena;
    int _rings;
    float _shakeUntil;
    readonly Collider2D[] _found = new Collider2D[8];

    /// <summary>終わったら true（攻撃が終わった・全部回収した）</summary>
    public bool IsDone { get; private set; }
    /// <summary>残りのベルの数</summary>
    public int Remaining => _bells.Count;
    /// <summary>鳴った回数</summary>
    public int Rings => _rings;

    public static NotificationAlarm Spawn(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        GameObject go = owner.CreateSpawnedObject($"Alarm_{data.name}", arena.center);
        var a = go.AddComponent<NotificationAlarm>();
        a._owner = owner;
        a._data = data;
        a._arena = arena;
        a.Build();
        a.StartCoroutine(a.Run());
        return a;
    }

    static Sprite Pixel => GameArt.Instance != null && GameArt.Instance.Pixel != null ? GameArt.Instance.Pixel : PlaceholderSprites.Square;

    void Build()
    {
        // 部屋全体の危険エリア（判定も同じ大きさ。最後に鳴ったときだけ出す）
        _overlay = AttackRunner.AddVisual(transform, Pixel, Transparent(DangerColor), _arena.size, 40, "Danger");
        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = _arena.size;
        _hitbox = _owner.AddHitbox(gameObject, _data, col);

        // 回収するベル（床・足場の上のあちこち）。どのベルにも、右上に残りの数
        Sprite[] frames = _data.AlarmBellFrames;
        Sprite bellSprite = frames.Length > 0 ? frames[0] : _data.Sprite;
        int i = 0;
        foreach (Vector2 p in PickPositions(_data.SpawnCount))
        {
            var bg = new GameObject("NotificationBell");
            bg.layer = gameObject.layer;
            bg.transform.SetParent(transform, false);
            bg.transform.position = p;
            var body = bg.AddComponent<SpriteRenderer>();
            body.sprite = bellSprite != null ? bellSprite : PlaceholderSprites.Circle;
            if (bellSprite == null) { body.color = new Color(1f, 0.82f, 0.25f); bg.transform.localScale = Vector3.one * 0.8f; }
            body.sortingOrder = 30;

            var badgeGo = new GameObject("Badge");
            badgeGo.layer = gameObject.layer;
            badgeGo.transform.SetParent(bg.transform, false);
            Vector2 ext = body.sprite.bounds.extents;
            badgeGo.transform.localPosition = new Vector3(ext.x * 0.7f, ext.y * 0.7f, 0f);
            var badge = badgeGo.AddComponent<SpriteRenderer>();
            badge.sortingOrder = 31;

            _bells.Add(new Bell { Body = body, Badge = badge, Phase = i++ * 1.7f });
            Vfx.Sparkle(p);
        }
        UpdateBadges();
    }

    static Color Transparent(Color c) { c.a = 0f; return c; }

    /// <summary>ベルを置く位置：部屋のあちこちの床・足場の上（いろいろな高さ）。プレイヤーから少し離し、ベルどうしも離す</summary>
    List<Vector2> PickPositions(int count)
    {
        var result = new List<Vector2>();
        Vector2 player = _owner.TargetPoint ?? _arena.center;
        for (int i = 0; i < count; i++)
        {
            Vector2 best = new Vector2(Random.Range(_arena.xMin + 1f, _arena.xMax - 1f), _arena.yMin + 0.8f);
            float bestScore = float.NegativeInfinity;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float x = Random.Range(_arena.xMin + 1f, _arena.xMax - 1f);
                float from = Random.Range(_arena.yMin + 1.2f, _arena.yMax - 1.5f); // ここから真下の床・足場の上に置く（高さがばらける）
                _owner.TryFindGround(x, from, out float groundY);
                var p = new Vector2(x, groundY + 0.8f);
                float score = Mathf.Min(Vector2.Distance(p, player), 6f);
                foreach (var q in result) score = Mathf.Min(score, Vector2.Distance(p, q));
                if (score > bestScore) { bestScore = score; best = p; }
                if (score >= 3.5f) break;
            }
            result.Add(best);
        }
        return result;
    }

    IEnumerator Run()
    {
        float nextRing = Time.time + _data.AlarmFirstRing;
        while (_rings < _data.AlarmRings)
        {
            CollectTouched();
            if (_bells.Count == 0)
            {
                yield return Clear();
                yield break;
            }
            if (Time.time >= nextRing)
            {
                _rings++;
                Ring();
                nextRing += _data.AlarmRingInterval;
                if (_rings >= _data.AlarmRings) break;
            }
            AnimateWaiting();
            yield return null;
        }
        yield return Impact();
    }

    /// <summary>鳴る：残りのベルが全部揺れて、それぞれから赤いもやが出る</summary>
    void Ring()
    {
        _shakeUntil = Time.time + BellShakeTime;
        GameAudio.Event("gm_chat_bell_ring", 1f + 0.1f * Mathf.Max(0, _rings - 1));
        Sprite[] mist = RedMist();
        foreach (var b in _bells)
        {
            if (b.Body != null) Vfx.Play(mist, b.Body.transform.position, 9f, 32, new Color(1f, 1f, 1f, 0.85f));
        }
    }

    static Sprite[] _mistFrames;

    /// <summary>ウイルスの煙（小）を赤くしたもや</summary>
    static Sprite[] RedMist()
    {
        if (_mistFrames != null && _mistFrames.Length > 0 && _mistFrames[0] != null) return _mistFrames;
        Sprite[] src = GameArt.Instance != null ? GameArt.Instance.SmokeSmall : null;
        if (src == null) return null;
        _mistFrames = new Sprite[src.Length];
        for (int i = 0; i < src.Length; i++) _mistFrames[i] = ThemeRecolor.Colorize(src[i], MistColor);
        return _mistFrames;
    }

    /// <summary>待っている間：危険エリアの点滅（鳴るたびに速く・濃く）、ベルの揺れ、ふわふわ</summary>
    void AnimateWaiting()
    {
        float speed = 2f + _rings * 2.5f;
        float a = 0.08f + 0.06f * _rings + 0.07f * (0.5f + 0.5f * Mathf.Sin(Time.time * speed * Mathf.PI));
        Color c = DangerColor;
        c.a = a;
        _overlay.color = c;

        Sprite[] frames = _data.AlarmBellFrames;
        bool shaking = Time.time < _shakeUntil;
        foreach (var b in _bells)
        {
            if (b.Body == null) continue;
            if (frames.Length > 1)
                b.Body.sprite = shaking ? frames[1 + Mathf.FloorToInt(Time.time * 16f + b.Phase) % (frames.Length - 1)] : frames[0];
            b.Body.transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.time * 4f + b.Phase));
        }
    }

    /// <summary>プレイヤーが触れたベルを回収する</summary>
    void CollectTouched()
    {
        var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
        filter.SetLayerMask(_owner.TargetLayers);
        bool changed = false;
        for (int i = _bells.Count - 1; i >= 0; i--)
        {
            Bell b = _bells[i];
            if (b.Body == null) { _bells.RemoveAt(i); changed = true; continue; }
            int n = Physics2D.OverlapCircle(b.Body.transform.position, PickupRadius, filter, _found);
            bool touched = false;
            for (int k = 0; k < n && !touched; k++)
            {
                if (_found[k].TryGetComponent(out Hurtbox h) && h.Owner != null && h.Owner.Team != _owner.Team) touched = true;
            }
            if (!touched) continue;
            Vfx.Sparkle(b.Body.transform.position);
            GameAudio.Event("gm_chat_bell_get");
            Destroy(b.Body.gameObject);
            _bells.RemoveAt(i);
            changed = true;
        }
        if (changed) UpdateBadges();
    }

    /// <summary>どのベルにも、残りのベルの数を出す</summary>
    void UpdateBadges()
    {
        Sprite[] digits = _data.AlarmBadgeFrames;
        int n = _bells.Count;
        foreach (var b in _bells)
        {
            if (b.Badge == null) continue;
            b.Badge.enabled = n > 0 && digits.Length > 0;
            if (b.Badge.enabled) b.Badge.sprite = digits[Mathf.Clamp(n - 1, 0, digits.Length - 1)];
        }
    }

    /// <summary>全部回収した：危険エリアが消えて終わる</summary>
    IEnumerator Clear()
    {
        GameAudio.Event("gm_chat_alarm_clear");
        float start = Time.time;
        Color from = _overlay.color;
        while (Time.time - start < 0.4f)
        {
            float t = (Time.time - start) / 0.4f;
            Color c = from;
            c.a = from.a * (1f - t);
            _overlay.color = c;
            yield return null;
        }
        Finish();
    }

    /// <summary>ベルが残っていた：部屋全体が真っ赤になって攻撃する</summary>
    IEnumerator Impact()
    {
        _shakeUntil = Time.time + ImpactTime;
        _hitbox.Activate();
        GameAudio.Event("gm_chat_alarm_blast");
        float start = Time.time;
        while (Time.time - start < ImpactTime)
        {
            float t = (Time.time - start) / ImpactTime;
            Color c = DangerColor;
            c.a = Mathf.Lerp(0.75f, 0.35f, t);
            _overlay.color = c;
            yield return null;
        }
        _hitbox.Deactivate();
        foreach (var b in _bells)
        {
            if (b.Body != null) Destroy(b.Body.gameObject);
        }
        _bells.Clear();
        Finish();
    }

    void Finish()
    {
        IsDone = true;
        Destroy(gameObject);
    }
}
