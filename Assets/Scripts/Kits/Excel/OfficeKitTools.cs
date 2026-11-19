using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Office のキット（Excel・PowerPoint・Word・Keynote）で使い回す道具（2026-11-19。担当4）。
/// ・実行時に攻撃データを作る（ステージのギミック用。アセットを持てないので、JSON で欄を入れる）
/// ・決まった相手に1回だけ当てる（能力の「当たったセルの数字 ×2」など、相手ごとに数値を変えるとき）
/// ・円の全方向の予告の線と弾（Excel の円グラフ・Keynote の One more thing...）
/// ・プレイヤーの能力の範囲（ボス部屋の外でも使えるように）
/// </summary>
public static class OfficeKitTools
{
    static readonly Dictionary<string, AttackData> Runtime = new Dictionary<string, AttackData>();
    static readonly Dictionary<(AttackData, int), AttackData> GaugeCopies = new Dictionary<(AttackData, int), AttackData>();

    /// <summary>
    /// 実行時に攻撃データを作る（同じ名前は1回だけ）。json は AttackData の欄の名前（"_kind":2 など）。
    /// 名前に枠の語を入れると、スキンの絵が付く
    /// </summary>
    public static AttackData RuntimeData(string name, string json)
    {
        if (Runtime.TryGetValue(name, out AttackData d) && d != null) return d;
        d = ScriptableObject.CreateInstance<AttackData>();
        d.name = name;
        d.hideFlags = HideFlags.DontSave;
        try { JsonUtility.FromJsonOverwrite(json, d); }
        catch (Exception e) { Debug.LogException(e); }
        Runtime[name] = d;
        return d;
    }

    /// <summary>ゲージの量だけを変えた写し（同じ組は使い回す）</summary>
    public static AttackData WithGauge(AttackData data, int gauge)
    {
        if (data == null) return null;
        var key = (data, gauge);
        if (GaugeCopies.TryGetValue(key, out AttackData c) && c != null) return c;
        c = data.CreateScaledCopy(AttackStat.GaugeGain, gauge);
        c.hideFlags = HideFlags.DontSave;
        GaugeCopies[key] = c;
        return c;
    }

    /// <summary>四角の中の、当てられる相手の被弾範囲（owner の当てる相手のレイヤー）</summary>
    public static void FindTargets(AttackRunner owner, Rect rect, List<Hurtbox> into)
    {
        into.Clear();
        if (owner == null) return;
        var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
        filter.SetLayerMask(owner.TargetLayers);
        var hits = new Collider2D[24];
        int n = Physics2D.OverlapBox(rect.center, rect.size, 0f, filter, hits);
        for (int i = 0; i < n; i++)
        {
            if (hits[i] == null || !hits[i].TryGetComponent(out Hurtbox hb) || hb.Owner == null) continue;
            if (hb.Owner.Team == owner.Team) continue;
            into.Add(hb);
        }
    }

