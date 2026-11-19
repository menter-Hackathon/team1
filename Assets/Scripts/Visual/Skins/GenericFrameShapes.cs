using System.Collections.Generic;
using UnityEngine;
using K = GenericFrameKit;

// 【仮】Generic のスキンの枠の絵（2026-11-18。docs/app-skill-visuals-design.md 10章の上の表と、1-7 の Generic の行）。
// 何をまねるか：Mac そのもの（Dock で跳ねるアイコン・通知の板・赤いバッジ・ウィンドウの信号機）。そのアプリのアイコンと色をそのまま使う。
// 敵＝赤いふち・赤い予告、プレイヤー＝白いふち・アプリの色（0-3）。当たり判定・秒数は変えない（絵だけ）。
// ワープ（最小化）は作っていない（持ち主の体を動かす枠なので、今の見た目のまま）。
// 2026-11-19：見た目を Mac に揃えた（docs/mac-look-design.md 2-4 の 31〜35）。通知は明るい地、落下物は Mac のウィンドウ、
// 上の列の予告はメニューバー、窓の予告は進行の輪。Mac の部品は GenericMac（担当1 の MacUi の入口。MacUi に無い板・閉じる動きだけ足した）。

/// <summary>【仮】板の見た目（2026-11-19）：Banner＝Mac の通知（Generic）、Window＝Mac のウィンドウ（Finder）、Panel＝今の板（Creative・AI・Media の中身）</summary>
public enum GenericPlateLook { Panel, Banner, Window }

/// <summary>【仮】Generic の枠の絵を選ぶ（ほかのスキンの「無い枠」の代わりにも使う）</summary>
public static class GenericFrameDresser
{
    public static bool Dress(AttackFrame frame, DressPhase phase, DressInfo info)
    {
        if (info.Target == null) return false;
        switch (frame)
        {
            case AttackFrame.SmallShot:
                if (K.IsVolleyTelegraph(info))
                {
                    // 予備動作の物（攻撃する人の子）：予告だけ描く。消えるは何もしない（すぐ消される）
                    if (phase != DressPhase.Telegraph) return true;
                    Sprite icon = K.IconOf(info.AppIcon, K.Main(info.Theme, info.AppIcon));
                    Color edge = info.IsEnemy ? K.Red : Color.white;
                    return GenericFrameVolley.Show(info, K.VolleyCount(info, true), 0.8f, true, (slot, i) =>
                    {
                        SpriteRenderer e = K.Spr(slot, K.RoundRect(14, 14, 2), Vector2.zero, edge, K.OrderDeco, 1f, "Edge");
                        K.IconAt(slot, icon, Vector2.zero, 12, K.OrderDeco + 1);
                        return e;
                    }, K.VolleyRows(info)) != null;
                }
                return GenericFrameFx.Apply<GenericFxSmallShot>(frame, phase, info);
            case AttackFrame.LongShot:
                if (K.IsVolleyTelegraph(info))
                {
                    if (phase != DressPhase.Telegraph) return true;
                    int n = K.VolleyCount(info, false);
                    return GenericFrameVolley.Show(info, n, 0.5f, false, (slot, i) => GenericFrameBits.Badge(slot, Vector2.zero, K.Red, (n - i).ToString(), K.OrderDeco)) != null;
                }
                return GenericFrameFx.Apply<GenericFxLongShot>(frame, phase, info);
            case AttackFrame.Thrown:
                if (K.IsVolleyTelegraph(info)) return false; // 投げ物の予備動作は今のまま
                return GenericFrameFx.Apply<GenericFxThrown>(frame, phase, info);
            case AttackFrame.Pillar: return GenericFrameFx.Apply<GenericFxPillar>(frame, phase, info);
            case AttackFrame.Floor: return GenericFrameFx.Apply<GenericFxFloor>(frame, phase, info);
            case AttackFrame.Paint: return GenericFrameFx.Apply<GenericFxPaint>(frame, phase, info);
            case AttackFrame.Circle: return GenericFrameFx.Apply<GenericFxCircle>(frame, phase, info);
            case AttackFrame.Window: return GenericFrameFx.Apply<GenericFxWindow>(frame, phase, info);
            case AttackFrame.Falling: return GenericFrameFx.Apply<GenericFxFalling>(frame, phase, info);
            case AttackFrame.TopRow: return GenericFrameFx.Apply<GenericFxTopRow>(frame, phase, info);
            default: return false; // ワープなど：今の見た目のまま
        }
    }
}

/// <summary>【仮】Generic の小さい部品（はじける・バッジ・残像）</summary>
public static class GenericFrameBits
{
    static readonly string[] Digit1 = { ".X.", "XX.", ".X.", ".X.", "XXX" };

    /// <summary>赤いバッジ（7ドットの丸に白い字）。数字が1字なら字、それ以外は白い点</summary>
    public static SpriteRenderer Badge(Transform parent, Vector2 localPos, Color color, string digit, int order)
    {
        SpriteRenderer disc = K.Spr(parent, K.Disc(7), localPos, color, order, 1f, "Badge");
        Sprite glyph = DigitSprite(digit);
        K.Spr(parent, glyph, localPos, Color.white, order + 1, 1f, "BadgeText");
        return disc;
    }

    /// <summary>3×5 の数字（0〜9。それ以外は点）</summary>
    public static Sprite DigitSprite(string digit)
    {
        string[] rows;
        switch (digit)
        {
            case "1": rows = Digit1; break;
            case "2": rows = new[] { "XX.", "..X", ".X.", "X..", "XXX" }; break;
            case "3": rows = new[] { "XX.", "..X", ".X.", "..X", "XX." }; break;
            case "4": rows = new[] { "X.X", "X.X", "XXX", "..X", "..X" }; break;
            case "5": rows = new[] { "XXX", "X..", "XX.", "..X", "XX." }; break;
            case "6": rows = new[] { ".XX", "X..", "XXX", "X.X", "XXX" }; break;
            case "7": rows = new[] { "XXX", "..X", ".X.", ".X.", ".X." }; break;
            case "8": rows = new[] { "XXX", "X.X", "XXX", "X.X", "XXX" }; break;
            case "9": rows = new[] { "XXX", "X.X", "XXX", "..X", "XX." }; break;
            case "0": rows = new[] { "XXX", "X.X", "X.X", "X.X", "XXX" }; break;
            default: rows = new[] { "...", "...", ".X.", "...", "..." }; break;
        }
        return K.Glyph("Digit_" + (digit ?? "dot"), rows);
    }

