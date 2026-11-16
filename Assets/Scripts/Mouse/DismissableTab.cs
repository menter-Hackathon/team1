using System;
using UnityEngine;

/// <summary>
/// 【仮】クリックで消せるタブ。Chrome の第2形態「タブ増殖」（置き方 TopRow）のタブに、AttackRunner が付ける。
/// 2026-11-16 にまさぴ〜が選んだ形。チームの合意はまだ。
/// ・画面の上端に並んでから、落ち始めるまでの間だけ、マウスの左クリックで消せる（落ち始めたら消せない）
/// ・技の数値・流れ・間隔は変えない。消したタブは落ちてこないだけで、残りのタブは予定どおりに落ちる
/// ・マウスの下にある間は明るくして、消せることを見せる
/// マウスが無くてもクリアできること（マウスは、あると楽になる追加の操作）。
/// </summary>
[RequireComponent(typeof(FallingObject))]
public class DismissableTab : MonoBehaviour
{
    [Tooltip("【仮】クリックを受ける範囲を、タブの大きさより広げる量（マス）。小さいタブでも押しやすくする")]
    [SerializeField, Min(0f)] float _clickPadding = 0.2f;
    [Tooltip("マウスの下にあるときの色")]
    [SerializeField] Color _hoverColor = Color.white;

    FallingObject _falling;
    Color _baseColor;
    bool _hovered;

    /// <summary>クリックで消したとき（SE・エフェクト用）。消える直前に呼ばれる</summary>
    public static event Action<DismissableTab> Dismissed;

    /// <summary>今クリックすれば消せるか（見えていて、まだ落ち始めていない）</summary>
    public bool CanDismiss => _falling != null && _falling.IsVisible && !_falling.IsFalling && !_falling.HasLanded;

    void Awake()
    {
        _falling = GetComponent<FallingObject>();
        if (_falling.Visual != null) _baseColor = _falling.Visual.color;
        MousePointer.ShowCursor();
    }

    void Update()
    {
        bool over = CanDismiss && MousePointer.TryGetWorldPosition(out Vector2 m) && ContainsPoint(m);
        if (over && MousePointer.PressedThisFrame)
        {
            MousePointer.Consume(); // 重なっている盾ウィンドウはつかまない
            TryDismiss();
            return;
        }
        SetHovered(over);
    }

    /// <summary>その点がタブの上か（クリックを受ける範囲）</summary>
    public bool ContainsPoint(Vector2 world)
    {
        Vector2 half = _falling.Data.ObjectSize * 0.5f + Vector2.one * _clickPadding;
        Vector2 c = transform.position;
        return Mathf.Abs(world.x - c.x) <= half.x && Mathf.Abs(world.y - c.y) <= half.y;
    }

    /// <summary>消す。落ち始めたあとなど、消せないときは false</summary>
    public bool TryDismiss()
    {
        if (!CanDismiss) return false;
        Dismissed?.Invoke(this);
        Destroy(gameObject);
        return true;
    }

    void SetHovered(bool on)
    {
        if (_hovered == on || _falling.Visual == null) return;
        _hovered = on;
        _falling.Visual.color = on ? Color.Lerp(_baseColor, _hoverColor, 0.6f) : _baseColor;
    }
}
