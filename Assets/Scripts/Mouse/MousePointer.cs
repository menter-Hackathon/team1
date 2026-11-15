using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【仮】マウス操作（盾ウィンドウ・タブを閉じる）で共通に使う、マウスの読み取り。2026-11-16 にまさぴ〜が選んだ形。チームの合意はまだ。
/// ・マウスは「あると楽になる追加の操作」。マウスが無くても（ゲームパッド・キーボードだけでも）クリアできるようにしておくこと
/// ・トラックパッドのクリックも、左ボタンとして届く
/// ・同じフレームの1回のクリックを、2つの物が取り合わないようにする（先に Consume した方が使う）
/// </summary>
public static class MousePointer
{
    static int _consumedFrame = -1;

    /// <summary>左ボタンがこのフレームに押され、まだ誰も使っていないか</summary>
    public static bool PressedThisFrame
    {
        get
        {
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame && _consumedFrame != Time.frameCount;
        }
    }

    /// <summary>左ボタンを押し続けているか</summary>
    public static bool Held
    {
        get
        {
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }
    }

    /// <summary>このフレームのクリックを使ったことにする（ほかの物には届かない）</summary>
    public static void Consume() => _consumedFrame = Time.frameCount;

    /// <summary>マウスの位置（ワールド座標）。マウスかメインカメラが無ければ false</summary>
    public static bool TryGetWorldPosition(out Vector2 world)
    {
        world = default;
        Mouse mouse = Mouse.current;
        Camera cam = Camera.main;
        if (mouse == null || cam == null) return false;
        Vector2 screen = mouse.position.ReadValue();
        Vector3 p = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));
        world = p;
        return true;
    }

    /// <summary>ゲーム中もマウスカーソルを見えるようにしておく（マウスで操作する物があるシーンで呼ぶ）</summary>
    public static void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
