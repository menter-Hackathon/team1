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
        float hpX = x;
        x = DrawHp(x, y);
        DrawSlots(x, y);
        DrawKitMarks(hpX + 20f, y + 29f, x - hpX - 24f); // アプリのキットの印とメーター（2026-11-19）
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
            if (_executor != null && _executor.IsSealed(i)) DrawSeal(r); // 封印（2026-11-19：アプリのキット）
            else if (_skills.GetSkill(i) != null && PermissionSealed()) DrawUsableGlow(r); // chmod の封印の間、能力は使える（緑に光る）
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
        // 専用の技の能力（2026-11-18）：もう一度押せる間（zip を持っている間）はふちを光らせ、枠の絵を変える（次に投げるアイコン）
        CustomAttack custom = skill.Attack != null && skill.Attack.Kind == AttackKind.Custom ? skill.Attack.Custom : null;
        AttackRunner runner = _executor != null ? _executor.Runner : null;
        bool charged = custom != null && runner != null && custom.IsCharged(runner);
        if (charged && Mathf.FloorToInt(Time.time / 0.25f) % 2 == 0) edge = custom.ChargedColor;
        Sprite dynamicIcon = custom != null && runner != null ? custom.DisplayIcon(runner) : null;
        PixelGui.Fill(r, PixelGui.FrameDark);
        PixelGui.Fill(PixelGui.Inset(r, 1f), edge);
        PixelGui.Fill(PixelGui.Inset(r, 3f), PixelGui.SlotBg);
        // 元になったアプリのアイコン。能力のアイコンが空なら（中身を決める前のアプリ専用の能力）、これを大きく出す
        RunState run = GameSession.Instance != null ? GameSession.Instance.Run : null;
        Sprite source = run != null ? run.SourceIcon(skill) : null;
        var icon = new Rect(r.x + 2f, r.y + 2f, 32f, 32f);
        PixelGui.DrawSprite(icon, dynamicIcon != null ? dynamicIcon : skill.Icon != null ? skill.Icon : source);
        float ratio = _executor != null ? _executor.GetCooldownRatio(skill) : 0f;
        if (ratio > 0f) PixelGui.Fill(new Rect(icon.x, icon.y, icon.width, Mathf.Ceil(icon.height * ratio)), CooldownColor);

        // 元になったアプリのアイコンを右下に小さく重ねる（どのアプリの技か、絵で分かるように。2026-11-18：メンターのFB）
        if (source != null && (skill.Icon != null || dynamicIcon != null))
        {
            var badge = new Rect(r.xMax - 15f, r.yMax - 15f, 18f, 18f);
            PixelGui.Fill(badge, PixelGui.FrameDark);
            PixelGui.Fill(PixelGui.Inset(badge, 1f), PixelGui.White);
            PixelGui.DrawSprite(PixelGui.Inset(badge, 1f), source);
        }
    }

    // ───────── アプリのキット（2026-11-19。docs/app-kits-1-design.md E5・E6）【仮】 ─────────

    static readonly Color SealShade = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color SealRed = new Color32(0xff, 0x45, 0x3a, 0xff);
    static readonly Color SealGold = new Color32(0xff, 0xd6, 0x0a, 0xff);
    static readonly Color PipGood = new Color32(0x30, 0xd1, 0x58, 0xff);
    static readonly Color PipBad = new Color32(0xff, 0x45, 0x3a, 0xff);
    readonly System.Collections.Generic.List<KitPips.Item> _pips = new System.Collections.Generic.List<KitPips.Item>();
    readonly System.Collections.Generic.List<KitMeter.Item> _meters = new System.Collections.Generic.List<KitMeter.Item>();

    static readonly Color UsableGreen = new Color32(0x33, 0xff, 0x66, 0xff);
    int _permFrame = -1;
    bool _permSealed;

    /// <summary>
    /// Terminal の chmod の封印（ジャンプと近接が使えない）がかかっているか（2026-11-19：docs/app-feedback-v2-design.md 6-2 の B）。
    /// 1フレームに1回だけ探す
    /// </summary>
    bool PermissionSealed()
    {
        if (_permFrame == Time.frameCount) return _permSealed;
        _permFrame = Time.frameCount;
        _permSealed = false;
        foreach (var seal in FindObjectsOfType<TerminalSealEffect>())
        {
            if (seal != null && !seal.Released) { _permSealed = true; break; }
        }
        return _permSealed;
    }

    // 使える枠のふちを緑に光らせる（点滅でなく、明るさをゆらす）
    static void DrawUsableGlow(Rect r)
    {
        float k = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 10f);
        Color c = UsableGreen;
        c.a = k;
        PixelGui.Border(r, c, 2f);
    }

    // 封印された枠：暗くして、赤い斜線と鍵（文字なし）
    static void DrawSeal(Rect r)
    {
        var inner = PixelGui.Inset(r, 2f);
        PixelGui.Fill(inner, SealShade);
        for (float d = 0f; d < inner.width; d += 6f)
        {
            // 斜線（右上がり）を 2ドットの点で描く
            for (float k = 0f; k < inner.height; k += 2f)
            {
                float px = inner.x + d + k;
                if (px > inner.xMax - 2f) break;
                PixelGui.Fill(new Rect(px, inner.yMax - k - 2f, 2f, 2f), SealRed);
            }
        }
        float cx = r.center.x, cy = r.center.y;
        PixelGui.Fill(new Rect(cx - 4f, cy - 7f, 8f, 2f), SealGold);  // つる
        PixelGui.Fill(new Rect(cx - 4f, cy - 7f, 2f, 5f), SealGold);
        PixelGui.Fill(new Rect(cx + 2f, cy - 7f, 2f, 5f), SealGold);
        PixelGui.Fill(new Rect(cx - 6f, cy - 2f, 12f, 9f), PixelGui.FrameDark); // 本体
        PixelGui.Fill(new Rect(cx - 5f, cy - 1f, 10f, 7f), SealGold);
        PixelGui.Fill(new Rect(cx - 1f, cy + 1f, 2f, 3f), PixelGui.FrameDark); // 鍵穴
    }

    // ライフの下：倍率の印（アイコン 8ドット＋矢印）と、細いメーター
    void DrawKitMarks(float x, float y, float width)
    {
        _pips.Clear();
        _meters.Clear();
        KitPips.Collect(_pips);
        KitMeter.Collect(_meters);
        if (_pips.Count == 0 && _meters.Count == 0) return;
        width = Mathf.Max(40f, width);
        const float pipStep = 14f;
        float px = x, py = y;
        foreach (var p in _pips)
        {
            if (px + 12f > x + width) { px = x; py += 11f; }
            PixelGui.Fill(new Rect(px - 1f, py - 1f, 10f, 10f), PixelGui.FrameDark);
            if (p.Icon != null) PixelGui.DrawSprite(new Rect(px, py, 8f, 8f), p.Icon);
            else PixelGui.Fill(new Rect(px, py, 8f, 8f), PixelGui.White);
            Arrow(px + 9f, py + 1f, p.Up, p.Good ? PipGood : PipBad);
            px += pipStep;
        }
        float my = _pips.Count > 0 ? py + 11f : y;
        bool blinkOn = Mathf.FloorToInt(Time.unscaledTime / 0.25f) % 2 == 0;
        foreach (var m in _meters)
        {
            float bx = x;
            if (m.Icon != null)
            {
                PixelGui.DrawSprite(new Rect(x, my, 8f, 8f), m.Icon);
                bx = x + 10f;
            }
            float bw = Mathf.Min(48f, width - (bx - x));
            var bar = new Rect(bx, my + 2f, bw, 5f);
            PixelGui.Fill(bar, PixelGui.FrameDark);
            Color c = m.Color;
            if (m.Blink && !blinkOn) c = Color.Lerp(c, PixelGui.White, 0.6f);
            PixelGui.Fill(new Rect(bar.x + 1f, bar.y + 1f, Mathf.Round((bar.width - 2f) * Mathf.Clamp01(m.Ratio)), bar.height - 2f), c);
            my += 10f;
        }
    }

    // 5×5 の矢印（上向き・下向き）
    static void Arrow(float x, float y, bool up, Color c)
    {
        float[] widths = { 1f, 3f, 5f, 1f, 1f };
        for (int i = 0; i < 5; i++)
        {
            int row = up ? i : 4 - i;
            float w = widths[i];
            PixelGui.Fill(new Rect(x + (5f - w) * 0.5f, y + row, w, 1f), c);
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
            // 「あと少し」（2026-11-19：ステージの山と谷 6章・7章【仮】）：80% を越えたか残り3体で、影が小さくふるえ、ゲージがゆっくり明滅する
            bool close = !wave.IsCleared && IsAlmostThere(wave);
            Rect shadow = iconRect;
            if (close)
            {
                int j = Mathf.FloorToInt(Time.unscaledTime * 20f);
                shadow.x += j % 3 - 1;
                shadow.y += (j / 3) % 3 - 1;
            }
            PixelGui.DrawSprite(shadow, _director.Plan.Profile != null ? _director.Plan.Profile.Icon : null, new Color(0.22f, 0.22f, 0.26f, 1f));
            Rect inside = GaugeFrame(gauge);
            bool blink = wave.IsCleared && Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
            Color fill = blink ? PixelGui.White : PixelGui.Magenta;
            if (close) fill = Color.Lerp(PixelGui.Magenta, PixelGui.White, (Mathf.Sin(Time.unscaledTime * 5f) * 0.5f + 0.5f) * 0.45f);
            PixelGui.Fill(new Rect(inside.x, inside.y, Mathf.Round(inside.width * wave.Progress), inside.height), fill);
            DrawBossPips(gauge);
            return;
        }

        PixelGui.DrawSprite(iconRect, boss.Profile != null ? boss.Profile.Icon : null);
        Rect fillArea = GaugeFrame(gauge);
        float ratio = boss.Gauge != null ? Mathf.Clamp01(boss.Gauge.Ratio) : 0f;
        // そのボスだけの棒の見せ方（2026-11-18：Terminal は本当の50%で満タンに見せ、再起動で空・赤に戻す）
        IBossGaugeDisplay display = boss.GetComponent<IBossGaugeDisplay>();
        Color? overrideColor = null;
        if (display != null)
        {
            ratio = Mathf.Clamp01(display.DisplayRatio(boss));
            overrideColor = display.FillColor(boss);
        }
        // ゲージの色：そのステージのアプリの色（4色なら左から4つに区切る）。形態変化中は赤く点滅、正常化で白
        AppTheme theme = _director != null && _director.Plan != null ? _director.Plan.App.Theme : null;
        Color[] colors = theme != null && theme.Accents != null && theme.Accents.Length > 0 ? theme.Accents : new[] { GaugeColor };
        bool angry = display == null && boss.State == BossState.Transition && Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
        bool done = boss.State == BossState.Normalized || boss.State == BossState.Ejected;
        int n = Mathf.Clamp(colors.Length, 1, 4);
        float fillW = Mathf.Round(fillArea.width * ratio);
        for (int i = 0; i < n; i++)
        {
            float segStart = Mathf.Round(fillArea.width * i / n), segEnd = Mathf.Round(fillArea.width * (i + 1) / n);
            float w = Mathf.Clamp(fillW - segStart, 0f, segEnd - segStart);
            if (w <= 0f) continue;
            PixelGui.Fill(new Rect(fillArea.x + segStart, fillArea.y, w, fillArea.height), done ? PixelGui.White : angry ? GaugeAngryColor : overrideColor ?? colors[i]);
        }
        if ((boss.State == BossState.Fighting || boss.State == BossState.Transition) && (display == null || display.ShowThresholdMark(boss)))
        {
            float threshold = boss.Profile != null ? boss.Profile.Phase2Threshold : 0.5f;
            PixelGui.Fill(new Rect(fillArea.x + Mathf.Round(fillArea.width * threshold), gauge.y - 2f, 2f, gauge.height + 4f), PixelGui.Ink);
        }
        DrawBossPips(gauge);
    }

    readonly System.Collections.Generic.List<KitPips.Item> _bossPips = new System.Collections.Generic.List<KitPips.Item>();

    // ボスの印（アプリのキット：ボスの実績・アドオン・ボスの倍率など）。ゲージの下に、右から左へ並べる（2026-11-19）
    void DrawBossPips(Rect gauge)
    {
        _bossPips.Clear();
        KitPips.CollectBoss(_bossPips);
        if (_bossPips.Count == 0) return;
        float px = gauge.xMax - 13f, py = gauge.yMax + 4f;
        foreach (var p in _bossPips)
        {
            if (px < gauge.x) { px = gauge.xMax - 13f; py += 11f; }
            PixelGui.Fill(new Rect(px - 1f, py - 1f, 10f, 10f), PixelGui.FrameDark);
            if (p.Icon != null) PixelGui.DrawSprite(new Rect(px, py, 8f, 8f), p.Icon);
            else PixelGui.Fill(new Rect(px, py, 8f, 8f), PixelGui.White);
            Arrow(px + 9f, py + 1f, p.Up, p.Good ? PipGood : PipBad);
            px -= 14f;
        }
    }

    /// <summary>ボスまで「あと少し」か（ステージの山と谷が動いているときだけ。StagePaceTable.Enabled = false なら、いつも false）</summary>
    static bool IsAlmostThere(EnemyWave wave)
    {
        StagePacer pacer = StagePacer.Current;
        if (pacer == null || pacer.Phase == PacePhase.Done) return false;
        return wave.Progress >= StagePaceTable.GaugeShakeFrom || (pacer.Remaining > 0 && pacer.Remaining <= StagePaceTable.MarkFrom);
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
