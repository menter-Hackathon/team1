using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Steam のボスの技1：SALE の値札（2026-11-19。docs/app-kits-2-design.md 2-3）。
/// 床に影（プレイヤーの近く ±5マス）→ 値札（-50%・-75%・-90%）が 0.25秒おきに落ちる。影から出れば当たらない。
/// ・値札の場所は 2.8マスおきの枠から選ぶ。まん中（プレイヤーの真上）は必ず入れ、となりの枠の片方は必ず空ける（逃げ場）
/// ・第2形態：6つ＋最後に幅 3マスの大きい -95%（影も大きい）。落ちると左右へ低い衝撃（ジャンプで越える）
/// ・落とすのは今の部品（FallingObject）。絵だけキットの値札に差し替える（KitDress.Swap）
/// </summary>
[CreateAssetMenu(fileName = "Steam_Sale", menuName = "Team1/Kits/Steam Sale")]
public class SteamSaleDrop : CustomAttack
{
    static readonly string[] Texts = { "-50%", "-75%", "-90%" };

    [Tooltip("値札の落下物（種類 Spawn。大きさ・落ちる速さ・残る秒数）")]
    [SerializeField] AttackData _tag;
    [Tooltip("大きい -95%（種類 Spawn）。空なら出さない")]
    [SerializeField] AttackData _bigTag;
    [Tooltip("大きい値札の低い衝撃（種類 Projectile）")]
    [SerializeField] AttackData _shock;
    [Tooltip("値札の数")]
    [SerializeField, Min(1)] int _count = 4;
    [Tooltip("枠の間（マス）")]
    [SerializeField, Min(0.5f)] float _slot = 2.8f;
    [Tooltip("影を出してから落ち始めるまで（秒。短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("1つずつ落とす間（秒）")]
    [SerializeField, Min(0f)] float _interval = 0.25f;
    [Tooltip("大きい値札の影の秒数")]
    [SerializeField, Min(0.8f)] float _bigTelegraph = 1.0f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _tag != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float px = owner.TargetPoint.HasValue ? owner.TargetPoint.Value.x : arena.center.x;
        List<float> xs = PickSlots(px, arena);
        var drops = new List<FallingObject>();
        float now = Time.time;
        for (int i = 0; i < xs.Count; i++)
        {
            FallingObject f = Drop(owner, _tag, xs[i], arena, now + i * _interval * 0.5f, now + _telegraph + i * _interval);
            if (f != null)
            {
                f.gameObject.AddComponent<SteamClearMark>(); // 50％の形態変化でも消える
                KitDress.Swap(f.gameObject, SteamArt.PriceTag(Texts[Random.Range(0, Texts.Length)]));
                drops.Add(f);
            }
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_telegraph + xs.Count * _interval + 0.4f);

        if (_bigTag == null) yield break;
        // 大きい -95%：今のプレイヤーの真上（影は大きい）
        px = owner.TargetPoint.HasValue ? owner.TargetPoint.Value.x : arena.center.x;
        float half = _bigTag.ObjectSize.x * 0.5f;
        px = Mathf.Clamp(px, arena.xMin + half + 0.2f, arena.xMax - half - 0.2f);
        now = Time.time;
        FallingObject big = Drop(owner, _bigTag, px, arena, now, now + _bigTelegraph);
        if (big == null) yield break;
        big.gameObject.AddComponent<SteamClearMark>();
        KitDress.Swap(big.gameObject, SteamArt.PriceTag("-95%", true));
        float timeout = Time.time + _bigTelegraph + 2f;
        while (big != null && !big.HasLanded && Time.time < timeout) yield return null;
        if (big == null || !big.HasLanded || _shock == null) yield break;
        // 左右へ低い衝撃
        Vector2 at = new Vector2(px, big.GroundY + _shock.HitSize.y * 0.5f);
        for (int d = -1; d <= 1; d += 2)
        {
            Projectile p = Projectile.Spawn(owner, _shock, at + new Vector2(d * half, 0f), new Vector2(d, 0f));
            if (p != null)
            {
                KitDress.Swap(p.gameObject, SteamArt.Shock);
                SpriteRenderer v = KitDress.FindVisual(p.transform);
                if (v != null && d < 0) v.transform.localRotation = Quaternion.Euler(180f, 0f, 0f); // 左向きでも上下を保つ
            }
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>値札の x（まん中は必ず。となりの片方は必ず空ける）</summary>
    List<float> PickSlots(float px, Rect arena)
    {
        float half = _tag.ObjectSize.x * 0.5f;
        float minX = arena.xMin + half + 0.2f, maxX = arena.xMax - half - 0.2f;
        int side = _count > 4 ? 3 : 2;
        int keepNeighbor = Random.value < 0.5f ? -1 : 1; // こちらのとなりに出し、反対のとなりを空ける
        var candidates = new List<int>();
        for (int k = -side; k <= side; k++)
        {
            if (k == 0 || k == -keepNeighbor) continue;
            candidates.Add(k);
        }
        var chosen = new List<int> { 0 };
        while (chosen.Count < _count && candidates.Count > 0)
        {
            int i = Random.Range(0, candidates.Count);
            chosen.Add(candidates[i]);
            candidates.RemoveAt(i);
        }
        // 落とす順はばらばら
        for (int i = chosen.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (chosen[i], chosen[j]) = (chosen[j], chosen[i]);
        }
        var xs = new List<float>();
        foreach (int k in chosen)
        {
            float x = Mathf.Clamp(px + k * _slot, minX, maxX);
            bool dup = false;
            foreach (float o in xs) if (Mathf.Abs(o - x) < half * 1.2f) dup = true;
            if (!dup) xs.Add(x);
        }
        return xs;
    }

    static FallingObject Drop(AttackRunner owner, AttackData d, float x, Rect arena, float appearAt, float fallAt)
    {
        if (d == null) return null;
        Vector2 hang = new Vector2(x, arena.yMax + d.ObjectSize.y * 0.5f);
        return FallingObject.Spawn(owner, d, hang, appearAt, fallAt, true);
    }
}

/// <summary>今の部品（落下物など）に付ける印。CustomEffect なので、ボスの形態変化（50％）で一緒に消える</summary>
public class SteamClearMark : CustomEffect { }
