using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Firefox のボスの技3（第2形態）：ピクチャー・イン・ピクチャー（2026-11-19。docs/app-kits-2-design.md 4-6）。
/// ボスから小さい動画の窓（2×1.5マス。赤いふち）がはがれる（0.8秒）→ 毎秒 3.5マスでプレイヤーを 4秒追う
/// → 止まって点滅（0.8秒。半径 1.5マスの赤い輪）→ はじける（半径 1.5マス。0.15秒）。
/// ・避け方：歩いて離れる。近接1回でこわせる（KitProp。こわすと、はじけない）
/// ・窓に触れても当たらない（当たるのは、はじけたときだけ）
/// </summary>
[CreateAssetMenu(fileName = "Firefox_Pip", menuName = "Team1/Kits/Firefox Pip")]
public class FirefoxPip : CustomAttack
{
    [SerializeField, Min(0.1f)] float _peel = 0.8f;
    [SerializeField, Min(0f)] float _chaseSpeed = 3.5f;
    [SerializeField, Min(0f)] float _chaseTime = 4f;
    [Tooltip("止まってからはじけるまで（短くしない）")]
    [SerializeField, Min(0.8f)] float _blink = 0.8f;
    [SerializeField, Min(0.3f)] float _radius = 1.5f;
    [SerializeField, Min(0.05f)] float _hitWindow = 0.15f;
    [SerializeField] Vector2 _size = new Vector2(2f, 1.5f);

    public float Peel => _peel;
    public float ChaseSpeed => _chaseSpeed;
    public float ChaseTime => _chaseTime;
    public float Blink => _blink;
    public float Radius => _radius;
    public float HitWindow => _hitWindow;
    public Vector2 Size => _size;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        FirefoxPipEffect e = FirefoxPipEffect.Spawn(owner, data, this);
        owner.NotifyReleased(data);
        // 窓がはがれるまで待つ（追うのは自分で進む。ボスは次の技へ）
        yield return new WaitForSeconds(_peel + 0.2f);
    }
}

/// <summary>ピクチャー・イン・ピクチャーの窓（自分で進む）。形態変化・正常化で消える</summary>
public class FirefoxPipEffect : CustomEffect
{
    AttackRunner _owner;
    AttackData _data;
    FirefoxPip _a;
    KitProp _prop;
    bool _broken;

    public override bool Lingers => true; // 出ている間は、ほかの場に残る技を出さない

    public static FirefoxPipEffect Spawn(AttackRunner owner, AttackData data, FirefoxPip attack)
    {
        GameObject go = owner.CreateSpawnedObject("Firefox_Pip", owner.OriginPoint);
        var e = go.AddComponent<FirefoxPipEffect>();
        e._owner = owner;
        e._data = data;
        e._a = attack;
        e.StartCoroutine(e.Run());
        return e;
    }

    IEnumerator Run()
    {
        Vector2 start = _owner != null ? _owner.OriginPoint : (Vector2)transform.position;
        _prop = KitProp.Spawn(_owner, start, FirefoxArt.PipWindow, _a.Size, p => OnBroken(), 1, 0f, 30);
        _prop.transform.SetParent(transform, true);
        _prop.DestroyOnBreak = false;
        // はがれる（小さく → 大きく、少し下へ）
        float t0 = Time.time;
        Transform pt = _prop.transform;
        while (Time.time - t0 < _a.Peel)
        {
            if (_broken) yield break;
            float k = (Time.time - t0) / _a.Peel;
            pt.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, 1f - (1f - k) * (1f - k));
            pt.position = start + new Vector2(0f, -1.2f * k);
            yield return null;
        }
        pt.localScale = Vector3.one;
        // 追う
        t0 = Time.time;
        while (Time.time - t0 < _a.ChaseTime)
        {
            if (_broken) yield break;
            if (_owner != null && _owner.TargetPoint is Vector2 target)
            {
                Vector2 p = pt.position;
                pt.position = Vector2.MoveTowards(p, target + Vector2.up * 0.3f, _a.ChaseSpeed * Time.deltaTime);
            }
            yield return null;
        }
        // 止まって点滅（赤い輪）
        Vector2 at = pt.position;
        int d = Mathf.Clamp(Mathf.RoundToInt(_a.Radius * 2f * 18f), 8, 90);
        SpriteRenderer ring = AttackRunner.AddVisual(transform, FirefoxArt.Ring(d), (Color)FirefoxArt.Red, Vector2.one * _a.Radius * 2f, 29, "Ring");
        ring.transform.position = at;
        SpriteRenderer fill = AttackRunner.AddVisual(transform, FxArt.Disc(d), new Color(1f, 0.27f, 0.23f, 0.15f), Vector2.one * _a.Radius * 2f, 28, "Fill");
        fill.transform.position = at;
        t0 = Time.time;
        while (Time.time - t0 < _a.Blink)
        {
            if (_broken) { Destroy(ring.gameObject); Destroy(fill.gameObject); yield break; }
            bool on = Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
            if (_prop != null && _prop.Visual != null) _prop.Visual.color = on ? Color.white : new Color(1f, 0.5f, 0.5f);
            ring.enabled = on || _a.Blink - (Time.time - t0) > 0.3f;
            yield return null;
        }
        // はじける
        if (_prop != null) Destroy(_prop.gameObject);
        Destroy(ring.gameObject);
        Destroy(fill.gameObject);
        if (_owner != null && _owner.isActiveAndEnabled)
        {
            var hitGo = new GameObject("Burst");
            hitGo.layer = gameObject.layer;
            hitGo.transform.SetParent(transform, false);
            hitGo.transform.position = at;
            var rb = hitGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
            var col = hitGo.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = _a.Radius;
            Hitbox hb = _owner.AddHitbox(hitGo, _data, col);
            hb.Activate();
            _owner.NotifyReleased(_data);
        }
        FxKit.Ring(at, _a.Radius * 2f, 0.25f, (Color)FirefoxArt.Orange);
        FxKit.Ring(at, _a.Radius * 1.2f, 0.2f, Color.white);
        yield return new WaitForSeconds(_a.HitWindow);
        Destroy(gameObject);
    }

    void OnBroken()
    {
        if (_broken) return;
        _broken = true;
        Vector2 at = _prop != null ? (Vector2)_prop.transform.position : (Vector2)transform.position;
        FxKit.Ring(at, 1.6f, 0.2f, Color.white);
        if (_prop != null) Destroy(_prop.gameObject);
        Destroy(gameObject, 0.05f);
    }
}
