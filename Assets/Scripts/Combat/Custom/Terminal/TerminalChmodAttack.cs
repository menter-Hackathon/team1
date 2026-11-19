using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】chmod 000 player（2026-11-18：Terminal の技。docs/app-boss-skills-design.md 4-2）。
/// 2026-11-18 まさぴ〜の指示で変更：鍵の弾ではなく「アクセス禁止エリアにいると封印」にした。
/// 1. コマンドを打つ（約0.9秒）。打ち始めた瞬間のプレイヤーの位置を真ん中に、幅5マスの縦の帯（床から天井まで）が出る。
///    左右のふちに赤い鎖が天井から下りてくる（TerminalChmodZone）。帯はあとから動かない。第2形態は帯3つ（左右 7.5マス）
/// 2. 鎖が床に着く瞬間（打ち終わり）に1回だけ判定：プレイヤーの体の真ん中の x が帯の中なら封印。足場の上・空中でも同じ。
///    外なら帯が砕けて、ここで技は終わり（ボスのすき）
/// 3. 封印 3秒（TerminalSealEffect）：ジャンプと近接だけが使えない。移動と能力は使える。ダメージなし。
///    封印の間、ボスはアクセス拒否の柱を 1.2秒おきに2回出し、封印が切れるまで次の技を始めない
/// 場に残る技（攻撃データの「場に残る技」をオン）。封印の間は、ほかの場に残る技は選ばれない
/// </summary>
[CreateAssetMenu(fileName = "Terminal_Chmod", menuName = "Team1/Custom Attack/Terminal chmod")]
public class TerminalChmodAttack : CustomAttack
{
    [Tooltip("【仮】コマンドの行に打つ文字")]
    [SerializeField] string _command = "chmod 000 player";
    [Tooltip("【仮】封印の秒数（ジャンプ・近接）")]
    [SerializeField, Min(0.1f)] float _sealTime = 3f;
    [Tooltip("【仮】アクセス禁止エリアの幅（マス）。床から天井まで")]
    [SerializeField, Min(0.5f)] float _zoneWidth = 5f;
    [Tooltip("【仮】エリアの数。1つ目はプレイヤーの位置、2つ目は左、3つ目は右")]
    [SerializeField, Min(1)] int _zoneCount = 1;
    [Tooltip("【仮】エリアの真ん中どうしの間（マス）。幅5・間7.5なら、すき間は2.5マス")]
    [SerializeField, Min(0f)] float _zoneSpacing = 7.5f;
    [Tooltip("【仮】判定の幅を、見た目より左右それぞれこれだけせまくする（マス）")]
    [SerializeField, Min(0f)] float _judgeInset = 0.2f;
    [Tooltip("【仮】判定のあと、エリアが消えるまで（秒）")]
    [SerializeField, Min(0.05f)] float _fadeTime = 0.3f;
    [Tooltip("アクセス拒否の柱（種類 Area。ブレークポイントの複製を赤にした物）。数・間隔は柱の攻撃データの「同時に出す数」「ずらす距離」")]
    [SerializeField] AttackData _pillar = null;
    [Tooltip("【仮】柱を出す回数")]
    [SerializeField, Min(0)] int _pillarRepeats = 2;
    [Tooltip("【仮】柱を出す間隔（秒）")]
    [SerializeField, Min(0f)] float _pillarInterval = 1.2f;
    [Tooltip("【仮】南京錠が閉まってから、1回目の柱を出すまで（秒）")]
    [SerializeField, Min(0f)] float _pillarDelay = 0.3f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        TerminalCommandLine line = TerminalCommandLine.Spawn(owner, _command);
        Rect arena = BattleArena.GetBounds();
        float telegraph = TerminalCommandLine.TypingTime(_command);

        // 1. 打ち始めた瞬間のプレイヤーの位置で、エリアの場所を決める（あとから追わない。壁ぎわでも部屋の中へ押し戻さない）
        float px = (owner.TargetPoint ?? owner.OriginPoint).x;
        var centers = new List<float>();
        for (int i = 0; i < _zoneCount; i++)
        {
            int step = (i + 1) / 2;
            float sign = i % 2 == 1 ? -1f : 1f;
            float cx = px + sign * step * _zoneSpacing;
            if (cx + _zoneWidth * 0.5f < arena.xMin || cx - _zoneWidth * 0.5f > arena.xMax) continue; // 全部が部屋の外なら出さない
            centers.Add(cx);
        }
        TerminalChmodZone zone = TerminalChmodZone.Spawn(owner, centers, _zoneWidth, telegraph, _fadeTime);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(telegraph);

        // 2. 判定（1回だけ）：体の真ん中の x が、どれかの帯の中か
        PlayerController player = owner.Target != null ? owner.Target.GetComponentInParent<PlayerController>() : null;
        bool caught = false;
        if (player != null && owner.TargetPoint is Vector2 body)
        {
            float half = Mathf.Max(0.05f, _zoneWidth * 0.5f - _judgeInset);
            foreach (float cx in centers)
            {
                if (Mathf.Abs(body.x - cx) <= half) { caught = true; break; }
            }
        }
        if (zone != null) zone.Resolve(caught);
        if (!caught)
        {
            if (_fadeTime > 0f) yield return new WaitForSeconds(_fadeTime);
            if (line != null) line.Close();
            yield break; // 外にいた：ボスのすき
        }

        // 3. 封印。柱を出し、封印が切れるまで次の技を始めない
        TerminalSealEffect seal = TerminalSealEffect.SpawnOn(owner, player, _sealTime);
        if (seal != null)
        {
            if (_pillarDelay > 0f) yield return new WaitForSeconds(_pillarDelay);
            for (int r = 0; r < _pillarRepeats && seal != null && !seal.Released; r++)
            {
                if (r > 0 && _pillarInterval > 0f) yield return new WaitForSeconds(_pillarInterval);
                SpawnPillars(owner, arena);
            }
            while (seal != null && !seal.Released) yield return null;
        }
        if (line != null) line.Close();
    }

    /// <summary>
    /// アクセス拒否の柱を出す（1本目はプレイヤーの位置、2本目は左、3本目は右）。
    /// AreaStrike を直接出して目印（TerminalClearMarker）を付け、形態変化（50%）でも消えるようにする
    /// </summary>
    void SpawnPillars(AttackRunner owner, Rect arena)
    {
        if (_pillar == null) return;
        Vector2 anchor = owner.TargetPoint ?? owner.OriginPoint;
        float half = _pillar.AreaSize.x * 0.5f;
        for (int i = 0; i < _pillar.AreaCount; i++)
        {
            int step = (i + 1) / 2;
            float sign = i % 2 == 1 ? -1f : 1f;
            Vector2 p = anchor + new Vector2(sign * step * _pillar.AreaSpacing, 0f);
            p.x = Mathf.Clamp(p.x, arena.xMin + half, arena.xMax - half);
            AreaStrike strike = AreaStrike.Spawn(owner, _pillar, p, Vector2.up, i * _pillar.AreaStagger);
            if (strike != null) strike.gameObject.AddComponent<TerminalClearMarker>();
        }
        owner.NotifyReleased(_pillar);
    }
}
