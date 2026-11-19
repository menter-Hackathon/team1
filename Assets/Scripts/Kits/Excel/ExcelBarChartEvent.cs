using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Excel の場面③の出来事「棒グラフ」（2026-11-19。docs/stage-gimmick-variety-b.md 6章）。数値はすべて【仮】。
/// ・床を、セルの幅（2.1マス）で 9列に分ける。1回ごとに：緑の枠のセル（KitMotion.SafeSpot。右下にフィルハンドルの小さい四角）が 1列だけ点滅 →
///   1.2秒後、それ以外の列から、緑の棒が床からせり上がる（KitMotion.Erupt。左の列から 0.08秒ずつずらす・上がる 0.25秒・止まる 0.6秒）
/// ・棒の高さは列ごとにちがう（1.5／2.5／3.5 をグラフらしく）。1回目は全部 2.5 以下（下の足場の上に乗ればよい、と分かる）。
///   3.5 の棒は足場にも届く。危ない列の予告は、床の割れ目と、列の上のうすい赤い棒の形（高さが分かる）
/// ・3回（ステージで 2〜4回）。安全の列は毎回変わる（前の列から 2〜3列となり。プレイヤーから 6マス以内）
/// ・棒が上がると、上に数字（飾り）。最後の回だけ、棒の上をつなぐ折れ線が 0.3秒光る（飾り）
/// </summary>
public class ExcelBarChartEvent : KitTimedEvent
{
    const int Columns = 9;
    const float CellWidth = 2.1f;
    const float SafeWarn = 1.2f, Stagger = 0.08f, Rise = 0.25f, Hold = 0.6f;
    const float RoundStep = 3.8f, RoundStep4 = 3.5f;
    const float NearPlayer = 6f;
    static readonly float[] Low = { 1.5f, 2.5f };
    static readonly float[] Heights = { 1.5f, 2.5f, 3.5f };

    readonly int _stage;
    int _rounds;
    int _lastSafe = -1;

    public ExcelBarChartEvent(int stage) : base("Insert Chart")
    {
        _stage = stage;
    }

    protected override void Build()
    {
        _rounds = Mathf.Clamp(GroupBMotion.Scaled(3, _stage), 1, 4);
        float step = _rounds >= 4 ? RoundStep4 : RoundStep;
        for (int r = 0; r < _rounds; r++)
        {
            int n = r;
            At(r * step, () => Round(n));
        }
        EndAt((_rounds - 1) * step + SafeWarn + Columns * Stagger + Rise * 2f + Hold + 0.4f);
    }

    float ColumnX(int i) => View.center.x + (i - (Columns - 1) * 0.5f) * CellWidth;

    /// <summary>列の左右（はしの列は、壁まで広げる。壁ぎわに逃げ場ができないように）</summary>
    void ColumnSpan(int i, out float center, out float width)
    {
        float xMin = i == 0 ? View.xMin : ColumnX(i) - CellWidth * 0.5f;
        float xMax = i == Columns - 1 ? View.xMax : ColumnX(i) + CellWidth * 0.5f;
        center = (xMin + xMax) * 0.5f;
        width = Mathf.Max(0.5f, xMax - xMin);
    }

