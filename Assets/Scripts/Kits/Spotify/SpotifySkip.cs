using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Spotify のボスの技2：スキップ（2026-11-19。docs/app-kits-2-design.md 4-2）。
/// ボスの頭に ⏭ の印 → プレイヤーの後ろ 2マスに赤い体の枠と赤い輪（半径 2.5マス。0.8秒）→ ボスがそこへ飛び、輪の判定（0.15秒）。
/// ・避け方：赤い輪の外へ（プレイヤーの向いている方へ 1マス弱）
/// ・第2形態：3回続ける。頭の上に 2 SKIPS LEFT・1 SKIP LEFT（飾り）
/// ・終わったら、もとの高さへ戻る。止められたらボスを見える状態に戻す（SpotifySkipMark の Restore）
/// </summary>
[CreateAssetMenu(fileName = "Spotify_Skip", menuName = "Team1/Kits/Spotify Skip")]
public class SpotifySkip : CustomAttack
{
    [SerializeField, Min(1)] int _skips = 1;
    [Tooltip("プレイヤーの後ろ（マス）")]
    [SerializeField, Min(0f)] float _behind = 2f;
    [Tooltip("輪の半径（マス）")]
    [SerializeField, Min(0.5f)] float _radius = 2.5f;
    [Tooltip("予告（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("輪の判定の時間")]
    [SerializeField, Min(0.05f)] float _hitWindow = 0.15f;
    [Tooltip("1回ずつの間（秒）")]
    [SerializeField, Min(0f)] float _gap = 0.35f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        BossController boss = owner.GetComponent<BossController>();
        float homeY = owner.transform.position.y;
        SpotifySkipMark mark = SpotifySkipMark.Spawn(owner, boss, homeY);
        mark.Icon(SpotifyArt.SkipIcon, null);
        yield return new WaitForSeconds(0.35f);
        for (int i = 0; i < _skips; i++)
        {
            int left = _skips - 1 - i;
            mark.Icon(SpotifyArt.SkipIcon, _skips > 1 ? (left == 1 ? "1 SKIP LEFT" : left == 0 ? "LAST SKIP" : $"{left} SKIPS LEFT") : null);
            Bounds body = boss != null ? boss.BodyBounds : new Bounds(owner.transform.position, new Vector3(2f, 2f, 0f));
            Vector2 dest = Destination(owner, arena);
            dest.y = Mathf.Max(dest.y, arena.yMin + body.size.y * 0.5f + 0.05f); // 床にめりこまない
            Vector2 offset = (Vector2)body.center - (Vector2)owner.transform.position;
            GameObject tg = owner.CreateSpawnedObject("Spotify_SkipTelegraph", dest);
            tg.AddComponent<SpotifyRingTelegraph>().Setup(body.size, _radius, _telegraph);
            float end = Time.time + _telegraph;
            while (Time.time < end)
            {
                float rem = end - Time.time;
                if (boss != null && rem < 0.15f) boss.SetVisibility(rem / 0.15f);
                yield return null;
            }
            if (tg != null) Destroy(tg);
            // 飛ぶ
            if (boss != null) boss.Teleport(dest - offset);
            else owner.transform.position = dest - offset;
            if (boss != null) boss.SetVisibility(1f);
            Strike(owner, data, dest);
            owner.NotifyReleased(data);
            yield return new WaitForSeconds(_hitWindow + _gap);
        }
        mark.Icon(null, null);
        // もとの高さへ戻る
        Vector3 from = owner.transform.position;
        float t0 = Time.time;
        while (Time.time - t0 < 0.4f)
        {
            float k = (Time.time - t0) / 0.4f;
            Vector3 p = owner.transform.position;
            p.y = Mathf.Lerp(from.y, homeY, 1f - (1f - k) * (1f - k));
            owner.transform.position = p;
            yield return null;
        }
        Vector3 q = owner.transform.position;
        q.y = homeY;
        owner.transform.position = q;
        if (mark != null) Destroy(mark.gameObject);
    }

    Vector2 Destination(AttackRunner owner, Rect arena)
    {
        Vector2 p = owner.TargetPoint ?? (Vector2)arena.center;
        var pc = owner.Target != null ? owner.Target.GetComponent<PlayerController>() : null;
        int facing = pc != null ? pc.FacingDirection : 1;
        float x = p.x - facing * _behind;
        // 壁に近ければ前へ
        if (x < arena.xMin + 1.2f || x > arena.xMax - 1.2f) x = p.x + facing * _behind;
        x = Mathf.Clamp(x, arena.xMin + 1.2f, arena.xMax - 1.2f);
        return new Vector2(x, Mathf.Max(p.y, arena.yMin + 1.2f));
    }

    void Strike(AttackRunner owner, AttackData data, Vector2 at)
    {
        GameObject go = owner.CreateSpawnedObject("Spotify_SkipRing", at);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = _radius;
        Hitbox hb = owner.AddHitbox(go, data, col);
        hb.Activate();
        FxKit.Ring(at, _radius * 2f, 0.25f, (Color)SpotifyArt.Green);
        FxKit.Ring(at, _radius * 1.2f, 0.2f, Color.white);
        Destroy(go, _hitWindow);
    }
}

/// <summary>赤い体の枠と、赤い輪（スキップの行き先の予告）。最後の 0.3秒は点滅</summary>
public class SpotifyRingTelegraph : CustomEffect
{
    SpriteRenderer[] _parts;
    float _end;

