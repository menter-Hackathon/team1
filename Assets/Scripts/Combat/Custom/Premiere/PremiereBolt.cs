using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】落雷の絵（2026-11-18。docs/app-boss-skills-design.md 3-2「黄色のぎざぎざの柱に白い芯」）。
/// 判定（AreaStrike）の子に付ける。判定と一緒に消える。ぎざぎざの段を縦に並べ、左右を入れ替えてちらつかせる
/// </summary>
public class PremiereBolt : MonoBehaviour
{
    const float FlickerInterval = 0.05f;

    readonly List<SpriteRenderer> _tiles = new List<SpriteRenderer>();
    float _duration;
    float _start;
    int _variant = -1;

    /// <summary>parent の真ん中に、横 width×縦 height（マス）の柱を duration 秒出す</summary>
    public static PremiereBolt Attach(Transform parent, float width, float height, float duration)
    {
        Transform root = PremiereArt.Child(parent, "PremiereBolt", Vector2.zero);
        var b = root.gameObject.AddComponent<PremiereBolt>();
        b._duration = Mathf.Max(0.05f, duration);
        b._start = Time.time;
        int count = Mathf.Max(1, Mathf.CeilToInt(height));
        float bottom = -height * 0.5f;
        for (int i = 0; i < count; i++)
        {
            var sr = RuntimeSprites.Show(root, PremiereArt.BoltTile(0), new Vector2(0f, bottom + 0.5f + i), 22, 1f, "Tile");
            // 段の絵は 1.5×1 マス。幅だけ合わせる
            sr.transform.localScale = new Vector3(width / 1.5f, 1f, 1f);
            b._tiles.Add(sr);
        }
        b.Apply(0f);
        return b;
    }

    void Update() => Apply(Mathf.Clamp01((Time.time - _start) / _duration));

    void Apply(float t)
    {
        int v = Mathf.FloorToInt((Time.time - _start) / FlickerInterval) & 1;
        if (v != _variant)
        {
            _variant = v;
            Sprite s = PremiereArt.BoltTile(v);
            foreach (var tile in _tiles) tile.sprite = s;
        }
        float a = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
        foreach (var tile in _tiles) tile.color = new Color(1f, 1f, 1f, a);
    }
}
