using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】物が文字にくずれて、上へばらばらに飛んで消える演出（2026-11-18：rm -rf で床がくずれる・分身が消える。docs/app-boss-skills-design.md 4-1・4-3）。
/// 当たり判定は無い。持ち主の AttackRunner に出した物として登録するので、形態変化・正常化で消える
/// </summary>
public class TerminalGlyphBurst : CustomEffect
{
    class Bit
    {
        public Transform T;
        public TextMesh Text;
        public Vector2 Velocity;
    }

    readonly List<Bit> _bits = new List<Bit>();
    Color _color;
    float _start;
    float _life;

    /// <summary>
    /// 文字を count 個出す。center・area（横×縦のマス）の範囲のランダムな位置から、上へ飛ばす。
    /// height は文字の高さ（マス）、life は消えるまでの秒数
    /// </summary>
    public static TerminalGlyphBurst Spawn(AttackRunner owner, Vector2 center, Vector2 area, int count, Color color,
        string[] glyphs, float height, float life, int sortingOrder)
    {
        if (owner == null || glyphs == null || glyphs.Length == 0) return null;
        GameObject go = owner.CreateSpawnedObject("TerminalGlyphBurst", center);
        var burst = go.AddComponent<TerminalGlyphBurst>();
        burst._color = color;
        burst._start = Time.time;
        burst._life = Mathf.Max(0.05f, life);
        for (int i = 0; i < count; i++)
        {
            string g = glyphs[Random.Range(0, glyphs.Length)];
            TextMesh tm = WorldText.Create(go.transform, g, height, color, sortingOrder, true, TextAnchor.MiddleCenter, "Glyph");
            tm.richText = false;
            tm.transform.localPosition = new Vector3(Random.Range(-0.5f, 0.5f) * area.x, Random.Range(-0.5f, 0.5f) * area.y, 0f);
            burst._bits.Add(new Bit
            {
                T = tm.transform,
                Text = tm,
                Velocity = new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(1.5f, 3.8f)),
            });
        }
        return burst;
    }

    void Update()
    {
        float t = (Time.time - _start) / _life;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        float dt = Time.deltaTime;
        Color c = _color;
        c.a = _color.a * (1f - t * t);
        foreach (var b in _bits)
        {
            if (b.T == null) continue;
            b.T.localPosition += (Vector3)(b.Velocity * dt);
            WorldText.SetColor(b.Text, c);
        }
    }
}
