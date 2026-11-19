using System.Collections.Generic;
using UnityEngine;
using K = GenericFrameKit;

// 【仮】Premiere Pro のスキン（2026-11-18。担当4。docs/app-skill-visuals-design.md 12章）。
// ・専用の技（エフェクトアイコンの雨・再生ヘッド・レーザーツール・スピードデュレーション）と能力（エフェクトアイコン投げ）の絵は、
//   それぞれの部品（Combat/Custom/Premiere）の中に足した。
// ・ここは、当たった絵・エラーの絵・通知の題名（12-5）と、アイコンごとの当たり方（12-1 の表）。
//   どのアイコンかは、エラーの絵の種類（ErrorKind）で見分ける：Fire・Explosion＝炎、Thunder・Bolt＝雷、Dissolve＝ディゾルブ、Razor＝レーザーツール
// ・Chrome・VS Code から受けついだ枠は false（Generic に任せる）
public class PremiereSkin : AppSkin
{
    public override SkinId Id => SkinId.Premiere;
    public override string NotificationTitle => "Export complete";

    enum Kind { None, Fire, Thunder, Dissolve, Razor }

    static Kind KindOf(string errorKind)
    {
        if (string.IsNullOrEmpty(errorKind)) return Kind.None;
        if (errorKind.Contains("Fire") || errorKind.Contains("Explosion")) return Kind.Fire;
        if (errorKind.Contains("Thunder") || errorKind.Contains("Bolt")) return Kind.Thunder;
        if (errorKind.Contains("Dissolve")) return Kind.Dissolve;
        if (errorKind.Contains("Razor")) return Kind.Razor;
        return Kind.None;
    }

    // ───────── 当たった絵（12-5。12-1 の表の物が優先） ─────────

    public override bool Hit(HitInfo info)
    {
        Vector2 dir = info.Direction.sqrMagnitude > 0.0001f ? info.Direction : Vector2.up;
        if (info.Strength == HitStrength.Small)
        {
            K.Shards(info.Point, dir, 1, i => PremiereSkinArt.Film8, i => Color.white);
            return true;
        }
        bool fin = info.Strength == HitStrength.Finisher;
        bool heavy = info.Strength == HitStrength.Heavy || fin;
        int shards = fin ? 12 : heavy ? 6 : 3;
        Bounds b = K.TargetBounds(info.Target, info.Point);
        switch (KindOf(info.ErrorKind))
        {
            case Kind.Fire:
                PremiereSkinArt.FlamePillar(new Vector2(info.Point.x, Mathf.Max(b.min.y, info.Point.y - 1f)), fin ? 1.5f : 1f);
                K.Shards(info.Point, Vector2.up, 6, i => null, i => i % 2 == 0 ? PremiereArt.FireColor : PremiereArt.FireCore, 50f, 0.7f);
                K.Shards(info.Point, dir, shards / 2, i => PremiereSkinArt.Film8, i => Color.white);
                return true;
            case Kind.Thunder:
                PremiereSkinArt.SkyBolt(info.Point, 0.1f);
                K.Shards(info.Point, dir, shards / 2, i => PremiereSkinArt.Film8, i => Color.white);
                return true;
            case Kind.Dissolve:
                PremiereSkinArt.DissolveOver(b, 0.3f);
                K.Shards(info.Point, dir, shards / 2, i => K.Disc(3), i => PremiereArt.DissolveColor);
                return true;
        }
        // フィルムのコマ（20×14）が、まん中で2つに切れて左右へ
        Vector2 at = info.Point + dir * 0.3f + Vector2.up * 0.2f;
        float s = fin ? 1.5f : heavy ? 1.2f : 0.8f;
        Transform root = K.Loose("Gen4_PremiereHit", at);
        float life = heavy ? 0.3f : 0.2f;
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(life + 0.02f);
        tw.Scale(root, Vector3.one * s * 0.6f, Vector3.one * s, 0.06f, 0f, GenericFrameTweens.Ease.OutBack);
        SpriteRenderer l = K.Spr(root, PremiereSkinArt.FilmHalf(false), new Vector2(-5f, 0f) * K.Dot, Color.white, K.OrderHit, 1f, "L");
        SpriteRenderer r = K.Spr(root, PremiereSkinArt.FilmHalf(true), new Vector2(5f, 0f) * K.Dot, Color.white, K.OrderHit, 1f, "R");
        SpriteRenderer cut = K.Box(root, Vector2.zero, new Vector2(K.Dot, 16f * K.Dot), Color.white, K.OrderHit + 1, "Cut");
        tw.Move(l.transform, l.transform.localPosition, new Vector3(-0.55f, -0.1f, 0f), life, 0.05f).Rotate(l.transform, 0f, 18f, life, 0.05f);
        tw.Move(r.transform, r.transform.localPosition, new Vector3(0.55f, -0.1f, 0f), life, 0.05f).Rotate(r.transform, 0f, -18f, life, 0.05f);
        tw.Alpha(l, 1f, 0f, life * 0.4f, life * 0.6f).Alpha(r, 1f, 0f, life * 0.4f, life * 0.6f).Alpha(cut, 1f, 0f, 0.06f, 0.04f);
        K.Shards(info.Point, dir, shards, i => PremiereSkinArt.Film8, i => Color.white, 40f, fin ? 1.3f : 1f);
        return true;
    }

