using System.Collections;
using UnityEngine;

/// <summary>
/// スマートフォルダマグネット（Finder のボスの技。2026-11-18。docs/app-boss-skills-design.md 2-2）。数値はすべて【仮】。
/// 1. 構え：ボスの上にタグの丸。空中にトゲ付きファイルの出る場所（薄い枠）
/// 2. タグ付け：タグの弾（ダメージなし）を扇状に撃つ。同時にトゲ付きファイルが現れる（まだ動かない・当たらない）。
///    弾に当たると、プレイヤーの頭の上にその色のタグが付く（あとから当たった色に上書き。1色だけ）
/// 3. スマートフォルダが開く：タグが付いていれば、その色のトゲがふるえ、プレイヤーへ点線が伸びる。最後の 0.3秒で点線が止まる。
///    付いていなければ、トゲは「？」のように少しゆれるだけ
/// 4. 吸い寄せ：タグが付いていれば、その色のトゲが止まった位置へまっすぐ飛ぶ（壁・床で消える。当たるとライフ -1）
/// 5. 終わり：フォルダが閉じ、タグとトゲが消える
/// 第2形態（2色）：赤の弾 → 0.6秒後に青の弾。フォルダは赤 → 0.8秒後に青。付いている色のトゲだけが飛ぶ。
/// 場に出る物は FinderSmartFolderEffect（自分で進む）。ダメージ・ノックバックは攻撃データの共通の欄
/// </summary>
[CreateAssetMenu(fileName = "Finder_SmartFolder", menuName = "Team1/Custom/Finder Smart Folder")]
public class FinderSmartFolderAttack : CustomAttack
{
    [Header("色")]
    [Tooltip("オン：第2形態（赤 → 青の2色）。オフ：赤だけ")]
    [SerializeField] bool _twoColors;

    [Header("流れ（秒）")]
    [Tooltip("構え（タグの丸・出る場所の枠）")]
    [SerializeField, Min(0f)] float _windup = 0.8f;
    [Tooltip("2色のとき、赤の弾から青の弾まで")]
    [SerializeField, Min(0f)] float _volleyGap = 0.6f;
    [Tooltip("最後のタグの弾を撃ってから、フォルダが開くまで")]
    [SerializeField, Min(0f)] float _tagPhaseTime = 1.0f;
    [Tooltip("フォルダが開いてから、トゲが飛ぶまで（1色分）")]
    [SerializeField, Min(0.05f)] float _openTime = 0.8f;
    [Tooltip("飛ぶ前の、点線が止まっている時間")]
    [SerializeField, Min(0f)] float _lockBeforeFly = 0.3f;
    [Tooltip("2色のとき、赤のフォルダから青のフォルダまで")]
    [SerializeField, Min(0f)] float _colorGap = 0.8f;
    [Tooltip("トゲが飛んでいる時間の上限（壁・床に当たれば先に消える）")]
    [SerializeField, Min(0.05f)] float _flyTime = 0.8f;
    [Tooltip("フォルダが閉じるまで")]
    [SerializeField, Min(0f)] float _closeTime = 0.3f;

    [Header("タグの弾（ダメージなし）")]
    [SerializeField, Min(1)] int _bulletsPerVolley = 3;
    [Tooltip("扇の角度（度）")]
    [SerializeField, Range(0f, 180f)] float _bulletSpread = 30f;
    [Tooltip("速さ（マス/秒）")]
    [SerializeField, Min(0.1f)] float _bulletSpeed = 6f;
    [Tooltip("直径（マス）")]
    [SerializeField, Min(0.05f)] float _bulletDiameter = 0.5f;
    [Tooltip("寿命（秒）。部屋の外・壁・床でも消える")]
    [SerializeField, Min(0.1f)] float _bulletLifetime = 3f;

    [Header("トゲ付きファイル")]
    [Tooltip("1色あたりの数")]
    [SerializeField, Min(1)] int _spikesPerColor = 3;
    [Tooltip("大きさ（マス）。判定も同じ")]
    [SerializeField] Vector2 _spikeSize = new Vector2(0.8f, 0.8f);
    [Tooltip("出る高さ（部屋の床から、真ん中まで。マス）")]
    [SerializeField] Vector2 _spikeHeightRange = new Vector2(2.5f, 6f);
    [Tooltip("プレイヤーから離す距離（マス）")]
    [SerializeField, Min(0f)] float _spikeMinDistanceFromTarget = 3f;
    [Tooltip("トゲどうしを離す距離（マス）")]
    [SerializeField, Min(0f)] float _spikeMinSpacing = 3f;
    [Tooltip("飛ぶ速さ（マス/秒）。まっすぐ、曲がらない")]
    [SerializeField, Min(0.1f)] float _flySpeed = 10f;

    [Header("スマートフォルダ")]
    [Tooltip("大きさ（マス）")]
    [SerializeField] Vector2 _folderSize = new Vector2(2f, 1.6f);

    public bool TwoColors => _twoColors;
    public int ColorCount => _twoColors ? 2 : 1;
    public float Windup => _windup;
    public float VolleyGap => _volleyGap;
    public float TagPhaseTime => _tagPhaseTime;
    public float OpenTime => _openTime;
    public float LockBeforeFly => Mathf.Min(_lockBeforeFly, _openTime);
    public float ColorGap => _colorGap;
    public float FlyTime => _flyTime;
    public float CloseTime => _closeTime;
    public int BulletsPerVolley => _bulletsPerVolley;
    public float BulletSpread => _bulletSpread;
    public float BulletSpeed => _bulletSpeed;
    public float BulletDiameter => _bulletDiameter;
    public float BulletLifetime => _bulletLifetime;
    public int SpikesPerColor => _spikesPerColor;
    public Vector2 SpikeSize => _spikeSize;
    public Vector2 SpikeHeightRange => _spikeHeightRange;
    public float SpikeMinDistanceFromTarget => _spikeMinDistanceFromTarget;
    public float SpikeMinSpacing => _spikeMinSpacing;
    public float FlySpeed => _flySpeed;
    public Vector2 FolderSize => _folderSize;

    /// <summary>出してから消えるまでの秒数（目安。トゲが早く消えれば短くなる）</summary>
    public float TotalTime =>
        _windup + (ColorCount - 1) * _volleyGap + _tagPhaseTime
        + (ColorCount - 1) * _colorGap + _openTime + _flyTime + _closeTime;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        FinderSmartFolderEffect effect = FinderSmartFolderEffect.Spawn(owner, data, this);
        if (effect == null) yield break;
        float timeout = Time.time + TotalTime + 2f;
        while (effect != null && Time.time < timeout) yield return null;
    }
}
