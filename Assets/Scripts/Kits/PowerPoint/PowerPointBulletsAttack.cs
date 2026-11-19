using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】箇条書き（PowerPoint のボスの技 P1-1。2026-11-19。docs/app-kits-1-design.md 11章）。数値はすべて【仮】。
/// ボスの横に「•」が縦に3つ（高・中・低）、上から 0.25秒おきに出る → 出た順に、文の行（2.5×0.6、毎秒 11マス）が横へ飛ぶ（間 0.6秒）。
/// それぞれの通り道に、赤い細い線（0.8秒。WallShot）。
/// 避け方：高＝床にいる。中＝ジャンプしない。低＝ジャンプ（床にいて、最後だけジャンプ）。
/// 第1形態はいつも 高 → 中 → 低。第2形態は順番が変わる。
/// </summary>
[CreateAssetMenu(fileName = "PowerPoint_Bullets", menuName = "Team1/Kits/PowerPoint Bullets")]
public class PowerPointBulletsAttack : CustomAttack
{
    [Tooltip("文の行の攻撃データ（弾として出す。速さ・大きさ・寿命）")]
    [SerializeField] AttackData _line;
    [Tooltip("オン：順番を毎回変える（第2形態）")]
    [SerializeField] bool _shuffle;
    [Tooltip("「•」が出る間隔（秒）")]
    [SerializeField, Min(0f)] float _dotInterval = 0.25f;
    [Tooltip("行が飛ぶ間隔（秒）")]
    [SerializeField, Min(0.1f)] float _interval = 0.6f;
    [Tooltip("通り道の赤い線（秒。短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("ボスの真ん中から、行を出す位置までの横の距離（マス）")]
    [SerializeField, Min(0f)] float _side = 1.6f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _line != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        var order = new List<WallShotHeight> { WallShotHeight.High, WallShotHeight.Mid, WallShotHeight.Low };
        if (_shuffle)
        {
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
        }
        Rect area = OfficeKitTools.Area(owner, out float floor);
        int dir = owner.Facing;
        float x = Mathf.Clamp(owner.OriginPoint.x + dir * _side, area.xMin + 0.5f, area.xMax - 0.5f);

        float start = Time.time;
        int dots = 0, fired = 0;
        float lastFire = (order.Count - 1) * _interval + _telegraph;
        while (Time.time - start < lastFire + 0.1f)
        {
            if (fired < order.Count && OfficeKitTools.OwnerStopped(owner, data)) yield break;
            float t = Time.time - start;
            // 「•」：上から（高 → 中 → 低）の並びで出す（出る順は、飛ぶ順と同じ）
            while (dots < order.Count && t >= dots * _dotInterval)
            {
                float y = floor + WallShot.HeightOf(order[dots]);
                float life = dots * _interval + _telegraph - t + 0.05f;
                OfficeFx dot = OfficeFx.Sprite(owner, PowerPointArt.Dot, new Vector2(x - dir * 0.6f, y), Vector2.one * 0.5f, Mathf.Max(0.1f, life), 22, null, 0.2f);
                dot.transform.localScale = Vector3.one * 1.4f;
                dots++;
            }
            // 行：赤い線（0.8秒）のあとに飛ぶ
            while (fired < order.Count && t >= fired * _interval)
            {
                float y = floor + WallShot.HeightOf(order[fired]);
                WallShot.FireFrom(owner, _line, new Vector2(x, y), dir, _telegraph, PowerPointArt.Line);
                fired++;
            }
            yield return null;
        }
        yield return new WaitForSeconds(0.2f);
    }
}
