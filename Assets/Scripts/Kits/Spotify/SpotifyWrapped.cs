using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Spotify のボスの技1：Wrapped（2026-11-19。docs/app-kits-2-design.md 4-2）。
/// 部屋を 5列（幅 4マス）に分ける。1・3・5 列に赤い印（0.9秒）→ 縦長のカード（#1・TOP 5・99%）が上からささる（床から天井まで。0.3秒）
/// → 0.8秒あと、2・4 列。
/// ・避け方：印の無い列へ
/// ・第2形態：そのあと「まん中の 1列だけ安全」（この1回だけ予告 1.5秒。壁から 8マス＜1.2秒×7）
/// ・カード1枚は SpotifyCardEffect（自分で進む。形態変化・正常化で消える）
/// </summary>
[CreateAssetMenu(fileName = "Spotify_Wrapped", menuName = "Team1/Kits/Spotify Wrapped")]
public class SpotifyWrapped : CustomAttack
{
    [SerializeField, Min(3)] int _columns = 5;
    [Tooltip("予告（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.9f;
    [Tooltip("1・3・5 から 2・4 まで（秒）")]
    [SerializeField, Min(0f)] float _gap = 0.8f;
    [Tooltip("第2形態：最後に「まん中だけ安全」を出す")]
    [SerializeField] bool _finale;
    [Tooltip("まん中だけ安全の予告（秒）")]
    [SerializeField, Min(0.8f)] float _finaleTelegraph = 1.5f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float w = arena.width / _columns;
        int card = Random.Range(0, 3);
        for (int i = 0; i < _columns; i += 2) Spawn(owner, data, arena, w, i, 0f, _telegraph, card++);
        for (int i = 1; i < _columns; i += 2) Spawn(owner, data, arena, w, i, _gap, _telegraph, card++);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_gap + _telegraph + SpotifyCardEffect.Life);
        if (!_finale) yield break;
        int mid = _columns / 2;
        for (int i = 0; i < _columns; i++) if (i != mid) Spawn(owner, data, arena, w, i, 0f, _finaleTelegraph, card++);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_finaleTelegraph + SpotifyCardEffect.Life);
    }

    static void Spawn(AttackRunner owner, AttackData data, Rect arena, float w, int index, float delay, float telegraph, int card)
    {
        float x = arena.xMin + w * (index + 0.5f);
        SpotifyCardEffect.Spawn(owner, data, new Rect(x - w * 0.5f, arena.yMin, w, arena.height), delay, telegraph, card);
    }
}

/// <summary>Wrapped のカード1枚（列の赤い枠 → 上からささる → うすくなる）</summary>
public class SpotifyCardEffect : CustomEffect
{
    public const float Drop = 0.12f, Stay = 0.3f, Fade = 0.25f;
    public static float Life => Drop + Stay + Fade;

    AttackRunner _owner;
    AttackData _data;
    Rect _col;
    float _delay, _telegraph;
    int _card;

    public static SpotifyCardEffect Spawn(AttackRunner owner, AttackData data, Rect column, float delay, float telegraph, int card)
    {
        GameObject go = owner.CreateSpawnedObject("Spotify_Card", column.center);
        var e = go.AddComponent<SpotifyCardEffect>();
        e._owner = owner;
        e._data = data;
        e._col = column;
        e._delay = delay;
        e._telegraph = telegraph;
        e._card = card;
        e.StartCoroutine(e.Run());
        return e;
    }

    IEnumerator Run()
    {
        if (_delay > 0f) yield return new WaitForSeconds(_delay);
        float inner = _col.width - 0.3f, h = _col.height;
        float t = 2f / 18f;
        Color red = SpotifyArt.Warn;
        var tg = new GameObject("Telegraph");
        tg.layer = gameObject.layer;
        tg.transform.SetParent(transform, false);
        SpriteRenderer[] parts =
        {
            FxKit.Box(tg.transform, Vector2.zero, new Vector2(inner, h), new Color(1f, 0.27f, 0.23f, 0.14f), 15, "Fill"),
            FxKit.Box(tg.transform, new Vector2(-inner * 0.5f, 0f), new Vector2(t, h), red, 16, "L"),
            FxKit.Box(tg.transform, new Vector2(inner * 0.5f, 0f), new Vector2(t, h), red, 16, "R"),
            FxKit.Box(tg.transform, new Vector2(0f, -h * 0.5f + 0.08f), new Vector2(inner, 0.16f), red, 16, "Floor"),
        };
        float end = Time.time + _telegraph;
        while (Time.time < end)
        {
            bool on = end - Time.time > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            foreach (var p in parts) p.enabled = on;
            yield return null;
        }
        Destroy(tg);

        // カード（地・ふち・大きい字）
        var cardGo = new GameObject("Card");
        cardGo.layer = gameObject.layer;
        cardGo.transform.SetParent(transform, false);
        Color col = SpotifyArt.CardColor(_card);
        SpriteRenderer face = FxKit.Box(cardGo.transform, Vector2.zero, new Vector2(inner - 0.1f, h), col, 18, "Face");
        FxKit.Box(cardGo.transform, new Vector2(0f, h * 0.5f - 0.6f), new Vector2(inner - 0.6f, 0.12f), new Color(1f, 1f, 1f, 0.5f), 19, "Line");
        TextMesh tm = WorldText.Create(cardGo.transform, SpotifyArt.CardText(_card), _card % 3 == 1 ? 0.6f : 0.9f, SpotifyArt.CardInk(_card), 20, true, TextAnchor.MiddleCenter, "Text");
        tm.transform.localPosition = new Vector3(0f, h * 0.18f, 0f);
        var hitGo = new GameObject("Hit");
        hitGo.layer = gameObject.layer;
        hitGo.transform.SetParent(transform, false);
        var rb = hitGo.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var box = hitGo.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(inner - 0.3f, h);
        Hitbox hb = _owner != null ? _owner.AddHitbox(hitGo, _data, box) : null;
        if (hb != null) hb.Activate();
        if (_owner != null) _owner.NotifyReleased(_data);

        float start = Time.time;
        while (Time.time - start < Drop)
        {
            float k = (Time.time - start) / Drop;
            cardGo.transform.localPosition = new Vector3(0f, (1f - k) * h, 0f);
            yield return null;
        }
        cardGo.transform.localPosition = Vector3.zero;
        yield return new WaitForSeconds(Stay);
        if (hb != null) hb.Deactivate();
        start = Time.time;
        while (Time.time - start < Fade)
        {
            float k = 1f - (Time.time - start) / Fade;
            foreach (var sr in cardGo.GetComponentsInChildren<SpriteRenderer>()) { Color c = sr.color; c.a = Mathf.Min(c.a, k); sr.color = c; }
            Color ink = SpotifyArt.CardInk(_card);
            ink.a = k;
            WorldText.SetColor(tm, ink);
            yield return null;
        }
        Destroy(gameObject);
    }
}
