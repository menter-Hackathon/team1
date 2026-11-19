using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 【仮】図鑑の詳しい画面の、ボスの姿と技の札（2026-11-19：docs/ui-text-and-dex-design.md 3-4・3-5）。OnGUI で描く。
/// ・ボスはランと同じ決め方（RunPlanner.ResolveBoss）で決める
/// ・プレハブは Instantiate しない。プレハブの BossArmorVisual から、体・ウィンドウ・手・顔の絵と位置を読むだけ
///   着ているウィンドウ：対応表のボス（Chrome・VS Code）はプレハブの専用の絵、代用ボス・ラスボスはアイコンから作った絵（StageDirector と同じ）
/// ・技の札：プロフィールの第1形態・第2形態の技の、アセットの名前から作る英字（第2形態だけの技はマゼンタの縁）
/// アプリごとに1回作って持っておく（OnGUI は1フレームに何回も呼ばれるため）
/// </summary>
public class BossPortrait
{
    /// <summary>技の札1つ分</summary>
    public struct Move
    {
        public string Name;
        public Sprite Icon;
        public bool Phase2Only;
    }

    // 絵と、ボスの中心からのずれ（マス）
    Sprite[] _body;
    Vector2 _bodyPos;
    float _bodyFps = 4f;
    Sprite _hands;
    Vector2 _handsPos;
    Sprite[] _face;
    Vector2 _facePos;
    Sprite _window;
    Vector2 _windowPos;

    public AppTheme Theme { get; private set; }
    public Sprite Icon16 { get; private set; }
    public Sprite Icon24 { get; private set; }
    public readonly List<Move> Moves = new List<Move>();

    bool HasArt => (_body != null && _body.Length > 0) || _window != null || _hands != null || (_face != null && _face.Length > 0);

    static readonly Dictionary<string, BossPortrait> Cache = new Dictionary<string, BossPortrait>();

    /// <summary>図鑑の1アプリ分のボス（作ってあれば、それ）</summary>
    public static BossPortrait For(AppDexEntry e)
    {
        string key = !string.IsNullOrEmpty(e.key) ? e.key : AppDex.KeyOf(e.name);
        if (Cache.TryGetValue(key, out BossPortrait p)) return p;
        p = new BossPortrait();
        try { p.Build(e); }
        catch (System.Exception ex) { Debug.LogException(ex); } // 読めなかった所は飛ばす（アイコンだけでも出す）
        Cache[key] = p;
        return p;
    }

    void Build(AppDexEntry e)
    {
        AppUsageRow row = e.Row;
        AppVisual appVisual = AppVisuals.Get(row);
        Icon16 = appVisual != null ? appVisual.Icon16 : null;
        Icon24 = appVisual != null ? appVisual.Icon24 : null;
        GameSession session = GameSession.Instance;
        BossLook look = session != null ? RunPlanner.ResolveBoss(session.Catalog, session.Abilities, row) : null;
        if (look == null) return;
        Theme = look.Theme;

        // 絵（プレハブの BossArmorVisual から読むだけ）
        BossController prefab = look.Prefab;
        BossArmorVisual armor = prefab != null ? prefab.GetComponentInChildren<BossArmorVisual>(true) : null;
        if (armor != null)
        {
            Transform root = prefab.transform;
            if (armor.Body != null)
            {
                var anim = armor.Body.GetComponent<SpriteFrameAnimation>();
                if (anim != null && anim.Frames != null && anim.Frames.Count > 0)
                {
                    _body = new Sprite[anim.Frames.Count];
                    for (int i = 0; i < _body.Length; i++) _body[i] = anim.Frames[i];
                }
                else if (armor.Body.sprite != null) _body = new[] { armor.Body.sprite };
                _bodyPos = Local(root, armor.Body.transform);
            }
            if (armor.Hands != null)
            {
                _hands = armor.Hands.sprite;
                _handsPos = Local(root, armor.Hands.transform);
            }
            if (armor.Face != null)
            {
                IReadOnlyList<Sprite> frames = armor.FaceFrames;
                _face = frames != null && frames.Count >= 2 ? new[] { frames[0], frames[1] } : armor.Face.sprite != null ? new[] { armor.Face.sprite } : null;
                _facePos = Local(root, armor.Face.transform);
            }
            if (armor.Window != null)
            {
                _windowPos = Local(root, armor.Window.transform);
                IReadOnlyList<Sprite> stages = armor.WindowStages;
                _window = stages != null && stages.Count > 0 ? stages[0] : armor.Window.sprite;
            }
        }
        // 代用ボス・ラスボス：着ているウィンドウは、アイコンから作ったきれいな絵（StageDirector と同じ作り方）
        if (look.Visual != null && look.Visual.Icon24 != null)
        {
            Sprite[] made = BossWindowArt.Create(look.Visual.Icon24, look.Theme, $"DexWindow_{look.App.DisplayName}");
            _window = made[0];
            for (int i = 1; i < made.Length; i++) // ひびの絵は使わない
            {
                if (made[i] == null) continue;
                Object.Destroy(made[i].texture);
                Object.Destroy(made[i]);
            }
        }

        // 技の札（第1形態 → 第2形態だけの技。最大4つ。移動用の瞬間移動は入れない）
        BossProfile profile = look.Profile;
        if (profile == null) return;
        var phase1 = new HashSet<string>();
        AddMoves(profile.Phase1 != null ? profile.Phase1.Moves : null, profile.WarpAttack, false, phase1);
        AddMoves(profile.Phase2 != null ? profile.Phase2.Moves : null, profile.WarpAttack, true, phase1);
    }

