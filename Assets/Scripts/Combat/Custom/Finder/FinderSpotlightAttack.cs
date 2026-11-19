using System.Collections;
using UnityEngine;

/// <summary>
/// Spotlight・サーチ（Finder のボスの技。2026-11-18。docs/app-boss-skills-design.md 2-1）。数値はすべて【仮】。
/// 画面の上に検索バーが出て PLAYER と打つ。打っている間は水色の線と円がプレイヤーを追い、検索が終わると赤に変わってその場に止まる。
/// 0.5秒後、検索バーから止まった円へ光の帯を撃つ。赤になってから動けば当たらない。
/// 第2形態（追加の回数 2）：名前は打ち直さず、「水色で追う → 赤で止まる → 撃つ」をあと2回。
/// 場に出る物は FinderSpotlightEffect（自分で進む）。ダメージ・ノックバックは攻撃データの共通の欄
/// </summary>
[CreateAssetMenu(fileName = "Finder_Spotlight", menuName = "Team1/Custom/Finder Spotlight")]
public class FinderSpotlightAttack : CustomAttack
{
    [Header("流れ（秒）")]
    [Tooltip("検索バーが出るまで")]
    [SerializeField, Min(0f)] float _appearTime = 0.3f;
    [Tooltip("1文字打つ間隔")]
    [SerializeField, Min(0.01f)] float _typeInterval = 0.2f;
    [Tooltip("検索バーに打つ文字（英字。アカウント名は使わない：docs 0章）")]
    [SerializeField] string _query = "PLAYER";
    [Tooltip("検索完了（赤で止まる）から撃つまで")]
    [SerializeField, Min(0f)] float _lockTime = 0.5f;
    [Tooltip("光の帯が見えている時間")]
    [SerializeField, Min(0.01f)] float _fireTime = 0.3f;
    [Tooltip("光の帯の判定を出している時間")]
    [SerializeField, Min(0.01f)] float _hitWindow = 0.15f;
    [Tooltip("検索バーが引っ込むまで")]
    [SerializeField, Min(0f)] float _retractTime = 0.2f;

    [Header("第2形態")]
    [Tooltip("1発目のあとに、追加で撃つ回数（第2形態は 2）")]
    [SerializeField, Min(0)] int _extraShots;
    [Tooltip("追加の回の、水色で追う時間")]
    [SerializeField, Min(0f)] float _extraTrackTime = 0.4f;

    [Header("判定（マス）")]
    [Tooltip("光の帯の太さ（検索バーの下の端から円の中心まで）")]
    [SerializeField, Min(0.05f)] float _beamWidth = 0.8f;
    [Tooltip("足元の円の直径（下の端が足元）")]
    [SerializeField, Min(0.1f)] float _circleDiameter = 1.6f;

    [Header("検索バー（マス）")]
    [Tooltip("検索バーの大きさ")]
    [SerializeField] Vector2 _barSize = new Vector2(10f, 1.2f);
    [Tooltip("画面の上の端から、検索バーの上の端まで（左上の能力の枠・右上のゲージの下に出す）")]
    [SerializeField, Min(0f)] float _barTopMargin = 2.1f;

    public float AppearTime => _appearTime;
    public float TypeInterval => _typeInterval;
    public string Query => string.IsNullOrEmpty(_query) ? "PLAYER" : _query;
    public float LockTime => _lockTime;
    public float FireTime => _fireTime;
    public float HitWindow => _hitWindow;
    public float RetractTime => _retractTime;
    public int ExtraShots => _extraShots;
    public float ExtraTrackTime => _extraTrackTime;
    public float BeamWidth => _beamWidth;
    public float CircleDiameter => _circleDiameter;
    public Vector2 BarSize => _barSize;
    public float BarTopMargin => _barTopMargin;

    /// <summary>出してから消えるまでの秒数</summary>
    public float TotalTime =>
        _appearTime + Query.Length * _typeInterval + _lockTime + _fireTime
        + _extraShots * (_extraTrackTime + _lockTime + _fireTime) + _retractTime;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        FinderSpotlightEffect effect = FinderSpotlightEffect.Spawn(owner, data, this);
        if (effect == null) yield break;
        // 技の長さだけ待つ（場に出た物が消えるまで。止められたら、ここで止まる）
        float timeout = Time.time + TotalTime + 1f;
        while (effect != null && Time.time < timeout) yield return null;
    }
}
