using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Simulator（Xcode のボスの技2。2026-11-19。docs/app-kits-2-design.md 3-2）。
/// 床に赤い影（0.8秒。プレイヤーから 3マス以上）→ iPhone（2×4マス）が落ちて立つ → 画面が点く（0.8秒。画面からプレイヤーへ赤い線）→
/// 通知の弾を撃つ（毎秒 8マス。弾の数値は _bullet）→ 画面が消えて引っ込む。
/// iPhone は近接1回でこわせる（KitProp。撃つ前にこわすと撃たない）。第2形態：3台。弾は 2発ずつ。
/// </summary>
[CreateAssetMenu(fileName = "Xcode_Simulator", menuName = "Team1/Kits/Xcode/Simulator")]
public class XcodeSimulatorAttack : CustomAttack
{
    [Tooltip("通知の弾（弾の攻撃データ。名前に枠の語を入れない）")]
    [SerializeField] AttackData _bullet;
    [Tooltip("【仮】iPhone の台数")]
    [SerializeField, Min(1)] int _phones = 2;
    [Tooltip("【仮】1台の弾の数")]
    [SerializeField, Min(1)] int _shots = 1;
    [Tooltip("【仮】影の予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("【仮】画面が点いてから撃つまで（秒。赤い線の予告）")]
    [SerializeField, Min(0.8f)] float _aimTime = 0.8f;
    [Tooltip("【仮】プレイヤーから離す距離（マス）")]
    [SerializeField, Min(0f)] float _minDistance = 3f;
    [Tooltip("【仮】iPhone の大きさ（マス）")]
    [SerializeField] Vector2 _size = new Vector2(2f, 4f);

    public AttackData Bullet => _bullet;
    public int Shots => _shots;
    public float Telegraph => _telegraph;
    public float AimTime => _aimTime;
    public Vector2 Size => _size;

    public override bool CanRun(AttackRunner owner, AttackData data) => _bullet != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float length = 0f;
        try
        {
            Rect arena = BattleArena.GetBounds();
            float px = XcodeArt.TargetOf(owner).x;
            var used = new List<float>();
            for (int i = 0; i < _phones; i++)
            {
                float x = PickX(arena, px, used);
                used.Add(x);
                XcodeSimulatorPhone.Spawn(owner, data, this, x, i * 0.15f);
            }
            length = (_phones - 1) * 0.15f + XcodeSimulatorPhone.LengthOf(this);
        }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        if (length > 0f) yield return new WaitForSeconds(length);
    }

    float PickX(Rect arena, float px, List<float> used)
    {
        float half = _size.x * 0.5f + 0.5f;
        float best = arena.center.x;
        float bestScore = float.MinValue;
        for (int t = 0; t < 24; t++)
        {
            float x = Random.Range(arena.xMin + half + 1f, arena.xMax - half - 1f);
            float score = 0f;
            if (Mathf.Abs(x - px) < _minDistance + half) score -= 100f;
            foreach (float u in used) if (Mathf.Abs(x - u) < _size.x + 1f) score -= 50f;
            score += Random.value;
            if (score > bestScore) { bestScore = score; best = x; }
        }
        return best;
    }
}
