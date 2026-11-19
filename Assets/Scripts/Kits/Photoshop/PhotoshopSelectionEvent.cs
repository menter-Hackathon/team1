using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Photoshop のステージの出来事「選択範囲」（場面③。2026-11-19。docs/stage-gimmick-variety-c.md 11章）。
/// ・WallCrawl（主）：点線の虫（白と水色の、流れる点線のかたまり）。部屋のふち（壁・天井・床）を回る。速さ 6。2／2／3つ。
///   左下の角から左の壁を上へ（時計回り）と、右下の角から右の壁を上へ（反時計回り）の 2つが、天井のまん中・床のまん中ですれちがう。
///   3つ目（強さ 3）は 1つ目の 0.7秒うしろを続く。予告 1.0秒（角で赤く点滅＋進む向きの矢印）。床を通る時はジャンプで越える（高さ 0.7）
///   ※設計の「左下・右上から床 → 右の壁 → 天井（cw）」は、本当の部屋の大きさ（1周 約 10秒）では SafeSpot の時に床に虫がいるので、
///     出る角と向きを変えた（KitTurn の向きの名前も設計と逆：左下から床を右へ行くのは CCW）。道は自分で作る（下の「止まる」のため）
/// ・SafeSpot：「選択範囲の中だけ残す」。流れる点線の四角（幅 4・床から天井まで・水色）。1回目は x −5、2回目は x ＋5
///   （プレイヤーから 4マス以内・予告の間に歩いて届かない遠さなら反対側）。予告 2.5／2.2／2.0秒（その間、選択の外の床がだんだん市松もように変わる）→
///   選択の外の床ぜんぶに Erupt（低い。高さ 1.5。市松もようの柱）。足場の上は届かない（逃げ場は「選択の中」と「足場」の2つ）
/// ・虫と SafeSpot の時間合わせ：設計の「4秒・10秒は虫が壁か天井」は、本当の部屋の大きさ（1周 約 10秒）と予告では合わないので、
///   ① SafeSpot は、床に虫がいない時に出す（3秒・7秒より後で、いちばん早い時）② SafeSpot の間、床に入ろうとする虫は、壁の下で止まって待つ
/// ・PhotoshopRule の「Platforms の目が閉じる」は、出来事の間は起こさない（PhotoshopRule.SetEventRunning）
/// ・設計は 16秒だったが、StagePacer の打ち切り（20秒）とダメージの止まりがあるので 13.5秒に詰めた（SafeSpot 3.0・7.0秒〜、虫は 13秒で消える）
/// </summary>
public class PhotoshopSelectionEvent : KitTimedEvent
{
    const float BugSize = 0.7f, BugSpeed = 6f, BugWarn = 1f, BugEnd = 13f, ApproachGap = 0.9f;
    const float WalkSpeed = 7f; // プレイヤーの歩く速さ（PlayerController の _moveSpeed）
    const float SafeWidth = 4f, SafeOffset = 5f, SafeHeight = 1.5f, SafeStay = 0.8f, MinFromPlayer = 4f, SafeGap = 0.8f, SafeLate = 4f, TrainDelay = 0.7f;
    static readonly float[] SafeTimes = { 3f, 7f };

    class Bug
    {
        public KitMotionMover Mover;
        public PhotoshopCrawl Crawl;
    }

    readonly List<Bug> _bugs = new List<Bug>();
    readonly HashSet<KitMotionErupt> _skinned = new HashSet<KitMotionErupt>();
    PhotoshopRule _rule;
    KitMotionSafe _safe;
    SpriteRenderer _checkL, _checkR;
    int _safeIndex;
    float _safeDoneAt = -99f;
    bool _holdFloor;

    public PhotoshopSelectionEvent() : base("Selection") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(PhotoshopKit.Label, stage => new PhotoshopSelectionEvent());

    float SafeWarn => Level >= 3 ? 2f : Level == 2 ? 2.2f : 2.5f;

