using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】共同編集のカーソル（FigmaCursorAttack）の、場に出る物。自分で進む。
/// カーソル i は「入る → 赤い線（引いた時のプレイヤーの位置で固定）→ 突っ込む → 床で消える」。i ごとに Interval ずつずらす
/// </summary>
public class FigmaCursorEffect : CustomEffect
{
    const int Order = 42;

    class Cursor
    {
        public Transform Root;
        public Vector2 Home;
        public Hitbox Hit;
        public bool Done;
    }

    AttackRunner _owner;
    AttackData _data;
    FigmaCursorAttack _a;
    readonly List<Cursor> _cursors = new List<Cursor>();
    int _running;

    public static FigmaCursorEffect Spawn(AttackRunner owner, AttackData data, FigmaCursorAttack attack)
    {
        if (owner == null || data == null || attack == null) return null;
        GameObject go = owner.CreateSpawnedObject($"Figma_Cursor_{data.name}", owner.OriginPoint);
        var e = go.AddComponent<FigmaCursorEffect>();
        e._owner = owner;
        e._data = data;
        e._a = attack;
        e.StartCoroutine(e.Run());
        return e;
    }

    IEnumerator Run()
    {
        Rect arena = BattleArena.GetBounds();
        int n = Mathf.Max(1, _a.Count);
        float top = arena.yMax - 1.6f;
        for (int i = 0; i < n; i++)
        {
            float x = n == 1 ? arena.center.x : Mathf.Lerp(arena.xMin + 2.5f, arena.xMax - 2.5f, i / (float)(n - 1));
            int colorIndex = i == n - 1 ? 2 : i % 2; // 最後は Client（紫）
            string name = i == n - 1 ? FigmaArt.NameOf(2) : FigmaArt.NameOf(i % 2);
            var root = FigmaArt.CursorWithTag(name, FigmaArt.CursorColor(colorIndex), new Vector2(x, top + 2f), Order, transform);
            var c = new Cursor { Root = root, Home = new Vector2(x, top) };
            // 判定（突っ込む間だけ）
            var hitGo = new GameObject("Hit");
            hitGo.layer = gameObject.layer;
            hitGo.transform.SetParent(root, false);
            hitGo.transform.localPosition = new Vector2(0.25f, -0.35f);
            var rb = hitGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
            var box = hitGo.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = _a.HitSize;
            c.Hit = _owner.AddHitbox(hitGo, _data, box);
            _cursors.Add(c);
        }

        // 入ってくる
        float start = Time.time;
        while (Time.time - start < _a.EnterTime)
        {
            float k = Mathf.Clamp01((Time.time - start) / Mathf.Max(0.01f, _a.EnterTime));
            float e = 1f - (1f - k) * (1f - k);
            foreach (var c in _cursors) c.Root.position = Vector2.Lerp(c.Home + new Vector2(0f, 2f), c.Home, e);
            yield return null;
        }
        foreach (var c in _cursors) c.Root.position = c.Home;

        // 1つずつ
        for (int i = 0; i < _cursors.Count; i++)
        {
            if (OwnerGone()) { Destroy(gameObject); yield break; }
            _running++;
            StartCoroutine(One(_cursors[i], i == _cursors.Count - 1));
            if (i < _cursors.Count - 1) yield return new WaitForSeconds(_a.Interval);
        }
        while (_running > 0) yield return null;
        Destroy(gameObject);
    }

    bool OwnerGone() => _owner == null || !_owner.isActiveAndEnabled;

    IEnumerator One(Cursor c, bool last)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 from = c.Root.position;
        Vector2 target = _owner.TargetPoint ?? new Vector2(from.x, arena.yMin + 0.5f);
        Vector2 dir = target - from;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
        dir.Normalize();
        if (dir.y > -0.2f) { dir.y = -0.2f; dir.Normalize(); } // 上へは突っ込まない
        Vector2 to = EndPoint(from, dir, arena);

        // 赤い線（固定）
        KitDress.Line(_owner, from, to, _a.AimTime, 2f);
        float aimEnd = Time.time + _a.AimTime;
        while (Time.time < aimEnd)
        {
            // カーソルが少しふるえる（狙っている）
            c.Root.position = from + new Vector2(Mathf.Sin(Time.time * 40f) * 0.03f, 0f);
            yield return null;
        }
        c.Root.position = from;

        // 突っ込む
        c.Hit.Activate();
        _owner.NotifyReleased(_data);
        float dist = Vector2.Distance(from, to);
        float t = 0f;
        while (t < dist)
        {
            t = Mathf.Min(dist, t + _a.RushSpeed * Time.deltaTime);
            c.Root.position = from + dir * t;
            yield return null;
        }
        c.Hit.Deactivate();
        FxKit.Ring(to, 1.2f, 0.25f, FigmaArt.CursorColor(last ? 2 : 0));

        if (last && _a.LastFrame && !OwnerGone())
        {
            Vector2 center = new Vector2(Mathf.Clamp(to.x, arena.xMin + _a.FrameSize.x * 0.5f, arena.xMax - _a.FrameSize.x * 0.5f), arena.yMin + _a.FrameSize.y * 0.5f);
            FigmaFrameAttack.SpawnFrame(_owner, _data, center, _a.FrameSize, 0.25f, _a.FrameTelegraph, _a.FrameActive, "Frame 2");
        }
        yield return new WaitForSeconds(0.15f);
        if (c.Root != null) c.Root.gameObject.SetActive(false);
        _running--;
    }

    /// <summary>線の先（床か壁まで）</summary>
    static Vector2 EndPoint(Vector2 from, Vector2 dir, Rect arena)
    {
        float tMax = 40f;
        float floor = arena.yMin + 0.35f;
        if (dir.y < -0.001f) tMax = Mathf.Min(tMax, (floor - from.y) / dir.y);
        if (dir.x > 0.001f) tMax = Mathf.Min(tMax, (arena.xMax - 0.3f - from.x) / dir.x);
        if (dir.x < -0.001f) tMax = Mathf.Min(tMax, (arena.xMin + 0.3f - from.x) / dir.x);
        return from + dir * Mathf.Max(0.5f, tMax);
    }
}
