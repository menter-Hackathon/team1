// 【仮】アプリごとの出来事の表（2026-11-19。docs/stage-pacing-design.md 3-3・2-3・10-2）。担当2 の物。
// 3つの関数の名前と形は変えない（担当1 の StagePacer が呼ぶ）。For と ExtraZako は、同じ決め方（PlanOf）から出す（ずれない）。
// ・型：雨（Rain）・柱床（Sweep）・群れ（Swarm）。アプリごとの違いは「どの型か」と「スキンの絵」と「隅の通知の短い文字」
// ・3ステージ目から（ラスボスを除く）：表の型に「群れ」を1回重ねる（表の型が群れなら「雨」を重ねる）
// ・雑魚の上乗せ（ExtraZako）：表の型が群れ → 4、重ねる群れ → 3、ほか → 0。群れは攻撃データが要らないので、上乗せした分は必ず出来事が出す
// ・群れが入る出来事（SpawnsOwnZako）には、③の雑魚を全部（③の数＋上乗せ。StagePacer が渡す zakoBudget）まかせる。
//   重ねる群れ（3ステージ目）も、③の雑魚を全部、短い列2つで出す（③の雑魚は群れだけになる）【仮】
// ・攻撃データ（PaceAssets）・出来事用の AttackRunner のプレハブが無ければ、雨・柱床は出さない（群れだけなら出す。無ければ null）
// ・隅の通知の文字は、実在の UI の短い言葉（飾り。【要相談⑥】決まるまでは出す）。Press Start 2P に無い「…」は「...」にした

/// <summary>アプリごとの出来事の表（3-3）</summary>
public static class StageEventTable
{
    /// <summary>【仮】false にすると、柱・床のアプリ（Office・Creative・Media・Premiere・Terminal）も雨にする（柱・床が間に合わないとき。3-3）</summary>
    public static bool SweepReady = true;

    /// <summary>表の型の細かい違い</summary>
    enum Kind
    {
        Rain,          // 雨（ふつう）
        RainSmall,     // 雨（小さい・速い）
        RainBig,       // 雨（大きい・ゆっくり）
        Halves,        // 柱床：低い柱で床の半分を左右交互
        HalvesFloor,   // 柱床：床の半分を左右交互
        Rhythm,        // 柱床：低い柱で床の半分を左右交互（短い予告・リズムよく）
        Playhead,      // 柱床：左から右へ順に進む帯
        Reboot,        // 柱床：床の全部（予告が長い。フォルダの上だけ安全）
        Swarm,         // 群れ：遠い方の端から
        SwarmBoth,     // 群れ：両端から同時
        SwarmTop,      // 群れ：画面の上のタブの列から
    }

    struct Plan
    {
        public Kind Main;
        public bool HasOverlay;
        public Kind Overlay;
    }

    /// <summary>そのステージの出来事（無ければ null。場面③は②の出し方で進む）</summary>
    public static StageEvent For(SkinId skin, int stageNumber, bool isFinal)
    {
        Plan plan = PlanOf(skin, stageNumber, isFinal);
        string toast = ToastOf(isFinal ? SkinId.Terminal : skin);
        PaceEvent main = Build(plan.Main);
        PaceEvent overlay = plan.HasOverlay ? Build(plan.Overlay) : null;
        if (overlay != null)
        {
            overlay.StartDelay = PaceEvent.LeadTime + 2.5f; // 重ねる物は、表の型が始まってから少しあと
            if (overlay is SwarmEvent swarm) swarm.MaxPerLine = 5; // 雨と重なるので、短い列を2つ（はさみうち）
            if (overlay is RainEvent rain) rain.Seconds = 8f;      // 群れが終わったあとに、雨だけが長く残らないように
        }
        if (main != null && overlay != null) return new ComboEvent(toast, main, overlay);
        PaceEvent only = main ?? overlay;
        if (only == null) return null;
        only.StartDelay = PaceEvent.LeadTime;
        only.Toast = toast;
        return only;
    }

    /// <summary>全部の雑魚の数への上乗せ（2-3）。StagePacer が Start で AddQuota に足す</summary>
    public static int ExtraZako(SkinId skin, int stageNumber, bool isFinal)
    {
        Plan plan = PlanOf(skin, stageNumber, isFinal);
        if (IsSwarm(plan.Main)) return 4;
        if (plan.HasOverlay && IsSwarm(plan.Overlay)) return 3;
        return 0;
    }

    /// <summary>出来事の隅の通知の題名（無ければ null）</summary>
    public static string ToastOf(SkinId skin)
    {
        switch (skin)
        {
            case SkinId.Browser: return "Too many tabs";
            case SkinId.Dev: return "Build failed";
            case SkinId.Chat: return "99+";
            case SkinId.Office: return "Calculating...";
            case SkinId.Creative: return "Fill";
            case SkinId.Game: return "Downloading...";
            case SkinId.AI: return "Generating...";
            case SkinId.Media: return "Volume MAX";
            case SkinId.Finder: return "Copying...";
            case SkinId.Premiere: return "Rendering...";
            case SkinId.Zoom: return "Joining...";
            case SkinId.Terminal: return "reboot";
            default: return "99+"; // Generic（ほか）：通知の嵐
        }
    }

