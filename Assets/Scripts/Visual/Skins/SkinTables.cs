using System;
using UnityEngine;

// 【仮】アプリ → スキン、攻撃データの名前 → 枠 の表（2026-11-18。docs/app-skill-visuals-design.md 1-1）。
// アプリ → スキン：専用（Finder・Premiere Pro・Terminal）→ 名前とバンドルIDの表 → カテゴリ → Generic。
//   Zoom は、ボスのときは Chat（7章）。プレイヤーの Zoom の能力だけが Zoom（ForSkill）。
// 攻撃データの名前 → 枠：攻撃データのアセットは書き換えないので、名前で決める（その場で作る能力の複製も、名前は元のまま）。
// 表の中身は【仮】。チームで見て直してよい（15章の8）。
public static class SkinTables
{
    // ───────── アプリ → スキン ─────────

    /// <summary>スキン1つ分の行：アプリの名前（単語ごとに比べる。大文字小文字は区別しない）と、バンドルIDの前方一致</summary>
    struct Row
    {
        public SkinId Skin;
        public string[] Names;
        public string[] Bundles;
        public Row(SkinId skin, string[] names, string[] bundles)
        {
            Skin = skin;
            Names = names;
            Bundles = bundles;
        }
    }

    /// <summary>専用（いちばん先に見る）。名前は設計メモ9章の表</summary>
    static readonly Row[] Dedicated =
    {
        new Row(SkinId.Finder, new[] { "Finder" }, new[] { "com.apple.finder" }),
        new Row(SkinId.Premiere, new[] { "Premiere Pro", "Adobe Premiere Pro", "Premiere" }, new[] { "com.adobe.PremierePro" }),
        new Row(SkinId.Terminal, new[] { "Terminal", "ターミナル" }, new[] { "com.apple.Terminal" }),
    };

    /// <summary>名前の表（仕様書 1-1 のスキンの一覧と、設計メモ9章のバンドルID）</summary>
    static readonly Row[] Table =
    {
        new Row(SkinId.Browser,
            new[] { "Chrome", "Google Chrome", "Safari", "Arc", "Firefox", "Edge", "Microsoft Edge", "Brave", "Brave Browser", "Opera", "Vivaldi", "Orion" },
            new[] { "com.google.Chrome", "com.apple.Safari", "company.thebrowser.Browser", "org.mozilla.firefox", "com.microsoft.edgemac", "com.brave.Browser", "com.operasoftware.", "com.vivaldi." }),
        new Row(SkinId.Dev,
            new[] { "VS Code", "Visual Studio Code", "Code", "Cursor", "Xcode", "Unity", "Unity Hub", "IntelliJ IDEA", "IntelliJ IDEA CE", "GitHub Desktop",
                    "Simulator", "シミュレーター", "iTerm2", "iTerm", "Warp", "Rider", "Docker Desktop", "Docker", "Postman", "Android Studio", "Sublime Text", "Zed" },
            new[] { "com.microsoft.VSCode", "com.todesktop.230313mzl4w4u92", "com.apple.dt.Xcode", "com.apple.iphonesimulator", "com.unity3d.", "com.jetbrains.",
                    "com.github.GitHubClient", "com.googlecode.iterm2", "dev.warp.", "com.docker.", "com.postmanlabs.", "com.google.android.studio", "com.sublimetext.", "dev.zed." }),
        new Row(SkinId.Office,
            new[] { "Word", "Microsoft Word", "Excel", "Microsoft Excel", "PowerPoint", "Microsoft PowerPoint", "Keynote", "Pages", "Numbers", "Notion",
                    "Mail", "メール", "Calendar", "カレンダー", "Notes", "メモ", "Outlook", "Microsoft Outlook", "Obsidian", "Linear", "Reminders", "リマインダー" },
            new[] { "com.microsoft.Word", "com.microsoft.Excel", "com.microsoft.Powerpoint", "com.apple.iWork.", "notion.id", "com.apple.mail", "com.apple.iCal",
                    "com.apple.Notes", "com.microsoft.Outlook", "md.obsidian", "com.linear", "com.apple.reminders" }),
        new Row(SkinId.Creative,
            new[] { "Figma", "Photoshop", "Adobe Photoshop", "Illustrator", "Adobe Illustrator", "After Effects", "Adobe After Effects", "Blender", "OBS", "OBS Studio",
                    "GarageBand", "Aseprite", "CLIP STUDIO PAINT", "Final Cut Pro", "Lightroom", "Logic Pro", "Affinity Designer", "Affinity Photo" },
            new[] { "com.figma.Desktop", "com.adobe.Photoshop", "com.adobe.illustrator", "com.adobe.AfterEffects", "org.blenderfoundation.blender", "com.obsproject.obs-studio",
                    "com.apple.garageband", "com.aseprite.", "jp.co.celsys.", "com.apple.FinalCut", "com.adobe.LightroomClassic", "com.apple.logic", "com.seriflabs." }),
        new Row(SkinId.Game,
            new[] { "Steam", "Minecraft Launcher", "Minecraft", "Epic Games Launcher", "Battle.net" },
            new[] { "com.valvesoftware.steam", "com.mojang.", "com.epicgames.", "net.battle." }),
        new Row(SkinId.Chat,
            new[] { "Slack", "Discord", "LINE", "Messages", "メッセージ", "Teams", "Microsoft Teams", "Zoom", "zoom.us", "Telegram", "WhatsApp", "Messenger" },
            new[] { "com.tinyspeck.slackmacgap", "com.hnc.Discord", "jp.naver.line.mac", "com.apple.MobileSMS", "com.microsoft.teams", "us.zoom.xos",
                    "ru.keepcoder.Telegram", "net.whatsapp.", "com.facebook.archon" }),
        new Row(SkinId.AI,
            new[] { "ChatGPT", "Claude", "Codex", "Perplexity", "Gemini" },
            new[] { "com.openai.", "com.anthropic.", "ai.perplexity." }),
        new Row(SkinId.Media,
            new[] { "Music", "ミュージック", "Spotify", "QuickTime Player", "QuickTime", "TV", "Podcasts", "Podcast", "VLC", "IINA" },
            new[] { "com.apple.Music", "com.spotify.client", "com.apple.QuickTimePlayerX", "com.apple.TV", "com.apple.podcasts", "org.videolan.vlc", "com.colliderli.iina" }),
    };

