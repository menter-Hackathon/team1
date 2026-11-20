using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ゲームの音（2026-11-19）。SE と BGM を鳴らす。シーンに置かなくてよい（ゲームが始まるときに自分で作られ、シーンをまたいで残る）。
/// ・音のファイル：Resources/Audio/SE/&lt;id&gt;・Resources/Audio/BGM/&lt;id&gt;（イントロがある曲は &lt;id&gt;_intro も）
/// ・割り当て：Resources/Audio/Maps/*.json を全部読んでまとめる（ファイル名の順。同じキーは後のファイル）。形は Tools/audio/FORMAT.md 3章
/// ・音や割り当てが無くても動く（無い id は1回だけ警告して、鳴らさない。例外は外に出さない）
/// ・時間はすべて実時間（Time.timeScale に左右されない。ヒットストップ・ゲームオーバーで時間を止めても、音は止まらない）
/// SE：GameAudio.Se(id)・GameAudio.Event(出来事のキー)。技の音は AttackRunner.AnyStarted・AnyReleased から自動で鳴る
/// BGM：PlayBgm・PlayJingle・StopBgm・SetBgmPitch。音量は MasterVolume・BgmVolume・SeVolume（PlayerPrefs に残る）
/// 確かめ用：LogPlays を true にする（または起動の引数に -audiolog）と、鳴らそうとした音を全部ログに出す
/// </summary>
public partial class GameAudio : MonoBehaviour
{
    // ───────── 決まった数【仮】 ─────────
    const int SeVoices = 24;           // 同時に鳴らせる SE の数
    const int MaxSameVoices = 3;       // 同じ SE は同時に3つまで（4つ目は一番古いのを止めて鳴らす）
    const float SameGap = 0.06f;       // 同じ SE は 0.06秒あける
    const float PitchJitter = 0.03f;   // SE のピッチを ±3% ばらす
    const double ScheduleAhead = 0.06; // 曲を少し先の時刻に予約して、切れ目なく始める（秒）
    const double ThenLookahead = 0.25; // ジングルのあとの曲を、この秒数だけ前に予約する
    const string SeFolder = "Audio/SE/";
    const string BgmFolder = "Audio/BGM/";
    const string MapFolder = "Audio/Maps";

    /// <summary>曲ごとの大きさ（BGM の音量にかける。作った曲の大きさをそろえる。書いていない曲は 1）</summary>
    static readonly Dictionary<string, float> TrackVolumes = new Dictionary<string, float>
    {
        { "bgm_desktop", 0.6f },
        { "bgm_dex", 0.85f },
    };

    /// <summary>出来事ごとの、次に鳴らすまでの最低の間（秒）。何度も出る出来事を鳴らしすぎない</summary>
    static readonly Dictionary<string, float> EventGaps = new Dictionary<string, float>
    {
        { "telegraph", 0.15f },
        { "toast", 0.2f },
        { "popup_open", 0.08f },
        { "block_vanish", 0.08f },
        { "pace_phase", 0.5f },
        { "wave_clear", 1f },
        { "skill_denied", 0.15f },
        { "skill_ready", 0.1f },
        { "enemy_hit", 0.04f },
    };

    // ───────── 確かめ用のログ ─────────

    /// <summary>true の間、鳴らそうとした音をログに出す（"[Audio] SE &lt;id&gt; &lt;どこから&gt;"・"[Audio] BGM &lt;id&gt;"・"[Audio] JINGLE &lt;id&gt;"・"[Audio] NOMAP &lt;どこから&gt;"）</summary>
    public static bool LogPlays;

    // ───────── 音量（PlayerPrefs） ─────────

    const string KeyMaster = "audio.master", KeyBgm = "audio.bgm", KeySe = "audio.se";
    static float _master = -1f, _bgmVolume = -1f, _seVolume = -1f;

    /// <summary>全体の音量（0〜1。最初は 1）</summary>
    public static float MasterVolume { get => ReadVolume(ref _master, KeyMaster, 1f); set => WriteVolume(ref _master, KeyMaster, value); }
    /// <summary>BGM・ジングルの音量（0〜1。最初は 0.42。2026-11-19：0.55 だと当たりの音が曲に埋もれたので下げた）</summary>
    public static float BgmVolume { get => ReadVolume(ref _bgmVolume, KeyBgm, 0.42f); set => WriteVolume(ref _bgmVolume, KeyBgm, value); }
    /// <summary>SE の音量（0〜1。最初は 0.8）</summary>
    public static float SeVolume { get => ReadVolume(ref _seVolume, KeySe, 0.8f); set => WriteVolume(ref _seVolume, KeySe, value); }

