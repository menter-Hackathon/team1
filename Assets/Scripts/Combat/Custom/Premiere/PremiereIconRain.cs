using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// エフェクトアイコンの雨（Premiere Pro のボスの技。2026-11-18。docs/app-boss-skills-design.md 3-1）。数値は【仮】。
/// 流れ：画面の上に、アイコン（炎・雷を交互）を左から1つずつ並べる（真下の床に影）→ 少し待つ → 左から順に真下へ落とす → 床・足場に着いたら消える。
/// 第2形態は、炎のアイコンが着地した所に火（残るダメージ床）を出す。
///
/// 落とす物は今ある落下物（FallingObject。Chrome のタブ増殖と同じ部品）。大きさ・落下速度・ダメージは、この技の攻撃データの
/// 「落下物」の欄を使う（落下物の置き方の欄は使わない。クリックで消せるタブにはしない）。絵は出したあとでアイコンに差し替える。
/// 落下物は専用の技の部品ではないので、形態変化（50%）では消えずに落ちきる（タブ増殖と同じ）
/// </summary>
public class PremiereIconRain : CustomAttack
{
    [Header("並べ方（3-1）")]
    [Tooltip("並べる数（第1形態 5、第2形態 6）")]
    [SerializeField, Min(1)] int _count = 5;
    [Tooltip("並べる間隔（マス。第1形態 3.5、第2形態 3）。部屋の真ん中を中心に並べる")]
    [SerializeField, Min(0f)] float _spacing = 3.5f;
    [Tooltip("1つ並べるごとの時間（秒）")]
    [SerializeField, Min(0f)] float _placeInterval = 0.3f;
    [Tooltip("並べ終えてから落とし始めるまで（秒）")]
    [SerializeField, Min(0f)] float _holdTime = 0.5f;
    [Tooltip("落とす間隔（秒）。左から順に落とす")]
    [SerializeField, Min(0f)] float _dropInterval = 0.25f;
    [Tooltip("【仮】並べる高さ：画面の上の端から、アイコンの上の端までの距離（マス）。画面の上の表示（HUD）に隠れないように下げてある")]
    [SerializeField, Min(0f)] float _topInset = PremiereArt.HudInset;

    [Header("第2形態")]
    [Tooltip("炎のアイコンが着地した所に出す火（Area・箱・床の上・持続あり）。空なら出さない（第1形態）")]
    [SerializeField] AttackData _landingFire;

    struct FireWatch
    {
        public FallingObject Icon;
        public float X;
        public float GroundY;
        public bool Done;
    }

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 size = data.ObjectSize;
        int n = Mathf.Max(1, _count);
        float startX = arena.center.x - (n - 1) * _spacing * 0.5f;
        float hangY = arena.yMax - _topInset - size.y * 0.5f;
        float start = Time.time;
        float lastPlace = start + (n - 1) * _placeInterval;

        var icons = new List<FallingObject>(n);
        var fires = new List<FireWatch>();
        for (int i = 0; i < n; i++)
        {
            // 0.3秒ごとに1つずつ並べる（影も、並べた時に出る）
            float placeAt = start + i * _placeInterval;
            while (Time.time < placeAt) yield return null;

            PremiereIcon kind = i % 2 == 0 ? PremiereIcon.Fire : PremiereIcon.Thunder; // 炎と雷を交互
            float x = Mathf.Clamp(startX + i * _spacing, arena.xMin + size.x * 0.5f, arena.xMax - size.x * 0.5f);
            float fallAt = lastPlace + _holdTime + i * _dropInterval;
            FallingObject f = FallingObject.Spawn(owner, data, new Vector2(x, hangY), Time.time, fallAt, true);
            f.gameObject.name = $"PremiereIcon_{kind}";
            PremiereArt.SetSprite(f.Visual, PremiereArt.Icon(kind), size);
            icons.Add(f);
            if (kind == PremiereIcon.Fire && _landingFire != null)
            {
                fires.Add(new FireWatch { Icon = f, X = x, GroundY = f.GroundY });
            }
            if (i == 0) owner.NotifyReleased(data);
        }

        // 全部が着地する（消える）まで待つ。炎が着地したら、その場に火を出す
        float timeout = Time.time + 30f;
        while (Time.time < timeout)
        {
            bool pending = false;
            for (int k = 0; k < fires.Count; k++)
            {
                FireWatch w = fires[k];
                if (w.Done) continue;
                // 着地すると落下物はすぐ消えるので、消えた（null）ことも着地として扱う。
                // 技ごと止められたとき（形態変化・正常化）は、このコルーチンも止まるので、火は出ない
                if (w.Icon == null || w.Icon.HasLanded)
                {
                    w.Done = true;
                    fires[k] = w;
                    PremiereFx.FireFloor(owner, _landingFire, new Vector2(w.X, w.GroundY));
                }
                else pending = true;
            }
            foreach (var f in icons)
            {
                if (f != null && !f.HasLanded) pending = true;
            }
            if (!pending) break;
            yield return null;
        }
    }
}
