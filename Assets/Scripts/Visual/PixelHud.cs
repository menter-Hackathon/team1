using UnityEngine;

/// <summary>
/// 【仮】ゲーム中の画面表示（2026-11-18：デザイン担当の画面イメージの部品に合わせて、OnGUI で描く）。
/// イメージは画面の下のバーだが、1画面のステージは床が画面の下の端にあり隠れてしまうので、場所は今までどおり上にした（まさぴ〜了承）。
/// ・左上：相棒（選んだアプリのアイコンと名前）・HP（区切りのゲージ）・能力の枠3つ（枠の左上に 1・2・3。空の枠は点線で「空き」）
///   枠の右下には、その能力の元になったアプリのアイコンを小さく重ねる（RunState.SourceIcon。2026-11-18）
///   選択中の枠は白い縁、ほかの枠の縁はその能力のアプリの色。クールタイム中は、残りの割合だけ上から暗くする
///   入れ替え待ちのときは、新しい能力を枠の右に出して点滅させる。X 長押しの進み具合は、選んでいる枠の下に赤い棒で出す
/// ・右上：「STAGE 1/3  02:31」（何ステージ目か・遊んだ時間）。その下に、ボスが出るまでのゲージ（暗いボスのアイコン・マゼンタ）、
///   ボスが出たらアプリのアイコンとウイルス解除ゲージ（アプリの色。第2形態の印。形態変化中は赤く点滅、正常化で白）
/// UI 担当の本番の画面ができたら差し替える。
/// </summary>
public class PixelHud : MonoBehaviour
{
    [SerializeField] Camera _camera;
    [SerializeField] Health _playerHealth;
    [SerializeField] PlayerSkills _skills;
    [SerializeField] PlayerSkillExecutor _executor;
    [Tooltip("ステージの進行（あれば、そのステージのボスを出す）。無ければ、出ているボスを探す")]
    [SerializeField] StageDirector _director;
#pragma warning disable 0414
    [Tooltip("（使っていない。前の絵で描く表示の並び順）")]
    [SerializeField] int _sortingOrder = 200;
#pragma warning restore 0414

    static readonly Color GaugeColor = new Color32(0x2a, 0xb1, 0x1a, 0xff);
    static readonly Color GaugeAngryColor = new Color32(0xff, 0x40, 0x40, 0xff);
    static readonly Color CooldownColor = new Color(0f, 0f, 0f, 0.62f);
    static readonly Color DiscardColor = new Color32(0xff, 0x00, 0x00, 0xff);
    static readonly Color EmptySlot = new Color32(0xa0, 0xa0, 0xa0, 0xff);
    static readonly Color StageBox = new Color32(0x8a, 0x8a, 0x8a, 0xff);

    BossController _boss;
    float _nextBossSearch;

    void Start()
    {
        if (_skills == null) _skills = FindObjectOfType<PlayerSkills>();
        if (_skills != null)
        {
            if (_playerHealth == null) _playerHealth = _skills.GetComponent<Health>();
            if (_executor == null) _executor = _skills.GetComponent<PlayerSkillExecutor>();
        }
    }

    void OnGUI()
    {
        GameSession session = GameSession.Instance;
        if (session != null && (session.IsTransitioning || session.IsGameOver)) return;
        GUI.depth = 10; // ほかの画面（ゲームオーバーなど）より奥
        PixelGui.Begin(false);
        float x = 6f, y = 6f;
        x = DrawPartner(session, x, y);
        x = DrawHp(x, y);
        DrawSlots(x, y);
        float right = PixelGui.Width - 6f;
        float by = DrawStage(session, right, y);
        DrawBoss(right, by + 4f);
        PixelGui.End();
    }

    // ───────── 左上 ─────────

    float DrawPartner(GameSession session, float x, float y)
    {
        RunState run = session != null ? session.Run : null;
        if (run == null || run.Partner.Skill == null) return x;
        var box = new Rect(x, y, 96f, 36f);
        Rect inner = PixelGui.BevelBox(box);
        Sprite icon = run.Partner.AppIcon != null ? run.Partner.AppIcon : run.Partner.Skill.Icon;
        PixelGui.DrawSprite(new Rect(inner.x + 4f, inner.y + 8f, 16f, 16f), icon);
        PixelGui.Text(new Rect(inner.x + 24f, inner.y + 1f, 60f, 14f), "相棒", 12, PixelGui.SubText);
        string name = run.Partner.IsFiller ? run.Partner.Skill.DisplayName : run.Partner.AppName;
        PixelGui.TextFit(new Rect(inner.x + 24f, inner.y + 16f, inner.width - 26f, 14f), name, 12, PixelGui.Ink);
        return box.xMax + 8f;
    }

