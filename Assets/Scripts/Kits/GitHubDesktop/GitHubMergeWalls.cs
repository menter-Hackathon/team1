using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Merge conflict の壁（2026-11-19）。GitHubMergeConflictAttack が出し、あとは自分で進む。場に残る技の効果（Lingers）。
/// 予告（壁の字・床の矢印・緑の =======）→ 閉じる（当たる）→ 止まる（当たる）→ 開く（当たらない）→ 消える。
/// </summary>
public class GitHubMergeWalls : CustomEffect
{
    const int Order = 17;

    class Wall
    {
        public int Side; // -1＝左、+1＝右
        public float Target; // 閉じきったときの幅
        public SpriteRenderer Fill;
        public SpriteRenderer Edge;
        public Hitbox Hit;
        public readonly List<TextMesh> Marks = new List<TextMesh>();
    }

    AttackRunner _owner;
    GitHubMergeConflictAttack _cfg;
    Rect _arena;
    float _safeL, _safeR;
    float _start;
    readonly Wall[] _walls = new Wall[2];
    readonly List<SpriteRenderer> _tele = new List<SpriteRenderer>();
    readonly List<TextMesh> _teleText = new List<TextMesh>();
    bool _active;

    public override bool Lingers => true;

    public static GitHubMergeWalls Spawn(AttackRunner owner, AttackData data, GitHubMergeConflictAttack cfg, Rect arena, float safeCenter)
    {
        if (owner == null || data == null || cfg == null) return null;
        GameObject go = owner.CreateSpawnedObject("GitHubMergeWalls", arena.center);
        var w = go.AddComponent<GitHubMergeWalls>();
        w._owner = owner;
        w._cfg = cfg;
        w._arena = arena;
        w._safeL = safeCenter - cfg.SafeWidth * 0.5f;
        w._safeR = safeCenter + cfg.SafeWidth * 0.5f;
        w._start = Time.time;
        w.Build(data);
        return w;
    }

    Vector2 L(float x, float y) => new Vector2(x - _arena.center.x, y - _arena.center.y);

    void Build(AttackData data)
    {
        float h = _arena.height;
        Color red = GitHubDesktopArt.Danger;
        Color green = GitHubDesktopArt.Green;

        // 安全な所：緑の点線の柱と =======
        float sc = (_safeL + _safeR) * 0.5f;
        _tele.Add(GitHubDesktopArt.Box(transform, L(sc, _arena.center.y), new Vector2(_safeR - _safeL, h), GitHubDesktopArt.A(green, 0.14f), Order - 1, "SafeFill"));
        _tele.Add(GitHubDesktopArt.Dotted(transform, L(_safeL, _arena.center.y), h, 2, 90f, green, Order));
        _tele.Add(GitHubDesktopArt.Dotted(transform, L(_safeR, _arena.center.y), h, 2, 90f, green, Order));
        _teleText.Add(GitHubDesktopArt.Text(transform, "=======", L(sc, _arena.yMin + 4.6f), 0.45f, green, Order + 2));

        // 壁の字（左右の端）
        _teleText.Add(GitHubDesktopArt.Text(transform, "<<<<<<< HEAD", L(_arena.xMin + 0.3f, _arena.yMin + 7.6f), 0.4f, red, Order + 2, TextAnchor.MiddleLeft));
        _teleText.Add(GitHubDesktopArt.Text(transform, ">>>>>>> virus", L(_arena.xMax - 0.3f, _arena.yMin + 7.6f), 0.4f, red, Order + 2, TextAnchor.MiddleRight));

        // 床の矢印（閉じる向き）
        Sprite arrow = GitHubDesktopArt.FloorArrow();
        for (int side = -1; side <= 1; side += 2)
        {
            float from = side < 0 ? _arena.xMin + 0.8f : _arena.xMax - 0.8f;
            float to = side < 0 ? _safeL - 0.6f : _safeR + 0.6f;
            int n = Mathf.Max(1, Mathf.FloorToInt(Mathf.Abs(to - from) / 1.6f));
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Lerp(from, to, n > 0 ? (float)i / n : 0f);
                SpriteRenderer a = RuntimeSprites.Show(transform, arrow, L(x, _arena.yMin + 0.25f), Order + 1, 1f, "Arrow");
                a.color = red;
                a.flipX = side > 0;
                _tele.Add(a);
            }
        }

