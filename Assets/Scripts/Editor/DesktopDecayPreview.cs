using UnityEditor;
using UnityEngine;

public sealed class DesktopDecayPreview : EditorWindow
{
    PreviewRenderUtility _preview;
    GameObject _background;
    DesktopDecay _decay;
    Sprite _wallpaper;
    int _stage;
    bool _animate = true;
    float _proximity;
    float _previewTime;
    float _battleIntensity;
    float _gaugeProgress;
    float _waveProgress;

    [MenuItem("Tools/Team1/デスクトップ崩壊プレビュー")]
    static void Open() => GetWindow<DesktopDecayPreview>("デスクトップ崩壊");

    void OnEnable()
    {
        minSize = new Vector2(480f, 320f);
        _wallpaper = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Generated/Background/Bg_Desktop.png");
        _preview = new PreviewRenderUtility();
        _preview.camera.orthographic = true;
        _preview.camera.clearFlags = CameraClearFlags.SolidColor;
        _preview.camera.backgroundColor = new Color32(16, 13, 30, 255);
        _preview.camera.transform.position = new Vector3(0f, 0f, -10f);
        _preview.camera.nearClipPlane = 0.1f;
        _preview.camera.farClipPlane = 30f;
        Rebuild();
        EditorApplication.update += UpdatePreview;
    }

    void Rebuild()
    {
        if (_background != null) DestroyImmediate(_background);
        _decay = null;
        if (_wallpaper == null) return;
        _background = new GameObject("Desktop decay preview") { hideFlags = HideFlags.HideAndDontSave };
        var renderer = _background.AddComponent<SpriteRenderer>();
        renderer.sprite = _wallpaper;
        renderer.sortingOrder = -100;
        if (_stage > 0)
        {
            _decay = _background.AddComponent<DesktopDecay>();
            _decay.Initialize(renderer, _stage - 1, 3);
            _decay.SetProximity(_proximity);
            _decay.SetBattleIntensity(_battleIntensity);
            _decay.SetGaugeProgress(_gaugeProgress);
            _decay.SetWaveProgress(_waveProgress);
            _decay.Animate(_previewTime, true);
        }
        _preview.AddSingleGO(_background);
        Repaint();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("ステージ進行・ボスへの接近・解除ゲージに応じて、感染が深刻になります。", EditorStyles.wordWrappedLabel);
        int stage = GUILayout.Toolbar(_stage, new[] { "元の壁紙", "初期", "中盤", "終盤" });
        if (stage != _stage)
        {
            _stage = stage;
            Rebuild();
        }
        _animate = EditorGUILayout.ToggleLeft("感染ノイズを動かす", _animate);
        EditorGUI.BeginChangeCheck();
        _waveProgress = EditorGUILayout.Slider("雑魚の撃破率", _waveProgress, 0f, 1f);
        _proximity = EditorGUILayout.Slider("ボスへの接近（0:遠い / 1:近い）", _proximity, 0f, 1f);
        _battleIntensity = EditorGUILayout.Slider("ボス戦への移行", _battleIntensity, 0f, 1f);
        _gaugeProgress = EditorGUILayout.Slider("ボス解除ゲージ", _gaugeProgress, 0f, 1f);
        if (EditorGUI.EndChangeCheck() && _decay != null)
        {
            _decay.SetProximity(_proximity);
            _decay.SetBattleIntensity(_battleIntensity);
            _decay.SetGaugeProgress(_gaugeProgress);
            _decay.SetWaveProgress(_waveProgress);
            _decay.Animate(_previewTime, true);
            Repaint();
        }
        if (_wallpaper == null)
        {
            EditorGUILayout.HelpBox("背景画像 Bg_Desktop.png が見つかりません。", MessageType.Warning);
            return;
        }

        Rect view = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (Event.current.type != EventType.Repaint || view.width <= 0f || view.height <= 0f) return;
        Bounds bounds = _wallpaper.bounds;
        _preview.camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / (view.width / view.height)) * 1.03f;
        _preview.camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
        _preview.BeginPreview(view, GUIStyle.none);
        _preview.camera.Render();
        GUI.DrawTexture(view, _preview.EndPreview(), ScaleMode.StretchToFill, false);
    }

    void UpdatePreview()
    {
        if (!_animate || _decay == null) return;
        _previewTime = (float)EditorApplication.timeSinceStartup;
        _decay.Animate(_previewTime);
        Repaint();
    }

    void OnDisable()
    {
        EditorApplication.update -= UpdatePreview;
        if (_background != null) DestroyImmediate(_background);
        _preview?.Cleanup();
        _preview = null;
    }
}
