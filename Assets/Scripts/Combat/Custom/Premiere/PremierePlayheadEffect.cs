using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 再生ヘッドレーザーの本体（2026-11-18。docs/app-boss-skills-design.md 3-2）。PremierePlayhead が出し、あとは自分で進む。
/// タイムラインの帯・マーカー・再生ヘッドを出し、再生ヘッドがマーカーに触れたら、少し待って爆発・落雷（AreaStrike）を出す。
/// 再生ヘッドの線には当たり判定を付けない。形態変化・正常化で消されたら、その場で全部消える（まだ出ていない爆発・落雷は出ない）。
/// 場に残る技ではない（技はこれが終わるまで待つ）
/// </summary>
public class PremierePlayheadEffect : CustomEffect
{
    /// <summary>マーカー1つ（アイコンの種類と、置く位置）</summary>
    public struct Marker
    {
        public PremiereIcon Kind;
        public Vector2 Position;
    }

    class MarkerView
    {
        public Marker Data;
        public Transform Root;
        public SpriteRenderer Flash;
        public float AppearAt;
        public int LastPass = -1;                       // 最後に触れた往復の回（同じ回で2回発動しない）
        public readonly List<float> Pending = new List<float>(); // 発動する時刻
        public float FlashUntil = float.NegativeInfinity;
    }

    // ───────── 見た目【仮】 ─────────
    const float StripHeight = 0.5f;               // 帯の高さ
    const float LineWidth = 2f * PremiereArt.Pixel;
    const float TailWidth = 4f * PremiereArt.Pixel;
    const float MarkerLineWidth = 1f * PremiereArt.Pixel;
    const float RangeAlpha = 0.22f;
    const int RangeOrder = 6, MarkerLineOrder = 24, MarkerOrder = 25, FlashOrder = 26;
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

    /// <summary>走り終え、最後の爆発・落雷まで出し終えたら true（このあとすぐ消える）</summary>
    public bool Finished { get; private set; }

    public static PremierePlayheadEffect Spawn(AttackRunner owner, PremierePlayhead config, List<Marker> markers)
    {
        Rect arena = BattleArena.GetBounds();
        GameObject go = owner.CreateSpawnedObject("PremierePlayhead", arena.center);
        var e = go.AddComponent<PremierePlayheadEffect>();
        e._owner = owner;
        e._config = config;
        e._arena = arena;
        e._start = Time.time;
        e._stripTop = arena.yMax - config.TopInset;
        e._stripBottom = e._stripTop - StripHeight;
        e._runStart = e._start + config.StripTime + config.PlaceTime;
        e._passTime = arena.width / Mathf.Max(0.1f, config.Speed);
        e._passes = config.RoundTrip ? 2 : 1;
        e._lastX = arena.xMin;
        e.BuildStrip();
        e.BuildHead();
        // 左から順に置く（再生ヘッドは左から来るので、どのマーカーも発動の 1秒以上前に見える）
        var ordered = new List<Marker>(markers);
        ordered.Sort((a, b) => a.Position.x.CompareTo(b.Position.x));
        float interval = ordered.Count > 0 ? config.PlaceTime / ordered.Count : 0f;
        for (int i = 0; i < ordered.Count; i++) e.BuildMarker(ordered[i], e._start + config.StripTime + i * interval);
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

        // マーカー：時間が来たら出す。光っている間は白く点滅
        foreach (var m in _markers)
        {
            bool shown = now >= m.AppearAt;
            if (m.Root.gameObject.activeSelf != shown) m.Root.gameObject.SetActive(shown);
            if (m.Flash != null) m.Flash.enabled = shown && now < m.FlashUntil && Mathf.FloorToInt(now / 0.05f) % 2 == 0;
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

    float HitTime()
    {
        float t = 0f;
        if (_config.Explosion != null) t = Mathf.Max(t, _config.Explosion.HitWindow);
        if (_config.Bolt != null) t = Mathf.Max(t, _config.Bolt.HitWindow);
        return t;
    }

    /// <summary>from から to へ動いた間に、マーカーの x を通ったら、発動を予約する（同じ回では1回だけ）</summary>
    void CheckCross(float from, float to, int pass, float now)
    {
        float lo = Mathf.Min(from, to), hi = Mathf.Max(from, to);
        foreach (var m in _markers)
        {
            if (m.LastPass == pass) continue;
            float mx = m.Data.Position.x;
            if (mx < lo || mx > hi) continue;
            m.LastPass = pass;
            m.Pending.Add(now + _config.TriggerDelay);
            m.FlashUntil = now + _config.TriggerDelay;
        }
    }

    void Fire(MarkerView m, float now)
    {
        if (_owner == null) return;
        if (m.Data.Kind == PremiereIcon.Fire) PremiereFx.Explosion(_owner, _config.Explosion, m.Data.Position);
        else PremiereFx.Bolt(_owner, _config.Bolt, m.Data.Position);
    }

    // ───────── 見た目を作る ─────────

    /// <summary>タイムラインの帯（地と目盛り）。左端から横に広がるように、根元を左端に置く</summary>
    void BuildStrip()
    {
        _strip = PremiereArt.Child(transform, "Timeline", Vector2.zero);
        _strip.position = new Vector3(_arena.xMin, _stripTop - StripHeight * 0.5f, 0f);
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

    /// <summary>マーカー：アイコン・帯へ伸びる細い線・起きる範囲（うすい円か柱）・発動前に光る白い四角</summary>
    void BuildMarker(Marker data, float appearAt)
    {
        var m = new MarkerView { Data = data, AppearAt = appearAt };
        m.Root = PremiereArt.Child(transform, $"Marker_{data.Kind}", Vector2.zero);
        m.Root.position = data.Position;
        Color color = PremiereArt.IconColor(data.Kind);

        // 起きる範囲（置いた瞬間から見せる）
        if (data.Kind == PremiereIcon.Fire)
        {
            float d = _config.Explosion != null ? _config.Explosion.AreaSize.x : 3f;
            AttackRunner.AddVisual(m.Root, PlaceholderSprites.Circle, PremiereArt.WithAlpha(color, RangeAlpha), new Vector2(d, d), RangeOrder, "Range");
        }
        else
        {
            float w = _config.Bolt != null ? _config.Bolt.AreaSize.x : 1.5f;
            var range = PremiereArt.Box(m.Root, Vector2.zero, new Vector2(w, _arena.height), PremiereArt.WithAlpha(color, RangeAlpha), RangeOrder, "Range");
            range.transform.position = new Vector3(data.Position.x, _arena.center.y, 0f);
        }

        // 帯へ伸びる線（タイムラインのここで起きる、の印）
        float lineBottom = data.Position.y + 0.5f;
        float lineLen = _stripBottom - lineBottom;
        if (lineLen > 0.01f)
        {
            var line = PremiereArt.Box(m.Root, Vector2.zero, new Vector2(MarkerLineWidth, lineLen), color, MarkerLineOrder, "Link");
            line.transform.position = new Vector3(data.Position.x, lineBottom + lineLen * 0.5f, 0f);
        }

        RuntimeSprites.Show(m.Root, PremiereArt.Icon(data.Kind), Vector2.zero, MarkerOrder, 1f, "Icon");
        m.Flash = PremiereArt.Box(m.Root, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.85f), FlashOrder, "Flash");
        m.Flash.enabled = false;
        m.Root.gameObject.SetActive(false);
        _markers.Add(m);
    }
}
