using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】メールのボスの技「全員に返信」（2026-11-19。docs/app-kits-1-design.md 15章 P2）。Custom 小。
/// 場に残っている封筒の全部と、ボスから、プレイヤーへ赤い細い線（0.8秒）→ それぞれから紙飛行機が 1発ずつ（Re: の字が付く）。
/// 封筒は撃ったら消える。弾の数＝残っている封筒の数＋1。先に封筒を叩いておけば、弾が減る。
/// 封筒が 1通も無いときは、先に「迷惑メール」（spam）を出す。
/// ・弾の数値（速さ・大きさ）はこの技の攻撃データの「弾」の欄
/// </summary>
[CreateAssetMenu(fileName = "Mail_ReplyAll", menuName = "Team1/Kits/Mail Reply All")]
public class MailReplyAllAttack : CustomAttack
{
    [Tooltip("封筒が無いときに先に出す技（迷惑メール）")]
    [SerializeField] AttackData _spam;
    [Tooltip("線の予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("先に迷惑メールを出したあと、待つ秒数")]
    [SerializeField, Min(0f)] float _afterSpam = 0.6f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        if (MailMine.All().Count == 0 && _spam != null)
        {
            yield return owner.Execute(_spam);
            if (_afterSpam > 0f) yield return new WaitForSeconds(_afterSpam);
        }

        Vector2 target = owner.TargetPoint ?? owner.OriginPoint;
        var sources = new List<(Vector2 from, MailMine mine)>();
        var w = owner.GetComponent<IWarpable>();
        Vector2 bossFrom = w != null ? (Vector2)w.BodyBounds.center : owner.OriginPoint;
        sources.Add((bossFrom, null));
        foreach (MailMine m in MailMine.All()) if (m != null) sources.Add(((Vector2)m.transform.position + Vector2.up * 0.2f, m));

        var dirs = new List<Vector2>();
        foreach (var s in sources)
        {
            Vector2 d = target - s.from;
            Vector2 dir = d.sqrMagnitude > 0.01f ? d.normalized : Vector2.left;
            dirs.Add(dir);
            KitDress.Line(owner, s.from + dir * 0.4f, s.from + dir * 26f, _telegraph);
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_telegraph);

        for (int i = 0; i < sources.Count; i++)
        {
            var s = sources[i];
            if (s.mine != null) s.mine.Consume();
            else if (i > 0) continue; // 叩かれて消えた封筒からは撃たない
            Projectile p = Projectile.Spawn(owner, data, s.from, dirs[i], i);
            if (p == null) continue;
            KitDress.Swap(p.gameObject, MailArt.Plane);
            MailArt.Upright(p.gameObject, dirs[i]);
            MailArt.AddRe(p.transform, new Vector2(0f, 0.55f));
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(0.3f);
    }
}