    void AddMoves(AttackData[] moves, AttackData warp, bool phase2, HashSet<string> phase1)
    {
        if (moves == null) return;
        foreach (AttackData a in moves)
        {
            if (a == null || a == warp || Moves.Count >= 4) continue;
            string name = MoveName(a.name);
            if (string.IsNullOrEmpty(name) || Moves.Exists(m => m.Name == name)) continue; // 同じ名前（P1 と P2）は1つにまとめる
            if (!phase2) phase1.Add(name);
            Moves.Add(new Move { Name = name, Icon = a.Sprite, Phase2Only = phase2 && !phase1.Contains(name) });
        }
    }

    // ボスの中心からのずれ（マス）。プレハブの中の localPosition を、ボスの根元までたどって足す（シーンに置かないので position は使わない）
    static Vector2 Local(Transform root, Transform t)
    {
        Vector3 p = Vector3.zero;
        for (Transform c = t; c != null && c != root; c = c.parent) p = c.localPosition + c.localRotation * Vector3.Scale(c.localScale, p);
        return p;
    }

    /// <summary>
    /// アセットの名前 → 画面に出す技の名前（3-5）：末尾の _P1・_P2 を取る → 最後の「_」より後ろだけ → 小文字の次の大文字の前に空白。
    /// 例：Attack_Zoom_ClapStorm_P2 → Clap Storm、Attack_Terminal_RmRf_P1 → Rm Rf
    /// </summary>
    public static string MoveName(string asset)
    {
        if (string.IsNullOrEmpty(asset)) return "";
        string s = asset.Trim();
        if (s.EndsWith("_P1") || s.EndsWith("_P2")) s = s.Substring(0, s.Length - 3);
        int u = s.LastIndexOf('_');
        if (u >= 0) s = s.Substring(u + 1);
        var sb = new StringBuilder(s.Length + 4);
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i]) && char.IsLower(s[i - 1])) sb.Append(' ');
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    // ───────── 描く ─────────

    const float Px = 2f;           // 元の絵の 1ドット＝2
    const float Unit = 18f * Px;   // 1マス＝18 ドット
    const float FloorH = 24f;

    /// <summary>ボスの枠（縁 2・テーマの空を暗くした地・下に床の帯）と、ボスの姿（2倍。足を床の帯に合わせる）</summary>
    public void Draw(Rect frame)
    {
        PixelGui.Fill(frame, PixelGui.Frame);
        Rect inner = PixelGui.Inset(frame, 2f);
        Color sky = Theme != null ? new Color(Theme.Sky.r * 0.45f, Theme.Sky.g * 0.45f, Theme.Sky.b * 0.45f, 1f) : PixelGui.SlotBg;
        Color floor = Theme != null ? Theme.Floor : PixelGui.Frame;
        floor.a = 1f;
        PixelGui.Fill(inner, sky);
        float floorTop = inner.yMax - FloorH;
        PixelGui.Fill(new Rect(inner.x, floorTop, inner.width, FloorH), floor);

        if (!HasArt)
        {
            // 絵が読めなかったとき：アプリのアイコン（24 を4倍）だけ
            if (Icon24 != null)
            {
                float s = 96f;
                PixelGui.DrawSprite(new Rect(Mathf.Round(inner.center.x - s * 0.5f), Mathf.Round(floorTop - s - 8f), s, s), Icon24);
            }
            return;
        }

        float t = Time.unscaledTime;
        Sprite body = Frame(_body, Mathf.FloorToInt(t / 0.25f));        // 体：0.25秒ごとにコマ送り
        Sprite face = _face != null && _face.Length > 0
            ? _face[_face.Length >= 2 && Mathf.Repeat(t, 3f) < 0.12f ? 1 : 0] // 顔：3秒に1回まばたき
            : null;

        // 足の位置と横のまん中を、絵の外形（体は1コマ目）から決める
        float minX = float.MaxValue, maxX = float.MinValue, maxBottom = float.MinValue;
        void Measure(Sprite sp, Vector2 pos)
        {
            if (sp == null) return;
            Rect r = OffsetRect(sp, pos);
            minX = Mathf.Min(minX, r.xMin);
            maxX = Mathf.Max(maxX, r.xMax);
            maxBottom = Mathf.Max(maxBottom, r.yMax);
        }
        Measure(Frame(_body, 0), _bodyPos);
        Measure(_window, _windowPos);
        Measure(_hands, _handsPos);
        Measure(face, _facePos);
        if (minX > maxX) return;

        float bob = Mathf.Repeat(t, 2f) < 1f ? 0f : -Px; // 全体を上下に 1ドット（2秒の周期）
        var anchor = new Vector2(Mathf.Round(inner.center.x - (minX + maxX) * 0.5f), Mathf.Round(floorTop - maxBottom + bob));
        // 重ねる順（後ろから）：体 → ウィンドウ → 手 → 顔
        DrawPart(anchor, body, _bodyPos);
        DrawPart(anchor, _window, _windowPos);
        DrawPart(anchor, _hands, _handsPos);
        DrawPart(anchor, face, _facePos);
    }

    static Sprite Frame(Sprite[] frames, int index) => frames != null && frames.Length > 0 ? frames[Mathf.Abs(index) % frames.Length] : null;

    // ボスの中心を (0, 0) にしたときの、絵の四角（画面の向き：y は下へ）。絵の pivot を位置に合わせる
    static Rect OffsetRect(Sprite sp, Vector2 pos)
    {
        Rect r = sp.rect;
        Vector2 pivot = sp.pivot;
        float x = pos.x * Unit - pivot.x * Px;
        float y = -pos.y * Unit - (r.height - pivot.y) * Px;
        return new Rect(x, y, r.width * Px, r.height * Px);
    }

    static void DrawPart(Vector2 anchor, Sprite sp, Vector2 pos)
    {
        if (sp == null) return;
        Rect r = OffsetRect(sp, pos);
        PixelGui.DrawSprite(new Rect(Mathf.Round(anchor.x + r.x), Mathf.Round(anchor.y + r.y), r.width, r.height), sp);
    }

    /// <summary>技の札（高さ 22）：左に技の絵（枠 20。無ければアプリのアイコン）、右に名前（英字 8）。第2形態だけの技は縁がマゼンタ</summary>
    public void DrawMove(Rect r, Move m)
    {
        PixelGui.Fill(r, m.Phase2Only ? PixelGui.Magenta : PixelGui.FrameDark);
        PixelGui.Fill(PixelGui.Inset(r, 1f), PixelGui.White);
        var box = new Rect(r.x + 1f, r.y + 1f, 20f, 20f);
        PixelGui.Fill(box, PixelGui.SlotBg);
        DrawFit(PixelGui.Inset(box, 2f), m.Icon != null ? m.Icon : Icon16);
        GUIStyle en = PixelGui.Style(true, 8);
        float w = r.xMax - (box.xMax + 4f) - 3f;
        PixelGui.TextEn(new Rect(box.xMax + 4f, r.y + 7f, w, 10f), PixelGui.Fit(m.Name, en, w), 8, PixelGui.Ink);
    }

    // 縦横の比を保って、四角の中に収めて描く（細長い技の絵がつぶれないように）
    static void DrawFit(Rect box, Sprite sp)
    {
        if (sp == null) return;
        Rect r = sp.rect;
        float s = Mathf.Min(box.width / Mathf.Max(1f, r.width), box.height / Mathf.Max(1f, r.height));
        float w = Mathf.Max(1f, Mathf.Round(r.width * s)), h = Mathf.Max(1f, Mathf.Round(r.height * s));
        PixelGui.DrawSprite(new Rect(Mathf.Round(box.center.x - w * 0.5f), Mathf.Round(box.center.y - h * 0.5f), w, h), sp);
    }
}