    float DrawHp(float x, float y)
    {
        if (_playerHealth == null) return x;
        PixelGui.ShadowText(new Rect(x, y + 14f, 20f, 10f), "HP", PixelGui.Style(true, 8), PixelGui.White, PixelGui.FrameDark, 1f);
        int max = Mathf.Max(1, _playerHealth.Max);
        int life = _playerHealth.Current;
        const float segW = 8f, gap = 2f;
        var frame = new Rect(x + 20f, y + 10f, max * (segW + gap) + gap, 16f);
        PixelGui.Fill(frame, PixelGui.FrameDark);
        bool lowBlink = life == 1 && Mathf.FloorToInt(Time.unscaledTime / 0.25f) % 2 == 0;
        for (int i = 0; i < max; i++)
        {
            Color c = i < life ? (lowBlink ? PixelGui.White : PixelGui.HpGreen) : PixelGui.HpOff;
            PixelGui.Fill(new Rect(frame.x + gap + i * (segW + gap), frame.y + 2f, segW, frame.height - 4f), c);
        }
        return frame.xMax + 10f;
    }

    void DrawSlots(float x, float y)
    {
        if (_skills == null) return;
        const float size = 36f, gap = 6f;
        bool pending = _skills.HasPending;
        bool blinkOn = Mathf.FloorToInt(Time.unscaledTime / 0.25f) % 2 == 0;
        for (int i = 0; i < _skills.Capacity; i++)
        {
            var r = new Rect(x + i * (size + gap), y, size, size);
            bool selected = i == _skills.SelectedSlot;
            bool highlight = pending ? blinkOn : selected; // 入れ替え待ちのときは、全部の枠を点滅させて「1/2/3 で選ぶ」ことを見せる
            DrawSlot(r, _skills.GetSkill(i), highlight);
            Number(r, (i + 1).ToString());
        }
        float px = x + _skills.Capacity * (size + gap) + 6f;
        if (pending)
        {
            var r = new Rect(px, y, size, size);
            DrawSlot(r, _skills.Pending, blinkOn);
            Number(r, "+");
        }

        float hold = _skills.DiscardHoldProgress;
        if (hold > 0f)
        {
            float bx = pending ? px : x + _skills.SelectedSlot * (size + gap);
            PixelGui.Fill(new Rect(bx, y + size + 3f, Mathf.Round(size * hold), 3f), DiscardColor);
        }
    }

    void DrawSlot(Rect r, SkillDefinition skill, bool highlight)
    {
        if (skill == null)
        {
            PixelGui.Fill(r, EmptySlot);
            PixelGui.DashedBorder(r, PixelGui.FrameDark);
            PixelGui.Text(new Rect(r.x, r.y + 4f, r.width, r.height), "空き", 12, PixelGui.Ink, TextAnchor.MiddleCenter);
            if (highlight) PixelGui.Border(r, PixelGui.White, 2f);
            return;
        }
        Color edge = highlight ? PixelGui.White : skill.Theme != null ? skill.Theme.Main : PixelGui.Frame;
        PixelGui.Fill(r, PixelGui.FrameDark);
        PixelGui.Fill(PixelGui.Inset(r, 1f), edge);
        PixelGui.Fill(PixelGui.Inset(r, 3f), PixelGui.SlotBg);
        // 元になったアプリのアイコン。能力のアイコンが空なら（中身を決める前のアプリ専用の能力）、これを大きく出す
        RunState run = GameSession.Instance != null ? GameSession.Instance.Run : null;
        Sprite source = run != null ? run.SourceIcon(skill) : null;
        var icon = new Rect(r.x + 2f, r.y + 2f, 32f, 32f);
        PixelGui.DrawSprite(icon, skill.Icon != null ? skill.Icon : source);
        float ratio = _executor != null ? _executor.GetCooldownRatio(skill) : 0f;
        if (ratio > 0f) PixelGui.Fill(new Rect(icon.x, icon.y, icon.width, Mathf.Ceil(icon.height * ratio)), CooldownColor);

        // 元になったアプリのアイコンを右下に小さく重ねる（どのアプリの技か、絵で分かるように。2026-11-18：メンターのFB）
        if (source != null && skill.Icon != null)
        {
            var badge = new Rect(r.xMax - 15f, r.yMax - 15f, 18f, 18f);
            PixelGui.Fill(badge, PixelGui.FrameDark);
            PixelGui.Fill(PixelGui.Inset(badge, 1f), PixelGui.White);
            PixelGui.DrawSprite(PixelGui.Inset(badge, 1f), source);
        }
    }

    // 枠の左上の番号（キー）
    static void Number(Rect r, string text)
    {
        var tag = new Rect(r.x - 2f, r.y - 3f, 11f, 11f);
        PixelGui.Fill(tag, PixelGui.Ink);
        PixelGui.TextEn(new Rect(tag.x + 2f, tag.y + 2f, 8f, 8f), text, 8, PixelGui.White);
    }

    // ───────── 右上 ─────────

    // 何ステージ目かと遊んだ時間。箱の下端を返す
    float DrawStage(GameSession session, float right, float y)
    {
        RunState run = session != null ? session.Run : null;
        if (run == null || run.Current == null) return y;
        string text = $"STAGE {run.CurrentIndex + 1}/{run.Stages.Count}  {PixelGui.Clock(run.PlaySeconds)}";
        float w = Mathf.Ceil(PixelGui.Measure(text, PixelGui.Style(true, 8))) + 20f;
        var box = new Rect(right - w, y, w, 22f);
        PixelGui.Fill(box, PixelGui.FrameDark);
        PixelGui.Fill(PixelGui.Inset(box, 2f), PixelGui.White);
        PixelGui.Fill(PixelGui.Inset(box, 4f), StageBox);
        PixelGui.TextEn(new Rect(box.x, box.y + 7f, box.width, 8f), text, 8, PixelGui.White, TextAnchor.MiddleCenter);
        return box.yMax;
    }

