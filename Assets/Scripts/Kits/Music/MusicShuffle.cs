using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ミュージックのボスの技3（第2形態）：シャッフル（2026-11-19。docs/app-kits-2-design.md 4-1）。
/// 頭の上に交差した矢印の印 → ボスが 3か所へ順に瞬間移動（行き先に赤い体の枠。各 0.8秒）→ 着くたびに音符の弾を扇に 3発。
/// ・浮くボスなので、高さは今のまま。行き先は x＝-7・0・7 のうち今いない所（同じ所が続かない）
/// ・止められたら（形態変化・正常化）、ボスを見える状態に戻す（MusicHopMark の Restore）
/// </summary>
[CreateAssetMenu(fileName = "Music_Shuffle", menuName = "Team1/Kits/Music Shuffle")]
public class MusicShuffle : CustomAttack
{
    [Tooltip("音符の弾（種類 Projectile。扇の数・角度もこちら）")]
    [SerializeField] AttackData _notes;
    [SerializeField, Min(1)] int _hops = 3;
    [Tooltip("行き先の予告（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("着いてから撃つまで（秒）")]
    [SerializeField, Min(0f)] float _aimDelay = 0.25f;
    [Tooltip("浮くボスの行き先（部屋のまん中からの x）")]
    [SerializeField] float[] _spots = { -7f, 0f, 7f };

    public override bool CanRun(AttackRunner owner, AttackData data) => _notes != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        BossController boss = owner.GetComponent<BossController>();
        MusicHopMark mark = MusicHopMark.Spawn(owner, boss);
        mark.ShowIcon(MusicArt.Shuffle, 0.5f);
        yield return new WaitForSeconds(0.5f);
        float y = owner.transform.position.y;
        int last = -1;
        for (int h = 0; h < _hops; h++)
        {
            int pick = PickSpot(owner.transform.position.x - arena.center.x, last);
            last = pick;
            Vector2 dest = new Vector2(arena.center.x + _spots[pick], y);
            yield return MusicHopMark.Hop(owner, boss, mark, dest, _telegraph);
            yield return new WaitForSeconds(_aimDelay);
            Fan(owner);
            owner.NotifyReleased(data);
            yield return new WaitForSeconds(0.35f);
        }
        if (mark != null) Destroy(mark.gameObject);
    }

    int PickSpot(float curX, int last)
    {
        var c = new List<int>();
        for (int i = 0; i < _spots.Length; i++)
        {
            if (i == last || Mathf.Abs(_spots[i] - curX) < 1f) continue;
            c.Add(i);
        }
        if (c.Count == 0) for (int i = 0; i < _spots.Length; i++) if (i != last) c.Add(i);
        return c.Count > 0 ? c[Random.Range(0, c.Count)] : 0;
    }

    void Fan(AttackRunner owner)
    {
        Vector2 origin = owner.OriginPoint;
        Vector2 baseDir = Vector2.down;
        if (owner.TargetPoint is Vector2 t && (t - origin).sqrMagnitude > 0.01f) baseDir = (t - origin).normalized;
        int n = Mathf.Max(1, _notes.ProjectileCount);
        for (int i = 0; i < n; i++)
        {
            float a = n > 1 ? -_notes.SpreadAngle * 0.5f + _notes.SpreadAngle * i / (n - 1) : 0f;
            Projectile p = Projectile.Spawn(owner, _notes, origin, Quaternion.Euler(0f, 0f, a) * baseDir, i);
            if (p != null)
            {
                KitDress.Swap(p.gameObject, MusicArt.Note(i));
                SpriteRenderer v = KitDress.FindVisual(p.transform);
                if (v != null) v.gameObject.AddComponent<MusicUpright>();
            }
        }
    }
}

/// <summary>弾が回っても、絵だけは上を向いたままにする（音符）</summary>
public class MusicUpright : MonoBehaviour
{
    void LateUpdate() => transform.rotation = Quaternion.identity;
}

/// <summary>
/// ボスの瞬間移動の予告と、頭の上の印（ミュージックのキットの中で使う）。
/// 行き先に赤い体の枠を出し、ボスを消して移し、また見せる。消えるとき（止められたとき）はボスを見える状態に戻す
/// </summary>
public class MusicHopMark : CustomEffect
{
    BossController _boss;
    SpriteRenderer _icon;
    float _iconUntil;

