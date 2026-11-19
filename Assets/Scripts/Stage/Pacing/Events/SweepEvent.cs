using System.Collections.Generic;
using UnityEngine;

// 【仮】出来事「柱・床」（2026-11-19。docs/stage-pacing-design.md 3-2・3-3。優先度 B-3）。担当2 の物。
// 床の一部に、予告（うすい赤の帯＋部品の予告）→ 短い判定。判定の高さは低い（フォルダ＝高さ3 の上は当たらない）。
// 部品は AreaStrike.Spawn。幅の決まった攻撃データ（2マス）を横に並べて、範囲をすき間なく覆う（index で絵を変える）。
// 絵は出来事用の AttackRunner のスキンが付ける（名前に Breakpoint → 柱、RedWave → 床）。
// 並べ方
// ・Halves（左右交互）：床の半分ずつ。最初はプレイヤーのいる側。前の判定が終わってから、次の予告が始まる（反対側へ動く時間がある）
// ・Playhead（再生ヘッド）：床を5つに分け、左から右へ順に。同時に判定が出るのは1つだけ（間にすき間がある）
// ・WholeFloor（再起動の予告）：床の全部。予告が長い。フォルダの上だけ安全。画面にすり抜け床が無ければ、左右交互に変える
// どの並べ方も、ダメージのすぐあとは次の予告を待つ（時計を止める）。

/// <summary>出来事「柱・床」（Office・Creative・Media・Premiere・Terminal）</summary>
public class SweepEvent : PaceEvent
{
    public enum Pattern
    {
        Halves = 0,
        Playhead = 1,
        WholeFloor = 2,
    }

    /// <summary>再生ヘッドの分け方</summary>
    const int Segments = 5;

    struct Strike
    {
        public float At;   // 予告を始める時刻（出来事の時計）
        public int Slot;   // Halves：0 ＝ 最初の側、1 ＝ 反対側。Playhead：左から何番目。WholeFloor：0
    }

    Pattern _pattern;
    readonly AttackData _data;
    readonly float _gap;
    readonly int[] _counts;
    readonly List<Strike> _plan = new List<Strike>();
    int _nextStrike;
    float _clock;
    float _endAt;
    int _firstSide;   // Halves：最初の側（右 1／左 -1）

    /// <param name="pattern">並べ方</param>
    /// <param name="data">判定の攻撃データ（種類 Area。床の上に出す。名前に Breakpoint か RedWave）</param>
    /// <param name="gap">判定が終わってから、次の予告までの秒数（Playhead は、判定と判定の間のすき間）</param>
    /// <param name="counts">強さ 1・2・3 のときの回数（Playhead は通る回数）</param>
    public SweepEvent(Pattern pattern, AttackData data, float gap, params int[] counts)
    {
        _pattern = pattern;
        _data = data;
        _gap = Mathf.Max(0f, gap);
        _counts = counts != null && counts.Length > 0 ? counts : new[] { 3, 4, 4 };
    }

    public override float MaxSeconds => 18f;

    // 予告は必ず 0.8秒以上（8章）。攻撃データの値（ビルダーが 0.8秒以上にしている）をそのまま使う（部品の予告とずらさない）
    float Telegraph => _data != null ? _data.TelegraphTime : 1f;
    float Active => _data != null ? _data.HitWindow : 0.6f;

    protected override void OnBegin()
    {
        if (_data == null) return;
        int count = _counts[Mathf.Clamp(Strength - 1, 0, _counts.Length - 1)];
        // 再起動の予告：画面にすり抜け床（フォルダ）が無ければ、逃げ場が無いので左右交互に変える
        if (_pattern == Pattern.WholeFloor && !HasPlatform()) _pattern = Pattern.Halves;

        float t = 0f;
        switch (_pattern)
        {
            case Pattern.Playhead:
            {
                float step = Active + Mathf.Max(0.2f, _gap);
                for (int pass = 0; pass < count; pass++)
                {
                    for (int s = 0; s < Segments; s++)
                    {
                        _plan.Add(new Strike { At = t, Slot = s });
                        t += step;
                    }
                    t += Telegraph; // 2回目は、1回目が終わってから
                }
                break;
            }
            default:
            {
                float period = Telegraph + Active + _gap;
                for (int i = 0; i < count; i++)
                {
                    _plan.Add(new Strike { At = t, Slot = _pattern == Pattern.Halves ? i % 2 : 0 });
                    t += period;
                }
                break;
            }
        }
        Strike last = _plan[_plan.Count - 1];
        _endAt = last.At + Telegraph + Active + 0.3f;
    }

    protected override bool OnTick(float dt)
    {
        if (_data == null || _plan.Count == 0) return false;
        // ダメージのすぐあとは、時計を止める（出ている予告・判定はそのまま。次の予告が遅れるだけ）
        if (!Quiet) _clock += dt;
        while (_nextStrike < _plan.Count && _clock >= _plan[_nextStrike].At)
        {
            Fire(_plan[_nextStrike]);
            _nextStrike++;
        }
        return _clock < _endAt;
    }

    void Fire(Strike s)
    {
        Rect a = Area;
        float floorY = FloorY(a);
        float xMin = a.xMin, xMax = a.xMax;
        switch (_pattern)
        {
            case Pattern.Halves:
            {
                if (_nextStrike == 0) _firstSide = PlayerPos.x >= a.center.x ? 1 : -1; // 最初は、プレイヤーのいる側（場所を変えさせる）
                int side = s.Slot == 0 ? _firstSide : -_firstSide;
                if (side > 0) xMin = a.center.x;
                else xMax = a.center.x;
                break;
            }
            case Pattern.Playhead:
            {
                float w = a.width / Segments;
                xMin = a.xMin + w * s.Slot;
                xMax = xMin + w;
                break;
            }
        }
        Cover(xMin, xMax, floorY);
    }

    /// <summary>xMin〜xMax を、決まった幅の判定ですき間なく覆う。上にうすい赤の予告の帯を足す</summary>
    void Cover(float xMin, float xMax, float floorY)
    {
        float unit = Mathf.Max(0.5f, _data.AreaSize.x);
        float range = xMax - xMin;
        if (range < 0.5f) return;
        int n = Mathf.Max(1, Mathf.CeilToInt(range / unit - 0.01f));
        for (int i = 0; i < n; i++)
        {
            float cx = n == 1 ? (xMin + xMax) * 0.5f : Mathf.Lerp(xMin + unit * 0.5f, xMax - unit * 0.5f, i / (float)(n - 1));
            // 床の高さより少し上から真下を探させる（フォルダの上に置かない。穴の上は部屋の床の高さ）
            AreaStrike.Spawn(Runner, _data, new Vector2(cx, floorY + 0.05f), Vector2.up, 0f, i); // 絵の番号は、1回の中で左から 0, 1, 2 …
        }
        PaceZoneMark.Show(Runner, xMin, xMax, floorY, _data.AreaSize.y, Telegraph, Active);
    }

    /// <summary>画面の中に、判定より高い所にすり抜け床（フォルダ）があるか</summary>
    bool HasPlatform()
    {
        Rect a = Area;
        float floorY = FloorY(a);
        int oneWay = LayerMask.GetMask("OneWayPlatform");
        if (oneWay == 0) return false;
        float bottom = floorY + (_data != null ? _data.AreaSize.y : 1.2f) + 0.4f;
        Collider2D c = Physics2D.OverlapArea(new Vector2(a.xMin + 0.5f, bottom), new Vector2(a.xMax - 0.5f, floorY + 4.5f), oneWay);
        return c != null;
    }
}
