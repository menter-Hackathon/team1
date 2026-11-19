using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ふたごの影（2026-11-19。Gemini のふたご座。docs/app-kits-1-design.md 7章）。担当3 の物。
/// ボスの絵を左右反転して、青紫のうすい影にした物。うすく現れて、時間が来たら消える。判定は無い（触っても痛くない）。
/// CustomEffect なので、形態変化（50％）・正常化で消える
/// </summary>
public class GeminiTwinShadow : CustomEffect
{
    const float FadeIn = 0.5f;
    const float FadeOut = 0.25f;
    const float Alpha = 0.55f;

    readonly List<SpriteRenderer> _look = new List<SpriteRenderer>();
    float _start;
    float _end;
    Rect _body;
    float _nextSpark;

    public static GeminiTwinShadow Spawn(AttackRunner owner, Vector2 rootPosition, float seconds)
    {
        if (owner == null) return null;
        GameObject go = owner.CreateSpawnedObject("GeminiTwin", rootPosition);
        var t = go.AddComponent<GeminiTwinShadow>();
        t._start = Time.time;
        t._end = Time.time + Mathf.Max(0.2f, seconds);
        Color tint = Color.Lerp(GeminiArt.Blue, GeminiArt.Purple, 0.6f);
        tint.a = 1f;
        t._look.AddRange(GeminiArt.CloneShadow(owner.transform, go.transform, tint, true));
        Bounds b = GeminiArt.BodyOf(owner);
        Vector2 off = (Vector2)(b.center - owner.transform.position);
        off.x = -off.x;
        t._body = new Rect(rootPosition + off - (Vector2)b.extents, b.size);
        t.Apply(0f);
        // 現れる瞬間、ボスから影へ星が飛ぶ
        FxAnim.Sprite(GeminiArt.Star4(9), b.center, 33, 0.25f, Color.white, 1f, "GeminiTwinStar")
            .Velocity(((Vector2)t._body.center - (Vector2)b.center) / 0.25f).Spin(540f).FadeFrom(0.8f);
        return t;
    }

    void Update()
    {
        float now = Time.time;
        if (now >= _end)
        {
            Destroy(gameObject);
            return;
        }
        float a = Mathf.Clamp01((now - _start) / FadeIn) * Mathf.Clamp01((_end - now) / FadeOut);
        Apply(a);
        if (now >= _nextSpark)
        {
            _nextSpark = now + 0.18f;
            var p = new Vector2(Random.Range(_body.xMin, _body.xMax), Random.Range(_body.yMin, _body.yMax));
            FxAnim.Sprite(GeminiArt.Star4(5), p, 32, 0.3f, Color.white, 1f, "GeminiTwinSpark").Rise(0.4f).FadeFrom(0.3f);
        }
    }

    void Apply(float a)
    {
        // ゆらゆら（うすい影）
        float wobble = 0.85f + 0.15f * Mathf.Sin(Time.time * 10f);
        foreach (var sr in _look)
        {
            if (sr == null) continue;
            Color c = sr.color;
            c.a = Alpha * a * wobble;
            sr.color = c;
        }
    }
}
