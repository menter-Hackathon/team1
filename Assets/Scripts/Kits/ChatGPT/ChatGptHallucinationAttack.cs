using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】ハルシネーション（2026-11-19。ChatGPT の第1形態の技。docs/app-kits-1-design.md 5章 P1-2）。担当3 の物。
/// 1. ボスの左右に「ウソ」が出る（ボスの絵を 7割の濃さで。0.6秒で、1行ずつ書かれて現れる）
/// 2. 本物とウソが同時に、言葉の弾を 3発ずつ扇に撃つ（予告の線 0.8秒。本物の線は赤、ウソの線は灰色）
/// 3. ウソは消える
/// ・本物の弾だけが当たる。ウソの弾は灰色で、0.15秒ごとに文字化けする（判定なし）
/// ・ウソは 1回当てると消える（ゲージは増えない。KitProp）。触っても痛くない
/// ・弾の速さ・大きさ・寿命・ダメージは攻撃データの「弾」の欄。中身は ChatGptHallucinationEffect（形態変化で消える）
/// </summary>
[CreateAssetMenu(fileName = "ChatGPT_Hallucination", menuName = "Team1/Kits/ChatGPT Hallucination")]
public class ChatGptHallucinationAttack : CustomAttack
{
    [Tooltip("【仮】ウソの数（第1形態 2・第2形態 3）")]
    [SerializeField, Min(1)] int _fakeCount = 2;
    [Tooltip("【仮】ウソを出す x（ボスからのずれ）。前から順に、部屋に入る所を使う")]
    [SerializeField] float[] _offsets = { -5f, 5f, -10f, 10f, -7.5f, 7.5f, -3.5f, 3.5f };
    [Tooltip("【仮】ボス・ほかのウソから、この距離（マス）以上離す")]
    [SerializeField, Min(0f)] float _minGap = 3f;
    [Tooltip("【仮】ウソが現れるのにかける秒数（1行ずつ）")]
    [SerializeField, Min(0.05f)] float _appearTime = 0.6f;
    [Tooltip("【仮】予告の線の秒数（0.8 が基本。短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("【仮】1体が撃つ弾の数")]
    [SerializeField, Min(1)] int _shots = 3;
    [Tooltip("【仮】扇の角度（度）")]
    [SerializeField, Range(0f, 120f)] float _spread = 30f;
    [Tooltip("【仮】撃ってから、ウソが消えるまで（秒）")]
    [SerializeField, Min(0f)] float _vanishDelay = 0.35f;
    [Tooltip("【仮】ウソの濃さ（0〜1）")]
    [SerializeField, Range(0.1f, 1f)] float _fakeAlpha = 0.7f;

    public int FakeCount => _fakeCount;
    public float[] Offsets => _offsets;
    public float MinGap => _minGap;
    public float AppearTime => _appearTime;
    public float Telegraph => _telegraph;
    public int Shots => _shots;
    public float Spread => _spread;
    public float VanishDelay => _vanishDelay;
    public float FakeAlpha => _fakeAlpha;

    /// <summary>技の長さ（ボスはこのあと次の技へ）</summary>
    public float Duration => _appearTime + _telegraph + _vanishDelay + 0.1f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        ChatGptHallucinationEffect.Spawn(owner, data, this);
        yield return new WaitForSeconds(_appearTime + _telegraph);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_vanishDelay + 0.1f);
    }
}
