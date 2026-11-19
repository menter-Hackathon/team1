using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】diff の行（GitHub Desktop のボスの技1。2026-11-19。docs/app-kits-2-design.md 2-2）。
/// 部屋を上下の2段に分け、片方に緑の + の帯、もう片方に赤の - の帯（1.2秒。赤は終わりぎわに速く点滅）→ 赤の段が消える（判定 0.15秒）。
/// ・下の段：床〜高さ 2.2。上の段：高さ 2.8〜6.5（床からのマス）
/// ・第1形態は 2回（上下はランダム）。第2形態は 3回で、最後の1回は左右の半分ずつで上下が逆
/// 避け方：下が赤なら足場の上かジャンプ。上が赤なら床にいる。
/// </summary>
[CreateAssetMenu(fileName = "GitHub_Diff", menuName = "Team1/Kits/GitHub Desktop/Diff")]
public class GitHubDiffAttack : CustomAttack
{
    [Tooltip("【仮】何回くり返すか")]
    [SerializeField, Min(1)] int _count = 2;
    [Tooltip("【仮】予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 1.2f;
    [Tooltip("【仮】判定（秒）")]
    [SerializeField, Min(0.05f)] float _hitTime = 0.15f;
    [Tooltip("【仮】判定のあと、次の回までの間（秒）")]
    [SerializeField, Min(0f)] float _gap = 0.35f;
    [Tooltip("【仮】下の段の上の端（床からのマス）")]
    [SerializeField] float _lowerTop = 2.2f;
    [Tooltip("【仮】上の段の下の端・上の端（床からのマス）")]
    [SerializeField] float _upperBottom = 2.8f;
    [SerializeField] float _upperTop = 6.5f;
    [Tooltip("【仮】オン：最後の1回は、左右の半分ずつで上下が逆（第2形態）")]
    [SerializeField] bool _splitLast;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        float length = Spawn(owner, data);
        owner.NotifyReleased(data);
        if (length > 0f) yield return new WaitForSeconds(length);
    }

    /// <summary>帯を全部出す（例外はここで止める）。技の長さを返す</summary>
    float Spawn(AttackRunner owner, AttackData data)
    {
        try
        {
            Rect arena = BattleArena.GetBounds();
            float floor = arena.yMin;
            var lower = new Vector2(floor, floor + _lowerTop);
            var upper = new Vector2(floor + _upperBottom, floor + _upperTop);
            float step = _telegraph + _hitTime + _gap;
            bool lastRedLower = Random.value < 0.5f;
            for (int i = 0; i < _count; i++)
            {
                var bands = new List<GitHubDiffBand.Band>();
                bool redLower = Random.value < 0.5f;
                if (i > 0 && Random.value < 0.6f) redLower = !lastRedLower; // 同じ段が続きすぎないように
                lastRedLower = redLower;
                if (_splitLast && i == _count - 1 && _count > 1)
                {
                    // 左右の半分ずつで上下が逆
                    float mid = arena.center.x;
                    bands.Add(Band(arena.xMin, mid, lower, redLower, 1));
                    bands.Add(Band(arena.xMin, mid, upper, !redLower, 1));
                    bands.Add(Band(mid, arena.xMax, lower, !redLower, 2));
                    bands.Add(Band(mid, arena.xMax, upper, redLower, 2));
                }
                else
                {
                    bands.Add(Band(arena.xMin, arena.xMax, lower, redLower, 0));
                    bands.Add(Band(arena.xMin, arena.xMax, upper, !redLower, 0));
                }
                GitHubDiffBand.Spawn(owner, data, bands, i * step, _telegraph, _hitTime, i);
            }
            return _count * step - _gap;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            return 0f;
        }
    }

    static GitHubDiffBand.Band Band(float x0, float x1, Vector2 y, bool red, int side)
        => new GitHubDiffBand.Band { Area = Rect.MinMaxRect(x0, y.x, x1, y.y), Red = red, Side = side };
}
