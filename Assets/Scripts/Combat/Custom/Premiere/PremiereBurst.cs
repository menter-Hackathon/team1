using UnityEngine;

/// <summary>
/// 【仮】爆発の絵（2026-11-18。docs/app-boss-skills-design.md 3-2・3-5「オレンジの円が広がる」）。
/// 判定（AreaStrike）の子に付ける。判定と一緒に消える。見た目だけで、当たり判定は持たない
/// </summary>
public class PremiereBurst : MonoBehaviour
{
    const float StartScale = 0.35f; // 広がり始めの大きさ（直径の割合）

    SpriteRenderer _outer;
    SpriteRenderer _inner;
    float _diameter;
    float _duration;
    float _start;

    /// <summary>parent の位置に、直径 diameter（マス）の爆発を duration 秒かけて広げる</summary>
    public static PremiereBurst Attach(Transform parent, float diameter, float duration)
    {
        Transform root = PremiereArt.Child(parent, "PremiereBurst", Vector2.zero);
        var b = root.gameObject.AddComponent<PremiereBurst>();
        b._outer = AttackRunner.AddVisual(root, PlaceholderSprites.Circle, PremiereArt.FireColor, Vector2.one, 22, "Outer");
        b._inner = AttackRunner.AddVisual(root, PlaceholderSprites.Circle, PremiereArt.FireCore, Vector2.one * 0.6f, 23, "Inner");
        b._diameter = Mathf.Max(0.1f, diameter);
        b._duration = Mathf.Max(0.05f, duration);
        b._start = Time.time;
        b.Apply(0f);
        return b;
    }

    void Update() => Apply(Mathf.Clamp01((Time.time - _start) / _duration));

    void Apply(float t)
    {
        float ease = 1f - (1f - t) * (1f - t);
        float d = _diameter * Mathf.Lerp(StartScale, 1f, ease);
        transform.localScale = new Vector3(d, d, 1f);
        _outer.color = PremiereArt.WithAlpha(PremiereArt.FireColor, Mathf.Lerp(0.95f, 0.15f, t * t));
        _inner.color = PremiereArt.WithAlpha(Color.Lerp(Color.white, PremiereArt.FireCore, t), Mathf.Lerp(1f, 0f, t));
    }
}
