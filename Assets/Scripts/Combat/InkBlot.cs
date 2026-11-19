using UnityEngine;

/// <summary>
/// 筆から飛び出したインク（2026-11-19。Photoshop の能力）。BrushSwing が、インクの攻撃データから作る。
/// ・飛び出す向きへ少し飛んで、その場にとどまる（置き武器。地形は気にしない）
/// ・出るときは、小さい状態から、いつもより少し大きくなってから、いつもの大きさに戻る
/// ・とどまっている間、触れた敵に当たる。「相手に当たったら消えるか」がオンなら、当たったらはじけて消える（2026-11-19）。
///   オフなら当たり続ける（ヒット間隔ごとに当たり直す）。持続時間が過ぎたら、薄くなって消える
/// 大きさは攻撃データの「形の大きさ」の x（直径）、残る秒数は「持続時間」、当たり直す間隔は「ヒット間隔」、絵は「見た目」の絵
/// ・予告時間があるインク（Photoshop のボス。2026-11-19）：小さなインクの点のまま空中に漂い、攻撃の範囲（荒れたしぶきの形）が
///   薄い赤で数回点滅してから、点が一気にしぶきに広がり、真っ赤になって攻撃する（判定時間のあいだ、1回だけ当たる）。そのあと消える
/// </summary>
public class InkBlot : MonoBehaviour
{
    const float FlyTime = 0.12f;
    const float GrowTime = 0.12f;
    const float SettleTime = 0.1f;
    const float Overshoot = 1.25f;
    const float FadeTime = 0.3f;
    static readonly Color DangerColor = new Color(1f, 0.15f, 0.15f, 1f);

    AttackData _data;
    SpriteRenderer _sr;
    Hitbox _hitbox;
    Vector2 _from, _to;
    float _start;
    float _nextTick;
    float _baseScale = 1f;
    SpriteRenderer _danger;   // 予告のあるインクの、攻撃の範囲
    float _dangerScale = 1f;
    bool _fired;
    bool Delayed => _data.TelegraphTime > 0f;

