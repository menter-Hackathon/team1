using System.Collections;
using UnityEngine;

/// <summary>
/// zip圧縮（Finder の能力。2026-11-18。docs/app-boss-skills-design.md 2-3）。数値はすべて【仮】。2回押しの能力。
/// ・1回目（圧縮）：正面の範囲（横3×縦2マス、足元から）にいる雑魚と敵の弾を 0.15秒の間吸いこみ、zip にする。
///   雑魚はその場で倒れる（倒した数に入る）。弾は消える。範囲に何もなくても空の zip ができる。クールタイムは始めない
/// ・2回目（発射）：zip を正面へ散弾として撃つ。弾の数＝3＋吸いこんだ数（最大8）。ここからクールタイム（攻撃データの間隔 3.5秒）
/// ・散弾1発の速さ・寿命・大きさ・ダメージ・ゲージは、攻撃データの共通の欄と弾の欄（Projectile.Spawn がそのまま読む）
/// ・zip の状態は、プレイヤーに実行時に足す部品（FinderZipHolder）が持つ。この能力が枠から外れたら、zip ごと消える
/// </summary>
[CreateAssetMenu(fileName = "Finder_Zip", menuName = "Team1/Custom/Finder Zip")]
public class FinderZipAttack : CustomAttack
{
    [Header("圧縮")]
    [Tooltip("吸いこむ範囲（マス）。体の前の端から横、足元から縦")]
    [SerializeField] Vector2 _range = new Vector2(3f, 2f);
    [Tooltip("吸いこむ時間（秒）。この間に範囲に入った物も吸う")]
    [SerializeField, Min(0.01f)] float _compressTime = 0.15f;

    [Header("発射（散弾）")]
    [Tooltip("空の zip の弾の数")]
    [SerializeField, Min(1)] int _basePellets = 3;
    [Tooltip("弾の数の最大")]
    [SerializeField, Min(1)] int _maxPellets = 8;
    [Tooltip("扇の角度（度）")]
    [SerializeField, Range(0f, 180f)] float _spread = 40f;

    public Vector2 RangeSize => _range;
    public float CompressTime => _compressTime;
    public int BasePellets => _basePellets;
    public int MaxPellets => Mathf.Max(_basePellets, _maxPellets);
    public float Spread => _spread;

    /// <summary>吸いこんだ数から、撃つ弾の数</summary>
    public int PelletCount(int absorbed) => Mathf.Min(MaxPellets, _basePellets + Mathf.Max(0, absorbed));

    static FinderZipHolder HolderOf(AttackRunner owner) => owner != null ? owner.GetComponent<FinderZipHolder>() : null;

    /// <summary>吸いこんでいる途中（0.15秒）は押しても何もしない</summary>
    public override bool CanRun(AttackRunner owner, AttackData data)
    {
        FinderZipHolder holder = HolderOf(owner);
        return holder == null || holder.State != FinderZipHolder.ZipState.Compressing;
    }

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        // 状態は最初の yield より前に変える（PlayerSkillExecutor が Run の直後に DefersCooldown を聞く）
        FinderZipHolder holder = FinderZipHolder.GetOrAdd(owner, this);
        if (holder.State == FinderZipHolder.ZipState.Holding) holder.Fire(data);
        else holder.BeginCompress();
        owner.NotifyReleased(data);
        yield break;
    }

    /// <summary>
    /// 圧縮したあとはクールタイムを始めない。発射したときだけ始める。
    /// （Run がまだ進んでいないときも同じ答えになるように：このフレームに撃った → 始める、zip を持っている → これから撃つので始める、それ以外 → 圧縮なので始めない）
    /// </summary>
    public override bool DefersCooldown(AttackRunner owner)
    {
        FinderZipHolder holder = HolderOf(owner);
        if (holder == null) return true;
        if (holder.LastFireFrame == Time.frameCount) return false;
        return holder.State != FinderZipHolder.ZipState.Holding;
    }

    /// <summary>zip を持っている間は、枠の絵を zip（大きさ3段階）にする。持っていなければいつものアイコン</summary>
    public override Sprite DisplayIcon(AttackRunner owner)
    {
        FinderZipHolder holder = HolderOf(owner);
        // 持っていないときは、能力のアイコン（ファスナー付きの書類、いちばん大きい絵）。持っている間は、吸った数で大きさが変わる
        if (holder == null || holder.State == FinderZipHolder.ZipState.None) return FinderArt.ZipIcon(18);
        return FinderArt.ZipIcon(FinderArt.ZipHeight(holder.Count));
    }

    /// <summary>zip を持っている間（もう一度押せる）は、枠のふちを Finder の青で光らせる</summary>
    public override bool IsCharged(AttackRunner owner)
    {
        FinderZipHolder holder = HolderOf(owner);
        return holder != null && holder.State != FinderZipHolder.ZipState.None;
    }
}
