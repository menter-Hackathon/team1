using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】どのアプリに、どのウイルスが出るか（割り当て。2026-11-20。docs/virus-types-design.md 5-2・5-3・7章 D）。
/// ・流れの係（StagePacer）の枠はそのまま使う。StagePacer.TemplateOf から 1 か所だけ呼ぶ（Pick）
/// ・歩く枠：W1 が 30%、W2 が 20%、のこり 50% は今のスス玉。ワームが 3 体・ルートキットが 2 体いるときは、スス玉にする
/// ・撃つ枠：表に書いてあれば、全部その種類（撃つ雑魚は同時 2 体まで、は今のまま：アドウェア・フィッシングも ZakoShooter を持つ）
/// ・群れ（Small）＝ボットネット、強い（Elite）＝ランサムウェア、とぶ（Flyer）＝スパイウェア、突っ込む（Charger）＝トロイ：表に印があれば、全部その種類
/// ・ワーム・ルートキットは VirusArt.IndexOf で名前から探すだけ（ZakoWorm・ZakoRootkit がまだ・無ければスス玉）。のこり 10 種は VirusSkins が出す直前に作る
/// ・例外は外へ出さない（スス玉の番号を返す）。VirusArt.Enabled が false なら、今までどおり
/// 数値はすべて【仮】
/// </summary>
public static class VirusRoster
{
    public const float W1Chance = 0.3f;
    public const float W2Chance = 0.2f;
    public const int MaxWorms = 3;
    public const int MaxRootkits = 2;
    // ウィンドウのステージ（2026-11-20。docs/window-stage-and-pickup-design.md 1-7）：W1 をワームにして 45%、W2 は表のまま 15%（スス玉 40%）。ワームは同時 4 体まで
    public const float WindowWormChance = 0.45f;
    public const float WindowW2Chance = 0.15f;
    public const int WindowMaxWorms = 4;

    /// <summary>
    /// 【確認用】種類の名前を入れると、歩く枠すべてと、その種類の枠で、表を見ずにかならずその種類にする（出せるときだけ。ワーム・ルートキットの上限は守る）。
    /// その種類の枠：Adware・Phish＝撃つ枠、Botnet＝群れ、Ransomware＝強い、Spyware＝とぶ、Trojan＝突っ込む（とぶ雑魚は床から出ても自分で上がる）
    /// </summary>
    public static string ForceType;
    /// <summary>【確認用】ラベルを入れると、そのアプリの行を使う（例："Mail"）</summary>
    public static string ForceLabel;

    [Flags]
    enum Mark { None = 0, Spy = 1, Trojan = 2, Botnet = 4, Ransom = 8 }

    class Row
    {
        public string W1, W2, Shoot;
        public Mark Marks;
    }

    const Mark Su = Mark.Spy, To = Mark.Trojan, Bo = Mark.Botnet, Ra = Mark.Ransom, No = Mark.None;

    static readonly Dictionary<string, Row> Table = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
    static readonly Row Fallback = new Row { W1 = "Worm", W2 = "Poly", Shoot = null, Marks = No }; // 表に無いラベル

    static StagePacer _labelPacer;
    static string _label;

    static VirusRoster()
    {
        // ラベル（Resources/AppBackgrounds/Bg_<ラベル>.png と同じ名前）, W1, W2, 撃つ, 印
        Add("Chrome", "Worm", "Bomb", "Adware", Su | To | Bo);
        Add("Safari", "Worm", "Poly", "Adware", Su);
        Add("Arc", "Worm", "Poly", "Adware", Su);
        Add("Firefox", "Worm", "Poly", "Adware", Su | To);
        Add("Mail", "Worm", "Macro", "Phish", To | Ra);
        Add("Messages", "Worm", null, "Phish", Su);
        Add("LINE", "Worm", null, "Phish", Bo);
        Add("Slack", "Worm", "Macro", "Phish", Bo);
        Add("Discord", "Worm", "Miner", "Phish", Bo | To);
        Add("Teams", "Macro", "Worm", "Phish", Su | Bo);
        Add("Zoom", "Worm", null, "Phish", Su | Bo);
        Add("Word", "Macro", "Worm", null, Ra);
        Add("Excel", "Macro", "Bomb", null, Ra);
        Add("PowerPoint", "Macro", "Poly", null, Ra);
        Add("Keynote", "Macro", "Poly", null, To);
        Add("Notes", "Macro", "Bomb", null, Ra | Su);
        Add("Notion", "Macro", "Worm", "Phish", Ra);
        Add("Calendar", "Bomb", "Macro", "Phish", No);
        Add("Finder", "Worm", "Macro", null, Ra | To);
        Add("Terminal", "Rootkit", "Bomb", null, Bo);
        Add("VSCode", "Rootkit", "Poly", null, To | Bo);
        Add("Cursor", "Rootkit", "Poly", null, To);
        Add("Xcode", "Rootkit", "Bomb", null, To);
        Add("GitHubDesktop", "Worm", "Poly", "Phish", To | Bo);
        Add("Unity", "Miner", "Rootkit", null, To);
        Add("Blender", "Miner", "Poly", null, To);
        Add("Minecraft", "Miner", "Worm", "Adware", To | Bo);
        Add("Steam", "Miner", "Rootkit", "Adware", To | Bo);
        Add("Photoshop", "Poly", "Miner", null, Ra | To);
        Add("Illustrator", "Poly", "Macro", null, Ra);
        Add("Figma", "Poly", "Worm", null, Su);
        Add("PremierePro", "Miner", "Poly", null, Ra);
        Add("QuickTime", "Miner", "Poly", null, Su | To);
        Add("Music", "Miner", "Worm", "Adware", To);
        Add("Spotify", "Miner", "Worm", "Adware", Bo);
        Add("ChatGPT", "Poly", "Bomb", "Phish", Su);
        Add("Claude", "Poly", "Bomb", "Phish", Su);
        Add("Gemini", "Poly", "Bomb", "Phish", Su);
    }

