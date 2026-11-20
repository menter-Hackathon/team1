using System.Collections.Generic;
using UnityEngine;

// 【仮】感染のもや（2026-11-19。docs/stage-pacing-design.md 6章・7章。優先度 A-4・B-5 の紫が吸いこまれる所）。担当2 の物。
// ・ステージが始まったとき（StagePacer.Started）、画面全体に紫のもや：半透明の紫の板＋マゼンタの乱れの横線（5本。最後の1本は吸いこまれる）＋四隅に濃い紫。
//   感染したデスクトップの画面（20章）と同じ紫（#6A2A8C）とマゼンタ（#ED38FF）。
// ・場面が変わるたびに1段、目に見えて晴れる：入り 100% → 盛り上がり 75% → 出来事 55% → ひと休み 25% → 最高潮の間に 0% へ（進み具合で）。
//   晴れるたびに、乱れの線が1本ずつ、はじけて消える。もやがうすくなると、背景の壁紙がアプリの本来の色で見えてくる。
// ・ボスの登場（PacePhase.Done）：残った紫が、ボスの出る位置へ吸いこまれて消える（0.65秒。2026-11-20 にボスの登場の待ちを 1秒 → 0.35秒に詰めたので、終わりはボスの点滅と重なる）。
// ・雑魚・プレイヤー・攻撃の予告・床より後ろ、背景（壁紙 -100・部屋の中 -50）より前に描く（重なる順番 -8）。一番濃くても 35%。
// ・カメラの子（横スクロールでも画面に貼り付く）。ボス戦の間は出さない。文字は出さない。

/// <summary>感染のもや（雑魚の時間に画面を紫にする。進むほど晴れる）</summary>
public class InfectionVeil : MonoBehaviour
{
    const int Order = -8;
    const int LineCount = 5;
    /// <summary>板の濃さ（いちばん濃いとき）。四隅と重なっても 35% をこえない</summary>
    const float PlateAlpha = 0.2f;
    const float EdgeAlpha = 0.06f;
    const float CornerAlpha = 0.05f;
    const float LineAlpha = 0.55f;
    /// <summary>晴れる速さ（1秒あたりの濃さ）</summary>
    const float ClearSpeed = 0.9f;
    /// <summary>吸いこまれる秒数</summary>
    const float SuckSeconds = 0.55f;

    static readonly Color Purple = new Color32(0x6a, 0x2a, 0x8c, 0xff);
    static readonly Color DeepPurple = new Color32(0x1e, 0x08, 0x30, 0xff);
    static readonly Color Magenta = new Color32(0xed, 0x38, 0xff, 0xff);

    static InfectionVeil _current;

    class Line
    {
        public SpriteRenderer Sr;
        public float Y;          // 画面の中の高さ（ローカル）
        public float Width;
        public float Next;       // 次に形を変える時刻
        public float Flicker = 1f;
    }