    /// <summary>はじける（絵の写しが大きくなりながら消え、輪が広がり、かけらが飛ぶ）。icon が無ければ輪とかけらだけ</summary>
    public static void Pop(Vector2 at, Sprite icon, int iconPx, Color edge, Color tint, int shards = 4)
    {
        Transform root = K.Loose("Gen4_Pop", at);
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(0.2f);
        if (icon != null)
        {
            SpriteRenderer copy = K.IconAt(root, icon, Vector2.zero, iconPx, K.OrderSpark);
            Vector3 s = copy.transform.localScale;
            tw.Scale(copy.transform, s, s * 1.7f, 0.12f).Alpha(copy, 1f, 0f, 0.12f);
        }
        SpriteRenderer ring = K.Spr(root, K.Ring(12, 1), Vector2.zero, edge, K.OrderSpark + 1);
        tw.Scale(ring.transform, Vector3.one * 0.5f, Vector3.one * 2f, 0.15f).Alpha(ring, 1f, 0f, 0.15f);
        K.Shards(at, Vector2.up, shards, i => null, i => i % 2 == 0 ? tint : edge, 80f, 0.8f);
    }

    /// <summary>閉じる（横にしぼむ）</summary>
    public static void Squeeze(Transform detached, float dur)
    {
        if (detached == null) return;
        var tw = GenericFrameTweens.Of(detached.gameObject);
        Vector3 s = detached.localScale;
        tw.Scale(detached, s, new Vector3(0.05f, s.y * 1.1f, 1f), dur, 0f, GenericFrameTweens.Ease.InQuad);
    }
}

/// <summary>【仮】少し前の位置を覚える（残像用）</summary>
public class GenericFrameTrail
{
    struct Sample
    {
        public float T;
        public Vector3 P;
    }

    readonly List<Sample> _samples = new List<Sample>();

    public void Record(Vector3 p)
    {
        float now = Time.time;
        _samples.Add(new Sample { T = now, P = p });
        while (_samples.Count > 2 && _samples[1].T < now - 0.3f) _samples.RemoveAt(0);
    }

    /// <summary>ago 秒前の位置（覚えていなければ一番古い位置）</summary>
    public Vector3 Ago(float ago, Vector3 fallback)
    {
        if (_samples.Count == 0) return fallback;
        float t = Time.time - ago;
        for (int i = _samples.Count - 1; i > 0; i--)
        {
            if (_samples[i - 1].T <= t)
            {
                Sample a = _samples[i - 1], b = _samples[i];
                float k = b.T > a.T ? Mathf.Clamp01((t - a.T) / (b.T - a.T)) : 1f;
                return Vector3.Lerp(a.P, b.P, k);
            }
        }
        return _samples[0].P;
    }
}

// ───────── 小弾：アプリのアイコン（回らずに飛び、後ろにうすい残像 2枚） ─────────

public class GenericFxSmallShot : GenericFrameFx
{
    Transform _body;
    readonly SpriteRenderer[] _ghosts = new SpriteRenderer[2];
    readonly GenericFrameTrail _trail = new GenericFrameTrail();
    int _px;

    protected override void Build()
    {
        Upright = true;
        // 敵の弾は判定より大きく見せない（はみ出しは 3ドットまで。0-3）
        _px = Info.IsEnemy ? Mathf.Clamp(Px(Mathf.Max(Size.x, Size.y)) + 1, 8, 12) : 12;
        _body = K.Node(Root, "Body", Vector2.zero);
        K.Spr(_body, K.RoundRect(_px + 2, _px + 2, 2), Vector2.zero, Edge, K.OrderCore, 1f, "Edge");
        K.IconAt(_body, Icon, Vector2.zero, _px, K.OrderCore + 1);
        for (int i = 0; i < 2; i++)
        {
            _ghosts[i] = K.IconAt(Root, Icon, Vector2.zero, _px, K.OrderCore - 1, i == 0 ? 0.5f : 0.2f, "Ghost");
        }
    }

    protected override void OnActive() => GenericFrameTweens.Of(gameObject).Pop(_body, Vector3.one, 0.08f);

    protected override void OnLanded()
    {
        GenericFrameBits.Pop(transform.position, Icon, _px, Edge, Tint);
        if (Root != null && !KeepsFlyingAfterLanding) Root.gameObject.SetActive(false);
    }

    protected override void OnVanish()
    {
        if (!IsLanded) GenericFrameBits.Pop(transform.position, Icon, _px, Edge, Tint);
        if (Root != null) Destroy(Root.gameObject);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (Root == null) return;
        _trail.Record(transform.position);
        _ghosts[0].transform.position = _trail.Ago(0.035f, transform.position);
        _ghosts[1].transform.position = _trail.Ago(0.07f, transform.position);
    }
}

// ───────── 長弾：通知の板（小。1-4）。明るい板、前にアプリの色の角の丸い四角、後ろに題名の棒（黒）と本文の棒（灰） ─────────

public class GenericFxLongShot : GenericFrameFx
{
    Transform _body, _shadow;

    protected override void Build()
    {
        FlipWhenLeft = true;
        _body = K.Node(Root, "Body", Vector2.zero);
        // 影は真下に出す（弾は向きで回る・左では上下を返すので、LateUpdate で毎回置き直す）
        _shadow = GenericMac.Shadow(_body, Vector2.zero, 22, 9, 2, K.OrderCore - 1).transform;
        GenericMac.Plate(_body, Vector2.zero, 22, 9, 2, GenericMac.NotifLight, Edge, K.OrderCore, false, out _);
        K.Spr(_body, K.RoundRect(5, 5, 1), new Vector2(6.5f, 0f) * K.Dot, Main, K.OrderCore + 1, 1f, "AppColor");
        K.BoxPx(_body, -2.5f, 1.5f, 11f, 1f, GenericMac.BannerInk, K.OrderCore + 1, "Bar");
        K.BoxPx(_body, -4f, -1.5f, 8f, 1f, GenericMac.Gray1, K.OrderCore + 1, "Bar");
        // 尾：うすい線
        K.BoxPx(_body, -16f, 0f, 8f, 1f, K.WithAlpha(Edge, 0.35f), K.OrderTrail, "Tail");
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (Root == null || _shadow == null || _body == null) return;
        _shadow.position = _body.position + Vector3.down * (GenericMac.ShadowDrop * K.Dot);
    }

    protected override void OnActive() => GenericFrameTweens.Of(gameObject).Pop(_body, Vector3.one, 0.08f);