    protected override void Build()
    {
        _rule = StageRules.Current as PhotoshopRule;
        if (_rule != null) _rule.SetEventRunning(true);
        At(0f, () => SpawnBug(KitCorner.BottomLeft, KitTurn.CW));   // 左の壁を上へ
        At(0f, () => SpawnBug(KitCorner.BottomRight, KitTurn.CCW)); // 右の壁を上へ
        if (Level >= 3) At(TrainDelay, () => SpawnBug(KitCorner.BottomLeft, KitTurn.CW));
        At(BugEnd, () => { foreach (var b in _bugs) if (b.Mover != null) b.Mover.Cancel(); _bugs.Clear(); });
        EndAt(BugEnd + 0.5f);
    }

    protected override void OnEnd(bool byBoss)
    {
        if (_rule != null) _rule.SetEventRunning(false);
        _rule = null;
        base.OnEnd(byBoss);
    }

    /// <summary>点線の虫を1つ（KitMotion.WallCrawl と同じ作りで、道だけ自分の物：SafeSpot の間は床の手前で止まる）</summary>
    void SpawnBug(KitCorner start, KitTurn turn)
    {
        Rect box = KitMotion.BoxFor(BugSize);
        var crawl = new PhotoshopCrawl(box, start, turn, BugSpeed, ApproachGap, () => _holdFloor);
        KitPath path = crawl.ToPath();
        Vector2 p0 = path.At(0f);
        KitMotionMover m = KitMotion.Make<KitMotionMover>(Owner, null, "PhotoshopAnts", p0);
        float w = KitMotion.WarnFor(m.Owner, BugWarn, true);
        m.BuildBody(AntsSprite(false), Color.white, Vector2.one * BugSize, false, 1);
        Vector2 d = crawl.StartDir;
        KitMotion.WarnArrow(m.transform, p0 + d * (BugSize * 0.5f + 0.45f), d, w, KitMotion.Red);
        m.Setup(path, w, true);
        GameAudio.Telegraph();
        _bugs.Add(new Bug { Mover = m, Crawl = crawl });
    }

    protected override void OnFrame(float clock)
    {
        _bugs.RemoveAll(b => b.Mover == null || b.Mover.IsDone);
        // 虫の点線を流す（白と水色を入れ替える）
        bool flip = Mathf.FloorToInt(Time.time / 0.12f) % 2 == 0;
        foreach (var b in _bugs) if (b.Mover.Visual != null) b.Mover.Visual.sprite = AntsSprite(flip);

        bool safeLive = _safe != null && !_safe.IsDone;
        if (!safeLive && _safe != null)
        {
            _safe = null;
            _safeDoneAt = clock;
        }
        _holdFloor = safeLive;
        if (safeLive) DressSafe();
        else TryStartSafe(clock);
    }

    void TryStartSafe(float clock)
    {
        if (_safeIndex >= SafeTimes.Length || clock < SafeTimes[_safeIndex] || clock < _safeDoneAt + SafeGap || Quiet) return;
        bool late = clock >= SafeTimes[_safeIndex] + SafeLate;
        if (clock + SafeWarn + SafeStay > BugEnd) { _safeIndex = SafeTimes.Length; return; } // もう間に合わない
        if (!late)
            foreach (var b in _bugs) if (b.Crawl.OnFloor) return; // 床に虫がいる間は待つ
        StartSafe(_safeIndex++);
    }

