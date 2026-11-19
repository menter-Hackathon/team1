using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Minecraft のキットの色と絵（2026-11-19。docs/app-kits-2-design.md 3-6）。担当A。
/// ・ブロックは 3D の立方体（Kit3D。1マス。べた塗り）：土 #8B5A2B、砂 #E0D3A0、TNT #D93A2B に白い帯、緑 #5DB85D
/// ・ロゴ・本物の絵は使わない（【要相談③】のかっこ内：形と色だけ寄せる）
/// </summary>
public static class MinecraftArt
{
    public static readonly Color Dirt = RuntimeSprites.Hex("8B5A2B");
    public static readonly Color Sand = RuntimeSprites.Hex("E0D3A0");
    public static readonly Color Tnt = RuntimeSprites.Hex("D93A2B");
    public static readonly Color Band = RuntimeSprites.Hex("F2F2F2");
    public static readonly Color Green = RuntimeSprites.Hex("5DB85D");
    public static readonly Color Face = RuntimeSprites.Hex("1E2A1E");
    public static readonly Color Night = new Color(0.05f, 0.08f, 0.28f, 1f);
    public static readonly Color SunYellow = RuntimeSprites.Hex("FFE066");
    public static readonly Color MoonGrey = RuntimeSprites.Hex("DADADA");

    static readonly Color32 White32 = new Color32(255, 255, 255, 255);
    static readonly Dictionary<char, Color32> Mono = new Dictionary<char, Color32> { { 'X', White32 } };

    /// <summary>TNT の 3D（赤い立方体＋白い帯）</summary>
    public static Kit3DVisual TntVisual(Transform parent, float size, GameObject root, string group = null)
    {
        Kit3DVisual v = Kit3D.AddVisual(parent, Kit3DShape.Cube, Vector3.one * size, Tnt, root, group, 12);
        v.AddPart(Kit3DShape.Cube, Vector3.zero, new Vector3(size * 1.04f, size * 0.36f, size * 1.04f), Band);
        return v;
    }

    /// <summary>緑のブロックの生き物の 3D（緑の立方体 2つを積んだ形・暗い四角の顔）。size は立方体1つ。足元が親の位置</summary>
    public static Kit3DVisual CreeperVisual(Transform parent, float size, GameObject root)
    {
        var holder = new GameObject("Body");
        holder.layer = parent.gameObject.layer;
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = new Vector3(0f, size, 0f);
        Kit3DVisual v = Kit3D.AddVisual(holder.transform, Kit3DShape.Cube, Vector3.one * size, Green, root, "minecraft.boss", 12);
        v.transform.localPosition = new Vector3(0f, -size * 0.5f, v.transform.localPosition.z);
        v.AddPart(Kit3DShape.Cube, new Vector3(0f, size, 0f), Vector3.one * size, Green);
        float f = -size * 0.51f; // 前の面（画面側）
        v.AddPart(Kit3DShape.Cube, new Vector3(-size * 0.22f, size * 1.12f, f), new Vector3(size * 0.2f, size * 0.2f, 0.04f), Face);
        v.AddPart(Kit3DShape.Cube, new Vector3(size * 0.22f, size * 1.12f, f), new Vector3(size * 0.2f, size * 0.2f, 0.04f), Face);
        v.AddPart(Kit3DShape.Cube, new Vector3(0f, size * 0.86f, f), new Vector3(size * 0.2f, size * 0.3f, 0.04f), Face);
        return v;
    }

    /// <summary>輪（直径 d ドット、太さ t ドット。白）</summary>
    public static Sprite Ring(int d, int t)
    {
        d = Mathf.Max(4, d);
        var rows = new string[d];
        var line = new char[d];
        float r = d * 0.5f;
        for (int y = 0; y < d; y++)
        {
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                line[x] = dist <= r && dist > r - t - 0.25f ? 'X' : '.';
            }
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii($"Minecraft_Ring_{d}_{t}", rows, Mono);
    }

