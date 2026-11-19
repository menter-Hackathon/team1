using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>能力の絵のタグ（12個で固定。docs/ui-text-and-dex-design.md 2-2）</summary>
public enum Picto { Area, Pierce, Guard, Move, Auto, Place, Chain, Stop, Buff, AntiAir, Shot, Random }

/// <summary>能力の届く所（届く所の図。2-3）</summary>
public enum SkillReach { None, Self, Front, Far, Pillar, Up, Around, All }

/// <summary>
/// 【仮】メニューに出す能力の短い説明（2026-11-19：docs/ui-text-and-dex-design.md 2-1）。
/// 能力の名前（SkillDefinition.DisplayName）で引く。図鑑は名前しか持っていないため。
/// ・Line：1行（12字まで）。Pictos：絵のタグ 1〜3個。Reach：届く所
/// ・表に無い名前（これから増える能力）は、SkillTexts の説明の最初の「。」までと、前の文字のタグを絵のタグに置き換えた物。届く所は出さない
/// SkillTexts・KitSkillTexts.json・キットの Register は変えない（メニューがこちらを呼ぶだけ）。文はすべて【仮】
/// </summary>
public static class SkillBriefs
{
    struct Brief
    {
        public string Line;
        public Picto[] Pictos;
        public SkillReach Reach;
    }

    static readonly Dictionary<string, Brief> Table = new Dictionary<string, Brief>();

    // 1行・届く所の記号・絵のタグ（ラベル）
    static void Add(string name, string line, string reach, params string[] tags)
    {
        var pictos = new List<Picto>();
        foreach (string t in tags)
        {
            if (SkillPictos.TryParse(t, out Picto p) && !pictos.Contains(p)) pictos.Add(p);
            else Debug.LogWarning($"SkillBriefs：絵のタグ「{t}」が分かりません（{name}）");
        }
        Table[name] = new Brief { Line = line, Pictos = pictos.ToArray(), Reach = ParseReach(reach) };
    }

    static SkillReach ParseReach(string s) => s switch
    {
        "自" => SkillReach.Self,
        "前" => SkillReach.Front,
        "遠" => SkillReach.Far,
        "柱" => SkillReach.Pillar,
        "上" => SkillReach.Up,
        "周" => SkillReach.Around,
        "全" => SkillReach.All,
        _ => SkillReach.None,
    };

