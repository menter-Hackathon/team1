using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Discord のボスの技「ボイス」（2026-11-19。docs/app-kits-1-design.md 3章 P1-1）。
/// ボスのふちが緑に光る（話している）→ 音の波（床から 0.6 の高さ）を、プレイヤーのいる向きへ shots 発（間 interval 秒）。
/// ・高さが決まった横の弾なので WallShot.FireFrom を使う（通り道に赤い細い線の予告 0.8秒）
/// ・弾の数値（速さ 8・大きさ 1.2×0.8・寿命）は、この技の攻撃データの「弾」の欄。絵は音の波（緑の弧 3本）
/// ・避け方：ジャンプで越える。2発目は着地してからもう一度
/// 第2形態：3連・間 0.6秒（全部 低）
/// </summary>
[CreateAssetMenu(fileName = "Discord_Voice", menuName = "Team1/Kits/Discord Voice")]
public class DiscordVoiceAttack : CustomAttack
{
    [Tooltip("撃つ数")]
    [SerializeField, Min(1)] int _shots = 2;
    [Tooltip("撃つ間隔（秒）")]
    [SerializeField, Min(0.1f)] float _interval = 0.7f;
    [Tooltip("波の高さ（床から。マス）")]
    [SerializeField, Min(0.1f)] float _height = 0.6f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        Rect area = WallShot.AreaFor(owner);
        float floor = WallShot.FloorY(owner, area);
        Vector2 target = owner.TargetPoint ?? (Vector2)owner.transform.position + Vector2.right;
        int dir = target.x >= owner.transform.position.x ? 1 : -1;
        Bounds body = BodyOf(owner);
        float total = WallShot.DefaultTelegraph + (_shots - 1) * _interval;
        DiscordSpeakingGlow.Spawn(owner, total + 0.2f);

        for (int i = 0; i < _shots; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_interval);
            float x = dir > 0 ? body.max.x : body.min.x;
            WallShot.FireFrom(owner, data, new Vector2(x, floor + _height), dir, WallShot.DefaultTelegraph, DiscordArt.Wave);
        }
        yield return new WaitForSeconds(WallShot.DefaultTelegraph + 0.1f);
    }

    static Bounds BodyOf(AttackRunner owner)
    {
        var w = owner.GetComponent<IWarpable>();
        if (w != null) return w.BodyBounds;
        return new Bounds(owner.transform.position + Vector3.up, new Vector3(1.5f, 2f, 0f));
    }
}

/// <summary>話している人の緑のふち（ボスの体のまわり。点滅）。DiscordVoiceAttack が出す。形態変化でも消える</summary>
public class DiscordSpeakingGlow : CustomEffect
{
    const int Order = 30;
    AttackRunner _owner;
    SpriteRenderer _frame;
    float _endAt;

    public static DiscordSpeakingGlow Spawn(AttackRunner owner, float seconds)
    {
        if (owner == null) return null;
        GameObject go = owner.CreateSpawnedObject("Discord_SpeakingGlow", owner.transform.position);
        var g = go.AddComponent<DiscordSpeakingGlow>();
        g._owner = owner;
        g._endAt = Time.time + Mathf.Max(0.1f, seconds);
        g.Place();
        return g;
    }

    void Place()
    {
        if (_owner == null) return;
        var w = _owner.GetComponent<IWarpable>();
        Bounds b = w != null ? w.BodyBounds : new Bounds(_owner.transform.position + Vector3.up, new Vector3(1.5f, 2f, 0f));
        Vector2 size = (Vector2)b.size + new Vector2(0.4f, 0.4f);
        transform.position = b.center;
        int wp = Mathf.Clamp(Mathf.RoundToInt(size.x * CallArt.Ppu), 6, 200);
        int hp = Mathf.Clamp(Mathf.RoundToInt(size.y * CallArt.Ppu), 6, 200);
        if (_frame == null) _frame = DiscordArt.Put(transform, DiscordArt.SpeakingFrame(wp, hp), Vector2.zero, size, DiscordArt.Green, Order, "Frame");
    }

    void Update()
    {
        if (_owner == null || Time.time >= _endAt)
        {
            Destroy(gameObject);
            return;
        }
        var w = _owner.GetComponent<IWarpable>();
        if (w != null) transform.position = w.BodyBounds.center;
        if (_frame != null) _frame.color = CallKitUtil.Alpha(DiscordArt.Green, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 10f)));
    }
}
