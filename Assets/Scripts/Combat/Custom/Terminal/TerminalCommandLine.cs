using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】コマンドの行（2026-11-18：Terminal の rm -rf・chmod・fork で共通の予告。docs/app-boss-skills-design.md 4-0）。
/// 画面の上のほう、中央に、ターミナルの1行（横14×縦1マス。地 #0C0C0C）を OnGUI で出す。
/// 2026-11-19【仮】Mac の Terminal の暗いウィンドウにした（docs/mac-look-design.md 2章 7）：左上に信号機、題名のまん中に zsh、中は黒に緑の字。
/// 行（中の面）の大きさ・上の端・秒数は今のまま。タイトルバー（ふち＋7＋線＝9ドット）とふちの分だけ、下へ伸びる
/// ・「% 」（zsh のプロンプト）のあとにコマンドを1文字ずつ打つ（1文字 0.05秒）。後ろに四角いカーソル
/// ・打ち終わると行が白く光る。技が終わったら Close で消す（技の途中でボスが止められたときは、形態変化・正常化の片づけで消える）
/// 文字は雰囲気。どの技かは、同時に出る見た目の予告（床が赤く光る・鍵が浮かぶ・分身の枠）で分かるようにする
/// 2026-11-19【仮】ステージのギミック（TerminalKit）用に足した：打ち直す（Retype）・下に出力の行を流す（AddOutput。灰色の小さい字）
/// </summary>
public class TerminalCommandLine : CustomEffect
{
    /// <summary>【仮】1文字を打つ秒数</summary>
    public const float CharTime = 0.05f;
    /// <summary>【仮】打ち終わって白く光る秒数</summary>
    public const float FlashTime = 0.1f;
    /// <summary>【仮】行の大きさ（マス）</summary>
    const float WidthUnits = 14f;
    /// <summary>【仮】行の上の端の、画面の上の端からの距離（マス）。上の HUD（能力の枠・ゲージ）と重ならない所</summary>
    const float TopFromCeiling = 2f;

    /// <summary>zsh のプロンプト（Mac の今のシェル）【仮】</summary>
    const string Prompt = "% ";

    /// <summary>【仮】出力の行の高さ（PixelGui のドット）</summary>
    const float OutputRowH = 10f;

    string _command = "";
    float _start;
    float _endAt;
    int _outputRows;                                   // 下に出す出力の行の数（0 なら出さない）
    readonly List<string> _output = new List<string>();

    /// <summary>打ち終わったか（打ち直したら、また false）</summary>
    public bool Typed => Time.time - _start >= TypingTime(_command);

    /// <summary>打ち始めてから、打ち終わって光り終わるまでの秒数</summary>
    public static float TypingTime(string command) => (command != null ? command.Length : 0) * CharTime + FlashTime;

    /// <summary>コマンドの行を出す。maxLifetime 秒たったら、Close しなくても消える（念のため）</summary>
    public static TerminalCommandLine Spawn(AttackRunner owner, string command, float maxLifetime = 15f)
    {
        if (owner == null) return null;
        GameObject go = owner.CreateSpawnedObject("TerminalCommandLine", owner.transform.position);
        var line = go.AddComponent<TerminalCommandLine>();
        line._command = command ?? "";
        line._start = Time.time;
        line._endAt = Time.time + Mathf.Max(0.1f, maxLifetime);
        return line;
    }

    /// <summary>コマンドを打ち直す（1文字目から。出力の行は残す）</summary>
    public void Retype(string command)
    {
        _command = command ?? "";
        _start = Time.time;
        _endAt = Mathf.Max(_endAt, Time.time + TypingTime(_command) + 1f);
    }

    /// <summary>下に出す出力の行の数（0 なら出さない）。行が増えた分だけ、ウィンドウが下へ伸びる</summary>
    public void SetOutputRows(int rows)
    {
        _outputRows = Mathf.Max(0, rows);
        while (_output.Count > _outputRows) _output.RemoveAt(0);
    }

