using UnityEngine;

/// <summary>
/// 【仮】じっくり考えるの進行（2026-11-19。docs/app-kits-1-design.md 6章 P2）。ClaudeThinkingAttack が出す。担当3 の物。
/// ボスの頭の上で火花が回る → 0.6秒おきにプレイヤーの足元へ印（赤い輪と数字。中身は ZoneStrike の区画）→ 自分は消える。
/// 印（区画）はそれぞれ自分で進む（CustomEffect なので、形態変化・正常化で消える）
/// </summary>
public class ClaudeThinkingEffect : CustomEffect
{
    const int SpinOrder = 33;

    AttackRunner _owner;
    AttackData _data;
    ClaudeThinkingAttack _cfg;
    float _start;
    int _placed;
    Transform _spin;
    readonly SpriteRenderer[] _orbit = new SpriteRenderer[3];

    public static ClaudeThinkingEffect Spawn(AttackRunner owner, AttackData data, ClaudeThinkingAttack cfg)
    {
        if (owner == null || data == null || cfg == null) return null;
        Bounds body = owner.TryGetComponent(out BossController boss) ? boss.BodyBounds : new Bounds(owner.OriginPoint, Vector3.one);
        GameObject go = owner.CreateSpawnedObject("ClaudeThinking", new Vector2(body.center.x, body.max.y + 0.9f));
        var fx = go.AddComponent<ClaudeThinkingEffect>();
        fx._owner = owner;
        fx._data = data;
        fx._cfg = cfg;
        fx._start = Time.time;
        fx.Build();
        return fx;
    }

    void Build()
    {
        var spin = new GameObject("Spin");
        spin.layer = gameObject.layer;
        spin.transform.SetParent(transform, false);
        _spin = spin.transform;
        RuntimeSprites.Show(_spin, ClaudeArt.Spark(13), Vector2.zero, SpinOrder, 1.6f, "Spark");
        for (int i = 0; i < _orbit.Length; i++)
            _orbit[i] = RuntimeSprites.Show(transform, ClaudeArt.Spark(5), Vector2.zero, SpinOrder + 1, 1f, "Orbit" + i);
    }

    void Update()
    {
        if (_owner == null)
        {
            Destroy(gameObject);
            return;
        }
        float t = Time.time - _start;
        // 回る火花（だんだん速く）
        if (_spin != null) _spin.localRotation = Quaternion.Euler(0f, 0f, -(t * 200f + t * t * 120f));
        for (int i = 0; i < _orbit.Length; i++)
        {
            if (_orbit[i] == null) continue;
            float a = t * 5f + i * Mathf.PI * 2f / _orbit.Length;
            _orbit[i].transform.localPosition = new Vector3(Mathf.Cos(a) * 0.75f, Mathf.Sin(a) * 0.3f, 0f);
        }

        // 印を付ける
        while (_placed < _cfg.Marks && t >= _cfg.MarkInterval * (_placed + 1))
        {
            PlaceMark(_placed);
            _placed++;
        }

        if (t >= _cfg.ThinkTime && _placed >= _cfg.Marks) Destroy(gameObject);
    }

    float FireStart => Mathf.Max(_cfg.ThinkTime, _cfg.MarkInterval * _cfg.Marks);

    void PlaceMark(int k)
    {
        Vector2 at = _owner.TargetPoint ?? _owner.OriginPoint;
        _owner.TryFindGround(at.x, at.y + 0.1f, out float y);
        Rect arena = BattleArena.GetBounds();
        float w = _cfg.PillarSize.x, h = _cfg.PillarSize.y;
        float x = arena.Contains(at) ? Mathf.Clamp(at.x, arena.xMin + w * 0.5f, arena.xMax - w * 0.5f) : at.x;
        float fireAt = _start + FireStart + k * _cfg.PillarInterval;
        float telegraph = Mathf.Max(0.8f, fireAt - Time.time);
        var zone = new Rect(x - w * 0.5f, y, w, h);
        ZoneStrikeZone z = ZoneStrike.Strike(_owner, _data, zone, 0f, telegraph, _cfg.Active, ClaudeArt.Beam, Color.white);
        if (z == null) return;
        // 足元の赤い輪と数字（区画の子なので、区画といっしょに消える）
        SpriteRenderer ring = RuntimeSprites.Show(z.transform, ClaudeArt.MarkRing, Vector2.zero, 18, 1f, "Ring");
        ring.transform.position = new Vector3(x, y + 0.12f, 0f);
        TextMesh num = WorldText.Create(z.transform, (k + 1).ToString(), 0.6f, ClaudeArt.Red, 19, true, TextAnchor.MiddleCenter, "Num");
        num.transform.position = new Vector3(x, y + 0.75f, 0f);
        FxKit.Ring(new Vector2(x, y + 0.3f), 1.6f, 0.25f, ClaudeArt.Red);
        z.Fired += _ =>
        {
            if (ring != null) ring.enabled = false;
            if (num != null) num.gameObject.SetActive(false);
        };
    }
}
