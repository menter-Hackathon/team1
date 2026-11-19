using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Blender の能力「S スケール」（2026-11-19。docs/app-kits-2-design.md 3-5）。担当A。
/// ・正面 2マスに立方体（3D。オレンジの輪かく）を置き、0.15秒で 3×3マスに広げる（判定）。雑魚は外へ押す（攻撃データのノックバック）
/// ・広がったあと少し止まって縮んで消える。ステージの決まりに頼らない
/// </summary>
[CreateAssetMenu(fileName = "BlenderScaleSkill_", menuName = "Team1/Kits/Blender/Scale Skill")]
public class BlenderScaleSkill : CustomAttack
{
    [SerializeField, Min(0.5f)] float _forward = 2f;
    [SerializeField, Min(0.5f)] float _size = 3f;
    [SerializeField, Min(0.05f)] float _grow = 0.15f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        int face = owner.Facing;
        Vector2 o = owner.OriginPoint;
        float x = o.x + face * _forward;
        float floor = o.y - 0.5f;
        if (Kit3D.FindFloor(x, o.y + 0.3f, out float f, true) && o.y - f < 3f) floor = f;
        GameObject root = owner.CreateSpawnedObject("BlenderScaleSkill", new Vector2(x, floor + _size * 0.5f));
        var e = root.AddComponent<BlenderScaleSkillEffect>();
        e.Setup(owner, data, _size, _grow);
        owner.NotifyReleased(data);
        yield break;
    }
}

/// <summary>広がる立方体（能力。自分で進む）</summary>
public class BlenderScaleSkillEffect : MonoBehaviour
{
    Kit3DVisual _visual;
    Hitbox _hitbox;
    Transform _holder;
    float _size, _grow, _t0;

    internal void Setup(AttackRunner owner, AttackData data, float size, float grow)
    {
        _size = size;
        _grow = grow;
        _t0 = Time.time;
        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = Vector2.one * size;
        _hitbox = owner.AddHitbox(gameObject, data, box);
        _hitbox.Activate();
        var h = new GameObject("Holder");
        h.layer = gameObject.layer;
        h.transform.SetParent(transform, false);
        _holder = h.transform;
        _visual = Kit3D.AddVisual(_holder, Kit3DShape.Cube, Vector3.one * size, BlenderArt.Object, gameObject);
        _visual.AddOutline(BlenderArt.Select, 0.12f);
        SetSize(0.4f);
    }

    void SetSize(float s)
    {
        _visual.SetScale(s / _size);
        _holder.localPosition = new Vector3(0f, -_size * 0.5f + s * 0.5f, 0f);
    }

    void Update()
    {
        float t = Time.time - _t0;
        if (t < _grow) { SetSize(Mathf.Lerp(0.4f, _size, t / _grow)); return; }
        if (t < _grow + 0.2f) { SetSize(_size); return; }
        if (_hitbox.IsActive) _hitbox.Deactivate();
        float k = Mathf.Clamp01((t - _grow - 0.2f) / 0.2f);
        SetSize(Mathf.Lerp(_size, 0.01f, k));
        if (k >= 1f) Destroy(gameObject);
    }
}
