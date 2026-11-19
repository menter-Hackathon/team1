using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】落下＋左右の波の技（2026-11-19。docs/app-kits-1-design.md E6 の DropQuake）。担当4 の物。ほかのキットも使ってよい。
/// プレイヤーの位置に、落下物を「間隔」ごとに「数」だけ落とす（影の予告）。着地すると、床の上を左右に波が走る（ジャンプで越える）。
/// ・「波を出す」をオフにすると、ただの「プレイヤーの位置に N個、間隔をあけて落とす」技になる（データの落下物には、くり返しが無いため）
/// ・落下物の大きさ・落ちる速さ・予告の秒数・着地後に残る秒数・足場になるかは、「落下物」の攻撃データの欄
/// ・絵：落下物は「落下物の絵の名前」（KitDress.RegisterArt。index＝何個目か）。空なら落下物の攻撃データのまま（枠の語があればスキンの絵）
/// ・作り方：KitAssetBuilder.Custom&lt;DropQuakeAttack&gt;(...) で中身を作り、CustomData(...) で攻撃データにする
/// </summary>
[CreateAssetMenu(fileName = "DropQuake_", menuName = "Team1/Kits/Drop Quake")]
public class DropQuakeAttack : CustomAttack
{
    [Tooltip("落下物の攻撃データ（種類 Spawn。大きさ・落ちる速さ・予告・残る秒数・足場は、ここの欄）")]
    [SerializeField] AttackData _fall;
    [Tooltip("波の攻撃データ（ダメージ・ノックバック）。空なら落下物の数値")]
    [SerializeField] AttackData _wave;
    [Tooltip("落とす数")]
    [SerializeField, Min(1)] int _count = 1;
    [Tooltip("落とす間隔（秒）")]
    [SerializeField, Min(0f)] float _interval = 0.5f;
    [Tooltip("オン：落とすたびに、プレイヤーの今の位置を狙う。オフ：2つ目からは、プレイヤーから離れたランダムな位置")]
    [SerializeField] bool _aimEach = true;
    [Tooltip("オン：着地したら左右に波を出す")]
    [SerializeField] bool _waves = true;
    [SerializeField, Min(0.1f)] float _waveSpeed = DropQuake.DefaultSpeed;
    [SerializeField, Min(0.1f)] float _waveHeight = DropQuake.DefaultHeight;
    [SerializeField, Min(0.1f)] float _waveWidth = DropQuake.DefaultWidth;
    [Tooltip("落下物の絵の名前（KitDress.RegisterArt）。空なら攻撃データの絵")]
    [SerializeField] string _fallArt;
    [Tooltip("波の絵の名前（KitDress.RegisterArt）。空なら既定の山")]
    [SerializeField] string _waveArt;

    public AttackData Fall => _fall;

    public override bool CanRun(AttackRunner owner, AttackData data) => _fall != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_fall == null) yield break;
        var settings = new DropQuake.Settings
        {
            Speed = _waveSpeed,
            Height = _waveHeight,
            Width = _waveWidth,
            Sprite = !string.IsNullOrEmpty(_waveArt) ? KitDress.Art(_waveArt) : null,
        };
        var objects = new List<FallingObject>();
        Rect area = WallShot.AreaFor(owner);
        float firstX = owner.TargetPoint?.x ?? owner.OriginPoint.x;
        for (int i = 0; i < _count; i++)
        {
            if (i > 0 && _interval > 0f) yield return new WaitForSeconds(_interval);
            float x = owner.TargetPoint?.x ?? owner.OriginPoint.x;
            if (!_aimEach && i > 0) x = AwayFrom(firstX, area, _fall.ObjectSize.x);
            FallingObject f = DropQuake.Drop(owner, _fall, x, _fall.TelegraphTime, _waves, _wave, settings, i);
            if (f == null) continue;
            if (!string.IsNullOrEmpty(_fallArt))
            {
                Sprite s = KitDress.Art(_fallArt, i);
                if (s != null) KitDress.Swap(f.gameObject, s);
            }
            objects.Add(f);
            owner.NotifyReleased(data);
        }
        // 全部が着地する（または消える）まで待つ
        float timeout = Time.time + 10f;
        while (Time.time < timeout && objects.Exists(o => o != null && !o.HasLanded)) yield return null;
    }

    static float AwayFrom(float x, Rect area, float width)
    {
        float half = width * 0.5f;
        for (int i = 0; i < 20; i++)
        {
            float c = Random.Range(area.xMin + half, area.xMax - half);
            if (Mathf.Abs(c - x) >= width + 1f) return c;
        }
        return x + (x < area.center.x ? width + 1f : -(width + 1f));
    }
}
