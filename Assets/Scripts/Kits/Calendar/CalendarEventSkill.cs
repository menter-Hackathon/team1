using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】カレンダーの能力「予定を入れる」（2026-11-19。docs/app-kits-1-design.md 9章）。Custom 小（中身は指定位置の判定を遅らせて出すだけ）。
/// 足元に予定のブロックを置く（置くだけ。すぐ動ける）→ 3秒後、その場所に大きい判定（幅 5×高さ 4、0.4秒）。
/// ブロックの上で、輪が 3秒かけて閉じる（時刻が近づく）。ボスが 3秒後にいる所へ置く能力（浮くボスは 3か所を回るので読める）。
/// ・ゲージ +20（一番強い）・雑魚は倒れる・クールタイム 5秒（攻撃データの共通の欄）
/// ・判定の大きさ・時間・色は、この技の攻撃データの「指定位置の判定」の欄（予告は 0 にする：赤い予告はボスの物）
/// </summary>
[CreateAssetMenu(fileName = "Calendar_Event", menuName = "Team1/Kits/Calendar Event")]
public class CalendarEventSkill : CustomAttack
{
    [Tooltip("置いてから判定が出るまで（秒）")]
    [SerializeField, Min(0.1f)] float _delay = 3f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        Vector2 feet = owner.transform.position;
        owner.TryFindGround(feet.x, feet.y + 0.3f, out float ground);
        if (ground > feet.y + 0.3f) ground = feet.y;
        Vector2 spot = new Vector2(feet.x, ground);
        int color = Random.Range(0, CalendarArt.EventColors.Length);
        AreaStrike.Spawn(owner, data, spot + Vector2.up * 0.05f, Vector2.up, _delay, 0);
        CalendarPlannedBlock.Spawn(owner, spot, data.AreaSize, _delay, data.HitWindow, color);
        owner.NotifyReleased(data);
        yield return null;
    }

    /// <summary>HUD の枠の絵（カレンダーのアイコン風）</summary>
    public override Sprite DisplayIcon(AttackRunner owner) => CalendarArt.SkillIcon;
}

/// <summary>置いた予定のブロックと、閉じていく輪。時刻に、予定の色の大きい枠が光って消える</summary>
public class CalendarPlannedBlock : MonoBehaviour
{
    const int BlockOrder = 15, RingOrder = 41, FlashOrder = 42;
    const float RingStart = 2.6f, RingEnd = 0.5f;
    float _born, _delay, _active;
    Color _color;
    Vector2 _area;
    SpriteRenderer _block, _ring, _flash;
    bool _fired;

    public static CalendarPlannedBlock Spawn(AttackRunner owner, Vector2 ground, Vector2 area, float delay, float active, int color)
    {
        var go = new GameObject("Calendar_PlannedBlock");
        go.transform.position = ground;
        var b = go.AddComponent<CalendarPlannedBlock>();
        b._born = Time.time;
        b._delay = delay;
        b._active = active;
        b._area = area;
        b._color = CalendarArt.EventColor(color);
        b._block = CalendarArt.Put(go.transform, CalendarArt.Block(color), new Vector2(0f, 0.45f), new Vector2(1.5f, 0.9f), Color.white, BlockOrder, "Block");
        b._ring = CalendarArt.Put(go.transform, CallArt.Ring(48, 3), new Vector2(0f, 1.6f), Vector2.one * RingStart, b._color, RingOrder, "Ring");
        return b;
    }

    void Update()
    {
        float t = Time.time - _born;
        if (!_fired)
        {
            float k = Mathf.Clamp01(t / _delay);
            float d = Mathf.Lerp(RingStart, RingEnd, k);
            if (_ring != null)
            {
                _ring.transform.localScale = new Vector3(d / (48f * CallArt.Px), d / (48f * CallArt.Px), 1f);
                // 最後の 0.5秒は点滅（来る）
                _ring.enabled = _delay - t > 0.5f || Mathf.FloorToInt(t / 0.06f) % 2 == 0;
            }
            if (t < _delay) return;
            _fired = true;
            if (_ring != null) _ring.enabled = false;
            if (_block != null) _block.enabled = false;
            // 時刻：予定の色の大きい枠（判定の大きさ）とチェック
            int wp = Mathf.Clamp(Mathf.RoundToInt(_area.x * CallArt.Ppu), 4, 200);
            int hp = Mathf.Clamp(Mathf.RoundToInt(_area.y * CallArt.Ppu), 4, 200);
            _flash = CalendarArt.Put(transform, CallArt.Frame(wp, hp, 3), new Vector2(0f, _area.y * 0.5f), _area, _color, FlashOrder, "Flash");
            FxAnim.Sprite(CalendarArt.Check, (Vector2)transform.position + new Vector2(0f, _area.y + 0.3f), FlashOrder + 1, 0.5f, Color.white, 2f, "Calendar_Check").Pop().Rise(0.4f);
            FxKit.Fragments((Vector2)transform.position + Vector2.up * _area.y * 0.6f, Vector2.up, 8, null, CalendarArt.EventColors, 1f);
        }
        float after = t - _delay;
        if (_flash != null) _flash.color = CallKitUtil.Alpha(_color, Mathf.Clamp01(1f - after / (_active + 0.2f)));
        if (after >= _active + 0.2f) Destroy(gameObject);
    }
}
