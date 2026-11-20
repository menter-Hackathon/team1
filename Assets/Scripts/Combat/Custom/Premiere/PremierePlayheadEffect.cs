using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 再生ヘッドの本体（2026-11-18。docs/app-boss-skills-design.md 3-2）。PremierePlayhead が出し、あとは自分で進む。
/// タイムラインの帯・マーカー・再生ヘッドを出し、再生ヘッドがマーカーに着いたら、少し待ってディップ トゥ ホワイトの白い柱（AreaStrike）を出す。
/// 2026-11-19（docs/app-feedback-v2-design.md 5-2）：マーカーは赤い下向きの五角形（ルーラーの上）・真下へ赤い点線・柱の幅のうすい赤の帯。
/// 炎・雷のアイコン、円の爆発、黄色の予告は出さない。
/// 再生ヘッドの線には当たり判定を付けない。形態変化・正常化で消されたら、その場で全部消える（まだ出ていない柱は出ない）。
/// 場に残る技ではない（技はこれが終わるまで待つ）
/// 2026-11-20（docs/premiere-edit-screen-design.md 6-2）：編集画面全体（PremiereEditScreen）のときは、帯を下の段のルーラーの上に置き、帯の地・目盛り・時計は描かない
/// （背景のルーラーがある）。マーカーの点線と帯は床から部屋の上の端まで。プログラムモニター（PremiereMonitor）に合図：出たら巻き戻す・
/// マーカーの印をスクラブバーに・再生ヘッドに合わせて映像が流れる・着いたらモニターが白く飛ぶ。判定は今のまま
/// </summary>
public class PremierePlayheadEffect : CustomEffect
{
    class MarkerView
    {
        public Vector2 Position;
        public Transform Root;
        public SpriteRenderer Mark;
        public float AppearAt;
        public int LastPass = -1;                       // 最後に触れた往復の回（同じ回で2回起きない）
        public readonly List<float> Pending = new List<float>(); // 起きる時刻
        public float FlashUntil = float.NegativeInfinity;
        public bool Ticked;                             // 2026-11-20：モニターのスクラブバーに印を出したか
    }

    // ───────── 見た目【仮】 ─────────
    const float StripHeight = 0.5f;               // 帯の高さ
    const float LineWidth = 2f * PremiereArt.Pixel;
    const float TailWidth = 4f * PremiereArt.Pixel;
    const int MarkerOrder = 8; // 帯 6・点線 7（キャラクターより後ろ）。五角形だけ帯の上（TickOrder + 1）
    const int StripOrder = 27, TickOrder = 28, TailOrder = 29, LineOrder = 30, KnobOrder = 31;

    AttackRunner _owner;
    PremierePlayhead _config;
    readonly List<MarkerView> _markers = new List<MarkerView>();
    Rect _arena;
    float _stripTop;
    float _stripBottom;
    float _start;
    float _runStart;   // 再生ヘッドが走り始める時刻
    float _passTime;   // 片道の秒数
    int _passes;
    float _lastX;
    int _lastPass = -1;
    float _endAt = float.PositiveInfinity; // 全部の発動が終わる時刻（決まったら）

    Transform _strip;
    readonly List<SpriteRenderer> _stripParts = new List<SpriteRenderer>();
    Transform _head;
    SpriteRenderer _tail;
    // 2026-11-18：スキルの見た目（docs/app-skill-visuals-design.md 12-2）【仮】
    Transform _timecode;                                     // 帯の右の端の時計（00:00:SS:FF）
    readonly SpriteRenderer[] _digits = new SpriteRenderer[8];
    bool _editScreen;                                        // 2026-11-20：編集画面全体の割り付け（帯は背景のルーラーの上）

    /// <summary>走り終え、最後の白い柱まで出し終えたら true（このあとすぐ消える）</summary>
    public bool Finished { get; private set; }

