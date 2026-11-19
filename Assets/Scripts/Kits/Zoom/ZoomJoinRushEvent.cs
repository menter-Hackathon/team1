using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Zoom の場面③の出来事「入室ラッシュ」（2026-11-19。docs/stage-gimmick-variety-a.md 2章 Zoom）。
/// ・ギャラリーの枠（横 3列 × 床の段と足場の段。足場のタイルの絵に重なる枠は使わない）に、参加者のタイルが奥から大きくなって出る（KitMotion.Pop）
///   1度に 2枚（強さ 2・3 は 3枚）。プレイヤーのいる枠は 2回に 1回だけえらぶ
/// ・予告 1.0（赤い点線の枠と、まん中に「接続中」の 3つの点）→ 出る 0.15＋出ている 0.8秒が当たる → カメラが切れて消える。
///   間は 2.2／1.8／1.5秒、回数 4／5／6（強さ 1／2／3）【仮】
/// ・強さ 3 だけ：消える時に、タイルから弾が 4つ輪に出る（Pop の ringCount 4・毎秒 4マス。消える前の 0.6秒は赤く点滅）
/// ・危ないタイルは橙のふち（#c9a07a・マイク切りの色）が点滅。足場のタイルは灰色のふちのまま
/// ・足場のタイルの絵に重なる枠は使わない。出たり消えたりする足場（YOU・SATO）の真下の枠は、その足場がタイルの出ている間（＋前後 1秒）に消えるなら使わない
/// </summary>
public class ZoomJoinRushEvent : KitTimedEvent
{
    const float Warn = 1.0f, Life = 0.8f, RingSpeed = 4f;
    static readonly Vector2 Tile = new Vector2(2f, 1.5f);
    static readonly Color Orange = RuntimeSprites.Hex("C9A07A");

    readonly List<Vector2> _cells = new List<Vector2>();
    readonly List<List<AppPlatform>> _above = new List<List<AppPlatform>>(); // 枠の真上の、出たり消えたりする足場
    int _wave;

    public ZoomJoinRushEvent() : base("Joining...") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(ZoomKit.Label, stage => new ZoomJoinRushEvent());

    protected override void Build()
    {
        FindCells();
        int waves = Level <= 1 ? 4 : Level == 2 ? 5 : 6;
        float interval = Level <= 1 ? 2.2f : Level == 2 ? 1.8f : 1.5f;
        Every(0f, interval, waves, i => Wave());
        EndAt((waves - 1) * interval + Warn + Life + 0.6f + (Level >= 3 ? 1.5f : 0f));
    }

