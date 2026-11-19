using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】レーザー（メッセージのボスの技 P2。2026-11-19）の場に出る物。MessagesLaserAttack が出す。
/// 流れ：予告（ボスの上から床の端へ赤い細い線・床に沿った赤い細い線・進む向きの矢印）→ 光の帯（緑と紫）が床をなぞって反対の端まで振れる → 消える。
/// 判定：床の上の光の点（回さない箱）と、帯が足場の上の高さを通る所（足場の上に立っていると当たる）。
/// CustomEffect なので、形態変化・正常化で消える。地形もプレイヤーも変えないので、戻す物はない
/// </summary>
public class MessagesLaserEffect : CustomEffect
{
    const int LineOrder = 17, BeamOrder = 26, SpotOrder = 27;

    AttackRunner _owner;
    AttackData _data;
    MessagesLaserAttack _attack;
    Vector2 _origin;
    float _floorY, _startX, _endX;
    float _start, _sweepAt, _endAt;
    readonly List<GameObject> _tele = new List<GameObject>();
    Transform _beam, _spot;
    SpriteRenderer _outer, _core;
    Hitbox _spotHit;
    readonly List<(Bounds plat, Transform t, Hitbox h)> _platHits = new List<(Bounds, Transform, Hitbox)>();
    bool _sweeping;

    public static MessagesLaserEffect Spawn(AttackRunner owner, AttackData data, MessagesLaserAttack attack)
    {
        if (owner == null || data == null || attack == null) return null;
        GameObject go = owner.CreateSpawnedObject("Messages_Laser", owner.OriginPoint);
        var e = go.AddComponent<MessagesLaserEffect>();
        e._owner = owner;
        e._data = data;
        e._attack = attack;
        e.Build();
        return e;
    }

    void Build()
    {
        Rect arena = BattleArena.GetBounds();
        Bounds body = TalkKitUtil.OwnerBody(_owner);
        _floorY = TalkKitUtil.FloorY(arena.center.x, arena.yMin + 2.4f);
        // 光はボスの上の高い所から出す（足場より十分高く。帯が足場の上を通るように）
        _origin = new Vector2(body.center.x, Mathf.Max(body.max.y + 1.2f, _floorY + 6f));
        float px = _owner.TargetPoint?.x ?? arena.center.x;
        float left = arena.xMin + 0.5f, right = arena.xMax - 0.5f;
        // プレイヤーから遠い方の端から始める（光の点が近づいてくるのが見える）
        bool fromLeft = Mathf.Abs(px - left) > Mathf.Abs(px - right);
        _startX = fromLeft ? left : right;
        _endX = fromLeft ? right : left;
        _start = Time.time;
        _sweepAt = _start + _attack.Telegraph;
        _endAt = _sweepAt + _attack.Sweep;

        // 予告：ボスの上から始まりの端へ赤い線、床に沿って赤い線、進む向きの矢印
        Vector2 startPt = new Vector2(_startX, _floorY + 0.1f);
        _tele.Add(KitDress.Line(_owner, _origin, startPt, _attack.Telegraph, 2f));
        _tele.Add(KitDress.Line(_owner, new Vector2(left, _floorY + 0.08f), new Vector2(right, _floorY + 0.08f), _attack.Telegraph, 2f));
        float dir = Mathf.Sign(_endX - _startX);
        for (int i = 0; i < 3; i++)
        {
            Vector2 at = startPt + new Vector2(dir * (0.8f + i * 0.7f), 0.5f);
            var arrow = TalkArt.Child(transform, "Arrow", at);
            var sr = TalkArt.ShowNative(arrow, ArrowSprite, Vector2.zero, LineOrder + 1, 1.5f, "Arrow");
            sr.color = TalkArt.Red;
            sr.flipX = dir < 0f;
            arrow.gameObject.AddComponent<SlackBlink>();
            _tele.Add(arrow.gameObject);
        }

        // 光の出る所（ボスの上に光る点。予告の間から見せる）
        var emitter = TalkArt.Child(transform, "Emitter", _origin);
        TalkArt.Show(emitter, TalkArt.Disc(16), Vector2.zero, new Vector2(0.9f, 0.9f), TalkArt.Fade((Color)MessagesArt.LaserGreen, 0.7f), BeamOrder + 2, "Glow");
        TalkArt.Show(emitter, TalkArt.Disc(8), Vector2.zero, new Vector2(0.4f, 0.4f), Color.white, BeamOrder + 3, "Core");

        // 帯と光の点（振れるまで隠す）
        _beam = TalkArt.Child(transform, "Beam", _origin);
        _outer = TalkArt.Show(_beam, PlaceholderSprites.Square, Vector2.zero, new Vector2(1f, _attack.BeamWidth), TalkArt.Fade((Color)MessagesArt.LaserPurple, 0.55f), BeamOrder, "Outer");
        _core = TalkArt.Show(_beam, PlaceholderSprites.Square, Vector2.zero, new Vector2(1f, _attack.BeamWidth * 0.35f), (Color)MessagesArt.LaserGreen, BeamOrder + 1, "Core");
        _beam.gameObject.SetActive(false);

        _spot = TalkArt.Child(transform, "Spot", startPt);
        TalkArt.Show(_spot, TalkArt.Disc(24), new Vector2(0f, _attack.Spot.y * 0.5f), new Vector2(_attack.Spot.x * 1.4f, _attack.Spot.y * 1.4f), TalkArt.Fade((Color)MessagesArt.LaserGreen, 0.6f), SpotOrder, "Glow");
        TalkArt.Show(_spot, TalkArt.Disc(12), new Vector2(0f, _attack.Spot.y * 0.5f), new Vector2(_attack.Spot.x * 0.6f, _attack.Spot.y * 0.6f), Color.white, SpotOrder + 1, "Core");
        var box = TalkKitUtil.AddTrigger<BoxCollider2D>(_spot.gameObject);
        box.size = _attack.Spot;
        box.offset = new Vector2(0f, _attack.Spot.y * 0.5f);
        _spotHit = _owner.AddHitbox(_spot.gameObject, _data, box);
        _spot.gameObject.SetActive(false);

        // 足場ごとに、帯が足場の上の高さを通る所の判定
        foreach (Bounds p in TalkKitUtil.Platforms(arena))
        {
            var t = TalkArt.Child(transform, "PlatformHit", p.center);
            var c = TalkKitUtil.AddTrigger<BoxCollider2D>(t.gameObject);
            c.size = new Vector2(0.8f, _attack.PlatformHit);
            Hitbox h = _owner.AddHitbox(t.gameObject, _data, c);
            t.gameObject.SetActive(false);
            _platHits.Add((p, t, h));
        }
    }