    // ───────── エラーの絵（12-5。Media Offline）と、アイコンごとの当たり方（12-1 の表の「敵」） ─────────

    public override bool Error(HitInfo info)
    {
        bool down = info.Strength == HitStrength.Down;
        Bounds b = K.TargetBounds(info.Target, info.Point);
        Vector2 head = new Vector2(b.center.x, b.max.y + 0.7f);
        if (down)
        {
            Rect view = ScreenUi.ViewRect();
            head = Vector2.Lerp(head, view.center, 0.35f);
        }
        float scale = down ? 2f : 1f;
        float life = down ? 0.9f : 0.4f;
        switch (KindOf(info.ErrorKind))
        {
            case Kind.Fire:
                // 炎：足元でオレンジの炎が 0.3秒。エラーの絵は「焼けたコマ」
                PremiereSkinArt.FeetFlame(new Vector2(b.center.x, b.min.y));
                PremiereSkinArt.Picture(PremiereSkinArt.BurntFrame, head, scale, life);
                return true;
            case Kind.Thunder:
                // 雷：画面全体が白く1回。黄色のぎざぎざがプレイヤーを通る
                ScreenFx.WhiteFlash(0.3f, 0.05f);
                PremiereSkinArt.ThroughBolt(b);
                break;
            case Kind.Razor:
                // レーザーツール：プレイヤーの絵が、上下に 2ドットずれて見える（切られた）
                PremiereSkinArt.CutLook(info.Target, b);
                break;
            case Kind.Dissolve:
                PremiereSkinArt.DoubleImage(info.Target, 0.3f);
                break;
        }
        PremiereSkinArt.MediaOffline(head, scale, life);
        return true;
    }
}

/// <summary>【仮】Premiere Pro のスキンの絵と、当たったときの小さい演出</summary>
public static class PremiereSkinArt
{
    static readonly Dictionary<char, Color32> FilmPal = new Dictionary<char, Color32>
    {
        { 'k', RuntimeSprites.Hex("2B2B2E") },
        { 'w', new Color32(255, 255, 255, 255) },
        { 'g', RuntimeSprites.Hex("5A5A5E") },
        { 'p', RuntimeSprites.Hex("9999FF") },
        { 'l', RuntimeSprites.Hex("E6E6FF") },
        { 'b', new Color32(0, 0, 0, 255) },
        { 'o', RuntimeSprites.Hex("FF9500") },
    };

    public static readonly Color OfflineRed = RuntimeSprites.Hex("D93A2F"); // 2026-11-19：メディアオフラインの赤（docs/app-feedback-v2-design.md 5-1）
    public static readonly Color ClipPurple = RuntimeSprites.Hex("9999FF");
    public static readonly Color ClipDark = RuntimeSprites.Hex("5B5BB8");

    /// <summary>フィルムのコマ（8×6。暗い灰色、上下に白い小さい穴）。共通のかけら（12章）</summary>
    public static Sprite Film8 => K.Art("PrFilm8", new[]
    {
        "kwkwkwkk",
        "kkkkkkkk",
        "kggggggk",
        "kggggggk",
        "kkkkkkkk",
        "kwkwkwkk",
    }, FilmPal);

