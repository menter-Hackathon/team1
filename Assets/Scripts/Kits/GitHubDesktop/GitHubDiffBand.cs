using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】diff の行の帯（1回分。2026-11-19）。GitHubDiffAttack が出し、あとは自分で進む。
/// 待つ → 予告（緑の + と赤の - の帯。赤は終わりぎわに速く点滅）→ 赤の帯に判定（取り消し線が左から右へ）→ うすくなって消える。
/// ボスの形態変化・正常化で消える（CustomEffect）。
/// </summary>
public class GitHubDiffBand : CustomEffect
{
    const int Order = 16;
    const float FadeTime = 0.2f;

    /// <summary>帯1つ（範囲はワールド座標）</summary>
    public struct Band
    {
        public Rect Area;
        public bool Red;
        /// <summary>0＝横いっぱい、1＝左の半分、2＝右の半分</summary>
        public int Side;
    }

    class View
    {
        public Band Band;
        public SpriteRenderer Fill;
        public SpriteRenderer EdgeTop;
        public SpriteRenderer EdgeBottom;
        public SpriteRenderer Strike;
        public TextMesh Label;
        public Hitbox Hit;
    }

    AttackRunner _owner;
    AttackData _data;
    List<Band> _bands;
    readonly List<View> _views = new List<View>();
    float _start;
    float _telegraph;
    float _hitTime;
    int _index;
    bool _built;
    bool _fired;

    public static GitHubDiffBand Spawn(AttackRunner owner, AttackData data, List<Band> bands, float delay, float telegraph, float hitTime, int index)
    {
        if (owner == null || data == null || bands == null) return null;
        Rect arena = BattleArena.GetBounds();
        GameObject go = owner.CreateSpawnedObject("GitHubDiff", new Vector2(arena.center.x, arena.yMin));
        var fx = go.AddComponent<GitHubDiffBand>();
        fx._owner = owner;
        fx._data = data;
        fx._bands = bands;
        fx._start = Time.time + Mathf.Max(0f, delay);
        fx._telegraph = Mathf.Max(0.8f, telegraph);
        fx._hitTime = Mathf.Max(0.05f, hitTime);
        fx._index = index;
        return fx;
    }

    void Build()
    {
        _built = true;
        int line = 10 + _index * 7 + Random.Range(0, 5);
        foreach (var b in _bands)
        {
            var v = new View { Band = b };
            Vector2 c = (Vector2)transform.InverseTransformPoint(b.Area.center);
            Vector2 size = b.Area.size;
            Color fill = b.Red ? GitHubDesktopArt.A(GitHubDesktopArt.DiffRed, 0.32f) : GitHubDesktopArt.A(GitHubDesktopArt.Green, 0.22f);
            Color edge = b.Red ? GitHubDesktopArt.Danger : GitHubDesktopArt.Green;
            v.Fill = GitHubDesktopArt.Box(transform, c, size, fill, Order, "Fill");
            float t = 2f * GitHubDesktopArt.Px;
            v.EdgeTop = GitHubDesktopArt.Box(transform, c + new Vector2(0f, size.y * 0.5f - t * 0.5f), new Vector2(size.x, t), edge, Order + 1, "EdgeTop");
            v.EdgeBottom = GitHubDesktopArt.Box(transform, c + new Vector2(0f, -size.y * 0.5f + t * 0.5f), new Vector2(size.x, t), edge, Order + 1, "EdgeBottom");
            // 左のふちに + ・ - と行番号（右の半分の帯は、右の半分の左のふちに）
            string text = (b.Red ? "- " : "+ ") + line;
            Vector2 labelPos = new Vector2(c.x - size.x * 0.5f + 0.35f, c.y);
            v.Label = GitHubDesktopArt.Text(transform, text, labelPos, 0.45f, b.Red ? GitHubDesktopArt.Danger : Color.white, Order + 2, TextAnchor.MiddleLeft);
            line++;
            if (b.Red)
            {
                v.Strike = GitHubDesktopArt.Box(transform, new Vector2(c.x - size.x * 0.5f, c.y), new Vector2(0f, 3f * GitHubDesktopArt.Px), GitHubDesktopArt.Danger, Order + 3, "Strike");
                v.Hit = GitHubDesktopArt.AddHit(_owner, _data, transform, c, size);
            }
            _views.Add(v);
        }
    }

    void Update()
    {
        float e = Time.time - _start;
        if (e < 0f) return;
        if (!_built) Build();
        if (e < _telegraph)
        {
            float left = _telegraph - e;
            foreach (var v in _views)
            {
                if (!v.Band.Red) continue;
                bool on = GitHubDesktopArt.Blink(left, 0.14f, 0.05f);
                if (v.Fill != null) v.Fill.enabled = on;
                if (v.EdgeTop != null) v.EdgeTop.enabled = on || left > 0.3f;
                if (v.EdgeBottom != null) v.EdgeBottom.enabled = on || left > 0.3f;
            }
            return;
        }
        float h = e - _telegraph;
        if (h < _hitTime)
        {
            if (!_fired)
            {
                _fired = true;
                foreach (var v in _views)
                {
                    if (!v.Band.Red) continue;
                    if (v.Hit != null) v.Hit.Activate();
                    if (v.Fill != null)
                    {
                        v.Fill.enabled = true;
                        v.Fill.color = GitHubDesktopArt.A(GitHubDesktopArt.DiffRed, 0.7f);
                    }
                }
            }
            // 取り消し線が左から右へ
            float k = Mathf.Clamp01(h / _hitTime);
            foreach (var v in _views)
            {
                if (v.Strike == null) continue;
                Vector2 c = (Vector2)transform.InverseTransformPoint(v.Band.Area.center);
                float w = v.Band.Area.width * k;
                GitHubDesktopArt.SetBox(v.Strike, new Vector2(c.x - v.Band.Area.width * 0.5f + w * 0.5f, c.y), new Vector2(w, 3f * GitHubDesktopArt.Px));
            }
            return;
        }
        if (_fired)
        {
            _fired = false;
            foreach (var v in _views) if (v.Hit != null) v.Hit.Deactivate();
        }
        float f = (h - _hitTime) / FadeTime;
        if (f >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
        {
            Color c = sr.color;
            c.a = Mathf.Min(c.a, 1f - f);
            sr.color = c;
        }
    }
}
