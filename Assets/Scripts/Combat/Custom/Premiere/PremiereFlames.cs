using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】着地した所の火の絵（2026-11-18。docs/app-boss-skills-design.md 3-1 第2形態）。
/// 判定（AreaStrike の持続）の子に付ける。判定と一緒に消える。小さい炎を横に並べ、ばらばらにゆらす
/// </summary>
public class PremiereFlames : MonoBehaviour
{
    readonly List<Transform> _flames = new List<Transform>();
    float _start;

    /// <summary>parent（判定の真ん中）に、横 size.x×縦 size.y（マス）の火を出す。炎は下の端に並べる</summary>
    public static PremiereFlames Attach(Transform parent, Vector2 size)
    {
        Transform root = PremiereArt.Child(parent, "PremiereFlames", new Vector2(0f, -size.y * 0.5f));
        var f = root.gameObject.AddComponent<PremiereFlames>();
        f._start = Time.time;
        int count = Mathf.Max(1, Mathf.RoundToInt(size.x / 0.5f));
        for (int i = 0; i < count; i++)
        {
            float x = -size.x * 0.5f + (i + 0.5f) * size.x / count;
            var sr = RuntimeSprites.Show(root, PremiereArt.SmallFlame, new Vector2(x, 0f), 21, 1f, "Flame");
            f._flames.Add(sr.transform);
        }
        return f;
    }

    void Update()
    {
        float time = Time.time - _start;
        for (int i = 0; i < _flames.Count; i++)
        {
            // 炎ごとに、ゆれる速さとずれを変える
            float k = 0.8f + 0.25f * Mathf.Sin(time * (11f + i * 3f) + i * 1.7f);
            _flames[i].localScale = new Vector3(i % 2 == 0 ? 1f : -1f, k, 1f);
        }
    }
}