    public static PremierePlayheadEffect Spawn(AttackRunner owner, PremierePlayhead config, List<Vector2> markers)
    {
        Rect arena = BattleArena.GetBounds();
        GameObject go = owner.CreateSpawnedObject("PremierePlayhead", arena.center);
        var e = go.AddComponent<PremierePlayheadEffect>();
        e._owner = owner;
        e._config = config;
        e._arena = arena;
        e._start = Time.time;
        e._editScreen = PremiereEditScreen.Active;
        e._stripTop = PremiereEditScreen.RulerTop(arena, config.TopInset);
        e._stripBottom = e._stripTop - StripHeight;
        e._runStart = e._start + config.StripTime + config.PlaceTime;
        e._passTime = arena.width / Mathf.Max(0.1f, config.Speed);
        e._passes = config.RoundTrip ? 2 : 1;
        e._lastX = arena.xMin;
        e.BuildStrip();
        e.BuildHead();
        e.BuildTimecode();
        // 左から順に置く（再生ヘッドは左から来るので、どのマーカーも発動の 1秒以上前に見える）
        var ordered = new List<Vector2>(markers);
        ordered.Sort((a, b) => a.x.CompareTo(b.x));
        float interval = ordered.Count > 0 ? config.PlaceTime / ordered.Count : 0f;
        for (int i = 0; i < ordered.Count; i++) e.BuildMarker(ordered[i], e._start + config.StripTime + i * interval);
        if (PremiereMonitor.Current != null) PremiereMonitor.Current.Rewind(Mathf.Max(0.1f, config.StripTime)); // 映像が頭へ巻き戻る
        e.Tick(Time.time);
        return e;
    }

    void Update() => Tick(Time.time);

    void Tick(float now)
    {
        if (Finished) return;

        // 帯：横に広がって出る
        float stripK = _config.StripTime > 0f ? Mathf.Clamp01((now - _start) / _config.StripTime) : 1f;
        _strip.localScale = new Vector3(Mathf.Max(0.001f, stripK), 1f, 1f);
        UpdateTimecode(now, stripK);

        // マーカー：時間が来たら出す。着いたら白く光る（点滅）
        foreach (var m in _markers)
        {
            bool shown = now >= m.AppearAt;
            if (m.Root.gameObject.activeSelf != shown) m.Root.gameObject.SetActive(shown);
            if (shown && !m.Ticked && PremiereMonitor.Current != null)
            {
                // 2026-11-20：スクラブバーの同じ所に赤い印（再生ヘッドが走り終えて、最後の柱が消えるまで）
                m.Ticked = true;
                PremiereMonitor monitor = PremiereMonitor.Current;
                monitor.AddTick(monitor.KAt(m.Position.x), _runStart + _passTime * _passes + _config.TriggerDelay + HitTime() - now, PremiereTimelineArt.EnemyRed);
            }
            if (m.Mark != null)
            {
                bool flash = shown && now < m.FlashUntil && Mathf.FloorToInt(now / 0.05f) % 2 == 0;
                m.Mark.color = flash ? Color.white : PremiereTimelineArt.EnemyRed;
                // 打たれた瞬間：150% → 100%
                float pop = Mathf.Clamp01((now - m.AppearAt) / 0.1f);
                float s = shown ? Mathf.Lerp(1.5f, 1f, pop) : 1f;
                m.Mark.transform.localScale = new Vector3(s, s, 1f);
            }
        }

        // 再生ヘッド
        float elapsed = now - _runStart;
        bool running = elapsed >= 0f && elapsed < _passTime * _passes;
        _head.gameObject.SetActive(running);
        if (running)
        {
            int pass = Mathf.Min(_passes - 1, Mathf.FloorToInt(elapsed / _passTime));
            float k = Mathf.Clamp01((elapsed - pass * _passTime) / _passTime);
            bool forward = pass % 2 == 0;
            float x = forward ? Mathf.Lerp(_arena.xMin, _arena.xMax, k) : Mathf.Lerp(_arena.xMax, _arena.xMin, k);
            if (pass != _lastPass)
            {
                // 折り返し：前の回の残り（端まで）を調べてから、新しい回の始まりから調べ直す
                if (_lastPass >= 0) CheckCross(_lastX, _lastPass % 2 == 0 ? _arena.xMax : _arena.xMin, _lastPass, now);
                _lastX = forward ? _arena.xMin : _arena.xMax;
                _lastPass = pass;
            }
            CheckCross(_lastX, x, pass, now);
            _lastX = x;
            _head.position = new Vector3(x, _head.position.y, 0f);
            if (PremiereMonitor.Current != null) PremiereMonitor.Current.Follow(PremiereMonitor.Current.KAt(x)); // 映像が線と一緒に流れる（帰りは逆に）
            // 尾は、進む向きの後ろ
            _tail.transform.localPosition = new Vector3((forward ? -1f : 1f) * (LineWidth + TailWidth) * 0.5f, _tail.transform.localPosition.y, 0f);
        }
        else if (elapsed >= _passTime * _passes && _lastPass >= 0 && _lastPass < _passes)
        {
            // 走り終えた：最後の回の残り（端まで）を調べる
            CheckCross(_lastX, _lastPass % 2 == 0 ? _arena.xMax : _arena.xMin, _lastPass, now);
            _lastPass = _passes; // 調べ終えた印
        }

        // 発動
        float lastPending = float.NegativeInfinity;
        foreach (var m in _markers)
        {
            for (int i = m.Pending.Count - 1; i >= 0; i--)
            {
                if (now < m.Pending[i])
                {
                    lastPending = Mathf.Max(lastPending, m.Pending[i]);
                    continue;
                }
                m.Pending.RemoveAt(i);
                Fire(m, now);
            }
        }

        // 終わり：走り終え、出す物を出し終え、最後の判定が消えるまで待ってから消える
        if (elapsed >= _passTime * _passes && lastPending == float.NegativeInfinity)
        {
            if (float.IsPositiveInfinity(_endAt)) _endAt = now + HitTime();
            if (now >= _endAt)
            {
                Finished = true;
                Destroy(gameObject);
            }
        }
    }

