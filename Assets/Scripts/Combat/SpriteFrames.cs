using System;
using UnityEngine;

/// <summary>
/// コマ送りの絵の計算（行の取り出し・経過時間からコマ番号）。テストできるように、Unity の時間には触らない。
/// </summary>
public static class SpriteFrames
{
    /// <summary>
    /// 行ごとに並んだコマから、row 行目を取り出す。1行のコマ数が 0 以下、または行が1つしか無いなら全部を返す。
    /// row が行数を超えたら、最初の行に戻る（4色の棒グラフを5本目以降も出せるように）
    /// </summary>
    public static Sprite[] Row(Sprite[] frames, int framesPerRow, int row)
    {
        if (frames == null || frames.Length == 0) return Array.Empty<Sprite>();
        if (framesPerRow <= 0 || framesPerRow >= frames.Length) return frames;
        int rows = frames.Length / framesPerRow;
        if (rows <= 0) return frames;
        int r = ((row % rows) + rows) % rows;
        var result = new Sprite[framesPerRow];
        Array.Copy(frames, r * framesPerRow, result, 0, framesPerRow);
        return result;
    }

    /// <summary>
    /// 経過秒数からコマ番号を求める。繰り返さないときは最後のコマで止まる。コマが無ければ -1
    /// </summary>
    public static int Index(float elapsed, float fps, int count, bool loop)
    {
        if (count <= 0) return -1;
        int i = Mathf.FloorToInt(Mathf.Max(0f, elapsed) * Mathf.Max(0.1f, fps));
        return loop ? i % count : Mathf.Min(i, count - 1);
    }

    /// <summary>最後まで再生する秒数</summary>
    public static float Duration(int count, float fps) => count <= 0 ? 0f : count / Mathf.Max(0.1f, fps);

    /// <summary>
    /// 絵の中心が transform の原点に来るように、絵の置き場所を求める（Pivot が下や左の素材を、判定の中心に合わせる）。
    /// facing が -1（左向き）なら flipX で反転する前提で、左右を合わせる
    /// </summary>
    public static Vector2 CenterOffset(Sprite sprite, int facing)
    {
        if (sprite == null) return Vector2.zero;
        Vector2 c = sprite.bounds.center;
        return new Vector2(-c.x * (facing >= 0 ? 1f : -1f), -c.y);
    }
}

/// <summary>
/// 順番に1つずつ取り出して、最後まで行ったら最初に戻る（Office のグラフの順番・Unity の形の順番・ボスの技の順番）。
/// </summary>
public class SequenceCycle<T>
{
    readonly T[] _items;

    public SequenceCycle(T[] items)
    {
        _items = items ?? Array.Empty<T>();
    }

    public int Count => _items.Length;
    /// <summary>次に出す物の番号</summary>
    public int Index { get; private set; }
    /// <summary>次に出す物（無ければ既定値）</summary>
    public T Peek => _items.Length > 0 ? _items[Index] : default;

    /// <summary>次に出す物が変わったとき（新しい「次」）</summary>
    public event Action<T> NextChanged;

    /// <summary>今の物を取り出し、次へ進める</summary>
    public T Advance()
    {
        if (_items.Length == 0) return default;
        T current = _items[Index];
        Index = (Index + 1) % _items.Length;
        NextChanged?.Invoke(Peek);
        return current;
    }

    /// <summary>最初に戻す</summary>
    public void Reset()
    {
        if (Index == 0) return;
        Index = 0;
        NextChanged?.Invoke(Peek);
    }
}

/// <summary>
/// 数に上限のある一覧。上限を超えて足したら、一番古い物を押し出して返す（Unity のオブジェクトは同時に3個まで）。
/// </summary>
public class BoundedSpawnList<T> where T : class
{
    readonly System.Collections.Generic.List<T> _items = new System.Collections.Generic.List<T>();

    public BoundedSpawnList(int capacity)
    {
        Capacity = Mathf.Max(1, capacity);
    }

    public int Capacity { get; set; }
    public int Count => _items.Count;
    public System.Collections.Generic.IReadOnlyList<T> Items => _items;

    /// <summary>足す。上限を超えたら、古い物から押し出す。押し出した物を返す（無ければ空）</summary>
    public System.Collections.Generic.List<T> Add(T item)
    {
        var evicted = new System.Collections.Generic.List<T>();
        _items.Add(item);
        while (_items.Count > Capacity)
        {
            evicted.Add(_items[0]);
            _items.RemoveAt(0);
        }
        return evicted;
    }

    public bool Remove(T item) => _items.Remove(item);

    /// <summary>もう無い物（消えた物）を外す</summary>
    public void Prune(Func<T, bool> isGone) => _items.RemoveAll(i => i == null || isGone(i));
}

/// <summary>物理オブジェクト1種類分（Unity の Cube・Sphere・Capsule）</summary>
[Serializable]
public struct PrimitiveEntry
{
    [Tooltip("本体の絵")]
    public Sprite Sprite;
    public PrimitiveShape Shape;
    [Tooltip("コライダーの大きさ（マス）。円は x が直径")]
    public Vector2 Size;
}