    public static void SetMasterVolume(float v) => MasterVolume = v;
    public static void SetBgmVolume(float v) => BgmVolume = v;
    public static void SetSeVolume(float v) => SeVolume = v;

    static float ReadVolume(ref float cache, string key, float def)
    {
        if (cache < 0f) cache = Mathf.Clamp01(PlayerPrefs.GetFloat(key, def));
        return cache;
    }

    static void WriteVolume(ref float cache, string key, float value)
    {
        cache = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(key, cache);
        PlayerPrefs.Save();
    }

    // ───────── 自分で作る ─────────

    static GameAudio _i;
    static bool _quitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _i = null;
        _quitting = false;
        LogPlays = false;
        _master = _bgmVolume = _seVolume = -1f;
        _warned.Clear();
        GamePaused = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        try
        {
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg == "-audiolog") LogPlays = true;
            }
        }
        catch (Exception) { /* 引数が読めなくても動く */ }
        Application.quitting -= OnQuitting;
        Application.quitting += OnQuitting;
        HookEvents();
        Ensure();
    }

    static void OnQuitting() => _quitting = true;

    /// <summary>無ければ作る（遊んでいないとき・終わるときは null）</summary>
    static GameAudio Ensure()
    {
        if (_i != null) return _i;
        if (_quitting || !Application.isPlaying) return null;
        var go = new GameObject("GameAudio");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        _i = go.AddComponent<GameAudio>();
        return _i;
    }

    // ───────── 中身 ─────────

    class Voice
    {
        public AudioSource Src;
        public string Id;
        public float StartedAt;
    }

    /// <summary>曲を流す台（イントロ用とループ用の2つの AudioSource）。入れ替えのフェードのため2台使う</summary>
    class Deck
    {
        public AudioSource Intro, Loop;
        public string Id;          // 流している曲（無ければ null）
        public float Vol;          // フェードの大きさ（0〜1）
        public float Target;       // フェードの行き先
        public float Speed;        // 1秒あたりの変化
        public bool Stopping;      // 0 になったら止める
        public bool Pending;       // 音のデータの読み込みを待っている
        public bool Started;       // 予約した（鳴っている）
        public double WantDsp;     // この時刻（以降）に始めたい
        public double StartDsp;    // 鳴り始めた（鳴り始める）時刻
        public double IntroLen;    // イントロの長さ（秒。無ければ 0）
    }

    readonly List<Voice> _voices = new List<Voice>();
    readonly Deck[] _decks = new Deck[2];
    int _cur; // 今の曲の台
    AudioSource _jingle;

    readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    static readonly HashSet<string> _warned = new HashSet<string>();
    readonly Dictionary<string, float> _lastSe = new Dictionary<string, float>();
    readonly Dictionary<string, float> _lastEvent = new Dictionary<string, float>();

    // 割り当て
    readonly Dictionary<string, MoveRow> _moves = new Dictionary<string, MoveRow>();
    readonly Dictionary<string, EventRow> _events = new Dictionary<string, EventRow>();
    readonly Dictionary<string, FallbackRow> _fallbacks = new Dictionary<string, FallbackRow>();

    // UI の音（同じフレームに重なったら、強い方を1つだけ鳴らす）
    string _uiPending;
    int _uiPriority;
    float _uiPitch = 1f;

    // ダック（ジングルの間、今の曲を小さくする）
    float _duck = 1f, _duckTarget = 1f, _duckSpeed = 10f;
    // ピッチ（ボスの第2形態）
    float _pitch = 1f, _pitchTarget = 1f, _pitchSpeed = 1f;

    // ジングル
    bool _jingleOn;         // ジングルの間（あとの曲を待っている）
    bool _jinglePending;    // 音のデータの読み込み待ち
    bool _jingleHold;       // ジングルの間に来た PlayBgm を、あとの曲にする（false なら、ジングルを止めてすぐ流す）
    string _jingleThen;     // あと：null＝前の曲に戻す、""＝静かなまま、ほか＝その曲
    float _jingleDelay;     // ジングルが始まってから、あとへ移るまでの秒数（負ならジングルの長さ）
    double _jingleThenDsp;
    float _jingleVol = 1f, _jingleVolTarget = 1f, _jingleVolSpeed = 1f;
    string _jingleId;

    double _dspSeen, _dspSeenReal;

    void Awake()
    {
        if (_i != null && _i != this)
        {
            Destroy(gameObject);
            return;
        }
        _i = this;
        for (int i = 0; i < SeVoices; i++) _voices.Add(new Voice { Src = MakeSource(128) });
        for (int i = 0; i < 2; i++) _decks[i] = new Deck { Intro = MakeSource(0), Loop = MakeSource(0) };
        _jingle = MakeSource(0);
        LoadMaps();
    }

    void OnDestroy()
    {
        if (_i == this) _i = null;
    }

    AudioSource MakeSource(int priority)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.dopplerLevel = 0f;
        s.priority = priority;
        s.loop = false;
        return s;
    }

    static void LogOnce(string key, Exception e)
    {
        if (_warned.Add("ex:" + key)) Debug.LogWarning($"[Audio] {key} で例外（音を鳴らさずに続けます）：{e}");
    }

    // ───────── 割り当て（Maps/*.json） ─────────

    [Serializable]
    class MoveRow { public string move; public string start; public string release; public float vol; public float pitch; }
    [Serializable]
    class EventRow { public string key; public string se; public float vol; public float pitch; }
    [Serializable]
    class FallbackRow { public string team; public string kind; public string start; public string release; public float vol; public float pitch; }
    [Serializable]
    class MapFile { public MoveRow[] moves; public EventRow[] events; public FallbackRow[] fallbacks; }

    /// <summary>割り当てを読み直す（Maps/*.json を書き換えたとき）</summary>
    public static void ReloadMaps()
    {
        var a = Ensure();
        if (a != null) a.LoadMaps();
    }

    void LoadMaps()
    {
        _moves.Clear();
        _events.Clear();
        _fallbacks.Clear();
        TextAsset[] files;
        try { files = Resources.LoadAll<TextAsset>(MapFolder); }
        catch (Exception e) { LogOnce("LoadMaps", e); return; }
        if (files == null || files.Length == 0)
        {
            if (_warned.Add("maps")) Debug.LogWarning($"[Audio] 割り当て（Resources/{MapFolder}/*.json）がありません。技・出来事の音は鳴りません");
            return;
        }
        Array.Sort(files, (x, y) => string.CompareOrdinal(x.name, y.name));
        foreach (TextAsset f in files)
        {
            MapFile m = null;
            try { m = JsonUtility.FromJson<MapFile>(f.text); }
            catch (Exception e) { Debug.LogWarning($"[Audio] 割り当て {f.name} を読めません：{e.Message}"); }
            if (m == null) continue;
            if (m.moves != null)
            {
                foreach (MoveRow r in m.moves)
                {
                    if (r != null && !string.IsNullOrEmpty(r.move)) _moves[r.move.Trim()] = r;
                }
            }
            if (m.events != null)
            {
                foreach (EventRow r in m.events)
                {
                    if (r != null && !string.IsNullOrEmpty(r.key)) _events[r.key.Trim()] = r;
                }
            }
            if (m.fallbacks != null)
            {
                foreach (FallbackRow r in m.fallbacks)
                {
                    if (r != null && !string.IsNullOrEmpty(r.team) && !string.IsNullOrEmpty(r.kind)) _fallbacks[FallbackKey(r.team, r.kind)] = r;
                }
            }
        }
    }

    static string FallbackKey(string team, string kind) => (team.Trim() + "|" + kind.Trim()).ToLowerInvariant();

    /// <summary>技の名前（複製された物の「(Clone)」は外す）</summary>
    static string MoveKey(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        int k = name.IndexOf("(Clone)", StringComparison.Ordinal);
        return (k >= 0 ? name.Substring(0, k) : name).Trim();
    }

    // 割り当ての大きさ・ピッチ（書いていない＝0 は 1。範囲は FORMAT.md のとおり）
    static float RowVol(float v) => v <= 0f ? 1f : Mathf.Min(v, 1.5f);
    static float RowPitch(float p) => p <= 0f ? 1f : Mathf.Clamp(p, 0.5f, 2f);

    // ───────── 音のファイル ─────────

    /// <summary>音を読む（読んだ物は覚える。無い物も覚えて、何度も探さない）。warn：無ければ1回だけ警告</summary>
    AudioClip Clip(string folder, string id, bool warn = true)
    {
        if (string.IsNullOrEmpty(id)) return null;
        string path = folder + id;
        // 覚えていない・覚えていた物が消えていた（アンロードされた）ときだけ読む
        if (!_clips.TryGetValue(path, out AudioClip c) || (!ReferenceEquals(c, null) && c == null))
        {
            c = null;
            try { c = Resources.Load<AudioClip>(path); }
            catch (Exception e) { LogOnce("Load " + path, e); }
            _clips[path] = c;
        }
        if (c == null && warn && _warned.Add(path)) Debug.LogWarning($"[Audio] MISSING {id}（Resources/{path} がありません。鳴らさずに続けます）");
        return c;
    }

    /// <summary>音のデータが使える（null・読み込み失敗も「待たない」）</summary>
    static bool Ready(AudioClip clip)
    {
        if (clip == null) return true;
        AudioDataLoadState s = clip.loadState;
        if (s == AudioDataLoadState.Loaded || s == AudioDataLoadState.Failed) return true;
        if (s == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        return false;
    }

    static void Preload(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
    }

    static double Length(AudioClip clip) => clip != null && clip.frequency > 0 ? clip.samples / (double)clip.frequency : 0.0;

    // ───────── SE ─────────

    /// <summary>SE を鳴らす。vol・pitch は倍率。jitter：ピッチを少しばらす（false で決まった高さ）</summary>
    public static void Se(string id, float vol = 1f, float pitch = 1f, bool jitter = true) => PlaySe(id, vol, pitch, jitter, "code");

    static void PlaySe(string id, float vol, float pitch, bool jitter, string source)
    {
        if (string.IsNullOrEmpty(id)) return;
        try
        {
            if (LogPlays) Debug.Log($"[Audio] SE {id} {source}");
            GameAudio a = Ensure();
            if (a != null) a.PlaySeNow(id, vol, pitch, jitter);
        }
        catch (Exception e) { LogOnce("Se " + id, e); }
    }

    void PlaySeNow(string id, float vol, float pitch, bool jitter)
    {
        float now = Time.unscaledTime;
        if (_lastSe.TryGetValue(id, out float last) && now - last < SameGap) return;
        AudioClip clip = Clip(SeFolder, id);
        if (clip == null) return;
        _lastSe[id] = now;
        Voice v = PickVoice(id);
        v.Src.Stop();
        v.Src.clip = clip;
        v.Src.volume = Mathf.Clamp01(vol * MasterVolume * SeVolume);
        float p = pitch * (jitter ? UnityEngine.Random.Range(1f - PitchJitter, 1f + PitchJitter) : 1f);
        v.Src.pitch = Mathf.Clamp(p, 0.1f, 3f);
        v.Id = id;
        v.StartedAt = now;
        v.Src.Play();
    }

    /// <summary>鳴らす AudioSource を選ぶ：同じ音が3つ鳴っていれば一番古いそれ、空きがあれば空き、無ければ一番古い物</summary>
    Voice PickVoice(string id)
    {
        int same = 0;
        Voice oldestSame = null, free = null, oldest = null;
        foreach (Voice v in _voices)
        {
            if (!v.Src.isPlaying)
            {
                if (free == null) free = v;
                continue;
            }
            if (v.Id == id)
            {
                same++;
                if (oldestSame == null || v.StartedAt < oldestSame.StartedAt) oldestSame = v;
            }
            if (oldest == null || v.StartedAt < oldest.StartedAt) oldest = v;
        }
        if (same >= MaxSameVoices && oldestSame != null) return oldestSame;
        return free ?? oldest ?? _voices[0];
    }

    // ───────── 出来事の SE ─────────

    /// <summary>出来事のキー（FORMAT.md 3章の表）に割り当てた SE を鳴らす。pitchMul：ピッチの倍率（ゲームオーバーのゲージで上げていく など）</summary>
    public static void Event(string key, float pitchMul = 1f)
    {
        if (string.IsNullOrEmpty(key)) return;
        try
        {
            GameAudio a = Ensure();
            if (a == null) return;
            if (key.StartsWith("ui_", StringComparison.Ordinal)) a.QueueUi(key, pitchMul);
            else a.PlayEventNow(key, pitchMul);
        }
        catch (Exception e) { LogOnce("Event " + key, e); }
    }

    /// <summary>
    /// 出来事のキーに割り当てた SE の音と、大きさ・ピッチを取り出す（鳴らさない。SeLoop が鳴らし続けるときに使う）。
    /// 割り当てが無い・音が空・ファイルが無いときは false。決め方は Event（PlayEventNow）と同じ
    /// </summary>
    public static bool TryGetEventClip(string key, out AudioClip clip, out float vol, out float pitch)
    {
        clip = null;
        vol = 1f;
        pitch = 1f;
        if (string.IsNullOrEmpty(key)) return false;
        try
        {
            GameAudio a = Ensure();
            if (a == null || !a._events.TryGetValue(key.Trim(), out EventRow row) || string.IsNullOrEmpty(row.se)) return false;
            clip = a.Clip(SeFolder, row.se.Trim());
            if (clip == null) return false;
            vol = RowVol(row.vol);
            pitch = RowPitch(row.pitch);
            return true;
        }
        catch (Exception e)
        {
            LogOnce("TryGetEventClip " + key, e);
            clip = null;
            return false;
        }
    }

    /// <summary>赤い予告（線・枠）が出た。たくさん出るので、0.15秒に1回まで</summary>
    public static void Telegraph() => Event("telegraph");

    // ───────── 一時停止（2026-11-20：PauseMenu） ─────────

    /// <summary>ゲームが一時停止しているか（PauseMenu が切り替える）</summary>
    public static bool GamePaused { get; private set; }
    readonly List<AudioSource> _pausedVoices = new List<AudioSource>();

    /// <summary>
    /// 一時停止：今鳴っている SE（技の音など）を止める／続きから鳴らす。
    /// BGM と、止めている間に新しく鳴らす音（メニューのカーソル・決定の音）は止めない
    /// </summary>
    public static void SetGamePaused(bool paused)
    {
        if (GamePaused == paused) return;
        GamePaused = paused;
        try
        {
            GameAudio a = Ensure();
            if (a == null) return;
            if (paused)
            {
                a._pausedVoices.Clear();
                foreach (Voice v in a._voices)
                {
                    if (v.Src == null || !v.Src.isPlaying) continue;
                    v.Src.Pause();
                    a._pausedVoices.Add(v.Src);
                }
            }
            else
            {
                foreach (AudioSource s in a._pausedVoices) if (s != null) s.UnPause();
                a._pausedVoices.Clear();
            }
        }
        catch (Exception e) { LogOnce("SetGamePaused", e); }
    }

    void PlayEventNow(string key, float pitchMul)
    {
        float now = Time.unscaledTime;
        if (EventGaps.TryGetValue(key, out float gap) && _lastEvent.TryGetValue(key, out float last) && now - last < gap) return;
        _lastEvent[key] = now;
        if (!_events.TryGetValue(key, out EventRow row))
        {
            if (LogPlays) Debug.Log($"[Audio] NOMAP event:{key}");
            return;
        }
        if (string.IsNullOrEmpty(row.se))
        {
            if (LogPlays) Debug.Log($"[Audio] SILENT event:{key}");
            return;
        }
        PlaySe(row.se, RowVol(row.vol), RowPitch(row.pitch) * pitchMul, true, "event:" + key);
    }

    // UI の音：同じフレームに「決定」と「はじめる」などが重なったら、強い方だけ（次の Update で鳴らす）
    static int UiPriority(string key)
    {
        switch (key)
        {
            case "ui_start": return 5;
            case "ui_cancel": return 4;
            case "ui_decide": return 3;
            case "ui_card": return 2;
            default: return 1; // ui_move など
        }
    }

    void QueueUi(string key, float pitchMul)
    {
        int p = UiPriority(key);
        if (_uiPending != null && p <= _uiPriority) return;
        _uiPending = key;
        _uiPriority = p;
        _uiPitch = pitchMul;
    }

    // ───────── 技の SE（AttackRunner.AnyStarted・AnyReleased） ─────────

    static void OnMove(AttackRunner runner, AttackData data, bool start, bool mappedOnly = false)
    {
        if (data == null) return;
        try
        {
            GameAudio a = Ensure();
            if (a == null) return;
            string name = MoveKey(data.name);
            string phase = start ? "start" : "release";
            string id;
            float vol, pitch;
            string source = $"move:{name}:{phase}";
            if (a._moves.TryGetValue(name, out MoveRow m))
            {
                id = start ? m.start : m.release;
                vol = RowVol(m.vol);
                pitch = RowPitch(m.pitch);
            }
            else if (mappedOnly) return;
            else
            {
                string team = runner != null ? runner.Team.ToString() : "Enemy";
                if (!a._fallbacks.TryGetValue(FallbackKey(team, data.Kind.ToString()), out FallbackRow f))
                {
                    if (LogPlays) Debug.Log($"[Audio] NOMAP {source}");
                    return;
                }
                id = start ? f.start : f.release;
                vol = RowVol(f.vol);
                pitch = RowPitch(f.pitch);
                source += " (fallback)";
            }
            if (string.IsNullOrEmpty(id))
            {
                // 空文字＝わざと鳴らさない（割り当てでそう決めた。fallbacks へは行かない）
                if (LogPlays) Debug.Log($"[Audio] SILENT {source}");
                return;
            }
            PlaySe(id, vol, pitch, true, source);
        }
        catch (Exception e) { LogOnce("OnMove", e); }
    }

    // ───────── BGM ─────────

    /// <summary>今の曲の id（止めている途中・無ければ null）</summary>
    public static string CurrentBgm
    {
        get
        {
            if (_i == null) return null;
            Deck d = _i._decks[_i._cur];
            return d == null || d.Stopping ? null : d.Id;
        }
    }

    static bool CurrentBgmIs(string id) => CurrentBgm == id;

    /// <summary>
    /// 曲を流す（前の曲から fade 秒で入れ替える）。同じ曲が流れていれば何もしない（restart なら最初から）。
    /// イントロ（&lt;id&gt;_intro）があれば、それを1回流してから、ちょうどの時刻にループの本体へつなぐ。
    /// ジングルの間に呼ぶと、ジングルのあとに流す（ジングルが待たない設定なら、ジングルを止めてすぐ流す。restart でも止める）
    /// </summary>
    public static void PlayBgm(string id, float fade = 0.8f, bool restart = false)
    {
        if (string.IsNullOrEmpty(id)) return;
        try
        {
            GameAudio a = Ensure();
            if (a != null) a.PlayBgmNow(id, Mathf.Max(0f, fade), restart);
        }
        catch (Exception e) { LogOnce("PlayBgm " + id, e); }
    }

    void PlayBgmNow(string id, float fade, bool restart)
    {
        if (_jingleOn)
        {
            if (_jingleHold && !restart)
            {
                Deck ducked = _decks[_cur];
                if (_jingleThen == null && ducked.Id == id && !ducked.Stopping) return; // 待っている曲と同じ：そのまま戻す
                _jingleThen = id;
                Preload(Clip(BgmFolder, id, false));
                Preload(Clip(BgmFolder, id + "_intro", false));
                if (LogPlays) Debug.Log($"[Audio] BGM {id} (after jingle {_jingleId})");
                return;
            }
            CancelJingle(0.3f);
        }
        Deck cur = _decks[_cur];
        if (!restart && cur.Id == id && !cur.Stopping)
        {
            FadeDeck(cur, 1f, fade, false);
            SetDuck(1f, fade);
            return;
        }
        if (LogPlays) Debug.Log($"[Audio] BGM {id}");
        StartTrack(id, AudioSettings.dspTime + ScheduleAhead, fade, Mathf.Max(fade, 0.3f));
    }

    /// <summary>曲を止める（fade 秒で消す）。ジングルも止める</summary>
    public static void StopBgm(float fade = 1f)
    {
        try
        {
            GameAudio a = _i;
            if (a == null) return;
            if (a._jingleOn || a._jingle.isPlaying) a.CancelJingle(Mathf.Max(0.05f, fade));
            Deck cur = a._decks[a._cur];
            if (cur.Id != null) FadeDeck(cur, 0f, fade, true);
            a._pitchTarget = 1f;
        }
        catch (Exception e) { LogOnce("StopBgm", e); }
    }

    /// <summary>今の曲のピッチ（速さ・高さ）を seconds 秒で p にする（ボスの第2形態）。新しい曲は 1 に戻る</summary>
    public static void SetBgmPitch(float p, float seconds = 0.5f)
    {
        GameAudio a = Ensure();
        if (a == null) return;
        a._pitchTarget = Mathf.Clamp(p, 0.5f, 2f);
        if (seconds <= 0f) a._pitch = a._pitchTarget;
        else a._pitchSpeed = Mathf.Max(0.0001f, Mathf.Abs(a._pitchTarget - a._pitch) / seconds);
    }

    /// <summary>
    /// ジングル（1回だけ流れる短い曲）。今の曲は、then が null なら小さくして待ち、ジングルのあとに戻す。
    /// then が "" なら今の曲を消して、あとは静かなまま。ほかの id ならジングルのあとにその曲（thenDelay 秒後。負ならジングルの長さ）。
    /// holdBgm：ジングルの間に来た PlayBgm を、あとの曲にする（false なら、PlayBgm が来たらジングルを止めてすぐ流す）
    /// </summary>
    public static void PlayJingle(string id, string then = null, float thenDelay = -1f, bool holdBgm = true)
    {
        if (string.IsNullOrEmpty(id)) return;
        try
        {
            GameAudio a = Ensure();
            if (a != null) a.PlayJingleNow(id, then, thenDelay, holdBgm);
        }
        catch (Exception e) { LogOnce("PlayJingle " + id, e); }
    }

    void PlayJingleNow(string id, string then, float thenDelay, bool holdBgm)
    {
        if (LogPlays) Debug.Log($"[Audio] JINGLE {id}");
        _jingle.Stop();
        _jingleId = id;
        _jingleOn = true;
        _jingleHold = holdBgm;
        _jingleThen = then;
        _jingleDelay = thenDelay;
        _jingleVol = _jingleVolTarget = 1f;

        Deck cur = _decks[_cur];
        if (then == null) SetDuck(0f, 0.12f);                       // 前の曲は小さくして待つ
        else if (cur.Id != null) FadeDeck(cur, 0f, 0.25f, true);  // 前の曲は消す
        if (!string.IsNullOrEmpty(then))
        {
            Preload(Clip(BgmFolder, then, false));
            Preload(Clip(BgmFolder, then + "_intro", false));
        }

        AudioClip clip = Clip(BgmFolder, id);
        if (clip == null)
        {
            _jinglePending = false;
            _jingleThenDsp = AudioSettings.dspTime; // ジングルが無い：すぐあとへ
            return;
        }
        _jingle.clip = clip;
        _jingle.loop = false;
        _jinglePending = true;
        TryStartJingle();
    }

    void TryStartJingle()
    {
        if (!_jinglePending || !Ready(_jingle.clip)) return;
        double at = AudioSettings.dspTime + ScheduleAhead;
        _jingle.pitch = 1f;
        _jingle.volume = Mathf.Clamp01(_jingleVol * MasterVolume * BgmVolume * TrackVolume(_jingleId)); // 次の Update を待たずに入れる
        _jingle.PlayScheduled(at);
        _jingleThenDsp = at + (_jingleDelay >= 0f ? _jingleDelay : Length(_jingle.clip));
        _jinglePending = false;
    }

    void CancelJingle(float fade)
    {
        _jingleOn = false;
        _jinglePending = false;
        _jingleVolTarget = 0f;
        _jingleVolSpeed = fade > 0f ? 1f / fade : 1000f;
        SetDuck(1f, 0.4f);
    }

    /// <summary>
    /// 曲の再生位置（秒。イントロの頭から）。その曲が今流れているときだけ true（ミュージックのビートを曲に合わせる）。
    /// ループの本体はくり返すたびに、イントロの長さ＋ループの中の位置に戻る
    /// </summary>
    public static bool TryGetBgmTime(string id, out float seconds)
    {
        seconds = 0f;
        GameAudio a = _i;
        if (a == null || string.IsNullOrEmpty(id)) return false;
        try
        {
            Deck d = a._decks[a._cur];
            if (d == null || d.Id != id || !d.Started || d.Stopping) return false;
            if (Mathf.Approximately(a._pitch, 1f))
            {
                double el = a.SmoothDsp() - d.StartDsp;
                if (el < 0.0) return false;
                double loopLen = Length(d.Loop.clip);
                if (el < d.IntroLen || loopLen <= 0.0) seconds = (float)el;
                else seconds = (float)(d.IntroLen + (el - d.IntroLen) % loopLen);
                return true;
            }
            // ピッチを変えているとき：音の再生位置そのもの
            if (d.Intro.isPlaying && d.Intro.clip != null) seconds = d.Intro.timeSamples / (float)d.Intro.clip.frequency;
            else if (d.Loop.isPlaying && d.Loop.clip != null) seconds = (float)d.IntroLen + d.Loop.timeSamples / (float)d.Loop.clip.frequency;
            else return false;
            return true;
        }
        catch (Exception e)
        {
            LogOnce("TryGetBgmTime", e);
            return false;
        }
    }

    // dspTime は音の処理の区切りでしか進まないので、その間は実時間でなめらかにつなぐ
    double SmoothDsp()
    {
        double dsp = AudioSettings.dspTime;
        double real = Time.realtimeSinceStartupAsDouble;
        if (dsp != _dspSeen)
        {
            _dspSeen = dsp;
            _dspSeenReal = real;
            return dsp;
        }
        return dsp + Math.Min(real - _dspSeenReal, 0.1);
    }

    /// <summary>新しい曲を、もう一方の台で始める（今の曲は fadeOut 秒で消す）</summary>
    void StartTrack(string id, double atDsp, float fadeIn, float fadeOut)
    {
        Deck old = _decks[_cur];
        if (old.Id != null) FadeDeck(old, 0f, fadeOut, true);
        _cur = 1 - _cur;
        Deck d = _decks[_cur];
        HardStop(d);
        d.Id = id;
        _pitch = _pitchTarget = 1f;
        _duck = _duckTarget = 1f;

        AudioClip loop = Clip(BgmFolder, id, false);
        AudioClip intro = Clip(BgmFolder, id + "_intro", false);
        if (loop == null && intro == null)
        {
            Clip(BgmFolder, id); // 1回だけ警告（曲が無いときは静かにする。前の曲は消える）
            return;
        }
        d.Intro.clip = intro;
        d.Loop.clip = loop;
        d.Vol = fadeIn > 0f ? 0f : 1f;
        FadeDeck(d, 1f, fadeIn, false);
        d.WantDsp = atDsp;
        d.Pending = true;
        TrySchedule(d);
    }

    void TrySchedule(Deck d)
    {
        if (!d.Pending) return;
        AudioClip intro = d.Intro.clip, loop = d.Loop.clip;
        bool introReady = Ready(intro), loopReady = Ready(loop); // 両方に読み込みを頼む
        if (!introReady || !loopReady) return;
        double at = Math.Max(d.WantDsp, AudioSettings.dspTime + ScheduleAhead);
        d.StartDsp = at;
        d.IntroLen = 0.0;
        // 大きさ・ピッチは次の Update を待たずに入れる（長いフレームで、最初が大きな音にならないように）
        float v = Mathf.Clamp01(d.Vol * MasterVolume * BgmVolume * TrackVolume(d.Id) * (d == _decks[_cur] ? _duck : 1f));
        d.Intro.volume = d.Loop.volume = v;
        if (d == _decks[_cur]) d.Intro.pitch = d.Loop.pitch = _pitch;
        if (intro != null)
        {
            d.IntroLen = Length(intro);
            d.Intro.loop = loop == null; // ループの本体が無ければ、イントロをくり返す
            d.Intro.PlayScheduled(at);
        }
        if (loop != null)
        {
            d.Loop.loop = true;
            d.Loop.PlayScheduled(at + d.IntroLen);
        }
        d.Pending = false;
        d.Started = true;
    }

    static void HardStop(Deck d)
    {
        d.Intro.Stop();
        d.Loop.Stop();
        d.Intro.pitch = d.Loop.pitch = 1f; // 次に使うときの予約（イントロ→ループのつなぎ目）はピッチ 1 で数える
        d.Id = null;
        d.Pending = false;
        d.Started = false;
        d.Stopping = false;
        d.Vol = d.Target = 0f;
    }

    static void FadeDeck(Deck d, float target, float seconds, bool stopAtZero)
    {
        d.Target = target;
        d.Stopping = stopAtZero;
        if (seconds <= 0f)
        {
            d.Vol = target;
            d.Speed = 1000f;
        }
        else d.Speed = Mathf.Max(0.0001f, Mathf.Abs(target - d.Vol) / seconds);
    }

    void SetDuck(float target, float seconds)
    {
        _duckTarget = target;
        _duckSpeed = seconds > 0f ? Mathf.Max(0.0001f, Mathf.Abs(target - _duck) / seconds) : 1000f;
        if (seconds <= 0f) _duck = target;
    }

    static float TrackVolume(string id) => id != null && TrackVolumes.TryGetValue(id, out float v) ? v : 1f;

    // ───────── 毎フレーム（実時間） ─────────

    void Update()
    {
        try
        {
            float dt = Time.unscaledDeltaTime;

            // UI の音（前のフレームにたまった分）
            if (_uiPending != null)
            {
                string k = _uiPending;
                float p = _uiPitch;
                _uiPending = null;
                _uiPriority = 0;
                _uiPitch = 1f;
                PlayEventNow(k, p);
            }

            // ジングル：読み込み待ち → 予約。あとの曲へ移る時刻が来たら移る
            TryStartJingle();
            if (_jingleOn && !_jinglePending && AudioSettings.dspTime >= _jingleThenDsp - ThenLookahead)
            {
                _jingleOn = false;
                string then = _jingleThen;
                if (then == null) SetDuck(1f, 0.8f);
                else if (then.Length > 0)
                {
                    if (LogPlays) Debug.Log($"[Audio] BGM {then}");
                    StartTrack(then, Math.Max(_jingleThenDsp, AudioSettings.dspTime + ScheduleAhead), 0f, 0.3f);
                }
            }

            _duck = Mathf.MoveTowards(_duck, _duckTarget, _duckSpeed * dt);
            _pitch = Mathf.MoveTowards(_pitch, _pitchTarget, _pitchSpeed * dt);
            float bgm = MasterVolume * BgmVolume;

            for (int i = 0; i < _decks.Length; i++)
            {
                Deck d = _decks[i];
                if (d.Pending) TrySchedule(d);
                d.Vol = Mathf.MoveTowards(d.Vol, d.Target, d.Speed * dt);
                if (d.Stopping && d.Vol <= 0f)
                {
                    HardStop(d);
                    continue;
                }
                float v = Mathf.Clamp01(d.Vol * bgm * TrackVolume(d.Id) * (i == _cur ? _duck : 1f));
                d.Intro.volume = v;
                d.Loop.volume = v;
                if (i == _cur)
                {
                    d.Intro.pitch = _pitch;
                    d.Loop.pitch = _pitch;
                }
            }

            _jingleVol = Mathf.MoveTowards(_jingleVol, _jingleVolTarget, _jingleVolSpeed * dt);
            if (_jingleVol <= 0f && _jingle.isPlaying) _jingle.Stop();
            _jingle.volume = Mathf.Clamp01(_jingleVol * bgm * TrackVolume(_jingleId));
        }
        catch (Exception e) { LogOnce("Update", e); }
    }
}
