using UnityEngine;

/// <summary>
/// ステージの壁紙・床・壁・足場（フォルダ）を、そのステージのアプリの色（AppTheme）に塗り替える。
/// StageDirector が、ステージの計画（どのアプリか）が決まったときに Apply を呼ぶ。
/// シーンの中で、元の絵（下の4つ）を使っている SpriteRenderer を探して、色を入れ替えた絵に差し替える（形・大きさは同じ）。
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

    public void Apply(AppTheme theme)
    {
        if (theme == null) return;
        Current = theme;
        foreach (var sr in FindObjectsOfType<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;
            if (sr.sprite == _wallpaper) sr.sprite = ThemeRecolor.Stage(_wallpaper, theme, ThemeRecolor.Kind.Wallpaper);
            else if (sr.sprite == _floor) sr.sprite = ThemeRecolor.Stage(_floor, theme, ThemeRecolor.Kind.Floor);
            else if (sr.sprite == _wall) sr.sprite = ThemeRecolor.Stage(_wall, theme, ThemeRecolor.Kind.Wall);
            else if (sr.sprite == _folder) sr.sprite = ThemeRecolor.Stage(_folder, theme, ThemeRecolor.Kind.Folder);
            else if (_titleBars != null && System.Array.IndexOf(_titleBars, sr.sprite) >= 0) sr.sprite = ThemeRecolor.Stage(sr.sprite, theme, ThemeRecolor.Kind.TitleBar);
        }
        // 壁紙の外（画面の端が壁紙より広いとき）も空の色にする
        foreach (var cam in FindObjectsOfType<Camera>()) cam.backgroundColor = theme.Sky;
    }
}
