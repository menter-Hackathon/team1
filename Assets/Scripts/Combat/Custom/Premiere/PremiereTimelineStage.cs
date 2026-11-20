using System.Collections.Generic;
using UnityEngine;
using K = GenericFrameKit;

/// <summary>
/// 【仮】Premiere Pro のタイムラインのステージ（2026-11-19。docs/app-feedback-v2-design.md 5-6）。PremiereKit が、Premiere Pro のステージで付ける。
/// シーンもシーンを作るツールも変えない。実行時に飾りつける。床と足場2枚の位置・当たり判定は変えない。
/// ・見た目（ボス戦でも残す）：壁紙の上にパネルの暗い幕、上にルーラー（目盛り・数字・青いタイムコード）とレンダリングのバー（ボスまでの進み）、
///   トラックの線（V1・V2・V3）と左の札、足場＝クリップ（足場の子に描く。レーザーツールが足場を消すと一緒に消える）、床＝音声トラック（緑の帯と波形）
/// ・決まり「再生ヘッドとマーカー」（雑魚の時間だけ）：青い再生ヘッドが部屋の左から右へ 8秒で1回、流れ続ける（線には当たらない）。
///   場面②③⑤の間は、通りごとに赤いマーカーを打つ（強さ 1／2／3 → 1／2／3個。③は +1）。プレイヤーの近く（±3マス）、
///   再生ヘッドより 4マス以上・1.6秒以上先。再生ヘッドが着くとマーカーが白く光り、0.25秒後にディップ トゥ ホワイトの白い柱
///   （幅 1.5、0.3秒。ライフ -1。ボスの技と同じ読み方）。雑魚も倒れる（B）
/// ・出来事③（PremiereRenderEvent）の間：書き出しのウィンドウ（進みのバー）、再生ヘッドが2倍の速さ（B）
/// ・ボスの登場（PacePhase.Done）で、再生ヘッド・マーカー・書き出しのウィンドウを消す。見た目は残す
/// 重ねる順番：飾りはどれも 8 以下（レーザーツールの黒い列 9 の下。キャラクター 11〜 より後ろ）
/// 2026-11-20（docs/premiere-edit-screen-design.md）：編集画面全体の割り付け（PremiereEditScreen）が有効なときは、ルーラーを下の段（py 78）に下げ、
///   上の段のプログラムモニター（PremiereMonitor）を作る。再生ヘッドの位置をモニターに渡し（Follow）、白い柱でモニターが白く飛ぶ（Flash）。
///   マーカーの赤い点線・帯は床から部屋の上の端まで（上の段にいても見える）。書き出しのウィンドウは右上のパネルの上。ボスの登場でモニターはウイルスを映す
/// </summary>
public class PremiereTimelineStage : MonoBehaviour
{
    // ───────── 数値【仮】 ─────────
    const float PassSeconds = 8f;
    const float RulerHeight = 0.6f;
    const float MarkerAhead = 4f;      // 再生ヘッドより、これ以上先
    const float MarkerLead = 1.6f;     // 打たれてから着くまで、これ以上の秒数
    const float NearPlayer = 3f;       // プレイヤーの x から ±この距離に打つ
    const float MarkerSpacing = 2.2f;  // マーカーどうしを離す
    const float PlaceInterval = 0.7f;  // 1つ打ってから次を打つまで
    const float DipWidth = 1.5f;
    const float DipTime = 0.3f;
    const float DipDelay = 0.25f;      // 着いてから柱が出るまで（マーカーが白く光る。ボスの技と同じ読み方）
    const float ExportSpeedUp = 2f;    // 書き出しの間の再生ヘッドの速さ（B）
    const bool ZakoHitByDip = true;    // B：雑魚も白い柱で倒れる
    static readonly float[] TrackHeights = { 3f, 5.5f, 8f };

    // ───────── 重ねる順番 ─────────
    const int OrderPanel = -95, OrderTrack = -90, OrderLabel = -89;
    const int OrderAudio = 3, OrderClip = 3;
    const int OrderRuler = 4, OrderTick = 5, OrderHeadLine = 7, OrderMarker = 8, OrderHeadKnob = 8;
    const int OrderExport = 60;

    /// <summary>今のステージのタイムライン（無ければ null）</summary>
    public static PremiereTimelineStage Current { get; private set; }