    static SkillBriefs()
    {
        // カテゴリの能力・アプリ専用の能力
        Add("新しいタブ", "前へワープ。空中でも", "自", "移動");
        Add("ブレークポイント", "前の床から縦の柱", "柱", "範囲", "対空");
        Add("棒グラフ", "柱を3本、順に立てる", "柱", "連続", "対空");
        Add("バケツ塗り", "床を塗る。当たり続ける", "前", "設置", "連続");
        Add("ボム", "斜め上に投げて爆発", "上", "飛ばす", "範囲");
        Add("AIアシスタント", "ロボットが自動で攻撃", "周", "自動");
        Add("LINE", "立ち止まり連投して飛ばす", "遠", "連続", "飛ばす");
        Add("Slack", "立ち止まり連投して飛ばす", "遠", "連続", "飛ばす");
        Add("Discord", "立ち止まり連投して飛ばす", "遠", "連続", "飛ばす");
        Add("Photoshop", "筆で振り上げインクを残す", "前", "設置", "連続");
        Add("パケット", "追尾弾で倒すと回復", "周", "自動", "飛ばす");
        Add("グラフ", "使うたびグラフが変わる", "前", "範囲");
        Add("オブジェクト生成", "物体を落とし足場にする", "前", "設置");
        Add("実績解除", "実績で攻撃力と速度アップ", "自", "強化");
        Add("ギャラリービュー", "映る敵みんなにレーザー", "全", "自動", "範囲");
        Add("zip圧縮", "敵と弾を固めて、撃つ", "前", "弾よけ", "飛ばす");
        Add("恐竜ラン", "恐竜が走ってはね飛ばす", "遠", "貫通");
        Add("リップル削除", "前を切り取り敵を寄せる", "前", "範囲");
        Add("rm -rf", "床の波で敵と弾を消す", "遠", "貫通", "弾よけ");
        Add("エフェクトアイコン投げ", "投げるたびに違う効果", "遠", "飛ばす", "おまかせ");
        // その場で作る能力のひな形
        Add("ピラー", "前に縦の柱", "柱", "範囲", "対空");
        Add("連続ピラー", "前に柱をいくつも", "柱", "連続", "対空");
        Add("ペイント", "床を塗る。当たり続ける", "前", "設置", "連続");
        Add("ロブ弾", "斜め上に投げて爆発", "上", "飛ばす", "範囲");
        Add("拡散ショット", "扇の形に弾を撃つ", "遠", "飛ばす", "範囲");
        Add("トラップ弾", "投げた所に罠の床", "前", "飛ばす", "設置");
        Add("ノヴァ", "前に大きな爆発", "柱", "範囲", "対空");
        // アプリのキットの能力
        Add("Zap", "近い敵に稲妻。弾も消す", "全", "自動", "弾よけ");
        Add("Sスケール", "立方体を広げて押しのけ", "前", "範囲");
        Add("予定を入れる", "置いて3秒後に大きい一撃", "周", "設置", "範囲");
        Add("プロンプト", "遅いが、とても強い柱", "柱", "範囲", "対空");
        Add("アーティファクト", "空中に足場を作る", "上", "設置", "対空");
        Add("コマンドK", "毎回ちがう能力が出る", "なし", "おまかせ");
        Add("ミュート", "弾を消し、ボスを止める", "前", "弾よけ", "止める", "範囲");
        Add("オートフィル", "8マス伸びる。遠いほど得", "遠", "貫通");
        Add("ペンツール", "近くの3体を線で切る", "周", "自動", "連続");
        Add("広告ブロック", "盾。最初の弾は打ち返す", "自", "弾よけ");
        Add("ふたご星", "自分と左右に星の柱", "周", "範囲", "対空");
        Add("Revert", "保存した場所へ戻って攻撃", "自", "移動", "貫通");
        Add("スターツール", "星で切り、5つの弾が飛ぶ", "前", "範囲", "飛ばす");
        Add("マジックムーブ", "無敵で5マス突進", "遠", "移動", "貫通");
        Add("スタンプ", "目の前に大きくドン", "前", "範囲");
        Add("送信", "紙飛行機が行って戻る", "遠", "飛ばす", "貫通");
        Add("花火", "空中に花火を3発", "上", "対空", "連続");
        Add("TNT", "置いて待つと大爆発", "前", "設置", "範囲");
        Add("リピート", "前に使った能力をもう1回", "なし", "おまかせ");
        Add("ピン留め", "画びょうでボスを足止め", "遠", "飛ばす", "止める");
        Add("ブロック", "壁を置いて弾を止める", "前", "設置", "弾よけ");
        Add("コピースタンプ", "分身が4秒、切り続ける", "前", "設置", "連続");
        Add("フライイン", "矢印が画面を横切る", "全", "貫通", "範囲");
        Add("トリム", "左右からはさんで切る", "周", "範囲");
        Add("リーダー表示", "画面の弾をぜんぶ消す", "全", "弾よけ", "範囲");
        Add("リアクション", "命中後5秒ゲージ増加量UP", "遠", "飛ばす", "強化");
        Add("再生キュー", "音符3つが勝手に飛ぶ", "全", "自動", "連続");
        Add("トロフィー", "命中後6秒ゲージ増加量UP", "遠", "飛ばす", "強化");
        Add("挙手", "真上に大きい手", "上", "対空");
        Add("キューブ投げ", "はずむ箱。止まると足場", "遠", "飛ばす", "設置");
        Add("太字", "4秒、近接が強く長く", "周", "強化", "範囲");
        Add("ハンマー", "ためて大きくふり下ろす", "前", "範囲");
    }

    /// <summary>表にある名前か</summary>
    public static bool Has(string name) => !string.IsNullOrEmpty(name) && Table.ContainsKey(name);

