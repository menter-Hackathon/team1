using UnityEngine;

/// <summary>
/// 【仮】見た目だけの写し（2026-11-18：zip圧縮で吸いこむ物が縮みながら1点に集まる・zip がはじける）。
/// 元の絵を写して、決まった時間で動き・大きさ・濃さを変え、終わったら自分で消える。当たり判定はない
/// </summary>
public class FinderGhost : MonoBehaviour
{
    SpriteRenderer _sr;
    Transform _follow;       // 集まる先（動く物）。消えたら最後の位置
    Vector2 _from, _to;
    Vector3 _startScale;
    float _endScale;
    float _start, _duration;
    float _startAlpha, _endAlpha;

    /// <summary>元の絵（SpriteRenderer）を写して、follow（無ければ to）へ縮みながら動かす</summary>
    public static FinderGhost CopyOf(SpriteRenderer source, Transform follow, Vector2 to, float duration)
    {
        if (source == null || source.sprite == null) return null;
        Transform t = source.transform;
        FinderGhost g = Create(source.sprite, source.color, t.position, t.rotation, t.lossyScale, source.sortingOrder + 1);
        g._sr.flipX = source.flipX;
        g._sr.flipY = source.flipY;
        g._follow = follow;
        g._to = to;
        g._endScale = 0f;
        g._duration = Mathf.Max(0.01f, duration);
        return g;
    }

    /// <summary>その場で大きくなりながら消える（はじける）</summary>
    public static FinderGhost Pop(Sprite sprite, Vector2 position, float scale, float endScale, float duration, int sortingOrder)
    {
        if (sprite == null) return null;
        FinderGhost g = Create(sprite, Color.white, position, Quaternion.identity, Vector3.one * scale, sortingOrder);
        g._to = position;
        g._endScale = endScale / Mathf.Max(0.001f, scale);
        g._duration = Mathf.Max(0.01f, duration);
        g._endAlpha = 0f;
        return g;
    }

    static FinderGhost Create(Sprite sprite, Color color, Vector3 position, Quaternion rotation, Vector3 scale, int order)
    {
        var go = new GameObject("FinderGhost");
        go.transform.position = position;
        go.transform.rotation = rotation;
        go.transform.localScale = scale;
        var g = go.AddComponent<FinderGhost>();
        g._sr = go.AddComponent<SpriteRenderer>();
        g._sr.sprite = sprite;
        g._sr.color = color;
        g._sr.sortingOrder = order;
        g._from = position;
        g._startScale = scale;
        g._start = Time.time;
        g._startAlpha = color.a;
        g._endAlpha = color.a;
        return g;
    }

    void Update()
    {
        float k = Mathf.Clamp01((Time.time - _start) / _duration);
        if (_follow != null) _to = _follow.position;
        float e = k * k; // だんだん速く
        transform.position = Vector2.Lerp(_from, _to, e);
        transform.localScale = _startScale * Mathf.Lerp(1f, _endScale, e);
        Color c = _sr.color;
        c.a = Mathf.Lerp(_startAlpha, _endAlpha, k);
        _sr.color = c;
        if (k >= 1f) Destroy(gameObject);
    }
}
