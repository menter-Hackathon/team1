using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】本物の VS Code のステージのギミック（2026-11-19。docs/app-feedback-v2-design.md 3-4）。ほかの開発アプリ・代用ボスのステージは今のまま。
/// ・A：デバッグ実行（場面③の出来事）。ボスの技と同じ「行のブレークポイント」が、ボスなしで起きる（ボス戦の前に、行の読み方を覚える所）。
///   2026-11-19：上から降る「ビルドエラーの雨」をやめて、床から出る「エラーの波線」にした（行の判定の前後 0.5秒は出さない）
/// ・A：差分の足場（見た目だけ。ステージ全体）。開いたり閉じたりする足場を、Git の差分の行に見せる（緑の + → 赤の - → 赤い点線の枠）
/// どちらも、ボスの登場（PacePhase.Done）とシーンが変わるときに全部消す
/// </summary>
public static class VSCodeKit
{
    // 「Code」だけの名前は入れない（"Claude Code" などの代用ボスに当たるため。本物の VS Code のステージの名前は「VS Code」）【仮】
    public static readonly string[] Names = { "VS Code", "Visual Studio Code" };
    public static readonly string[] Bundles = { "com.microsoft.VSCode" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        AppStageEvents.Register("VS Code", Names, Bundles, (stage, isFinal) => isFinal ? null : new VSCodeDebugEvent());
        StagePacer.Started -= OnStarted;
        StagePacer.Started += OnStarted;
    }

    static bool IsVSCode(string name, string bundle)
    {
        var row = new AppUsageRow { Name = name ?? "", BundleId = bundle ?? "", Seconds = 1 };
        return AppMatch.Matches(row, Names, Bundles);
    }

    static void OnStarted(StagePacer pacer)
    {
        if (pacer == null || !IsVSCode(pacer.AppName, pacer.BundleId)) return;
        var holder = new GameObject("VSCodeDiffPlatforms");
        holder.transform.SetParent(pacer.transform, false); // シーンが変わると、いっしょに消える
        holder.AddComponent<VSCodeDiffHolder>().Setup(pacer);
    }
}

/// <summary>【仮】差分の足場を付けて、ボスの登場で外す係（2026-11-19）</summary>
public class VSCodeDiffHolder : MonoBehaviour
{
    StagePacer _pacer;
    readonly List<VSCodeDiffPlatform> _added = new List<VSCodeDiffPlatform>();

    public void Setup(StagePacer pacer)
    {
        _pacer = pacer;
        foreach (BlinkingPlatform bp in FindObjectsOfType<BlinkingPlatform>())
        {
            if (bp == null || bp.GetComponent<VSCodeDiffPlatform>() != null) continue;
            var d = bp.gameObject.AddComponent<VSCodeDiffPlatform>();
            if (d.Setup(bp)) _added.Add(d);
            else Destroy(d);
        }
        _pacer.PhaseChanged += OnPhase;
    }

    void OnPhase(PacePhase phase)
    {
        if (phase == PacePhase.Done) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_pacer != null) _pacer.PhaseChanged -= OnPhase;
        foreach (var d in _added) if (d != null) d.Remove();
        _added.Clear();
    }
}

/// <summary>
/// 【仮】開いたり閉じたりする足場を、Git の差分の行に見せる（2026-11-19。docs 3-4）。動き・秒数は変えない（足場の秒数を読んで、同じ時計で絵だけ変える）。
/// ・出ている間：緑 #2EA043（4割）の帯、左の端に白い +
/// ・消える前の点滅：赤 #F85149 に変わり、左の端が -、まん中に取り消しの線
/// ・消えている間：赤い点線の枠だけ
/// 足場の絵（BlinkingPlatform が透明さを書きかえる）には触らず、自分の子の絵を重ねる
/// </summary>
public class VSCodeDiffPlatform : MonoBehaviour
{
    [Serializable]
    class Dump
    {
        public float _onTime = 2.2f;
        public float _offTime = 1.4f;
        public float _warnTime = 0.6f;
        public float _phase;
    }

