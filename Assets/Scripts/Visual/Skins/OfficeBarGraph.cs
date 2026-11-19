using System.Collections.Generic;
using UnityEngine;
using K = OfficeGameChatKit;

/// <summary>
/// 【仮】能力：棒グラフ（2026-11-18。docs/app-skill-visuals-design.md 4-1）。同時に出す柱（AreaCount 本）を、1つのグラフとして描く。
/// ・使った瞬間：床にグラフの軸（白い横の線）が手前から奥へ引かれる。後ろに横の目盛りの線（うすい白）
/// ・柱が判定を始めるたびに、棒が下から伸びる（0 → 110% → 100%）。色は1本目がアプリの色、残りはオレンジ・黄・緑。白いふち
///   棒の上にデータの数字（120% → 250% → 999%）。棒の高さは右へ1段ずつ高くする（プレイヤーの攻撃は、判定より大きく見えてよい。0-3）
/// ・最後の棒が伸びきったら、棒の頭をつなぐ右上がりの矢印が走って、画面の上へ抜ける（ハッタリ）
/// ・柱の判定が終わった棒は、うすく（5割）して立てたまま残す（3本が並んだグラフとして見せる。判定の無い飾りの決まり 0-3）。
///   最後の棒が終わり、矢印が抜けたら、全部の棒がいっしょに下へ縮み、軸が奥から消える
/// 柱（AreaStrike）ごとに場面の知らせが来るので、柱の物の状態（OfficeGameChatTag）から、このグラフ（自分で消える演出）へつなぐ。
/// 知らせが来なくても、柱の状態（IsHitActive・消えた）を見て進む。当たり判定・秒数は変えない
/// </summary>
public class OfficeBarGraph : MonoBehaviour
{
    static readonly List<OfficeBarGraph> Live = new List<OfficeBarGraph>();
    static readonly string[] Labels = { "120%", "250%", "999%", "9999%" };

    // 矢じり（7×7。右向き）
    static readonly string[] ArrowHeadArt =
    {
        "W......",
        "WWW....",
        "WWWWW..",
        "WWWWWWW",
        "WWWWW..",
        "WWW....",
        "W......",
    };

    class Bar
    {
        public GameObject Target;
        public AreaStrike Area;
        public bool Joined;
        public float ActiveAt = -1f;
        public float VanishAt = -1f;
        public Transform Pivot;
        public Transform Label;
        public Transform Telegraph;
        public float X, Bottom, Height;
        public bool Grown, Dimmed;
    }

    OfficeGameChatFx _fx;
    AttackRunner _owner;
    AttackData _data;
    AppTheme _theme;
    int _count;
    float _dir = 1f;
    Vector2 _size;
    float _born;
    Bar[] _bars;
    float _axisStart, _axisLen;
    SpriteRenderer _axis, _yAxis;
    readonly List<SpriteRenderer> _grid = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> _ticks = new List<SpriteRenderer>();
    float _arrowAt = -1f;
    Transform _arrow;
    float _axisGoneAt = -1f;
    float _collapseAt = -1f;
    bool _ended;

    /// <summary>柱1本ぶんの場面の知らせ（OfficeSkin.Dress から）。いつも true</summary>
    public static bool Dress(DressPhase phase, DressInfo info)
    {
        var tag = OfficeGameChatTag.Of(info, "Office.BarGraph");
        var graph = tag.Link as OfficeBarGraph;
        if (graph == null)
        {
            graph = FindOrCreate(info);
            graph.Join(info, tag);
            tag.Link = graph;
        }
        int i = Mathf.Clamp(tag.Number, 0, graph._count - 1);
        switch (phase)
        {
            case DressPhase.Telegraph: graph.ShowTelegraph(i, info); break;
            case DressPhase.Active: graph.Activate(i); break;
            case DressPhase.Vanish: graph.Vanish(i); break;
        }
        return true;
    }

