using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】メッセージのボスの技「スラム」（2026-11-19。docs/app-kits-1-design.md 16章 P1-1）。
/// ボスの頭の上に「…」→ 大きい青い吹き出し（4×2.5マス）が、プレイヤーの位置に「ドン」と落ちる（影の予告 0.8秒、毎秒 14マス）
/// → 着地で、床の上を左右に走る波（高さ 0.8・毎秒 9マス。DropQuake）。画面が小さくゆれる。第2形態は 2連続。
/// 避け方：影から出て、波をジャンプで越える
/// </summary>
[CreateAssetMenu(fileName = "Messages_Slam", menuName = "Team1/Kits/Messages Slam")]
public class MessagesSlamAttack : CustomAttack
{
    [Tooltip("落ちる吹き出しの攻撃データ（種類＝落下物。大きさ・速さ・予告・ダメージ）。波も同じ数値で当たる")]
    [SerializeField] AttackData _fall;
    [SerializeField, Min(1)] int _count = 1;
    [Tooltip("次を落とすまで（秒）")]
    [SerializeField, Min(0f)] float _interval = 0.9f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _fall != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_fall == null) yield break;
        float tel = Mathf.Max(0.8f, _fall.TelegraphTime);
        MessagesTypingDots.Spawn(owner, tel + (_count - 1) * _interval);
        var list = new List<FallingObject>();
        for (int i = 0; i < _count; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_interval);
            float x = (owner.TargetPoint ?? owner.OriginPoint).x;
            FallingObject f = DropQuake.Drop(owner, _fall, x, tel, false, null, default, i);
            if (f == null) continue;
            KitDress.Swap(f.gameObject, MessagesArt.SlamBubble);
            DropQuake.Attach(f, owner, null, default, OnLanded);
            list.Add(f);
            owner.NotifyReleased(data);
        }
        float timeout = Time.time + 6f;
        while (Time.time < timeout && list.Exists(f => f != null && !f.HasLanded)) yield return null;
    }

    static void OnLanded(FallingObject f)
    {
        if (f == null) return;
        StageCamera.Shake(0.15f, 0.2f);
        Vector2 at = (Vector2)f.transform.position + Vector2.down * (f.Data != null ? f.Data.ObjectSize.y * 0.5f : 1f);
        FxKit.Text("Sent with Slam", at + Vector2.up * 0.5f, 0.4f, new Color(0.56f, 0.56f, 0.58f, 1f), 46, 0.8f).Rise(0.4f).FadeFrom(0.5f);
    }
}
