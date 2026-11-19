using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】キットの絵を今の部品に付ける（2026-11-19。docs/app-kits-2-design.md X3・app-kits-1-design.md E6 の KitDressed）。担当0 の物。
// ・攻撃データの名前に枠の語を入れない（スキンの絵が付かない）で、部品が返した物の絵を Swap で差し替える。
// ・絵はキットのフォルダの <Id>Art.cs に、コードの中のドット絵で作る。攻撃データのアセットからは絵を指せないので、
//   キットの実行時のファイルで RegisterArt("slack.emoji", i => SlackArt.Emoji(i)) と名前で登録し、KitDressed の「絵の名前」に入れる。
// ・Line：予告の赤い細い線（弾の通り道など）。Visual/Skins/ は変えない。

/// <summary>キットの絵の差し替えと、予告の線</summary>
public static class KitDress
{
    /// <summary>危ない物の予告の赤（#FF453A）</summary>
    public static readonly Color Red = new Color32(0xFF, 0x45, 0x3A, 0xFF);
    /// <summary>1ドット（マス）</summary>
    public const float Px = 1f / 18f;

    static readonly Dictionary<string, Func<int, Sprite>> Arts = new Dictionary<string, Func<int, Sprite>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>絵を名前で登録する（index：同時に出す物の何個目か。絵を変えないなら無視してよい）</summary>
    public static void RegisterArt(string key, Func<int, Sprite> art)
    {
        if (string.IsNullOrEmpty(key) || art == null) return;
        Arts[key] = art;
    }

    /// <summary>絵を名前で登録する（いつも同じ絵）</summary>
    public static void RegisterArt(string key, Func<Sprite> art)
    {
        if (art == null) return;
        RegisterArt(key, _ => art());
    }

    /// <summary>その名前の絵が登録されているか</summary>
    public static bool HasArt(string key) => !string.IsNullOrEmpty(key) && Arts.ContainsKey(key);

    /// <summary>名前の絵（無い・作れないときは null）</summary>
    public static Sprite Art(string key, int index = 0)
    {
        if (string.IsNullOrEmpty(key) || !Arts.TryGetValue(key, out var f)) return null;
        try { return f(index); }
        catch (Exception e)
        {
            Debug.LogException(e);
            return null;
        }
    }

    /// <summary>
    /// 部品（弾・指定位置の判定・落下物）が出した物の絵を差し替える。大きさは今の絵と同じ（伸び縮みさせる）。
    /// tint を渡さなければ白（絵の色のまま）。差し替えたら true
    /// </summary>
    public static bool Swap(GameObject spawned, Sprite sprite, Color? tint = null)
    {
        if (spawned == null || sprite == null) return false;
        SpriteRenderer sr = FindVisual(spawned.transform);
        if (sr == null) return false;
        Vector2 size = WorldSize(sr);
        sr.drawMode = SpriteDrawMode.Simple;
        sr.sprite = sprite;
        Vector2 native = sprite.bounds.size;
        sr.transform.localScale = new Vector3(size.x / Mathf.Max(native.x, 0.001f), size.y / Mathf.Max(native.y, 0.001f), 1f);
        Color c = tint ?? Color.white;
        if (!tint.HasValue) c.a = 1f;
        sr.color = c;
        return true;
    }

    /// <summary>部品の本体の絵（子の "Visual"。無ければ予告・影・枠でない最初の絵）</summary>
    public static SpriteRenderer FindVisual(Transform root)
    {
        if (root == null) return null;
        Transform v = root.Find("Visual");
        if (v != null && v.TryGetComponent(out SpriteRenderer sr)) return sr;
        foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            string n = r.gameObject.name;
            if (n == "Circle" || n == "Ellipse" || n == "Frame" || n == "Telegraph") continue;
            return r;
        }
        return null;
    }

    static Vector2 WorldSize(SpriteRenderer sr)
    {
        Vector3 s = sr.transform.localScale;
        if (sr.drawMode != SpriteDrawMode.Simple) return new Vector2(sr.size.x * s.x, sr.size.y * s.y);
        Vector2 b = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        return new Vector2(b.x * s.x, b.y * s.y);
    }

    /// <summary>
    /// 予告の赤い細い線を seconds 秒出す（最後の 0.3秒は点滅）。owner の出した物として登録する（ボスの形態変化・正常化で消える）。
    /// widthPx：太さ（ドット）。color：無ければ赤
    /// </summary>
    public static GameObject Line(AttackRunner owner, Vector2 from, Vector2 to, float seconds, float widthPx = 2f, Color? color = null)
    {
        Vector2 d = to - from;
        float len = d.magnitude;
        if (len < 0.01f) return null;
        Vector2 mid = (from + to) * 0.5f;
        GameObject go = owner != null ? owner.CreateSpawnedObject("KitLine", mid) : new GameObject("KitLine");
        go.transform.position = mid;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        var line = go.AddComponent<KitLine>();
        line.Setup(new Vector2(len, Mathf.Max(1f, widthPx) * Px), color ?? Red, seconds);
        if (owner == null || owner.Team == Team.Enemy) GameAudio.Telegraph();
        return go;
    }
}

/// <summary>予告の線（KitDress.Line が作る）。時間が来たら消える。ボスの形態変化でも消える（CustomEffect）</summary>
public class KitLine : CustomEffect
{
    SpriteRenderer _sr;
    float _endAt;

    internal void Setup(Vector2 size, Color color, float seconds)
    {
        _sr = AttackRunner.AddVisual(transform, PlaceholderSprites.Square, color, size, 17, "Line");
        _endAt = Time.time + Mathf.Max(0.05f, seconds);
    }

    void Update()
    {
        float left = _endAt - Time.time;
        if (left <= 0f) { Destroy(gameObject); return; }
        if (_sr != null) _sr.enabled = left > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
    }
}