    void DrawBoss(float right, float y)
    {
        const float gaugeW = 150f, gaugeH = 12f;
        var gauge = new Rect(right - gaugeW, y + 6f, gaugeW, gaugeH);
        var iconRect = new Rect(gauge.x - 28f, y, 24f, 24f);
        BossController boss = FindBoss();
        if (boss == null)
        {
            EnemyWave wave = PreBossWave();
            if (wave == null) return;
            // ボスが出るまで：これから出るボスのアイコンを暗い影で出し、雑魚を倒すごとにマゼンタのゲージがたまる。満タンで白く点滅
            PixelGui.DrawSprite(iconRect, _director.Plan.Profile != null ? _director.Plan.Profile.Icon : null, new Color(0.22f, 0.22f, 0.26f, 1f));
            Rect inside = GaugeFrame(gauge);
            bool blink = wave.IsCleared && Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
            PixelGui.Fill(new Rect(inside.x, inside.y, Mathf.Round(inside.width * wave.Progress), inside.height), blink ? PixelGui.White : PixelGui.Magenta);
            return;
        }

        PixelGui.DrawSprite(iconRect, boss.Profile != null ? boss.Profile.Icon : null);
        Rect fillArea = GaugeFrame(gauge);
        float ratio = boss.Gauge != null ? Mathf.Clamp01(boss.Gauge.Ratio) : 0f;
        // ゲージの色：そのステージのアプリの色（4色なら左から4つに区切る）。形態変化中は赤く点滅、正常化で白
        AppTheme theme = _director != null && _director.Plan != null ? _director.Plan.App.Theme : null;
        Color[] colors = theme != null && theme.Accents != null && theme.Accents.Length > 0 ? theme.Accents : new[] { GaugeColor };
        bool angry = boss.State == BossState.Transition && Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
        bool done = boss.State == BossState.Normalized || boss.State == BossState.Ejected;
        int n = Mathf.Clamp(colors.Length, 1, 4);
        float fillW = Mathf.Round(fillArea.width * ratio);
        for (int i = 0; i < n; i++)
        {
            float segStart = Mathf.Round(fillArea.width * i / n), segEnd = Mathf.Round(fillArea.width * (i + 1) / n);
            float w = Mathf.Clamp(fillW - segStart, 0f, segEnd - segStart);
            if (w <= 0f) continue;
            PixelGui.Fill(new Rect(fillArea.x + segStart, fillArea.y, w, fillArea.height), done ? PixelGui.White : angry ? GaugeAngryColor : colors[i]);
        }
        if (boss.State == BossState.Fighting || boss.State == BossState.Transition)
        {
            float threshold = boss.Profile != null ? boss.Profile.Phase2Threshold : 0.5f;
            PixelGui.Fill(new Rect(fillArea.x + Mathf.Round(fillArea.width * threshold), gauge.y - 2f, 2f, gauge.height + 4f), PixelGui.Ink);
        }
    }

    static Rect GaugeFrame(Rect gauge)
    {
        PixelGui.Fill(gauge, PixelGui.FrameDark);
        Rect inside = PixelGui.Inset(gauge, 2f);
        PixelGui.Fill(inside, PixelGui.HpOff);
        return inside;
    }

    /// <summary>
    /// 雑魚を倒すとボスが出るステージ（1画面）で、ボスが出る前なら、その雑魚の集まり（2026-11-17）。
    /// 横スクロール・ウィンドウ移動（ボス部屋の入口でボスが出る）と、ボスが出たあとは null
    /// </summary>
    EnemyWave PreBossWave()
    {
        if (_director == null || _director.Plan == null || _director.Entrance == null || _director.Wave == null) return null;
        if (_director.Entrance.WaitsForGate || _director.Entrance.HasAppeared) return null;
        return _director.Wave;
    }

    BossController FindBoss()
    {
        if (_director != null)
        {
            if (_director.Boss == null || _director.Entrance == null || !_director.Entrance.HasAppeared) return null;
            return _director.Boss;
        }
        if (_boss != null && _boss.isActiveAndEnabled) return _boss;
        if (Time.unscaledTime < _nextBossSearch) return null;
        _nextBossSearch = Time.unscaledTime + 0.25f;
        _boss = null;
        foreach (var b in FindObjectsOfType<BossController>())
        {
            if (b.isActiveAndEnabled)
            {
                _boss = b;
                break;
            }
        }
        // 雑魚戦のテストシーン：ボスが登場するまでは出さない
        if (_boss != null)
        {
            var entrance = FindObjectOfType<BossEntrance>();
            if (entrance != null && !entrance.HasAppeared) return null;
        }
        return _boss;
    }
}