    // ───────── 決め方 ─────────

    static Plan PlanOf(SkinId skin, int stageNumber, bool isFinal)
    {
        var plan = new Plan { Main = MainOf(skin, isFinal) };
        if (!SweepReady && IsSweep(plan.Main)) plan.Main = Kind.Rain;
        // 3ステージ目から（ラスボスを除く）：群れを重ねる。表の型が群れなら雨を重ねる
        if (stageNumber >= 3 && !isFinal)
        {
            plan.HasOverlay = true;
            plan.Overlay = IsSwarm(plan.Main) ? Kind.Rain : Kind.Swarm;
        }
        return plan;
    }

    /// <summary>3-3 の表（スキン → 型）</summary>
    static Kind MainOf(SkinId skin, bool isFinal)
    {
        if (isFinal || skin == SkinId.Terminal) return Kind.Reboot;
        switch (skin)
        {
            case SkinId.Browser: return Kind.SwarmTop;     // タブを開きすぎた
            case SkinId.Dev: return Kind.Rain;             // ビルドエラーの雨
            case SkinId.Chat: return Kind.RainSmall;       // 通知の嵐（速い・小さい）
            case SkinId.Office: return Kind.Halves;        // 計算が止まらない（棒グラフが左右交互）
            case SkinId.Creative: return Kind.HalvesFloor; // 塗りつぶしの事故（床の半分が塗られる）
            case SkinId.Game: return Kind.SwarmBoth;       // 一斉ダウンロード（両端から同時）
            case SkinId.AI: return Kind.RainBig;           // 生成が止まらない（ゆっくり・大きい）
            case SkinId.Media: return Kind.Rhythm;         // 音量が最大（リズムよく）
            case SkinId.Finder: return Kind.Rain;          // ファイルが降ってくる
            case SkinId.Premiere: return Kind.Playhead;    // 書き出し中（再生ヘッド）
            case SkinId.Zoom: return Kind.Swarm;           // 参加者が一気に入室（ボスの Zoom は Chat のスキンなので、ふつうは来ない）
            default: return Kind.Rain;                     // Generic：通知の嵐
        }
    }

    static bool IsSwarm(Kind k) => k == Kind.Swarm || k == Kind.SwarmBoth || k == Kind.SwarmTop;
    static bool IsSweep(Kind k) => k == Kind.Halves || k == Kind.HalvesFloor || k == Kind.Rhythm || k == Kind.Playhead || k == Kind.Reboot;

    /// <summary>型から出来事を作る。危ない物の攻撃データ・AttackRunner のプレハブが無ければ null（群れは要らない）</summary>
    static PaceEvent Build(Kind k)
    {
        switch (k)
        {
            case Kind.Swarm: return new SwarmEvent(SwarmEvent.Entry.FarEdge);
            case Kind.SwarmBoth: return new SwarmEvent(SwarmEvent.Entry.BothEdges);
            case Kind.SwarmTop: return new SwarmEvent(SwarmEvent.Entry.FromTop);
        }

        PaceAssets a = PaceAssets.Load();
        if (a == null || PaceAssets.RunnerPrefab() == null) return null;
        switch (k)
        {
            case Kind.Rain: return RainOf(a.Rain, 0.7f);
            case Kind.RainSmall: return RainOf(a.RainSmall != null ? a.RainSmall : a.Rain, 0.5f);
            case Kind.RainBig: return RainOf(a.RainBig != null ? a.RainBig : a.Rain, 1.0f);
            case Kind.Halves: return SweepOf(SweepEvent.Pattern.Halves, a.Pillar != null ? a.Pillar : a.Floor, 0.2f, 3, 4, 4);
            case Kind.HalvesFloor: return SweepOf(SweepEvent.Pattern.Halves, a.Floor != null ? a.Floor : a.Pillar, 0.2f, 3, 4, 4);
            case Kind.Rhythm: return SweepOf(SweepEvent.Pattern.Halves, a.PillarRhythm != null ? a.PillarRhythm : a.Pillar, 0f, 4, 5, 6);
            case Kind.Playhead: return SweepOf(SweepEvent.Pattern.Playhead, a.Floor, 0.25f, 1, 1, 2);
            case Kind.Reboot: return SweepOf(SweepEvent.Pattern.WholeFloor, a.FloorReboot != null ? a.FloorReboot : a.Floor, 2.0f, 2, 2, 2);
            default: return null;
        }
    }

    static PaceEvent RainOf(AttackData data, float interval) => data != null ? new RainEvent(data, interval) : null;

    static PaceEvent SweepOf(SweepEvent.Pattern pattern, AttackData data, float gap, params int[] counts)
        => data != null ? new SweepEvent(pattern, data, gap, counts) : null;
}
