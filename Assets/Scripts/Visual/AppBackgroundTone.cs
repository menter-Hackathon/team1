using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】アプリの画面の背景の濃さと、後ろに敷く色を、その絵の明るさで決める（2026-11-20。まさぴ〜の依頼）。
/// 背景を白く薄くしたら（AppBackgroundFade）、何のアプリか分からなくなった。白ではなく紫の板を敷き、アプリが分かる濃さで少し沈める。
/// 足場（PlatformSlab の「厚みのある板」）だけが手前に浮いて見えるようにする。黒い雑魚が沈まないように、暗いアプリは暗くしない。
/// ・絵の平均の明るさ L（0〜255。0.299R＋0.587G＋0.114B）で分ける（数はどれも【仮】）：
///   明るいアプリ（L 140 以上。Slack・Excel・Notion・Chrome など）：濃い紫 #1E0830 の上に、濃さ clamp(101 / (L − 19), 0.50, 0.75)（平均の明るさが 120 くらいになる）
///   暗いアプリ（L 140 未満。Premiere Pro・Terminal・Spotify・Zoom など）：紫 #6A2A8C の上に、濃さ 0.85（明るさはほとんど変えない）
/// ・上の濃さは「絵の色どうしで混ぜた」ときの値。このプロジェクトはリニアの色空間なので、GPU の混ぜ方だと明るく出る。
///   そこで、絵の色どうしで混ぜたときの色に1ドットずつがいちばん近くなる濃さを、絵を間引いて探して決める（アプリごとに1回。覚えておく）
/// ・絵が読めない（Read/Write が無い）ときは false（StageTheme が今までの値を使う）
/// </summary>
public static class AppBackgroundTone
{
    const float LightFrom = 140f;          // 【仮】これ以上を明るいアプリとする
    const float TargetLuma = 120f;         // 【仮】明るいアプリを、平均の明るさこのくらいにする（濃さ = (120 − 19) / (L − 19)）
    const float BackingLuma = 19f;         // 濃い紫 #1E0830 の明るさ
    const float LightMin = 0.50f, LightMax = 0.75f; // 【仮】明るいアプリの濃さの幅
    const float DarkAlpha = 0.85f;         // 【仮】暗いアプリの濃さ
    const int Step = 4;                    // 4ドットおきに数える（320×180 → 80×45）

    static readonly Color DeepPurple = new Color32(0x1e, 0x08, 0x30, 0xff);
    static readonly Color Purple = new Color32(0x6a, 0x2a, 0x8c, 0xff);

    struct Tone
    {
        public bool Ok;
        public float Alpha;
        public Color Backing;
    }

    static readonly Dictionary<Sprite, Tone> Cache = new Dictionary<Sprite, Tone>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Cache.Clear();

    /// <summary>その背景の絵の濃さ（SpriteRenderer のアルファ）と、後ろに敷く色。決められなければ false</summary>
    public static bool TryFor(Sprite bg, out float alpha, out Color backing)
    {
        alpha = 1f;
        backing = DeepPurple;
        if (bg == null) return false;
        if (!Cache.TryGetValue(bg, out Tone t))
        {
            t = Measure(bg);
            Cache[bg] = t;
        }
        if (!t.Ok) return false;
        alpha = t.Alpha;
        backing = t.Backing;
        return true;
    }

    static Tone Measure(Sprite bg)
    {
        try
        {
            Texture2D tex = bg.texture;
            if (tex == null || !tex.isReadable) return default;
            Color32[] all = tex.GetPixels32();
            Rect r = bg.textureRect;
            int x0 = Mathf.FloorToInt(r.x), y0 = Mathf.FloorToInt(r.y);
            int x1 = Mathf.Min(tex.width, Mathf.FloorToInt(r.xMax)), y1 = Mathf.Min(tex.height, Mathf.FloorToInt(r.yMax));
            var px = new List<Color32>((x1 - x0) * (y1 - y0) / (Step * Step) + 1);
            double sum = 0;
            for (int y = y0; y < y1; y += Step)
            {
                for (int x = x0; x < x1; x += Step)
                {
                    Color32 c = all[y * tex.width + x];
                    px.Add(c);
                    sum += Luma(c.r, c.g, c.b);
                }
            }
            if (px.Count == 0) return default;
            float mean = (float)(sum / px.Count);
            bool light = mean >= LightFrom;
            Color backing = light ? DeepPurple : Purple;
            float srgbAlpha = light ? Mathf.Clamp((TargetLuma - BackingLuma) / Mathf.Max(1f, mean - BackingLuma), LightMin, LightMax) : DarkAlpha;
            float alpha = QualitySettings.activeColorSpace == ColorSpace.Linear ? LinearAlpha(px, backing, srgbAlpha) : srgbAlpha;
            if (Debug.isDebugBuild) Debug.Log($"[AppBackgroundTone] {bg.name}：明るさ {mean:0} → {(light ? "明るい" : "暗い")}・濃さ {srgbAlpha:0.00}（リニアで {alpha:0.00}）");
            return new Tone { Ok = true, Alpha = alpha, Backing = backing };
        }
        catch (System.Exception e)
        {
            Debug.LogException(e); // 決められなくても、ステージは今までの値で続ける
            return default;
        }
    }

    // 絵の色どうしで混ぜたとき（srgbAlpha）の色に、1ドットずつの色がいちばん近くなる、リニアの混ぜ方の濃さ（黄金分割で探す）。
    // 平均の明るさだけを合わせると、暗いアプリ（絵と紫の板の明るさが近い）で濃さが下がりすぎて、紫が強く出る（2026-11-20 に撮って分かった）
    static float LinearAlpha(List<Color32> px, Color backing, float srgbAlpha)
    {
        int n = px.Count;
        var lin = new Vector3[n];
        var target = new Vector3[n];
        Vector3 b255 = new Vector3(backing.r, backing.g, backing.b) * 255f;
        Vector3 bLin = new Vector3(Mathf.GammaToLinearSpace(backing.r), Mathf.GammaToLinearSpace(backing.g), Mathf.GammaToLinearSpace(backing.b));
        for (int i = 0; i < n; i++)
        {
            Color32 c = px[i];
            lin[i] = new Vector3(Mathf.GammaToLinearSpace(c.r / 255f), Mathf.GammaToLinearSpace(c.g / 255f), Mathf.GammaToLinearSpace(c.b / 255f));
            target[i] = Vector3.Lerp(b255, new Vector3(c.r, c.g, c.b), srgbAlpha);
        }
        float Error(float a)
        {
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 m = lin[i] * a + bLin * (1f - a);
                var shown = new Vector3(Mathf.LinearToGammaSpace(m.x), Mathf.LinearToGammaSpace(m.y), Mathf.LinearToGammaSpace(m.z)) * 255f;
                sum += (shown - target[i]).sqrMagnitude;
            }
            return (float)sum;
        }
        const float G = 0.618034f;
        float lo = 0f, hi = 1f;
        float x1 = hi - G * (hi - lo), x2 = lo + G * (hi - lo);
        float f1 = Error(x1), f2 = Error(x2);
        for (int k = 0; k < 24; k++)
        {
            if (f1 < f2) { hi = x2; x2 = x1; f2 = f1; x1 = hi - G * (hi - lo); f1 = Error(x1); }
            else { lo = x1; x1 = x2; f1 = f2; x2 = lo + G * (hi - lo); f2 = Error(x2); }
        }
        return Mathf.Clamp01((lo + hi) * 0.5f);
    }

    static float Luma(float r, float g, float b) => 0.299f * r + 0.587f * g + 0.114f * b;
}
