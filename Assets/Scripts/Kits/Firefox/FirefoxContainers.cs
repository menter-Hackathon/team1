using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Firefox のボスの技2：コンテナタブ（2026-11-19。docs/app-kits-2-design.md 4-6）。
/// 床が 4色の区画（幅 5マス。青・オレンジ・緑・ピンク。床に色の線）。ボスの頭の上のタブに色が出る
/// → その色の区画に赤い枠（1.2秒）→ 炎の柱（床から天井まで）。2回。
/// ・避け方：赤い枠の区画から出る
/// ・第2形態：2色を同時に。3回。区画の並びが毎回変わる。プレイヤーのいる区画か、となりの区画の1つは必ず安全
/// </summary>
[CreateAssetMenu(fileName = "Firefox_Containers", menuName = "Team1/Kits/Firefox Containers")]
public class FirefoxContainers : CustomAttack
{
    [SerializeField, Min(1)] int _rounds = 2;
    [Tooltip("1回に燃やす色の数")]
    [SerializeField, Min(1)] int _colors = 1;
    [Tooltip("予告（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 1.2f;
    [Tooltip("タブの色を見せてから赤い枠までの秒数")]
    [SerializeField, Min(0f)] float _lead = 0.35f;
    [Tooltip("区画の並びを毎回変える")]
    [SerializeField] bool _reshuffle;

    const int Zones = 4;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float w = arena.width / Zones;
        int[] order = Shuffled();
        FirefoxContainerBoard board = FirefoxContainerBoard.Spawn(owner, arena, order);
        for (int r = 0; r < _rounds; r++)
        {
            if (r > 0 && _reshuffle)
            {
                order = Shuffled();
                board.SetOrder(order);
                yield return new WaitForSeconds(0.3f);
            }
            List<int> zones = PickZones(owner, arena, w);
            var tabColors = new List<Color>();
            foreach (int z in zones) tabColors.Add(FirefoxArt.Containers[order[z]]);
            board.ShowTabs(tabColors);
            yield return new WaitForSeconds(_lead);
            foreach (int z in zones)
            {
                FirefoxPillar.Spawn(owner, data, new Rect(arena.xMin + w * z, arena.yMin, w, arena.height), _telegraph, FirefoxArt.Containers[order[z]]);
            }
            owner.NotifyReleased(data);
            yield return new WaitForSeconds(_telegraph + FirefoxPillar.Life + 0.15f);
        }
        if (board != null) Destroy(board.gameObject);
    }

    static int[] Shuffled()
    {
        int[] o = { 0, 1, 2, 3 };
        for (int i = o.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (o[i], o[j]) = (o[j], o[i]);
        }
        return o;
    }

    /// <summary>燃やす区画（プレイヤーのいる区画か、となりの1つは必ず残す）</summary>
    List<int> PickZones(AttackRunner owner, Rect arena, float w)
    {
        float px = owner.TargetPoint.HasValue ? owner.TargetPoint.Value.x : arena.center.x;
        int pz = Mathf.Clamp(Mathf.FloorToInt((px - arena.xMin) / w), 0, Zones - 1);
        int n = Mathf.Clamp(_colors, 1, Zones - 1);
        for (int tries = 0; tries < 20; tries++)
        {
            var pick = new List<int>();
            var pool = new List<int> { 0, 1, 2, 3 };
            for (int i = 0; i < n; i++)
            {
                int k = Random.Range(0, pool.Count);
                pick.Add(pool[k]);
                pool.RemoveAt(k);
            }
            bool safe = false;
            for (int z = pz - 1; z <= pz + 1; z++)
                if (z >= 0 && z < Zones && !pick.Contains(z)) safe = true;
            if (safe) return pick;
        }
        // 念のため：プレイヤーの区画から一番遠い所
        return new List<int> { pz < Zones / 2 ? Zones - 1 : 0 };
    }
}

/// <summary>床の色の線と、ボスの頭の上のタブ（技の間だけ）。形態変化・正常化で消える</summary>
public class FirefoxContainerBoard : CustomEffect
{
    Rect _arena;
    readonly List<SpriteRenderer> _lines = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> _tabs = new List<SpriteRenderer>();
    Transform _boss;
    Transform _tabRoot;

    public static FirefoxContainerBoard Spawn(AttackRunner owner, Rect arena, int[] order)
    {
        GameObject go = owner.CreateSpawnedObject("Firefox_Containers", arena.center);
        var b = go.AddComponent<FirefoxContainerBoard>();
        b._arena = arena;
        b._boss = owner.transform;
        float w = arena.width / order.Length;
        for (int i = 0; i < order.Length; i++)
        {
            Vector2 local = new Vector2(arena.xMin + w * (i + 0.5f), arena.yMin + 0.06f) - arena.center;
            b._lines.Add(FxKit.Box(go.transform, local, new Vector2(w - 0.25f, 0.12f), FirefoxArt.Containers[order[i]], 14, "Line" + i));
        }
        var tr = new GameObject("Tabs");
        tr.layer = go.layer;
        tr.transform.SetParent(go.transform, false);
        b._tabRoot = tr.transform;
        return b;
    }

    public void SetOrder(int[] order)
    {
        for (int i = 0; i < _lines.Count && i < order.Length; i++) _lines[i].color = FirefoxArt.Containers[order[i]];
    }