    protected override void OnLanded()
    {
        GenericFrameBits.Pop(transform.position, null, 0, Edge, Main, 5);
        if (Root != null && !KeepsFlyingAfterLanding) Root.gameObject.SetActive(false);
    }

    protected override void OnVanish()
    {
        if (IsLanded)
        {
            if (Root != null) Destroy(Root.gameObject);
            return;
        }
        GenericFrameBits.Pop(transform.position, null, 0, Edge, Main, 3);
        FadeAway(0.08f);
    }
}

// ───────── 投げ物：アプリのアイコン（12ドット）。回る ─────────

public class GenericFxThrown : GenericFrameFx
{
    Transform _body;
    float _spin;

    protected override void Build()
    {
        _body = K.Node(Root, "Body", Vector2.zero);
        K.Spr(_body, K.RoundRect(14, 14, 2), Vector2.zero, Edge, K.OrderCore, 1f, "Edge");
        K.IconAt(_body, Icon, Vector2.zero, 12, K.OrderCore + 1);
        _spin = Info.Direction.x < 0f ? 720f : -720f;
    }

    protected override void OnActive() => GenericFrameTweens.Of(gameObject).Pop(_body, Vector3.one, 0.08f);

    protected override void OnLanded()
    {
        GenericFrameBits.Pop(transform.position, Icon, 12, Edge, Main);
        if (Root != null && !KeepsFlyingAfterLanding) Root.gameObject.SetActive(false);
    }

    protected override void OnVanish()
    {
        if (!IsLanded) GenericFrameBits.Pop(transform.position, Icon, 12, Edge, Main);
        if (Root != null) Destroy(Root.gameObject);
    }

    protected override void LateUpdate()
    {
        if (Root == null) return;
        Root.rotation = Quaternion.Euler(0f, 0f, _spin * (Time.time - PhaseStart));
    }
}

// ───────── 柱：Dock で跳ねるアイコン。床に起動中の印、アイコンが残像を引いて跳ね上がる ─────────

public class GenericFxPillar : GenericFrameFx
{
    float _w, _h;
    Color _tint;
    Transform _tele;
    SpriteRenderer _band, _dot;
    Transform _icon;
    readonly List<SpriteRenderer> _ghosts = new List<SpriteRenderer>();
    readonly List<float> _ghostY = new List<float>();
    int _iconPx;

    protected override void Build()
    {
        Vector2 s = Size;
        _w = s.x;
        _h = s.y;
        _tint = K.Step(Tint, Info.Index); // 連続ピラーは1本ごとに明るさを変える（1-7）
        _iconPx = Mathf.Clamp(Px(_w) - 4, 12, 24);
    }

    float Bottom => -_h * 0.5f;

    protected override void OnTelegraph()
    {
        _tele = K.Node(Root, "Telegraph", Vector2.zero);
        _band = K.Box(_tele, new Vector2(0f, Bottom), new Vector2(_w, 0.01f), K.WithAlpha(Edge, Info.IsEnemy ? 0.2f : 0.25f), K.OrderTelegraph, "Band");
        _dot = K.Spr(_tele, K.Disc(3), new Vector2(0f, Bottom + 2.5f * K.Dot), Edge, K.OrderTelegraph + 1, 1f, "Running");
        // 起動中の印の下に、Dock の棚のような横線
        K.Box(_tele, new Vector2(0f, Bottom + 0.5f * K.Dot), new Vector2(_w * 0.8f, K.Dot), K.WithAlpha(Edge, 0.6f), K.OrderTelegraph, "Shelf");
    }

    protected override void OnActive()
    {
        if (_tele != null) Destroy(_tele.gameObject);
        var tw = GenericFrameTweens.Of(gameObject);
        // 中（判定の中）：アプリの色の柱。左右にふち
        K.Box(Root, Vector2.zero, new Vector2(_w - 2f * K.Dot, _h), K.WithAlpha(_tint, 0.38f), K.OrderPillar, "Core");
        K.Box(Root, new Vector2(-_w * 0.5f + 0.5f * K.Dot, 0f), new Vector2(K.Dot, _h), Edge, K.OrderPillar + 1, "EdgeL");
        K.Box(Root, new Vector2(_w * 0.5f - 0.5f * K.Dot, 0f), new Vector2(K.Dot, _h), Edge, K.OrderPillar + 1, "EdgeR");
        // 残像の列
        float step = _iconPx * K.Dot * 1.15f;
        int n = Mathf.Max(1, Mathf.FloorToInt((_h - step * 0.5f) / step));
        for (int i = 0; i < n; i++)
        {
            float y = Bottom + step * (i + 0.5f);
            float a = Mathf.Lerp(0.15f, 0.55f, n > 1 ? i / (float)(n - 1) : 1f);
            SpriteRenderer g = K.IconAt(Root, Icon, new Vector2(0f, y), _iconPx, K.OrderPillar + 2, a, "Ghost");
            g.enabled = false;
            _ghosts.Add(g);
            _ghostY.Add(y);
        }
        // 跳ねるアイコン（ふち付き）
        _icon = K.Node(Root, "Icon", new Vector2(0f, Bottom));
        K.Spr(_icon, K.RoundRect(_iconPx + 2, _iconPx + 2, 3), Vector2.zero, Edge, K.OrderPillar + 3, 1f, "Edge");
        K.IconAt(_icon, Icon, Vector2.zero, _iconPx, K.OrderPillar + 4);
        // 出た瞬間の白い光
        SpriteRenderer flash = K.Box(Root, Vector2.zero, new Vector2(_w, _h), new Color(1f, 1f, 1f, 0.7f), K.OrderPillar + 5, "Flash");
        tw.Alpha(flash, 0.7f, 0f, 0.08f);
    }