    static Sprite ArrowSprite => RuntimeSprites.FromAscii("Msg_Arrow", new[]
    {
        "W....",
        "WW...",
        "WWW..",
        "WWWW.",
        "WWW..",
        "WW...",
        "W....",
    }, new Dictionary<char, Color32> { { 'W', new Color32(255, 255, 255, 255) } });

    void Update()
    {
        float now = Time.time;
        if (now < _sweepAt) return;
        if (!_sweeping)
        {
            _sweeping = true;
            foreach (var g in _tele) if (g != null) Destroy(g);
            _tele.Clear();
            _beam.gameObject.SetActive(true);
            _spot.gameObject.SetActive(true);
            _spotHit.Activate();
            foreach (var ph in _platHits) ph.h.Activate();
            if (_owner != null) _owner.NotifyReleased(_data);
            ScreenFx.WhiteFlash(0.25f, 0.05f);
        }
        float t = Mathf.Clamp01((now - _sweepAt) / Mathf.Max(0.05f, _endAt - _sweepAt));
        float x = Mathf.Lerp(_startX, _endX, t);
        Vector2 floorPt = new Vector2(x, _floorY);
        _spot.position = floorPt;
        PlaceBeam(_origin, floorPt + Vector2.up * 0.1f);
        PlacePlatformHits(floorPt);
        // 光の点から火花（飾り）
        if (Random.value < 0.5f)
        {
            FxAnim.Sprite(TalkArt.Disc(2), floorPt + Vector2.up * 0.2f, SpotOrder + 2, 0.25f, Random.value < 0.5f ? (Color)MessagesArt.LaserGreen : (Color)MessagesArt.LaserPurple, 1f, "Spark")
                .Velocity(new Vector2(Random.Range(-3f, 3f), Random.Range(2f, 5f))).Gravity(12f).FadeFrom(0.3f);
        }
        if (now >= _endAt) Destroy(gameObject);
    }

    void PlaceBeam(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        float len = Mathf.Max(0.05f, d.magnitude);
        _beam.position = (from + to) * 0.5f;
        _beam.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        Vector3 so = _outer.transform.localScale, sc = _core.transform.localScale;
        // 絵は 1マスの長さで作ってあるので、長さだけ伸ばす（太さはちらつかせる）
        float flick = 1f + Mathf.Sin(Time.time * 60f) * 0.15f;
        _outer.transform.localScale = new Vector3(len / Mathf.Max(0.001f, _outer.sprite.bounds.size.x), so.y, 1f);
        _core.transform.localScale = new Vector3(len / Mathf.Max(0.001f, _core.sprite.bounds.size.x), sc.y, 1f);
        _outer.color = TalkArt.Fade((Color)MessagesArt.LaserPurple, 0.45f * flick);
    }

    /// <summary>帯が足場の上の高さを通る所に、足場の判定を動かす（通らなければ消す）</summary>
    void PlacePlatformHits(Vector2 floorPt)
    {
        foreach (var ph in _platHits)
        {
            if (ph.t == null) continue;
            float y = ph.plat.max.y + _attack.PlatformHit * 0.5f;
            float dy = _origin.y - floorPt.y;
            bool on = false;
            if (Mathf.Abs(dy) > 0.01f)
            {
                float s = (_origin.y - y) / dy; // 0 ＝ ボスの上、1 ＝ 床
                if (s >= 0f && s <= 1f)
                {
                    float x = Mathf.Lerp(_origin.x, floorPt.x, s);
                    if (x >= ph.plat.min.x && x <= ph.plat.max.x)
                    {
                        ph.t.position = new Vector3(x, y, 0f);
                        on = true;
                    }
                }
            }
            if (ph.t.gameObject.activeSelf != on) ph.t.gameObject.SetActive(on);
        }
    }
}
