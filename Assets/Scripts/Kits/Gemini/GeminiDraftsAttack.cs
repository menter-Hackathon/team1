using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】3つの回答案（2026-11-19。Gemini の第1形態の技。docs/app-kits-1-design.md 7章 P1-1）。担当3 の物。
/// 床が 3つの区画（左・中・右）に分かれ、上に札 1・2・3 → 3つとも点滅（0.6秒）→ 1つが青（安全）、2つが赤に決まる（予告）
/// → 赤い区画に星の光（床から高さ 5、0.5秒）。第2形態は 2回続く（安全な区画が変わる）。
/// ・安全な区画は、プレイヤーが今いる区画のとなり（遠い端にはしない）。区画は ZoneStrike（形態変化で消える）
/// ・予告は、部屋の端からとなりの区画まで走って間に合うように 1.2秒【仮】（仕様書は 1.0秒。約7マス＝1.0秒ちょうどで、反応の分が無い）
/// ・ダメージ・ノックバックは攻撃データの共通の欄
/// </summary>
[CreateAssetMenu(fileName = "Gemini_Drafts", menuName = "Team1/Kits/Gemini Drafts")]
public class GeminiDraftsAttack : CustomAttack
{
    [Tooltip("【仮】続ける回数（第1形態 1・第2形態 2）")]
    [SerializeField, Min(1)] int _rounds = 1;
    [Tooltip("【仮】3つとも点滅する秒数（1回目）")]
    [SerializeField, Min(0f)] float _flicker = 0.6f;
    [Tooltip("【仮】点滅する秒数（2回目から）")]
    [SerializeField, Min(0f)] float _flickerNext = 0.3f;
    [Tooltip("【仮】赤・青に決まってから光るまで（予告。0.8 より短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 1.2f;
    [Tooltip("【仮】星の光の秒数")]
    [SerializeField, Min(0.05f)] float _active = 0.5f;
    [Tooltip("【仮】星の光の高さ（床から。足場の上も当たる）")]
    [SerializeField, Min(1f)] float _height = 5f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect area = ZoneStrike.AreaFor(owner);
        float floor = ZoneStrike.FloorY(owner);
        Rect[] zones = ZoneStrike.Columns(area, 3, floor, _height);
        float total = _flicker + _telegraph + _active + (_rounds - 1) * (_flickerNext + _telegraph + _active) + 0.1f;
        GeminiDraftsEffect labels = GeminiDraftsEffect.Spawn(owner, zones, total);
        int lastSafe = -1;
        for (int r = 0; r < _rounds; r++)
        {
            float flick = r == 0 ? _flicker : _flickerNext;
            if (labels != null) labels.Flicker(flick);
            if (flick > 0f) yield return new WaitForSeconds(flick);

            float px = (owner.TargetPoint ?? owner.OriginPoint).x;
            int pz = ZoneStrike.IndexOfX(zones, px);
            int safe = PickSafe(pz, lastSafe, zones.Length);
            lastSafe = safe;
            ZoneStrike.Safe(owner, zones[safe], 0f, _telegraph + _active, GeminiArt.Blue);
            for (int i = 0; i < zones.Length; i++)
            {
                if (i == safe) continue;
                ZoneStrike.Strike(owner, data, zones[i], 0f, _telegraph, _active, GeminiArt.Beam, new Color(1f, 1f, 1f, 0.9f));
            }
            if (labels != null) labels.Decide(safe);
            owner.NotifyReleased(data);
            yield return new WaitForSeconds(_telegraph + _active);
        }
    }

    /// <summary>安全な区画：今いる区画のとなり（前の回と同じにはしない。ほかに無ければ同じでよい＝間に合うことが先）</summary>
    static int PickSafe(int playerZone, int last, int count)
    {
        var c = new System.Collections.Generic.List<int>();
        if (playerZone - 1 >= 0) c.Add(playerZone - 1);
        if (playerZone + 1 < count) c.Add(playerZone + 1);
        var fresh = c.FindAll(i => i != last);
        if (fresh.Count > 0) return fresh[Random.Range(0, fresh.Count)];
        if (c.Count > 0) return c[Random.Range(0, c.Count)];
        return Mathf.Clamp(playerZone, 0, count - 1);
    }
}