    /// <summary>フィルムのコマ（20×14）の左右の半分（10×14）</summary>
    public static Sprite FilmHalf(bool right)
    {
        var rows = new string[14];
        var chars = new char[10];
        for (int y = 0; y < 14; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                int gx = right ? x + 10 : x;
                char c;
                if (y == 0 || y == 2 || y == 11 || y == 13) c = 'k';
                else if (y == 1 || y == 12) c = gx % 3 == 1 ? 'w' : 'k';
                else c = gx == 0 || gx == 19 ? 'k' : (y < 7 ? 'l' : 'p');
                chars[x] = c;
            }
            rows[y] = new string(chars);
        }
        return K.Art(right ? "PrFilmR" : "PrFilmL", rows, FilmPal);
    }

    /// <summary>焼けたコマ（16×12。フィルムのコマの角が黒い）</summary>
    public static Sprite BurntFrame
    {
        get
        {
            var rows = new string[12];
            var chars = new char[16];
            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    char c;
                    if (y == 0 || y == 2 || y == 9 || y == 11) c = 'k';
                    else if (y == 1 || y == 10) c = x % 3 == 1 ? 'w' : 'k';
                    else c = x == 0 || x == 15 ? 'k' : 'l';
                    // 右上の角が焼けて黒い（ふちはオレンジ）
                    int d = (15 - x) + y;
                    if (d < 6) c = '.';
                    else if (d < 8) c = 'b';
                    else if (d == 8) c = 'o';
                    chars[x] = c;
                }
                rows[y] = new string(chars);
            }
            return K.Art("PrBurnt", rows, FilmPal);
        }
    }

    /// <summary>キーフレームのひし形（白、7ドット）</summary>
    public static Sprite Keyframe => K.Glyph("PrKey", new[] { "...X...", "..XXX..", ".XXXXX.", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..." });

    /// <summary>頭の上に絵を出して、浮きながら消す</summary>
    public static void Picture(Sprite sprite, Vector2 at, float scale, float life)
    {
        Transform root = K.Loose("Gen4_PremierePic", at);
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(life + 0.02f);
        SpriteRenderer sr = K.Spr(root, sprite, Vector2.zero, Color.white, K.OrderHit);
        tw.Pop(sr.transform, Vector3.one * scale, 0.1f).Move(root, at, at + Vector2.up * 0.4f, life).Alpha(sr, 1f, 0f, 0.12f, life - 0.12f);
    }

    /// <summary>Media Offline の板（赤い地に白い字。2行）</summary>
    public static void MediaOffline(Vector2 at, float scale, float life)
    {
        Transform root = K.Loose("Gen4_MediaOffline", at);
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(life + 0.02f);
        Transform pic = K.Node(root, "Pic", Vector2.zero);
        K.Plate(pic, Vector2.zero, 58, 21, 1, Color.white, OfflineRed, K.OrderHit, out SpriteRenderer o, out SpriteRenderer i);
        TextMesh a = K.Text(pic, "Media", new Vector2(0f, 0.22f), 0.4f, Color.white, K.OrderHit + 2);
        TextMesh b = K.Text(pic, "Offline", new Vector2(0f, -0.22f), 0.4f, Color.white, K.OrderHit + 2);
        tw.Pop(pic, Vector3.one * scale, 0.1f).Move(root, at, at + Vector2.up * 0.4f, life);
        tw.Alpha(o, 1f, 0f, 0.12f, life - 0.12f).Alpha(i, 1f, 0f, 0.12f, life - 0.12f);
        tw.TextAlpha(a, Color.white, 1f, 0f, 0.12f, life - 0.12f).TextAlpha(b, Color.white, 1f, 0f, 0.12f, life - 0.12f);
    }

    /// <summary>足元のオレンジの炎（14ドット。0.3秒）</summary>
    public static void FeetFlame(Vector2 feet)
    {
        Transform root = K.Loose("Gen4_PremiereFeetFlame", feet);
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(0.32f);
        for (int i = -1; i <= 1; i++)
        {
            SpriteRenderer f = K.Spr(root, PremiereArt.SmallFlame, new Vector2(i * 0.3f, 0f), Color.white, K.OrderHit, i == 0 ? 2f : 1.4f, "Flame");
            Vector3 s = f.transform.localScale;
            tw.Scale(f.transform, new Vector3(s.x, 0f, 1f), s, 0.08f, 0f, GenericFrameTweens.Ease.OutBack).Alpha(f, 1f, 0f, 0.1f, 0.2f);
        }
    }

    /// <summary>炎の柱（幅 1×高さ 2マス。オレンジ → 黄 → 白。0.25秒）。bottom は柱の下の端</summary>
    public static void FlamePillar(Vector2 bottom, float scale)
    {
        Transform root = K.Loose("Gen4_PremiereFlamePillar", bottom);
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(0.27f);
        Color[] cols = { PremiereArt.FireColor, PremiereArt.FireCore, Color.white };
        for (int i = 0; i < 3; i++)
        {
            float w = (1f - i * 0.28f) * scale, h = (2f - i * 0.45f) * scale;
            SpriteRenderer col = K.Box(root, new Vector2(0f, h * 0.5f), new Vector2(w, h), K.WithAlpha(cols[i], 0.9f), K.OrderHit + i, "Pillar");
            Vector3 s = col.transform.localScale;
            tw.Scale(col.transform, new Vector3(s.x, 0f, 1f), s, 0.06f, i * 0.02f, GenericFrameTweens.Ease.OutQuad);
            tw.Move(col.transform, new Vector3(0f, 0f, 0f), new Vector3(0f, h * 0.5f, 0f), 0.06f, i * 0.02f);
            tw.Alpha(col, 0.9f, 0f, 0.12f, 0.13f);
        }
        for (int i = 0; i < 3; i++)
        {
            SpriteRenderer f = K.Spr(root, PremiereArt.SmallFlame, new Vector2((i - 1) * 0.32f * scale, 1.9f * scale), Color.white, K.OrderHit + 3, 1.6f * scale, "Tip");
            tw.Alpha(f, 1f, 0f, 0.12f, 0.13f);
        }
    }

    /// <summary>画面の上の端から相手まで、黄色のぎざぎざの稲妻（白い芯）。見た目だけ</summary>
    public static void SkyBolt(Vector2 to, float life)
    {
        Rect view = ScreenUi.ViewRect();
        float top = view.yMax;
        float h = Mathf.Max(1f, top - to.y);
        Transform root = K.Loose("Gen4_PremiereSkyBolt", new Vector2(to.x, to.y));
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(life + 0.05f);
        int n = Mathf.CeilToInt(h);
        var tiles = new List<SpriteRenderer>();
        for (int i = 0; i < n; i++)
        {
            SpriteRenderer t = K.Spr(root, PremiereArt.BoltTile(i & 1), new Vector2(0f, 0.5f + i), Color.white, K.OrderHit + 1, 1f, "Bolt");
            t.transform.localScale = new Vector3(0.6f, 1f, 1f);
            tiles.Add(t);
        }
        SpriteRenderer flash = K.Spr(root, K.Disc(20), Vector2.zero, Color.white, K.OrderHit + 2, 1f, "Flash");
        tw.Scale(flash.transform, Vector3.one * 0.5f, Vector3.one * 1.6f, life).Alpha(flash, 1f, 0f, life);
        tw.Each(life + 0.05f, k =>
        {
            int v = Mathf.FloorToInt(Time.time / 0.03f) & 1;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] == null) continue;
                tiles[i].sprite = PremiereArt.BoltTile((i + v) & 1);
                K.SetAlpha(tiles[i], k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
            }
        });
    }

    /// <summary>黄色のぎざぎざが、プレイヤー（体の範囲 b）を上から下へ通る</summary>
    public static void ThroughBolt(Bounds b)
    {
        Transform root = K.Loose("Gen4_PremiereThroughBolt", new Vector2(b.center.x, b.min.y - 0.3f));
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(0.14f);
        for (int i = 0; i < 3; i++)
        {
            SpriteRenderer t = K.Spr(root, PremiereArt.BoltTile(i & 1), new Vector2(0f, 0.5f + i), Color.white, K.OrderHit + 1, 1f, "Bolt");
            t.transform.localScale = new Vector3(0.5f, 1f, 1f);
            tw.Alpha(t, 1f, 0f, 0.06f, 0.06f);
        }
    }

    /// <summary>相手が、紫の点のます目（4段）で溶けるように見える（重ねるだけ）</summary>
    public static void DissolveOver(Bounds b, float life)
    {
        int w = Mathf.Clamp(Mathf.RoundToInt(b.size.x * 18f), 6, 80), h = Mathf.Clamp(Mathf.RoundToInt(b.size.y * 18f), 6, 64);
        var cv = new GenericFrameCanvas(w, h, "Dissolve");
        Transform root = K.Loose("Gen4_PremiereDissolve", b.center);
        cv.Show(root, Vector2.zero, K.OrderHit, "Dots");
        var tw = GenericFrameTweens.Of(root.gameObject).Kill(life + 0.02f);
        int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        Color32 p = PremiereArt.DissolveColor, l = PremiereArt.DissolveLight;
        int lastStep = -1;
        tw.Each(life, k =>
        {
            int step = Mathf.Min(3, Mathf.FloorToInt(k * 4f)); // 4段
            if (step == lastStep) return;
            lastStep = step;
            cv.Clear();
            float density = (step + 1) / 4f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float th = (bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
                    if (th < density) cv.Set(x, y, ((x / 4 + y / 4) & 1) == 0 ? p : l);
                }
            }
            cv.Apply();
        });
    }

    /// <summary>プレイヤーの絵が、紫の2重の像になる（左右に 2ドット）</summary>
    public static void DoubleImage(GameObject target, float life)
    {
        SpriteRenderer src = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
        if (src == null || src.sprite == null) return;
        for (int s = -1; s <= 1; s += 2)
        {
            Transform t = K.Loose("Gen4_PremiereDouble", src.transform.position + new Vector3(s * 2f * K.Dot, 0f, 0f));
            t.localScale = src.transform.lossyScale;
            var sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = src.sprite;
            sr.flipX = src.flipX;
            sr.sortingOrder = src.sortingOrder + 1;
            sr.color = new Color(0.6f, 0.6f, 1f, 0.55f);
            GenericFrameTweens.Of(t.gameObject).Alpha(sr, 0.55f, 0f, life).Kill(life);
        }
    }

    /// <summary>切られた：プレイヤーの絵が上下に 2ドットずれて見える（写しを重ね、白い切れ目の線）。0.1秒</summary>
    public static void CutLook(GameObject target, Bounds b)
    {
        SpriteRenderer src = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
        if (src != null && src.sprite != null)
        {
            Transform t = K.Loose("Gen4_PremiereCut", src.transform.position + new Vector3(2f * K.Dot, 2f * K.Dot, 0f));
            t.localScale = src.transform.lossyScale;
            var sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = src.sprite;
            sr.flipX = src.flipX;
            sr.sortingOrder = src.sortingOrder + 1;
            sr.color = new Color(1f, 1f, 1f, 0.6f);
            GenericFrameTweens.Of(t.gameObject).Kill(0.1f);
        }
        Transform line = K.Loose("Gen4_PremiereCutLine", b.center);
        SpriteRenderer l = K.Box(line, Vector2.zero, new Vector2(b.size.x + 0.4f, K.Dot), Color.white, K.OrderHit + 1, "Line");
        line.rotation = Quaternion.Euler(0f, 0f, 12f);
        GenericFrameTweens.Of(line.gameObject).Alpha(l, 1f, 0f, 0.1f).Kill(0.1f);
    }
}

