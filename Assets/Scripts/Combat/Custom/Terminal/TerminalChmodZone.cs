using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】chmod 000 player の「アクセス禁止エリア」の絵（2026-11-18 まさぴ〜の指示で変更：鍵の弾ではなく、エリアにいると封印。docs/app-boss-skills-design.md 4-2）。
/// ・幅5マスの縦の帯（床から天井まで）。中はうすい赤、左右のふちに赤い鎖が天井から下りてくる。帯の上に開いた南京錠
/// ・鎖が床に着く瞬間が判定（判定そのものは TerminalChmodAttack がする）。中にいたら南京錠が閉まって白く光り、外なら帯が砕けて消える
/// 判定は持たない（絵だけ）。形態変化・正常化で消える（CustomEffect）
/// </summary>
public class TerminalChmodZone : CustomEffect
{
    const float LinkStep = 0.28f;     // 鎖の輪の間
    const float LinkSize = 0.2f;      // 鎖の輪の大きさ
    const float FillAlpha = 0.16f;
    const float LockTopInset = 2.3f;  // 天井から南京錠まで（HUD に隠れないように）
    const int FillOrder = 6, ChainOrder = 7, LockOrder = 30;

    class Band
    {
        public float CenterX;
        public SpriteRenderer Fill;
        public SpriteRenderer Lock;
        public readonly List<SpriteRenderer> Links = new List<SpriteRenderer>();
    }

    readonly List<Band> _bands = new List<Band>();
    float _start, _telegraph, _fadeTime, _resolvedAt = -1f;
    bool _caught;
    float _floor, _ceiling;

    /// <summary>帯を出す。centers は帯の真ん中の x（部屋の外に出る分は、そのまま外にはみ出す）</summary>
    public static TerminalChmodZone Spawn(AttackRunner owner, IReadOnlyList<float> centers, float width, float telegraph, float fadeTime)
    {
        if (owner == null || centers == null || centers.Count == 0) return null;
        GameObject go = owner.CreateSpawnedObject("TerminalChmodZone", Vector2.zero);
        var zone = go.AddComponent<TerminalChmodZone>();
        zone.Build(centers, width, telegraph, fadeTime);
        return zone;
    }

    void Build(IReadOnlyList<float> centers, float width, float telegraph, float fadeTime)
    {
        Rect arena = BattleArena.GetBounds();
        _floor = arena.yMin;
        _ceiling = arena.yMax;
        _start = Time.time;
        _telegraph = Mathf.Max(0.05f, telegraph);
        _fadeTime = Mathf.Max(0.05f, fadeTime);
        float height = _ceiling - _floor;
        float midY = (_floor + _ceiling) * 0.5f;
        foreach (float cx in centers)
        {
            var band = new Band { CenterX = cx };
            band.Fill = TerminalArt.Box(transform, new Vector2(cx, midY), new Vector2(width, height), TerminalArt.WithAlpha(TerminalArt.Red, FillAlpha), FillOrder, "Fill");
            // 左右のふちの鎖（上から順に見せる）
            int links = Mathf.Max(1, Mathf.CeilToInt(height / LinkStep));
            foreach (float edge in new[] { cx - width * 0.5f, cx + width * 0.5f })
            {
                for (int i = 0; i < links; i++)
                {
                    float y = _ceiling - LinkStep * (i + 0.5f);
                    Color c = i % 2 == 0 ? TerminalArt.Red : TerminalArt.WithAlpha(TerminalArt.White, 0.85f);
                    var link = TerminalArt.Box(transform, new Vector2(edge, y), new Vector2(i % 2 == 0 ? LinkSize : LinkSize * 0.5f, LinkStep * 0.8f), c, ChainOrder, "Link");
                    link.enabled = false;
                    band.Links.Add(link);
                }
            }
            band.Lock = RuntimeSprites.Show(transform, TerminalArt.LockOpen, new Vector2(cx, _ceiling - LockTopInset), LockOrder, 1.4f, "Lock");
            _bands.Add(band);
        }
    }

    /// <summary>判定の結果を見せる。caught＝プレイヤーが中にいた（南京錠が閉まる）。外なら帯が砕けて消える</summary>
    public void Resolve(bool caught)
    {
        if (_resolvedAt >= 0f) return;
        _resolvedAt = Time.time;
        _caught = caught;
        foreach (var b in _bands)
        {
            if (b.Lock != null) b.Lock.sprite = caught ? TerminalArt.LockClosed : TerminalArt.LockOpen;
        }
        if (!caught) TerminalCameraShake.Shake(0.15f, 0.05f);
    }

    void Update()
    {
        float now = Time.time;
        if (_resolvedAt < 0f)
        {
            // 予告：鎖が天井から床へ下りていく。中のうすい赤は、判定が近づくほど点滅が速くなる
            float k = Mathf.Clamp01((now - _start) / _telegraph);
            foreach (var b in _bands)
            {
                int shown = Mathf.CeilToInt(b.Links.Count * 0.5f * k);
                int perEdge = b.Links.Count / 2;
                for (int i = 0; i < b.Links.Count; i++) b.Links[i].enabled = (i % perEdge) < shown;
                float blink = k > 0.6f && Mathf.FloorToInt(now / 0.08f) % 2 == 0 ? 1.8f : 1f;
                b.Fill.color = TerminalArt.WithAlpha(TerminalArt.Red, FillAlpha * blink);
            }
            return;
        }

        // 結果：中にいたら白く光ってから消える。外なら砕ける（鎖が落ち、赤が薄れる）
        float t = Mathf.Clamp01((now - _resolvedAt) / _fadeTime);
        foreach (var b in _bands)
        {
            Color fill = _caught && t < 0.4f ? TerminalArt.WithAlpha(TerminalArt.White, 0.35f * (1f - t / 0.4f)) : TerminalArt.WithAlpha(TerminalArt.Red, FillAlpha * (1f - t));
            b.Fill.color = fill;
            foreach (var l in b.Links)
            {
                if (!_caught) l.transform.localPosition += Vector3.down * (6f * Time.deltaTime);
                Color c = l.color;
                c.a = 1f - t;
                l.color = c;
            }
            if (b.Lock != null)
            {
                Color c = b.Lock.color;
                c.a = _caught ? 1f : 1f - t;
                b.Lock.color = c;
            }
        }
        if (t >= 1f) Destroy(gameObject);
    }
}
