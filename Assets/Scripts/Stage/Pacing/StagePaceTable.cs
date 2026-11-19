// 【仮】ステージの山と谷の数の表（2026-11-19。docs/stage-pacing-design.md 2-2・2-3・2-4・7章・8章）。
// 数・秒数はすべて【仮】。コードの中の表（アセットにしない）。担当1（流れ）の物。

/// <summary>ステージの山と谷の数と秒数</summary>
public static class StagePaceTable
{
    /// <summary>安全のためのスイッチ。false：今までの出し方に戻す（StagePacer が付かない。締め切りの日に問題が出たら切る）</summary>
    public static bool Enabled = true;

    /// <summary>【要相談①】雑魚の数を増やす（16／20／24／14）。false：今の数のまま、表の割合で場面だけ分ける</summary>
    public static bool MoreZako = true;

    /// <summary>1ステージ分の数（① 入りは、最初から置いてある雑魚。ここには入れない）</summary>
    public class Row
    {
        /// <summary>② 盛り上がりの組（2〜3体ずつ）</summary>
        public int[] Build;
        /// <summary>③ 出来事の雑魚（出来事の上乗せは別。StageEventTable.ExtraZako）</summary>
        public int Event;
        /// <summary>⑤ 最高潮の群れ（1つ目は遠い方の端から、2つ目は反対の端から）</summary>
        public int[] Climax;
        /// <summary>⑤ の最後に強い雑魚を1体</summary>
        public bool Elite;
        /// <summary>④ ひと休みがあるか（【要相談⑧】Terminal はなし）</summary>
        public bool Rest;
        /// <summary>出来事の強さ（1＝弱い、2＝ふつう、3＝強い）</summary>
        public int Strength;

        /// <summary>出す数の合計（② ＋ ③ ＋ ⑤）</summary>
        public int Planned => Sum(Build) + Event + Sum(Climax) + (Elite ? 1 : 0);
    }

    // 2-3 の表（最初の4体と合わせて 16／20／24／14）
    static readonly Row Stage1 = new Row { Build = new[] { 2, 2 }, Event = 3, Climax = new[] { 2, 2 }, Elite = true, Rest = true, Strength = 1 };
    static readonly Row Stage2 = new Row { Build = new[] { 2, 3 }, Event = 4, Climax = new[] { 3, 3 }, Elite = true, Rest = true, Strength = 2 };
    static readonly Row Stage3 = new Row { Build = new[] { 3, 3 }, Event = 5, Climax = new[] { 4, 4 }, Elite = true, Rest = true, Strength = 3 };
    static readonly Row Final = new Row { Build = new[] { 3 }, Event = 3, Climax = new[] { 3 }, Elite = true, Rest = false, Strength = 2 };

    /// <summary>そのステージの数（stageNumber は 1〜。4 以降は 3 と同じ。isFinal は Terminal）</summary>
    public static Row For(int stageNumber, bool isFinal)
    {
        if (isFinal) return Final;
        if (stageNumber <= 1) return Stage1;
        if (stageNumber == 2) return Stage2;
        return Stage3;
    }

    // ───────── 出し方（1画面） ─────────
    /// <summary>画面の雑魚が0体になったら、間隔を待たずにこの秒数で次を出す（空白をなくす）</summary>
    public const float NoGapDelay = 0.3f;
    /// <summary>② 組と組の間隔・同時の数</summary>
    public const float BuildInterval = 1.4f;
    public const int BuildCap = 4;
    /// <summary>組の中の1体ずつの間（ポンポンと出る）</summary>
    public const float GroupStep = 0.12f;
    /// <summary>③ 出来事の間に1体ずつ出す間隔・同時の数（撃つ雑魚は出さない）</summary>
    public const float EventInterval = 2.5f;
    public const int EventCap = 2;
    /// <summary>③ 出来事が終わらないときに、こちらで止める秒数（出来事は 8〜12秒で自分で終わる）</summary>
    public const float EventMaxSeconds = 20f;
    /// <summary>④ ひと休みの秒数（③ が終わってから）</summary>
    public const float RestSeconds = 3f;
    /// <summary>⑤ に入ってから1つ目の群れまで</summary>
    public const float ClimaxLeadIn = 0.6f;
    /// <summary>⑤ 群れの1体ずつの間・群れと群れの間・同時の数</summary>
    public const float SwarmStep = 0.25f;
    public const float ClimaxGap = 1.2f;
    public const int ClimaxCap = 7;

    // ───────── 出す場所 ─────────
    /// <summary>列の間（マス）</summary>
    public const float LineSpacing = 0.8f;
    /// <summary>画面の端から、列の先頭までの距離（マス）</summary>
    public const float EdgeInset = 1.2f;
    /// <summary>撃つ雑魚が組に入る割合・同時にいてよい数</summary>
    public const float ShooterChance = 0.3f;
    public const int MaxShooters = 2;
    /// <summary>とぶ雑魚は何ステージ目から（② と ⑤ に1体ずつ）・出す高さ（床から。マス）</summary>
    public const int FlyerFromStage = 2;
    public const float FlyerHeight = 3f;
    /// <summary>突っ込む雑魚は何ステージ目から（Terminal にも。② に1体）</summary>
    public const int ChargerFromStage = 3;

    // ───────── 横スクロール・ウィンドウ移動（2-4） ─────────
    /// <summary>横スクロール：場面が変わる進み具合（② ③ ④ ⑤ の始まり）</summary>
    public const float ScrollBuildFrom = 0.2f;
    public const float ScrollEventFrom = 0.45f;
    public const float ScrollRestFrom = 0.65f;
    public const float ScrollClimaxFrom = 0.75f;
    /// <summary>③ 区間の出来事の長さの上限</summary>
    public const float GateEventSeconds = 12f;
    /// <summary>ウィンドウ移動：部屋2に入ってから出来事が始まるまで</summary>
    public const float RoomsEventDelay = 1.5f;
    /// <summary>道中の組の間隔・同時の数（画面の近くの雑魚だけで数える）</summary>
    public const float GateGroupInterval = 2.6f;
    public const int GateCap = 5;
    /// <summary>⑤ 前の群れから後ろの群れまで（少し遅れて）</summary>
    public const float GateSwarmGap = 1.5f;

    // ───────── あと少し（7章） ─────────
    /// <summary>残りがこの数以下で、残っている雑魚の頭の上に小さい白い印</summary>
    public const int MarkFrom = 3;
    /// <summary>最後の1体を倒したときのスロー（実時間の秒）</summary>
    public const float LastSlowSeconds = 0.25f;
    /// <summary>右上のゲージがこの割合を越えたら、ボスの影がふるえ、ゲージがゆっくり明滅する（6章）</summary>
    public const float GaugeShakeFrom = 0.8f;

    // ───────── そのほか ─────────
    /// <summary>雑魚が1体もいないまま、出せない時間がこれだけ続いたら、今までの出し方に戻す（ボスが出なくなるのを防ぐ）</summary>
    public const float WatchdogSeconds = 3f;
    /// <summary>出来事用の AttackRunner のプレハブ（担当2 が作る。Resources.Load の名前）</summary>
    public const string HazardRunnerPath = "StagePacing/HazardRunner";

    static int Sum(int[] a)
    {
        int s = 0;
        if (a != null) foreach (int v in a) s += v;
        return s;
    }
}
