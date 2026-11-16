using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerController の状態を Animator のパラメータに流す橋渡し。
/// Animator Controller 側に次の名前のパラメータを作れば自動で反映される（無いものは無視する）。
///   SpeedX     (Float) : 横方向の速さ（絶対値）
///   VelocityY  (Float) : 縦方向の速度（上昇中は＋、落下中は−）
///   IsGrounded (Bool)  : 接地しているか
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimationBridge : MonoBehaviour
{
    [SerializeField] Animator _animator;

    static readonly int SpeedXHash = Animator.StringToHash("SpeedX");
    static readonly int VelocityYHash = Animator.StringToHash("VelocityY");
    static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");

    PlayerController _controller;
    readonly HashSet<int> _params = new HashSet<int>();
    RuntimeAnimatorController _cachedController;

    void Awake()
    {
        _controller = GetComponent<PlayerController>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
    }

    void LateUpdate()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return;

        // Animator Controller が差し替えられたらパラメータ一覧を取り直す
        if (_cachedController != _animator.runtimeAnimatorController)
        {
            _cachedController = _animator.runtimeAnimatorController;
            _params.Clear();
            foreach (AnimatorControllerParameter p in _animator.parameters) _params.Add(p.nameHash);
        }

        Vector2 v = _controller.Velocity;
        if (_params.Contains(SpeedXHash)) _animator.SetFloat(SpeedXHash, Mathf.Abs(v.x));
        if (_params.Contains(VelocityYHash)) _animator.SetFloat(VelocityYHash, v.y);
        if (_params.Contains(IsGroundedHash)) _animator.SetBool(IsGroundedHash, _controller.IsGrounded);
    }
}