    /// <summary>ボスの頭の上に、色つきのタブを並べる</summary>
    public void ShowTabs(List<Color> colors)
    {
        foreach (Transform c in _tabRoot) Destroy(c.gameObject);
        _tabs.Clear();
        for (int i = 0; i < colors.Count; i++)
        {
            var tab = new GameObject("Tab" + i);
            tab.layer = gameObject.layer;
            tab.transform.SetParent(_tabRoot, false);
            tab.transform.localPosition = new Vector3((i - (colors.Count - 1) * 0.5f) * 1.9f, 0f, 0f);
            FxKit.Box(tab.transform, Vector2.zero, new Vector2(1.7f, 0.6f), new Color(0.17f, 0.16f, 0.2f, 0.95f), 58, "Back");
            _tabs.Add(FxKit.Box(tab.transform, new Vector2(0f, 0.27f), new Vector2(1.7f, 0.12f), colors[i], 59, "Color"));
            FxKit.Box(tab.transform, new Vector2(-0.55f, -0.02f), new Vector2(0.3f, 0.3f), colors[i], 59, "Dot");
            FxKit.Box(tab.transform, new Vector2(0.2f, -0.02f), new Vector2(0.8f, 0.1f), new Color(1f, 1f, 1f, 0.6f), 59, "Title");
        }
    }

    void LateUpdate()
    {
        if (_boss == null || _tabRoot == null) return;
        BossController b = _boss.GetComponent<BossController>();
        Vector3 top = b != null ? new Vector3(b.BodyBounds.center.x, b.BodyBounds.max.y + 0.6f, 0f) : _boss.position + Vector3.up * 2f;
        _tabRoot.position = top;
    }
}

/// <summary>区画の炎の柱1本（赤い枠 → 炎の柱）。形態変化・正常化で消える</summary>
public class FirefoxPillar : CustomEffect
{
    public const float Burn = 0.35f, Fade = 0.2f;
    public static float Life => Burn + Fade;

    AttackRunner _owner;
    AttackData _data;
    Rect _zone;
    float _telegraph;
    Color _tint;

    public static FirefoxPillar Spawn(AttackRunner owner, AttackData data, Rect zone, float telegraph, Color tint)
    {
        GameObject go = owner.CreateSpawnedObject("Firefox_Pillar", zone.center);
        var p = go.AddComponent<FirefoxPillar>();
        p._owner = owner;
        p._data = data;
        p._zone = zone;
        p._telegraph = telegraph;
        p._tint = tint;
        p.StartCoroutine(p.Run());
        return p;
    }

    IEnumerator Run()
    {
        float inner = _zone.width - 0.25f, h = _zone.height, t = 2f / 18f;
        Color red = FirefoxArt.Red;
        var tg = new GameObject("Telegraph");
        tg.layer = gameObject.layer;
        tg.transform.SetParent(transform, false);
        SpriteRenderer[] parts =
        {
            FxKit.Box(tg.transform, Vector2.zero, new Vector2(inner, h), new Color(1f, 0.27f, 0.23f, 0.15f), 15, "Fill"),
            FxKit.Box(tg.transform, new Vector2(-inner * 0.5f, 0f), new Vector2(t, h), red, 16, "L"),
            FxKit.Box(tg.transform, new Vector2(inner * 0.5f, 0f), new Vector2(t, h), red, 16, "R"),
            FxKit.Box(tg.transform, new Vector2(0f, -h * 0.5f + 0.1f), new Vector2(inner, 0.2f), red, 16, "Floor"),
            FxKit.Box(tg.transform, new Vector2(0f, -h * 0.5f + 0.3f), new Vector2(inner, 0.1f), _tint, 16, "Tint"),
        };
        float end = Time.time + _telegraph;
        while (Time.time < end)
        {
            bool on = end - Time.time > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            foreach (var p in parts) p.enabled = on;
            yield return null;
        }
        Destroy(tg);

        // 炎の柱
        SpriteRenderer fire = AttackRunner.AddVisual(transform, FirefoxArt.PillarStrip, Color.white, new Vector2(inner - 0.1f, h), 18, "Fire");
        SpriteRenderer core = FxKit.Box(transform, new Vector2(0f, -h * 0.25f), new Vector2(inner * 0.4f, h * 0.5f), new Color(1f, 0.97f, 0.85f, 0.7f), 19, "Core");
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
        Vector3 baseScale = fire.transform.localScale;
        while (Time.time - start < Burn)
        {
            float k = (Time.time - start) / Burn;
            fire.transform.localScale = new Vector3(baseScale.x * (0.9f + 0.1f * Mathf.Sin(Time.time * 40f)), baseScale.y, 1f);
            core.enabled = Mathf.FloorToInt(Time.time / 0.05f) % 2 == 0;
            yield return null;
        }
        if (hb != null) hb.Deactivate();
        start = Time.time;
        while (Time.time - start < Fade)
        {
            float k = 1f - (Time.time - start) / Fade;
            fire.color = new Color(1f, 1f, 1f, k);
            core.color = new Color(1f, 0.97f, 0.85f, 0.7f * k);
            yield return null;
        }
        Destroy(gameObject);
    }
}
