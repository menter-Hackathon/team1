using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Blender の技3（第2形態）「Suzanne」（2026-11-19。docs/app-kits-2-design.md 3-5）。担当A。
/// 流れ：ボスの上にサルの頭（直径 2.5マス。3D）→ 3回はずんで部屋を横切る。着地する所に赤い円（0.8秒前から）。
///       着地の間は 4マス、1回 0.9秒。はずんでいる下はくぐれる（頭の下の端が高さ 4マスまで上がる）。
/// ・当たるのは頭に触れたとき（判定は見た目より少し小さい円）
/// </summary>
[CreateAssetMenu(fileName = "BlenderSuzanne_", menuName = "Team1/Kits/Blender/Suzanne")]
public class BlenderSuzanneAttack : CustomAttack
{
    [SerializeField, Min(1f)] float _diameter = 2.5f;
    [SerializeField, Min(1)] int _bounces = 3;
    [SerializeField, Min(1f)] float _step = 4f;
    [SerializeField, Min(0.3f)] float _hop = 0.9f;
    [SerializeField, Min(1f)] float _apex = 4f;
    [Tooltip("赤い円を着地の何秒前から出すか（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 boss = owner.transform.position;
        Vector2 target = owner.TargetPoint ?? arena.center;
        int dir = target.x >= boss.x ? 1 : -1;
        float r = _diameter * 0.5f;
        float span = _step * (_bounces - 1);
        float minL = arena.xMin + r + 0.3f, maxL = arena.xMax - r - 0.3f;
        float first = boss.x + dir * 2f;
        // 3つ目の着地も部屋の中に収める
        if (dir > 0) first = Mathf.Clamp(first, minL, maxL - span);
        else first = Mathf.Clamp(first, minL + span, maxL);
        GameObject root = owner.CreateSpawnedObject("BlenderSuzanne", boss);
        var e = root.AddComponent<BlenderSuzanneEffect>();
        e.Setup(owner, data, arena.yMin, first, dir, _diameter, _bounces, _step, _hop, _apex, _telegraph);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(0.45f + _hop * (_bounces - 0.5f) + 0.6f);
    }
}

/// <summary>はずむサルの頭（自分で進む）</summary>
public class BlenderSuzanneEffect : CustomEffect
{
    Transform _head;
    Rigidbody2D _rb;
    Hitbox _hitbox;
    Kit3DVisual _visual;
    float _floor, _first, _step, _hop, _apex, _telegraph, _r;
    int _dir, _bounces;
    float _start;
    GameObject[] _marks;

    const float Appear = 0.45f;

    internal void Setup(AttackRunner owner, AttackData data, float floor, float first, int dir, float d, int bounces, float step, float hop, float apex, float telegraph)
    {
        _floor = floor;
        _first = first;
        _dir = dir;
        _r = d * 0.5f;
        _bounces = bounces;
        _step = step;
        _hop = hop;
        _apex = apex;
        _telegraph = telegraph;
        _start = Time.time;

        var go = new GameObject("Head");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        _head = go.transform;
        _rb = go.AddComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.useFullKinematicContacts = true;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = _r * 0.8f;
        _hitbox = owner.AddHitbox(go, data, col, false);
        _visual = BlenderArt.Suzanne(go.transform, d, gameObject);
        _visual.Busy = true;
        _visual.SetScale(0.01f);
        _head.position = PosAt(0f);

        // 着地する所の赤い円（着地の telegraph 秒前から）
        _marks = new GameObject[bounces];
        for (int i = 0; i < bounces; i++)
        {
            var m = new GameObject("Mark" + i);
            m.layer = gameObject.layer;
            m.transform.SetParent(transform, false);
            m.transform.position = new Vector2(first + dir * step * i, floor + 0.12f);
            AttackRunner.AddVisual(m.transform, PlaceholderSprites.Circle, new Color(1f, 0.27f, 0.23f, 0.55f), new Vector2(d + 0.4f, 0.45f), 17, "Circle");
            m.SetActive(false);
            _marks[i] = m;
        }
    }

    // 着地の時刻（0 ＝ 1回目）
    float LandTime(int i) => Appear + _hop * 0.5f + _hop * i;

    // 頭の真ん中の位置（t は始まってからの秒）
    Vector2 PosAt(float t)
    {
        // 最初は 1回目の着地の半分手前の、いちばん高い所に出る
        float x0 = _first - _dir * _step * 0.5f;
        if (t <= Appear) return new Vector2(x0, _floor + _r + _apex);
        float s = (t - Appear) / _hop;      // いちばん高い所から何回ぶん（0.5 で 1回目の着地）
        float x = x0 + _dir * _step * s;
        float u = s + 0.5f;
        float frac = u - Mathf.Floor(u);    // 着地から着地までの 0〜1（0.5 がいちばん高い）
        float h = 4f * _apex * frac * (1f - frac);
        return new Vector2(x, _floor + _r + h);
    }

    void Update()
    {
        float t = Time.time - _start;
        if (t <= Appear) _visual.SetScale(Mathf.Clamp01(t / Appear));
        else
        {
            _visual.SetScale(1f);
            if (!_hitbox.IsActive && t < LandTime(_bounces - 1) + 0.05f) _hitbox.Activate();
        }
        for (int i = 0; i < _marks.Length; i++)
        {
            if (_marks[i] == null) continue;
            float land = LandTime(i);
            bool on = t >= land - _telegraph && t < land + 0.1f;
            if (on && land - t < 0.3f && land - t > 0f) on = Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            _marks[i].SetActive(on);
            if (t >= land + 0.1f) { Destroy(_marks[i]); _marks[i] = null; }
        }
        float end = LandTime(_bounces - 1);
        if (t > end + 0.05f)
        {
            // 最後の着地のあとは、その場で縮んで消える
            _hitbox.Deactivate();
            _visual.Busy = false;
            float k = 1f - Mathf.Clamp01((t - end - 0.05f) / 0.35f);
            _visual.SetScale(k);
            if (k <= 0f) Destroy(gameObject);
            return;
        }
        // 顔が見えるように、回さずに少しかしげるだけ
        _visual.SetExtraRotation(Quaternion.AngleAxis(Mathf.Sin(t * 7f) * 10f, Vector3.forward));
    }

    void FixedUpdate()
    {
        float t = Time.time - _start;
        if (t > LandTime(_bounces - 1) + 0.05f) return;
        _rb.MovePosition(PosAt(t));
    }
}