    protected override void Tick(float t)
    {
        if (Phase == DressPhase.Telegraph)
        {
            if (_band == null) return;
            float h = _h * Mathf.Clamp01(t / 0.3f);
            _band.transform.localPosition = new Vector3(0f, Bottom + h * 0.5f, 0f);
            K.SetSize(_band, new Vector2(_w, Mathf.Max(0.01f, h)));
            bool on = K.BlinkOn(TelegraphTime - t);
            _band.enabled = on;
            _dot.enabled = on;
            return;
        }
        if (Phase != DressPhase.Active || _icon == null) return;
        float top = _h * 0.5f - (_iconPx * 0.5f + 2f) * K.Dot;
        float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.12f), 2f);
        float y = Mathf.Lerp(Bottom + _iconPx * 0.5f * K.Dot, top, rise);
        if (rise >= 1f) y += Mathf.Abs(Mathf.Sin(t * 18f)) * 2f * K.Dot; // 上で小さく跳ねる
        _icon.localPosition = new Vector3(0f, y, 0f);
        for (int i = 0; i < _ghosts.Count; i++) _ghosts[i].enabled = _ghostY[i] < y;
    }

    protected override void OnVanish()
    {
        Transform icon = _icon;
        float bottom = Bottom + _iconPx * 0.5f * K.Dot;
        Transform r = Detach(0.2f);
        if (r == null) return;
        var tw = GenericFrameTweens.Of(r.gameObject);
        foreach (var sr in r.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, sr.color.a, 0f, 0.16f);
        // アイコンは下へ落ちて消える
        if (icon != null) tw.Move(icon, icon.localPosition, new Vector3(0f, bottom, 0f), 0.15f, 0f, GenericFrameTweens.Ease.InQuad);
    }
}

// ───────── 床：赤いバッジの列。0.2秒ごとに1つずつ跳ねる ─────────

public class GenericFxFloor : GenericFrameFx
{
    readonly List<Transform> _badges = new List<Transform>();
    readonly List<SpriteRenderer> _discs = new List<SpriteRenderer>();
    readonly List<int> _column = new List<int>();
    readonly List<float> _baseY = new List<float>();
    int _columns;
    Color _badgeColor;
    bool _flash;

    protected override void Build()
    {
        Vector2 s = Size;
        int rows = s.y >= 0.8f ? 2 : 1; // トラップ弾の床（2.6×1）は2段に重ねる（1-7）
        _columns = Mathf.Max(2, Mathf.FloorToInt(s.x * 18f / 9f));
        float spacing = s.x / _columns;
        _badgeColor = Info.IsEnemy ? K.Red : Main;
        for (int r = 0; r < rows; r++)
        {
            for (int i = 0; i < _columns; i++)
            {
                float x = -s.x * 0.5f + spacing * (i + 0.5f) + (r == 1 ? spacing * 0.5f : 0f);
                if (r == 1 && i == _columns - 1) continue;
                float y = -s.y * 0.5f + (4f + r * 8f) * K.Dot;
                Transform b = K.Node(Root, "Badge", new Vector2(x, y));
                SpriteRenderer disc = GenericFrameBits.Badge(b, Vector2.zero, _badgeColor, "1", K.OrderFloor + 1);
                if (!Info.IsEnemy) K.Spr(b, K.Ring(9, 1), Vector2.zero, Color.white, K.OrderFloor, 1f, "Edge");
                b.localScale = Vector3.zero;
                _badges.Add(b);
                _discs.Add(disc);
                _column.Add(i);
                _baseY.Add(y);
            }
        }
        // バッジの下に、うすい線（判定の面）
        K.Box(Root, new Vector2(0f, -s.y * 0.5f + 0.5f * K.Dot), new Vector2(s.x, K.Dot), K.WithAlpha(_badgeColor, 0.5f), K.OrderFloor, "Line");
    }

    protected override void OnActive() => Show();

    void Show()
    {
        var tw = GenericFrameTweens.Of(gameObject);
        for (int i = 0; i < _badges.Count; i++)
        {
            if (_badges[i].localScale.x > 0.5f) continue;
            tw.Pop(_badges[i], Vector3.one, 0.1f, _column[i] * 0.025f);
        }
    }

    protected override void Tick(float t)
    {
        if (Phase != DressPhase.Active) return;
        int hop = Mathf.FloorToInt(t / 0.2f) % Mathf.Max(1, _columns);
        bool up = t % 0.2f < 0.1f;
        for (int i = 0; i < _badges.Count; i++)
        {
            if (_badges[i] == null) continue;
            Vector3 p = _badges[i].localPosition;
            p.y = _baseY[i] + (_column[i] == hop && up ? 2f * K.Dot : 0f);
            _badges[i].localPosition = p;
        }
        // 判定の瞬間（持続のヒット間隔）に白く光る
        AttackData d = Info.Data;
        bool flash = d != null && d.PersistDuration > 0f && d.TickInterval > 0f && (t % d.TickInterval) < 0.06f;
        if (flash != _flash)
        {
            _flash = flash;
            foreach (var disc in _discs) if (disc != null) disc.color = flash ? Color.white : _badgeColor;
        }
    }

    protected override void OnVanish()
    {
        // 左から消える
        var badges = new List<Transform>(_badges);
        var cols = new List<int>(_column);
        int columns = _columns;
        Transform r = Detach(0.2f);
        if (r == null) return;
        GenericFrameTweens.Of(r.gameObject).Each(0.15f, k =>
        {
            for (int i = 0; i < badges.Count; i++)
            {
                if (badges[i] != null && cols[i] < k * columns) badges[i].gameObject.SetActive(false);
            }
        });
    }
}

// ───────── 塗り：アプリの色の四角います目（市松）が、近い所から順にうまる ─────────

public class GenericFxPaint : GenericFrameFx
{
    readonly List<SpriteRenderer> _cells = new List<SpriteRenderer>();
    readonly List<Color> _colors = new List<Color>();
    readonly List<SpriteRenderer> _edges = new List<SpriteRenderer>();
    bool _pulse;
    float _fade = -1f;
    const float PaintAlpha = 0.62f; // 塗りの中の敵が見えるように、少しすかす

    static Sprite CellSprite => K.Art("Cell6", new[]
    {
        "wwwwww",
        "wwwwws",
        "wwwwws",
        "wwwwws",
        "wwwwws",
        "ssssss",
    }, new Dictionary<char, Color32> { { 'w', new Color32(255, 255, 255, 255) }, { 's', new Color32(196, 196, 196, 255) } });