    static OfficeBarGraph FindOrCreate(DressInfo info)
    {
        Live.RemoveAll(g => g == null);
        int index = Mathf.Max(0, info.Index);
        foreach (var g in Live)
        {
            if (g._owner != info.Owner || g._data != info.Data || g._ended) continue;
            if (Time.time - g._born > 1.5f) continue;
            if (index < g._bars.Length && g._bars[index].Joined) continue; // 同じ番号がもういる＝次の1回
            return g;
        }
        Vector2 size = K.SizeOf(info);
        int count = Mathf.Max(1, Mathf.Max(info.Count, info.Data != null ? info.Data.AreaCount : 1));
        float spacing = info.Data != null ? info.Data.AreaSpacing : 1.8f;
        float dir = info.Owner != null ? info.Owner.Facing : 1f;
        Vector2 p = info.Target.transform.position;
        float x0 = p.x - dir * index * spacing;
        float ground = p.y - size.y * 0.5f;

        var fx = K.Adopted(info, "OfficeBarGraph", new Vector2(x0, ground), 4f);
        var graph = fx.gameObject.AddComponent<OfficeBarGraph>();
        graph._fx = fx;
        graph._owner = info.Owner;
        graph._data = info.Data;
        graph._theme = info.Theme;
        graph._count = count;
        graph._dir = dir >= 0f ? 1f : -1f;
        graph._size = size;
        graph._born = Time.time;
        graph._bars = new Bar[count];
        for (int b = 0; b < count; b++) graph._bars[b] = new Bar();
        graph.BuildAxis(spacing);
        Live.Add(graph);
        return graph;
    }

    void Join(DressInfo info, OfficeGameChatTag tag)
    {
        int i = Mathf.Clamp(info.Index, 0, _count - 1);
        tag.Number = i;
        Bar b = _bars[i];
        b.Joined = true;
        b.Target = info.Target;
        b.Area = info.Target.GetComponent<AreaStrike>();
        Vector2 p = info.Target.transform.position;
        b.X = p.x;
        b.Bottom = p.y - _size.y * 0.5f;
        b.Height = _size.y * (1f + 0.15f * i); // 右へ1段ずつ高く（右上がりのグラフ）
    }

    // ───────── 軸と目盛り ─────────

    void BuildAxis(float spacing)
    {
        _axisLen = (_count - 1) * spacing + _size.x + 1f;
        _axisStart = -_dir * (_size.x * 0.5f + 0.5f);
        _axis = K.Box(transform, Vector2.zero, new Vector2(0.001f, K.Dot), Color.white, 40, "Axis");
        _yAxis = K.Box(transform, new Vector2(_axisStart, 0f), new Vector2(K.Dot, 0.001f), K.Alpha(Color.white, 0.7f), 40, "YAxis");
        for (int k = 1; k <= 4; k++)
        {
            float y = _size.y * k / 4f;
            _grid.Add(K.Box(transform, new Vector2(_axisStart + _dir * _axisLen * 0.5f, y), new Vector2(_axisLen, K.Dot), K.Alpha(Color.white, 0f), 40, "GridLine"));
            // Y軸の目盛り
            _ticks.Add(K.Box(transform, new Vector2(_axisStart - _dir * K.Dot, y), new Vector2(K.Dot * 3f, K.Dot), K.Alpha(Color.white, 0f), 40, "Tick"));
        }
    }

    void UpdateAxis(float now)
    {
        float age = now - _born;
        float k = Mathf.Clamp01(age / 0.1f); // 手前から奥へ引く（0.1秒）
        float len = _axisLen * k;
        if (_axisGoneAt >= 0f)
        {
            // 奥から消える
            float g = Mathf.Clamp01((now - _axisGoneAt) / 0.12f);
            len = _axisLen * (1f - g);
            foreach (var sr in _grid) K.SetAlpha(sr, 0.2f * (1f - g));
            foreach (var sr in _ticks) K.SetAlpha(sr, 0.7f * (1f - g));
            K.SetAlpha(_yAxis, 0.7f * (1f - g));
            if (g >= 1f) End();
        }
        else
        {
            foreach (var sr in _grid) K.SetAlpha(sr, 0.2f * k);
            foreach (var sr in _ticks) K.SetAlpha(sr, 0.7f * k);
        }
        _axis.transform.localPosition = new Vector2(_axisStart + _dir * len * 0.5f, K.Dot * 0.5f);
        _axis.transform.localScale = new Vector3(Mathf.Max(0.001f, len), K.Dot, 1f);
        float yh = _size.y * 1.15f * k;
        _yAxis.transform.localPosition = new Vector2(_axisStart, yh * 0.5f);
        _yAxis.transform.localScale = new Vector3(K.Dot, Mathf.Max(0.001f, yh), 1f);
    }

