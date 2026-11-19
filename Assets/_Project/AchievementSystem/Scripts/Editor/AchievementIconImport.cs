using UnityEditor;
using UnityEngine;

namespace Team1.Achievements.EditorTools
{
    /// <summary>
    /// 実績のアイコン（Icons・Locked・Frames）のインポート設定：
    /// Sprite (2D and UI) / Filter Mode = Point / Compression = None / Mip Maps オフ / PPU はプレイヤーと同じ（54：3倍書き出しのプレイヤーの絵と同じ）
    /// </summary>
    public class AchievementIconImport : AssetPostprocessor
    {
        /// <summary>プレイヤーのスプライト（Sprites/Generated/Player、3倍書き出し）と同じ</summary>
        public const float PixelsPerUnit = 54f;

        static readonly string[] Folders =
        {
            AchievementCsvImporter.Root + "/Icons/",
            AchievementCsvImporter.Root + "/Locked/",
            AchievementCsvImporter.Root + "/Frames/",
        };

        static bool IsTarget(string path)
        {
            foreach (var f in Folders)
                if (path.StartsWith(f) && path.EndsWith(".png")) return true;
            return false;
        }

        void OnPreprocessTexture()
        {
            if (!IsTarget(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = PixelsPerUnit;
        }

        [MenuItem("Tools/Achievements/アイコンのインポート設定をやり直す", priority = 1)]
        public static void ReimportAll()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { AchievementCsvImporter.Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsTarget(path)) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool ok = importer.textureType == TextureImporterType.Sprite && importer.filterMode == FilterMode.Point &&
                          importer.textureCompression == TextureImporterCompression.Uncompressed && !importer.mipmapEnabled &&
                          Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit);
                if (!ok) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.Refresh();
        }
    }
}
