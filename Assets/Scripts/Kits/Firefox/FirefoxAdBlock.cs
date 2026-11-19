using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Firefox の能力：広告ブロック（2026-11-19。docs/app-kits-2-design.md 4-6）。クールタイム 7秒・ゲージ 14。
/// 2.5秒の盾。敵の弾が盾に触れると消える。最初に1つ防ぐと、防いだ相手（弾を撃った人）の方向へ弾を返す（ゲージ 14）。
/// 防がなければ何も出ない（【要相談⑭】のとおり）。ステージの決まりに頼らない
/// ・敵の弾（Projectile）は、盾に触れたら何発でも消える
/// ・体当たり・床の判定などの当たりは、1回だけ止める（KitPlayer.Guard。止めたあと 0.5秒は無敵）
/// </summary>
[CreateAssetMenu(fileName = "Firefox_AdBlock", menuName = "Team1/Kits/Firefox Ad Block")]
public class FirefoxAdBlock : CustomAttack
{
    [Tooltip("返す弾（種類 Projectile。ダメージ・ゲージはこちら）")]
    [SerializeField] AttackData _counter;
    [SerializeField, Min(0.1f)] float _seconds = 2.5f;
    [Tooltip("盾の半径（マス）")]
    [SerializeField, Min(0.3f)] float _radius = 1.25f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _counter != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        GameObject go = owner.CreateSpawnedObject("Firefox_AdBlock", owner.transform.position);
        go.AddComponent<FirefoxShield>().Setup(owner, _counter, _seconds, _radius);
        owner.NotifyReleased(data);
        yield break;
    }
}

/// <summary>広告ブロックの盾（プレイヤーについて回る）。時間が来たら消える</summary>
public class FirefoxShield : MonoBehaviour
{
    AttackRunner _owner;
    AttackData _counter;
    float _until;
    float _radius;
    bool _countered;
    SpriteRenderer _ring, _fill, _icon;
    TextMesh _count;
    int _blocked;
    KitGuard _guard;

    public void Setup(AttackRunner owner, AttackData counter, float seconds, float radius)
    {
        _owner = owner;
        _counter = counter;
        _until = Time.time + seconds;
        _radius = radius;
        int d = Mathf.Clamp(Mathf.RoundToInt(radius * 2f * 18f), 8, 90);
        _fill = AttackRunner.AddVisual(transform, FxArt.Disc(d), new Color(0.46f, 0.26f, 0.9f, 0.18f), Vector2.one * radius * 2f, 35, "Fill");
        _ring = AttackRunner.AddVisual(transform, FirefoxArt.Ring(d), (Color)FirefoxArt.Purple, Vector2.one * radius * 2f, 36, "Ring");
        _icon = RuntimeSprites.Show(transform, FirefoxArt.Shield, new Vector2(0f, radius + 0.35f), 37, 1f, "Icon");
        _count = WorldText.Create(transform, "", 0.4f, Color.white, 37, true, TextAnchor.MiddleLeft, "Count");
        _count.transform.localPosition = new Vector3(0.45f, radius + 0.35f, 0f);
        _guard = KitPlayer.Guard(seconds, OnGuard, 0.5f);
        Follow();
    }

    void OnDestroy() => _guard?.Release();

    /// <summary>当たりを1回だけ止める（弾でない当たりも）。最初の1回なら、当てた相手へ弾を返す</summary>
    bool OnGuard(DamageInfo info)
    {
        Vector2 from = info.Source != null ? (Vector2)info.Source.transform.position : info.HitPoint;
        FxKit.Ring(info.HitPoint, 1.2f, 0.15f, Color.white);
        _blocked++;
        if (_count != null) _count.text = "x" + _blocked;
        if (!_countered)
        {
            _countered = true;
            Counter(from);
        }
        _guard?.Release();
        return true;
    }

    void Follow()
    {
        if (_owner == null) return;
        transform.position = (Vector2)_owner.transform.position + Vector2.up * 0.5f;
    }

    void LateUpdate()
    {
        if (_owner == null || !_owner.isActiveAndEnabled || Time.time >= _until) { Destroy(gameObject); return; }
        Follow();
        float left = _until - Time.time;
        bool on = left > 0.5f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
        _ring.enabled = _fill.enabled = on;
        Block();
    }

    void Block()
    {
        Vector2 c = transform.position;
        float r2 = _radius * _radius;
        foreach (var p in FindObjectsOfType<Projectile>())
        {
            if (p == null || p.Owner == null || p.Owner.Team != Team.Enemy) continue;
            if (((Vector2)p.transform.position - c).sqrMagnitude > r2) continue;
            Vector2 from = p.Owner.transform.position;
            FxKit.Ring(p.transform.position, 0.8f, 0.15f, Color.white);
            Destroy(p.gameObject);
            _blocked++;
            _count.text = "x" + _blocked;
            if (!_countered)
            {
                _countered = true;
                Counter(from);
            }
        }
    }

    void Counter(Vector2 target)
    {
        Vector2 origin = transform.position;
        Vector2 dir = target - origin;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : new Vector2(_owner.Facing, 0f);
        Projectile p = Projectile.Spawn(_owner, _counter, origin, dir);
        if (p != null) KitDress.Swap(p.gameObject, FirefoxArt.Fireball);
        _owner.NotifyReleased(_counter);
        FxKit.Ring(origin, _radius * 2.4f, 0.2f, (Color)FirefoxArt.Orange);
    }
}
