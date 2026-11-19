using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】ゲームオーバーの画面（2026-11-18：デザイン担当の画面イメージから）。UI 担当の本番画面ができたら差し替える。
/// ライフが0になると GameSession.ShowGameOver がゲームを止め、この画面を出す。
/// ・濃い紫の画面に「:(」と「PCがウイルスに完全に感染しました」、最後に戦ったアプリ、感染率のゲージ（100% までたまる）
/// ・「▶ このステージからリトライ」（GameSession.FailStage）と「タイトルへ」。← → で選び、Z か Enter で決定
/// GameSession が自分に付ける（シーンに置かなくてよい）。
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    int _focus; // 0＝リトライ、1＝タイトルへ
    float _shownAt = -1f;
    int _tick; // 感染率のゲージの音（10％ごと）

    GameSession Session => GameSession.Instance;
    bool Visible => Session != null && Session.IsGameOver && !Session.IsTransitioning;

    void Update()
    {
        if (!Visible)
        {
            _shownAt = -1f;
            return;
        }
        if (_shownAt < 0f)
        {
            _shownAt = Time.unscaledTime;
            _focus = 0;
            _tick = 0;
        }
        int tick = Mathf.Min(10, Mathf.FloorToInt((Time.unscaledTime - _shownAt) / 0.1f));
        if (tick > _tick)
        {
            _tick = tick;
            GameAudio.Event("gameover_tick", 1f + tick * 0.04f); // ゲージが増えるほど高く
        }
        var kb = Keyboard.current;
        if (kb == null || Time.unscaledTime - _shownAt < 0.4f) return; // 倒れた瞬間の押しっぱなしで決めない
        int before = _focus;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) _focus = 0;
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) _focus = 1;
        if (_focus != before) GameAudio.Event("ui_move");
        if (kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Choose(_focus);
    }

    void Choose(int index)
    {
        if (!Visible) return;
        GameAudio.Event("ui_decide");
        if (index == 0) Session.FailStage();
        else Session.ReturnToTitle();
    }

    void OnGUI()
    {
        if (!Visible) return;
        GUI.depth = -100; // ステージの表示より手前（暗転より奥）
        PixelGui.Begin(false);
        float W = PixelGui.Width, H = PixelGui.Height;
        PixelGui.Fill(new Rect(0f, 0f, W, H), PixelGui.Ink);

        float x = Mathf.Round(Mathf.Max(24f, (W - 640f) * 0.5f + 60f));
        float y = Mathf.Round(Mathf.Max(12f, (H - 360f) * 0.5f + 36f));
        PixelGui.TextEn(new Rect(x, y, 200f, 48f), ":(", 40, PixelGui.Magenta, TextAnchor.UpperLeft);
        PixelGui.Text(new Rect(x, y + 76f, W - x - 24f, 28f), "PCがウイルスに完全に感染しました", 24, PixelGui.White);

        StagePlan last = Session.Run != null ? Session.Run.LastFailed : null;
        int stageNo = Session.Run != null ? Session.Run.CurrentIndex + 1 : 1;
        PixelGui.Text(new Rect(x, y + 114f, W - x - 24f, 16f), "感染率が100%に達しました。", 12, PixelGui.Lavender);
        if (last != null)
            PixelGui.Text(new Rect(x, y + 132f, W - x - 24f, 16f), $"最後に戦ったウィンドウ：{last.App.DisplayName}.exe（STAGE {stageNo}）", 12, PixelGui.Lavender);

        // 感染率のゲージ（出てから1秒で 100% までたまる）
        float t = _shownAt < 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - _shownAt) / 1f);
        float gy = y + 160f;
        PixelGui.TextEn(new Rect(x, gy, 80f, 10f), "INFECTED", 8, PixelGui.Lavender);
        var bar = new Rect(x + 80f, gy + 1f, 180f, 8f);
        PixelGui.Fill(bar, new Color32(0x3a, 0x1c, 0x4f, 0xff));
        PixelGui.Fill(new Rect(bar.x, bar.y, Mathf.Round(bar.width * t), bar.height), PixelGui.Magenta);
        PixelGui.TextEn(new Rect(bar.xMax + 10f, gy, 60f, 10f), $"{Mathf.RoundToInt(t * 100f)}%", 8, PixelGui.Lavender);

        // ボタン
        float by = gy + 28f;
        if (PixelGui.DarkButton(new Rect(x, by, 184f, 28f), "▶ このステージからリトライ", true, _focus == 0)) Choose(0);
        if (PixelGui.DarkButton(new Rect(x + 196f, by, 96f, 28f), "タイトルへ", false, _focus == 1)) Choose(1);

        // 右下：感染が進んでいくウイルス（灰 → 紫 → マゼンタ）
        Sprite[] zako = GameArt.Instance != null ? GameArt.Instance.ZakoWalk : null;
        if (zako != null && zako.Length > 0)
        {
            Sprite f = zako[Mathf.FloorToInt(Time.unscaledTime * 6f) % zako.Length];
            Rect sr = f.textureRect;
            float zw = sr.width * 2f, zh = sr.height * 2f;
            Color[] tints = { new Color(0.6f, 0.6f, 0.6f), new Color(0.7f, 0.55f, 0.9f), new Color(1f, 0.55f, 1f) };
            for (int i = 0; i < tints.Length; i++)
                PixelGui.DrawSprite(new Rect(W - 24f - (3 - i) * (zw + 12f), H - 16f - zh, zw, zh), f, tints[i]);
        }
        PixelGui.End();
    }
}
