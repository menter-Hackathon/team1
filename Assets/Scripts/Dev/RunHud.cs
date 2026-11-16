using UnityEngine;

/// <summary>
/// 【仮】ステージ中の画面表示：ボスが出たら、画面の右上にウイルス解除ゲージを出す（文字はアプリ名だけ）。
/// ライフは HealthDebugLabel（頭の上）、能力の枠は SkillSlotsDebugLabel（左上）。UI 担当の本番表示ができたら消す。
/// </summary>
public class RunHud : MonoBehaviour
{
    [SerializeField] StageDirector _director;

    GUIStyle _style;

    void OnGUI()
    {
        if (_director == null || _director.Boss == null || _director.Entrance == null || !_director.Entrance.HasAppeared) return;
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 16, fontStyle = FontStyle.Bold };
            _style.normal.textColor = Color.white;
        }

        BossController boss = _director.Boss;
        float w = Mathf.Min(360f, Screen.width - 40f);
        var bar = new Rect(Screen.width - w - 12f, 32f, w, 16f);
        GUI.Label(new Rect(bar.x, 8f, w, 24f), boss.Profile.DisplayName, _style);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = boss.State == BossState.Normalized ? new Color(0.4f, 0.9f, 1f) : new Color(0.3f, 0.95f, 0.5f);
        GUI.DrawTexture(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * Mathf.Clamp01(boss.Gauge.Ratio), bar.height - 4f), Texture2D.whiteTexture);
        // 形態変化の位置
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(bar.x + bar.width * boss.Profile.Phase2Threshold - 1f, bar.y - 3f, 2f, bar.height + 6f), Texture2D.whiteTexture);
    }
}
