using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】sudo rm -rf /floor の進行（2026-11-18。docs/app-boss-skills-design.md 4-1）。TerminalRmRfAttack が出す。
/// 区画ごとに「赤く点滅 → 文字にくずれる → 文字化けの床（触れている間ライフ -1）」と進み、時間が来たら全部いっしょに戻る。
/// ・床の当たり判定は消さない（穴をあけない）。文字化けの床は、床の上に重ねた判定（Hitbox、触れている間ずっと当たる）
/// ・見た目：点滅は床の上の線の赤と、うすい赤の帯。くずれると床の絵を黒でかくし、赤と白の小さい文字が 0.1秒ごとに入れ替わる帯になる
/// ・形態変化・正常化・技の途中で止められたときは、この GameObject ごと消えるので、床はすぐ元に戻る
/// 出ている間：場に残る技の効果（Lingers）・床が文字化けしている（CorruptsFloor）
/// </summary>
public class TerminalRmRfEffect : CustomEffect
{
    const float GlyphCell = 0.4f;       // 【仮】文字化けの床の字のマス目（マス）
    const float CoverDepth = 1.2f;      // 床の絵を隠す黒の深さ（床の上面から下へ）
    const float SwapInterval = 0.1f;    // 文字が入れ替わる間隔
    const float BlinkInterval = 0.1f;
    const int CoverOrder = 3;           // 床（1）・足場（2）より手前
    const int BandOrder = 4;
    const int LineOrder = 5;
    const int GlyphOrder = 7;

    class Section
    {
        public int Order;
        public float CenterX;
        public float Width;
        public Transform Root;
        public SpriteRenderer Line;
        public SpriteRenderer Band;
        public SpriteRenderer Cover;
        public readonly List<TextMesh> Glyphs = new List<TextMesh>();
        public Hitbox Hit;
        public bool Crumbled;
        public bool Corrupted;
    }

    public override bool Lingers => true;
    public override bool CorruptsFloor => true;

    AttackRunner _owner;
    AttackData _data;
    TerminalRmRfAttack _cfg;
    float _start;
    float _floorY;
    float _nextSwap;
    bool _hitsOff;
    readonly List<Section> _sections = new List<Section>();

    public static TerminalRmRfEffect Spawn(AttackRunner owner, AttackData data, TerminalRmRfAttack cfg, bool fromRight)
    {
        if (owner == null || data == null || cfg == null) return null;
        Rect arena = BattleArena.GetBounds();
        GameObject go = owner.CreateSpawnedObject("TerminalRmRf", new Vector2(arena.center.x, arena.yMin));
        var fx = go.AddComponent<TerminalRmRfEffect>();
        fx._owner = owner;
        fx._data = data;
        fx._cfg = cfg;
        fx._start = Time.time;
        fx._floorY = arena.yMin;

        int count = Mathf.Max(1, cfg.SectionCount);
        float w = arena.width / count;
        for (int k = 0; k < cfg.ErasedSections; k++)
        {
            int index = fromRight ? count - 1 - k : k;
            fx._sections.Add(fx.BuildSection(k, arena.xMin + w * (index + 0.5f), w));
        }
        return fx;
    }

    Section BuildSection(int order, float centerX, float width)
    {
        float band = _cfg.BandHeight;
        var root = new GameObject($"Section{order}").transform;
        root.gameObject.layer = gameObject.layer;
        root.SetParent(transform, false);
        root.position = new Vector3(centerX, _floorY, 0f);

        var s = new Section { Order = order, CenterX = centerX, Width = width, Root = root };
        // 点滅：床の上の線（2ドット）と、うすい赤の帯（文字化けになる範囲）
        s.Line = TerminalArt.Box(root, new Vector2(0f, -TerminalArt.Pixel), new Vector2(width, TerminalArt.Pixel * 2f), TerminalArt.Red, LineOrder, "Line");
        s.Band = TerminalArt.Box(root, new Vector2(0f, band * 0.5f), new Vector2(width, band), TerminalArt.WithAlpha(TerminalArt.Red, 0.25f), BandOrder, "Band");
        // 床の絵を黒でかくす
        s.Cover = TerminalArt.Box(root, new Vector2(0f, -CoverDepth * 0.5f), new Vector2(width, CoverDepth), TerminalArt.Black, CoverOrder, "Cover");
        s.Line.enabled = s.Band.enabled = s.Cover.enabled = false;

        // 判定：区画の幅 × 床から高さ band。触れている間ずっと当たる（連続で減らないのは、プレイヤーの無敵時間のおかげ）
        var hit = new GameObject("Hit");
        hit.layer = gameObject.layer;
        hit.transform.SetParent(root, false);
        hit.transform.localPosition = new Vector3(0f, band * 0.5f, 0f);
        var rb = hit.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var box = hit.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(width, band);
        s.Hit = _owner.AddHitbox(hit, _data, box, false);
        return s;
    }

