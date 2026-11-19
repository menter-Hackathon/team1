using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// zip圧縮（Finder の能力。2026-11-18。docs/app-boss-skills-design.md 2-3）。数値はすべて【仮】。2回押しの能力。
/// ・1回目（圧縮）：正面の範囲（横3×縦2マス、足元から）にいる雑魚と敵の弾を 0.15秒の間吸いこみ、zip にする。
///   雑魚はその場で倒れる（倒した数に入る）。弾は消える。範囲に何もなくても空の zip ができる。クールタイムは始めない
/// ・2回目（発射）：2026-11-19（docs/app-feedback-v2-design.md 4-2）吸いこんだ数で4段にした。ここからクールタイム（攻撃データの間隔 3.5秒）
///   0個＝書類1発（ゲージ +4）／1個＝散弾4発・扇30°／2〜3個＝弾幕8発・10発（半分ずつ 0.12秒あけて2回。扇50°）／
///   4個以上＝解凍レーザー（正面へ画面の端まで。太さ 1.2・1.6・2.0。0.4秒。貫通。雑魚 3、ゲージ +24 を1回）
/// ・散弾・弾幕の1発は、攻撃データの共通の欄と弾の欄（雑魚 1・ゲージ +2）。1発・レーザーの数値は、この中の欄から、
///   攻撃データの写しを実行時に作って使う（アセットを足さない。古いアセットのままでも新しい値になる）
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
    [Tooltip("2026-11-19：数える上限（これより多く吸っても、強さは同じ）")]
    [SerializeField, Min(1)] int _countCap = 6;

    [Header("0個：書類1発（2026-11-19）")]
    [Tooltip("ボスのゲージ（1発）")]
    [SerializeField, Min(0)] int _singleGauge = 4;
    [Tooltip("雑魚へのダメージ（1発）")]
    [SerializeField, Min(0)] int _singleDamage = 1;

    [Header("1個：散弾（2026-11-19）")]
    [SerializeField, Min(1)] int _scatterCount = 4;
    [SerializeField, Range(0f, 180f)] float _scatterSpread = 30f;

    [Header("2〜3個：弾幕（2026-11-19）")]
    [Tooltip("2個のときの数")]
    [SerializeField, Min(1)] int _barrageCount2 = 8;
    [Tooltip("3個のときの数")]
    [SerializeField, Min(1)] int _barrageCount3 = 10;
    [SerializeField, Range(0f, 180f)] float _barrageSpread = 50f;
    [Tooltip("半分ずつ撃つ間（秒）")]
    [SerializeField, Min(0f)] float _barrageGap = 0.12f;

    [Header("4個以上：解凍レーザー（2026-11-19）")]
    [Tooltip("太さ（マス）：4個・5個・6個以上")]
    [SerializeField] float _laserWidth4 = 1.2f;
    [SerializeField] float _laserWidth5 = 1.6f;
    [SerializeField] float _laserWidth6 = 2f;
    [Tooltip("出ている時間（秒）")]
    [SerializeField, Min(0.05f)] float _laserTime = 0.4f;
    [Tooltip("雑魚へのダメージ（1体に1回）")]
    [SerializeField, Min(0)] int _laserDamage = 3;
    [Tooltip("ボスのゲージ（1回だけ。太さが変わっても同じ）")]
    [SerializeField, Min(0)] int _laserGauge = 24;

    public Vector2 RangeSize => _range;
    public float CompressTime => _compressTime;
    public int CountCap => Mathf.Max(1, _countCap);
    public int ScatterCount => _scatterCount;
    public float ScatterSpread => _scatterSpread;
    public float BarrageSpread => _barrageSpread;
    public float BarrageGap => _barrageGap;
    public float LaserTime => _laserTime;

    /// <summary>弾幕の数（2個 → 8、3個 → 10）</summary>
    public int BarrageCount(int count) => count >= 3 ? _barrageCount3 : _barrageCount2;

    /// <summary>レーザーの太さ（4個 → 1.2、5個 → 1.6、6個以上 → 2.0）</summary>
    public float LaserWidth(int count) => count >= 6 ? _laserWidth6 : count == 5 ? _laserWidth5 : _laserWidth4;

    // ───────── 攻撃データの写し（実行時。数値だけ変える） ─────────

    static readonly Dictionary<(AttackData, string), AttackData> Variants = new Dictionary<(AttackData, string), AttackData>();

    /// <summary>空の zip の1発の攻撃データ（ゲージ +4）</summary>
    public AttackData SingleData(AttackRunner owner, AttackData baseData) =>
        Variant(owner, baseData, "Single", $"{{\"_damage\":{_singleDamage},\"_gaugeGain\":{_singleGauge}}}");

    /// <summary>レーザーの攻撃データ（雑魚 3、ゲージ +24、弱いノックバック）</summary>
    public AttackData LaserData(AttackRunner owner, AttackData baseData) =>
        Variant(owner, baseData, "Laser", $"{{\"_damage\":{_laserDamage},\"_gaugeGain\":{_laserGauge},\"_knockback\":{{\"x\":4.0,\"y\":3.0}},\"_hitStun\":0.2}}");

    static AttackData Variant(AttackRunner owner, AttackData baseData, string key, string json)
    {
        if (baseData == null) return null;
        string fullKey = key + json;
        if (Variants.TryGetValue((baseData, fullKey), out AttackData v) && v != null) return v;
        v = Instantiate(baseData);
        v.name = $"{baseData.name}_{key}"; // 名前に App_Finder を残す（スキンの決め方）
        v.hideFlags = HideFlags.DontSave;
        try { JsonUtility.FromJsonOverwrite(json, v); }
        catch (System.Exception e) { Debug.LogWarning($"zip圧縮：攻撃データの写しを作れません（{e.Message}）"); }
        // 能力のスキンを、元の攻撃データと同じにする（同時に使える能力の見分け）
        SkinDresser.Look look = SkinDresser.LookOf(owner, baseData);
        if (look.Skin != SkinId.None) SkinDresser.Remember(v, look.Skin, look.Theme, look.Icon);
        Variants[(baseData, fullKey)] = v;
        return v;
    }

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

    /// <summary>zip を持っている間は、枠の絵を段ごとの zip にする。持っていなければいつものアイコン</summary>
    public override Sprite DisplayIcon(AttackRunner owner)
    {
        FinderZipHolder holder = HolderOf(owner);
        // 持っていないときは、能力のアイコン（ファスナー付きの書類）。持っている間は、段ごとの絵
        if (holder == null || holder.State == FinderZipHolder.ZipState.None) return FinderArt.ZipIcon(18);
        return FinderArt.ZipTierIcon(FinderArt.ZipTier(holder.Count));
    }

    // 枠を光らせる色は持ち主を受け取らないので、直前の IsCharged で見た段を覚えておく（PixelHud は IsCharged → ChargedColor の順に聞く）
    [System.NonSerialized] int _lastTier;

    /// <summary>zip を持っている間（もう一度押せる）は、枠のふちを光らせる</summary>
    public override bool IsCharged(AttackRunner owner)
    {
        FinderZipHolder holder = HolderOf(owner);
        bool charged = holder != null && holder.State != FinderZipHolder.ZipState.None;
        _lastTier = charged ? FinderArt.ZipTier(holder.Count) : 0;
        return charged;
    }

    /// <summary>4個以上（レーザー）は金色 #FFD60A、ほかは Finder の青 #0A84FF</summary>
    public override Color ChargedColor => _lastTier >= 3 ? new Color32(0xff, 0xd6, 0x0a, 0xff) : new Color32(0x0a, 0x84, 0xff, 0xff);
}
