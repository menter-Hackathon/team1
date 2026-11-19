using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】ミュージックのボスの技1：イコライザー（2026-11-19。docs/app-kits-2-design.md 4-1）。
/// 床を 8列（幅 2.5マス）に分ける。奇数の列に赤い印（0.8秒）→ 棒が上がる（高さ 4〜7マス。0.3秒）→ 0.6秒あと、偶数の列も同じ。
/// ・避け方：印の無い列へ。足場の上も当たる（棒は 4マス以上）
/// ・第2形態：左から右へ波（1列ずつ 0.2秒おき。各列の予告は 0.8秒）を 1回、そのあと奇数・偶数
/// ・列1つは MusicBarEffect（自分で進む）。音は読まない（決まった秒数）
/// </summary>
[CreateAssetMenu(fileName = "Music_Equalizer", menuName = "Team1/Kits/Music Equalizer")]
public class MusicEqualizer : CustomAttack
{
    [SerializeField, Min(2)] int _columns = 8;
    [Tooltip("予告（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("奇数の列から偶数の列まで（秒）")]
    [SerializeField, Min(0f)] float _gap = 0.6f;
    [Tooltip("棒の高さ（マス）")]
    [SerializeField] Vector2 _height = new Vector2(4f, 7f);
    [Tooltip("第2形態：先に左から右への波を出す")]
    [SerializeField] bool _sweep;
    [Tooltip("波の1列ずつの間（秒）")]
    [SerializeField, Min(0.05f)] float _sweepStep = 0.2f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float w = arena.width / _columns;
        float total = 0f;
        if (_sweep)
        {
            for (int i = 0; i < _columns; i++)
            {
                Spawn(owner, data, arena, w, i, i * _sweepStep);
            }
            total = (_columns - 1) * _sweepStep + _telegraph + MusicBarEffect.Life;
            owner.NotifyReleased(data);
            yield return new WaitForSeconds(total);
        }
        for (int i = 0; i < _columns; i += 2) Spawn(owner, data, arena, w, i, 0f);
        for (int i = 1; i < _columns; i += 2) Spawn(owner, data, arena, w, i, _gap);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_gap + _telegraph + MusicBarEffect.Life);
    }

    void Spawn(AttackRunner owner, AttackData data, Rect arena, float w, int index, float delay)
    {
        float x = arena.xMin + w * (index + 0.5f);
        float h = Random.Range(_height.x, _height.y);
        MusicBarEffect.Spawn(owner, data, new Vector2(x, arena.yMin), w, h, delay, _telegraph);
    }
}

/// <summary>イコライザーの棒1本（予告の赤い枠 → 上がる → 下がる）。形態変化・正常化で消える</summary>
public class MusicBarEffect : CustomEffect
{
    /// <summary>上がって下がるまでの秒数</summary>
    public const float Rise = 0.3f, Hold = 0.15f, Fall = 0.2f;
    public static float Life => Rise + Hold + Fall;
    const float SegH = 0.35f;

    AttackRunner _owner;
    AttackData _data;
    float _w, _h, _delay, _telegraph;
    Vector2 _base;

    public static MusicBarEffect Spawn(AttackRunner owner, AttackData data, Vector2 floorCenter, float width, float height, float delay, float telegraph)
    {
        GameObject go = owner.CreateSpawnedObject("Music_Bar", floorCenter);
        var e = go.AddComponent<MusicBarEffect>();
        e._owner = owner;
        e._data = data;
        e._w = width;
        e._h = height;
        e._delay = delay;
        e._telegraph = telegraph;
        e._base = floorCenter;
        e.StartCoroutine(e.Run());
        return e;
    }

    IEnumerator Run()
    {
        if (_delay > 0f) yield return new WaitForSeconds(_delay);
        // 予告：列の赤い枠と、床の赤い帯
        float inner = _w - 0.2f;
        var tg = new GameObject("Telegraph");
        tg.layer = gameObject.layer;
        tg.transform.SetParent(transform, false);
        tg.transform.localPosition = new Vector3(0f, _h * 0.5f, 0f);
        SpriteRenderer fill = FxKit.Box(tg.transform, Vector2.zero, new Vector2(inner, _h), new Color(1f, 0.27f, 0.23f, 0.16f), 15, "Fill");
        SpriteRenderer floor = FxKit.Box(tg.transform, new Vector2(0f, -_h * 0.5f + 0.08f), new Vector2(inner, 0.16f), (Color)MusicArt.Warn, 16, "Floor");
        SpriteRenderer l = FxKit.Box(tg.transform, new Vector2(-inner * 0.5f, 0f), new Vector2(2f / 18f, _h), (Color)MusicArt.Warn, 16, "L");
        SpriteRenderer r = FxKit.Box(tg.transform, new Vector2(inner * 0.5f, 0f), new Vector2(2f / 18f, _h), (Color)MusicArt.Warn, 16, "R");
        SpriteRenderer t = FxKit.Box(tg.transform, new Vector2(0f, _h * 0.5f), new Vector2(inner, 2f / 18f), (Color)MusicArt.Warn, 16, "T");
        float end = Time.time + _telegraph;
        while (Time.time < end)
        {
            bool on = end - Time.time > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            fill.enabled = floor.enabled = l.enabled = r.enabled = t.enabled = on;
            yield return null;
        }
        Destroy(tg);

        // 棒（段の四角を積む）と判定
        int segs = Mathf.Max(1, Mathf.CeilToInt(_h / SegH));
        var seg = new SpriteRenderer[segs];
        for (int i = 0; i < segs; i++)
        {
            seg[i] = FxKit.Box(transform, new Vector2(0f, i * SegH + SegH * 0.5f), new Vector2(inner - 0.1f, SegH - 2f / 18f), MusicArt.BarColor((float)i / segs), 18, "Seg" + i);
            seg[i].enabled = false;
        }
        var hitGo = new GameObject("Hit");
        hitGo.layer = gameObject.layer;
        hitGo.transform.SetParent(transform, false);
        var rb = hitGo.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var box = hitGo.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        Hitbox hb = _owner != null ? _owner.AddHitbox(hitGo, _data, box) : null;
        if (hb != null) hb.Activate();

        float start = Time.time;
        while (Time.time - start < Rise + Hold + Fall)
        {
            float a = Time.time - start;
            float k = a < Rise ? a / Rise : a < Rise + Hold ? 1f : 1f - (a - Rise - Hold) / Fall;
            k = Mathf.Clamp01(k);
            float cur = _h * (a < Rise ? 1f - (1f - k) * (1f - k) : k);
            int shown = Mathf.CeilToInt(cur / SegH);
            for (int i = 0; i < segs; i++) seg[i].enabled = i < shown;
            if (a < Rise + Hold)
            {
                box.size = new Vector2(inner - 0.2f, Mathf.Max(0.1f, cur));
                box.offset = new Vector2(0f, cur * 0.5f);
            }
            else if (hb != null && hb.IsActive) hb.Deactivate();
            yield return null;
        }
        Destroy(gameObject);
    }
}
