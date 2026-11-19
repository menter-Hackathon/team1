using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Slack の場面③の出来事「スレッドが伸びる」（2026-11-19。docs/stage-gimmick-variety-a.md 2章 Slack）。
/// ・床の 3／4／5か所（強さ 1／2／3）に、左から右へ、次は右から左へ、順番にメッセージの行が下から積み上がる
///   （KitMotion.Erupt。1行・2行・3行＝高さ 0.9／1.8／2.7。幅 3。足場の上には出さない＝床から）
///   予告 0.9 → 0.3 でせり上がる → 1.0 止まる → 引っこむ。間は 1.4／1.1／0.9秒【仮】
/// ・3行の柱が上がると、その上に緑の ✓ の丸（HitTarget。3秒。出来事に 2回まで）。
///   叩くと未読の板が 1枚既読（SlackRule.ReadOne。無ければクールタイム 1秒進む）。叩かなくても罰は無い
/// ・隅の通知は 12 replies。危ない物の予告は赤（KitMotion のまま）。@channel・板は出来事の間は出ない（StageRule の CanSpawnHazard）
/// </summary>
public class SlackThreadEvent : KitTimedEvent
{
    const float Width = 3f, RowHeight = 0.9f, Warn = 0.9f, Rise = 0.3f, Hold = 1.0f, TargetLife = 3f;
    const int MaxTargets = 2;
    const int RowPx = 16, WidthPx = 54;

    int _targets;

    public SlackThreadEvent() : base("12 replies") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(SlackKit.Label, stage => new SlackThreadEvent());

    protected override void Build()
    {
        int spots = Level <= 1 ? 3 : Level == 2 ? 4 : 5;
        float interval = Level <= 1 ? 1.4f : Level == 2 ? 1.1f : 0.9f;
        Rect a = View;
        float margin = Width * 0.5f + 0.8f;
        var xs = new List<float>();
        for (int i = 0; i < spots; i++) xs.Add(Mathf.Lerp(a.xMin + margin, a.xMax - margin, spots == 1 ? 0.5f : i / (float)(spots - 1)));
        int n = 0;
        for (int pass = 0; pass < 2; pass++) // 左から右、次は右から左（波のように読める）
        {
            for (int i = 0; i < spots; i++)
            {
                float x = xs[pass == 0 ? i : spots - 1 - i];
                int rows = n % 3 + 1;
                At(n * interval, () => Thread(x, rows));
                n++;
            }
        }
        EndAt(Mathf.Min(10.8f, (n - 1) * interval + Warn + Rise * 2f + Hold + 0.4f));
    }

    void Thread(float x, int rows)
    {
        float h = RowHeight * rows;
        float surface = KitMotion.FloorY(x);
        KitMotion.Erupt(Owner, x, Width, h, Warn, Rise, Hold, ThreadSprite(rows), null, 1, surface);
        GameAudio.Event("gm_slack_unread_add", 1f + 0.1f * (rows - 1));
        if (rows >= 3 && _targets < MaxTargets)
        {
            _targets++;
            Vector2 at = new Vector2(x, surface + h + 0.7f);
            At(Clock + Warn + Rise, () => Check(at));
        }
    }

    void Check(Vector2 at)
    {
        KitMotionTarget t = KitMotion.HitTarget(Owner, at, TargetLife, OnCheck, null, TalkArt.Disc(16), (Color)SlackArt.Green, 0.9f);
        if (t != null && t.Body != null) TalkArt.ShowNative(t.Body, TalkArt.Check, Vector2.zero, KitMotionItem.OrderBody + 2, 1.4f, "Check");
    }

    static void OnCheck()
    {
        GameAudio.Event("gm_slack_read");
        if (!SlackRule.ReadOne()) KitPlayer.AdvanceCooldowns(1f);
    }

    /// <summary>メッセージの行を rows 行積んだ柱（54×16・rows ドット）。白い行・左にアバター（行ごとに色が変わる）・名前と文の棒</summary>
    static Sprite ThreadSprite(int rows)
    {
        rows = Mathf.Clamp(rows, 1, 3);
        char[,] g = GimmickA.Grid(WidthPx, RowPx * rows);
        char[] avatars = { 'b', 'g', 'y', 'r' };
        for (int r = 0; r < rows; r++)
        {
            int y0 = r * RowPx;
            GimmickA.Fill(g, 0, y0, WidthPx, RowPx, 'L');
            GimmickA.Fill(g, 1, y0 + 1, WidthPx - 2, RowPx - 2, 'W');
            GimmickA.Fill(g, 3, y0 + 4, 8, 8, avatars[(r + rows) % avatars.Length]);
            GimmickA.Fill(g, 14, y0 + 4, 16, 2, 'N');
            GimmickA.Fill(g, 32, y0 + 4, 8, 2, 'T');
            GimmickA.Fill(g, 14, y0 + 8, 34 - r * 6, 2, 'T');
            GimmickA.Fill(g, 14, y0 + 11, 22 - r * 4, 2, 'T');
        }
        var pal = new Dictionary<char, Color32>
        {
            { 'L', SlackArt.Line }, { 'W', SlackArt.Paper }, { 'N', SlackArt.NameBar }, { 'T', SlackArt.TextBar },
            { 'b', SlackArt.Blue }, { 'g', SlackArt.Green }, { 'y', SlackArt.Yellow }, { 'r', SlackArt.Purple },
        };
        return RuntimeSprites.FromAscii($"SlackThread_{rows}", GimmickA.Rows(g), pal);
    }
}
