using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】文字列の雨の進行（2026-11-18。docs/app-boss-skills-design.md 4-4）。TerminalStringRainAttack が出す。
/// 予告（カーソル・赤い線）を出し、時間が来たら、その列に文字列（Projectile）を落とす。落とし終えたら自分は消える（文字列は弾として残る）。
/// 当たる文字：白い芯 #FFFFFF に赤 #FF453A のふち、手前、速く下へ。飾りの文字（暗い緑・小さい・奥・ゆっくり上へ）とは、色・大きさ・向き・予告の4つで見分ける
/// </summary>
public class TerminalStringRainEffect : CustomEffect
{
    const float CursorBlink = 0.12f;
    const float CharHeight = 0.6f;      // 【仮】当たる文字の高さ（マス）
    const int LineOrder = 6;            // 予告の線（床・足場より手前）
    const int CursorOrder = 41;
    const int EdgeOrder = 40;           // 文字列の赤いふち
    const int CoreOrder = 41;           // 文字列の白い芯

    class Volley
    {
        public float DropAt;
        public List<int> Columns;
        public Transform Root;
        public readonly List<SpriteRenderer> Cursors = new List<SpriteRenderer>();
        public bool Dropped;
    }

    AttackRunner _owner;
    AttackData _data;
    TerminalStringRainAttack _cfg;
    Rect _arena;
    float _start;
    int _started;
    readonly List<Volley> _volleys = new List<Volley>();
    List<int> _lastFalling;

    public static TerminalStringRainEffect Spawn(AttackRunner owner, AttackData data, TerminalStringRainAttack cfg)
    {
        if (owner == null || data == null || cfg == null) return null;
        GameObject go = owner.CreateSpawnedObject("TerminalStringRain", Vector2.zero);
        var rain = go.AddComponent<TerminalStringRainEffect>();
        rain._owner = owner;
        rain._data = data;
        rain._cfg = cfg;
        rain._arena = BattleArena.GetBounds();
        rain._start = Time.time;
        return rain;
    }

    void Update()
    {
        if (_owner == null)
        {
            Destroy(gameObject);
            return;
        }
        float t = Time.time - _start;
        while (_started < _cfg.Volleys && t >= _started * _cfg.VolleyInterval)
        {
            StartVolley();
            _started++;
        }

        bool done = _started >= _cfg.Volleys;
        bool cursorOn = Mathf.FloorToInt(Time.time / CursorBlink) % 2 == 0;
        foreach (var v in _volleys)
        {
            if (v.Dropped) continue;
            done = false;
            if (Time.time >= v.DropAt)
            {
                Drop(v);
                continue;
            }
            foreach (var c in v.Cursors)
            {
                if (c != null) c.enabled = cursorOn;
            }
        }
        if (done) Destroy(gameObject);
    }

    float ColumnWidth => _arena.width / Mathf.Max(1, _cfg.Columns);
    float ColumnX(int c) => _arena.xMin + ColumnWidth * (c + 0.5f);

    void StartVolley()
    {
        float px = _owner.TargetPoint is Vector2 tp ? tp.x : _arena.center.x;
        var v = new Volley { DropAt = Time.time + _cfg.TelegraphTime, Columns = PickFalling(px) };
        var root = new GameObject("Telegraph").transform;
        root.gameObject.layer = gameObject.layer;
        root.SetParent(transform, false);
        root.position = Vector3.zero; // 子の位置＝ゲームの世界の位置
        v.Root = root;

        float height = _arena.height;
        Color line = TerminalArt.WithAlpha(TerminalArt.Red, 0.35f);
        foreach (int c in v.Columns)
        {
            float x = ColumnX(c);
            // 天井から床まで、うすい赤の細い線（2ドット）
            TerminalArt.Box(root, new Vector2(x, _arena.yMin + height * 0.5f), new Vector2(TerminalArt.Pixel * 2f, height), line, LineOrder, "Line");
            // 天井に白い四角いカーソル
            v.Cursors.Add(TerminalArt.Box(root, new Vector2(x, _arena.yMax - 0.45f), new Vector2(0.55f, 0.8f), TerminalArt.White, CursorOrder, "Cursor"));
        }
        _volleys.Add(v);
    }