    static readonly Color Green = RuntimeSprites.Hex("2EA043");
    static readonly Color Red = RuntimeSprites.Hex("F85149");
    const int Order = 3; // 足場の絵の少し手前

    BlinkingPlatform _bp;
    Dump _t;
    Transform _root;
    SpriteRenderer _band, _plus, _minus, _strike;
    readonly List<SpriteRenderer> _frame = new List<SpriteRenderer>();

    public bool Setup(BlinkingPlatform bp)
    {
        _bp = bp;
        try { _t = JsonUtility.FromJson<Dump>(JsonUtility.ToJson(bp)); }
        catch (Exception) { _t = new Dump(); }
        if (_t == null) _t = new Dump();
        // 足場の大きさ：絵（子の SpriteRenderer）の範囲。無ければ当たり判定
        Bounds b = default;
        bool any = false;
        foreach (var sr in bp.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr == null || sr.sprite == null) continue;
            if (!any) { b = sr.bounds; any = true; }
            else b.Encapsulate(sr.bounds);
        }
        if (!any)
        {
            var col = bp.GetComponentInChildren<Collider2D>();
            if (col == null) return false;
            b = col.bounds;
        }
        if (b.size.x < 0.3f || b.size.y < 0.05f) return false;

        float d = BrowserDevFx.Dot;
        Vector2 size = new Vector2(b.size.x, Mathf.Max(0.25f, b.size.y));
        _root = BrowserDevFx.Child(transform, "Diff", Vector2.zero);
        _root.position = b.center;
        _root.rotation = Quaternion.identity;
        Vector3 ls = transform.lossyScale;
        _root.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(ls.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(ls.y)), 1f);
        _band = BrowserDevFx.Rect(_root, Vector2.zero, size, BrowserDevFx.WithAlpha(Green, 0.4f), Order, "Band");
        float leftX = -size.x * 0.5f + 5f * d;
        _plus = BrowserDevFx.Show(_root, BrowserDevFx.Text("+", Color.white, true), new Vector2(leftX, 0f), Order + 1, 1f, "Plus");
        _minus = BrowserDevFx.Show(_root, BrowserDevFx.Text("-", Color.white, true), new Vector2(leftX, 0f), Order + 1, 1f, "Minus");
        _strike = BrowserDevFx.Rect(_root, Vector2.zero, new Vector2(size.x - 12f * d, d), Color.white, Order + 1, "Strike");
        BrowserDevFx.DottedBox(_root, Vector2.zero, size, Red, Order, _frame);
        Apply();
        return true;
    }

    /// <summary>絵を外す</summary>
    public void Remove()
    {
        if (_root != null) Destroy(_root.gameObject);
        Destroy(this);
    }

    void LateUpdate() => Apply();

    void Apply()
    {
        if (_root == null || _t == null) return;
        float cycle = Mathf.Max(0.2f, _t._onTime + _t._offTime);
        float t = Mathf.Repeat(Time.time + _t._phase, cycle);
        bool open = _bp != null ? _bp.IsOpen : t < _t._onTime;
        bool warn = open && t > _t._onTime - _t._warnTime;
        _band.enabled = open;
        _band.color = BrowserDevFx.WithAlpha(warn ? Red : Green, 0.4f);
        _plus.enabled = open && !warn;
        _minus.enabled = open && warn;
        _strike.enabled = open && warn;
        foreach (var f in _frame) if (f != null) f.enabled = !open;
    }
}