    /// <summary>
    /// 決まった相手（被弾範囲）に1回だけ当てる：相手の体のまん中に小さい判定を、短い時間だけ出す。
    /// ダメージ・ゲージ・見た目は data（ふつうの判定と同じ道を通るので、当たった絵・倍率も付く）
    /// </summary>
    public static void HitOnce(AttackRunner owner, AttackData data, Hurtbox target, float life = 0.1f)
    {
        if (owner == null || data == null || target == null || target.Collider == null) return;
        Vector2 at = target.Collider.bounds.center;
        GameObject go = owner.CreateSpawnedObject($"Probe_{data.name}", at);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.2f, 0.2f);
        Hitbox hb = owner.AddHitbox(go, data, col);
        hb.Activate();
        UnityEngine.Object.Destroy(go, life);
    }

    /// <summary>ボスがこの技をやめたか（形態変化・正常化・止められた）</summary>
    public static bool OwnerStopped(AttackRunner owner, AttackData data) =>
        owner == null || !owner.isActiveAndEnabled || (data != null && owner.Current != data);

    /// <summary>全方向に n 本の向き（offsetDeg だけ回す。0 度＝右）</summary>
    public static List<Vector2> RadialDirs(int n, float offsetDeg)
    {
        var dirs = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            float a = (offsetDeg + 360f * i / Mathf.Max(1, n)) * Mathf.Deg2Rad;
            dirs.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
        }
        return dirs;
    }

    /// <summary>全方向の予告の赤い細い線（seconds 秒。最後の 0.3秒は点滅）</summary>
    public static void RadialLines(AttackRunner owner, Vector2 origin, List<Vector2> dirs, float length, float seconds, float startGap = 0.6f)
    {
        foreach (var d in dirs) KitDress.Line(owner, origin + d * startGap, origin + d * length, seconds);
    }

    /// <summary>全方向に撃つ（sprite があれば絵を差し替える）</summary>
    public static void RadialFire(AttackRunner owner, AttackData data, Vector2 origin, List<Vector2> dirs, Func<int, Sprite> sprite = null, int indexBase = 0)
    {
        for (int i = 0; i < dirs.Count; i++)
        {
            Projectile p = Projectile.Spawn(owner, data, origin, dirs[i], indexBase + i);
            if (p == null) continue;
            Sprite s = sprite?.Invoke(indexBase + i);
            if (s != null) KitDress.Swap(p.gameObject, s);
        }
        owner.NotifyReleased(data);
    }

    /// <summary>部屋（ボス部屋なら部屋、ほかは画面）と、その床の高さ</summary>
    public static Rect Area(AttackRunner owner, out float floorY)
    {
        Rect area = WallShot.AreaFor(owner);
        floorY = WallShot.FloorY(owner, area);
        return area;
    }

    /// <summary>
    /// プレイヤーの能力の範囲：プレイヤーがボス部屋にいれば部屋、ほかは画面（2026-11-19）。
    /// プレイヤーの AttackRunner は狙う相手を持たないので、WallShot.AreaFor だといつもボス部屋になる（スクロール・ウィンドウのステージで困る）
    /// </summary>
    public static Rect PlayerArea(AttackRunner owner)
    {
        Rect arena = BattleArena.GetBounds();
        if (owner == null) return arena;
        Vector2 p = (Vector2)owner.transform.position + Vector2.up * 0.5f;
        return arena.Contains(p) ? arena : ScreenUi.ViewRect();
    }

    /// <summary>子の入れ物を作る（位置はゲームの世界）</summary>
    public static Transform Child(Transform parent, string name, Vector2 worldPos)
    {
        var go = new GameObject(name);
        go.layer = parent != null ? parent.gameObject.layer : 0;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        return go.transform;
    }

    /// <summary>色の透明度だけを変える</summary>
    public static Color Fade(Color c, float a)
    {
        c.a = a;
        return c;
    }

    /// <summary>プレイヤーの見た目の絵（いちばん大きい SpriteRenderer。無ければ null）</summary>
    public static SpriteRenderer PlayerSprite(Transform player)
    {
        if (player == null) return null;
        SpriteRenderer best = null;
        float area = 0f;
        foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr == null || sr.sprite == null || !sr.enabled) continue;
            Vector3 s = sr.bounds.size;
            if (s.x * s.y > area) { area = s.x * s.y; best = sr; }
        }
        return best;
    }
}

/// <summary>
/// 【仮】ボスの技が出す、見た目だけの物（2026-11-19。担当4）。life 秒で消える。最後の blink 秒は点滅。
/// owner の出した物として作るので、形態変化・正常化で消える
/// </summary>
public class OfficeFx : CustomEffect
{
    float _endAt;
    float _blink;
    float _spin;
    SpriteRenderer[] _srs;

    /// <summary>空の入れ物を作る（子に絵を足してから使う）</summary>
    public static OfficeFx Create(AttackRunner owner, string name, Vector2 pos, float life, float blink = 0.3f)
    {
        GameObject go = owner != null ? owner.CreateSpawnedObject(name, pos) : new GameObject(name);
        go.transform.position = pos;
        var fx = go.AddComponent<OfficeFx>();
        fx._endAt = Time.time + Mathf.Max(0.02f, life);
        fx._blink = blink;
        return fx;
    }

    /// <summary>絵を1枚出す</summary>
    public static OfficeFx Sprite(AttackRunner owner, Sprite sprite, Vector2 pos, Vector2 size, float life, int order, Color? color = null, float blink = 0.3f)
    {
        OfficeFx fx = Create(owner, "OfficeFx", pos, life, blink);
        AttackRunner.AddVisual(fx.transform, sprite, color ?? Color.white, size, order);
        return fx;
    }

    /// <summary>回す（度/秒）</summary>
    public OfficeFx Spin(float degPerSec) { _spin = degPerSec; return this; }

    /// <summary>今すぐ消す</summary>
    public void End() => Destroy(gameObject);

    void Update()
    {
        float left = _endAt - Time.time;
        if (left <= 0f) { Destroy(gameObject); return; }
        if (_spin != 0f) transform.Rotate(0f, 0f, _spin * Time.deltaTime);
        if (_blink > 0f)
        {
            if (_srs == null) _srs = GetComponentsInChildren<SpriteRenderer>();
            bool on = left > _blink || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            foreach (var sr in _srs) if (sr != null) sr.enabled = on;
        }
    }
}
