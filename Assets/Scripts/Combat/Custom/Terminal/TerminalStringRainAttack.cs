using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】文字列の雨（2026-11-18：Terminal の第2形態の技。docs/app-boss-skills-design.md 4-4）。
/// 部屋を2マスずつ10本の列に分け、落ちてくる列の天井に白い四角いカーソル（点滅）と、床までのうすい赤の線を出す（予告 0.7秒）。
/// そのあと、その列に縦に並んだ文字列（5文字）が落ちる（毎秒9マス）。足場はすり抜け、床で消える。これを3回（0.9秒おき）。
/// ・1回に落ちる列は 10本のうち6本。安全な列は、プレイヤーの今の列から 4マス以内に必ず1本ある
/// ・文字列は弾（Projectile）として出す。形態変化（50%）で消え、足場をすり抜け、床で消える
/// ・速さ・判定の大きさ（x が長さ 3、y が幅 1.4）・寿命は、攻撃データの「弾」の欄から読む
/// ・床が文字化けしている間（rm -rf）は選ばない（攻撃データの「床が文字化けしている間は選ばない」をオン）
/// </summary>
[CreateAssetMenu(fileName = "Terminal_StringRain", menuName = "Team1/Custom Attack/Terminal String Rain")]
public class TerminalStringRainAttack : CustomAttack
{
    [Tooltip("【仮】部屋を分ける列の数（2マスずつ）")]
    [SerializeField, Min(2)] int _columns = 10;
    [Tooltip("【仮】1回に落ちる列の数（残りが安全な列）")]
    [SerializeField, Min(1)] int _fallingColumns = 6;
    [Tooltip("【仮】落とす回数")]
    [SerializeField, Min(1)] int _volleys = 3;
    [Tooltip("【仮】落とす間隔（秒）")]
    [SerializeField, Min(0f)] float _volleyInterval = 0.9f;
    [Tooltip("【仮】予告（カーソルと赤い線）を出してから落とすまで（秒）")]
    [SerializeField, Min(0f)] float _telegraphTime = 0.7f;
    [Tooltip("【仮】安全な列が、プレイヤーの今の列から必ずこの距離（マス）以内に1本ある")]
    [SerializeField, Min(0f)] float _safeReach = 4f;
    [Tooltip("【仮】1本の文字列の文字の数")]
    [SerializeField, Min(1)] int _stringLength = 5;
    [Tooltip("【仮】技の長さ（秒）。ボスはこのあと次の技へ")]
    [SerializeField, Min(0f)] float _totalTime = 3.5f;

    public int Columns => _columns;
    public int FallingColumns => _fallingColumns;
    public int Volleys => _volleys;
    public float VolleyInterval => _volleyInterval;
    public float TelegraphTime => _telegraphTime;
    public float SafeReach => _safeReach;
    public int StringLength => _stringLength;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        TerminalStringRainEffect.Spawn(owner, data, this);
        yield return new WaitForSeconds(_telegraphTime);
        owner.NotifyReleased(data);
        float rest = _totalTime - _telegraphTime;
        if (rest > 0f) yield return new WaitForSeconds(rest);
    }
}
