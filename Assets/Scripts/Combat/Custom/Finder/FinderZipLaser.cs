using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】解凍レーザー（zip圧縮の4段目。2026-11-19。docs/app-feedback-v2-design.md 4-2）。FinderZipHolder が出す。
/// ・正面へ横一直線、画面の端まで。太さは吸いこんだ数で 1.2・1.6・2.0。0.4秒。貫通。1回の判定なので、同じ相手には1回だけ当たる
///   （雑魚 3、ボスのゲージ +24 を1回）
/// ・撃っている間もプレイヤーは動ける。向きは撃った瞬間に固定。帯の根元はプレイヤーに付いてくる
/// ・見た目：Finder の青 #0A84FF の帯（芯は白）。帯の中を小さい書類が先へ流れる。頭の上に進みのバーが 0.4秒で満タン（解凍中）。
///   最後に帯が細くなって消える
/// </summary>
public class FinderZipLaser : CustomEffect
{
    const int OuterOrder = 24, CoreOrder = 25, DocOrder = 26, BarOrder = 52;
    const float DocSpeed = 26f;
    const int DocCount = 7;

    AttackRunner _owner;
    Func<Vector2> _muzzle;
    int _facing;
    float _width;
    float _start, _duration;
    Vector2 _lastMuzzle;
    Hitbox _hitbox;
    BoxCollider2D _box;
    Transform _beam;
    SpriteRenderer _outer, _core, _flare;
    readonly List<SpriteRenderer> _docs = new List<SpriteRenderer>();
    MacProgress _bar;

    /// <summary>レーザーを出す。muzzle は帯の根元（毎フレーム聞く。プレイヤーに付いてくる）</summary>
    public static FinderZipLaser Spawn(AttackRunner owner, AttackData data, Func<Vector2> muzzle, int facing, float width, float duration)
    {
        if (owner == null || data == null || muzzle == null) return null;
        Vector2 at = muzzle();
        GameObject go = owner.CreateSpawnedObject("FinderZipLaser", at);
        var e = go.AddComponent<FinderZipLaser>();
        e._owner = owner;
        e._muzzle = muzzle;
        e._facing = facing >= 0 ? 1 : -1;
        e._width = Mathf.Max(0.2f, width);
        e._start = Time.time;
        e._duration = Mathf.Max(0.05f, duration);
        e._lastMuzzle = at;
        e.Build(data);
        e.Tick();
        if (e._hitbox != null) e._hitbox.Activate();
        return e;
    }

    void Build(AttackData data)
    {
        var hitGo = new GameObject("LaserHit");
        hitGo.layer = gameObject.layer;
        hitGo.transform.SetParent(transform, false);
        var rb = hitGo.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        _box = hitGo.AddComponent<BoxCollider2D>();
        _box.isTrigger = true;
        _box.size = new Vector2(1f, _width);
        _hitbox = _owner.AddHitbox(hitGo, data, _box);
        _hitbox.SetErrorKind("ZipLaser");

        _beam = FinderArt.Child(transform, "Beam", transform.position);
        _outer = AttackRunner.AddVisual(_beam, PlaceholderSprites.Square, new Color(FinderArt.Blue.r, FinderArt.Blue.g, FinderArt.Blue.b, 0.9f), Vector2.one, OuterOrder, "Outer");
        _core = AttackRunner.AddVisual(_beam, PlaceholderSprites.Square, Color.white, Vector2.one, CoreOrder, "Core");
        _flare = RuntimeSprites.Show(transform, FinderArt.Disc(Mathf.RoundToInt(_width * 18f) + 4), Vector2.zero, CoreOrder + 1, 1f, "Flare");
        for (int i = 0; i < DocCount; i++) _docs.Add(RuntimeSprites.Show(_beam, FinderArt.Shot(i % 3), Vector2.zero, DocOrder, 1f, "Doc"));

        // 頭の上の進みのバー（解凍中）
        Transform head = FinderArt.Child(transform, "UnzipBar", transform.position);
        _bar = MacUi.PlaceProgress(head, Vector2.zero, 20, 4, MacLook.Light, BarOrder, FinderArt.Blue);
    }

    void Update()
    {
        Tick();
        if (Time.time - _start >= _duration)
        {
            if (_hitbox != null) _hitbox.Deactivate();
            Destroy(gameObject);
        }
    }

    void Tick()
    {
        float t = Mathf.Clamp01((Time.time - _start) / _duration);
        Vector2 m = _muzzle != null ? SafeMuzzle() : _lastMuzzle;
        _lastMuzzle = m;
        Rect view = ScreenUi.ViewRect();
        if (view.width < 1f) view = BattleArena.GetBounds();
        float end = _facing > 0 ? view.xMax + 0.5f : view.xMin - 0.5f;
        float len = Mathf.Max(0.5f, Mathf.Abs(end - m.x));
        Vector2 center = new Vector2(m.x + _facing * len * 0.5f, m.y);

        transform.position = m;
        if (_box != null)
        {
            _box.transform.position = center;
            _box.size = new Vector2(len, _width);
        }

        // 見た目：最後の25%で細くなって消える
        float thin = t < 0.75f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.75f) / 0.25f);
        float grow = Mathf.Clamp01((Time.time - _start) / 0.05f); // 出た瞬間に伸びる
        float shownLen = len * grow;
        float h = _width * thin;
        _beam.position = new Vector3(m.x + _facing * shownLen * 0.5f, m.y, 0f);
        SetSize(_outer, new Vector2(shownLen, Mathf.Max(0.01f, h)));
        SetSize(_core, new Vector2(shownLen, Mathf.Max(0.01f, h * 0.35f)));
        if (_flare != null)
        {
            _flare.transform.localScale = Vector3.one * (0.8f + 0.25f * Mathf.Sin(Time.time * 60f)) * thin;
        }
        // 書類が先へ流れる
        for (int i = 0; i < _docs.Count; i++)
        {
            float phase = Mathf.Repeat((Time.time - _start) * DocSpeed + i * len / _docs.Count, Mathf.Max(0.1f, shownLen));
            float wob = Mathf.Sin((Time.time * 20f) + i * 1.7f) * h * 0.25f;
            _docs[i].transform.localPosition = new Vector3(-_facing * shownLen * 0.5f + _facing * phase, wob, 0f);
            _docs[i].enabled = thin > 0.3f;
        }
        // 進みのバー：頭の上（体の上 1.1マス）
        if (_bar != null)
        {
            _bar.transform.parent.position = new Vector3(m.x - _facing * 0.4f, m.y + 1.1f, 0f);
            _bar.Value = Mathf.Clamp01((Time.time - _start) / _duration);
        }
    }

    Vector2 SafeMuzzle()
    {
        try { return _muzzle(); }
        catch (Exception) { _muzzle = null; return _lastMuzzle; }
    }

    static void SetSize(SpriteRenderer sr, Vector2 size)
    {
        if (sr == null) return;
        Vector2 native = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(size.x / Mathf.Max(native.x, 0.001f), size.y / Mathf.Max(native.y, 0.001f), 1f);
    }
}
