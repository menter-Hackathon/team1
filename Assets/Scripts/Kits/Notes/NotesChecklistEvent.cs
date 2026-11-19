using UnityEngine;

/// <summary>
/// 【仮】メモの場面③の出来事「チェックリスト」（2026-11-19。docs/stage-gimmick-variety-b.md 5章）。数値はすべて【仮】。
/// ・黄色い丸「○」の的（白いふち。KitMotion.HitTarget）が 1つずつ出る。場所は UI の足場の上：
///   チェックの行 1（左の下の段）→ チェックの行 2（右の下の段）→ チェックの行 3（右の上の段）。それぞれ 3.5秒
/// ・叩くと「✓」になって次が出る（次の場所へ、黄色い点線がすっと伸びる）。3つ全部で：能力のクールタイムを全部戻す＋
///   メモの決まり（NotesRule）のチェックリストの 1行分に数える
/// ・時間切れ：その的の場所に、インクのしみ（KitMotion.Pop。直径 2.5・予告 0.8秒・1.0秒）が出て、次の的へ進む
/// ・的が出ている間、2.5秒おき（ステージで 3.2／2.5／2.0秒）に、プレイヤーから 3マス以上離れた床に、小さいしみ（直径 1.5）を 1つ
/// ・しみは濃い灰色（#555。真っ黒にしない。敵と見分ける）。予告は赤い点線の丸（KitMotion の標準）
/// </summary>
public class NotesChecklistEvent : KitTimedEvent
{
    /// <summary>的の場所（部屋のまん中・床から）。Tools/art/appbg の Notes の足場 check1・check2・check3 の上</summary>
    static readonly Vector2[] Checks = { new Vector2(-3.1f, 3.85f), new Vector2(3.8f, 3.85f), new Vector2(7.5f, 5.8f) };
    const float TargetLife = 3.5f, TargetSize = 1f, TargetWarn = 0.35f;
    const float BlotSize = 2.5f, BlotWarn = 0.8f, BlotLife = 1f;
    const float SmallSize = 1.5f, SmallLife = 1f, SmallAway = 3f;
    static readonly Color Ink = new Color32(0x55, 0x55, 0x55, 0xFF);

    readonly int _stage;
    int _index = -1;
    int _hits;
    bool _finished;
    float _smallEvery;
    float _nextSmall;
    float _stopAt = -1f;
    KitMotionTarget _target;

    public NotesChecklistEvent(int stage) : base("Checklist")
    {
        _stage = stage;
    }

    protected override void Build()
    {
        int s = Mathf.Clamp(_stage, 1, 3);
        _smallEvery = s == 1 ? 3.2f : s == 2 ? 2.5f : 2f;
        _nextSmall = 1.5f;
        At(0f, () => Next(false));
        EndAt(Checks.Length * (TargetLife + TargetWarn) + BlotWarn + BlotLife + 1.5f);
    }

    Vector2 World(Vector2 spot) => new Vector2(View.center.x + spot.x, Floor + spot.y);

    /// <summary>次の的を出す（全部終わったら、ごほうびを見て終わる）</summary>
    void Next(bool lineFromPrev)
    {
        _index++;
        if (_index >= Checks.Length)
        {
            Finish();
            return;
        }
        Vector2 at = World(Checks[_index]);
        int me = _index;
        _target = KitMotion.HitTarget(Owner, at, TargetLife, () => OnHit(me), () => OnMiss(me), null, NotesArt.Yellow, TargetSize, 1, null,
            _index == 0 ? 0f : TargetWarn);
        // 前の的から、次の的の場所へ、黄色い点線がすっと伸びる（どこへ行くか分かる）
        if (lineFromPrev && _target != null && _index > 0)
            KitMotion.WarnLine(_target.transform, World(Checks[_index - 1]), at, TargetWarn + 0.3f, NotesArt.Yellow, 2f);
    }

    void OnHit(int index)
    {
        _hits++;
        Vector2 at = World(Checks[index]);
        FxAnim.Sprite(NotesArt.Check(), at, 60, 0.7f, Color.white, 2f, "Notes_Check").Pop().Rise(0.3f);
        GameAudio.Event("gm_notes_check", 1f + 0.12f * (_hits - 1));
        Next(true);
    }

    void OnMiss(int index)
    {
        // その場所に、大きいインクのしみ
        KitMotion.Pop(Owner, World(Checks[index]), BlotWarn, BlotLife, 0, null, Ink, 1, BlotSize);
        Next(true);
    }

    /// <summary>3つとも終わった：全部 ✓ なら、ごほうび。少しして出来事を終える</summary>
    void Finish()
    {
        if (_finished) return;
        _finished = true;
        _target = null;
        if (_hits >= Checks.Length)
        {
            KitPlayer.RefillCooldowns();
            NotesRule rule = GroupBMotion.Rule<NotesRule>();
            if (rule != null) rule.CheckOneRow();
            GameAudio.Event("gm_notes_complete");
            FxKit.Ring(KitMotion.PlayerPos, 2f, 0.35f, NotesArt.Yellow);
            _stopAt = Clock + 0.6f;
        }
        else _stopAt = Clock + BlotWarn + BlotLife + 0.2f; // 最後のしみが消えるまで
    }

    protected override void OnFrame(float clock)
    {
        if (_finished)
        {
            if (_stopAt >= 0f && clock >= _stopAt) Stop();
            return;
        }
        if (clock < _nextSmall) return;
        _nextSmall = clock + _smallEvery;
        SmallBlot();
    }

    /// <summary>プレイヤーから 3マス以上離れた床に、小さいしみ</summary>
    void SmallBlot()
    {
        Rect v = View;
        float px = PlayerPos.x;
        float x = float.NaN;
        for (int i = 0; i < 10; i++)
        {
            float c = Random.Range(v.xMin + 1.5f, v.xMax - 1.5f);
            if (Mathf.Abs(c - px) >= SmallAway) { x = c; break; }
        }
        if (float.IsNaN(x)) x = px < v.center.x ? v.xMax - 2f : v.xMin + 2f;
        KitMotion.Pop(Owner, new Vector2(x, Floor + SmallSize * 0.5f), BlotWarn, SmallLife, 0, null, Ink, 1, SmallSize);
    }
}
