using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Blender の技2「S（スケール）」（2026-11-19。docs/app-kits-2-design.md 3-5）。担当A。
/// 流れ：床の上（プレイヤーの位置）に小さい立方体（0.5マス）と `S` の印 → 大きくなった時の赤い枠（5×5マス。1.0秒）→
///       0.2秒で 5マスに広がる（判定）→ 0.5秒で縮んで消える。
/// ・避け方：枠の外へ（2.5マス＋）。枠と当たる所が同じ
/// ・第2形態：縮むときに 4つに分かれ、ななめ上へ 2つ、横へ 2つ飛ぶ（1マスの立方体。毎秒 7マス。攻撃データの「かけら」の欄）
/// </summary>
[CreateAssetMenu(fileName = "BlenderScale_", menuName = "Team1/Kits/Blender/Scale")]
public class BlenderScaleAttack : CustomAttack
{
    [SerializeField, Min(0.1f)] float _small = 0.5f;
    [SerializeField, Min(1f)] float _big = 5f;
    [Tooltip("赤い枠の秒数（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 1f;
    [SerializeField, Min(0.05f)] float _grow = 0.2f;
    [SerializeField, Min(0.05f)] float _shrink = 0.5f;
    [Tooltip("第2形態：縮むときに飛ぶかけら（弾の攻撃データ。空なら出さない）。名前に枠の語を入れない")]
    [SerializeField] AttackData _shard;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 target = owner.TargetPoint ?? arena.center;
        float half = _big * 0.5f;
        float x = Mathf.Clamp(target.x, arena.xMin + half, arena.xMax - half);
        float floor = arena.yMin;
        if (Kit3D.FindFloor(x, target.y + 0.1f, out float f, true)) floor = f;
        floor = Mathf.Min(floor, arena.yMax - _big);
        GameObject root = owner.CreateSpawnedObject("BlenderScale", new Vector2(x, floor + half));
        var e = root.AddComponent<BlenderScaleEffect>();
        e.Setup(owner, data, floor, _small, _big, _telegraph, _grow, _shrink, _shard);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_telegraph + _grow + 0.1f);
    }
}

/// <summary>S（スケール）の立方体（自分で進む）</summary>
public class BlenderScaleEffect : CustomEffect
{
    AttackRunner _owner;
    AttackData _shard;
    Kit3DVisual _visual;
    Hitbox _hitbox;
    GameObject _frame, _keys;
    float _floor, _small, _big, _telegraph, _grow, _shrink;
    float _t0;
    int _stage;

    internal void Setup(AttackRunner owner, AttackData data, float floor, float small, float big, float telegraph, float grow, float shrink, AttackData shard)
    {
        _owner = owner;
        _shard = shard;
        _floor = floor;
        _small = small;
        _big = big;
        _telegraph = telegraph;
        _grow = grow;
        _shrink = shrink;
        _t0 = Time.time;

        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = Vector2.one * (big - 0.2f);
        _hitbox = owner.AddHitbox(gameObject, data, box);

        // 立方体（大きさ big を倍率で縮めて出す。床に下の端を付ける）
        var holder = new GameObject("Holder");
        holder.layer = gameObject.layer;
        holder.transform.SetParent(transform, false);
        _visual = Kit3D.AddVisual(holder.transform, Kit3DShape.Cube, Vector3.one * big, BlenderArt.Object, gameObject, "blender.boss", 8);
        _visual.Busy = true;
        _visual.AddOutline(BlenderArt.Select, 0.15f);
        SetSize(small);

        // 予告：赤い枠（5×5）と S の印
        _frame = new GameObject("Frame");
        _frame.layer = gameObject.layer;
        _frame.transform.SetParent(transform, false);
        RuntimeSprites.Show(_frame.transform, Kit3DArt.Corners(Vector2.one * big), Vector2.zero, 18, 1f, "Corners").color = KitDress.Red;
        AttackRunner.AddVisual(_frame.transform, PlaceholderSprites.Square, new Color(1f, 0.27f, 0.23f, 0.15f), Vector2.one * big, 16, "Fill");
        _keys = BlenderArt.Key(transform, "S", new Vector2(0f, -big * 0.5f + small + 0.6f)).gameObject;
    }

    // 立方体の大きさ（下の端は床のまま）
    void SetSize(float s)
    {
        float k = s / _big;
        _visual.SetScale(k);
        float y = -_big * 0.5f + s * 0.5f;
        Transform h = _visual.transform.parent;
        h.localPosition = new Vector3(0f, y, 0f);
    }

    void Update()
    {
        float t = Time.time - _t0;
        if (_stage == 0)
        {
            float left = _telegraph - t;
            if (_frame != null) _frame.SetActive(left > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0);
            if (left > 0f) return;
            _stage = 1;
            _t0 = Time.time;
            if (_frame != null) Destroy(_frame);
            if (_keys != null) Destroy(_keys);
            _hitbox.Activate();
            return;
        }
        if (_stage == 1)
        {
            SetSize(Mathf.Lerp(_small, _big, Mathf.Clamp01(t / _grow)));
            if (t < _grow + 0.08f) return;
            _stage = 2;
            _t0 = Time.time;
            _hitbox.Deactivate();
            _visual.Busy = false;
            SpawnShards();
            return;
        }
        SetSize(Mathf.Lerp(_big, 0.01f, Mathf.Clamp01(t / _shrink)));
        if (t >= _shrink) Destroy(gameObject);
    }

    // 第2形態：4つに分かれて飛ぶ（ななめ上へ 2つ、横へ 2つ）
    void SpawnShards()
    {
        if (_shard == null || _owner == null) return;
        Vector2 c = new Vector2(transform.position.x, _floor + 0.6f);
        Vector2[] dirs = { new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0.7f, 0.7f), new Vector2(-0.7f, 0.7f) };
        for (int i = 0; i < dirs.Length; i++)
        {
            Vector2 at = c + dirs[i] * 0.8f + (dirs[i].y > 0f ? new Vector2(0f, 0.8f) : Vector2.zero);
            Projectile p = Projectile.Spawn(_owner, _shard, at, dirs[i], i);
            if (p != null) Kit3D.Dress(p.gameObject, Kit3DShape.Cube, Vector3.one * _shard.HitSize.x, BlenderArt.Object, "blender.boss", 8);
        }
        _owner.NotifyReleased(_shard);
    }
}
