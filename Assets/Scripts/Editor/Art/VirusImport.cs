using UnityEditor;
using UnityEngine;

/// <summary>
/// ウイルスの絵の取り込み設定（2026-11-20。docs/virus-types-design.md 6章）。
/// Tools/art/virus/build_virus.py が Assets/Resources/Virus/*.png を書いたときに、自動で入れる。
/// ・テクスチャ1枚（切り分けは実行時に VirusArt がする）・1マス 18 ドット
/// ・ドット絵なので、ぼかさない（Point）・ミップマップなし・圧縮しない・端はくり返さない（Clamp）・透明あり
/// </summary>
public class VirusImport : AssetPostprocessor
{
    const string Folder = "Assets/Resources/Virus/";

    // 設定を変えたら数を上げる（上げると、このフォルダの絵を全部取り込み直す）
    public override uint GetVersion() => 1;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder) || !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return;
        var importer = assetImporter as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude = 0;
        importer.SetTextureSettings(settings);

        importer.spritePixelsPerUnit = 18f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.sRGBTexture = true;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;

        var standalone = importer.GetPlatformTextureSettings("Standalone");
        if (standalone.overridden)
        {
            standalone.overridden = false;
            importer.SetPlatformTextureSettings(standalone);
        }
    }
}