    /// <summary>1行（12字まで）。表に無ければ、skill の説明の最初の「。」まで（skill が無ければ空）</summary>
    public static string Line(string name, SkillDefinition skill = null)
    {
        if (!string.IsNullOrEmpty(name) && Table.TryGetValue(name, out Brief b)) return b.Line;
        if (skill == null) return "";
        string d = SkillTexts.Description(skill) ?? "";
        int end = d.IndexOf('。');
        return end >= 0 ? d.Substring(0, end) : d;
    }

    public static string Line(SkillDefinition skill) => skill != null ? Line(skill.DisplayName, skill) : "";

    /// <summary>絵のタグ（1〜3個）。表に無ければ、skill の文字のタグを置き換えた物（分からないタグは飛ばす）</summary>
    public static IReadOnlyList<Picto> Pictos(string name, SkillDefinition skill = null)
    {
        if (!string.IsNullOrEmpty(name) && Table.TryGetValue(name, out Brief b)) return b.Pictos;
        var list = new List<Picto>();
        if (skill == null) return list;
        foreach (string tag in SkillTexts.Tags(skill))
        {
            if (SkillPictos.FromOldTag(tag, out Picto p) && !list.Contains(p)) list.Add(p);
            if (list.Count >= 3) break;
        }
        return list;
    }

    public static IReadOnlyList<Picto> Pictos(SkillDefinition skill) => skill != null ? Pictos(skill.DisplayName, skill) : Array.Empty<Picto>();

    /// <summary>届く所（表に無ければ None＝図を出さない）</summary>
    public static SkillReach Reach(string name) => !string.IsNullOrEmpty(name) && Table.TryGetValue(name, out Brief b) ? b.Reach : SkillReach.None;
}

/// <summary>
/// 絵のタグの絵（9×9 ドットの1色の絵。2-2）。最初に使うときに、白い型の Texture2D（Point）を作って持っておく。
/// 色は描くときに掛ける（PixelGui.Fill と同じく、OnGUI の色に直して）
/// </summary>
public static class SkillPictos
{
    public const int Size = 9;

    static readonly string[] Labels = { "範囲", "貫通", "弾よけ", "移動", "自動", "設置", "連続", "止める", "強化", "対空", "飛ばす", "おまかせ" };

    static readonly Color[] Colors =
    {
        new Color32(0xdf, 0x71, 0x26, 0xff), // 範囲
        new Color32(0x2a, 0x7f, 0xd6, 0xff), // 貫通
        new Color32(0x1e, 0x6c, 0x13, 0xff), // 弾よけ
        new Color32(0x0f, 0x9d, 0x9a, 0xff), // 移動
        new Color32(0xed, 0x38, 0xff, 0xff), // 自動
        new Color32(0x8a, 0x5a, 0x2b, 0xff), // 設置
        new Color32(0x6a, 0x2a, 0x8c, 0xff), // 連続
        new Color32(0x97, 0x00, 0x00, 0xff), // 止める
        new Color32(0xe0, 0xa4, 0x00, 0xff), // 強化
        new Color32(0x42, 0x9f, 0xec, 0xff), // 対空
        new Color32(0x33, 0x33, 0x33, 0xff), // 飛ばす
        new Color32(0xb3, 0x76, 0xe9, 0xff), // おまかせ
    };

    // 絵（上の行から。# を塗る）
    static readonly string[][] Patterns =
    {
        new[] { "....#....", ".#..#..#.", "..#...#..", "...###...", "##.###.##", "...###...", "..#...#..", ".#..#..#.", "....#...." }, // 範囲
        new[] { ".........", "...#.....", "...#..#..", "...#...#.", "#########", "...#...#.", "...#..#..", "...#.....", "........." }, // 貫通
        new[] { ".#######.", ".#.....#.", ".#..#..#.", ".#.###.#.", ".#..#..#.", ".#.....#.", "..#...#..", "...#.#...", "....#...." }, // 弾よけ
        new[] { ".........", ".#...#...", "..#...#..", "...#...#.", "....#...#", "...#...#.", "..#...#..", ".#...#...", "........." }, // 移動
        new[] { "....#....", "..#####..", ".#..#..#.", ".#.....#.", "###.#.###", ".#.....#.", ".#..#..#.", "..#####..", "....#...." }, // 自動
        new[] { "...#.....", "...###...", "...#####.", "...###...", "...#.....", "...#.....", "...#.....", ".#######.", "#########" }, // 設置
        new[] { ".........", ".........", "......##.", "...##.##.", "##.##.##.", "##.##.##.", "...##.##.", "......##.", "........." }, // 連続
        new[] { ".........", ".###.###.", ".###.###.", ".###.###.", ".###.###.", ".###.###.", ".###.###.", ".###.###.", "........." }, // 止める
        new[] { "....#....", "...###...", "..##.##..", ".##...##.", "....#....", "...###...", "..##.##..", ".##...##.", "........." }, // 強化
        new[] { "#########", ".........", "....#....", "...###...", "..#.#.#..", "....#....", "....#....", "....#....", "....#...." }, // 対空
        new[] { ".........", ".........", ".....###.", "#.#.#####", ".#.######", "#.#.#####", ".....###.", ".........", "........." }, // 飛ばす
        new[] { "..#####..", ".##...##.", ".##...##.", ".....##..", "....##...", "....##...", ".........", "....##...", "....##..." }, // おまかせ
    };

