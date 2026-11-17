using UnityEngine;

/// <summary>
/// アプリのアイコンの色から作った、色の組み合わせ（トンマナ）。2026-11-17 まさぴ〜決定。
/// ・ステージ：壁紙・床・壁・足場（フォルダ）・ボスのゲージの色を、この色に塗り替える（StageTheme）
/// ・能力：その能力の枠の縁と効果の色（PixelHud・PlayerVfx）。敵の攻撃は赤系のまま（自分の能力と見分けるため）
/// ・ウイルス（雑魚・ボスの煙と顔）は、どのステージでも黒と赤のまま
/// 対応アプリ表（AppCatalog）の各アプリと、そのアプリの能力（SkillDefinition）が、同じアセットを指す。
/// 作り方：Project ウィンドウで右クリック ＞ Create ＞ Team1 ＞ App Theme
/// </summary>
[CreateAssetMenu(fileName = "Theme_", menuName = "Team1/App Theme")]
public class AppTheme : ScriptableObject
{
    [Header("壁紙")]
    [SerializeField] Color _sky = new Color32(0x42, 0x9f, 0xec, 0xff);
    [SerializeField] Color _cloud = Color.white;
    [Tooltip("丘の色。暗い色から明るい色の順（壁紙の緑を、明るさの順にこの色へ置き換える）")]
    [SerializeField] Color[] _hills;

    [Header("地形")]
    [SerializeField] Color _floor = new Color32(0x79, 0x79, 0x79, 0xff);
    [Tooltip("床の上の縁")]
    [SerializeField] Color _floorTop = new Color32(0xa6, 0xa6, 0xa6, 0xff);
    [SerializeField] Color _wall = new Color32(0x51, 0x51, 0x51, 0xff);
    [Tooltip("壁の縁")]
    [SerializeField] Color _wallHighlight = new Color32(0x79, 0x79, 0x79, 0xff);
    [Tooltip("足場（フォルダ）の色")]
    [SerializeField] Color _folder = new Color32(0xf5, 0xf1, 0x98, 0xff);
    [SerializeField] Color _folderShade = new Color32(0xb3, 0xb0, 0x6f, 0xff);
    [Tooltip("フォルダのタブの色")]
    [SerializeField] Color _folderTab = new Color32(0xf5, 0xf1, 0x98, 0xff);

    [Header("代表の色")]
    [Tooltip("いちばん代表的な色（能力の柱など、1色で塗る物）")]
    [SerializeField] Color _main = Color.white;
    [Tooltip("アイコンの色の並び（能力の枠の縁・ゲージ・新しいタブのウィンドウ）。4色なら上・右・下・左に分けて塗る")]
    [SerializeField] Color[] _accents;

    public Color Sky => _sky;
    public Color Cloud => _cloud;
    public Color[] Hills => _hills;
    public Color Floor => _floor;
    public Color FloorTop => _floorTop;
    public Color Wall => _wall;
    public Color WallHighlight => _wallHighlight;
    public Color Folder => _folder;
    public Color FolderShade => _folderShade;
    public Color FolderTab => _folderTab;
    public Color Main => _main;
    /// <summary>アクセントの色。空なら代表の色1つ</summary>
    public Color[] Accents => _accents != null && _accents.Length > 0 ? _accents : new[] { _main };

    /// <summary>
    /// アイコンの色から、ゲームの中で色の組み合わせを作る（対応表に無いアプリ用。2026-11-17）。アセットにはしない。
    /// 代表の色から、空・丘・床・足場の色を決める（手で決めた Chrome・VS Code の色の作り方に合わせる）
    /// </summary>
    public static AppTheme CreateRuntime(string themeName, Color main, Color[] accents)
    {
        var t = CreateInstance<AppTheme>();
        t.name = themeName;
        main.a = 1f;
        Color.RGBToHSV(main, out float h, out float s, out float v);
        t._main = main;
        t._accents = accents != null && accents.Length > 0 ? accents : new[] { main };
        t._sky = Color.HSVToRGB(h, Mathf.Min(s, 0.55f) * 0.9f, 0.32f);
        t._cloud = Color.Lerp(t._sky, Color.white, 0.25f);
        t._hills = new[]
        {
            Color.Lerp(main, Color.black, 0.75f),
            Color.Lerp(main, Color.black, 0.55f),
            Color.Lerp(main, Color.black, 0.3f),
            main,
            Color.Lerp(main, Color.white, 0.35f),
        };
        t._floor = new Color32(0x3d, 0x41, 0x4a, 0xff);
        t._floorTop = main;
        t._wall = new Color32(0x24, 0x27, 0x2e, 0xff);
        t._wallHighlight = new Color32(0x3d, 0x41, 0x4a, 0xff);
        Color folder = t._accents.Length > 1 ? t._accents[1] : Color.Lerp(main, Color.white, 0.3f);
        folder.a = 1f;
        t._folder = folder;
        t._folderShade = Color.Lerp(folder, Color.black, 0.25f);
        t._folderTab = main;
        return t;
    }
}
