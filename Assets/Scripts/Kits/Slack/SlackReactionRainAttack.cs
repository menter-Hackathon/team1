using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Slack のボスの技「リアクションの雨」（2026-11-19。docs/app-kits-1-design.md 2章 P1-2）。
/// 画面の上にボスのメッセージ（灰色の吹き出し）→ その下に絵文字の札が左から 0.15秒おきに付く（数字が 1 → 2 と増える。飾り）
/// → 0.6秒待つ → 左から 0.25秒おきに落ちる。落ちる所の床に、影と赤い「！」（並んだ瞬間から）。
/// 落下物は FallingObject（影つき）。絵は SlackArt の札に差し替える。ダメージは落下物の攻撃データ
/// </summary>
[CreateAssetMenu(fileName = "Slack_ReactionRain", menuName = "Team1/Kits/Slack Reaction Rain")]
public class SlackReactionRainAttack : CustomAttack
{
    [Tooltip("落ちる札の攻撃データ（種類＝落下物。大きさ・落ちる速さ・ダメージ）。名前に枠の語を入れない")]
    [SerializeField] AttackData _drop;
    [SerializeField, Min(1)] int _count = 5;
    [Tooltip("札の間隔（マス。札の幅 1.5＋すき間 1.5）")]
    [SerializeField, Min(0.5f)] float _spacing = 3f;
    [Tooltip("札が付く間隔（秒）")]
    [SerializeField, Min(0f)] float _placeInterval = 0.15f;
    [Tooltip("並べ終えてから落とし始めるまで（秒）")]
    [SerializeField, Min(0f)] float _hold = 0.6f;
    [Tooltip("落とす間隔（秒）")]
    [SerializeField, Min(0f)] float _dropInterval = 0.25f;
    [Tooltip("ボスのメッセージの幅（マス）")]
    [SerializeField, Min(2f)] float _messageWidth = 14f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _drop != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_drop == null) yield break;
        Rect arena = BattleArena.GetBounds();
        Rect view = ScreenUi.ViewRect();
        float top = Mathf.Min(arena.yMax, view.yMax) - 1.6f; // 左上の能力の枠・右上のゲージの下
        float msgH = 1.3f;
        Vector2 msgCenter = new Vector2(arena.center.x, top - msgH * 0.5f);
        float now = Time.time;
        float lastPlace = now + (_count - 1) * _placeInterval;
        float total = lastPlace + _hold + (_count - 1) * _dropInterval - now;

        // ボスのメッセージ（飾り。CustomEffect で、形態変化・正常化で消える）
        SlackMessageBar.Spawn(owner, msgCenter, new Vector2(Mathf.Min(_messageWidth, arena.width - 1f), msgH), total + 0.6f);

        Vector2 size = _drop.ObjectSize;
        float hangY = msgCenter.y - msgH * 0.5f - 0.15f - size.y * 0.5f;
        float width = (_count - 1) * _spacing;
        float startX = arena.center.x - width * 0.5f;
        var drops = new List<FallingObject>();
        for (int i = 0; i < _count; i++)
        {
            float appearAt = now + i * _placeInterval;
            float fallAt = lastPlace + _hold + i * _dropInterval;
            FallingObject f = FallingObject.Spawn(owner, _drop, new Vector2(startX + i * _spacing, hangY), appearAt, fallAt, true, false, i);
            if (f == null) continue;
            KitDress.Swap(f.gameObject, SlackArt.EmojiTag(i));
            SlackTagDecor.Attach(f, appearAt);
            drops.Add(f);
        }
        owner.NotifyReleased(data);

        float timeout = Time.time + total + 4f;
        while (Time.time < timeout && drops.Exists(d => d != null && !d.HasLanded)) yield return null;
    }
}
