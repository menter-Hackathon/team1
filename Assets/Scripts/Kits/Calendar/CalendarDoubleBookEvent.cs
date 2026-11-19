using UnityEngine;

/// <summary>
/// 【仮】カレンダーの場面③の出来事「ダブルブッキング」（2026-11-19。docs/stage-gimmick-variety-a.md 2章 Calendar）。
/// ・予定のブロック（2.2×1.2。CalendarArt.Block・半透明 80％＋進む向きの矢印）が、左の壁から右へ、次は右から左へ、時間の行の高さを横切る
///   （GimmickA.SweepBox。まっすぐ）。行は 3つ（床の上・下の段の足場の高さ・上の段の足場の高さ）。1回に 1つの行（強さ 3 は 2つの行を同時に。
///   残りの 1つは必ず空く）
/// ・予告 1.0（壁ぎわの矢印と、その行の赤い線）→ 毎秒 7マス。間は 2.2／1.8／1.5秒、数 4／5／7（強さ 1／2／3）【仮】。
///   8秒より後には出さない（強さ 3 は 6回になる）
/// ・4秒目：床のまん中あたりに幅 3マスの白い枠「空き時間」（KitMotion.HoldZone）。続けて 2.5秒いると ✓：
///   次の予定の「欠席」の罰を 1回なかったことにする（CalendarRule.Excuse。決まりが無ければクールタイム全回復）。跳んでいても「いる」のまま
/// ・予定の時計（CalendarRule）は出来事の間は止まるので、会議室の時刻とブロックは重ならない
/// </summary>
public class CalendarDoubleBookEvent : KitTimedEvent
{
    const float Warn = 1.0f, Speed = 7f, LastStart = 8f, HoldAt = 4f, HoldSeconds = 2.5f, HoldWidth = 3f;
    static readonly Vector2 BlockSize = new Vector2(2.2f, 1.2f);

    public CalendarDoubleBookEvent() : base("Double booked") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(CalendarKit.Label, stage => new CalendarDoubleBookEvent());

    protected override void Build()
    {
        int count = Level <= 1 ? 4 : Level == 2 ? 5 : 7;
        float interval = Level <= 1 ? 2.2f : Level == 2 ? 1.8f : 1.5f;
        Rect a = View;
        float floor = Floor;
        float low = GimmickA.LowTier(a, floor, 3f), high = GimmickA.HighTier(a, floor, low + 2f);
        float[] lanes = { BlockSize.y * 0.5f + 0.05f, low + BlockSize.y * 0.5f + 0.05f, high + BlockSize.y * 0.5f + 0.05f }; // 床から（まん中）
        int last = -1;
        float lastAt = 0f;
        for (int i = 0; i < count; i++)
        {
            float at = i * interval;
            if (at > LastStart) break;
            lastAt = at;
            KitSide side = i % 2 == 0 ? KitSide.Left : KitSide.Right;
            // 行：前と同じ行は続けない。強さ 3 は 2つの行（残りの 1つは空く）
            int lane = Pick(last, -1);
            int second = Level >= 3 ? Pick(lane, last) : -1;
            last = lane;
            int color = i;
            At(at, () => Block(side, lanes[lane], color));
            if (second >= 0) At(at, () => Block(side, lanes[second], color + 1));
        }
        At(HoldAt, () => KitMotion.HoldZone(Owner, Mathf.Clamp(PlayerPos.x * 0.3f + a.center.x * 0.7f, a.xMin + 2f, a.xMax - 2f), HoldWidth, HoldSeconds,
            OnFree, 6f, null, Color.white, 3f, null, 0.4f));
        EndAt(Mathf.Max(HoldAt + 6.4f, lastAt + Warn + (a.width + BlockSize.x + 1f) / Speed + 0.3f));
    }

    static int Pick(int not1, int not2)
    {
        for (int tries = 0; tries < 12; tries++)
        {
            int l = Random.Range(0, 3);
            if (l != not1 && l != not2) return l;
        }
        for (int l = 0; l < 3; l++) if (l != not1 && l != not2) return l;
        return 0;
    }

    void Block(KitSide side, float y, int color)
    {
        bool fromLeft = side == KitSide.Left;
        GimmickA.SweepBox(Owner, side, y, BlockSize, Speed, 1, 0f, 0f, Warn, CalendarArt.Block(color), new Color(1f, 1f, 1f, 0.8f), 1, 4f,
            (m, n) =>
            {
                if (m == null || m.Body == null) return;
                // 進む向きの矢印（白）。足場の予定（会議室）と見分ける
                SpriteRenderer arrow = AttackRunner.AddVisual(m.Body, KitMotionArt.Chevron, Color.white, new Vector2(0.45f, 0.45f), KitMotionItem.OrderBody + 1, "Arrow");
                arrow.transform.localPosition = new Vector3((fromLeft ? 1f : -1f) * (BlockSize.x * 0.5f - 0.3f), 0f, 0f);
                arrow.transform.localRotation = Quaternion.Euler(0f, 0f, fromLeft ? 0f : 180f);
            });
    }

    static void OnFree()
    {
        if (!CalendarRule.Excuse()) KitPlayer.RefillCooldowns();
        GameAudio.Event("gm_calendar_attend");
    }
}
