using System;
using System.Collections.Generic;

/// <summary>
/// 【仮】能力の説明とタグ（能力えらびのカードに出す。2026-11-18：画面のイメージから）。
/// 能力の名前で引く。その場で作った能力は、ひな形の名前がそのまま能力の名前なので、ひな形の説明が出る。
/// 文はすべて【仮】。企画・UI 担当が決めたら書き換える（能力のアセットに持たせてもよい）。
/// </summary>
public static class SkillTexts
{
    static readonly Dictionary<string, (string description, string[] tags)> Texts = new Dictionary<string, (string, string[])>
    {
        // カテゴリの能力
        { "新しいタブ", ("前方へ瞬間移動する。空中でも使える。", new[] { "移動", "空中OK" }) },
        { "ブレークポイント", ("前方の床に印を出し、縦の柱で攻撃する。", new[] { "範囲", "対空" }) },
        { "棒グラフ", ("前方に背の高い柱を3本、手前から順に立てる。", new[] { "連続", "対空" }) },
        { "バケツ塗り", ("前方の床を絵の具で塗り、しばらく当たり続ける。", new[] { "設置", "持続" }) },
        { "ボム", ("斜め上へ投げる。当たると爆発する。", new[] { "投てき", "範囲" }) },
        // アプリ専用の能力（2026-11-18）
        { "ギャラリービュー", ("画面に映る敵をまとめてロックオンし、レーザーを撃つ。", new[] { "ロックオン", "広範囲" }) },
        { "zip圧縮", ("目の前の敵と弾をzipにまとめ、もう一度押すと撃つ。まとめた数だけ強くなる。", new[] { "圧縮", "2回押し" }) }, // 2026-11-19：app-feedback-v2 4-2
        // 2026-11-19：docs/app-feedback-v2-design.md（5つのアプリの直し）
        { "恐竜ラン", ("オフラインの恐竜が前へ走り、敵をはね飛ばす。", new[] { "突進", "貫通" }) },
        { "リップル削除", ("前方の区間を切り取って消す。先にいた敵は、手前に詰まる。", new[] { "範囲", "引き寄せ" }) },
        { "rm -rf", ("床をはう文字化けの波で、敵と弾を消す。", new[] { "貫通", "弾を消す" }) },
        { "エフェクトアイコン投げ", ("エフェクトのアイコンを投げる。投げるたびに、次のエフェクトが変わる。", new[] { "投げる", "ランダム" }) },
        // その場で作る能力のひな形
        { "ピラー", ("前方に縦の柱を立てて攻撃する。", new[] { "範囲", "対空" }) },
        { "連続ピラー", ("前方に柱をいくつも立てる。", new[] { "連続", "対空" }) },
        { "ペイント", ("前方の床を塗り、しばらく当たり続ける。", new[] { "設置", "持続" }) },
        { "ロブ弾", ("斜め上へ投げる。当たると爆発する。", new[] { "投てき", "範囲" }) },
        { "拡散ショット", ("正面の少し上へ、扇状に弾を撃つ。", new[] { "遠距離", "拡散" }) },
        { "トラップ弾", ("投げた所に、当たり続ける床を残す。", new[] { "投てき", "設置" }) },
        { "ノヴァ", ("前方に大きな円の爆発を起こす。", new[] { "範囲", "対空" }) },
    };

    public static string Description(SkillDefinition skill)
    {
        LoadKitTexts();
        return skill != null && Texts.TryGetValue(skill.DisplayName, out var t) ? t.description : "";
    }

    public static string[] Tags(SkillDefinition skill)
    {
        LoadKitTexts();
        return skill != null && Texts.TryGetValue(skill.DisplayName, out var t) ? t.tags : Array.Empty<string>();
    }

    /// <summary>AppKitsBuilder が書く、キットの能力の説明の表（Resources の中の場所。2026-11-19）</summary>
    public const string KitResource = "Kits/KitSkillTexts";
    static bool _kitLoaded;

    /// <summary>キットの能力の説明を読む（1回だけ。Register や上の表にある名前は上書きしない）</summary>
    static void LoadKitTexts()
    {
        if (_kitLoaded) return;
        _kitLoaded = true;
        try
        {
            var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(KitResource);
            if (asset == null) return;
            var file = UnityEngine.JsonUtility.FromJson<KitSkillTextFile>(asset.text);
            if (file == null || file.texts == null) return;
            foreach (var r in file.texts)
            {
                if (r == null || string.IsNullOrEmpty(r.name) || Texts.ContainsKey(r.name)) continue;
                Texts[r.name] = (r.description ?? "", r.tags ?? Array.Empty<string>());
            }
        }
        catch (Exception e) { UnityEngine.Debug.LogException(e); }
    }

    /// <summary>
    /// 【仮】能力の説明とタグを足す（2026-11-19：アプリのキット。キットの実行時のファイルが [RuntimeInitializeOnLoadMethod] で呼ぶ）。
    /// skillName は能力の名前（カードに出る名前）。同じ名前は上書き
    /// </summary>
    public static void Register(string skillName, string description, params string[] tags)
    {
        if (string.IsNullOrEmpty(skillName)) return;
        Texts[skillName] = (description ?? "", tags ?? Array.Empty<string>());
    }
}

/// <summary>キットの能力の説明の表（AppKitsBuilder が Resources/Kits/KitSkillTexts.json に書く。2026-11-19）</summary>
[Serializable]
public class KitSkillTextFile
{
    public KitSkillTextRow[] texts;
}

[Serializable]
public class KitSkillTextRow
{
    public string name;
    public string description;
    public string[] tags;
}
