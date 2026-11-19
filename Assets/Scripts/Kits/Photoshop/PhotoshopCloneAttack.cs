using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】コピースタンプ（Photoshop の第2形態の技。2026-11-19。docs/app-kits-2-design.md 3-1）。
/// プレイヤーに照準の印（⌥ クリックの丸に十字。0.6秒。当たらない）→ プレイヤーから 4マス以上離れた 3か所の床に、赤い輪かくのプレイヤーの形（0.8秒）
/// → 半透明の分身になり、毎秒 3.5マスでプレイヤーへ歩く。3秒で消える。触れると当たる。近接1回で消せる（KitProp）。
/// 分身は雑魚ではない（ゲージ・倒した数には入らない）
/// </summary>
[CreateAssetMenu(fileName = "Photoshop_Clone", menuName = "Team1/Kits/Photoshop Clone")]
public class PhotoshopCloneAttack : CustomAttack
{
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("照準の秒数（当たらない）")]
    [SerializeField, Min(0f)] float _reticle = 0.6f;
    [Tooltip("赤い輪かくの予告（0.8 以上）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [SerializeField, Min(0.5f)] float _walkSpeed = 3.5f;
    [SerializeField, Min(0.5f)] float _life = 3f;
    [Tooltip("プレイヤーから離す距離（マス）")]
    [SerializeField, Min(0f)] float _minDistance = 4f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 p = owner.TargetPoint ?? arena.center;
        Transform deco = PathStroke.Decor(owner, p, _reticle, "PsReticle");
        var ret = RuntimeSprites.Show(deco, PhotoshopArt.Reticle, Vector2.zero, 45, 2f, "Reticle");
        ret.color = Color.white;
        TextMesh alt = WorldText.Create(deco, "Alt+Click", 0.3f, Color.white, 46, true, TextAnchor.MiddleCenter, "Alt");
        alt.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        float end = Time.time + _reticle;
        while (Time.time < end)
        {
            if (owner.TargetPoint is Vector2 t) deco.position = t;
            yield return null;
        }

        // 3か所（プレイヤーから離れた床）
        float px = owner.TargetPoint is Vector2 tp ? tp.x : arena.center.x;
        var xs = new List<float>();
        for (int tries = 0; tries < 40 && xs.Count < _count; tries++)
        {
            float x = Random.Range(arena.xMin + 1f, arena.xMax - 1f);
            if (Mathf.Abs(x - px) < _minDistance) continue;
            bool near = false;
            foreach (var o in xs) if (Mathf.Abs(o - x) < 1.6f) near = true;
            if (!near) xs.Add(x);
        }
        foreach (var x in xs)
        {
            GameObject go = owner.CreateSpawnedObject("Photoshop_Clone", new Vector2(x, arena.yMin));
            go.AddComponent<PhotoshopCloneEffect>().Setup(owner, data, _telegraph, _walkSpeed, _life);
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_telegraph + 0.3f);
    }
}