    class MarkerView
    {
        public float X;
        public Transform Root;
        public SpriteRenderer Mark;
        public float FiredAt = -1f;   // 再生ヘッドが着いた時刻
        public bool Spawned;          // 柱を出したか
        public bool Late;             // 2026-11-19：着いたが DipHold で待っている
    }

    /// <summary>【仮】2026-11-19：true を返す x のマーカーは、今は光らせない（出来事の波が通る前後。再生ヘッドが通っても待ち、外れたら光る）</summary>
    public System.Func<float, bool> DipHold;
    /// <summary>【仮】2026-11-19：true を返す x には、マーカーを打たない（出来事の流れる床の間）</summary>
    public System.Func<float, bool> PlaceBlock;

    StagePacer _pacer;
    Rect _arena;
    Rect _view;
    float _rulerTop, _rulerBottom;
    int _strength = 2;
    bool _ruleOn = true;

    Transform _look, _rule;
    SpriteRenderer _renderGreen, _renderRed;
    readonly SpriteRenderer[] _digits = new SpriteRenderer[8];
    float _clock;

    Transform _head;
    float _headX;
    int _toPlace;
    float _nextPlaceAt;
    readonly List<MarkerView> _markers = new List<MarkerView>();

    AttackRunner _runner;
    AttackData _dip;

    Transform _export;
    MacProgress _exportBar;
    PremiereMonitor _monitor;   // 2026-11-20：編集画面全体のときだけ（無ければ null）
    float _exportStart, _exportLen;
    bool _exporting;

