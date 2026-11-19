using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// メニュー（タイトル・能力えらび・図鑑・デスクトップ・リザルト・ゲームオーバー）の入力（2026-11-20）。キーボードとコントローラーをまとめて読む。
/// PlayerInputReader と同じく、割り当てはコードで書く（.inputactions アセットを使わない。マージで衝突しにくい）。
/// ・上下左右：矢印／WASD、十字キー／左スティック（スティックは倒し続けると、少し待ってから続けて動く）
/// ・決定：Z／Enter／テンキーの Enter（Space は画面ごとに選ぶ）、A（下のボタン）／START
/// ・もどる：Esc／Backspace、B（右のボタン）／SELECT（Back）
/// ・前の項目・次の項目（図鑑の絞り込みなど）：Q／E、L（LB）／R（RB）
/// キーボードのキーは、今までの各画面のキーと同じ。のちのキーコンフィグは、ここの割り当てを変える
/// </summary>
public static class MenuInput
{
    const float StickOn = 0.6f;        // これより倒したら、その向きに1つ動く
    const float StickOff = 0.35f;      // これより戻したら、倒していない扱い
    const float RepeatDelay = 0.45f;   // 倒し続けたとき、続けて動き始めるまでの秒数
    const float RepeatInterval = 0.14f; // 続けて動く間隔

    static InputAction _up, _down, _left, _right, _confirm, _confirmSpace, _back, _stick, _prevTab, _nextTab;
    static int _frame = -1;
    static Vector2Int _stickDir, _stickStep;
    static float _repeatAt;

    /// <summary>上（この画面のこのフレームで押された）</summary>
    public static bool Up => Pressed(ref _up) || Stick.y > 0;
    public static bool Down => Pressed(ref _down) || Stick.y < 0;
    public static bool Left => Pressed(ref _left) || Stick.x < 0;
    public static bool Right => Pressed(ref _right) || Stick.x > 0;

    /// <summary>決定。allowSpace：Space でも決めるか（リザルト・ゲームオーバーは Space で決めない）</summary>
    public static bool Confirm(bool allowSpace = true) => Pressed(ref _confirm) || (allowSpace && Pressed(ref _confirmSpace));

    /// <summary>もどる（閉じる）</summary>
    public static bool Back => Pressed(ref _back);

    /// <summary>前の項目（Q・L）。図鑑の絞り込みなど、タブのような物を切り替える</summary>
    public static bool PrevTab => Pressed(ref _prevTab);
    /// <summary>次の項目（E・R）</summary>
    public static bool NextTab => Pressed(ref _nextTab);

    // ドメインの再読み込みをしない設定でも、遊ぶたびに作り直す
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        foreach (var a in new[] { _up, _down, _left, _right, _confirm, _confirmSpace, _back, _stick, _prevTab, _nextTab }) a?.Dispose();
        _up = _down = _left = _right = _confirm = _confirmSpace = _back = _stick = _prevTab = _nextTab = null;
        _frame = -1;
        _stickDir = _stickStep = Vector2Int.zero;
    }

    static void Ensure()
    {
        if (_up != null) return;
        _up = Button("MenuUp", "<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up");
        _down = Button("MenuDown", "<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down");
        _left = Button("MenuLeft", "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left");
        _right = Button("MenuRight", "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right");
        _confirm = Button("MenuConfirm", "<Keyboard>/z", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth", "<Gamepad>/start");
        _confirmSpace = Button("MenuConfirmSpace", "<Keyboard>/space");
        _back = Button("MenuBack", "<Keyboard>/escape", "<Keyboard>/backspace", "<Gamepad>/buttonEast", "<Gamepad>/select");
        _prevTab = Button("MenuPrevTab", "<Keyboard>/q", "<Gamepad>/leftShoulder");
        _nextTab = Button("MenuNextTab", "<Keyboard>/e", "<Gamepad>/rightShoulder");
        _stick = new InputAction("MenuStick", InputActionType.Value, "<Gamepad>/leftStick", expectedControlType: "Vector2");
        _stick.Enable();
    }

    static InputAction Button(string name, params string[] paths)
    {
        var a = new InputAction(name, InputActionType.Button);
        foreach (string p in paths) a.AddBinding(p);
        a.Enable();
        return a;
    }

    // ref で受け取る：最初の1回は Ensure で作ってから読む（値で受け取ると、作る前の null を読んでしまう）
    static bool Pressed(ref InputAction a)
    {
        Ensure();
        return a != null && a.WasPressedThisFrame();
    }

    /// <summary>このフレームでスティックが動かした向き（倒した瞬間と、倒し続けたときの続けての1歩。無ければ 0）</summary>
    static Vector2Int Stick
    {
        get
        {
            Ensure();
            if (_frame == Time.frameCount) return _stickStep;
            _frame = Time.frameCount;
            Vector2 v = _stick.ReadValue<Vector2>();
            Vector2Int dir = _stickDir;
            // いちばん大きく倒した軸の向き。戻したら（StickOff より小さい）向きなし
            if (Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y)) < StickOff) dir = Vector2Int.zero;
            else if (Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y)) >= StickOn)
                dir = Mathf.Abs(v.x) >= Mathf.Abs(v.y) ? new Vector2Int(v.x > 0 ? 1 : -1, 0) : new Vector2Int(0, v.y > 0 ? 1 : -1);

            float now = Time.unscaledTime;
            _stickStep = Vector2Int.zero;
            if (dir != _stickDir)
            {
                if (dir != Vector2Int.zero) { _stickStep = dir; _repeatAt = now + RepeatDelay; }
            }
            else if (dir != Vector2Int.zero && now >= _repeatAt)
            {
                _stickStep = dir;
                _repeatAt = now + RepeatInterval;
            }
            _stickDir = dir;
            return _stickStep;
        }
    }
}