    /// <summary>ギャラリーの枠：3列 × （床の段・足場の段の上）。足場のタイルの絵に重なる枠は外す</summary>
    void FindCells()
    {
        _cells.Clear();
        _above.Clear();
        Rect a = View;
        float floor = Floor;
        var rows = new List<float> { floor + Tile.y * 0.5f + 0.1f };
        foreach (float t in GimmickA.Tiers(a, floor)) rows.Add(floor + t + Tile.y * 0.5f + 0.15f);
        var arts = new List<Bounds>();
        foreach (AppPlatform p in Object.FindObjectsOfType<AppPlatform>())
        {
            if (p == null) continue;
            // 絵の範囲（消えている足場も、場所で数える。消えた絵の bounds は当てにしない）
            SpriteRenderer sr = p.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null && sr.sprite != null)
            {
                Vector3 size = Vector3.Scale(sr.sprite.bounds.size, sr.transform.lossyScale);
                arts.Add(new Bounds(sr.transform.TransformPoint(sr.sprite.bounds.center), new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 1f)));
            }
            else if (p.TryGetComponent(out BoxCollider2D col)) arts.Add(col.bounds);
        }
        float colW = a.width / 3f;
        foreach (float y in rows)
        {
            if (y + Tile.y * 0.5f > a.yMax - 0.3f) continue;
            for (int c = 0; c < 3; c++)
            {
                Vector2 cell = new Vector2(a.xMin + colW * (c + 0.5f), y);
                var box = new Bounds(cell, new Vector3(Tile.x - 0.3f, Tile.y - 0.3f, 1f));
                bool hit = false;
                foreach (Bounds b in arts)
                {
                    var bb = new Bounds(new Vector3(b.center.x, b.center.y, 0f), new Vector3(b.size.x, b.size.y, 1f));
                    if (bb.Intersects(box)) { hit = true; break; }
                }
                if (!hit) _cells.Add(cell);
            }
        }
        if (_cells.Count == 0) // 枠が見つからない：床の段の 3列
            for (int c = 0; c < 3; c++) _cells.Add(new Vector2(a.xMin + colW * (c + 0.5f), rows[0]));
        // 枠の真上にある、出たり消えたりする足場（消える時刻と、その真下の枠のタイルを重ねないため）
        foreach (Vector2 cell in _cells)
        {
            var list = new List<AppPlatform>();
            foreach (AppPlatform p in Object.FindObjectsOfType<AppPlatform>())
            {
                if (p == null || p.IsSteady) continue;
                Vector3 pos = p.transform.position;
                if (pos.y > cell.y && Mathf.Abs(pos.x - cell.x) < Tile.x) list.Add(p);
            }
            _above.Add(list);
        }
    }

    /// <summary>枠の真上の足場が、タイルが出ている間（＋前後 1秒）に消えるか</summary>
    bool AboveVanishes(int i)
    {
        if (i < 0 || i >= _above.Count) return false;
        foreach (AppPlatform p in _above[i])
            if (p != null && p.IsSolidNow && !p.IsSolidFor(Warn + Life + 1f)) return true;
        return false;
    }

    void Wave()
    {
        int want = Mathf.Min(Level >= 2 ? 3 : 2, _cells.Count);
        Vector2 player = KitMotion.PlayerPos;
        int mine = -1;
        float best = float.MaxValue;
        for (int i = 0; i < _cells.Count; i++)
        {
            Vector2 d = _cells[i] - player;
            if (Mathf.Abs(d.x) > Tile.x * 0.75f || Mathf.Abs(d.y) > Tile.y) continue;
            if (d.sqrMagnitude < best) { best = d.sqrMagnitude; mine = i; }
        }
        bool allowMine = _wave % 2 == 1; // プレイヤーのいる枠は 2回に 1回
        var pool = new List<int>();
        for (int i = 0; i < _cells.Count; i++) if ((i != mine || allowMine) && !AboveVanishes(i)) pool.Add(i);
        var pick = new List<int>();
        if (allowMine && mine >= 0 && pool.Contains(mine)) { pick.Add(mine); pool.Remove(mine); }
        while (pick.Count < want && pool.Count > 0)
        {
            int k = Random.Range(0, pool.Count);
            pick.Add(pool[k]);
            pool.RemoveAt(k);
        }
        foreach (int i in pick) Join(_cells[i], _wave + i);
        if (pick.Count > 0) GameAudio.Event("gm_zoom_tiles");
        _wave++;
    }

    void Join(Vector2 at, int look)
    {
        int ring = Level >= 3 ? 4 : 0;
        KitMotionPop pop = KitMotion.Pop(Owner, at, Warn, Life, ring, ZoomKitArt.ParticipantTile(look), null, 1, 1.2f, RingSpeed, true, 0f, Tile);
        if (pop == null) return;
        // 「接続中」の 3つの点（予告の間）
        for (int d = -1; d <= 1; d++)
            KitMotionMark.Create(pop.transform, KitMotionArt.Disc(4), at + new Vector2(d * 0.3f, 0f), Vector2.one * 0.18f, 0f, Color.white, KitMotionItem.OrderMark, pop.Warn, "Dot");
        // 危ないタイルの橙のふち（点滅）。本体の子なので、出る・消えるのと一緒に大きさが変わる
        if (pop.Body != null)
        {
            int wp = Mathf.RoundToInt(Tile.x * 18f), hp = Mathf.RoundToInt(Tile.y * 18f);
            SpriteRenderer frame = AttackRunner.AddVisual(pop.Body, CallArt.Frame(wp, hp, 2), Orange, Tile, KitMotionItem.OrderBody + 1, "Frame");
            frame.gameObject.AddComponent<SlackBlink>();
        }
    }
}
