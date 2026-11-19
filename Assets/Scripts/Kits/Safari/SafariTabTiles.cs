using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】タブ一覧の1回分（2026-11-19）。SafariTabGridAttack が出し、あとは自分で進む。CustomEffect（形態変化で消える）。
/// 待つ → 6枚のタブ（白い枠。赤いタブは点滅）→ 赤いタブが閉じる（縮みながら判定）→ 消える。
/// </summary>
public class SafariTabTiles : CustomEffect
{
    const int Order = 16;

    class Tile
    {
        public Rect Area;
        public bool Red;
        public Transform Root;
        public SpriteRenderer Fill;
        public readonly List<SpriteRenderer> Frame = new List<SpriteRenderer>();
        public Hitbox Hit;
    }

    AttackRunner _owner;
    AttackData _data;
    SafariTabGridAttack _cfg;
    float _start;
    bool _built;
    bool _fired;
    Rect _arena;
    readonly List<Tile> _tiles = new List<Tile>();

    public static SafariTabTiles Spawn(AttackRunner owner, AttackData data, SafariTabGridAttack cfg, float delay)
    {
        if (owner == null || cfg == null) return null;
        Rect arena = BattleArena.GetBounds();
        GameObject go = owner.CreateSpawnedObject("SafariTabs", arena.center);
        var t = go.AddComponent<SafariTabTiles>();
        t._owner = owner;
        t._data = data;
        t._cfg = cfg;
        t._arena = arena;
        t._start = Time.time + Mathf.Max(0f, delay);
        return t;
    }

    void Build()
    {
        _built = true;
        const int cols = 3, rows = 2;
        float w = _arena.width / cols, h = _arena.height / rows;
        float px = _owner.TargetPoint is Vector2 tp ? tp.x : _arena.center.x;
        int pc = Mathf.Clamp(Mathf.FloorToInt((px - _arena.xMin) / w), 0, cols - 1);
        // 安全なタブ：下の段の1枚（プレイヤーのいる列か、となり）＋ 残りからランダム
        var lowerChoices = new List<int>();
        for (int c = pc - 1; c <= pc + 1; c++) if (c >= 0 && c < cols) lowerChoices.Add(c);
        int safeLower = lowerChoices[Random.Range(0, lowerChoices.Count)]; // 下の段（row 0）の列
        var safe = new HashSet<int> { safeLower };
        var rest = new List<int>();
        for (int i = 0; i < cols * rows; i++) if (i != safeLower) rest.Add(i);
        int safeCount = cols * rows - Mathf.Clamp(_cfg.Red, 1, cols * rows - 1);
        while (safe.Count < safeCount && rest.Count > 0)
        {
            int k = Random.Range(0, rest.Count);
            safe.Add(rest[k]);
            rest.RemoveAt(k);
        }
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int index = r * cols + c; // r＝0 が下の段
                var tile = new Tile { Area = new Rect(_arena.xMin + c * w, _arena.yMin + r * h, w, h), Red = !safe.Contains(index) };
                BuildTile(tile);
                _tiles.Add(tile);
            }
        }
    }

    void BuildTile(Tile t)
    {
        var root = new GameObject("Tab").transform;
        root.gameObject.layer = gameObject.layer;
        root.SetParent(transform, false);
        root.localPosition = t.Area.center - _arena.center;
        t.Root = root;
        Vector2 size = t.Area.size - new Vector2(0.3f, 0.3f); // タブの間のすき間
        Color edge = t.Red ? SafariArt.Red : Color.white;
        t.Fill = SafariArt.Box(root, Vector2.zero, size, t.Red ? SafariArt.A(SafariArt.Red, 0.2f) : new Color(1f, 1f, 1f, 0.06f), Order, "Fill");
        float th = 2f * SafariArt.Px;
        t.Frame.Add(SafariArt.Box(root, new Vector2(0f, size.y * 0.5f - th * 0.5f), new Vector2(size.x, th), edge, Order + 1, "Top"));
        t.Frame.Add(SafariArt.Box(root, new Vector2(0f, -size.y * 0.5f + th * 0.5f), new Vector2(size.x, th), edge, Order + 1, "Bottom"));
        t.Frame.Add(SafariArt.Box(root, new Vector2(-size.x * 0.5f + th * 0.5f, 0f), new Vector2(th, size.y), edge, Order + 1, "Left"));
        t.Frame.Add(SafariArt.Box(root, new Vector2(size.x * 0.5f - th * 0.5f, 0f), new Vector2(th, size.y), edge, Order + 1, "Right"));
        // タブの題名の帯（うすい灰色）と、閉じるボタンの ×
        float barH = 0.55f;
        t.Frame.Add(SafariArt.Box(root, new Vector2(0f, size.y * 0.5f - barH * 0.5f - th), new Vector2(size.x - 2f * th, barH), SafariArt.A(MacUi.Light2, 0.85f), Order + 1, "Bar"));
        SafariArt.Text(root, "x", new Vector2(-size.x * 0.5f + 0.4f, size.y * 0.5f - barH * 0.5f - th), 0.4f, t.Red ? SafariArt.Red : MacUi.Gray2, Order + 2);
        SafariArt.Box(root, new Vector2(0.3f, size.y * 0.5f - barH * 0.5f - th), new Vector2(Mathf.Min(2.4f, size.x * 0.4f), 2f * SafariArt.Px), MacUi.Gray1, Order + 2, "Title");
        if (t.Red) t.Hit = SafariArt.AddHit(_owner, _data, transform, root.localPosition, size); // 判定は縮めない
    }

    void Update()
    {
        float e = Time.time - _start;
        if (e < 0f) return;
        if (!_built) Build();
        float tele = _cfg.Telegraph;
        if (e < tele)
        {
            bool on = SafariArt.Blink(tele - e, 0.15f, 0.05f);
            foreach (var t in _tiles)
            {
                if (!t.Red) continue;
                if (t.Fill != null) t.Fill.enabled = on;
            }
            return;
        }
        float h = e - tele;
        if (!_fired)
        {
            _fired = true;
            foreach (var t in _tiles)
            {
                if (!t.Red) continue;
                if (t.Hit != null) t.Hit.Activate();
                if (t.Fill != null) { t.Fill.enabled = true; t.Fill.color = SafariArt.A(SafariArt.Red, 0.6f); }
            }
        }
        // 赤いタブが閉じる（まん中へ縮む。判定はそのまま）
        float k = Mathf.Clamp01(h / _cfg.HitTime);
        foreach (var t in _tiles)
        {
            if (!t.Red || t.Root == null) continue;
            float s = Mathf.Lerp(1f, 0.6f, k);
            t.Root.localScale = new Vector3(s, s, 1f);
        }
        if (h >= _cfg.HitTime) Destroy(gameObject);
    }
}
