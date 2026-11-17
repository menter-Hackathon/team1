using UnityEngine;

/// <summary>
/// 【仮】リザルト画面：正常化したアプリと、手に入れた能力を並べる。UI 担当の本番画面ができたら消す。
/// Enter キーか「タイトルへ」で戻る。
/// </summary>
public class RunResultScreen : MonoBehaviour
{
    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.enterKey.wasPressedThisFrame && GameSession.Instance != null) GameSession.Instance.ReturnToTitle();
    }

    void OnGUI()
    {
        GameSession session = GameSession.Instance;
        if (session == null || session.IsTransitioning) return;
        PixelGui.Begin();

        int rows = session.Run != null ? session.Run.Cleared.Count : 0;
        float w = 340f;
        float panelH = 64f + rows * 30f + 30f + 44f;
        float x = (PixelGui.Width - w) * 0.5f;
        float y = Mathf.Max(8f, (PixelGui.Height - panelH) * 0.5f);
        GUI.Box(new Rect(x, y, w, panelH), GUIContent.none, PixelGui.Panel);
        GUI.Label(new Rect(x, y + 10f, w, 40f), "RESULT", PixelGui.Title);
        float cy = y + 60f;

        if (session.Run != null)
        {
            foreach (var record in session.Run.Cleared)
            {
                // 能力のアイコン（16×16 ドット）を等倍で
                if (record.Reward != null) PixelGui.DrawSpriteAt(x + 20f, cy + 2f, record.Reward.Icon);
                string ability = record.Reward != null ? record.Reward.DisplayName : "-";
                GUI.Label(new Rect(x + 44f, cy, w - 60f, 24f), $"{record.Stage.DisplayName}　{record.Stage.UsageHours:0.#} h　→　{ability}", PixelGui.Label);
                cy += 30f;
            }
            GUI.Label(new Rect(x + 20f, cy, w - 40f, 24f), $"リトライ {session.Run.RetryCount}", PixelGui.Label);
            cy += 30f;
        }
        if (GUI.Button(new Rect(x + w * 0.25f, cy + 4f, w * 0.5f, 28f), "タイトルへ", PixelGui.Button)) session.ReturnToTitle();
        PixelGui.End();
    }
}
