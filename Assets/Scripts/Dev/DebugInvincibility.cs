using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【確認用・仮】P キーで、プレイヤーの無敵を入れたり切ったりする（2026-11-18：まさぴ〜が手で試すため）。
/// ・エディタと開発用のビルドだけで動く（本番のビルドでは動かない）
/// ・無敵の間は、プレイヤーがダメージを受けない（Health.DebugInvincible）。点滅はしない。封印やスローなど、ダメージでない効果は受ける
/// ・無敵の間は、画面の上の真ん中に「無敵（P）」と出す
/// シーンが変わっても続く（もう一度 P で切る）
/// </summary>
public class DebugInvincibility : MonoBehaviour
{
    InputAction _toggle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        if (FindObjectOfType<DebugInvincibility>() != null) return;
        var go = new GameObject("DebugInvincibility");
        DontDestroyOnLoad(go);
        go.AddComponent<DebugInvincibility>();
    }

    void Awake()
    {
        _toggle = new InputAction("DebugInvincible", InputActionType.Button);
        _toggle.AddBinding("<Keyboard>/p");
    }

    void OnEnable() => _toggle.Enable();

    void OnDisable()
    {
        _toggle.Disable();
        Health.DebugInvincible = false;
    }

    void OnDestroy() => _toggle.Dispose();

    void Update()
    {
        if (_toggle.WasPressedThisFrame()) Health.DebugInvincible = !Health.DebugInvincible;
    }

    void OnGUI()
    {
        if (!Health.DebugInvincible) return;
        GUI.depth = 0;
        PixelGui.Begin(false);
        var box = new Rect(PixelGui.Width * 0.5f - 40f, 4f, 80f, 16f);
        PixelGui.Fill(box, new Color(0f, 0f, 0f, 0.75f));
        PixelGui.Text(box, "無敵（P）", 10, PixelGui.Orange, TextAnchor.MiddleCenter);
        PixelGui.End();
    }
}