    void StartSafe(int index)
    {
        Rect a = KitMotion.Area;
        float floor = KitMotion.FloorY();
        float px = KitMotion.PlayerPos.x;
        float x = a.center.x + (index == 0 ? -SafeOffset : SafeOffset);
        float other = a.center.x - (x - a.center.x);
        float reach = SafeWarn * WalkSpeed * 0.8f; // 予告の間に歩いて届く長さ
        // プレイヤーから 4マス以上はなれた方。遠すぎて届かないときも反対側（反対側が 4マス以上はなれていれば）
        if (Mathf.Abs(px - x) < MinFromPlayer || (Mathf.Abs(px - x) > reach && Mathf.Abs(px - other) >= MinFromPlayer)) x = other;
        _safe = KitMotion.SafeSpot(Owner, x, SafeWidth, SafeWarn, null, PhotoshopArt.Blue, 1, SafeHeight, SafeStay, null, Color.white, floor);
        if (_safe == null) return;
        _skinned.Clear();
        // 流れる点線の四角（選択範囲。床から天井まで）
        var ants = new GameObject("SelectionAnts");
        ants.transform.SetParent(_safe.transform, false);
        ants.transform.position = Vector3.zero;
        PhotoshopArt.AntsRect(ants.transform, new Rect(x - SafeWidth * 0.5f, floor, SafeWidth, Mathf.Max(1f, a.yMax - floor)), 30);
        // 選択の外の床：だんだん市松もように（予告の間）
        _checkL = Checker(_safe.transform, a.xMin, x - SafeWidth * 0.5f, floor);
        _checkR = Checker(_safe.transform, x + SafeWidth * 0.5f, a.xMax, floor);
        GameAudio.Event("gm_photoshop_eye_blink");
    }

    static SpriteRenderer Checker(Transform parent, float xMin, float xMax, float floor)
    {
        if (xMax - xMin < 0.2f) return null;
        var go = new GameObject("Checker");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((xMin + xMax) * 0.5f, floor + SafeHeight * 0.5f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PhotoshopArt.CheckerTile;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(xMax - xMin, SafeHeight);
        sr.sortingOrder = KitMotionItem.OrderZone;
        sr.color = new Color(1f, 1f, 1f, 0f);
        return sr;
    }

    void DressSafe()
    {
        float k = _safe.Warn > 0f ? Mathf.Clamp01(_safe.Age / _safe.Warn) : 1f;
        float alpha = k >= 1f ? 0f : 0.55f * k;
        if (_checkL != null) _checkL.color = new Color(1f, 1f, 1f, alpha);
        if (_checkR != null) _checkR.color = new Color(1f, 1f, 1f, alpha);
        // 外の床から出る Erupt を、市松もようの柱にする
        foreach (var e in _safe.GetComponentsInChildren<KitMotionErupt>())
        {
            if (e == null || e.Visual == null || !_skinned.Add(e)) continue;
            SpriteRenderer v = e.Visual;
            Vector2 native = v.sprite != null ? (Vector2)v.sprite.bounds.size : Vector2.one;
            Vector2 size = new Vector2(v.transform.localScale.x * native.x, v.transform.localScale.y * native.y);
            v.sprite = PhotoshopArt.CheckerTile;
            v.drawMode = SpriteDrawMode.Tiled;
            v.size = size;
            v.transform.localScale = Vector3.one;
            v.color = Color.white;
        }
    }

    static readonly Dictionary<char, Color32> AntsPal = new Dictionary<char, Color32>
    {
        { 'W', new Color32(255, 255, 255, 255) },
        { 'B', PhotoshopArt.Blue },
        { 'K', PhotoshopArt.Navy },
    };

    /// <summary>点線の虫（12×12。白と水色のしま。flip で入れ替えて流れて見せる）</summary>
    static Sprite AntsSprite(bool flip) => flip
        ? RuntimeSprites.FromAscii("PhotoshopEvent_AntsA", new[]
        {
            "KKKKKKKKKKKK",
            "KWWWBBBWWWBK",
            "KWWBBBWWWBBK",
            "KWBBBWWWBBBK",
            "KBBBWWWBBBWK",
            "KBBWWWBBBWWK",
            "KBWWWBBBWWWK",
            "KWWWBBBWWWBK",
            "KWWBBBWWWBBK",
            "KWBBBWWWBBBK",
            "KBBBWWWBBBWK",
            "KKKKKKKKKKKK",
        }, AntsPal)
        : RuntimeSprites.FromAscii("PhotoshopEvent_AntsB", new[]
        {
            "KKKKKKKKKKKK",
            "KBBBWWWBBBWK",
            "KBBWWWBBBWWK",
            "KBWWWBBBWWWK",
            "KWWWBBBWWWBK",
            "KWWBBBWWWBBK",
            "KWBBBWWWBBBK",
            "KBBBWWWBBBWK",
            "KBBWWWBBBWWK",
            "KBWWWBBBWWWK",
            "KWWWBBBWWWBK",
            "KKKKKKKKKKKK",
        }, AntsPal);
}

/// <summary>
/// 【仮】部屋のふちを這う道（KitPath.Perimeter と同じ向き。CCW＝左下からなら床を右へ → 右の壁を上へ…、CW はその逆）。2026-11-19。
/// 進む距離を毎フレーム足していく道（KitPath の秒は前へしか進まない前提）。hold() が true の間、床に入る手前 gap マスで止まって待つ。
/// 終わりの無い道（KitPath.Duration＝無限）。出来事の側で消す
/// </summary>
public class PhotoshopCrawl
{
    readonly Vector2[] _pts = new Vector2[5];
    readonly float[] _len = new float[4];
    readonly float _perim, _floorStart, _floorLen, _speed, _gap;
    readonly Func<bool> _hold;
    float _lastT, _dist;