    // 鍵（図鑑のグレーのアプリ。3-2）
    static readonly string[] LockPattern = { "..#####..", ".#.....#.", ".#.....#.", "#########", "#########", "####.####", "####.####", "#########", "#########" };

    // 前の文字のタグ → 絵のタグ（表に無い能力のとき）
    static readonly Dictionary<string, Picto> OldTags = new Dictionary<string, Picto>();

    static SkillPictos()
    {
        void Map(Picto p, params string[] tags) { foreach (string t in tags) OldTags[t] = p; }
        Map(Picto.Area, "範囲", "広範囲", "前方", "前後", "主力", "近い", "まわり", "横いっぱい", "拡散", "押しのけ", "引き寄せ", "大ダメージ", "ため", "圧縮");
        Map(Picto.Pierce, "貫通", "突進", "遠いほど強い", "2回当たる");
        Map(Picto.Guard, "弾よけ", "弾を消す", "弾消し", "盾", "守る", "反撃");
        Map(Picto.Move, "移動", "空中OK", "無敵");
        Map(Picto.Auto, "自動で狙う", "自動", "ロックオン");
        Map(Picto.Place, "設置", "足場", "持続");
        Map(Picto.Chain, "連続", "音符");
        Map(Picto.Stop, "止める", "足止め");
        Map(Picto.Buff, "強化", "近接");
        Map(Picto.AntiAir, "対空", "空中", "真上");
        Map(Picto.Shot, "飛ばす", "遠距離", "投てき", "投げる", "弾");
        Map(Picto.Random, "おまかせ", "ランダム", "AI", "もう1回", "2回押し");
    }

    static readonly Texture2D[] _textures = new Texture2D[12];
    static Texture2D _lock;

    public static string Label(Picto p) => Labels[(int)p];
    public static Color ColorOf(Picto p) => Colors[(int)p];

    /// <summary>ラベル（「範囲」など）→ 絵のタグ</summary>
    public static bool TryParse(string label, out Picto p)
    {
        int i = Array.IndexOf(Labels, label);
        p = i >= 0 ? (Picto)i : Picto.Area;
        return i >= 0;
    }

    /// <summary>前の文字のタグ（「遠距離」など）→ 絵のタグ</summary>
    public static bool FromOldTag(string tag, out Picto p) => OldTags.TryGetValue(tag ?? "", out p);

    /// <summary>絵（白い型。色は描くときに ColorOf を掛ける）</summary>
    public static Texture2D Texture(Picto p)
    {
        int i = (int)p;
        if (_textures[i] == null) _textures[i] = Make(Patterns[i], "Picto_" + p);
        return _textures[i];
    }

    /// <summary>鍵の絵（白い型）</summary>
    public static Texture2D Lock
    {
        get
        {
            if (_lock == null) _lock = Make(LockPattern, "Picto_Lock");
            return _lock;
        }
    }

    static Texture2D Make(string[] rows, string name)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var px = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            string row = y < rows.Length ? rows[y] : "";
            for (int x = 0; x < Size; x++)
            {
                bool on = x < row.Length && row[x] == '#';
                px[(Size - 1 - y) * Size + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0); // テクスチャの 0 行目は下
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