    protected override void Build()
    {
        Vector2 s = Size;
        int nx = Mathf.Max(1, Mathf.RoundToInt(s.x * 18f / 6f));
        int ny = Mathf.Max(1, Mathf.RoundToInt(s.y * 18f / 6f));
        float cw = s.x / nx, ch = s.y / ny;
        Color a = Info.IsEnemy ? K.Red : Main;
        Color b = Info.IsEnemy ? Color.Lerp(K.Red, Color.black, 0.3f) : K.Accent(Info.Theme, Info.AppIcon, 1);
        if (!Info.IsEnemy && Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.15f) b = Color.Lerp(a, Color.white, 0.35f);

        // 近い所（持ち主に近い側の下の端）から順に
        Vector2 near = new Vector2(0f, -s.y * 0.5f);
        if (Info.Owner != null)
        {
            Vector2 local = transform.InverseTransformPoint(Info.Owner.OriginPoint);
            near.x = Mathf.Clamp(local.x, -s.x * 0.5f, s.x * 0.5f);
        }
        float maxDist = 0.01f;
        var cells = new List<(Vector2 pos, float dist, Color col)>();
        for (int y = 0; y < ny; y++)
        {
            for (int x = 0; x < nx; x++)
            {
                var p = new Vector2(-s.x * 0.5f + cw * (x + 0.5f), -s.y * 0.5f + ch * (y + 0.5f));
                float d = Vector2.Distance(p, near);
                maxDist = Mathf.Max(maxDist, d);
                cells.Add((p, d, (x + y) % 2 == 0 ? a : b));
            }
        }
        var tw = GenericFrameTweens.Of(gameObject);
        foreach (var c in cells)
        {
            SpriteRenderer sr = K.SprSized(Root, CellSprite, c.pos, new Vector2(cw, ch), c.col, K.OrderPillar, "Cell");
            Vector3 size = sr.transform.localScale;
            tw.Scale(sr.transform, Vector3.zero, size, 0.05f, c.dist / maxDist * 0.15f, GenericFrameTweens.Ease.Pop);
            _cells.Add(sr);
            _colors.Add(c.col);
        }
        // ふち（プレイヤー＝白）
        float t = K.Dot;
        _edges.Add(K.Box(Root, new Vector2(0f, s.y * 0.5f - t * 0.5f), new Vector2(s.x, t), Edge, K.OrderPillar + 1, "Edge"));
        _edges.Add(K.Box(Root, new Vector2(0f, -s.y * 0.5f + t * 0.5f), new Vector2(s.x, t), Edge, K.OrderPillar + 1, "Edge"));
        _edges.Add(K.Box(Root, new Vector2(-s.x * 0.5f + t * 0.5f, 0f), new Vector2(t, s.y), Edge, K.OrderPillar + 1, "Edge"));
        _edges.Add(K.Box(Root, new Vector2(s.x * 0.5f - t * 0.5f, 0f), new Vector2(t, s.y), Edge, K.OrderPillar + 1, "Edge"));
    }

    protected override void Tick(float t)
    {
        if (Phase != DressPhase.Active) return;
        AttackData d = Info.Data;
        bool pulse = d != null && d.TickInterval > 0f && (t % d.TickInterval) < 0.06f;
        float remain = HitWindow - t;
        float fade = remain < 0.5f ? Mathf.Clamp01(remain / 0.5f) * 0.7f + 0.3f : 1f;
        if (pulse == _pulse && Mathf.Abs(fade - _fade) < 0.01f) return;
        _pulse = pulse;
        _fade = fade;
        for (int i = 0; i < _cells.Count; i++)
        {
            Color c = pulse ? Color.Lerp(_colors[i], Color.white, 0.4f) : _colors[i];
            c.a = fade * PaintAlpha;
            _cells[i].color = c;
        }
    }

    protected override void OnVanish() => FadeAway(0.1f);
}

// ───────── 円：アイコン（24ドット）が「ポン」とふくらみ、アプリの色の輪が2重に広がる ─────────

public class GenericFxCircle : GenericFrameFx
{
    bool _shown;

    protected override void OnTelegraph() => Show();
    protected override void OnActive() => Show();

    void Show()
    {
        if (_shown) return;
        _shown = true;
        Upright = true; // 壁で開いた爆発でも、アイコンは回さない
        float d = Size.x;
        int dpx = Px(d);
        float life = HitWindow;
        var tw = GenericFrameTweens.Of(gameObject);
        // 判定の中：うすい面（輪のいちばん外＝判定）
        SpriteRenderer fill = K.SprSized(Root, PlaceholderSprites.Circle, Vector2.zero, Vector2.one * d, K.WithAlpha(Main, 0.25f), K.OrderSpark - 2, "Fill");
        Vector3 fs = fill.transform.localScale;
        tw.Scale(fill.transform, fs * 0.15f, fs, life * 0.5f).Alpha(fill, 0.3f, 0f, life * 0.5f, life * 0.5f);
        SpriteRenderer ring1 = K.Spr(Root, K.Ring(dpx, 3), Vector2.zero, Main, K.OrderSpark - 1, 1f, "Ring1");
        tw.Scale(ring1.transform, Vector3.one * 0.15f, Vector3.one * 0.92f, life * 0.6f).Alpha(ring1, 1f, 0.2f, life * 0.4f, life * 0.6f);
        SpriteRenderer ring2 = K.Spr(Root, K.Ring(dpx, 1), Vector2.zero, Edge, K.OrderSpark, 1f, "Ring2");
        tw.Scale(ring2.transform, Vector3.one * 0.1f, Vector3.one, life * 0.7f, 0.03f).Alpha(ring2, 1f, 0.3f, life * 0.3f, life * 0.7f);
        // まん中の白い光と、ふくらむアイコン
        SpriteRenderer flash = K.Spr(Root, K.Disc(24), Vector2.zero, Color.white, K.OrderSpark + 1, 1f, "Flash");
        tw.Scale(flash.transform, Vector3.one * 0.5f, Vector3.one * 1.4f, 0.1f).Alpha(flash, 0.9f, 0f, 0.1f);
        Transform icon = K.Node(Root, "Icon", Vector2.zero);
        K.Spr(icon, K.RoundRect(26, 26, 3), Vector2.zero, Edge, K.OrderSpark + 2, 1f, "Edge");
        SpriteRenderer ic = K.IconAt(icon, Icon, Vector2.zero, 24, K.OrderSpark + 3);
        tw.Scale(icon, Vector3.zero, Vector3.one, 0.12f, 0f, GenericFrameTweens.Ease.OutBack);
        foreach (var sr in icon.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, 1f, 0f, life * 0.4f, life * 0.6f);
    }

    protected override void OnVanish() => FadeAway(0.1f, 1.1f);
}

// ───────── 窓：通知の板（54×36。1-4 の Mac の通知）。足場になる ─────────

public class GenericFxWindow : GenericFrameFx
{
    protected int W, H;
    Transform _tele;
    protected Transform Body;
    SpriteRenderer _dashed, _spinner, _outer, _flash, _top;
    int _spinD;
    float _platformAt = float.PositiveInfinity, _removeAt = float.PositiveInfinity;
    bool _platform;

