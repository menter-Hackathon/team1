using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】「あと少し」の印（2026-11-19。docs/stage-pacing-design.md 7章。担当1）。
/// 残りが3体以下のとき、残っている雑魚の頭の上に小さい白い印を出す（探さなくてよいように）。文字は出さない。
/// 印は StagePacer の子に作るので、ステージが終わると一緒に消える。
/// </summary>
public class PaceMarks
{
    const int Order = 60; // 雑魚・予告より手前、隅の通知（70）より奥
    static readonly Color32 Ink = new Color32(0x1c, 0x1c, 0x1e, 0xff);
    static readonly Color32 White = new Color32(0xff, 0xff, 0xff, 0xff);

    readonly Transform _root;
    readonly Dictionary<Health, Transform> _marks = new Dictionary<Health, Transform>();
    readonly List<Health> _gone = new List<Health>();

    public PaceMarks(Transform root) { _root = root; }

    /// <summary>毎フレーム。show が false なら全部消す</summary>
    public void Tick(IReadOnlyList<Health> alive, bool show)
    {
        if (!show || alive == null) { Clear(); return; }
        for (int i = 0; i < alive.Count; i++)
        {
            Health h = alive[i];
            if (h != null && !h.IsDead && h.gameObject.activeInHierarchy && !_marks.ContainsKey(h)) _marks[h] = Create();
        }
        _gone.Clear();
        float bob = Mathf.Sin(Time.time * 6f) * 0.08f;
        foreach (var kv in _marks)
        {
            Health h = kv.Key;
            if (h == null || h.IsDead || !h.gameObject.activeInHierarchy || kv.Value == null) { _gone.Add(h); continue; }
            Vector2 head = FxKit.HeadOf(h.gameObject, (Vector2)h.transform.position + Vector2.up);
            kv.Value.position = new Vector3(head.x, head.y + 0.45f + bob, 0f);
        }
        foreach (Health h in _gone)
        {
            if (_marks.TryGetValue(h, out Transform t) && t != null) Object.Destroy(t.gameObject);
            _marks.Remove(h);
        }
    }

    /// <summary>全部消す（ボスの登場のとき）</summary>
    public void Clear()
    {
        foreach (var kv in _marks)
        {
            if (kv.Value != null) Object.Destroy(kv.Value.gameObject);
        }
        _marks.Clear();
    }

    Transform Create()
    {
        var go = new GameObject("PaceMark");
        if (_root != null) go.transform.SetParent(_root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MarkSprite();
        sr.sortingOrder = Order;
        return go.transform;
    }

    /// <summary>下向きの小さい三角（白・黒いふち）</summary>
    static Sprite MarkSprite() => RuntimeSprites.FromAscii("Pace_Mark", new[]
    {
        "KKKKKKK",
        "KWWWWWK",
        ".KWWWK.",
        "..KWK..",
        "...K...",
    }, new Dictionary<char, Color32> { { 'K', Ink }, { 'W', White } });
}