    /// <summary>アプリの名前とバンドルIDから、スキンを決める（専用 → 名前の表 → カテゴリ → Generic）</summary>
    public static SkinId ForApp(string name, string bundleId)
    {
        var row = new AppUsageRow { Name = name, BundleId = bundleId };
        foreach (var r in Dedicated) if (AppMatch.Matches(row, r.Names, r.Bundles)) return r.Skin;
        foreach (var r in Table) if (AppMatch.Matches(row, r.Names, r.Bundles)) return r.Skin;
        // 表に無いアプリは、能力の表のカテゴリで決める（ランの中だけ。表を持っていなければ飛ばす）
        AbilityCatalog abilities = GameSession.Instance != null ? GameSession.Instance.Abilities : null;
        if (abilities != null && (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(bundleId)))
        {
            try
            {
                AbilityCategoryEntry c = abilities.Find(row);
                if (c != null) return ForCategory(c.Category);
            }
            catch (Exception) { /* 表の形が違っても、スキンは Generic で続ける */ }
        }
        return SkinId.Generic;
    }

    /// <summary>カテゴリ → スキン（分からなければ Generic）</summary>
    public static SkinId ForCategory(SkillCategory category)
    {
        switch (category)
        {
            case SkillCategory.Browser: return SkinId.Browser;
            case SkillCategory.Dev: return SkinId.Dev;
            case SkillCategory.Office: return SkinId.Office;
            case SkillCategory.Creative: return SkinId.Creative;
            case SkillCategory.Game: return SkinId.Game;
            case SkillCategory.SocialChat: return SkinId.Chat;
            case SkillCategory.AI: return SkinId.AI;
            case SkillCategory.Media: return SkinId.Media;
            default: return SkinId.Generic;
        }
    }

    /// <summary>
    /// 能力のスキン（1-1 の「プレイヤー」）：能力に持たせたスキン（その場で作る能力）→ アプリ専用の能力（Zoom・Finder・Premiere Pro）→ カテゴリ → Generic
    /// </summary>
    public static SkinId ForSkill(SkillDefinition skill)
    {
        if (skill == null) return SkinId.None;
        if (skill.Skin != SkinId.None) return skill.Skin;
        string n = skill.Attack != null ? skill.Attack.name : skill.name;
        if (Has(n, "app_zoom") || Has(skill.name, "app_zoom")) return SkinId.Zoom;
        if (Has(n, "app_finder") || Has(skill.name, "app_finder")) return SkinId.Finder;
        if (Has(n, "app_premiere") || Has(skill.name, "app_premiere")) return SkinId.Premiere;
        return ForCategory(skill.Category);
    }