    public static MusicHopMark Spawn(AttackRunner owner, BossController boss)
    {
        GameObject go = owner.CreateSpawnedObject("Music_HopMark", owner.transform.position);
        var m = go.AddComponent<MusicHopMark>();
        m._boss = boss;
        return m;
    }

    public void ShowIcon(Sprite s, float seconds)
    {
        if (_icon == null) _icon = RuntimeSprites.Show(transform, s, Vector2.zero, 60, 1f, "Icon");
        _icon.sprite = s;
        _iconUntil = Time.time + seconds;
    }

    void LateUpdate()
    {
        if (_icon == null) return;
        _icon.enabled = Time.time < _iconUntil;
        if (_boss != null)
        {
            Bounds b = _boss.BodyBounds;
            _icon.transform.position = new Vector3(b.center.x, b.max.y + 0.5f, 0f);
        }
    }

    /// <summary>行き先に赤い枠（telegraph 秒）→ 消えて移って現れる</summary>
    public static IEnumerator Hop(AttackRunner owner, BossController boss, MusicHopMark mark, Vector2 dest, float telegraph)
    {
        Bounds body = boss != null ? boss.BodyBounds : new Bounds(owner.transform.position, new Vector3(2f, 2f, 0f));
        Vector2 offset = (Vector2)body.center - (Vector2)owner.transform.position;
        GameObject frame = owner.CreateSpawnedObject("Music_HopFrame", dest + offset);
        frame.AddComponent<MusicFrameBlink>().Setup(body.size, telegraph);
        float end = Time.time + telegraph;
        while (Time.time < end)
        {
            // 最後の 0.2秒で消える
            float left = end - Time.time;
            if (boss != null && left < 0.2f) boss.SetVisibility(left / 0.2f);
            yield return null;
        }
        if (boss != null)
        {
            boss.SetContactEnabled(false);
            boss.Teleport(dest);
        }
        else owner.transform.position = dest;
        if (frame != null) Destroy(frame);
        float t0 = Time.time;
        while (Time.time - t0 < 0.15f)
        {
            if (boss != null) boss.SetVisibility((Time.time - t0) / 0.15f);
            yield return null;
        }
        if (boss != null)
        {
            boss.SetVisibility(1f);
            boss.SetContactEnabled(true);
        }
    }

    protected override void Restore()
    {
        if (_boss != null && _boss.State == BossState.Fighting)
        {
            _boss.SetVisibility(1f);
            _boss.SetContactEnabled(true);
        }
    }
}

/// <summary>赤い体の枠（行き先の予告）。最後の 0.3秒は点滅。形態変化で消える</summary>
public class MusicFrameBlink : CustomEffect
{
    SpriteRenderer[] _parts;
    float _end;

    public void Setup(Vector2 size, float seconds)
    {
        _end = Time.time + seconds;
        float t = 2f / 18f;
        Color red = MusicArt.Warn;
        _parts = new[]
        {
            FxKit.Box(transform, Vector2.zero, size, new Color(1f, 0.27f, 0.23f, 0.18f), 24, "Fill"),
            FxKit.Box(transform, new Vector2(0f, size.y * 0.5f), new Vector2(size.x, t), red, 25, "T"),
            FxKit.Box(transform, new Vector2(0f, -size.y * 0.5f), new Vector2(size.x, t), red, 25, "B"),
            FxKit.Box(transform, new Vector2(-size.x * 0.5f, 0f), new Vector2(t, size.y), red, 25, "L"),
            FxKit.Box(transform, new Vector2(size.x * 0.5f, 0f), new Vector2(t, size.y), red, 25, "R"),
        };
    }

    void Update()
    {
        float left = _end - Time.time;
        if (left <= -0.05f) { Destroy(gameObject); return; }
        bool on = left > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
        if (_parts != null) foreach (var p in _parts) if (p != null) p.enabled = on;
    }
}
