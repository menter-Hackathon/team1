using System.Linq;
using UnityEngine;

/// <summary>
/// 【仮】リザルト画面：正常化したアプリと、手に入れた能力を並べる。UI 担当の本番画面ができたら消す。
/// ・アプリはドット絵のアイコンで出す。このランで初めて正常化した（図鑑に色が付いた）アプリは、きらきら光らせて「NEW」を付ける（2026-11-17）
/// ・「図鑑」でアプリ図鑑を開く。Enter キーか「タイトルへ」で戻る
/// </summary>
public class RunResultScreen : MonoBehaviour
{
    AppDexMenu _dex;
    GUIStyle _new;

    void Awake()
    {
        _dex = GetComponent<AppDexMenu>();
        if (_dex == null) _dex = gameObject.AddComponent<AppDexMenu>();
        _dex.ShowButton = false; // ボタンはこの画面に出す
    }

    void Update()
    {
        if (_dex != null && _dex.IsOpen) return;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.enterKey.wasPressedThisFrame && GameSession.Instance != null) GameSession.Instance.ReturnToTitle();
    }

    void OnGUI()
    {
        GameSession session = GameSession.Instance;
        if (session == null || session.IsTransitioning) return;
        PixelGui.Begin();
        if (_new == null)
        {
            _new = new GUIStyle(GUI.skin.label) { fontSize = 8, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _new.normal.textColor = Color.white;
        }

        int rows = session.Run != null ? session.Run.Cleared.Count : 0;
        float w = 360f;
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
                // アプリのアイコン（16×16 ドットを等倍で）。初めて正常化したアプリは光らせる
                StagePlan stage = record.Stage;
                var row = new AppUsageRow { Name = stage.App.DisplayName, BundleId = stage.App.BundleIdPrefixes?.FirstOrDefault(b => !string.IsNullOrEmpty(b)) ?? "", Seconds = 1 };
                Sprite appIcon = stage.Visual != null ? stage.Visual.Icon16 : AppVisuals.Get(row)?.Icon16;
                var iconRect = new Rect(x + 18f, cy + 2f, 16f, 16f);
                PixelGui.DrawSprite(iconRect, appIcon);
                bool isNew = session.Run.NewDex.Contains(AppDex.KeyOf(stage.App.DisplayName));
                if (isNew) DrawSparkle(iconRect);
                string ability = record.Reward != null ? record.Reward.DisplayName : "-";
                GUI.Label(new Rect(x + 42f, cy, w - 100f, 24f), $"{stage.DisplayName}　{stage.UsageHours:0.#} h　→　{ability}", PixelGui.Label);
                if (record.Reward != null) PixelGui.DrawSpriteAt(x + w - 40f, cy + 2f, record.Reward.Icon);
                if (isNew)
                {
                    var badge = new Rect(x + 6f, cy - 4f, 22f, 10f);
                    GUI.color = new Color32(0xc8, 0x24, 0x32, 0xff);
                    GUI.DrawTexture(badge, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(badge, "NEW", _new);
                }
                cy += 30f;
            }
            GUI.Label(new Rect(x + 20f, cy, w - 40f, 24f), $"リトライ {session.Run.RetryCount}", PixelGui.Label);
            cy += 30f;
        }
        bool dexOpen = _dex != null && _dex.IsOpen;
        if (!dexOpen && GUI.Button(new Rect(x + w * 0.08f, cy + 4f, w * 0.38f, 28f), "図鑑", PixelGui.Button)) _dex.Open();
        if (!dexOpen && GUI.Button(new Rect(x + w * 0.54f, cy + 4f, w * 0.38f, 28f), "タイトルへ", PixelGui.Button)) session.ReturnToTitle();
        PixelGui.End();
    }

    // きらきら（ゲームの絵のきらめきを、コマ送りでアイコンに重ねる）
    static void DrawSparkle(Rect icon)
    {
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.Sparkle : null;
        if (frames == null || frames.Length == 0) return;
        Sprite f = frames[Mathf.FloorToInt(Time.unscaledTime * 10f) % frames.Length];
        Rect r = f.textureRect;
        PixelGui.DrawSprite(new Rect(icon.center.x - r.width * 0.5f, icon.center.y - r.height * 0.5f, r.width, r.height), f);
    }
}