    public static InkBlot Spawn(AttackRunner owner, AttackData data, Vector2 position, Vector2 fling)
    {
        GameObject go = owner.CreateSpawnedObject($"Ink_{data.name}", position);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;

        var ink = go.AddComponent<InkBlot>();
        ink._data = data;
        ink._from = position;
        ink._to = position + fling;
        ink._start = Time.time;

        float size = Mathf.Max(0.2f, data.AreaSize.x);
        var art = new GameObject("Art");
        art.layer = go.layer;
        art.transform.SetParent(go.transform, false);
        ink._sr = art.AddComponent<SpriteRenderer>();
        ink._sr.sprite = data.Sprite != null ? data.Sprite : PlaceholderSprites.Circle;
        ink._sr.color = data.Color;
        ink._sr.sortingOrder = 16;
        // 絵の元の大きさに関係なく、直径を「形の大きさ」にそろえる。向きと左右はばらばらに（同じ形が並ばないように）
        Vector2 native = ink._sr.sprite.bounds.size;
        ink._baseScale = size / Mathf.Max(0.01f, Mathf.Max(native.x, native.y));
        art.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * Random.Range(0, 4));
        ink._sr.flipX = Random.value < 0.5f;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = size * 0.42f;
        ink._hitbox = owner.AddHitbox(go, data, col);
        ink._hitbox.Hit += ink.OnHit;
        if (ink.Delayed)
        {
            // 攻撃の範囲（インクより少し大きい、荒れたしぶきの形）。予告の間は判定を出さない
            float range = size * 1.25f;
            col.radius = range * 0.45f;
            var dg = new GameObject("Danger");
            dg.layer = go.layer;
            dg.transform.SetParent(go.transform, false);
            dg.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            ink._danger = dg.AddComponent<SpriteRenderer>();
            Sprite ds = GameArt.Instance != null && GameArt.Instance.InkDanger != null ? GameArt.Instance.InkDanger : PlaceholderSprites.Circle;
            ink._danger.sprite = ds;
            ink._danger.sortingOrder = 15;
            Vector2 dn = ds.bounds.size;
            ink._dangerScale = range / Mathf.Max(0.01f, Mathf.Max(dn.x, dn.y));
            dg.transform.localScale = Vector3.one * ink._dangerScale;
            ink._danger.color = new Color(1f, 0.15f, 0.15f, 0f);
        }
        else
        {
            ink._hitbox.Activate();
            GameAudio.Event("gm_ps_ink");
        }
        ink._nextTick = Time.time + data.TickInterval;
        art.transform.localScale = Vector3.one * 0.3f * ink._baseScale; // 大きさのアニメーションは絵だけ（判定の大きさは変えない）
        return ink;
    }

    void Update()
    {
        if (Delayed)
        {
            UpdateDelayed();
            return;
        }
        float age = Time.time - _start;
        float life = Mathf.Max(0.1f, _data.PersistDuration > 0f ? _data.PersistDuration : _data.ActiveTime);
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }

        // 飛ぶ → とどまる
        float fly = Mathf.Clamp01(age / FlyTime);
        transform.position = Vector2.Lerp(_from, _to, 1f - (1f - fly) * (1f - fly));

        // 小さい → 少し大きく → いつもの大きさ
        float s;
        if (age < GrowTime) s = Mathf.Lerp(0.3f, Overshoot, age / GrowTime);
        else if (age < GrowTime + SettleTime) s = Mathf.Lerp(Overshoot, 1f, (age - GrowTime) / SettleTime);
        else s = 1f;
        _sr.transform.localScale = Vector3.one * s * _baseScale;

        // 残っている間、ヒット間隔ごとに当たり直す
        if (Time.time >= _nextTick)
        {
            _hitbox.Activate();
            _nextTick = Time.time + _data.TickInterval;
        }

        // 最後は薄くなって消える
        Color c = _data.Color;
        c.a *= Mathf.Clamp01((life - age) / FadeTime);
        _sr.color = c;
    }

    /// <summary>予告のあるインクの、予告の間の大きさ（いつもの大きさに対する割合）。小さな点のように見せる</summary>
    const float DotScale = 0.35f;

    /// <summary>
    /// 予告のあるインク：小さなインクの点のまま漂う → 攻撃の範囲のシルエットが薄い赤で数回点滅 →
    /// 攻撃の瞬間に、点が一気にしぶきに広がり、範囲が真っ赤になって当たる → 消える（攻撃のタイミングが分かるように。2026-11-19）
    /// </summary>
    void UpdateDelayed()
    {
        float age = Time.time - _start;
        float tele = _data.TelegraphTime;
        // 飛んで、空中に漂う（ゆっくり上下にゆれる）
        float fly = Mathf.Clamp01(age / FlyTime);
        Vector2 p = Vector2.Lerp(_from, _to, 1f - (1f - fly) * (1f - fly));
        p.y += Mathf.Sin(age * 3f + _from.x) * 0.08f;
        transform.position = p;

        if (age < tele)
        {
            // 小さなインクの点（飛ぶ間に少しずつ大きくなり、少し脈打つ）
            float dot = DotScale * Mathf.Lerp(0.5f, 1f, fly) * (1f + 0.1f * Mathf.Sin(age * 10f));
            _sr.transform.localScale = Vector3.one * dot * _baseScale;
            // 攻撃の範囲のシルエットが、薄い赤で点滅（0.4秒ごと。最後の0.4秒は速く）。飛び終わってから出す
            float period = tele - age < 0.4f ? 0.1f : 0.4f;
            bool on = fly >= 1f && Mathf.FloorToInt(age / (period * 0.5f)) % 2 == 0;
            Color c = DangerColor;
            c.a = fly < 1f ? 0f : on ? 0.42f : 0.12f;
            if (_danger != null) _danger.color = c;
            return;
        }

        // 攻撃：点が一気にしぶきに広がり、範囲が真っ赤になって、1回だけ当たる
        if (!_fired)
        {
            _fired = true;
            _hitbox.Activate();
            GameAudio.Event("gm_ps_ink_boss");
        }
        float k = Mathf.Clamp01((age - tele) / Mathf.Max(0.05f, _data.ActiveTime));
        float burst = Mathf.Clamp01(k / 0.35f); // 判定時間の最初の3割ほどで広がりきる
        if (_danger != null)
        {
            Color c = DangerColor;
            c.a = Mathf.Lerp(0.75f, 0f, k);
            _danger.color = c;
            _danger.transform.localScale = Vector3.one * _dangerScale * (1f + 0.12f * k);
        }
        _sr.transform.localScale = Vector3.one * _baseScale * Mathf.Lerp(DotScale, 1.3f, 1f - (1f - burst) * (1f - burst));
        Color ic = _data.Color;
        ic.a *= k < 0.6f ? 1f : Mathf.Clamp01(1f - (k - 0.6f) / 0.4f);
        _sr.color = ic;
        if (k >= 1f)
        {
            _hitbox.Deactivate();
            Destroy(gameObject);
        }
    }

    void OnHit(IDamageable target, DamageInfo info)
    {
        if (Delayed || !_data.DestroyOnHit) return;
        // はじけて消える（しぶきが飛ぶ）
        Vfx.HitSpark(info.HitPoint);
        if (_sr != null && _sr.sprite != null)
        {
            var go = new GameObject("InkPop");
            go.transform.SetPositionAndRotation(_sr.transform.position, _sr.transform.rotation);
            go.transform.localScale = _sr.transform.lossyScale * 1.3f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sr.sprite;
            sr.flipX = _sr.flipX;
            sr.sortingOrder = _sr.sortingOrder;
            Color c = _sr.color;
            c.a *= 0.6f;
            sr.color = c;
            go.AddComponent<FadeAndDestroy>().Setup(0.15f);
        }
        _hitbox.Deactivate();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_hitbox != null) _hitbox.Hit -= OnHit;
    }

    void OnDisable()
    {
        if (_hitbox != null) _hitbox.Deactivate();
    }
}
