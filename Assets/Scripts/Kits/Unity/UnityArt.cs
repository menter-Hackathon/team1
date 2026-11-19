using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Unity のキットの色と絵（2026-11-19。docs/app-kits-2-design.md 2-1）。担当A。
/// 物体は 3D のまま（Kit3D）。まわりの UI（矢印の帯・Hierarchy の行・Scene ビューの軸の印）はドット絵。
/// </summary>
public static class UnityArt
{
    /// <summary>物体の色（灰色がかった白。見本の動画と同じ）</summary>
    public static readonly Color Object = RuntimeSprites.Hex("D8D8D8");
    /// <summary>シェーダーが無いときのピンク</summary>
    public static readonly Color Missing = RuntimeSprites.Hex("FF00FF");
    /// <summary>エディタの地の色・行の色・選んだ行の青・字</summary>
    public static readonly Color EditorBg = RuntimeSprites.Hex("282828");
    public static readonly Color EditorRow = RuntimeSprites.Hex("383838");
    public static readonly Color EditorHeader = RuntimeSprites.Hex("3C3C3C");
    public static readonly Color EditorSelect = RuntimeSprites.Hex("2C5D87");
    public static readonly Color EditorText = RuntimeSprites.Hex("D2D2D2");
    /// <summary>軸の色（X 赤・Y 緑・Z 青）</summary>
    public static readonly Color AxisX = RuntimeSprites.Hex("E2463E");
    public static readonly Color AxisY = RuntimeSprites.Hex("8CD23C");
    public static readonly Color AxisZ = RuntimeSprites.Hex("3D84E6");

    static readonly Color32 White32 = new Color32(255, 255, 255, 255);
    static readonly Dictionary<char, Color32> Mono = new Dictionary<char, Color32> { { 'X', White32 } };

    static Sprite _chevron;

    /// <summary>進む向きの矢印「>」を並べた帯（右向き。白。並べて描く用に FullRect）</summary>
    public static Sprite Chevron
    {
        get
        {
            if (_chevron != null) return _chevron;
            string[] rows =
            {
                "XX..........",
                ".XX.........",
                "..XX........",
                "...XX.......",
                "....XX......",
                "...XX.......",
                "..XX........",
                ".XX.........",
                "XX..........",
            };
            int w = rows[0].Length, h = rows.Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Unity_Chevron",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[(h - 1 - y) * w + x] = rows[y][x] == 'X' ? White32 : new Color32(255, 255, 255, 0);
            tex.SetPixels32(px);
            tex.Apply();
            _chevron = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), RuntimeSprites.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            _chevron.name = "Unity_Chevron";
            _chevron.hideFlags = HideFlags.DontSave;
            return _chevron;
        }
    }

    /// <summary>四角の枠（太さ 2ドット。白）。w・h はドット</summary>
    public static Sprite FrameBox(int w, int h)
    {
        w = Mathf.Max(6, w); h = Mathf.Max(6, h);
        var rows = new string[h];
        var line = new char[w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++) line[x] = x < 2 || x >= w - 2 || y < 2 || y >= h - 2 ? 'X' : '.';
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii($"Unity_Frame_{w}x{h}", rows, Mono);
    }

    /// <summary>Console のエラーの印（赤い丸に「!」。9ドット）</summary>
    public static Sprite ErrorIcon => RuntimeSprites.FromAscii("Unity_ErrorIcon", new[]
    {
        "..RRRRR..",
        ".RRRWRRR.",
        "RRRRWRRRR",
        "RRRRWRRRR",
        "RRRRWRRRR",
        "RRRRRRRRR",
        "RRRRWRRRR",
        ".RRRRRRR.",
        "..RRRRR..",
    }, new Dictionary<char, Color32> { { 'R', new Color32(0xE2, 0x46, 0x3E, 0xFF) }, { 'W', White32 } });

    /// <summary>白い小さい四角（床にこすれるドット）</summary>
    public static Sprite Dot => PlaceholderSprites.Square;
}