    /// <summary>板の見た目（Generic＝通知。子は Panel か Window にする）</summary>
    protected virtual GenericPlateLook Look => GenericPlateLook.Banner;
    /// <summary>板の地の色</summary>
    protected virtual Color PanelColor => GenericMac.NotifLight;
    /// <summary>板の角の丸み（ドット）。Mac のウィンドウは幅で決まる（1-2）</summary>
    protected virtual int Corner => Look == GenericPlateLook.Window ? GenericMac.CornerOf(W) : 3;
    /// <summary>消えるとき右へすべるか（false ならその場でうすくなる。Mac のウィンドウは、まん中へ縮む）</summary>
    protected virtual bool SlideOnVanish => true;

    /// <summary>
    /// 板の中身（通知：左にアプリのアイコン 16ドット、右に題名の棒（黒）と本文の棒（灰）、右上に時刻の代わりの短い棒）。
    /// body の原点が板のまん中、大きさは w×h ドット
    /// </summary>
    protected virtual void BuildContent(Transform body, int w, int h)
    {
        float left = -w * 0.5f + 5f + 8f;
        K.IconAt(body, Icon, new Vector2(left, 0f) * K.Dot, 16, K.OrderWindow + 1);
        float barL = left + 8f + 4f, barR = w * 0.5f - 5f;
        float len = Mathf.Max(4f, barR - barL);
        K.BoxPx(body, barL + len * 0.35f, 5f, len * 0.7f, 2f, GenericMac.BannerInk, K.OrderWindow + 1, "Title");
        K.BoxPx(body, barL + len * 0.5f, 0f, len, 2f, GenericMac.Gray1, K.OrderWindow + 1, "Bar");
        K.BoxPx(body, barL + len * 0.4f, -5f, len * 0.8f, 2f, K.WithAlpha(GenericMac.Gray1, 0.6f), K.OrderWindow + 1, "Bar");
        K.BoxPx(body, barR - 3f, h * 0.5f - 5f, 7f, 1f, GenericMac.Light4, K.OrderWindow + 1, "Time");
    }

    /// <summary>消える直前（目を閉じるなど）</summary>
    protected virtual void OnVanishContent(Transform body) { }

    protected override void Build()
    {
        W = Px(Size.x);
        H = Px(Size.y);
    }

    protected override void OnTelegraph()
    {
        _tele = K.Node(Root, "Telegraph", Vector2.zero);
        _dashed = K.Spr(_tele, K.DashedEdge(W, H), Vector2.zero, K.WithAlpha(Edge, 0.85f), K.OrderTelegraph, 1f, "Frame");
        // 読み込み中：Mac の進行の輪（1-6。棒 8本。灰色、敵は赤）
        _spinD = W >= 30 && H >= 24 ? 16 : 8;
        _spinner = K.Spr(_tele, GenericMac.SpinnerFrame(_spinD, 0), Vector2.zero, Info.IsEnemy ? K.Red : GenericMac.Gray1, K.OrderTelegraph + 1, 1f, "Spinner");
    }

    protected override void OnActive()
    {
        if (Body != null) return;
        if (_tele != null) Destroy(_tele.gameObject);
        var tw = GenericFrameTweens.Of(gameObject);
        Body = K.Node(Root, "Body", Vector2.zero);
        switch (Look)
        {
            case GenericPlateLook.Panel:
                K.Plate(Body, Vector2.zero, W, H, Corner, Edge, PanelColor, K.OrderWindow, out _outer, out _);
                break;
            case GenericPlateLook.Window:
                // Mac のウィンドウ（1-2）：影・タイトルバー・左上に信号機・まん中に題名の棒。ふちは敵＝赤、プレイヤー＝白
                _outer = GenericMac.PlaceWindow(Body, Vector2.zero, W, H, false, Edge, K.OrderWindow);
                break;
            default:
                // Mac の通知（1-4）：明るい地・影。ふちは敵＝赤、プレイヤー＝白（足場になったら影だけ）
                _outer = GenericMac.Plate(Body, Vector2.zero, W, H, Corner, PanelColor, Edge, K.OrderWindow, true, out _);
                break;
        }
        BuildContent(Body, W, H);
        // 判定のある間は赤く光る（プレイヤーは白）
        _flash = K.Spr(Body, K.RoundRect(W, H, Corner), Vector2.zero, K.WithAlpha(Info.IsEnemy ? K.Red : Color.white, 0.5f), K.OrderWindow + 3, 1f, "Flash");
        tw.Pop(Body, Vector3.one, 0.1f);
        // 後ろに、うすい枠を2枚（ポップアップが重なって出る感じ）
        for (int i = 1; i <= 2; i++)
        {
            SpriteRenderer g = K.Spr(Root, K.RoundEdge(W, H, Corner), new Vector2(3f * i, -3f * i) * K.Dot, K.WithAlpha(Edge, 0.7f / i), K.OrderWindow - 1, 1f, "Echo");
            tw.Alpha(g, 0.7f / i, 0f, 0.2f);
        }
        AttackData d = Info.Data;
        _platformAt = Time.time + (d != null ? d.ActiveTime : 0.1f);
        _removeAt = d != null && d.LandRemainTime > 0f ? Time.time + d.LandRemainTime : float.PositiveInfinity;
    }

    protected override void OnLanded() => OnActive();

    protected override void Tick(float t)
    {
        float now = Time.time;
        if (Phase == DressPhase.Telegraph)
        {
            if (_spinner != null) _spinner.sprite = GenericMac.SpinnerFrame(_spinD, GenericMac.SpinnerIndex(t));
            if (_dashed != null) _dashed.enabled = K.BlinkOn(TelegraphTime - t);
            return;
        }
        if (Body == null) return;
        if (now < _platformAt)
        {
            _flash.enabled = Mathf.FloorToInt(now / 0.03f) % 2 == 0;
        }
        else if (!_platform)
        {
            // 足場になった：赤いふちを灰色にして、上の端（乗れる所）に線を引く
            _platform = true;
            _flash.enabled = false;
            Color top = Color.white;
            switch (Look)
            {
                case GenericPlateLook.Panel: _outer.color = K.WinDarkEdge; break;
                case GenericPlateLook.Window: _outer.color = GenericMac.Light5; break; // 決まりのふちの色に戻す
                default:
                    // 通知：ふちを消して影だけ。明るい地に白い線は見えないので、上の端は灰色の線
                    _outer.enabled = false;
                    top = GenericMac.Light5;
                    break;
            }
            _top = K.BoxPx(Body, 0f, H * 0.5f - 1.5f, W - 6f, 1f, top, K.OrderWindow + 3, "TopLine");
        }
        // 消える1秒前から点滅（今の点滅と同じ 0.1秒）
        if (_removeAt - now < 1f) Body.gameObject.SetActive(Mathf.FloorToInt(now / 0.1f) % 2 == 0);
    }

