using UnityEngine;

/// <summary>
/// 【仮】リザルト画面：正常化したアプリと、手に入れた能力を並べる。UI 担当の本番画面ができたら消す。
/// Enter キーか「タイトルへ」で戻る。
/// </summary>
public class RunResultScreen : MonoBehaviour
{
    GUIStyle _title;
    GUIStyle _label;
    GUIStyle _button;

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.enterKey.wasPressedThisFrame && GameSession.Instance != null) GameSession.Instance.ReturnToTitle();
    }

    void OnGUI()
    {
        GameSession session = GameSession.Instance;
        if (session == null || session.IsTransitioning) return;
        if (_title == null)
        {
            _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 36, fontStyle = FontStyle.Bold };
            _title.normal.textColor = Color.white;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            _label.normal.textColor = Color.white;
            _button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
        }

        float w = 520f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.15f;
        GUI.Label(new Rect(0f, y, Screen.width, 50f), "RESULT", _title);
        y += 80f;

        if (session.Run != null)
        {
            foreach (var record in session.Run.Cleared)
            {
                Sprite icon = record.Reward != null ? record.Reward.Icon : null;
                if (icon != null) GUI.DrawTexture(new Rect(x, y, 32f, 32f), icon.texture, ScaleMode.ScaleToFit);
                string ability = record.Reward != null ? record.Reward.DisplayName : "-";
                GUI.Label(new Rect(x + 44f, y, w - 44f, 34f), $"{record.Stage.DisplayName}　{record.Stage.UsageHours:0.#} h　→　{ability}", _label);
                y += 42f;
            }
            y += 10f;
            GUI.Label(new Rect(x, y, w, 30f), $"リトライ {session.Run.RetryCount}", _label);
            y += 50f;
        }

        if (GUI.Button(new Rect(x + w * 0.25f, y, w * 0.5f, 44f), "タイトルへ", _button)) session.ReturnToTitle();
    }
}