    float HitTime() => _config.Dip != null ? _config.Dip.HitWindow : 0.3f;

    /// <summary>from から to へ動いた間に、マーカーの x を通ったら、発動を予約する（同じ回では1回だけ）</summary>
    void CheckCross(float from, float to, int pass, float now)
    {
        float lo = Mathf.Min(from, to), hi = Mathf.Max(from, to);
        foreach (var m in _markers)
        {
            if (m.LastPass == pass) continue;
            float mx = m.Position.x;
            if (mx < lo || mx > hi) continue;
            m.LastPass = pass;
            m.Pending.Add(now + _config.TriggerDelay);
            m.FlashUntil = now + _config.TriggerDelay;
            if (PremiereMonitor.Current != null)
            {
                PremiereMonitor.Current.Flash(_config.TriggerDelay + HitTime()); // モニターが先に白く飛ぶ
                PremiereMonitor.Current.Beam(transform, new Vector2(mx, _stripBottom), _config.TriggerDelay, MarkerOrder); // B3：モニターからマーカーへ白い光の線
            }
        }
    }

    /// <summary>帯の右の端の時計（白い数字 8つ。00:00:秒:コマ）</summary>
    void BuildTimecode()
    {
        if (_editScreen) return; // 2026-11-20：モニターの下のタイムコードが代わり
        _timecode = PremiereArt.Child(transform, "Timecode", Vector2.zero);
        float w = (8 * 4 + 3 * 2 + 2) * PremiereArt.Pixel;
        _timecode.position = new Vector3(_arena.xMax - w * 0.5f - 3f * PremiereArt.Pixel, _stripTop - StripHeight * 0.5f, 0f);
        PremiereArt.Box(_timecode, Vector2.zero, new Vector2(w, 9f * PremiereArt.Pixel), PremiereArt.TimelineBg, TickOrder, "Bg");
        float x = -w * 0.5f + 2.5f * PremiereArt.Pixel;
        for (int i = 0; i < 8; i++)
        {
            _digits[i] = RuntimeSprites.Show(_timecode, GenericFrameBits.DigitSprite("0"), new Vector2(x, 0f), TickOrder + 1, 1f, "Digit");
            x += 4f * PremiereArt.Pixel;
            if (i % 2 == 1 && i < 7)
            {
                PremiereArt.Box(_timecode, new Vector2(x - 1.5f * PremiereArt.Pixel, 1f * PremiereArt.Pixel), Vector2.one * PremiereArt.Pixel, Color.white, TickOrder + 1, "Colon");
                PremiereArt.Box(_timecode, new Vector2(x - 1.5f * PremiereArt.Pixel, -1f * PremiereArt.Pixel), Vector2.one * PremiereArt.Pixel, Color.white, TickOrder + 1, "Colon");
                x += 2f * PremiereArt.Pixel;
            }
        }
        _timecode.gameObject.SetActive(false);
    }

    void UpdateTimecode(float now, float stripK)
    {
        if (_timecode == null) return;
        bool show = stripK >= 1f;
        if (_timecode.gameObject.activeSelf != show) _timecode.gameObject.SetActive(show);
        if (!show) return;
        float t = Mathf.Max(0f, now - _runStart);
        int frames = Mathf.FloorToInt(t * 24f);
        int ff = frames % 24, ss = (frames / 24) % 60, mm = (frames / (24 * 60)) % 60;
        int[] v = { 0, 0, mm / 10, mm % 10, ss / 10, ss % 10, ff / 10, ff % 10 };
        for (int i = 0; i < 8; i++) _digits[i].sprite = GenericFrameBits.DigitSprite(v[i].ToString());
    }

    void Fire(MarkerView m, float now)
    {
        if (_owner == null) return;
        SpawnDip(_owner, _config.Dip, m.Position.x);
    }

