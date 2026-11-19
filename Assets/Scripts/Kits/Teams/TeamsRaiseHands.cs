using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Teams の出来事「ブレイクアウトルーム」に足す「手を挙げる」（2026-11-19。docs/stage-gimmick-variety-a.md 2章 Teams）。TeamsBreakoutEvent が使う。
/// ・左の部屋と右の部屋に 1つずつ、動かない足場（チャットの行・会議のカード）の上 1マスに、手のアイコンの丸（KitMotion.HitTarget）。
///   足場が無い側は床の上。動く通話のバー（callbar）・出たり消えたりする足場（mine）には置かない（GimmickA.SteadyPlatforms）
/// ・予告 0.6 → 出ている 5秒（最後の 1秒は点滅）。叩くと ✓ に変わる
/// ・1組そろう：両方の部屋の雑魚が 1秒ひるむ（雑魚の速さ ×0.5。雑魚を止める仕組みが無いので、遅くする）。
///   まだ組が残っていれば（強さ 2・3 は 2組まで）次の組。最後の組がそろうと onAllDone（部屋が早く閉じる）
/// ・叩かなくても何も起きない（壁は今のまま 12秒で閉じる）
/// </summary>
public class TeamsRaiseHands
{
    const float Life = 5f, Warn = 0.6f, Above = 1.0f, StunSeconds = 1f, StunSpeed = 0.5f;
    static readonly Color Rim = RuntimeSprites.Hex("6F73C4");
    static readonly Color HandColor = RuntimeSprites.Hex("D6B79A");

    readonly KitOwner _owner;
    readonly float _wallX;
    readonly int _maxPairs;
    readonly Action _onAllDone;
    readonly List<KitMotionTarget> _live = new List<KitMotionTarget>();
    int _done, _hitThisPair;
    bool _over;

    public TeamsRaiseHands(KitOwner owner, float wallX, int maxPairs, Action onAllDone)
    {
        _owner = owner;
        _wallX = wallX;
        _maxPairs = Mathf.Max(1, maxPairs);
        _onAllDone = onAllDone;
    }

    /// <summary>1組（左右の部屋に 1つずつ）出す</summary>
    public void SpawnPair(Rect area, float floorY)
    {
        if (_over) return;
        _hitThisPair = 0;
        List<Bounds> plats = GimmickA.SteadyPlatforms(area, Warn + Life);
        Spawn(PointFor(-1, area, floorY, plats));
        Spawn(PointFor(1, area, floorY, plats));
    }

    /// <summary>出ている的を消す（部屋が閉じる時）</summary>
    public void Cancel()
    {
        _over = true;
        foreach (var t in _live) if (t != null && !t.IsDone) t.Cancel();
        _live.Clear();
    }

    Vector2 PointFor(int side, Rect area, float floorY, List<Bounds> plats)
    {
        var mine = new List<Bounds>();
        foreach (Bounds b in plats)
        {
            if (side < 0 && b.max.x < _wallX - 0.3f) mine.Add(b);
            if (side > 0 && b.min.x > _wallX + 0.3f) mine.Add(b);
        }
        if (mine.Count > 0)
        {
            Bounds b = mine[UnityEngine.Random.Range(0, mine.Count)];
            return new Vector2(b.center.x, b.max.y + Above);
        }
        float x = side < 0 ? (area.xMin + _wallX) * 0.5f : (_wallX + area.xMax) * 0.5f;
        return new Vector2(x, floorY + Above + 0.5f);
    }

    void Spawn(Vector2 at)
    {
        KitMotionTarget t = KitMotion.HitTarget(_owner, at, Life, () => OnHit(at), null, HandSprite, Color.white, 1f, 1, null, Warn);
        if (t != null) _live.Add(t);
    }

    void OnHit(Vector2 at)
    {
        if (_over) return;
        FxAnim.Sprite(FxArt.Check, at, 60, 0.6f, Color.white, 2f, "Teams_HandCheck").Pop().Rise(0.4f);
        GameAudio.Event("gm_teams_breakout_mark", 1.3f);
        _hitThisPair++;
        if (_hitThisPair < 2) return;
        // 1組そろった：雑魚がひるむ（1秒遅く）
        _done++;
        StatMods.Zako.Speed.SetFor("teams.raisehand", StunSpeed, StunSeconds, TeamsArt.Icon);
        _live.RemoveAll(x => x == null || x.IsDone);
        if (_done >= _maxPairs)
        {
            _over = true;
            KitMotion.Safe(_onAllDone);
            return;
        }
        SpawnPair(KitMotion.Area, KitMotion.FloorY());
    }

    /// <summary>白い丸に紫のふち、中に薄い橙の手（18×18）</summary>
    static Sprite HandSprite
    {
        get
        {
            char[,] g = GimmickA.Grid(18, 18);
            GimmickA.Disc(g, 9f, 9f, 9f, 'P');
            GimmickA.Disc(g, 9f, 9f, 7.6f, 'W');
            string[] hand =
            {
                ".H.H.H..",
                ".H.H.H..",
                ".HHHHH.H",
                ".HHHHHHH",
                ".HHHHHH.",
                "..HHHHH.",
                "..HHHH..",
                "..HHHH..",
            };
            for (int y = 0; y < hand.Length; y++)
                for (int x = 0; x < hand[y].Length; x++)
                    if (hand[y][x] == 'H') g[5 + y, 5 + x] = 'H';
            var pal = new Dictionary<char, Color32> { { 'P', (Color32)Rim }, { 'W', new Color32(255, 255, 255, 255) }, { 'H', (Color32)HandColor } };
            return RuntimeSprites.FromAscii("TeamsRaiseHand", GimmickA.Rows(g), pal);
        }
    }
}