    void Update()
    {
        float t = Time.time - _start;
        float now = Time.time;
        bool blinkOn = Mathf.FloorToInt(now / BlinkInterval) % 2 == 0;
        float whiteAt = _cfg.RestoreAt - _cfg.RestoreBlinkTime;

        foreach (var s in _sections)
        {
            if (t < _cfg.BlinkAt(s.Order)) continue;
            if (t < _cfg.CrumbleAt(s.Order))
            {
                // 赤く点滅（文字化けになる範囲の予告）
                s.Line.enabled = blinkOn;
                s.Band.enabled = blinkOn;
                continue;
            }
            if (!s.Crumbled)
            {
                // 床のドットが、緑の英数字になって上へ飛んで消える
                s.Crumbled = true;
                s.Line.enabled = false;
                s.Band.enabled = false;
                s.Cover.enabled = true;
                TerminalGlyphBurst.Spawn(_owner, new Vector2(s.CenterX, _floorY - 0.15f), new Vector2(s.Width, 0.3f), 12,
                    TerminalArt.Green, TerminalArt.CrumbleGlyphs, 6f / 18f, 0.6f, GlyphOrder);
            }
            if (t < _cfg.CorruptAt(s.Order)) continue;
            if (!s.Corrupted)
            {
                s.Corrupted = true;
                BuildGlyphs(s);
                s.Band.color = TerminalArt.WithAlpha(TerminalArt.Red, 0.18f);
                s.Band.enabled = true;
                if (!_hitsOff) s.Hit.Activate();
            }
        }

        // 戻る前の白い点滅の間は、もう当たらない
        if (t >= whiteAt && !_hitsOff)
        {
            _hitsOff = true;
            foreach (var s in _sections)
            {
                if (s.Hit != null) s.Hit.Deactivate();
            }
        }

        bool white = t >= whiteAt;
        if (white || now >= _nextSwap)
        {
            bool swap = now >= _nextSwap;
            if (swap) _nextSwap = now + SwapInterval;
            string[] glyphs = TerminalArt.CorruptGlyphs;
            foreach (var s in _sections)
            {
                if (!s.Corrupted) continue;
                s.Cover.color = white && blinkOn ? TerminalArt.White : TerminalArt.Black;
                foreach (var g in s.Glyphs)
                {
                    if (g == null) continue;
                    if (swap) g.text = glyphs[Random.Range(0, glyphs.Length)];
                    if (white) WorldText.SetColor(g, blinkOn ? TerminalArt.Black : TerminalArt.White);
                    else if (swap) WorldText.SetColor(g, Random.value < 0.6f ? TerminalArt.Red : TerminalArt.White);
                }
            }
        }

        if (t >= _cfg.RestoreAt) Destroy(gameObject);
    }

    /// <summary>文字化けの床の字（赤と白の小さい字）を、区画の幅 × 帯の高さに並べる</summary>
    void BuildGlyphs(Section s)
    {
        float band = _cfg.BandHeight;
        int cols = Mathf.Max(1, Mathf.RoundToInt(s.Width / GlyphCell));
        int rows = Mathf.Max(1, Mathf.RoundToInt(band / GlyphCell));
        float cw = s.Width / cols, ch = band / rows;
        string[] glyphs = TerminalArt.CorruptGlyphs;
        for (int j = 0; j < rows; j++)
        {
            for (int i = 0; i < cols; i++)
            {
                string g = glyphs[Random.Range(0, glyphs.Length)];
                Color c = Random.value < 0.6f ? TerminalArt.Red : TerminalArt.White;
                TextMesh tm = WorldText.Create(s.Root, g, Mathf.Min(cw, ch) * 0.8f, c, GlyphOrder, true, TextAnchor.MiddleCenter, "Glyph");
                tm.richText = false;
                tm.transform.localPosition = new Vector3(-s.Width * 0.5f + cw * (i + 0.5f), ch * (j + 0.5f), 0f);
                s.Glyphs.Add(tm);
            }
        }
    }
}