    /// <summary>1回分：安全の列を決めて、ほかの列から棒</summary>
    void Round(int round)
    {
        int safe = PickSafe();
        _lastSafe = safe;
        float floor = Floor;
        // 安全の列：緑の枠＋右下のフィルハンドル（then に何もしない関数を渡して、ほかの床からの Erupt は自分で出す）
        ColumnSpan(safe, out float sx, out float sw);
        KitMotionSafe spot = KitMotion.SafeSpot(Owner, sx, sw, SafeWarn, _ => { }, ExcelArt.Green, 1, 3f, 1f, null, null, floor);
        if (spot != null)
            KitMotionMark.Create(spot.transform, PlaceholderSprites.Square, new Vector2(sx + sw * 0.5f - 0.12f, floor + 0.12f),
                new Vector2(0.22f, 0.22f), 0f, ExcelArt.Green, KitMotionItem.OrderMark, SafeWarn + 1f, "FillHandle");
        // ほかの列：左から順に、少しずつずらしてせり上がる
        bool last = round == _rounds - 1;
        float[] pool = round == 0 ? Low : Heights;
        var tops = new List<Vector2>();
        for (int i = 0; i < Columns; i++)
        {
            if (i == safe) continue;
            // グラフらしく：山なりの並び（列ごとにずらす）。1回目は 2.5 まで
            float h = pool[(i + round * 2 + (i / 3)) % pool.Length];
            float delay = SafeWarn + Stagger * i;
            ColumnSpan(i, out float cx, out float cw);
            KitMotion.Erupt(Owner, cx, cw, h, delay, Rise, Hold, BarSprite(h), Color.white, 1, floor);
            Vector2 top = new Vector2(ColumnX(i), floor + h);
            tops.Add(top);
            int value = Mathf.RoundToInt(h * 12f + (i % 3) * 4f);
            float when = Clock + delay + Rise;
            At(when, () => FxKit.Text(value.ToString(), top + Vector2.up * 0.35f, 0.4f, Color.white, 40, Hold + 0.2f, ExcelArt.Green));
        }
        if (last && tops.Count >= 2)
        {
            float when = Clock + SafeWarn + Stagger * Columns + Rise;
            At(when, () => ChartLine(tops));
        }
        GameAudio.Event("gm_excel_bar_up");
    }

    /// <summary>安全の列：前の列から 2〜3列となり・プレイヤーから 6マス以内（無ければ、プレイヤーに近い列）</summary>
    int PickSafe()
    {
        float px = PlayerPos.x;
        var cands = new List<int>();
        for (int i = 0; i < Columns; i++)
        {
            if (Mathf.Abs(ColumnX(i) - px) > NearPlayer) continue;
            if (_lastSafe >= 0)
            {
                int d = Mathf.Abs(i - _lastSafe);
                if (d < 2 || d > 3) continue;
            }
            cands.Add(i);
        }
        if (cands.Count > 0) return cands[Random.Range(0, cands.Count)];
        int best = 0;
        for (int i = 0; i < Columns; i++)
        {
            if (i == _lastSafe) continue;
            if (Mathf.Abs(ColumnX(i) - px) < Mathf.Abs(ColumnX(best) - px) || best == _lastSafe) best = i;
        }
        return best;
    }

    /// <summary>棒の上をつなぐ折れ線（0.3秒光る。飾り）</summary>
    void ChartLine(List<Vector2> tops)
    {
        var holder = new GameObject("Excel_ChartLine");
        Track(holder);
        for (int i = 1; i < tops.Count; i++)
            KitMotion.WarnLine(holder.transform, tops[i - 1] + Vector2.up * 0.1f, tops[i] + Vector2.up * 0.1f, 0.3f, ExcelArt.GreenLight, 3f);
        Object.Destroy(holder, 0.5f);
    }

    /// <summary>棒（幅 2.1・高さ h。緑に、明るい上の帯と、白いセルの線）。高さごとに 1枚</summary>
    static Sprite BarSprite(float h)
    {
        int w = Mathf.RoundToInt(CellWidth * 18f);
        int hp = Mathf.Max(4, Mathf.RoundToInt(h * 18f));
        return KitArt.Paint($"excel.event.bar.{hp}", w, hp, c =>
        {
            Color32 green = ExcelArt.Green, light = ExcelArt.GreenLight, line = new Color32(255, 255, 255, 90);
            c.Fill(2, 0, w - 4, hp, green);
            c.Fill(2, 0, w - 4, 3, light);
            for (int y = 9; y < hp; y += 9) c.HLine(2, y, w - 4, line);
            c.VLine(2, 0, hp, light);
        });
    }
}