/// <summary>
/// 【仮】出来事「デバッグ実行」（2026-11-19。docs 3-4）。隅の通知は Debugging...。部屋の上のまん中に、デバッグの操作バーが出来事の間ずっと出る。
/// ・床の行と、足場の行を、交互に。間は、強さ 1／2／3 で 3.0／2.5／2.0秒。予告 1.0秒は同じ（ボスの技と同じ見た目・同じ避け方）
/// ・行の高さは、その部屋の床と、いちばん低い足場（床から 2〜5マスの上の面）から決める。足場が見つからなければ、床の行だけ
/// ・2026-11-19（docs/stage-gimmick-variety-a.md VS Code）：ログの束の雨（上から）をやめて「エラーの波線」（KitMotion.Erupt。下から）。
///   行のブレークポイントの合間に、プレイヤーの足元 ±2マスへ幅 2マスを 1本（強さ 2・3 は 2本。間を 3マス以上あける）。
///   予告 1.0（床に赤い割れ目）→ せり上がり 0.25 → 止まる 0.6 → 引っこむ。高さ 2.5マス。出来事の間に 3／4／5回【仮】。
///   せり上がる物は「問題」パネルの行（灰青の板に赤い × と文字の棒）。床からだけ出る（足場の上は安全）。穴の上には出さない。
///   せり上がる時刻が行の判定の前後 0.5秒に入る時・当たる間が行の判定と重なる時は出さない（足場の行の時も、床の波線を重ねない）
/// 危ない物は、出来事用の AttackRunner から出す。攻撃データは実行時に作る（名前に Breakpoint：当たると頭の上に ⏸）
/// </summary>
public class VSCodeDebugEvent : PaceEvent
{
    const float Duration = 10.5f;     // 出来事の長さ（GateEventSeconds 12秒の中に収める）
    const float RowQuiet = 0.5f;
    // エラーの波線（下から）【仮】
    const float WaveWarn = 1.0f, WaveRise = 0.25f, WaveHold = 0.6f, WaveHeight = 2.5f, WaveWidth = 2f, WaveSpread = 2f, WaveApart = 5f;
    const float WaveFirst = 0.8f;

    readonly VSCodeLineSettings _cfg = new VSCodeLineSettings();
    AttackData _lineData;
    float _clock;
    float _nextLine;
    float _nextWave;
    int _wavesLeft;
    float _waveGap;
    bool _platformTurn;
    int _lineNo = 12;
    readonly List<float> _hitTimes = new List<float>();   // 行の判定の時刻（_clock の上で）
    readonly List<KitMotionErupt> _waves = new List<KitMotionErupt>();
    readonly List<VSCodeBreakpointLineEffect> _lines = new List<VSCodeBreakpointLineEffect>();
    GameObject _bar;

    public VSCodeDebugEvent()
    {
        Toast = "Debugging...";
    }

    public override float MaxSeconds => 14f;

    protected override void OnBegin()
    {
        _lineData = ScriptableObject.CreateInstance<AttackData>(); // ダメージ 1・ノックバック (6, 5)・のけぞり 0.25
        _lineData.name = "Attack_VSCode_BreakpointLine_Stage";
        _lineData.hideFlags = HideFlags.DontSave;
        _nextLine = 0f;
        _wavesLeft = Strength <= 1 ? 3 : Strength == 2 ? 4 : 5;
        _waveGap = (Duration - WaveFirst - WaveWarn - WaveRise * 2f - WaveHold - 0.3f) / Mathf.Max(1, _wavesLeft);
        _nextWave = WaveFirst;
    }

    float Interval => Strength <= 1 ? 3.0f : Strength == 2 ? 2.5f : 2.0f;

    protected override bool OnTick(float dt)
    {
        _clock += dt;
        _lines.RemoveAll(l => l == null);
        _waves.RemoveAll(w => w == null || w.IsDone);
        if (_bar == null) ShowBar();
        else PlaceBar();

        // 行のブレークポイント（判定が終わりの前に収まる物だけ）
        if (_clock >= _nextLine && _clock + _cfg.Telegraph + _cfg.HitTime + _cfg.CloseTime < Duration)
        {
            _nextLine = _clock + Interval;
            SpawnLine();
        }
        // エラーの波線（下から。行の判定の前後は出さない）
        if (_wavesLeft > 0 && !Quiet && _clock >= _nextWave && _clock + WaveWarn + WaveRise * 2f + WaveHold < Duration)
        {
            if (TrySpawnWave())
            {
                _wavesLeft--;
                _nextWave = _clock + _waveGap;
            }
            else _nextWave = _clock + 0.1f;
        }
        bool more = _clock < Duration || _lines.Count > 0 || _waves.Count > 0;
        if (!more) GameAudio.Event("gm_vscode_debug_end"); // 自分で終わる時だけ（ボスで切られた時は鳴らない）
        return more;
    }