    // ───────── 棒 ─────────

    void ShowTelegraph(int i, DressInfo info)
    {
        Bar b = _bars[i];
        if (b.Telegraph != null) return;
        // 予告：床にセルの選択の枠（白いふち。1-7 の「予告の印を白いふちで」）
        int w = Mathf.Max(8, Mathf.RoundToInt(_size.x * 18f));
        b.Telegraph = K.Node(transform, "Telegraph", new Vector2(b.X - transform.position.x, b.Bottom - transform.position.y + 5f * K.Dot));
        K.Show(b.Telegraph, OfficeArt.CellFrame(w, 9, Color.white), Vector2.zero, 19, "Cell");
    }

    void Activate(int i)
    {
        Bar b = _bars[i];
        if (b.ActiveAt >= 0f) return;
        b.ActiveAt = Time.time;
        if (b.Telegraph != null) Destroy(b.Telegraph.gameObject);

        Color c = OfficeArt.Series(i, _theme);
        float w = _size.x, h = b.Height, px = K.Dot;
        b.Pivot = K.Node(transform, $"Bar{i}", new Vector2(b.X - transform.position.x, b.Bottom - transform.position.y));
        b.Pivot.localScale = new Vector3(1f, 0f, 1f);
        // 白いふち → 中の色 → 上の面（明るい）・右の面（暗い）・左の光
        K.Box(b.Pivot, new Vector2(0f, h * 0.5f), new Vector2(w, h), Color.white, 41, "Edge");
        K.Box(b.Pivot, new Vector2(0f, (h - px) * 0.5f), new Vector2(w - px * 2f, h - px), c, 42, "Fill");
        K.Box(b.Pivot, new Vector2(0f, h - px * 1.5f), new Vector2(w - px * 2f, px), K.Lighten(c, 0.45f), 43, "Top");
        K.Box(b.Pivot, new Vector2(w * 0.5f - px * 2f, (h - px * 2f) * 0.5f), new Vector2(px * 2f, h - px * 2f), K.Darken(c, 0.3f), 43, "Side");
        K.Box(b.Pivot, new Vector2(-w * 0.5f + px * 1.5f, (h - px * 2f) * 0.5f), new Vector2(px, h - px * 2f), K.Lighten(c, 0.25f), 43, "Shine");
    }

    void Vanish(int i)
    {
        Bar b = _bars[i];
        if (b.VanishAt >= 0f) return;
        if (b.ActiveAt < 0f) Activate(i);
        b.VanishAt = Time.time;
    }

    void UpdateBar(int i, Bar b, float now)
    {
        // 知らせが来なくても進む：判定が始まった・柱が消えた
        if (b.ActiveAt < 0f && b.Area != null && b.Area.IsHitActive) Activate(i);
        if (b.ActiveAt >= 0f && b.VanishAt < 0f && b.Joined && b.Target == null) Vanish(i);
        if (b.Pivot == null) return;

        float age = now - b.ActiveAt;
        float sy = K.Pop(age, 0.1f, 1.1f);
        if (_collapseAt >= 0f) sy *= 1f - Mathf.Clamp01((now - _collapseAt) / 0.1f); // 全部いっしょに下へ縮む
        b.Pivot.localScale = new Vector3(1f, Mathf.Max(0.0001f, sy), 1f);
        if (b.VanishAt >= 0f && !b.Dimmed)
        {
            b.Dimmed = true;
            K.FadeAll(b.Pivot, 0.5f); // 判定が終わった：うすくして残す
        }

        if (!b.Grown && age >= 0.1f)
        {
            b.Grown = true;
            // データの数字（最後の棒は大きく）
            bool last = i == _count - 1;
            string text = Labels[Mathf.Min(i, Labels.Length - 1)];
            Sprite s = K.Text(text, Color.white, last ? 2 : 1, K.Hex("1C1C1E"));
            float lift = last ? 0.5f : 0.35f; // 大きい字は、矢印（棒の頭 +0.15）にかからない高さへ
            b.Label = K.Node(transform, "Label", new Vector2(b.X - transform.position.x, b.Bottom - transform.position.y + b.Height + lift));
            K.Show(b.Label, s, Vector2.zero, 44, "Text");
            if (last) _arrowAt = now;
        }
        if (b.Label != null)
        {
            float la = now - b.ActiveAt - 0.1f;
            float s = K.Pop(la, 0.08f);
            b.Label.localScale = new Vector3(s, s, 1f);
            if (_collapseAt >= 0f) K.FadeAll(b.Label, 1f - Mathf.Clamp01((now - _collapseAt) / 0.12f));
        }
    }