    public void Setup(Vector2 body, float radius, float seconds)
    {
        _end = Time.time + seconds;
        float t = 2f / 18f;
        Color red = SpotifyArt.Warn;
        int d = Mathf.Clamp(Mathf.RoundToInt(radius * 2f * 18f), 8, 120);
        Sprite ring = FxArt.Disc(d);
        _parts = new[]
        {
            AttackRunner.AddVisual(transform, ring, new Color(1f, 0.27f, 0.23f, 0.16f), Vector2.one * radius * 2f, 15, "Disc"),
            FxKit.Box(transform, new Vector2(0f, body.y * 0.5f), new Vector2(body.x, t), red, 25, "T"),
            FxKit.Box(transform, new Vector2(0f, -body.y * 0.5f), new Vector2(body.x, t), red, 25, "B"),
            FxKit.Box(transform, new Vector2(-body.x * 0.5f, 0f), new Vector2(t, body.y), red, 25, "L"),
            FxKit.Box(transform, new Vector2(body.x * 0.5f, 0f), new Vector2(t, body.y), red, 25, "R"),
        };
        // 輪のふち（12個の点）
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI * 2f / 16f;
            FxKit.Box(transform, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, new Vector2(0.22f, 0.22f), red, 16, "Dot" + i);
        }
        _parts = GetComponentsInChildren<SpriteRenderer>();
    }

    void Update()
    {
        float left = _end - Time.time;
        if (left <= -0.05f) { Destroy(gameObject); return; }
        bool on = left > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
        if (_parts != null) foreach (var p in _parts) if (p != null) p.enabled = on;
    }
}

/// <summary>ボスの頭の上の印（⏭ と残りの回数）。消えるとき（止められたとき）はボスを見える状態に戻す</summary>
public class SpotifySkipMark : CustomEffect
{
    BossController _boss;
    SpriteRenderer _icon;
    TextMesh _text;

    float _homeY;

    public static SpotifySkipMark Spawn(AttackRunner owner, BossController boss, float homeY)
    {
        GameObject go = owner.CreateSpawnedObject("Spotify_SkipMark", owner.transform.position);
        var m = go.AddComponent<SpotifySkipMark>();
        m._boss = boss;
        m._homeY = homeY;
        m._icon = RuntimeSprites.Show(go.transform, SpotifyArt.SkipIcon, Vector2.zero, 60, 1f, "Icon");
        m._text = WorldText.Create(go.transform, "", 0.4f, Color.white, 60, true, TextAnchor.MiddleCenter, "Left");
        m._text.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        return m;
    }

    public void Icon(Sprite s, string text)
    {
        if (_icon != null)
        {
            _icon.enabled = s != null;
            if (s != null) _icon.sprite = s;
        }
        if (_text != null) _text.text = text ?? "";
    }

    void LateUpdate()
    {
        if (_boss == null) return;
        Bounds b = _boss.BodyBounds;
        transform.position = new Vector3(b.center.x, b.max.y + 0.45f, 0f);
    }

    protected override void Restore()
    {
        if (_boss == null) return;
        if (_boss.State == BossState.Fighting) _boss.SetVisibility(1f);
        // 途中で止められたら、もとの高さへ戻す（浮くボスは横にしか動かないため）
        Vector3 p = _boss.transform.position;
        bool alive = _boss.State == BossState.Fighting || _boss.State == BossState.Transition;
        if (alive && p.y < _homeY - 0.3f) _boss.Teleport(new Vector2(p.x, _homeY));
    }
}
