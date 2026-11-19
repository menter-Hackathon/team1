using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Space の切り替えの帯1本（ArcSpaceAttack が出す）。自分で進む。
/// 入ってくる側のふちに、赤い帯の高さと矢印（予告）→ グラデーションの帯が横切る（判定）→ 反対の壁で消える
/// </summary>
public class ArcBandEffect : CustomEffect
{
    AttackRunner _owner;
    AttackData _data;
    Rect _arena;
    bool _low;
    int _dir;
    float _delay, _telegraph, _speed, _width, _yMin, _yMax;

    public static ArcBandEffect Spawn(AttackRunner owner, AttackData data, Rect arena, bool low, int dir, float delay, float telegraph, float speed, float width, float lowTop, float highBottom)
    {
        if (owner == null || data == null) return null;
        GameObject go = owner.CreateSpawnedObject($"Arc_Band_{data.name}", arena.center);
        var e = go.AddComponent<ArcBandEffect>();
        e._owner = owner;
        e._data = data;
        e._arena = arena;
        e._low = low;
        e._dir = dir >= 0 ? 1 : -1;
        e._delay = delay;
        e._telegraph = telegraph;
        e._speed = speed;
        e._width = width;
        e._yMin = low ? arena.yMin : arena.yMin + highBottom;
        e._yMax = low ? arena.yMin + lowTop : arena.yMax;
        e.StartCoroutine(e.Run());
        return e;
    }

    IEnumerator Run()
    {
        if (_delay > 0f) yield return new WaitForSeconds(_delay);
        float h = _yMax - _yMin, cy = (_yMin + _yMax) * 0.5f;
        float edgeX = _dir > 0 ? _arena.xMin : _arena.xMax;

        // 予告：入ってくる側のふちに、帯の高さの赤い板と矢印
        transform.position = new Vector2(edgeX + _dir * 0.35f, cy);
        var tele = new GameObject("Telegraph").transform;
        tele.SetParent(transform, false);
        var plate = AttackRunner.AddVisual(tele, PlaceholderSprites.Square, new Color(1f, 0.27f, 0.23f, 0.35f), new Vector2(0.7f, h), 40, "Plate");
        var edge = AttackRunner.AddVisual(tele, PlaceholderSprites.Square, KitDress.Red, new Vector2(2f * KitDress.Px, h), 41, "Edge");
        edge.transform.localPosition = new Vector3(_dir * 0.35f, 0f, 0f);
        int arrows = Mathf.Clamp(Mathf.FloorToInt(h / 1.6f), 1, 5);
        var arrowSr = new SpriteRenderer[arrows];
        for (int i = 0; i < arrows; i++)
        {
            float y = arrows == 1 ? 0f : Mathf.Lerp(-h * 0.5f + 0.6f, h * 0.5f - 0.6f, i / (float)(arrows - 1));
            arrowSr[i] = RuntimeSprites.Show(tele, ArcArt.Arrow, new Vector2(_dir * 0.9f, y), 42, 1f, "Arrow");
            arrowSr[i].flipX = _dir < 0;
        }
        float end = Time.time + _telegraph;
        while (Time.time < end)
        {
            bool on = end - Time.time > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            tele.gameObject.SetActive(on);
            float k = Mathf.Repeat(Time.time * 2.5f, 1f);
            foreach (var a in arrowSr) if (a != null) a.transform.localPosition = new Vector3(_dir * (0.7f + k * 0.5f), a.transform.localPosition.y, 0f);
            yield return null;
        }
        Destroy(tele.gameObject);

        // 帯
        float startX = edgeX - _dir * _width * 0.5f, endX = (_dir > 0 ? _arena.xMax : _arena.xMin) + _dir * _width * 0.5f;
        transform.position = new Vector2(startX, cy);
        int pw = Mathf.RoundToInt(_width * 18f), ph = Mathf.RoundToInt(h * 18f);
        var body = RuntimeSprites.Show(transform, ArcArt.Gradient(pw, ph, 6, ArcArt.Pink, ArcArt.Purple, ArcArt.Cyan, true), Vector2.zero, 38, 1f, "Band");
        var trail = RuntimeSprites.Show(transform, ArcArt.Gradient(pw * 2, ph, 6, ArcArt.Pink, ArcArt.Purple, ArcArt.Cyan, true, 0.35f), new Vector2(-_dir * _width * 1.2f, 0f), 37, 1f, "Trail");
        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(_width, h);
        Hitbox hit = _owner.AddHitbox(gameObject, _data, box);
        hit.Activate();
        if (_owner != null) _owner.NotifyReleased(_data);
        while ((endX - transform.position.x) * _dir > 0f)
        {
            transform.position += new Vector3(_dir * _speed * Time.deltaTime, 0f, 0f);
            yield return null;
        }
        hit.Deactivate();
        Destroy(gameObject);
    }
}