    public PhotoshopCrawl(Rect box, KitCorner start, KitTurn turn, float speed, float gap, Func<bool> hold)
    {
        // 反時計回りの角の順：左下(0) → 右下(1) → 右上(2) → 左上(3)。時計回りは逆の順
        var ccw = new[] { new Vector2(box.xMin, box.yMin), new Vector2(box.xMax, box.yMin), new Vector2(box.xMax, box.yMax), new Vector2(box.xMin, box.yMax) };
        int s = start == KitCorner.BottomLeft ? 0 : start == KitCorner.BottomRight ? 1 : start == KitCorner.TopRight ? 2 : 3;
        int step = turn == KitTurn.CCW ? 1 : 3;
        var idx = new int[5];
        for (int i = 0; i <= 4; i++)
        {
            idx[i] = (s + step * i) % 4;
            _pts[i] = ccw[idx[i]];
        }
        float acc = 0f, floorAt = 0f;
        for (int i = 0; i < 4; i++)
        {
            if (idx[i] <= 1 && idx[i + 1] <= 1) floorAt = acc; // 下の角どうし＝床
            _len[i] = (_pts[i + 1] - _pts[i]).magnitude;
            acc += _len[i];
        }
        _perim = Mathf.Max(0.01f, acc);
        _floorStart = floorAt;
        _floorLen = box.width;
        _speed = speed;
        _gap = gap;
        _hold = hold;
    }

    /// <summary>はじめに進む向き</summary>
    public Vector2 StartDir => (_pts[1] - _pts[0]).sqrMagnitude > 1e-6f ? (_pts[1] - _pts[0]).normalized : Vector2.right;

    /// <summary>今、床の上を進んでいるか</summary>
    public bool OnFloor => Mathf.Repeat(_dist - _floorStart, _perim) < _floorLen;

    /// <summary>床に入る手前（gap マス以内）か</summary>
    bool Approaching
    {
        get
        {
            float g = Mathf.Repeat(_floorStart - _dist, _perim);
            return g > 0.001f && g <= _gap;
        }
    }

    public KitPath ToPath() => new KitPath(At, float.PositiveInfinity);

    Vector2 At(float t)
    {
        if (t <= _lastT)
        {
            if (t <= 0f) _dist = 0f;
            _lastT = Mathf.Max(0f, t);
            return Pos(_dist);
        }
        float dt = t - _lastT;
        _lastT = t;
        bool wait = _hold != null && _hold() && Approaching;
        if (!wait) _dist += dt * _speed;
        return Pos(_dist);
    }

    Vector2 Pos(float dist)
    {
        float m = Mathf.Repeat(dist, _perim);
        for (int i = 0; i < 4; i++)
        {
            if (m <= _len[i] || i == 3) return _len[i] > 1e-5f ? Vector2.Lerp(_pts[i], _pts[i + 1], Mathf.Clamp01(m / _len[i])) : _pts[i];
            m -= _len[i];
        }
        return _pts[0];
    }
}