    static void Add(string label, string w1, string w2, string shoot, Mark marks)
    {
        Table[label] = new Row { W1 = w1, W2 = w2, Shoot = shoot, Marks = marks };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ForceType = null;
        ForceLabel = null;
        _labelPacer = null;
        _label = null;
    }

    /// <summary>
    /// 枠（"Walker"・"Shooter"・"Small"・"Elite"・"Flyer"・"Charger"）と、今までの決め方の番号 basic から、出すひな形の番号を決める。
    /// 着せかえが無い・出せないときは basic をそのまま返す。返す番号は、かならず spawner.Template(i) != null の物。例外は外へ出さない
    /// </summary>
    public static int Pick(string slot, int basic, StagePacer pacer)
    {
        try
        {
            if (!VirusArt.Enabled || pacer == null || string.IsNullOrEmpty(slot)) return basic;
            ZakoSpawner spawner = pacer.Spawner;
            if (spawner == null) return basic;
            VirusSkins.EnsureBuilt(spawner); // 着せかえは最初に聞かれたときに作る（spawner ごとに 1 回）
            string type = Choose(slot, pacer, spawner);
            if (string.IsNullOrEmpty(type)) return basic;
            int i = VirusArt.IndexOf(spawner, type);
            return i >= 0 && spawner.Template(i) != null ? i : basic;
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("VirusRoster.Pick", $"ウイルスの割り当てで例外（今の雑魚で出します）：{e}");
            return basic;
        }
    }

    static string Choose(string slot, StagePacer pacer, ZakoSpawner spawner)
    {
        // 確認用：歩く枠はすべて、ほかはその種類の枠で、かならずその種類（撃つ種類を歩く枠で出すと、撃つ雑魚 2 体までの上限は守らない。確認のときだけ）
        string forced = ForceType;
        if (!string.IsNullOrEmpty(forced) && SlotOf(forced) != null && (slot == "Walker" || SlotOf(forced) == slot))
            return Capped(spawner, forced) ? null : forced;

        Row row = RowOf(LabelOf(pacer));
        switch (slot)
        {
            case "Walker":
            {
                // 1 回だけふる（出せない種類のときは、ふり直さずにスス玉。スス玉は半分より減らない）
                float r = UnityEngine.Random.value;
                string t = WindowStage.Active
                    ? (r < WindowWormChance ? "Worm" : r < WindowWormChance + WindowW2Chance ? row.W2 : null)
                    : (r < W1Chance ? row.W1 : r < W1Chance + W2Chance ? row.W2 : null);
                if (string.IsNullOrEmpty(t) || Capped(spawner, t)) return null;
                return t;
            }
            case "Shooter": return row.Shoot;
            case "Small": return (row.Marks & Mark.Botnet) != 0 ? "Botnet" : null;
            case "Elite": return (row.Marks & Mark.Ransom) != 0 ? "Ransomware" : null;
            case "Flyer": return (row.Marks & Mark.Spy) != 0 ? "Spyware" : null;
            case "Charger": return (row.Marks & Mark.Trojan) != 0 ? "Trojan" : null;
        }
        return null;
    }

    /// <summary>同時にいられる数をこえるか（ワーム 3 体＝ウィンドウのステージは 4 体・ルートキット 2 体）</summary>
    static bool Capped(ZakoSpawner spawner, string type)
    {
        if (type == "Worm") return VirusArt.AliveOf(spawner, "Worm") >= (WindowStage.Active ? WindowMaxWorms : MaxWorms);
        if (type == "Rootkit") return VirusArt.AliveOf(spawner, "Rootkit") >= MaxRootkits;
        return false;
    }

    /// <summary>その種類が出る枠（確認用の ForceType で使う）</summary>
    static string SlotOf(string type)
    {
        switch (type)
        {
            case "Adware":
            case "Phish": return "Shooter";
            case "Botnet": return "Small";
            case "Ransomware": return "Elite";
            case "Spyware": return "Flyer";
            case "Trojan": return "Charger";
            case "Worm":
            case "Rootkit":
            case "Bomb":
            case "Miner":
            case "Poly":
            case "Macro": return "Walker";
        }
        return null;
    }

    static Row RowOf(string label)
    {
        return !string.IsNullOrEmpty(label) && Table.TryGetValue(label, out Row row) ? row : Fallback;
    }

    /// <summary>
    /// そのステージのラベル（例："Slack"）。分からなければ null（表の最後の行）。
    /// ラスボス → Terminal。ほかは StagePacer のアプリの名前・バンドルIDから AppBackgrounds.LabelFor（背景の絵が無くても決まる）、
    /// 決まらなければ AppBackgrounds.CurrentLabel
    /// </summary>
    public static string LabelOf(StagePacer pacer)
    {
        if (!string.IsNullOrEmpty(ForceLabel)) return ForceLabel;
        if (pacer == null) return null;
        if (_labelPacer != null && ReferenceEquals(pacer, _labelPacer)) return _label;
        string label = null;
        try
        {
            label = pacer.IsFinal ? "Terminal" : AppBackgrounds.LabelFor(pacer.AppName, pacer.BundleId);
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("VirusRoster.LabelOf", $"ウイルスの割り当て：ラベルを決められません：{e}");
        }
        if (string.IsNullOrEmpty(label)) label = AppBackgrounds.CurrentLabel;
        _labelPacer = pacer;
        _label = label;
        return label;
    }
}