    StagePacer _pacer;
    Vector2 _view;
    readonly List<SpriteRenderer> _plates = new List<SpriteRenderer>();
    readonly List<float> _plateBase = new List<float>();
    readonly List<Line> _lines = new List<Line>();
    PacePhase _phase;
    float _level = 1f;
    float _climaxFrom = -1f;
    bool _sucking;
    float _suckAge;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        StagePacer.Started -= OnStarted;
        StagePacer.Started += OnStarted;
    }

    static void OnStarted(StagePacer pacer)
    {
        if (pacer == null) return;
        if (_current != null) Destroy(_current.gameObject);
        Transform root = FxKit.CameraChild("InfectionVeil");
        if (root == null) return;
        _current = root.gameObject.AddComponent<InfectionVeil>();
        _current.Init(pacer);
    }

    void Init(StagePacer pacer)
    {
        _pacer = pacer;
        _phase = pacer.Phase;
        _level = Target(pacer.Phase);
        _view = FxKit.ViewSize();
        Build();
        pacer.PhaseChanged += OnPhaseChanged;
        Apply();
    }

    void OnDestroy()
    {
        if (_pacer != null) _pacer.PhaseChanged -= OnPhaseChanged;
        if (_current == this) _current = null;
    }

    // ───────── 形 ─────────

    void Build()
    {
        float w = _view.x + 1f, h = _view.y + 1f;
        float hw = w * 0.5f, hh = h * 0.5f;
        // 板（紫）
        AddPlate(Vector2.zero, new Vector2(w, h), Purple, PlateAlpha, "Plate");
        // 画面のふち（濃い紫の帯。四隅は縦と横が重なって濃くなる）
        float band = 0.7f;
        AddPlate(new Vector2(0f, hh - band * 0.5f), new Vector2(w, band), DeepPurple, EdgeAlpha, "EdgeTop");
        AddPlate(new Vector2(0f, -hh + band * 0.5f), new Vector2(w, band), DeepPurple, EdgeAlpha, "EdgeBottom");
        AddPlate(new Vector2(-hw + band * 0.5f, 0f), new Vector2(band, h), DeepPurple, EdgeAlpha, "EdgeLeft");
        AddPlate(new Vector2(hw - band * 0.5f, 0f), new Vector2(band, h), DeepPurple, EdgeAlpha, "EdgeRight");
        // 四隅（さらに濃い四角）
        float c = 1.6f;
        for (int i = 0; i < 4; i++)
        {
            float sx = i % 2 == 0 ? -1f : 1f, sy = i < 2 ? 1f : -1f;
            AddPlate(new Vector2(sx * (hw - c * 0.5f), sy * (hh - c * 0.5f)), new Vector2(c, c), DeepPurple, CornerAlpha, "Corner");
        }
        // 乱れの横線（マゼンタ）。高さはばらばらに
        for (int i = 0; i < LineCount; i++)
        {
            var line = new Line
            {
                Y = Mathf.Lerp(-_view.y * 0.38f, _view.y * 0.38f, (i + Random.Range(0.15f, 0.85f)) / LineCount),
                Width = _view.x * Random.Range(0.3f, 0.65f),
            };
            line.Sr = FxKit.Box(transform, new Vector2(0f, line.Y), new Vector2(line.Width, FxKit.Pixel * 2f), Magenta, Order + 1, "Glitch");
            _lines.Add(line);
        }
    }

    void AddPlate(Vector2 pos, Vector2 size, Color color, float alpha, string objectName)
    {
        color.a = alpha;
        SpriteRenderer sr = FxKit.Box(transform, pos, size, color, Order, objectName);
        _plates.Add(sr);
        _plateBase.Add(alpha);
    }

    // ───────── 進み具合 ─────────

    static float Target(PacePhase phase)
    {
        switch (phase)
        {
            case PacePhase.Intro: return 1f;
            case PacePhase.Build: return 0.75f;
            case PacePhase.Event: return 0.55f;
            case PacePhase.Rest: return 0.25f;
            case PacePhase.Climax: return 0.25f;
            default: return 0f;
        }
    }

    void OnPhaseChanged(PacePhase phase)
    {
        if (_sucking || phase == _phase) return;
        _phase = phase;
        // StagePacer の知らせ（PhaseChanged）の中で呼ばれるので、例外を外へ出さない（ボスの登場の片づけを止めない）
        try
        {
            if (phase == PacePhase.Done)
            {
                BeginSuck();
                return;
            }
            if (phase == PacePhase.Climax && _pacer != null) _climaxFrom = _pacer.Progress;
            PopLine(); // 晴れるたびに、乱れの線が1本はじけて消える
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"感染のもや：{e}");
            if (phase == PacePhase.Done) Destroy(gameObject);
        }
    }

    float CurrentTarget()
    {
        if (_phase != PacePhase.Climax || _pacer == null) return Target(_phase);
        // 最高潮：入ったときの進み具合から 1 までで、25% → 0%
        float from = _climaxFrom >= 0f ? _climaxFrom : _pacer.Progress;
        float k = from >= 0.999f ? 1f : Mathf.InverseLerp(from, 1f, _pacer.Progress);
        return 0.25f * (1f - k);
    }

    void Update()
    {
        if (_pacer == null)
        {
            Destroy(gameObject);
            return;
        }
        if (!_sucking && _pacer.Phase != _phase) OnPhaseChanged(_pacer.Phase); // 知らせが来なかったときの予備

        float dt = Time.deltaTime;
        if (_sucking)
        {
            _suckAge += dt;
            _level = Mathf.MoveTowards(_level, 0f, dt / 0.35f);
            Apply();
            if (_suckAge >= SuckSeconds + 0.1f) Destroy(gameObject);
            return;
        }
        _level = Mathf.MoveTowards(_level, CurrentTarget(), dt * ClearSpeed);
        UpdateLines();
        Apply();
    }

    void LateUpdate()
    {
        // カメラの大きさが変わったら、合わせて伸ばす
        Vector2 v = FxKit.ViewSize();
        if (_view.x > 0.01f && _view.y > 0.01f) transform.localScale = new Vector3(v.x / _view.x, v.y / _view.y, 1f);
    }

    void Apply()
    {
        for (int i = 0; i < _plates.Count; i++)
        {
            if (_plates[i] == null) continue;
            Color c = _plates[i].color;
            c.a = _plateBase[i] * _level;
            _plates[i].color = c;
        }
        foreach (var line in _lines)
        {
            if (line.Sr == null) continue;
            Color c = line.Sr.color;
            c.a = LineAlpha * line.Flicker * Mathf.Clamp01(_level * 1.6f);
            line.Sr.color = c;
        }
    }

    /// <summary>乱れの横線：ときどき横にずれる・幅が変わる・太くなる・高さが飛ぶ（ドットにそろえる）</summary>
    void UpdateLines()
    {
        float now = Time.time;
        foreach (var line in _lines)
        {
            if (line.Sr == null || now < line.Next) continue;
            line.Next = now + Random.Range(0.06f, 0.28f);
            if (Random.value < 0.12f) line.Y = Mathf.Clamp(line.Y + Random.Range(-1.2f, 1.2f), -_view.y * 0.45f, _view.y * 0.45f);
            float width = line.Width * Random.Range(0.6f, 1.25f);
            float thick = Random.value < 0.2f ? 3f : Random.value < 0.5f ? 1f : 2f;
            float x = Mathf.Round(Random.Range(-_view.x * 0.3f, _view.x * 0.3f) / FxKit.Pixel) * FxKit.Pixel;
            float y = Mathf.Round(line.Y / FxKit.Pixel) * FxKit.Pixel;
            Transform t = line.Sr.transform;
            t.localPosition = new Vector3(x, y, t.localPosition.z);
            Vector2 native = line.Sr.sprite != null ? (Vector2)line.Sr.sprite.bounds.size : Vector2.one;
            t.localScale = new Vector3(width / Mathf.Max(native.x, 0.001f), thick * FxKit.Pixel / Mathf.Max(native.y, 0.001f), 1f);
            line.Flicker = Random.value < 0.15f ? 0.25f : Random.Range(0.7f, 1f);
        }
    }

    /// <summary>乱れの線を1本、はじけさせて消す（マゼンタと白のかけら）</summary>
    void PopLine()
    {
        Line pick = null;
        foreach (var line in _lines) if (line.Sr != null) { pick = line; break; }
        if (pick == null) return;
        Vector2 at = pick.Sr.transform.position;
        float half = pick.Sr.bounds.size.x * 0.5f;
        var sprites = new[] { FxArt.Square(2), FxArt.Square(3) };
        var colors = new[] { Magenta, Color.white, Purple };
        for (int i = 0; i < 3; i++)
        {
            Vector2 p = at + new Vector2(Mathf.Lerp(-half, half, (i + 0.5f) / 3f), 0f);
            FxKit.Fragments(p, Vector2.up, 4, sprites, colors, 1f);
        }
        Destroy(pick.Sr.gameObject);
        _lines.Remove(pick);
    }

    // ───────── ボスの登場：紫が吸いこまれる（B-5） ─────────

    void BeginSuck()
    {
        _sucking = true;
        _suckAge = 0f;
        Vector2 target = BossPoint();
        Vector2 center = transform.position;
        // 画面のあちこちから、紫とマゼンタのかけらが、ボスの出る位置へまっすぐ飛ぶ
        int count = 26;
        for (int i = 0; i < count; i++)
        {
            Vector2 from = center + new Vector2(Random.Range(-_view.x * 0.48f, _view.x * 0.48f), Random.Range(-_view.y * 0.45f, _view.y * 0.45f));
            float delay = Random.Range(0f, 0.12f);
            float life = SuckSeconds - delay;
            Color col = i % 3 == 0 ? Magenta : Purple;
            col.a = 0.85f;
            FxAnim.Sprite(FxArt.Square(Random.Range(2, 5)), from, 30, life, col, 1f, "VeilSuck")
                .Velocity((target - from) / Mathf.Max(0.05f, life)).Delay(delay).FadeFrom(0.85f);
        }
        // 残っている乱れの線も、そのまま吸いこまれる
        foreach (var line in _lines)
        {
            if (line.Sr == null) continue;
            Vector2 from = line.Sr.transform.position;
            FxAnim.Sprite(FxArt.Square(2), from, 30, SuckSeconds, Magenta, 1f, "VeilSuckLine")
                .Velocity((target - from) / SuckSeconds).FadeFrom(0.85f);
            line.Sr.gameObject.SetActive(false);
        }
        // 吸いこまれた所で、マゼンタの小さい輪
        FxKit.Ring(target, 2.2f, 0.22f, Magenta, SuckSeconds);
    }

    /// <summary>ボスの出る位置（StageDirector のボス。無ければボス部屋の真ん中）</summary>
    static Vector2 BossPoint()
    {
        var director = FindObjectOfType<StageDirector>();
        if (director != null && director.Boss != null) return (Vector2)director.Boss.transform.position + Vector2.up * 0.8f;
        Rect arena = BattleArena.GetBounds();
        return new Vector2(arena.center.x, arena.yMin + 2f);
    }
}