        // 壁（幅 0 から）
        for (int i = 0; i < 2; i++)
        {
            int side = i == 0 ? -1 : 1;
            var w = new Wall { Side = side, Target = side < 0 ? _safeL - _arena.xMin : _arena.xMax - _safeR };
            w.Fill = GitHubDesktopArt.Box(transform, Vector2.zero, Vector2.zero, GitHubDesktopArt.A(red, 0.45f), Order, "Wall");
            w.Edge = GitHubDesktopArt.Box(transform, Vector2.zero, Vector2.zero, red, Order + 1, "WallEdge");
            for (int k = 0; k < 6; k++)
                w.Marks.Add(GitHubDesktopArt.Text(transform, side < 0 ? "<" : ">", Vector2.zero, 0.5f, Color.white, Order + 2));
            w.Hit = GitHubDesktopArt.AddHit(_owner, data, transform, Vector2.zero, new Vector2(0.01f, h));
            _walls[i] = w;
            SetWall(w, 0f);
        }
    }

    void SetWall(Wall w, float width)
    {
        float h = _arena.height;
        float x0 = w.Side < 0 ? _arena.xMin : _arena.xMax - width;
        float cx = x0 + width * 0.5f;
        GitHubDesktopArt.SetBox(w.Fill, L(cx, _arena.center.y), new Vector2(width, h));
        float edgeX = w.Side < 0 ? _arena.xMin + width : _arena.xMax - width;
        GitHubDesktopArt.SetBox(w.Edge, L(edgeX, _arena.center.y), new Vector2(width > 0.01f ? 3f * GitHubDesktopArt.Px : 0f, h));
        GitHubDesktopArt.SetHit(w.Hit, L(cx, _arena.center.y), new Vector2(Mathf.Max(0.01f, width), h));
        float scroll = Mathf.Repeat(Time.time * 1.5f, 2f);
        for (int k = 0; k < w.Marks.Count; k++)
        {
            TextMesh tm = w.Marks[k];
            if (tm == null) continue;
            tm.gameObject.SetActive(width > 0.8f);
            float y = _arena.yMin + 0.6f + k * 2f + scroll;
            tm.transform.localPosition = new Vector3(edgeX - w.Side * 0.45f - _arena.center.x, y - _arena.center.y, -0.001f);
        }
    }

    void Update()
    {
        float e = Time.time - _start;
        float tele = _cfg.Telegraph, close = _cfg.Close, hold = _cfg.Hold, open = _cfg.Open;
        if (e < tele)
        {
            bool on = GitHubDesktopArt.Blink(tele - e, 0.15f, 0.05f);
            foreach (var sr in _tele) if (sr != null && sr.name == "Arrow") sr.enabled = on;
            return;
        }
        float k;
        if (e < tele + close)
        {
            if (!_active)
            {
                _active = true;
                foreach (var w in _walls) if (w.Hit != null) w.Hit.Activate();
                foreach (var sr in _tele) if (sr != null && sr.name == "Arrow") sr.enabled = false;
            }
            k = (e - tele) / close;
        }
        else if (e < tele + close + hold) k = 1f;
        else if (e < tele + close + hold + open)
        {
            if (_active)
            {
                _active = false;
                foreach (var w in _walls) if (w.Hit != null) w.Hit.Deactivate();
                foreach (var tm in _teleText) if (tm != null) tm.gameObject.SetActive(false);
            }
            k = 1f - (e - tele - close - hold) / open;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        foreach (var w in _walls) SetWall(w, w.Target * Mathf.Clamp01(k));
    }
}
