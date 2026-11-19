using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】メッセージのボスの技「花火」（2026-11-19。docs/app-kits-1-design.md 16章 P1-2）。
/// プレイヤーの体の位置と、その左右 3.5マス（第2形態はさらに外の 7マスも）に赤い輪（0.8秒）→ 直径 3 の花火が 0.3秒ずつ遅れて開く（0.4秒ずつ）。
/// プレイヤーがジャンプ中・足場の上なら、その高さに出る（床に付けない）。
/// 避け方：輪と輪の間は狭い（すき間 0.5）ので、上か下へ出る。または横へ走り抜ける
/// </summary>
[CreateAssetMenu(fileName = "Messages_Fireworks", menuName = "Team1/Kits/Messages Fireworks")]
public class MessagesFireworksAttack : CustomAttack
{
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("となりの花火までの距離（マス）")]
    [SerializeField, Min(0.5f)] float _spacing = 3.5f;
    [SerializeField, Min(0.5f)] float _diameter = 3f;
    [Tooltip("予告（秒。短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("1つずつ遅らせる秒数")]
    [SerializeField, Min(0f)] float _stagger = 0.3f;
    [Tooltip("花火の判定（秒）")]
    [SerializeField, Min(0.05f)] float _active = 0.4f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 c = owner.TargetPoint ?? arena.center;
        float r = _diameter * 0.5f;
        int placed = 0;
        for (int i = 0; i < _count; i++)
        {
            // まん中 → 左 → 右 → さらに左 → さらに右
            int step = (i + 1) / 2;
            float sign = i % 2 == 1 ? -1f : 1f;
            Vector2 p = c + new Vector2(sign * step * _spacing, 0f);
            if (p.x < arena.xMin + r * 0.5f || p.x > arena.xMax - r * 0.5f) continue;
            Color col = (Color)MessagesArt.FireworkColors[i % MessagesArt.FireworkColors.Length];
            TalkRing.Spawn(owner, data, p, _diameter, placed * _stagger, _telegraph, _active, TalkBurst.Firework, col);
            placed++;
        }
        owner.NotifyReleased(data);
        float total = Mathf.Max(0, placed - 1) * _stagger + _telegraph + _active;
        yield return new WaitForSeconds(total);
    }
}