    protected override void OnVanish()
    {
        if (Body != null)
        {
            Body.gameObject.SetActive(true);
            OnVanishContent(Body);
        }
        if (Look == GenericPlateLook.Window)
        {
            // Mac のウィンドウ：赤い丸がへこむ → まん中へ縮んで消える（1-2）
            Transform body = Body;
            Transform w = Detach(0.2f);
            GenericMac.CloseAnim(w, body, W, H, K.OrderWindow + 4);
            return;
        }
        Transform r = Detach(0.24f);
        if (r == null) return;
        var tw = GenericFrameTweens.Of(r.gameObject);
        if (SlideOnVanish) tw.Move(r, r.localPosition, r.localPosition + Vector3.right, 0.2f, 0f, GenericFrameTweens.Ease.InQuad);
        foreach (var sr in r.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, sr.color.a, 0f, 0.2f, SlideOnVanish ? 0f : 0.04f);
    }
}

// ───────── 落下物：白いウィンドウ（Mac のウィンドウ。1-2：左上に信号機、まん中に題名の棒、まん中にアイコン、下に影） ─────────
// 板（角・ふち・影）は Creative・AI・Media の落下物も同じ（33）。タイトルバーと信号機は、Generic の中身（BuildContent）だけ

public class GenericFxFalling : GenericFrameFx
{
    protected int W, H;
    Transform _warn, _shadow;
    protected Transform Body;
    SpriteRenderer _warnSr;
    FallingObject _fo;

    /// <summary>板の地の色</summary>
    protected virtual Color PanelColor => K.WinLight;
    /// <summary>落ちる間、左右に 3°ゆれるか</summary>
    protected virtual bool Wobble => true;

    /// <summary>中身（白いウィンドウ：タイトルバー・信号機・題名の棒と、まん中のアイコン）。ふちは下の板（敵は赤いまま）</summary>
    protected virtual void BuildContent(Transform body, int w, int h)
    {
        K.Spr(body, GenericMac.WindowSprite(w, h, false, new Color32(0, 0, 0, 0)), Vector2.zero, Color.white, K.OrderWindow + 1, 1f, "Window");
        int top = GenericMac.TitleHOf(h) + 2; // タイトルバーと下の線、上のふち
        int room = h - top - 3;
        int ipx = room >= 24 ? 24 : room >= 16 ? 16 : Mathf.Max(8, room);
        K.IconAt(body, Icon, new Vector2(0f, -top * 0.5f) * K.Dot, ipx, K.OrderWindow + 2);
    }

    /// <summary>着地で飛ぶ物（信号機の3色の丸）</summary>
    protected virtual void LandedBurst(Vector2 at)
    {
        Color[] lights = { K.LightR, K.LightY, K.LightG };
        K.Shards(at + new Vector2(0f, Size.y * 0.3f), Vector2.up, 3, i => K.Disc(3), i => lights[i], 50f, 2f);
        K.Shards(at, Vector2.up, 5, i => null, i => K.WinLight, 70f, 0.8f);
    }

    protected override void Build()
    {
        W = Px(Size.x);
        H = Px(Size.y);
    }

    protected override void OnTelegraph()
    {
        // 影（今の影は呼んだ側がかくすので、同じ場所・同じ大きさに出す）。落ち終わるまで床に置いたまま
        _fo = GetComponent<FallingObject>();
        if (_fo != null)
        {
            Sprite shadow = GameArt.Instance != null && GameArt.Instance.Shadow != null ? GameArt.Instance.Shadow : PlaceholderSprites.Circle;
            _shadow = K.Node(Root, "Shadow", Vector2.zero);
            K.SprSized(_shadow, shadow, Vector2.zero, new Vector2(Size.x, 0.35f), new Color(0f, 0f, 0f, 0.45f), K.OrderTelegraph - 1, "Ellipse");
            K.SprSized(_shadow, K.RoundEdge(Mathf.Max(4, W), 6, 2), Vector2.zero, new Vector2(Size.x, 0.35f), K.WithAlpha(Edge, 0.7f), K.OrderTelegraph, "Edge");
            PlaceShadow();
        }
        // 落ちてくる空（画面の上の端の少し下）に、赤い「！」
        _warn = K.Node(Root, "Warn", Vector2.zero);
        _warnSr = K.Spr(_warn, K.WarnTriangle, Vector2.zero, Edge, K.OrderTelegraph + 2, 1.5f, "Tri");
        PlaceWarn();
    }

    void PlaceShadow()
    {
        if (_shadow == null || _fo == null) return;
        _shadow.position = new Vector3(transform.position.x, _fo.GroundY + 0.12f, 0f);
        _shadow.rotation = Quaternion.identity;
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        PlaceShadow();
    }

    void PlaceWarn()
    {
        if (_warn == null) return;
        Rect view = ScreenUi.ViewRect();
        Rect arena = BattleArena.GetBounds();
        float top = Mathf.Min(view.yMax, arena.yMax) - PremiereArt.HudInset - 0.2f;
        _warn.position = new Vector3(transform.position.x, top, 0f);
        _warn.rotation = Quaternion.identity;
    }

    protected override void OnActive()
    {
        if (_warn != null) Destroy(_warn.gameObject);
        if (Body != null) return;
        Body = K.Node(Root, "Body", Vector2.zero);
        int r = GenericMac.CornerOf(W);
        // 影（1-2：下へ 2ドット。判定の外・下にだけ出る）。すけた板（Media のジャケット）には付けない
        if (PanelColor.a >= 0.9f) GenericMac.Shadow(Body, Vector2.zero, W, H, r, K.OrderWindow - 1);
        K.Plate(Body, Vector2.zero, W, H, r, Edge, PanelColor, K.OrderWindow, out _, out _);
        BuildContent(Body, W, H);
    }