    /// <summary>出力の行を1行足す（古い行から押し出される）</summary>
    public void AddOutput(string text)
    {
        if (_outputRows <= 0) return;
        _output.Add(text ?? "");
        while (_output.Count > _outputRows) _output.RemoveAt(0);
    }

    /// <summary>出力の行を全部消す</summary>
    public void ClearOutput() => _output.Clear();

    /// <summary>行を消す（技が終わったとき）</summary>
    public void Close()
    {
        if (this != null) Destroy(gameObject);
    }

    void Update()
    {
        if (Time.time >= _endAt) Destroy(gameObject);
    }

    void OnGUI()
    {
        if (TerminalArt.GuiHidden()) return;
        GUI.depth = 5; // 画面表示（PixelHud）より手前、再起動の暗転より奥
        PixelGui.Begin(false);

        float unit = TerminalArt.GuiPerUnit();
        float w = Mathf.Round(WidthUnits * unit);
        float h = Mathf.Max(12f, Mathf.Round(unit));
        float top = TerminalArt.GuiY(BattleArena.GetBounds().yMax - TopFromCeiling, 60f);
        float outH = _outputRows > 0 ? _outputRows * OutputRowH + 2f : 0f;
        var win = new Rect(Mathf.Round((PixelGui.Width - w) * 0.5f), Mathf.Round(top), w, h + 3f + MacUi.TitleH + outH);

        float t = Time.time - _start;
        int shown = Mathf.Clamp(Mathf.FloorToInt(t / CharTime), 0, _command.Length);
        bool typed = shown >= _command.Length;
        float flash = typed ? 1f - Mathf.Clamp01((t - _command.Length * CharTime) / FlashTime) : 0f;

        // 暗いウィンドウ（信号機・題名 zsh）。中の面が今までの行
        Rect area = MacUi.Gui.Window(win, "zsh", MacLook.Dark);
        PixelGui.Fill(area, TerminalArt.LineBg);
        Rect r = new Rect(area.x, area.y, area.width, area.height - outH); // プロンプトの行
        DrawOutput(new Rect(area.x, r.yMax, area.width, outH));
        if (flash > 0f) PixelGui.Fill(r, TerminalArt.WithAlpha(TerminalArt.White, 0.85f * flash));

        string text = Prompt + _command.Substring(0, shown);
        int size = 16;
        GUIStyle style = PixelGui.Style(true, size);
        if (r.height < 22f || PixelGui.Measure(Prompt + _command + "  ", style) > w - 12f)
        {
            size = 8;
            style = PixelGui.Style(true, size);
        }
        Color ink = flash > 0.4f ? TerminalArt.LineBg : TerminalArt.Green;
        float tx = r.x + 6f;
        float ty = r.y + Mathf.Round((r.height - size) * 0.5f);
        PixelGui.Draw(new Rect(tx, ty, r.width - 12f, size), text, style, ink);

        // 四角いカーソル（打っている間はずっと、打ち終わったら点滅）
        bool cursorOn = !typed || Mathf.FloorToInt(Time.time / 0.25f) % 2 == 0;
        if (cursorOn)
        {
            float cx = tx + Mathf.Ceil(PixelGui.Measure(text, style)) + 1f;
            PixelGui.Fill(new Rect(cx, ty, Mathf.Round(size * 0.75f), size), ink);
        }
        PixelGui.End();
    }

    /// <summary>出力の行（灰色の小さい字。下が新しい）</summary>
    void DrawOutput(Rect r)
    {
        if (_outputRows <= 0 || _output.Count == 0) return;
        GUIStyle style = PixelGui.Style(true, 8);
        Color ink = TerminalArt.Gray;
        for (int i = 0; i < _output.Count; i++)
        {
            float y = r.y + (_outputRows - _output.Count + i) * OutputRowH + 1f;
            PixelGui.Draw(new Rect(r.x + 6f, y, r.width - 12f, 8f), _output[i], style, ink);
        }
    }
}