/// <summary>
/// 【仮】Unity のエディタのまねの UI（OnGUI。2026-11-19）。担当A。
/// ・左上（HUD の下）：Hierarchy の小さいパネル。3D の物体が出るたびに「Cube (3)」のような行が増える（ギミックの間だけ）
/// ・右上（HUD の下）：Scene ビューの軸の印（赤・緑・青の矢）（ギミックの間だけ）
/// ・能力のキューブ投げ：「+ Cube」の行を 0.6秒（ギミックが無いステージでも出る）
/// 説明の文字は出さない（実在の UI をまねた短い英語だけ）
/// </summary>
public class UnityHud : MonoBehaviour
{
    const int MaxRows = 4;
    const float RowH = 10f;
    const float PanelW = 92f;

    static UnityHud _current;

    /// <summary>ギミックのパネル（Hierarchy・軸）を出すか</summary>
    public bool ShowPanel { get; set; }

    readonly List<string> _rows = new List<string>();
    float _rowFlashUntil;
    string _ping;
    float _pingUntil;
    readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

    /// <summary>このシーンの物（無ければ作る）</summary>
    public static UnityHud Ensure()
    {
        if (_current != null) return _current;
        var go = new GameObject("UnityHud");
        _current = go.AddComponent<UnityHud>();
        return _current;
    }

    /// <summary>短い行を出す（能力の「+ Cube」）</summary>
    public static void Ping(string text, float seconds)
    {
        UnityHud h = Ensure();
        h._ping = text;
        h._pingUntil = Time.time + seconds;
    }

    /// <summary>Hierarchy に行を足す（"Cube" → "Cube (3)"）</summary>
    public void AddObject(string baseName)
    {
        _counts.TryGetValue(baseName, out int n);
        _counts[baseName] = n + 1;
        _rows.Add(n == 0 ? baseName : $"{baseName} ({n})");
        while (_rows.Count > MaxRows) _rows.RemoveAt(0);
        _rowFlashUntil = Time.time + 0.6f;
    }

    void OnDestroy()
    {
        if (_current == this) _current = null;
    }

    void OnGUI()
    {
        bool ping = Time.time < _pingUntil && !string.IsNullOrEmpty(_ping);
        if (!ShowPanel && !ping) return;
        GameSession session = GameSession.Instance;
        if (session != null && (session.IsTransitioning || session.IsGameOver)) return;
        GUI.depth = 12; // HUD（10）より奥
        PixelGui.Begin(false);
        float x = 6f, y = 76f;
        if (ShowPanel)
        {
            y = DrawHierarchy(x, y);
            DrawAxes(PixelGui.Width - 30f, 82f);
        }
        if (ping) DrawRow(x, y, PanelW, _ping, true);
        PixelGui.End();
    }

    float DrawHierarchy(float x, float y)
    {
        int n = 2 + _rows.Count;
        PixelGui.Fill(new Rect(x - 1f, y - 1f, PanelW + 2f, RowH * (n + 1) + 2f), Color.black);
        PixelGui.Fill(new Rect(x, y, PanelW, RowH), UnityArt.EditorHeader);
        PixelGui.TextEn(new Rect(x + 3f, y + 1f, PanelW - 4f, RowH - 1f), "Hierarchy", 8, UnityArt.EditorText);
        y += RowH;
        DrawRow(x, y, PanelW, "Main Camera", false); y += RowH;
        DrawRow(x, y, PanelW, "Directional Light", false); y += RowH;
        for (int i = 0; i < _rows.Count; i++)
        {
            bool sel = i == _rows.Count - 1 && Time.time < _rowFlashUntil;
            DrawRow(x, y, PanelW, _rows[i], sel);
            y += RowH;
        }
        return y + 3f;
    }

    static void DrawRow(float x, float y, float w, string text, bool selected)
    {
        PixelGui.Fill(new Rect(x, y, w, RowH), selected ? UnityArt.EditorSelect : UnityArt.EditorBg);
        // 立方体のアイコン（小さい四角）
        PixelGui.Fill(new Rect(x + 3f, y + 2f, 6f, 6f), UnityArt.EditorText);
        PixelGui.Fill(new Rect(x + 4f, y + 3f, 4f, 4f), selected ? UnityArt.EditorSelect : UnityArt.EditorBg);
        PixelGui.TextEn(new Rect(x + 12f, y + 1f, w - 13f, RowH - 1f), text, 8, UnityArt.EditorText);
    }

    // Scene ビューの軸の印（Kit3D の共通の絵）
    static void DrawAxes(float cx, float cy) => Kit3DArt.DrawAxesGui(cx, cy);
}