    // ───────── 右上がりの矢印（ハッタリ） ─────────

    readonly List<Vector2> _path = new List<Vector2>();
    readonly List<Transform> _segs = new List<Transform>();
    Transform _head;

    void UpdateArrow(float now)
    {
        if (_arrowAt < 0f) return;
        if (_arrow == null)
        {
            _arrow = K.Node(transform, "Arrow", Vector2.zero);
            Vector2 o = transform.position;
            foreach (var b in _bars)
                if (b.Joined) _path.Add(new Vector2(b.X, b.Bottom + b.Height + 0.15f) - o);
            if (_path.Count == 0) { _arrowAt = -1f; return; }
            // 最後の棒の頭から、右上へ画面の上まで
            Vector2 last = _path[_path.Count - 1];
            float top = K.View().yMax + 1f - o.y;
            Vector2 d = new Vector2(_dir * 0.55f, 1f).normalized;
            _path.Add(last + d * Mathf.Max(1f, (top - last.y) / d.y));
            for (int s = 0; s < _path.Count - 1; s++) _segs.Add(K.Box(_arrow, Vector2.zero, Vector2.zero, Color.white, 45, "Seg").transform);
            _head = K.Show(_arrow, K.Make("Office_ArrowHead", ArrowHeadArt, K.Pal(('W', Color.white))), Vector2.zero, 45, "Head").transform;
        }
        float total = 0f;
        for (int s = 0; s < _path.Count - 1; s++) total += Vector2.Distance(_path[s], _path[s + 1]);
        float age = now - _arrowAt;
        float run = total * Mathf.Clamp01(age / 0.25f);
        float acc = 0f;
        Vector2 headPos = _path[0], headDir = Vector2.right * _dir;
        for (int s = 0; s < _path.Count - 1; s++)
        {
            Vector2 a = _path[s], c = _path[s + 1];
            float len = Vector2.Distance(a, c);
            float t = Mathf.Clamp01((run - acc) / Mathf.Max(0.0001f, len));
            Vector2 e = Vector2.Lerp(a, c, t);
            K.SetSegment(_segs[s], a, e, K.Dot * 2f);
            _segs[s].gameObject.SetActive(t > 0f);
            if (t > 0f) { headPos = e; headDir = (c - a).normalized; }
            acc += len;
        }
        _head.localPosition = headPos;
        _head.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(headDir.y, headDir.x) * Mathf.Rad2Deg);
        if (age > 0.25f) K.FadeAll(_arrow, 1f - Mathf.Clamp01((age - 0.25f) / 0.2f));
    }

    // ───────── 進める ─────────

    void Update()
    {
        try { Step(); }
        catch (System.Exception e)
        {
            Debug.LogWarning($"棒グラフの絵でエラー：{e.Message}\n{e.StackTrace}");
            End();
        }
    }

    void Step()
    {
        float now = Time.time;
        UpdateAxis(now);
        bool allGone = true, any = false;
        for (int i = 0; i < _bars.Length; i++)
        {
            Bar b = _bars[i];
            if (!b.Joined) continue;
            any = true;
            UpdateBar(i, b, now);
            if (b.VanishAt < 0f) allGone = false;
        }
        UpdateArrow(now);
        bool arrowDone = _arrowAt < 0f || now - _arrowAt > 0.45f;
        // 全部の柱の判定が終わり、矢印が抜けたら、棒をいっしょに縮める（来ない棒があっても、2.2秒で終わる）
        if (_collapseAt < 0f && ((any && allGone && arrowDone) || now - _born > 2.2f)) _collapseAt = now;
        // 棒が縮みきったら、軸を奥から消す
        if (_collapseAt >= 0f && _axisGoneAt < 0f && now - _collapseAt > 0.12f) _axisGoneAt = now;
    }

    void End()
    {
        if (_ended) return;
        _ended = true;
        if (_fx != null) _fx.Kill();
        else Destroy(gameObject);
    }

    void OnDestroy() => Live.Remove(this);
}
