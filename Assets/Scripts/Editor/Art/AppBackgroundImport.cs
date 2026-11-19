using UnityEditor;
using UnityEngine;

/// <summary>
/// アプリの画面（UI）の背景の絵の取り込み設定（2026-11-19。Tools/art/appbg/FORMAT.md）。
/// Tools/art/appbg/render.py が Assets/Resources/AppBackgrounds/Bg_&lt;ラベル&gt;.png を書いたときに、自動で入れる。
/// 足場の絵（同じフォルダの下の Parts/&lt;ラベル&gt;__&lt;id&gt;.png。AppStagePlatforms）も、同じ設定で入る（フォルダの先頭で見ているため）。
/// ・スプライト1枚・1マス 15 ドット（今の壁紙 Bg_Desktop と同じ大きさ 21.33×12 マスになる）・中心が基準
/// ・ドット絵なので、ぼかさない（Point）・ミップマップなし・圧縮しない・端はくり返さない（Clamp）・透明あり
/// </summary>
public class AppBackgroundImport : AssetPostprocessor
{
    const string Folder = "Assets/Resources/AppBackgrounds/";

    // 設定を変えたら数を上げる（上げると、このフォルダの絵を全部取り込み直す）
    public override uint GetVersion() => 2; // 2：背景（Bg_*）を読めるように（main の DesktopDecay の感染ノイズが、背景の絵を読むため）

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder) || !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return;
        var importer = assetImporter as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;

        // 基準は中心・四角いメッシュ（すき間を切らない）
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude = 0;
        importer.SetTextureSettings(settings);

        importer.spritePixelsPerUnit = 15f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.sRGBTexture = true;
        // 背景（Bg_*）は読めるようにする：main の DesktopDecay（感染ノイズ）が背景の絵の点を読んで、崩れた絵を作るため（2026-11-19）。足場の絵（Parts）は読まない
        importer.isReadable = !assetPath.Contains("/Parts/");
        if (importer.maxTextureSize < 512) importer.maxTextureSize = 512;

        // Standalone の個別の設定は外す（ふつうの設定がそのまま使われる）
        var standalone = importer.GetPlatformTextureSettings("Standalone");
        if (standalone.overridden)
        {
            standalone.overridden = false;
            importer.SetPlatformTextureSettings(standalone);
        }
    }
}