    /// <summary>ボスの設定の表示名から（テスト用のシーン：Chrome＝Browser、VS Code＝Dev。ステージは StageDirector がアプリから入れる）</summary>
    public static SkinId ForBossName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return SkinId.None;
        return ForApp(displayName, null);
    }

    // ───────── 攻撃データの名前 → 枠 ─────────

    /// <summary>攻撃データの名前から、枠を決める（無ければ None）。上から先に当たった物（Bomb_Blast は Bomb より先）</summary>
    public static AttackFrame FrameOf(AttackData data)
    {
        if (data == null) return AttackFrame.None;
        string n = data.name;
        // ウイルス（雑魚）の攻撃と、専用の技（Custom）は枠を使わない
        if (Has(n, "attack_zako") || Has(n, "attack_stage_")) return AttackFrame.None;
        if (data.Kind == AttackKind.Custom || data.Kind == AttackKind.LockOn || data.Kind == AttackKind.Melee) return AttackFrame.None;

        // Chrome
        if (Has(n, "tabshuriken")) return AttackFrame.SmallShot;
        if (Has(n, "tabmultiply")) return AttackFrame.TopRow;
        if (Has(n, "popupappear")) return AttackFrame.Window;   // Chrome の P1/P2 と、Finder の P2（ウィンドウ出現の複製）
        // VS Code
        if (Has(n, "errorbullet")) return AttackFrame.LongShot;
        if (Has(n, "redwave")) return AttackFrame.Floor;
        if (Has(n, "errorwindowfall")) return AttackFrame.Falling;
        if (Has(n, "vscode_warp")) return AttackFrame.Warp;
        if (Has(n, "breakpoint")) return AttackFrame.Pillar;    // ボスの技・能力の両方（能力はひな形「ピラー」でも使う）
        // カテゴリの能力（ひな形「連続ピラー」「ペイント」「ロブ弾」も、同じ名前の複製）
        if (Has(n, "ability_newtab")) return AttackFrame.Warp;
        if (Has(n, "bargraph")) return AttackFrame.Pillar;
        if (Has(n, "paintbucket")) return AttackFrame.Paint;
        if (Has(n, "bomb_blast")) return AttackFrame.Circle;
        if (Has(n, "ability_bomb")) return AttackFrame.Thrown;
        // その場で作る能力のひな形
        if (Has(n, "gen_spread")) return AttackFrame.SmallShot;
        if (Has(n, "gen_trapfloor")) return AttackFrame.Floor;
        if (Has(n, "gen_trap")) return AttackFrame.Thrown;
        if (Has(n, "gen_nova")) return AttackFrame.Circle;
        if (Has(n, "gen_lob")) return AttackFrame.Thrown;
        if (Has(n, "gen_paint")) return AttackFrame.Paint;
        if (Has(n, "gen_pillar")) return AttackFrame.Pillar;
        // Terminal のアクセス拒否の柱（DenyPillar）は、今の赤い柱のまま（枠を使わない。13章に絵が無いため）【仮】
        return AttackFrame.None;
    }

    // ───────── エラーの絵の種類 ─────────

    /// <summary>エラーの絵の種類（ErrorKind）の決まった名前。ほかは攻撃データの名前から作る（ErrorKindOf）</summary>
    public static class Kinds
    {
        /// <summary>ボスの体当たり（Terminal は Segmentation fault）</summary>
        public const string Contact = "Contact";
        /// <summary>雑魚（ウイルス）の体当たり・弾、トゲ。赤い「！」の三角（1-4）</summary>
        public const string Virus = "Virus";
    }

    /// <summary>
    /// 攻撃データの名前から、エラーの絵の種類を作る。「Attack_アプリ_名前_P1」→「名前」
    /// （Attack_VSCode_ErrorBullet_P1 → ErrorBullet、Attack_Terminal_RmRf_P2 → RmRf、Attack_Terminal_StringRain → StringRain）
    /// </summary>
    public static string ErrorKindOf(AttackData data)
    {
        if (data == null) return "";
        string[] parts = data.name.Split('_');
        int start = 0, end = parts.Length;
        if (start < end && string.Equals(parts[start], "Attack", StringComparison.OrdinalIgnoreCase)) start++;
        if (end - start >= 2) start++; // アプリ（Chrome・VSCode・Terminal・Ability・Gen など）
        if (end - start >= 2 && (parts[end - 1] == "P1" || parts[end - 1] == "P2")) end--;
        if (start >= end) return data.name;
        return string.Join("_", parts, start, end - start);
    }

    static bool Has(string s, string part) => !string.IsNullOrEmpty(s) && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
}
