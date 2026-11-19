using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】共同編集のカーソル（Figma のボスの技1。2026-11-19。docs/app-kits-2-design.md 2-4）。
/// カーソルが部屋の上に入ってくる（0.4秒）→ 1つずつ、プレイヤーへ赤い線を引いて止まる（0.8秒。線は引いた時の位置で固定）
/// → 線の上を毎秒 14マスで突っ込み、床で止まって消える。
/// 第2形態：数 5。最後の Client は、突っ込んだ先で選択の枠（3×3）を広げる（赤い枠 0.8秒 → 判定）。
/// ダメージ・ノックバックは攻撃データの共通の欄
/// </summary>
[CreateAssetMenu(fileName = "Figma_Cursor", menuName = "Team1/Kits/Figma Cursor")]
public class FigmaCursorAttack : CustomAttack
{
    [Tooltip("カーソルの数（第1形態 3・第2形態 5）")]
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("入ってくる秒数")]
    [SerializeField, Min(0f)] float _enterTime = 0.4f;
    [Tooltip("赤い線の予告（0.8 以上）")]
    [SerializeField, Min(0.8f)] float _aimTime = 0.8f;
    [Tooltip("カーソルとカーソルの間（予告の始まりの間）")]
    [SerializeField, Min(0.1f)] float _interval = 0.9f;
    [Tooltip("突っ込む速さ（マス/秒）")]
    [SerializeField, Min(1f)] float _rushSpeed = 14f;
    [Tooltip("カーソルの判定（マス）")]
    [SerializeField] Vector2 _hitSize = new Vector2(0.8f, 0.8f);
    [Tooltip("オン：最後のカーソル（Client）が、突っ込んだ先で選択の枠を広げる")]
    [SerializeField] bool _lastFrame;
    [SerializeField] Vector2 _frameSize = new Vector2(3f, 3f);
    [SerializeField, Min(0.8f)] float _frameTelegraph = 0.8f;
    [SerializeField, Min(0.05f)] float _frameActive = 0.15f;

    public int Count => _count;
    public float EnterTime => _enterTime;
    public float AimTime => _aimTime;
    public float Interval => _interval;
    public float RushSpeed => _rushSpeed;
    public Vector2 HitSize => _hitSize;
    public bool LastFrame => _lastFrame;
    public Vector2 FrameSize => _frameSize;
    public float FrameTelegraph => _frameTelegraph;
    public float FrameActive => _frameActive;

    /// <summary>一番長くかかる秒数（待つ上限）</summary>
    public float MaxTime => _enterTime + (_count - 1) * _interval + _aimTime + 2.5f + (_lastFrame ? _frameTelegraph + _frameActive + 0.6f : 0f);

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        FigmaCursorEffect e = FigmaCursorEffect.Spawn(owner, data, this);
        if (e == null) yield break;
        float timeout = Time.time + MaxTime + 1f;
        while (e != null && Time.time < timeout) yield return null;
    }
}