/// <summary>
/// 【仮】アイコンの後ろに、同じアイコンのうすい残像を 2枚（「ドラッグしている」。12-1）。
/// 動いている物（落ちるアイコン・投げたアイコン）に付ける。その物の子に作るので、一緒に消える
/// </summary>
public class PremiereSkinTrail : MonoBehaviour
{
    SpriteRenderer _source;
    readonly SpriteRenderer[] _copies = new SpriteRenderer[2];
    readonly GenericFrameTrail _trail = new GenericFrameTrail();

    public static PremiereSkinTrail Attach(GameObject host, SpriteRenderer source)
    {
        if (host == null || source == null) return null;
        var t = host.AddComponent<PremiereSkinTrail>();
        t._source = source;
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject("PremiereTrail");
            go.layer = host.layer;
            go.transform.SetParent(host.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = source.sortingOrder - 1;
            sr.enabled = false;
            t._copies[i] = sr;
        }
        return t;
    }

    void LateUpdate()
    {
        if (_source == null) return;
        Vector3 now = _source.transform.position;
        _trail.Record(now);
        for (int i = 0; i < 2; i++)
        {
            SpriteRenderer c = _copies[i];
            Vector3 past = _trail.Ago(0.045f * (i + 1), now);
            bool show = _source.enabled && !_source.forceRenderingOff && (past - now).sqrMagnitude > 0.004f;
            c.enabled = show;
            if (!show) continue;
            c.sprite = _source.sprite;
            c.transform.position = past;
            c.transform.rotation = _source.transform.rotation;
            Vector3 ls = _source.transform.lossyScale, ps = transform.lossyScale;
            c.transform.localScale = new Vector3(ls.x / Mathf.Max(0.0001f, ps.x), ls.y / Mathf.Max(0.0001f, ps.y), 1f);
            c.color = new Color(1f, 1f, 1f, i == 0 ? 0.45f : 0.2f);
        }
    }
}
