using UnityEngine;

/// <summary>
/// ステージの壁紙・床・壁・足場（フォルダ）を、そのステージのアプリの色（AppTheme）に塗り替える。
/// StageDirector が、ステージの計画（どのアプリか）が決まったときに Apply を呼ぶ。
/// シーンの中で、元の絵（下の4つ）を使っている SpriteRenderer を探して、色を入れ替えた絵に差し替える（形・大きさは同じ）。
/// 2026-11-19：そのアプリの画面（UI）の背景の絵（AppBackgrounds）があれば、壁紙はその絵にする（同じ 320×180 ドット）。
///   そのときは、部屋の中の絵（VS Code のステージの Room_Editor）も消す（画面のほとんどをかくしてしまうため）
/// </summary>
public class StageTheme : MonoBehaviour
{
    [Tooltip("元の絵（シーンの作成ツールが入れる）")]
    [SerializeField] Sprite _wallpaper;
    [SerializeField] Sprite _floor;
    [SerializeField] Sprite _wall;
    [SerializeField] Sprite _folder;
    [Tooltip("タイトルバーをアプリの色にする絵（ウィンドウの足場・部屋のタイトルバー）")]
    [SerializeField] Sprite[] _titleBars;

    /// <summary>今の色。塗り替えていなければ null</summary>
    public AppTheme Current { get; private set; }

    /// <summary>塗り替える。appBackground：そのアプリの背景の絵（無ければ null＝壁紙を色を変えて出す）</summary>
    public void Apply(AppTheme theme, Sprite appBackground = null)
    {
        if (theme == null && appBackground == null) return;
        if (theme != null) Current = theme;
        foreach (var sr in FindObjectsOfType<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;
            if (sr.sprite == _wallpaper)
            {
                if (appBackground != null) sr.sprite = appBackground;
                else sr.sprite = ThemeRecolor.Stage(_wallpaper, theme, ThemeRecolor.Kind.Wallpaper);
                continue;
            }
            if (appBackground != null && IsRoomInside(sr))
            {
                sr.enabled = false;
                continue;
            }
            if (theme == null) continue;
            if (sr.sprite == _floor) sr.sprite = ThemeRecolor.Stage(_floor, theme, ThemeRecolor.Kind.Floor);
            else if (sr.sprite == _wall) sr.sprite = ThemeRecolor.Stage(_wall, theme, ThemeRecolor.Kind.Wall);
            else if (sr.sprite == _folder) sr.sprite = ThemeRecolor.Stage(_folder, theme, ThemeRecolor.Kind.Folder);
            else if (_titleBars != null && System.Array.IndexOf(_titleBars, sr.sprite) >= 0) sr.sprite = ThemeRecolor.Stage(sr.sprite, theme, ThemeRecolor.Kind.TitleBar);
        }
        // 壁紙の外（画面の端が壁紙より広いとき）も空の色にする
        if (theme != null) foreach (var cam in FindObjectsOfType<Camera>()) cam.backgroundColor = theme.Sky;
    }

    // 部屋の中の絵（StageLayoutsBuilder の RoomLook が作る Room_*/Inside。絵は Room_Editor）
    static bool IsRoomInside(SpriteRenderer sr)
    {
        if (sr.sprite.name == "Room_Editor") return true;
        Transform parent = sr.transform.parent;
        return sr.gameObject.name == "Inside" && parent != null && parent.name.StartsWith("Room_");
    }
}
