using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Blender のキットの色と絵（2026-11-19。docs/app-kits-2-design.md 3-5）。担当A。
/// 物体は 3D のまま（Kit3D。灰色 #B4B4B4）。選んでいる物はオレンジ #FF9F0A の輪かく（少し大きい同じ形を後ろに）。
/// キーの印（G・X・Z・S）はドット絵のキーキャップ。
/// </summary>
public static class BlenderArt
{
    public static readonly Color Object = RuntimeSprites.Hex("B4B4B4");
    public static readonly Color Select = RuntimeSprites.Hex("FF9F0A");
    public static readonly Color Eye = RuntimeSprites.Hex("2E2E2E");
    public static readonly Color MenuBg = RuntimeSprites.Hex("303030");
    public static readonly Color MenuText = RuntimeSprites.Hex("E6E6E6");
    public static readonly Color MenuSelect = RuntimeSprites.Hex("4772B3");

    static readonly Dictionary<char, Color32> KeyPal = new Dictionary<char, Color32>
    {
        { 'E', new Color32(0x1C, 0x1C, 0x1E, 0xFF) },
        { 'F', new Color32(0xE5, 0xE5, 0xEA, 0xFF) },
        { 'S', new Color32(0xA0, 0xA0, 0xA5, 0xFF) },
    };

    /// <summary>キーキャップの地（14×14 ドット）</summary>
    public static Sprite KeyCap => RuntimeSprites.FromAscii("Blender_KeyCap", new[]
    {
        ".EEEEEEEEEEEE.",
        "EFFFFFFFFFFFFE",
        "EFFFFFFFFFFFFE",
        "EFFFFFFFFFFFFE",
        "EFFFFFFFFFFFFE",
        "EFFFFFFFFFFFFE",
        "EFFFFFFFFFFFFE",
        "EFFFFFFFFFFFFE",
        "EFFFFFFFFFFFFE",
        "EFFFFFFFFFFFFE",
        "ESSSSSSSSSSSSE",
        "ESSSSSSSSSSSSE",
        "EEEEEEEEEEEEEE",
        ".EEEEEEEEEEEE.",
    }, KeyPal);

    /// <summary>キーの印を1つ置く（parent の子。pos は親からの位置）</summary>
    public static Transform Key(Transform parent, string letter, Vector2 pos, int order = 22)
    {
        var go = new GameObject("Key_" + letter);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        RuntimeSprites.Show(go.transform, KeyCap, Vector2.zero, order, 1f, "Cap");
        TextMesh tm = WorldText.Create(go.transform, letter, 0.4f, MacUi.KeyInk, order + 1, true);
        tm.transform.localPosition = new Vector3(0.02f, 0.06f, -0.001f);
        return go.transform;
    }

    /// <summary>キーの印を2つ並べる（"G","X"）</summary>
    public static Transform Keys(Transform parent, Vector2 pos, params string[] letters)
    {
        var go = new GameObject("Keys");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        float w = 0.9f;
        for (int i = 0; i < letters.Length; i++) Key(go.transform, letters[i], new Vector2((i - (letters.Length - 1) * 0.5f) * w, 0f));
        return go.transform;
    }

    /// <summary>赤い細い線（予告）を置く。from から to へ。太さ 2ドット</summary>
    public static SpriteRenderer Line(Transform parent, Vector2 from, Vector2 to, Color color, int order = 17)
    {
        Vector2 d = to - from;
        var go = new GameObject("Line");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.position = (from + to) * 0.5f;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        return AttackRunner.AddVisual(go.transform, PlaceholderSprites.Square, color, new Vector2(Mathf.Max(0.1f, d.magnitude), 2f * Kit3D.Px), order, "Red");
    }

    /// <summary>サルの頭（Suzanne）の 3D：球1つ＋耳の球2つ＋暗い目の球2つ。d は頭の直径</summary>
    public static Kit3DVisual Suzanne(Transform parent, float d, GameObject root)
    {
        Kit3DVisual v = Kit3D.AddVisual(parent, Kit3DShape.Sphere, Vector3.one * d, Object, root, "blender.boss", 8);
        float r = d * 0.5f;
        v.AddPart(Kit3DShape.Sphere, new Vector3(-r * 1.0f, r * 0.25f, 0f), Vector3.one * d * 0.38f, Object);
        v.AddPart(Kit3DShape.Sphere, new Vector3(r * 1.0f, r * 0.25f, 0f), Vector3.one * d * 0.38f, Object);
        v.AddPart(Kit3DShape.Sphere, new Vector3(-r * 0.36f, r * 0.2f, -r * 0.86f), Vector3.one * d * 0.2f, Eye);
        v.AddPart(Kit3DShape.Sphere, new Vector3(r * 0.36f, r * 0.2f, -r * 0.86f), Vector3.one * d * 0.2f, Eye);
        // 口のふくらみ
        v.AddPart(Kit3DShape.Sphere, new Vector3(0f, -r * 0.35f, -r * 0.62f), new Vector3(d * 0.5f, d * 0.3f, d * 0.4f), Object);
        return v;
    }
}

/// <summary>
/// 【仮】Blender のまねの UI（OnGUI。2026-11-19）。担当A。
/// ・右上（HUD の下）：軸の印（ギミックの間だけ）
/// ・Default Cube を消したとき：小さいメニュー「Delete」を 0.6秒（ゲームの世界の位置に）
/// </summary>
public class BlenderHud : MonoBehaviour
{
    static BlenderHud _current;

    public bool ShowAxes { get; set; }

    Vector2 _menuWorld;
    float _menuUntil;

    public static BlenderHud Ensure()
    {
        if (_current != null) return _current;
        var go = new GameObject("BlenderHud");
        _current = go.AddComponent<BlenderHud>();
        return _current;
    }

    /// <summary>「Delete」の小さいメニューを出す（world の位置）</summary>
    public static void DeleteMenu(Vector2 world, float seconds)
    {
        BlenderHud h = Ensure();
        h._menuWorld = world;
        h._menuUntil = Time.time + seconds;
    }

    void OnDestroy()
    {
        if (_current == this) _current = null;
    }

    void OnGUI()
    {
        bool menu = Time.time < _menuUntil;
        if (!ShowAxes && !menu) return;
        GameSession session = GameSession.Instance;
        if (session != null && (session.IsTransitioning || session.IsGameOver)) return;
        GUI.depth = 12;
        PixelGui.Begin(false);
        if (ShowAxes) Kit3DArt.DrawAxesGui(PixelGui.Width - 30f, 82f);
        if (menu && ScreenUi.WorldToGui(_menuWorld) is Vector2 g)
        {
            var r = new Rect(g.x - 30f, g.y - 22f, 60f, 22f);
            PixelGui.Fill(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), Color.black);
            PixelGui.Fill(r, BlenderArt.MenuBg);
            PixelGui.TextEn(new Rect(r.x + 3f, r.y + 1f, r.width - 4f, 10f), "Delete?", 8, BlenderArt.MenuText);
            PixelGui.Fill(new Rect(r.x + 1f, r.y + 11f, r.width - 2f, 10f), BlenderArt.MenuSelect);
            PixelGui.TextEn(new Rect(r.x + 3f, r.y + 12f, r.width - 4f, 9f), "Delete", 8, Color.white);
        }
        PixelGui.End();
    }
}
