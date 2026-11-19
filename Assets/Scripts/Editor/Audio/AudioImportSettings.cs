using UnityEditor;
using UnityEngine;

/// <summary>
/// 音の取り込み設定（2026-11-19）。Python で作った音（Tools/audio/build_audio.py）を置いたときに、自動で入れる。
/// ・Assets/Resources/Audio/SE/：短い音。読み込むときに展開（DecompressOnLoad）・PCM・先に読んでおく（すぐ鳴らせる）
/// ・Assets/Resources/Audio/BGM/：曲。圧縮のままメモリに置く（CompressedInMemory）・Vorbis（品質 0.5）・裏で読み込む
///   ジングル（jingle_*）は短いので、裏で読み込まない（呼んだ瞬間に鳴らせる）
/// 音のファイルは Python がモノラルで書くので、Unity のモノラル化（音量のそろえ直しが付いてくる）は使わない。
/// 設定はふつうの設定（Default）に入れ、Standalone の個別の設定は外す（ふつうの設定がそのまま使われる）
/// </summary>
public class AudioImportSettings : AssetPostprocessor
{
    const string SeFolder = "Assets/Resources/Audio/SE/";
    const string BgmFolder = "Assets/Resources/Audio/BGM/";

    // 設定を変えたら数を上げる（上げると、この音のファイルを全部取り込み直す）
    public override uint GetVersion() => 2;

    void OnPreprocessAudio()
    {
        bool se = assetPath.StartsWith(SeFolder);
        bool bgm = assetPath.StartsWith(BgmFolder);
        if (!se && !bgm) return;
        var importer = assetImporter as AudioImporter;
        if (importer == null) return;

        // 音のファイルは初めからモノラル。forceToMono をオンにすると Unity が音量をそろえ直して（normalize）、
        // レシピでわざと小さくした音（溜めの音など）や静かな曲の大きさが消えるので、オフにしておく
        importer.forceToMono = false;
        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        s.preloadAudioData = true;
        if (se)
        {
            importer.loadInBackground = false;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.PCM;
        }
        else
        {
            bool jingle = System.IO.Path.GetFileName(assetPath).StartsWith("jingle_");
            importer.loadInBackground = !jingle;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.5f;
        }
        importer.defaultSampleSettings = s;
        if (importer.ContainsSampleSettingsOverride("Standalone")) importer.ClearSampleSettingOverride("Standalone");
    }
}
