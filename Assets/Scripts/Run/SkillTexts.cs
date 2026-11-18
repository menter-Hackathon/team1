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
        // その場で作る能力のひな形
        { "ピラー", ("前方に縦の柱を立てて攻撃する。", new[] { "範囲", "対空" }) },
        { "連続ピラー", ("前方に柱をいくつも立てる。", new[] { "連続", "対空" }) },
        { "ペイント", ("前方の床を塗り、しばらく当たり続ける。", new[] { "設置", "持続" }) },
        { "ロブ弾", ("斜め上へ投げる。当たると爆発する。", new[] { "投てき", "範囲" }) },
        { "拡散ショット", ("正面の少し上へ、扇状に弾を撃つ。", new[] { "遠距離", "拡散" }) },
        { "トラップ弾", ("投げた所に、当たり続ける床を残す。", new[] { "投てき", "設置" }) },
        { "ノヴァ", ("前方に大きな円の爆発を起こす。", new[] { "範囲", "対空" }) },
    };

    public static string Description(SkillDefinition skill) =>
        skill != null && Texts.TryGetValue(skill.DisplayName, out var t) ? t.description : "";

    public static string[] Tags(SkillDefinition skill) =>
        skill != null && Texts.TryGetValue(skill.DisplayName, out var t) ? t.tags : Array.Empty<string>();
}