    /// <summary>
    /// 落ちる列を選ぶ。安全な列（落ちない列）の1本は、プレイヤーの今の列から safeReach マス以内に必ず入れる。
    /// 前の回とまったく同じ並びは、なるべく避ける
    /// </summary>
    List<int> PickFalling(float playerX)
    {
        int n = Mathf.Max(2, _cfg.Columns);
        int falling = Mathf.Clamp(_cfg.FallingColumns, 1, n - 1);
        float colW = ColumnWidth;
        int pc = Mathf.Clamp(Mathf.FloorToInt((playerX - _arena.xMin) / colW), 0, n - 1);
        int reach = Mathf.Max(0, Mathf.FloorToInt(_cfg.SafeReach / colW + 0.001f));
        int lo = Mathf.Max(0, pc - reach), hi = Mathf.Min(n - 1, pc + reach);

        List<int> result = null;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            var safe = new List<int> { Random.Range(lo, hi + 1) };
            var others = new List<int>();
            for (int c = 0; c < n; c++)
            {
                if (!safe.Contains(c)) others.Add(c);
            }
            for (int i = others.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = others[i];
                others[i] = others[j];
                others[j] = tmp;
            }
            for (int i = 0; safe.Count < n - falling && i < others.Count; i++) safe.Add(others[i]);
            result = new List<int>();
            for (int c = 0; c < n; c++)
            {
                if (!safe.Contains(c)) result.Add(c);
            }
            if (_lastFalling == null || !SameColumns(result, _lastFalling)) break;
        }
        _lastFalling = result;
        return result;
    }

    static bool SameColumns(List<int> a, List<int> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    void Drop(Volley v)
    {
        v.Dropped = true;
        if (v.Root != null) Destroy(v.Root.gameObject);
        foreach (int c in v.Columns) SpawnString(ColumnX(c));
    }

    /// <summary>文字列を1本落とす（弾。判定は列の柱として1つ）</summary>
    void SpawnString(float x)
    {
        float length = Mathf.Max(0.5f, _data.HitSize.x); // 弾の判定は x が進む向きの長さ
        var pos = new Vector2(x, _arena.yMax + length * 0.5f + 0.1f); // 天井のすぐ上から
        Projectile p = Projectile.Spawn(_owner, _data, pos, Vector2.down);
        if (p == null) return;

        // いつもの絵（白い四角）は隠して、縦に並んだ文字を出す
        foreach (var sr in p.GetComponentsInChildren<SpriteRenderer>()) sr.enabled = false;
        var holder = new GameObject("Text").transform;
        holder.gameObject.layer = p.gameObject.layer;
        holder.SetParent(p.transform, false);
        holder.rotation = Quaternion.identity; // 弾は進む向き（下）に回っているので、文字は立てる

        int n = Mathf.Max(1, _cfg.StringLength);
        var sb = new System.Text.StringBuilder(n * 2);
        for (int i = 0; i < n; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(TerminalArt.RainChars[Random.Range(0, TerminalArt.RainChars.Length)]);
        }
        string text = sb.ToString();
        float spacing = length / n;

        // 赤いふち（上下左右に1ドットずらした4枚）と、芯（2026-11-18：スキルの見た目 13-2。先頭の1字だけ白、後ろは緑がだんだん暗く 3段）
        float d = TerminalArt.Pixel;
        var offsets = new[] { new Vector2(-d, 0f), new Vector2(d, 0f), new Vector2(0f, -d), new Vector2(0f, d) };
        string[] chars = text.Split('\n');
        for (int i = 0; i < chars.Length; i++)
        {
            Vector2 at = new Vector2(0f, ((chars.Length - 1) * 0.5f - i) * spacing);
            foreach (var o in offsets)
            {
                TextMesh edge = WorldText.Create(holder, chars[i], CharHeight, TerminalArt.Red, EdgeOrder, true, TextAnchor.MiddleCenter, "Edge");
                edge.richText = false;
                edge.transform.localPosition = at + o;
            }
            int fromHead = chars.Length - 1 - i; // 落ちる向きの先頭（いちばん下）が 0
            Color c = fromHead == 0 ? TerminalArt.White : TrailGreens[Mathf.Clamp((fromHead - 1) * 3 / Mathf.Max(1, chars.Length - 1), 0, 2)];
            TextMesh core = WorldText.Create(holder, chars[i], CharHeight, c, CoreOrder, true, TextAnchor.MiddleCenter, "Core");
            core.richText = false;
            core.transform.localPosition = new Vector3(at.x, at.y, -0.001f);
        }
        p.HitTerrain += OnStringLanded;
    }

    static readonly Color[] TrailGreens = { RuntimeSprites.Hex("33FF66"), RuntimeSprites.Hex("27CC52"), RuntimeSprites.Hex("1C9A3E") };

    /// <summary>床に着いたら、字が左右へ 2個ずつはねる（13-2）</summary>
    void OnStringLanded(Projectile p, Vector2 point)
    {
        AttackRunner owner = p != null ? p.Owner : _owner;
        for (int i = 0; i < 4; i++)
        {
            float side = i < 2 ? -1f : 1f;
            Vector2 v = new Vector2(side * Random.Range(2f, 4.5f), Random.Range(4f, 7f));
            string ch = TerminalArt.RainChars[Random.Range(0, TerminalArt.RainChars.Length)].ToString();
            FxAnim a = FxKit.Text(ch, point + Vector2.up * 0.2f, 0.4f, TrailGreens[i % 2], CoreOrder, Random.Range(0.35f, 0.5f), new Color(0f, 0f, 0f, 0.8f))
                .Velocity(v).Gravity(26f).FadeFrom(0.5f);
            if (owner != null)
            {
                a.gameObject.AddComponent<SkinFxMarker>();
                owner.Register(a.gameObject);
            }
        }
    }
}