    protected override void Tick(float t)
    {
        if (Phase == DressPhase.Telegraph)
        {
            PlaceWarn();
            if (_warnSr != null) _warnSr.enabled = Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
            return;
        }
        if (Phase == DressPhase.Active && Body != null && Wobble) Body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 14f) * 3f);
    }

    protected override void OnLanded()
    {
        if (_shadow != null) Destroy(_shadow.gameObject);
        LandedBurst(transform.position);
        Transform r = Detach(0.25f);
        if (r == null) return;
        var tw = GenericFrameTweens.Of(r.gameObject);
        tw.Scale(r, Vector3.one, new Vector3(1.15f, 0.5f, 1f), 0.08f);
        foreach (var sr in r.GetComponentsInChildren<SpriteRenderer>()) tw.Alpha(sr, sr.color.a, 0f, 0.2f, 0.04f);
    }

    protected override void OnVanish()
    {
        if (IsLanded) return;
        FadeAway(0.1f);
    }
}

// ───────── 上の列：小さい通知の板（27×11。1-4：明るい地、アイコンと棒）。予告は画面の上のメニューバー（1-5） ─────────

public class GenericFxTopRow : GenericFrameFx
{
    protected int W, H;
    protected Transform Body;
    FallingObject _fo;
    float _appearAt;
    bool _shown;

    /// <summary>板の見た目（Generic＝小さい通知。Creative・AI・Media の子は Panel）</summary>
    protected virtual GenericPlateLook Look => GenericPlateLook.Banner;
    /// <summary>板の地の色</summary>
    protected virtual Color PanelColor => GenericMac.NotifLight;

    /// <summary>中身（左にアイコン、右に題名の棒（黒）と本文の棒（灰））</summary>
    protected virtual void BuildContent(Transform body, int w, int h)
    {
        int ipx = Mathf.Clamp(h - 2, 6, 12);
        K.IconAt(body, Icon, new Vector2(-w * 0.5f + 2f + ipx * 0.5f, 0f) * K.Dot, ipx, K.OrderWindow + 1);
        float barL = -w * 0.5f + 4f + ipx, barR = w * 0.5f - 3f;
        float len = Mathf.Max(3f, barR - barL);
        K.BoxPx(body, barL + len * 0.5f, 1.5f, len, 1f, GenericMac.BannerInk, K.OrderWindow + 1, "Bar");
        K.BoxPx(body, barL + len * 0.35f, -1.5f, len * 0.7f, 1f, K.Grey, K.OrderWindow + 1, "Bar");
    }

    /// <summary>
    /// 画面の上の帯（メニューバー。1-5）。並べる間隔 spacing マス。
    /// Generic：闘技場の端から端まで、高さ 0.5マスのメニューバーを 1本（並べた物ごとに呼ばれるので、1個目だけが作る）。
    /// 判定は無いので 6割すかす。最後の1個が落ちるまで残す（自分で消える。形態変化・正常化では攻撃した人が消す）。
    /// 上の端は、画面と闘技場の上の端の低いほう（印が切れないように。吊られた板が少し重なるが、板のほうが手前）
    /// </summary>
    protected virtual void BuildBand(Transform root, float spacing)
    {
        if (Info.Index != 0) return;
        Rect arena = BattleArena.GetBounds();
        Rect view = ScreenUi.ViewRect();
        float y = Mathf.Min(view.yMax, arena.yMax) - 0.25f;
        float life = 3f;
        AttackData d = Info.Data;
        if (_fo != null && d != null) life = Mathf.Max(0.3f, _fo.FallStartTime + (Mathf.Max(1, d.SpawnCount) - 1) * d.DropInterval - Time.time);
        Transform bar = K.Loose("Gen4_MenuBar", new Vector2(arena.center.x, y));
        GenericFrameTweens.Of(bar.gameObject).Kill(life);
        SkinDresser.Adopt(Info, bar.gameObject);
        GenericMac.MenuBarWorld(bar, arena.width, 0.4f, K.OrderTelegraph);
    }

    /// <summary>着地（はじける）</summary>
    protected virtual void LandedBurst(Vector2 at) => GenericFrameBits.Pop(at, Icon, 10, Edge, Main);

    protected override void Build()
    {
        W = Px(Size.x);
        H = Px(Size.y);
        _fo = GetComponent<FallingObject>();
        Body = K.Node(Root, "Body", Vector2.zero);
        if (Look == GenericPlateLook.Panel) K.Plate(Body, Vector2.zero, W, H, 2, Edge, PanelColor, K.OrderWindow, out _, out _);
        else GenericMac.Plate(Body, Vector2.zero, W, H, 2, PanelColor, Edge, K.OrderWindow, true, out _); // 小さい通知：明るい地・影・ふち（敵は赤）
        BuildContent(Body, W, H);
        Body.gameObject.SetActive(false);

        // 並べる時刻（何個目かから。予告が並べた時に来るなら、すぐ出る）
        _appearAt = Time.time;
        AttackData d = Info.Data;
        if (_fo != null && d != null)
        {
            int n = Mathf.Max(1, d.SpawnCount), i = Mathf.Clamp(Info.Index, 0, n - 1);
            float computed = _fo.FallStartTime - ((n - 1) * d.PlaceInterval + d.DropHoldTime + i * d.DropInterval) + i * d.PlaceInterval;
            _appearAt = Mathf.Max(Time.time, computed);
        }
    }

    protected override void OnTelegraph()
    {
        float spacing = Info.Data != null && Info.Data.PlaceSpacing > 0f ? Info.Data.PlaceSpacing : Size.x * 1.5f;
        BuildBand(Root, spacing);
    }

    void ShowBody()
    {
        if (_shown) return;
        _shown = true;
        Body.gameObject.SetActive(true);
        GenericFrameTweens.Of(gameObject).Pop(Body, Vector3.one, 0.1f);
    }

    protected override void OnActive()
    {
        ShowBody();
    }

    protected override void Tick(float t)
    {
        float now = Time.time;
        if (Phase == DressPhase.Telegraph)
        {
            if (!_shown && now >= _appearAt) ShowBody();
            // 落ちる直前（0.5秒）は小さくふるえる
            if (_shown && _fo != null && _fo.FallStartTime - now < 0.5f)
            {
                Body.localPosition = new Vector3(Random.Range(-1f, 1f) * K.Dot, 0f, 0f);
            }
        }
        else if (Body != null) Body.localPosition = Vector3.zero;
    }

    protected override void OnLanded()
    {
        LandedBurst(transform.position);
        if (Root != null) Root.gameObject.SetActive(false);
    }

    protected override void OnVanish()
    {
        if (IsLanded)
        {
            if (Root != null) Destroy(Root.gameObject);
            return;
        }
        // クリックで消した：横に閉じる
        Transform r = Detach(0.12f);
        GenericFrameBits.Squeeze(r, 0.1f);
    }
}
