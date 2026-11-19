using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Notion のボスの技「ブロックを積む」（2026-11-19。docs/app-kits-1-design.md 8章 P1-1。担当0）。
/// ボスの横に「/」→ 白いブロック（横 4×縦 1。左に「⋮⋮」）が、プレイヤーの位置に 0.5秒おきに N個落ちる（影の予告 0.8秒）→
/// 着地後 4秒残って、足場になる。落下物の数値は中の攻撃データ（種類＝落下物）。今の部品では「プレイヤーの位置に、時間をあけて N個」が
/// 作れないので、ここで1個ずつ FallingObject.Spawn を呼ぶ（Custom 小）
/// </summary>
[CreateAssetMenu(fileName = "NotionBlockStack", menuName = "Team1/Kits/Notion/Block Stack")]
public class NotionBlockStack : CustomAttack
{
    [Tooltip("ブロック1個の攻撃データ（種類＝落下物。大きさ・落ちる速さ・着地後に残る秒数・足場になるか・予告の秒数）")]
    [SerializeField] AttackData _block;
    [Tooltip("落とす数")]
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("1個ずつの間（秒）")]
    [SerializeField, Min(0f)] float _interval = 0.5f;
    [Tooltip("ブロックの絵の名前（KitDress）")]
    [SerializeField] string _art = NotionKit.ArtBlock;

    public override bool CanRun(AttackRunner owner, AttackData data) => _block != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_block == null) yield break;
        // ボスの横に「/」（スラッシュのメニュー。飾り）
        Vector2 slash = owner.OriginPoint + new Vector2(owner.Facing * 1.2f, 1.2f);
        FxKit.Text("/", slash, 0.8f, NotionArt.Black, 30, 0.6f, NotionArt.White);

        var objects = new List<FallingObject>();
        Rect arena = BattleArena.GetBounds();
        Vector2 size = _block.ObjectSize;
        float half = size.x * 0.5f;
        for (int i = 0; i < _count; i++)
        {
            if (i > 0 && _interval > 0f) yield return new WaitForSeconds(_interval);
            float x = Mathf.Clamp((owner.TargetPoint ?? owner.OriginPoint).x, arena.xMin + half, arena.xMax - half);
            float hangY = arena.yMax + size.y * 0.5f; // 画面の上の端のすぐ外から
            float fallAt = Time.time + _block.TelegraphTime;
            FallingObject f = FallingObject.Spawn(owner, _block, new Vector2(x, hangY), fallAt, fallAt, true, false, i);
            if (f != null)
            {
                KitDress.Swap(f.gameObject, KitDress.Art(_art, i));
                objects.Add(f);
            }
            owner.NotifyReleased(data);
        }
        // 全部が着地する（または消える）まで待つ
        float timeout = Time.time + 10f;
        while (Time.time < timeout && objects.Exists(o => o != null && !o.HasLanded)) yield return null;
    }
}
