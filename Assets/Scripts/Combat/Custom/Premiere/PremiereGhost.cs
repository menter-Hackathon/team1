using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】残像（2026-11-18。docs/app-boss-skills-design.md 3-4：スローの場の中のプレイヤーに紫の残像、倍速のボスに赤っぽい残像）。
/// 相手の絵（子の SpriteRenderer）を写した絵を、少し前の位置に、色を付けて出す。相手が動かなければ、相手の後ろに重なって見えない。
/// 技の部品の子に作る（部品と一緒に消える）
/// </summary>
public class PremiereGhost : MonoBehaviour
{
    struct Sample
    {
        public float Time;
        public Vector3 Position;
    }

    Transform _target;
    Color _tint;
    float _delay;
    readonly List<SpriteRenderer> _sources = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> _copies = new List<SpriteRenderer>();
    readonly List<Sample> _samples = new List<Sample>();

    /// <summary>見せるか（スローの場の外にいる間は隠す、など）</summary>
    public bool Visible { get; set; } = true;

    /// <summary>parent の子に、target の delay 秒前の位置に出る残像を作る</summary>
    public static PremiereGhost Create(Transform parent, Transform target, Color tint, float delay)
    {
        if (target == null) return null;
        Transform root = PremiereArt.Child(parent, "Ghost", Vector2.zero);
        var g = root.gameObject.AddComponent<PremiereGhost>();
        g._target = target;
        g._tint = tint;
        g._delay = Mathf.Max(0.01f, delay);
        foreach (var src in target.GetComponentsInChildren<SpriteRenderer>())
        {
            if (src == null) continue;
            var go = new GameObject("Copy");
            go.layer = root.gameObject.layer;
            go.transform.SetParent(root, false);
            var copy = go.AddComponent<SpriteRenderer>();
            copy.sortingLayerID = src.sortingLayerID;
            copy.sortingOrder = src.sortingOrder - 1;
            g._sources.Add(src);
            g._copies.Add(copy);
        }
        return g;
    }

    void LateUpdate()
    {
        if (_target == null)
        {
            foreach (var c in _copies) c.enabled = false;
            return;
        }
        float now = Time.time;
        Vector3 current = _target.position;
        _samples.Add(new Sample { Time = now, Position = current });
        while (_samples.Count > 2 && _samples[1].Time <= now - _delay) _samples.RemoveAt(0);
        Vector3 past = _samples[0].Time <= now - _delay || _samples.Count == 1 ? _samples[0].Position : current;
        Vector3 offset = past - current;

        for (int i = 0; i < _sources.Count; i++)
        {
            SpriteRenderer src = _sources[i], copy = _copies[i];
            bool show = Visible && src != null && src.enabled && src.gameObject.activeInHierarchy && src.sprite != null && offset.sqrMagnitude > 0.0004f;
            copy.enabled = show;
            if (!show) continue;
            copy.sprite = src.sprite;
            copy.flipX = src.flipX;
            copy.flipY = src.flipY;
            Color c = _tint;
            c.a *= src.color.a;
            copy.color = c;
            Transform st = src.transform, ct = copy.transform;
            ct.position = st.position + offset;
            ct.rotation = st.rotation;
            ct.localScale = st.lossyScale; // 残像の親は拡大していない
        }
    }
}
