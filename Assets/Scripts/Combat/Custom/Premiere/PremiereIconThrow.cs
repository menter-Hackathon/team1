using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// エフェクトアイコン投げ（Premiere Pro の能力。2026-11-18。docs/app-boss-skills-design.md 3-5）。数値は【仮】。
/// ・押すと、正面の斜め上（45°）へ、エフェクトアイコンを1つ投げる（放物線）。体の中ほどから
/// ・床・壁・相手に当たると、そのアイコンの効果が出る（炎＝爆発、雷＝真下の床から立つ落雷、ディゾルブ＝残るもや）
/// ・どのアイコンかは、3種類を1回ずつ混ぜた袋から順に出す（同じ確率。続けて10回投げれば、3種類とも必ず出る）
/// ・次に投げるアイコンは、HUD の枠に出す（DisplayIcon）。次のアイコンの選び方の状態は、投げる人（AttackRunner）ごとに持つ
/// 直撃のダメージ・ゲージは、この能力の攻撃データの共通の欄（ダメージ 1・ゲージ +2）。効果の数値は、効果ごとの攻撃データ
/// </summary>
public class PremiereIconThrow : CustomAttack
{
    [Header("投げ方（3-5）")]
    [Tooltip("速さ（マス/秒）")]
    [SerializeField, Min(0f)] float _speed = 12f;
    [Tooltip("重力（マス/秒²）")]
    [SerializeField] float _gravity = 18f;
    [Tooltip("正面の上へ傾ける角度（度）")]
    [SerializeField, Range(0f, 85f)] float _launchAngle = 45f;
    [Tooltip("判定の大きさ（マス）")]
    [SerializeField, Min(0.05f)] float _hitSize = 0.6f;
    [Tooltip("絵の大きさ（マス）。仕様は 12ドット")]
    [SerializeField, Min(0.05f)] float _visualSize = 12f / 18f;
    [Tooltip("投げる位置のずれ（x は向きに合わせて左右を反転）。足元が原点のプレイヤーが、体の中ほどから投げる")]
    [SerializeField] Vector2 _spawnOffset = new Vector2(0.3f, 0.8f);
    [Tooltip("寿命（秒）。何にも当たらなければ、効果を出さずに消える")]
    [SerializeField, Min(0.1f)] float _lifetime = 3f;
    [Tooltip("【仮】飛んでいる間に回る速さ（度/秒）")]
    [SerializeField] float _spin = 540f;

    [Header("効果（攻撃データ。Area・予告 0）")]
    [Tooltip("炎：爆発。円、直径 3.5（ダメージ 3・ゲージ +8）")]
    [SerializeField] AttackData _fire;
    [Tooltip("雷：当たった所の x に落雷。幅 1.5、高さ 12（ダメージ 3・ゲージ +8）")]
    [SerializeField] AttackData _thunder;
    [Tooltip("ディゾルブ：紫のもや。横3×縦3、1.5秒、0.5秒ごと（ダメージ 1・ゲージ +3 を3回）")]
    [SerializeField] AttackData _dissolve;

    // 投げる人ごとの「次に投げる順番」（先頭が次）。アセットには保存しない
    readonly Dictionary<AttackRunner, List<PremiereIcon>> _bags = new Dictionary<AttackRunner, List<PremiereIcon>>();

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        PremiereIcon kind = Peek(owner);
        Advance(owner);

        int facing = owner.Facing;
        Vector2 origin = owner.OriginPoint + new Vector2(facing * _spawnOffset.x, _spawnOffset.y);
        float rad = _launchAngle * Mathf.Deg2Rad;
        Vector2 velocity = new Vector2(facing * Mathf.Cos(rad), Mathf.Sin(rad)) * _speed;
        PremiereThrownIcon.Spawn(owner, data, kind, EffectData(kind), origin, velocity, _gravity, _hitSize, _visualSize, _lifetime, _spin * -facing);
        owner.NotifyReleased(data);
        yield break;
    }

    /// <summary>HUD の枠の絵＝次に投げるアイコン</summary>
    public override Sprite DisplayIcon(AttackRunner owner) => owner != null ? PremiereArt.Icon(Peek(owner)) : null;

    AttackData EffectData(PremiereIcon kind)
    {
        switch (kind)
        {
            case PremiereIcon.Fire: return _fire;
            case PremiereIcon.Thunder: return _thunder;
            default: return _dissolve;
        }
    }

    /// <summary>次に投げるアイコン（まだ袋が無ければ作る。何度呼んでも変わらない）</summary>
    PremiereIcon Peek(AttackRunner owner)
    {
        if (!_bags.TryGetValue(owner, out List<PremiereIcon> bag) || bag.Count == 0)
        {
            bag = NewBag();
            PruneDeadOwners();
            _bags[owner] = bag;
        }
        return bag[0];
    }

    /// <summary>次のアイコンへ進める（袋が空になったら、混ぜ直す）</summary>
    void Advance(AttackRunner owner)
    {
        if (!_bags.TryGetValue(owner, out List<PremiereIcon> bag) || bag.Count == 0) return;
        bag.RemoveAt(0);
        if (bag.Count == 0) _bags[owner] = NewBag();
    }

    static List<PremiereIcon> NewBag()
    {
        var bag = new List<PremiereIcon> { PremiereIcon.Fire, PremiereIcon.Thunder, PremiereIcon.Dissolve };
        for (int i = bag.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            PremiereIcon t = bag[i];
            bag[i] = bag[j];
            bag[j] = t;
        }
        return bag;
    }

    /// <summary>消えた投げる人（シーンが変わった前のプレイヤー）の分を捨てる</summary>
    void PruneDeadOwners()
    {
        List<AttackRunner> dead = null;
        foreach (var key in _bags.Keys)
        {
            if (key == null) (dead ?? (dead = new List<AttackRunner>())).Add(key);
        }
        if (dead == null) return;
        foreach (var key in dead) _bags.Remove(key);
    }
}