    /// <summary>
    /// ディップ トゥ ホワイトの白い柱を1本出す（x の真下の床から天井まで）。攻撃データは Area・箱・床の上に置く・予告 0。
    /// エラーの絵の種類は "Dip"（PremiereSkin がメディアオフラインを出す。前の落雷のアセットでも雷の絵にならないように）
    /// </summary>
    public static AreaStrike SpawnDip(AttackRunner owner, AttackData data, float x)
    {
        if (owner == null || data == null) return null;
        Rect arena = BattleArena.GetBounds();
        AreaStrike strike = AreaStrike.Spawn(owner, data, new Vector2(x, arena.yMin + 0.5f), Vector2.up, 0f);
        if (strike == null) return null;
        Hitbox hb = strike.GetComponent<Hitbox>();
        if (hb != null) hb.SetErrorKind("Dip");
        PremiereDip.Attach(strike.transform, data.AreaSize.x, data.AreaSize.y, Mathf.Max(0.2f, data.HitWindow));
        return strike;
    }

    // ───────── 見た目を作る ─────────

    /// <summary>タイムラインの帯（地と目盛り）。左端から横に広がるように、根元を左端に置く</summary>
    void BuildStrip()
    {
        _strip = PremiereArt.Child(transform, "Timeline", Vector2.zero);
        _strip.position = new Vector3(_arena.xMin, _stripTop - StripHeight * 0.5f, 0f);
        if (_editScreen) return; // 2026-11-20：帯の地・目盛りは描かない（背景のルーラーの上。地を描くと V2 のクリップに立つ人の頭が隠れる）
        float w = _arena.width;
        _stripParts.Add(PremiereArt.Box(_strip, new Vector2(w * 0.5f, 0f), new Vector2(w, StripHeight), PremiereArt.TimelineBg, StripOrder, "Bar"));
        // 目盛り：1マスごとに短い線、5マスごとに長い線（上の端から下へ）
        int count = Mathf.FloorToInt(w + 0.001f);
        for (int i = 0; i <= count; i++)
        {
            bool major = i % 5 == 0;
            float h = (major ? 6f : 3f) * PremiereArt.Pixel;
            var tick = PremiereArt.Box(_strip, new Vector2(Mathf.Min(i, w - PremiereArt.Pixel), StripHeight * 0.5f - h * 0.5f),
                new Vector2(PremiereArt.Pixel, h), PremiereArt.TimelineTick, TickOrder, "Tick");
            _stripParts.Add(tick);
        }
    }

    /// <summary>再生ヘッド：青い縦の線（床から帯まで）・上に五角形のつまみ・後ろにうすい青の尾。当たり判定はない</summary>
    void BuildHead()
    {
        float bottom = _arena.yMin, top = _stripBottom;
        float h = Mathf.Max(0.1f, top - bottom);
        _head = PremiereArt.Child(transform, "Playhead", Vector2.zero);
        _head.position = new Vector3(_arena.xMin, (top + bottom) * 0.5f, 0f);
        PremiereArt.Box(_head, Vector2.zero, new Vector2(LineWidth, h), PremiereArt.PlayheadBlue, LineOrder, "Line");
        _tail = PremiereArt.Box(_head, Vector2.zero, new Vector2(TailWidth, h), PremiereArt.WithAlpha(PremiereArt.PlayheadBlue, 0.35f), TailOrder, "Tail");
        // つまみは帯に重ねる（下の先が帯の下の端）
        RuntimeSprites.Show(_head, PremiereArt.PlayheadKnob, new Vector2(0f, h * 0.5f), KnobOrder, 1f, "Knob");
        _head.gameObject.SetActive(false);
    }

    /// <summary>マーカー：ルーラー（帯）の上の赤い五角形・真下へ床まで赤い点線・柱の幅のうすい赤の帯（置いた瞬間から見せる）</summary>
    void BuildMarker(Vector2 position, float appearAt)
    {
        var m = new MarkerView { Position = position, AppearAt = appearAt };
        float rulerY = _stripBottom + 0.5f * PremiereArt.Pixel;
        m.Root = PremiereTimelineArt.BuildMarker(transform, position.x, rulerY, _arena.yMin, PremiereEditScreen.MarkerTop(_arena, _stripBottom), _config.DipWidth, MarkerOrder);
        Transform mark = m.Root.Find("Marker");
        m.Mark = mark != null ? mark.GetComponent<SpriteRenderer>() : null;
        if (m.Mark != null) m.Mark.sortingOrder = TickOrder + 1; // 帯の上に見える
        m.Root.gameObject.SetActive(false);
        _markers.Add(m);
    }
}
