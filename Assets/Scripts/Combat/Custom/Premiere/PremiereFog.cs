using UnityEngine;

/// <summary>
/// 【仮】ディゾルブのもやの絵（2026-11-18。docs/app-boss-skills-design.md 3-5「当たった所に紫のもや」）。
/// 判定（AreaStrike の持続）の子に付ける。判定と一緒に消える。2枚を少しずつずらして、ゆらゆら動かす
/// </summary>
public class PremiereFog : MonoBehaviour
{
    SpriteRenderer _a;
    SpriteRenderer _b;
    Vector2 _size;
    float _duration;
    float _start;

    /// <summary>parent の真ん中に、横 size.x×縦 size.y（マス）のもやを duration 秒出す</summary>
    public static PremiereFog Attach(Transform parent, Vector2 size, float duration)
    {
        Transform root = PremiereArt.Child(parent, "PremiereFog", Vector2.zero);
        var f = root.gameObject.AddComponent<PremiereFog>();
        f._size = size;
        f._duration = Mathf.Max(0.1f, duration);
        f._start = Time.time;
        f._a = AttackRunner.AddVisual(root, PremiereArt.FogBlob, Color.white, size, 22, "FogA");
        f._b = AttackRunner.AddVisual(root, PremiereArt.FogBlob, Color.white, size * 0.8f, 23, "FogB");
        f._b.flipX = true;
        f.Apply(0f);
        return f;
    }

    void Update() => Apply(Mathf.Clamp01((Time.time - _start) / _duration));

    void Apply(float t)
    {
        float time = Time.time - _start;
        float fade = t < 0.08f ? t / 0.08f : t > 0.8f ? Mathf.Lerp(1f, 0f, (t - 0.8f) / 0.2f) : 1f;
        _a.transform.localPosition = new Vector3(Mathf.Sin(time * 3.1f) * 0.12f, Mathf.Cos(time * 2.3f) * 0.08f, 0f);
        _b.transform.localPosition = new Vector3(Mathf.Cos(time * 2.7f) * 0.18f, Mathf.Sin(time * 3.7f) * 0.1f, 0f);
        _a.color = new Color(1f, 1f, 1f, 0.85f * fade);
        _b.color = new Color(1f, 1f, 1f, (0.6f + 0.25f * Mathf.Sin(time * 9f)) * fade);
    }
}