    /// <summary>付ける（1ステージに1つ）</summary>
    public static PremiereTimelineStage Attach(StagePacer pacer)
    {
        if (pacer == null) return null;
        if (Current != null) Destroy(Current.gameObject);
        var go = new GameObject("PremiereTimelineStage");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, pacer.gameObject.scene);
        var s = go.AddComponent<PremiereTimelineStage>();
        s.Setup(pacer);
        return s;
    }

    void Setup(StagePacer pacer)
    {
        Current = this;
        _pacer = pacer;
        _arena = BattleArena.GetBounds();
        Rect view = ScreenUi.ViewRect();
        _view = view.width > 1f ? view : new Rect(_arena.xMin - 1f, _arena.yMin - 2f, _arena.width + 2f, _arena.height + 4f);
        _rulerTop = PremiereEditScreen.RulerTop(_arena, PremiereArt.HudInset); // 2026-11-20：編集画面全体なら下の段のルーラー
        _rulerBottom = _rulerTop - RulerHeight;
        try { _strength = Mathf.Clamp(StagePaceTable.For(pacer.StageNumber, pacer.IsFinal).Strength, 1, 3); }
        catch (System.Exception) { _strength = 2; }
        pacer.PhaseChanged += OnPhase;

        _look = PremiereArt.Child(transform, "Look", Vector2.zero);
        _rule = PremiereArt.Child(transform, "Rule", Vector2.zero);
        BuildPanel();
        // 2026-11-19：アプリの UI の足場（AppStagePlatforms）を出しているときは、背景の絵（Bg_PremierePro）にルーラー・トラック・音声トラックが
        // 描いてあり、足場のクリップもそれに合っているので、ここでは作らない（再生ヘッド・マーカー・白い柱は今のまま。高さは部屋とルーラーの位置で決まる）
        bool painted = AppStagePlatforms.Active;
        if (!painted) BuildRuler();
        if (!painted) BuildTracks();
        DressPlatforms();
        if (!painted) BuildAudio();
        BuildHead();
        PrepareHazard();
        try { _monitor = PremiereMonitor.Create(transform); } // 2026-11-20：割り付けが有効なときだけ作る
        catch (System.Exception e) { Debug.LogException(e); _monitor = null; }
        if (_monitor != null) BossEntrance.CustomAppear = PremiereBossCrawl.Run; // 2026-11-20（10章）：ボスはモニターから這い出す
        _headX = _arena.xMin;
        BeginPass();
    }

    void OnDestroy()
    {
        if (_pacer != null) _pacer.PhaseChanged -= OnPhase;
        ClearRule();
        if (_monitor != null) Destroy(_monitor.gameObject);
        if (BossEntrance.CustomAppear == (System.Func<BossController, System.Collections.IEnumerator>)PremiereBossCrawl.Run) BossEntrance.CustomAppear = null;
        if (Current == this) Current = null;
    }

    void OnPhase(PacePhase phase)
    {
        if (phase == PacePhase.Done)
        {
            ClearRule();
            if (_monitor != null) _monitor.EnterBoss(); // 映像が頭へ巻き戻り、ここからはウイルスが映る
        }
    }

    /// <summary>2026-11-20：再生ヘッドの位置（部屋の左の壁＝0、右の壁＝1）</summary>
    public float HeadK => _arena.width > 0.01f ? Mathf.Clamp01((_headX - _arena.xMin) / _arena.width) : 0f;

    bool MarkersAllowed => _pacer != null && (_pacer.Phase == PacePhase.Build || _pacer.Phase == PacePhase.Event || _pacer.Phase == PacePhase.Climax);

    void Update()
    {
        UpdateRenderBar();
        if (!_ruleOn) return;
        float dt = Time.deltaTime;
        _clock += dt;
        UpdateTimecode();

        // 再生ヘッド：左から右へ。右の端に着いたら左の端から
        float speed = _arena.width / PassSeconds * (_exporting ? ExportSpeedUp : 1f);
        float prev = _headX;
        _headX += speed * dt;
        CheckCross(prev, Mathf.Min(_headX, _arena.xMax));
        if (_headX >= _arena.xMax)
        {
            _headX = _arena.xMin;
            GameAudio.Event("gm_premiere_pass");
            BeginPass();
        }
        _head.position = new Vector3(_headX, _head.position.y, 0f);
        if (_monitor != null) _monitor.Follow(HeadK);

        PlaceMarkers(speed);
        UpdateMarkers();
        UpdateExport();
    }

    // ───────── マーカー ─────────

    void BeginPass()
    {
        _toPlace = MarkersAllowed && _runner != null && _dip != null ? _strength + (_exporting ? 1 : 0) : 0;
        _nextPlaceAt = Time.time + 0.3f;
    }

    void PlaceMarkers(float speed)
    {
        if (_toPlace <= 0 || Time.time < _nextPlaceAt) return;
        if (!MarkersAllowed) { _toPlace = 0; return; }
        _nextPlaceAt = Time.time + PlaceInterval;
        Transform player = _pacer != null ? _pacer.Player : null;
        if (player == null) return;
        float lead = Mathf.Max(MarkerAhead, MarkerLead * speed);
        float minX = Mathf.Max(_headX + lead, _arena.xMin + DipWidth * 0.5f);
        float maxX = _arena.xMax - DipWidth * 0.5f;
        float lo = Mathf.Max(minX, player.position.x - NearPlayer);
        float hi = Mathf.Min(maxX, player.position.x + NearPlayer);
        if (hi < lo) return; // プレイヤーが再生ヘッドのすぐ先・後ろにいる：この回は待つ
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float x = Random.Range(lo, hi);
            bool ok = true;
            foreach (var m in _markers) if (m.FiredAt < 0f && Mathf.Abs(m.X - x) < MarkerSpacing) ok = false;
            if (ok && PlaceBlock != null && Held(PlaceBlock, x)) ok = false;
            if (!ok) continue;
            AddMarker(x);
            _toPlace--;
            return;
        }
    }

    void AddMarker(float x)
    {
        var m = new MarkerView { X = x };
        m.Root = PremiereTimelineArt.BuildMarker(_rule, x, _rulerBottom + 0.5f * PremiereArt.Pixel, _arena.yMin, PremiereEditScreen.MarkerTop(_arena, _rulerBottom), DipWidth, OrderMarker);
        Transform mark = m.Root.Find("Marker");
        m.Mark = mark != null ? mark.GetComponent<SpriteRenderer>() : null;
        if (m.Mark != null) GenericFrameTweens.Of(m.Mark.gameObject).Scale(m.Mark.transform, Vector3.one * 1.5f, Vector3.one, 0.1f);
        _markers.Add(m);
        GameAudio.Event("gm_premiere_marker");
        // 2026-11-20（B2）：スクラブバーにも同じ所の赤い印（再生ヘッドが着いて柱が消えるころまで）
        if (_monitor != null)
        {
            float speed = _arena.width / PassSeconds * (_exporting ? ExportSpeedUp : 1f);
            float until = Mathf.Max(0f, x - _headX) / Mathf.Max(0.1f, speed) + DipDelay + DipTime + 1f;
            _monitor.AddTick(_monitor.KAt(x), until, PremiereTimelineArt.EnemyRed);
        }
    }

    void CheckCross(float from, float to)
    {
        foreach (var m in _markers)
        {
            if (m.FiredAt >= 0f || m.X < from || m.X > to) continue;
            if (DipHold != null && Held(DipHold, m.X)) { m.Late = true; continue; } // 波が通る前後：待つ
            m.FiredAt = Time.time;
            GameAudio.Event("gm_premiere_dip");
            if (_monitor != null) _monitor.Flash(DipDelay + DipTime); // モニターが先に白く飛ぶ
        }
    }

    static bool Held(System.Func<float, bool> f, float x)
    {
        try { return f(x); }
        catch (System.Exception e) { Debug.LogException(e); return false; }
    }

    void UpdateMarkers()
    {
        for (int i = _markers.Count - 1; i >= 0; i--)
        {
            MarkerView m = _markers[i];
            if (m.FiredAt < 0f && m.Late && (DipHold == null || !Held(DipHold, m.X)))
            {
                m.Late = false;
                m.FiredAt = Time.time; // 待っていた分、遅れて光る
                GameAudio.Event("gm_premiere_dip");
                if (_monitor != null) _monitor.Flash(DipDelay + DipTime);
            }
            if (m.FiredAt < 0f) continue;
            float age = Time.time - m.FiredAt;
            if (m.Mark != null) m.Mark.color = Mathf.FloorToInt(age / 0.05f) % 2 == 0 ? Color.white : PremiereTimelineArt.EnemyRed;
            if (!m.Spawned && age >= DipDelay)
            {
                m.Spawned = true;
                FireDip(m.X);
            }
            if (age < DipDelay + DipTime + 0.05f) continue;
            if (m.Root != null) Destroy(m.Root.gameObject);
            _markers.RemoveAt(i);
        }
    }

    void FireDip(float x)
    {
        if (_runner == null || _dip == null) return;
        AreaStrike strike = PremierePlayheadEffect.SpawnDip(_runner, _dip, x);
        if (ZakoHitByDip && strike != null) KillZakoIn(new Rect(x - DipWidth * 0.5f, _arena.yMin, DipWidth, _arena.height));
    }

    static readonly Collider2D[] HitBuffer = new Collider2D[32];

    /// <summary>B：柱の中の雑魚も倒れる（倒した数に入る）。ボス・ウイルス本体・プレイヤーは外す</summary>
    void KillZakoIn(Rect r)
    {
        var filter = new ContactFilter2D { useTriggers = true };
        int n = Physics2D.OverlapBox(r.center, r.size, 0f, filter, HitBuffer);
        var seen = new HashSet<Health>();
        for (int i = 0; i < n; i++)
        {
            if (HitBuffer[i] == null || !HitBuffer[i].TryGetComponent(out Hurtbox hurt)) continue;
            if (!(hurt.Owner is Health h) || h == null || h.IsDead || h.Team != Team.Enemy || !seen.Add(h)) continue;
            if (h.GetComponentInParent<BossController>() != null || h.GetComponent<PlayerController>() != null) continue;
            var info = new DamageInfo
            {
                Amount = Mathf.Max(1, h.Current),
                SourceTeam = Team.Player,
                HitPoint = hurt.Collider != null ? (Vector2)hurt.Collider.bounds.center : (Vector2)h.transform.position,
                Source = gameObject,
                Skin = SkinId.Premiere,
                ErrorKind = "Dip",
            };
            h.TryTakeDamage(info);
        }
    }

    /// <summary>白い柱の攻撃データ（実行時に作る）と、出来事用の AttackRunner（敵・当てる相手はプレイヤー）。作れなければ柱は出さない</summary>
    void PrepareHazard()
    {
        _runner = PaceAssets.CreateRunner();
        if (_runner != null)
        {
            _runner.name = "PremiereTimelineRunner";
            _runner.transform.SetParent(transform, true);
            _runner.Skin = SkinId.Premiere;
            _runner.SkinTheme = _pacer.Theme;
            _runner.SkinIcon = _pacer.Icon;
            if (_pacer.Player != null) _runner.Target = _pacer.Player;
        }
        _dip = MakeDipData();
    }

    static AttackData _dipCache;

    static AttackData MakeDipData()
    {
        if (_dipCache != null) return _dipCache;
        var d = ScriptableObject.CreateInstance<AttackData>();
        d.name = "Attack_Premiere_StageDip";
        d.hideFlags = HideFlags.DontSave;
        string json = "{" +
            $"\"_kind\":{(int)AttackKind.Area},\"_anchor\":{(int)AreaAnchor.TargetPosition},\"_shape\":{(int)AreaShape.Box}," +
            "\"_snapToGround\":true,\"_alignToSurface\":false,\"_areaSize\":{\"x\":1.5,\"y\":12.0},\"_areaCount\":1," +
            "\"_windup\":0.0,\"_telegraphTime\":0.0,\"_activeTime\":0.3,\"_persistDuration\":0.0," +
            "\"_damage\":1,\"_gaugeGain\":0,\"_knockback\":{\"x\":6.0,\"y\":5.0},\"_hitStun\":0.25," +
            "\"_color\":{\"r\":1.0,\"g\":1.0,\"b\":1.0,\"a\":0.0}}";
        try { JsonUtility.FromJsonOverwrite(json, d); }
        catch (System.Exception e) { Debug.LogWarning($"Premiere のステージ：白い柱の攻撃データを作れません（{e.Message}）"); return null; }
        if (d.Kind != AttackKind.Area || d.AreaSize.x < 1f) { Debug.LogWarning("Premiere のステージ：白い柱の攻撃データの値が入りません。柱は出しません"); return null; }
        return _dipCache = d;
    }

    /// <summary>決まり（再生ヘッド・マーカー・書き出し）を消す。見た目は残す</summary>
    void ClearRule()
    {
        _ruleOn = false;
        _toPlace = 0;
        _markers.Clear();
        _exporting = false;
        if (_rule != null) Destroy(_rule.gameObject);
        _rule = null;
        if (_export != null) Destroy(_export.gameObject);
        _export = null;
        if (_runner != null)
        {
            _runner.Cancel();
            _runner.ClearSpawned();
            Destroy(_runner.gameObject);
        }
        _runner = null;
    }

    // ───────── 書き出し（出来事③。B） ─────────

    /// <summary>書き出しを始める（ウィンドウと進みのバー。再生ヘッドが2倍。マーカーは通りごとに +1）</summary>
    public void BeginExport(float seconds)
    {
        if (!_ruleOn) return;
        _exporting = true;
        _exportStart = Time.time;
        GameAudio.Event("gm_premiere_export_start");
        _exportLen = Mathf.Max(1f, seconds);
        if (_toPlace > 0) _toPlace++;
        if (_export != null) Destroy(_export.gameObject);
        // 2026-11-20：編集画面全体のときは右上のパネルの上（今の式だと遊ぶ所のまん中に出るため）
        Vector2 pos = PremiereEditScreen.Active ? PremiereEditScreen.ExportCenter(_arena) : new Vector2(_arena.center.x, _rulerBottom - 1.4f);
        _export = PremiereArt.Child(_rule != null ? _rule : transform, "Export", Vector2.zero);
        _export.position = pos;
        Transform win = MacUi.PlaceWindow(_export, Vector2.zero, new Vector2(96f, 40f), MacLook.Dark, null, OrderExport, "Export");
        _exportBar = MacUi.PlaceProgress(win, new Vector2(0f, -0.25f), 72, 4, MacLook.Dark, OrderExport + 2, PremiereTimelineArt.Blue);
        GenericFrameTweens.Of(_export.gameObject).Pop(_export, Vector3.one, 0.12f);
    }

    /// <summary>書き出しを終える。done：満タンで終わった（緑のチェック → 閉じる → 隅の通知）</summary>
    public void EndExport(bool done)
    {
        if (!_exporting && _export == null) return;
        _exporting = false;
        if (_export == null) return;
        Transform win = _export;
        _export = null;
        if (!done || !_ruleOn)
        {
            Destroy(win.gameObject);
            return;
        }
        if (_exportBar != null) _exportBar.Value = 1f;
        GameAudio.Event("gm_premiere_export_done");
        K.Spr(win, K.Check7, new Vector2(0.95f, -0.25f), PremiereTimelineArt.RenderGreen, OrderExport + 4, 1.5f, "Check");
        GenericFrameTweens.Of(win.gameObject).Scale(win, Vector3.one, new Vector3(1f, 0f, 1f), 0.15f, 0.35f).Kill(0.52f);
        CornerToast.Show("Export complete", _pacer != null ? _pacer.Icon : null, PremiereTimelineArt.RenderGreen);
    }

    void UpdateExport()
    {
        if (!_exporting || _exportBar == null) return;
        _exportBar.Value = Mathf.Clamp01((Time.time - _exportStart) / _exportLen);
    }

    // ───────── 見た目を作る ─────────

    void BuildPanel()
    {
        // 2026-11-19：Premiere Pro の画面の背景の絵（AppBackgrounds）が出ているときは、幕を出さない（絵が暗くかくれるため）
        if (AppBackgrounds.Current != null) return;
        // 壁紙の上に、パネルの暗い幕（6割）。壁紙はうっすら見える
        Rect r = new Rect(_view.xMin - 2f, _view.yMin - 2f, _view.width + 4f, _view.height + 4f);
        PremiereArt.Box(_look, r.center, r.size, PremiereTimelineArt.WithAlpha(PremiereTimelineArt.Panel, 0.6f), OrderPanel, "Panel");
    }

    void BuildRuler()
    {
        float w = _arena.width, cy = (_rulerTop + _rulerBottom) * 0.5f;
        Transform ruler = PremiereArt.Child(_look, "Ruler", Vector2.zero);
        PremiereArt.Box(ruler, new Vector2(_arena.center.x, cy), new Vector2(w, RulerHeight), PremiereTimelineArt.Dark, OrderRuler, "Bar");
        int count = Mathf.FloorToInt(w + 0.001f);
        for (int i = 0; i <= count; i++)
        {
            bool major = i % 5 == 0;
            float h = (major ? 5f : 2f) * PremiereArt.Pixel;
            float x = Mathf.Min(_arena.xMin + i, _arena.xMax - PremiereArt.Pixel);
            PremiereArt.Box(ruler, new Vector2(x, _rulerBottom + 2f * PremiereArt.Pixel + h * 0.5f), new Vector2(PremiereArt.Pixel, h), PremiereTimelineArt.Text, OrderTick, "Tick");
            if (major && i > 0 && i < count)
            {
                // 小さい数字（秒のつもり）
                string label = (i / 5 * 2).ToString();
                for (int c = 0; c < label.Length; c++)
                {
                    SpriteRenderer d = RuntimeSprites.Show(ruler, GenericFrameBits.DigitSprite(label[c].ToString()), new Vector2(x + (2.5f + c * 4f) * PremiereArt.Pixel, _rulerTop - 3.5f * PremiereArt.Pixel), OrderTick, 1f, "Num");
                    d.color = PremiereTimelineArt.Text;
                }
            }
        }
        // レンダリングのバー（ルーラーの下のふち、太さ 2ドット）：倒した割合だけ左から緑、まだの所は赤
        _renderRed = PremiereArt.Box(ruler, Vector2.zero, new Vector2(w, 2f * PremiereArt.Pixel), PremiereTimelineArt.RenderRed, OrderTick, "RenderRed");
        _renderGreen = PremiereArt.Box(ruler, Vector2.zero, new Vector2(w, 2f * PremiereArt.Pixel), PremiereTimelineArt.RenderGreen, OrderTick + 1, "RenderGreen");
        UpdateRenderBar();
        BuildTimecode(ruler, cy);
    }

    void UpdateRenderBar()
    {
        if (_renderGreen == null || _renderRed == null) return;
        float p = _pacer == null || !_ruleOn ? 1f : Mathf.Clamp01(_pacer.Progress);
        float w = _arena.width, y = _rulerBottom + PremiereArt.Pixel;
        float gw = Mathf.Max(0.001f, w * p);
        _renderGreen.enabled = p > 0.001f;
        K.SetSize(_renderGreen, new Vector2(gw, 2f * PremiereArt.Pixel));
        _renderGreen.transform.localPosition = new Vector3(_arena.xMin + gw * 0.5f, y, 0f);
        K.SetSize(_renderRed, new Vector2(w, 2f * PremiereArt.Pixel));
        _renderRed.transform.localPosition = new Vector3(_arena.center.x, y, 0f);
    }

    /// <summary>ルーラーの右の端の青いタイムコード（00:00:秒:コマ。雑魚の時間の秒数で進む。飾り）</summary>
    void BuildTimecode(Transform ruler, float cy)
    {
        float w = (8 * 4 + 3 * 2 + 2) * PremiereArt.Pixel;
        Transform tc = PremiereArt.Child(ruler, "Timecode", Vector2.zero);
        tc.position = new Vector3(_arena.xMax - w * 0.5f - 3f * PremiereArt.Pixel, cy + 0.5f * PremiereArt.Pixel, 0f);
        PremiereArt.Box(tc, Vector2.zero, new Vector2(w, 8f * PremiereArt.Pixel), PremiereTimelineArt.Dark, OrderTick, "Bg");
        float x = -w * 0.5f + 2.5f * PremiereArt.Pixel;
        for (int i = 0; i < 8; i++)
        {
            _digits[i] = RuntimeSprites.Show(tc, GenericFrameBits.DigitSprite("0"), new Vector2(x, 0f), OrderTick + 1, 1f, "Digit");
            _digits[i].color = PremiereTimelineArt.Blue;
            x += 4f * PremiereArt.Pixel;
            if (i % 2 == 1 && i < 7)
            {
                PremiereArt.Box(tc, new Vector2(x - 1.5f * PremiereArt.Pixel, 1f * PremiereArt.Pixel), Vector2.one * PremiereArt.Pixel, PremiereTimelineArt.Blue, OrderTick + 1, "Colon");
                PremiereArt.Box(tc, new Vector2(x - 1.5f * PremiereArt.Pixel, -1f * PremiereArt.Pixel), Vector2.one * PremiereArt.Pixel, PremiereTimelineArt.Blue, OrderTick + 1, "Colon");
                x += 2f * PremiereArt.Pixel;
            }
        }
    }

    void UpdateTimecode()
    {
        int frames = Mathf.FloorToInt(_clock * 24f);
        int ff = frames % 24, ss = (frames / 24) % 60, mm = (frames / (24 * 60)) % 60;
        int[] v = { 0, 0, mm / 10, mm % 10, ss / 10, ss % 10, ff / 10, ff % 10 };
        for (int i = 0; i < 8; i++) if (_digits[i] != null) _digits[i].sprite = GenericFrameBits.DigitSprite(v[i].ToString());
    }

    void BuildTracks()
    {
        Transform tracks = PremiereArt.Child(_look, "Tracks", Vector2.zero);
        string[] names = { "V1", "V2", "V3" };
        for (int i = 0; i < TrackHeights.Length; i++)
        {
            float y = _arena.yMin + TrackHeights[i];
            if (y > _rulerBottom - 0.2f) continue;
            PremiereArt.Box(tracks, new Vector2(_arena.center.x, y), new Vector2(_arena.width, PremiereArt.Pixel), PremiereTimelineArt.Line, OrderTrack, "Track");
            TrackLabel(tracks, names[i], y + 0.3f);
        }
        TrackLabel(tracks, "A1", _arena.yMin + 0.3f);
    }

    static readonly string[] EyeRows = { ".XXX.", "X.X.X", ".XXX." };
    static readonly string[] LockRows = { ".X.", "X.X", "XXX", "XXX" };

    void TrackLabel(Transform parent, string text, float y)
    {
        // 左の壁ぎわの小さい札（うすく、奥に）。名前と、目・鍵の小さい印
        Transform t = PremiereArt.Child(parent, "Label_" + text, Vector2.zero);
        t.position = new Vector3(_arena.xMin + 0.55f, y, 0f);
        Color ink = PremiereTimelineArt.WithAlpha(PremiereTimelineArt.Text, 0.55f);
        PremiereArt.Box(t, Vector2.zero, new Vector2(0.95f, 0.34f), PremiereTimelineArt.WithAlpha(PremiereTimelineArt.Dark, 0.7f), OrderLabel, "Plate");
        K.Text(t, text, new Vector2(-0.18f, 0f), 0.24f, ink, OrderLabel + 1);
        K.Spr(t, K.Glyph("PrTl_Eye", EyeRows), new Vector2(0.17f, 0f), ink, OrderLabel + 1, 1f, "Eye");
        K.Spr(t, K.Glyph("PrTl_Lock", LockRows), new Vector2(0.36f, 0f), ink, OrderLabel + 1, 1f, "Lock");
    }

    /// <summary>
    /// 足場（フォルダ）の絵を、V1 のクリップにする。足場の子に描く（レーザーツールが足場の絵を消すと、一緒に消えて戻る）。
    /// フォルダの絵は消す（enabled = false。レーザーツールは enabled の絵だけを消して戻すので、フォルダは出てこない）
    /// </summary>
    void DressPlatforms()
    {
        // 2026-11-19：アプリの UI の足場（AppStagePlatforms）を出しているステージは、足場の絵がもうクリップなので、何もしない
        if (AppStagePlatforms.Active) return;
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        if (oneWay < 0) return;
        var hits = new List<Collider2D>();
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(1 << oneWay);
        Physics2D.OverlapArea(new Vector2(_arena.xMin, _arena.yMin + 0.1f), new Vector2(_arena.xMax, _arena.yMax), filter, hits);
        int index = 0;
        var seen = new HashSet<GameObject>();
        foreach (Collider2D col in hits)
        {
            if (col == null || !col.enabled || !seen.Add(col.gameObject)) continue;
            Bounds b = col.bounds;
            if (b.size.x >= _arena.width * 0.5f) continue;
            if (col.GetComponentInParent<FallingObject>() != null || col.GetComponentInParent<BlinkingPlatform>() != null || col.GetComponentInParent<CustomEffect>() != null) continue;
            foreach (var sr in col.GetComponentsInChildren<SpriteRenderer>()) sr.enabled = false;
            Transform holder = new GameObject("PremiereClip").transform;
            holder.gameObject.layer = col.gameObject.layer;
            holder.SetParent(col.transform, false);
            Vector3 ls = col.transform.lossyScale;
            holder.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(ls.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(ls.y)), 1f);
            holder.position = new Vector3(b.center.x, b.max.y - 0.25f + PremiereArt.Pixel, 0f);
            BuildClip(holder, Mathf.Max(0.6f, b.size.x), index++ % 2 == 0 ? PremiereTimelineArt.ClipPurple : PremiereTimelineArt.ClipBlue);
        }
    }

    static void BuildClip(Transform parent, float w, Color fill)
    {
        const float h = 0.5f;
        float px = PremiereArt.Pixel;
        Color dark = Color.Lerp(fill, Color.black, 0.35f);
        int wp = Mathf.Max(6, Mathf.RoundToInt(w * 18f)), hp = 9;
        K.Spr(parent, K.RoundRect(wp, hp, 2), Vector2.zero, dark, OrderClip, 1f, "Edge");
        K.Spr(parent, K.RoundRect(wp - 2, hp - 2, 1), Vector2.zero, fill, OrderClip + 1, 1f, "Fill");
        // 左にサムネイル（暗い四角）、上に白い細い棒（クリップの名前）
        PremiereArt.Box(parent, new Vector2(-w * 0.5f + 4f * px, -0.5f * px), new Vector2(5f * px, h - 4f * px), PremiereTimelineArt.Dark, OrderClip + 2, "Thumb");
        PremiereArt.Box(parent, new Vector2(-w * 0.5f + 8f * px + Mathf.Min(0.5f, w * 0.25f), h * 0.5f - 2.5f * px), new Vector2(Mathf.Min(1f, w * 0.5f), px), Color.white, OrderClip + 2, "Name");
    }

    /// <summary>床＝音声トラック：床の上のふちに緑の帯（高さ 4ドット）と波形</summary>
    void BuildAudio()
    {
        int w = Mathf.Max(8, Mathf.RoundToInt(_arena.width * 18f));
        const int h = 4;
        var rows = new string[h];
        var sb = new System.Text.StringBuilder(w);
        var rng = new System.Random(1208);
        var amp = new int[w];
        for (int x = 0; x < w; x++) amp[x] = x % 2 == 0 ? 1 + rng.Next(3) : 0;
        for (int y = 0; y < h; y++)
        {
            sb.Length = 0;
            for (int x = 0; x < w; x++) sb.Append(amp[x] > 0 && (h - 1 - y) < amp[x] + 0 && y >= h - amp[x] ? 'd' : 'g');
            rows[y] = sb.ToString();
        }
        var pal = new Dictionary<char, Color32> { { 'g', PremiereTimelineArt.Audio }, { 'd', RuntimeSprites.Hex("2A7F61") } };
        Sprite s = RuntimeSprites.FromAscii($"PrTl_Audio_{w}", rows, pal);
        SpriteRenderer sr = RuntimeSprites.Show(_look, s, new Vector2(_arena.center.x, _arena.yMin - 2f * PremiereArt.Pixel), OrderAudio, 1f, "Audio");
        sr.color = Color.white;
    }

    void BuildHead()
    {
        float bottom = _arena.yMin, top = _rulerBottom;
        float h = Mathf.Max(0.1f, top - bottom);
        _head = PremiereArt.Child(_rule, "Playhead", Vector2.zero);
        _head.position = new Vector3(_arena.xMin, (top + bottom) * 0.5f, 0f);
        PremiereArt.Box(_head, Vector2.zero, new Vector2(2f * PremiereArt.Pixel, h), PremiereTimelineArt.WithAlpha(PremiereTimelineArt.Blue, 0.85f), OrderHeadLine, "Line");
        RuntimeSprites.Show(_head, PremiereArt.PlayheadKnob, new Vector2(0f, h * 0.5f), OrderHeadKnob, 1f, "Knob");
    }
}
