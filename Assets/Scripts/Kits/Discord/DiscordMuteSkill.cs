using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Discord の能力「ミュート」（2026-11-19。docs/app-kits-1-design.md 3章）。Custom 小。
/// 正面 2マス先に、直径 4.5 の円。中の敵の弾を全部消す（E5 KitBullets）。中の雑魚は倒れ、ボスにはゲージ +8。
/// ボスに当たると、ボスの頭の上に赤い斜線のマイク。2秒、ボスは次の技を始めない（E5 KitBoss.Stun。続けて当てても 2秒に戻るだけ）。
/// ダメージ・ゲージ・クールタイムは攻撃データの共通の欄（雑魚 5・ゲージ 8・5秒）。円の見た目は青紫の輪
/// </summary>
[CreateAssetMenu(fileName = "Discord_Mute", menuName = "Team1/Kits/Discord Mute")]
public class DiscordMuteSkill : CustomAttack
{
    [Tooltip("体の真ん中から、円の中心までの前の距離（マス）")]
    [SerializeField] float _forward = 2f;
    [Tooltip("円の直径（マス）")]
    [SerializeField, Min(0.5f)] float _diameter = 4.5f;
    [Tooltip("判定の時間（秒）。この間に入った弾も消す")]
    [SerializeField, Min(0.02f)] float _active = 0.15f;
    [Tooltip("ボスが止まる秒数")]
    [SerializeField, Min(0f)] float _stun = 2f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        CallKitUtil.TryBodyBounds(owner.transform, out Bounds body);
        int dir = owner.Facing >= 0 ? 1 : -1;
        Vector2 center = new Vector2(body.center.x + dir * _forward, body.center.y);
        float radius = _diameter * 0.5f;

        KitBullets.ClearEnemyProjectiles(center, radius);
        CallTimedHit hit = CallKitUtil.TimedHit(owner, data, center, Vector2.one * _diameter, true, 0f, _active, "Mute");
        if (hit != null && hit.Hitbox != null)
        {
            float stun = _stun;
            hit.Hitbox.Hit += (target, info) =>
            {
                BossController boss = CallKitUtil.BossOf(target);
                if (boss == null) return;
                KitBoss.Stun(boss, stun);
                DiscordMuteMark.Show(boss, stun);
            };
        }
        DiscordMuteFx.Spawn(center, _diameter, _active);
        owner.NotifyReleased(data);
        yield return null;
    }

    /// <summary>HUD の枠の絵（暗い地に赤い斜線のマイク）</summary>
    public override Sprite DisplayIcon(AttackRunner owner) => DiscordArt.MuteIcon;
}

/// <summary>ミュートの見た目：青紫の輪が広がって消える。判定の間は、入ってきた敵の弾も消す</summary>
public class DiscordMuteFx : MonoBehaviour
{
    const int Order = 46;
    const float Life = 0.4f;
    SpriteRenderer _ring, _fill, _mic;
    Vector2 _center;
    float _diameter, _clearUntil, _born;

    public static DiscordMuteFx Spawn(Vector2 center, float diameter, float clearSeconds)
    {
        var go = new GameObject("Discord_MuteFx");
        go.transform.position = center;
        var fx = go.AddComponent<DiscordMuteFx>();
        fx._center = center;
        fx._diameter = diameter;
        fx._born = Time.time;
        fx._clearUntil = Time.time + clearSeconds;
        int d = Mathf.Clamp(Mathf.RoundToInt(diameter * CallArt.Ppu), 8, 96);
        fx._fill = DiscordArt.Put(go.transform, CallArt.Disc(Mathf.Min(48, d)), Vector2.zero, Vector2.one * diameter, CallKitUtil.Alpha(DiscordArt.Blurple, 0.25f), Order, "Fill");
        fx._ring = DiscordArt.Put(go.transform, DiscordArt.MuteRing(d), Vector2.zero, Vector2.one * diameter, DiscordArt.Blurple, Order + 1, "Ring");
        fx._mic = RuntimeSprites.Show(go.transform, DiscordArt.MutedMic, Vector2.zero, Order + 2, 1.5f, "Mic");
        return fx;
    }

    void Update()
    {
        float t = (Time.time - _born) / Life;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        if (Time.time < _clearUntil) KitBullets.ClearEnemyProjectiles(_center, _diameter * 0.5f);
        float s = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(t * 3f));
        transform.localScale = new Vector3(s, s, 1f);
        float a = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;
        if (_ring != null) _ring.color = CallKitUtil.Alpha(DiscordArt.Blurple, a);
        if (_fill != null) _fill.color = CallKitUtil.Alpha(DiscordArt.Blurple, 0.25f * a);
        if (_mic != null) _mic.color = CallKitUtil.Alpha(Color.white, a);
    }
}

/// <summary>ボスの頭の上の、赤い斜線のマイク（止まっている間）。ボスが止まっていなければ消える</summary>
public class DiscordMuteMark : MonoBehaviour
{
    const int Order = 55;
    BossController _boss;
    float _endAt;
    static DiscordMuteMark _current;

    public static void Show(BossController boss, float seconds)
    {
        if (boss == null) return;
        if (_current != null && _current._boss == boss)
        {
            _current._endAt = Time.time + seconds; // 続けて当てたら、時間だけ戻す
            return;
        }
        if (_current != null) Destroy(_current.gameObject);
        var go = new GameObject("Discord_MuteMark");
        var m = go.AddComponent<DiscordMuteMark>();
        m._boss = boss;
        m._endAt = Time.time + seconds;
        // 暗い丸の上に、マイク
        DiscordArt.Put(go.transform, CallArt.Disc(24), Vector2.zero, Vector2.one * 1.4f, CallKitUtil.Alpha(DiscordArt.Darker, 0.9f), Order, "Back");
        RuntimeSprites.Show(go.transform, DiscordArt.MutedMic, Vector2.zero, Order + 1, 1f, "Mic");
        _current = m;
        m.Place();
    }

    void Place()
    {
        if (_boss == null) return;
        Bounds b = _boss.BodyBounds;
        transform.position = new Vector3(b.center.x, b.max.y + 0.9f, 0f);
    }

    void Update()
    {
        if (_boss == null || Time.time >= _endAt || !_boss.IsStunned)
        {
            Destroy(gameObject);
            return;
        }
        Place();
        float bob = Mathf.Sin(Time.time * 6f) * 0.06f;
        transform.position += new Vector3(0f, bob, 0f);
    }

    void OnDestroy()
    {
        if (_current == this) _current = null;
    }
}