    /// <summary>月の印（8ドット。HUD の倍率の印）</summary>
    public static Sprite MoonIcon => RuntimeSprites.FromAscii("Minecraft_MoonIcon", new[]
    {
        "MMMMMMMM",
        "MMMMMMMM",
        "MMDMMMMM",
        "MMMMMDMM",
        "MMMMMMMM",
        "MDMMMMMM",
        "MMMMDMMM",
        "MMMMMMMM",
    }, new Dictionary<char, Color32> { { 'M', new Color32(0xDA, 0xDA, 0xDA, 0xFF) }, { 'D', new Color32(0x9A, 0x9A, 0xA8, 0xFF) } });

    /// <summary>爆発の見た目（白とオレンジの四角が広がる・白い円が光る）。parent の子ではなく、その場に残る</summary>
    public static void Explosion(Vector2 at, float radius)
    {
        var go = new GameObject("MinecraftBoom");
        go.transform.position = at;
        go.AddComponent<MinecraftBoom>().Setup(radius);
    }
}

/// <summary>爆発の見た目（すぐ消える）</summary>
public class MinecraftBoom : MonoBehaviour
{
    SpriteRenderer _flash;
    readonly List<(Transform t, Vector2 v)> _bits = new List<(Transform, Vector2)>();
    float _born, _r;

    internal void Setup(float radius)
    {
        _born = Time.time;
        _r = radius;
        _flash = AttackRunner.AddVisual(transform, PlaceholderSprites.Circle, new Color(1f, 1f, 1f, 0.85f), Vector2.one * radius * 2f, 46, "Flash");
        Color[] cols = { Color.white, new Color(1f, 0.75f, 0.3f), new Color(0.6f, 0.6f, 0.6f) };
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            var b = new GameObject("Bit");
            b.transform.SetParent(transform, false);
            float s = Random.Range(0.25f, 0.5f);
            AttackRunner.AddVisual(b.transform, PlaceholderSprites.Square, cols[i % cols.Length], Vector2.one * s, 47, "Sq");
            _bits.Add((b.transform, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(4f, 8f)));
        }
        StageCamera.Shake(0.15f, 0.2f);
    }

    void Update()
    {
        float t = Time.time - _born;
        if (t > 0.45f) { Destroy(gameObject); return; }
        if (_flash != null)
        {
            Color c = _flash.color;
            c.a = Mathf.Max(0f, 0.85f - t * 4f);
            _flash.color = c;
        }
        foreach (var (tr, v) in _bits) if (tr != null) tr.localPosition += (Vector3)(v * Time.deltaTime * (1f - t / 0.45f));
    }
}

/// <summary>
/// 【仮】Minecraft のまねの空（OnGUI。2026-11-19）。担当A。四角い太陽と月が、画面の上（HUD の下）を横切る（時計の代わり）
/// </summary>
public class MinecraftSky : MonoBehaviour
{
    /// <summary>0〜1：今の昼（または夜）の進み</summary>
    public float Progress { get; set; }
    public bool IsNight { get; set; }

    void OnGUI()
    {
        GameSession session = GameSession.Instance;
        if (session != null && (session.IsTransitioning || session.IsGameOver)) return;
        GUI.depth = 12;
        PixelGui.Begin(false);
        float w = PixelGui.Width;
        float x = Mathf.Lerp(150f, w - 150f, Progress);
        float y = 64f - Mathf.Sin(Progress * Mathf.PI) * 10f;
        const float s = 14f;
        if (!IsNight)
        {
            PixelGui.Fill(new Rect(x - s * 0.5f - 1f, y - 1f, s + 2f, s + 2f), new Color(1f, 0.85f, 0.3f, 0.5f));
            PixelGui.Fill(new Rect(x - s * 0.5f, y, s, s), MinecraftArt.SunYellow);
            PixelGui.Fill(new Rect(x - s * 0.5f + 3f, y + 3f, s - 6f, s - 6f), new Color(1f, 0.96f, 0.7f));
        }
        else
        {
            PixelGui.Fill(new Rect(x - s * 0.5f, y, s, s), MinecraftArt.MoonGrey);
            PixelGui.Fill(new Rect(x - s * 0.5f + 3f, y + 3f, 3f, 3f), new Color(0.6f, 0.6f, 0.66f));
            PixelGui.Fill(new Rect(x + 1f, y + 7f, 4f, 3f), new Color(0.6f, 0.6f, 0.66f));
        }
        PixelGui.End();
    }
}
