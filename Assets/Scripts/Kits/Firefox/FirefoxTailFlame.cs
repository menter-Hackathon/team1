using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Firefox のボスの技1：しっぽの炎（2026-11-19。docs/app-kits-2-design.md 4-6）。
/// ボスのしっぽから火の玉を山なりに 3つ（落ちる所の床に赤い影 0.8秒）→ 落ちた所に炎が残る（幅 1.5・高さ 1マス。1.5秒）。
/// ・避け方：影から出る。炎はジャンプで越える
/// ・第2形態：5つ。炎は 2.5秒
/// ・火の玉は見た目だけ（当たるのは床の炎）。炎は今の部品（指定位置の判定・持続）に、キットの炎の絵を差し替える
/// </summary>
[CreateAssetMenu(fileName = "Firefox_TailFlame", menuName = "Team1/Kits/Firefox Tail Flame")]
public class FirefoxTailFlame : CustomAttack
{
    [Tooltip("床に残る炎（種類 Area。大きさ 1.5×1・持続の秒数・床に付ける）")]
    [SerializeField] AttackData _flame;
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("影の秒数＝火の玉が飛ぶ秒数（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("火の玉を1つずつ投げる間（秒）")]
    [SerializeField, Min(0f)] float _stagger = 0.15f;
    [Tooltip("落ちる所の間（マス）")]
    [SerializeField, Min(1f)] float _spacing = 2.6f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _flame != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float px = owner.TargetPoint.HasValue ? owner.TargetPoint.Value.x : arena.center.x;
        List<float> xs = Spots(px, arena);
        for (int i = 0; i < xs.Count; i++)
        {
            float gx = xs[i];
            owner.TryFindGround(gx, arena.yMax - 0.2f, out float gy);
            FirefoxFireball.Spawn(owner, _flame, owner.OriginPoint, new Vector2(gx, gy), i * _stagger, _telegraph);
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_telegraph + xs.Count * _stagger + 0.3f);
    }

    /// <summary>1つはプレイヤーの足元、残りは左右へ交互に spacing ずつ（壁の外は反対へ）</summary>
    List<float> Spots(float px, Rect arena)
    {
        float half = _flame.AreaSize.x * 0.5f;
        float minX = arena.xMin + half + 0.2f, maxX = arena.xMax - half - 0.2f;
        var xs = new List<float> { Mathf.Clamp(px, minX, maxX) };
        int side = Random.value < 0.5f ? -1 : 1;
        for (int i = 1; xs.Count < _count && i < _count * 3; i++)
        {
            int step = (i + 1) / 2;
            int s = i % 2 == 1 ? side : -side;
            float x = px + s * step * _spacing + Random.Range(-0.3f, 0.3f);
            if (x < minX || x > maxX) continue;
            xs.Add(x);
        }
        return xs;
    }
}

/// <summary>火の玉1つ（床に赤い影 → 山なりに飛ぶ → 落ちた所に炎）。形態変化・正常化で消える</summary>
public class FirefoxFireball : CustomEffect
{
    AttackRunner _owner;
    AttackData _flame;
    Vector2 _from, _to;
    float _delay, _flight;

    public static FirefoxFireball Spawn(AttackRunner owner, AttackData flame, Vector2 from, Vector2 groundPoint, float delay, float flight)
    {
        GameObject go = owner.CreateSpawnedObject("Firefox_Fireball", from);
        var f = go.AddComponent<FirefoxFireball>();
        f._owner = owner;
        f._flame = flame;
        f._from = from;
        f._to = groundPoint;
        f._delay = delay;
        f._flight = flight;
        f.StartCoroutine(f.Run());
        return f;
    }

    IEnumerator Run()
    {
        // 影（すぐ出す。最後の 0.3秒は点滅）
        var sh = new GameObject("Shadow");
        sh.layer = gameObject.layer;
        sh.transform.position = _to + new Vector2(0f, 0.1f);
        sh.transform.SetParent(transform, true);
        SpriteRenderer shadow = AttackRunner.AddVisual(sh.transform, FxArt.Disc(24), new Color(1f, 0.27f, 0.23f, 0.6f), new Vector2(_flame.AreaSize.x, 0.4f), 16, "Ellipse");
        SpriteRenderer ball = RuntimeSprites.Show(transform, FirefoxArt.Fireball, Vector2.zero, 40, 1f, "Ball");
        ball.enabled = false;
        float total = _delay + _flight;
        float start = Time.time;
        Transform ballT = ball.transform;
        float peak = Mathf.Max(_from.y, _to.y) + 3f;
        while (Time.time - start < total)
        {
            float a = Time.time - start;
            float left = total - a;
            shadow.enabled = left > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            if (a >= _delay)
            {
                float k = Mathf.Clamp01((a - _delay) / _flight);
                ball.enabled = true;
                float x = Mathf.Lerp(_from.x, _to.x, k);
                // 2次の山なり（始め → 頂点 → 着地）
                float y = (1 - k) * (1 - k) * _from.y + 2 * (1 - k) * k * peak + k * k * (_to.y + 0.3f);
                ballT.position = new Vector3(x, y, 0f);
                ballT.rotation = Quaternion.Euler(0f, 0f, -k * 360f);
            }
            yield return null;
        }
        // 炎
        if (_owner != null && _owner.isActiveAndEnabled)
        {
            AreaStrike a = AreaStrike.Spawn(_owner, _flame, _to + new Vector2(0f, 0.3f), Vector2.up, 0f);
            if (a != null)
            {
                a.gameObject.AddComponent<FirefoxClearMark>(); // 50％の形態変化でも消える
                KitDress.Swap(a.gameObject, FirefoxArt.Flame(0));
                SpriteRenderer v = KitDress.FindVisual(a.transform);
                if (v != null) v.gameObject.AddComponent<FirefoxFlicker>();
            }
            FxKit.Ring(_to + new Vector2(0f, 0.4f), 1.6f, 0.2f, (Color)FirefoxArt.Yellow);
        }
        Destroy(gameObject);
    }
}

/// <summary>炎の絵を 2つの形で交互にゆらす</summary>
public class FirefoxFlicker : MonoBehaviour
{
    SpriteRenderer _sr;
    void Awake() => _sr = GetComponent<SpriteRenderer>();
    void Update()
    {
        if (_sr == null) return;
        _sr.sprite = FirefoxArt.Flame(Mathf.FloorToInt(Time.time / 0.12f));
    }
}

/// <summary>今の部品（床の炎）に付ける印。CustomEffect なので、ボスの形態変化（50％）で一緒に消える</summary>
public class FirefoxClearMark : CustomEffect { }
