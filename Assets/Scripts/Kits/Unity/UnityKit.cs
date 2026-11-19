using UnityEngine;

/// <summary>
/// 【仮】Unity のキットの実行時の登録（2026-11-19。docs/app-kits-2-design.md 2-1）。担当A。
/// ・ラベル "Unity"（Editor/Kits/UnityKitAssets.cs の Label と同じ）。Unity Hub も同じキット（【要相談⑥】のかっこ内）
/// ・ステージの決まり：UnityRule（ランダムな座標に 3D の物体）
/// </summary>
public static class UnityKit
{
    public const string Label = "Unity";
    public static readonly string[] Names = { "Unity", "Unity Hub" };
    public static readonly string[] Bundles = { "com.unity3d." };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        AppKits.Register(Label, Names, Bundles);
        StageRules.Register(Label, () => new UnityRule());
    }
}
