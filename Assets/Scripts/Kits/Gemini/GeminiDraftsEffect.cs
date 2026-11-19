using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】3つの回答案の札と点滅（2026-11-19。docs/app-kits-1-design.md 7章 P1-1）。GeminiDraftsAttack が出す。担当3 の物。
/// 区画の上に、角の丸い白い札 1・2・3。点滅の間は、区画のふちが白く点滅する（どれもまだ決まっていない＝赤は使わない）。
/// 決まったら、安全な札が青になる。CustomEffect なので、形態変化（50％）で消える
/// </summary>
public class GeminiDraftsEffect : CustomEffect
{
    const int Order = 30;

    class Slot
    {
        public Rect Zone;
        public SpriteRenderer Tag;
        public TextMesh Num;
        public SpriteRenderer EdgeL, EdgeR, EdgeT;
    }

    readonly List<Slot> _slots = new List<Slot>();
    float _end;
    float _flickerUntil;
    int _safe = -1;

    public static GeminiDraftsEffect Spawn(AttackRunner owner, Rect[] zones, float seconds)
    {
        if (owner == null || zones == null) return null;
        GameObject go = owner.CreateSpawnedObject("GeminiDrafts", Vector2.zero);
        go.transform.position = Vector3.zero;
        var fx = go.AddComponent<GeminiDraftsEffect>();
        fx._end = Time.time + Mathf.Max(0.1f, seconds);
        for (int i = 0; i < zones.Length; i++) fx.AddSlot(zones[i], i);
        return fx;
    }

    void AddSlot(Rect z, int i)
    {
        var s = new Slot { Zone = z };
        Vector2 top = new Vector2(z.center.x, z.yMax + 0.55f);
        s.Tag = RuntimeSprites.Show(transform, GeminiArt.Label, top, Order, 2f, "Label" + i);
        s.Num = WorldText.Create(transform, (i + 1).ToString(), 0.55f, GeminiArt.Ink, Order + 1, true, TextAnchor.MiddleCenter, "Num" + i);
        s.Num.transform.position = new Vector3(top.x, top.y + 0.03f, 0f);
        Color w = new Color(1f, 1f, 1f, 0.8f);
        float px = GeminiArt.Px * 2f;
        s.EdgeL = FxKit.Box(transform, new Vector2(z.xMin + px * 0.5f + 0.05f, z.center.y), new Vector2(px, z.height), w, Order - 2, "EdgeL");
        s.EdgeR = FxKit.Box(transform, new Vector2(z.xMax - px * 0.5f - 0.05f, z.center.y), new Vector2(px, z.height), w, Order - 2, "EdgeR");
        s.EdgeT = FxKit.Box(transform, new Vector2(z.center.x, z.yMax - px * 0.5f), new Vector2(z.width - 0.1f, px), w, Order - 2, "EdgeT");
        _slots.Add(s);
    }

    /// <summary>3つとも点滅させる（seconds 秒）。安全の色は消す</summary>
    public void Flicker(float seconds)
    {
        _flickerUntil = Time.time + Mathf.Max(0f, seconds);
        _safe = -1;
        foreach (var s in _slots)
        {
            if (s.Tag != null) s.Tag.sprite = GeminiArt.Label;
            if (s.Num != null) WorldText.SetColor(s.Num, GeminiArt.Ink);
        }
    }

    /// <summary>安全な区画が決まった（その札を青に）</summary>
    public void Decide(int safe)
    {
        _flickerUntil = 0f;
        _safe = safe;
        for (int i = 0; i < _slots.Count; i++)
        {
            Slot s = _slots[i];
            bool ok = i == safe;
            if (s.Tag != null)
            {
                s.Tag.sprite = ok ? GeminiArt.LabelSafe : GeminiArt.Label;
                s.Tag.enabled = true;
            }
            if (s.Num != null)
            {
                WorldText.SetColor(s.Num, ok ? Color.white : GeminiArt.Ink);
                s.Num.gameObject.SetActive(true);
            }
            if (ok) FxAnim.Sprite(GeminiArt.Star4(9), s.Tag != null ? (Vector2)s.Tag.transform.position + new Vector2(0.55f, 0.3f) : s.Zone.center, Order + 2, 0.5f, Color.white, 1f, "GeminiPick").Pop().Spin(240f).FadeFrom(0.5f);
        }
    }

    void Update()
    {
        float now = Time.time;
        if (now >= _end)
        {
            Destroy(gameObject);
            return;
        }
        bool flicker = now < _flickerUntil;
        bool on = !flicker || Mathf.FloorToInt(now / 0.1f) % 2 == 0;
        foreach (var s in _slots)
        {
            if (s.Tag != null) s.Tag.enabled = on;
            if (s.Num != null) s.Num.gameObject.SetActive(on);
            bool edge = flicker && on;
            if (s.EdgeL != null) s.EdgeL.enabled = edge;
            if (s.EdgeR != null) s.EdgeR.enabled = edge;
            if (s.EdgeT != null) s.EdgeT.enabled = edge;
        }
    }
}