    protected override void OnEnd(bool byBoss)
    {
        foreach (var l in _lines) if (l != null) UnityEngine.Object.Destroy(l.gameObject);
        _lines.Clear();
        if (_bar != null) UnityEngine.Object.Destroy(_bar);
        _bar = null;
        if (_lineData != null) UnityEngine.Object.Destroy(_lineData);
        _lineData = null;
    }

    void SpawnLine()
    {
        Rect room = Area;
        float floor = FloorY(room);
        bool hasPlatform = LowestPlatformTop(room, floor, out float platTop);
        bool platform = hasPlatform && _platformTurn;
        _platformTurn = !_platformTurn;
        var row = new VSCodeBreakpointLineEffect.Row
        {
            Bottom = platform ? platTop : floor,
            Height = platform ? 1.2f : 1.0f,
            HitAt = _cfg.Telegraph,
            LineNo = _lineNo,
        };
        _lineNo += platform ? 3 : 2;
        VSCodeBreakpointLineEffect fx = VSCodeBreakpointLineEffect.Create(Runner, _lineData, _cfg, room, new List<VSCodeBreakpointLineEffect.Row> { row }, true, false);
        if (fx != null)
        {
            _lines.Add(fx);
            _hitTimes.Add(_clock + _cfg.Telegraph);
            GameAudio.Event("gm_vscode_line");
        }
    }

    /// <summary>部屋の中で、いちばん低い足場の上の面（床から 2〜5マス）。無ければ false</summary>
    static bool LowestPlatformTop(Rect room, float floor, out float top)
    {
        top = 0f;
        int mask = LayerMask.GetMask("Ground", "OneWayPlatform");
        if (mask == 0) mask = Physics2D.DefaultRaycastLayers;
        Collider2D[] cols = Physics2D.OverlapAreaAll(new Vector2(room.xMin, floor + 1.5f), new Vector2(room.xMax, floor + 5.5f), mask);
        bool found = false;
        foreach (var c in cols)
        {
            if (c == null || c.isTrigger) continue;
            float y = c.bounds.max.y;
            float h = y - floor;
            if (h < 2f || h > 5f) continue;
            if (c.bounds.size.x > room.width * 0.8f) continue; // 床や壁ではない物
            if (!found || y < top)
            {
                top = y;
                found = true;
            }
        }
        return found;
    }

    bool TrySpawnWave()
    {
        // せり上がる時刻が行の判定の前後 0.5秒に入る・当たる間（せり上がり〜止まる）が行の判定と重なる（前後 0.15秒）なら、今は出さない
        // （当たる間ぜんぶを前後 0.5秒あけると、強さ 3 の行の間 2秒に入らないため）
        float hitFrom = _clock + WaveWarn, hitTo = hitFrom + WaveRise + WaveHold;
        bool Near(float h) => Mathf.Abs(hitFrom - h) < RowQuiet || (hitTo > h - 0.15f && hitFrom < h + _cfg.HitTime + 0.15f);
        foreach (float h in _hitTimes) if (Near(h)) return false;
        if (Near(_nextLine + _cfg.Telegraph)) return false; // まだ出していない、次の行の判定も見る
        Rect a = Area;
        Vector2 player = PlayerPos;
        float half = WaveWidth * 0.5f;
        float minX = a.xMin + half + 0.3f, maxX = a.xMax - half - 0.3f;
        if (minX >= maxX) return false;
        int columns = Strength >= 2 ? 2 : 1;
        var placed = new List<float>();
        for (int attempt = 0; attempt < 10 && placed.Count < columns; attempt++)
        {
            float x = placed.Count == 0
                ? Mathf.Clamp(player.x + UnityEngine.Random.Range(-WaveSpread, WaveSpread), minX, maxX)
                : placed[0] + (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(WaveApart, WaveApart + 2f);
            if (x < minX || x > maxX) continue;
            bool near = false;
            foreach (float p in placed) if (Mathf.Abs(p - x) < WaveApart) near = true;
            if (near) continue;
            // 床（Ground）だけ。穴の上には出さない
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, player.y + 1.5f), Vector2.down, 8f, LayerMask.GetMask("Ground"));
            if (hit.collider == null || hit.distance < 0.05f) continue;
            if (!Physics2D.Raycast(new Vector2(x - half + 0.1f, hit.point.y + 0.2f), Vector2.down, 0.5f, LayerMask.GetMask("Ground"))) continue;
            if (!Physics2D.Raycast(new Vector2(x + half - 0.1f, hit.point.y + 0.2f), Vector2.down, 0.5f, LayerMask.GetMask("Ground"))) continue;
            KitMotionErupt e = KitMotion.Erupt(KitOwner.Of(Track, Runner), x, WaveWidth, WaveHeight, WaveWarn, WaveRise, WaveHold, ProblemRow, null, 1, hit.point.y);
            if (e == null) continue;
            _waves.Add(e);
            placed.Add(x);
        }
        if (placed.Count > 0) GameAudio.Event("gm_vscode_line", 0.8f);
        return placed.Count > 0;
    }

    /// <summary>「問題」パネルの行（36×45）：灰青の板に、赤い × と文字の棒が並ぶ</summary>
    static Sprite ProblemRow
    {
        get
        {
            char[,] g = GimmickA.Grid(36, 45);
            GimmickA.Fill(g, 0, 0, 36, 45, 'O');
            GimmickA.Fill(g, 1, 1, 34, 43, 'B');
            GimmickA.Fill(g, 1, 1, 34, 1, 'R'); // 上のふち：波線の赤
            for (int r = 0; r < 4; r++)
            {
                int y0 = 4 + r * 10;
                for (int k = 0; k < 5; k++) { g[y0 + k, 3 + k] = 'R'; g[y0 + k, 7 - k] = 'R'; }
                GimmickA.Fill(g, 11, y0 + 1, 20 - (r % 2) * 7, 2, 'T');
                GimmickA.Fill(g, 11, y0 + 4, 12 + (r % 3) * 3, 1, 'D');
            }
            var pal = new Dictionary<char, Color32>
            {
                { 'O', RuntimeSprites.Hex("444C56") }, { 'B', RuntimeSprites.Hex("2D333B") }, { 'R', RuntimeSprites.Hex("F85149") },
                { 'T', RuntimeSprites.Hex("ADBAC7") }, { 'D', RuntimeSprites.Hex("768390") },
            };
            return RuntimeSprites.FromAscii("VSCodeProblemRow", GimmickA.Rows(g), pal);
        }
    }

    // ───────── デバッグの操作バー（出来事の間ずっと） ─────────

    void ShowBar()
    {
        _bar = new GameObject("VSCodeDebugBar");
        SpriteRenderer sr = BrowserDevFx.Show(_bar.transform, DevArt.DebugBar(), Vector2.zero, 43, 1f, "Bar");
        sr.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        Track(_bar);
        GameAudio.Event("gm_vscode_debug_start");
        PlaceBar();
    }

    void PlaceBar()
    {
        if (_bar == null) return;
        Rect view = ScreenUi.ViewRect();
        _bar.transform.position = new Vector2(view.center.x, view.yMax - 1.9f - 0.4f); // 上の HUD の下
    }
}
