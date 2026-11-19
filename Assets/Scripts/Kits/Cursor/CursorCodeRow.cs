using System.Text;
using UnityEngine;

/// <summary>
/// 【仮】Generating... のコードの行1つ（2026-11-19）。CursorGeneratingAttack が出し、あとは自分で進む。CustomEffect（形態変化で消える）。
/// 待つ → 灰色の行（安全）→ 赤に変わる（予告）→ 左から右へ打たれる（打たれた所が少しの間当たる）→ 消える。
/// </summary>
public class CursorCodeRow : CustomEffect
{
    const int Order = 17;
    const float SegW = 1f;

    AttackRunner _owner;
    AttackData _data;
    CursorGeneratingAttack _cfg;
    float _start;
    float _y;
    int _index;
    bool _built;
    bool _red;
    Rect _arena;
    TextMesh _text;
    SpriteRenderer _bg;
    SpriteRenderer _edgeTop, _edgeBottom;
    SpriteRenderer _typed;
    SpriteRenderer _caret;
    Hitbox[] _segs;
    float[] _segOn;

    public static CursorCodeRow Spawn(AttackRunner owner, AttackData data, CursorGeneratingAttack cfg, float y, float delay, int index)
    {
        if (owner == null || cfg == null) return null;
        Rect arena = BattleArena.GetBounds();
        GameObject go = owner.CreateSpawnedObject("CursorCodeRow", new Vector2(arena.center.x, y));
        var r = go.AddComponent<CursorCodeRow>();
        r._owner = owner;
        r._data = data;
        r._cfg = cfg;
        r._arena = arena;
        r._y = y;
        r._index = index;
        r._start = Time.time + Mathf.Max(0f, delay);
        return r;
    }

    void Build()
    {
        _built = true;
        float w = _arena.width, h = _cfg.Height;
        _bg = CursorArt.Box(transform, Vector2.zero, new Vector2(w, h), CursorArt.A(CursorArt.Black, 0.45f), Order, "Bg");
        float t = CursorArt.Px;
        _edgeTop = CursorArt.Box(transform, new Vector2(0f, h * 0.5f - t), new Vector2(w, 2f * t), CursorArt.A(CursorArt.Ghost, 0.8f), Order + 1, "EdgeTop");
        _edgeBottom = CursorArt.Box(transform, new Vector2(0f, -h * 0.5f + t), new Vector2(w, 2f * t), CursorArt.A(CursorArt.Ghost, 0.8f), Order + 1, "EdgeBottom");
        var sb = new StringBuilder();
        int chars = Mathf.CeilToInt(w / 0.4f);
        int k = _index;
        while (sb.Length < chars)
        {
            sb.Append(CursorArt.CodeLines[k % CursorArt.CodeLines.Length]);
            sb.Append("  ");
            k += 2;
        }
        _text = CursorArt.Text(transform, sb.ToString(0, chars), new Vector2(-w * 0.5f, 0f), 0.4f, CursorArt.Ghost, Order + 3, TextAnchor.MiddleLeft);
        _typed = CursorArt.Box(transform, new Vector2(-w * 0.5f, 0f), new Vector2(0f, h), CursorArt.A(CursorArt.Red, 0.6f), Order + 2, "Typed");
        _caret = CursorArt.Box(transform, new Vector2(-w * 0.5f, 0f), new Vector2(0.15f, h), Color.white, Order + 4, "Caret");
        _caret.enabled = false;
        int n = Mathf.CeilToInt(w / SegW);
        _segs = new Hitbox[n];
        _segOn = new float[n];
        for (int i = 0; i < n; i++)
        {
            float cx = -w * 0.5f + (i + 0.5f) * SegW;
            _segs[i] = CursorArt.AddHit(_owner, _data, transform, new Vector2(cx, 0f), new Vector2(SegW, h * 0.9f));
            _segOn[i] = -1f;
        }
    }

    void Update()
    {
        float e = Time.time - _start;
        if (e < 0f) return;
        if (!_built) Build();
        if (e < _cfg.Ghost) return;
        float r = e - _cfg.Ghost;
        if (r < _cfg.Telegraph)
        {
            if (!_red)
            {
                _red = true;
                WorldText.SetColor(_text, CursorArt.A(CursorArt.Red, 0.9f));
                _edgeTop.color = CursorArt.Red;
                _edgeBottom.color = CursorArt.Red;
            }
            bool on = CursorArt.Blink(_cfg.Telegraph - r);
            _edgeTop.enabled = on;
            _edgeBottom.enabled = on;
            return;
        }
        // 打つ
        _edgeTop.enabled = _edgeBottom.enabled = true;
        float w = _arena.width;
        float typedW = Mathf.Min(w, (r - _cfg.Telegraph) * _cfg.TypeSpeed);
        CursorArt.SetBox(_typed, new Vector2(-w * 0.5f + typedW * 0.5f, 0f), new Vector2(typedW, _cfg.Height));
        _caret.enabled = typedW < w;
        _caret.transform.localPosition = new Vector2(-w * 0.5f + typedW, 0f);
        bool any = false;
        for (int i = 0; i < _segs.Length; i++)
        {
            float cx = (i + 0.5f) * SegW;
            if (_segOn[i] < 0f && typedW >= cx)
            {
                _segOn[i] = Time.time;
                if (_segs[i] != null) _segs[i].Activate();
            }
            if (_segOn[i] >= 0f && Time.time - _segOn[i] >= _cfg.Linger && _segs[i] != null && _segs[i].IsActive) _segs[i].Deactivate();
            if (_segOn[i] < 0f || (_segs[i] != null && _segs[i].IsActive)) any = true;
        }
        if (typedW >= w) WorldText.SetColor(_text, Color.white);
        if (!any) Destroy(gameObject);
    }
}

/// <summary>【仮】ボスの頭の上の Generating...（2026-11-19）。時間が来たら消える。CustomEffect</summary>
public class CursorGeneratingBadge : CustomEffect
{
    AttackRunner _owner;
    float _until;
    TextMesh _text;

    public static CursorGeneratingBadge Spawn(AttackRunner owner, float seconds)
    {
        if (owner == null) return null;
        GameObject go = owner.CreateSpawnedObject("CursorGeneratingBadge", CursorArt.HeadOf(owner));
        var b = go.AddComponent<CursorGeneratingBadge>();
        b._owner = owner;
        b._until = Time.time + seconds;
        CursorArt.Box(go.transform, Vector2.zero, new Vector2(6.0f, 0.75f), CursorArt.A(CursorArt.Black, 0.9f), 40, "Bg");
        b._text = CursorArt.Text(go.transform, "Generating...", Vector2.zero, 0.4f, CursorArt.Ghost, 41);
        return b;
    }

    void Update()
    {
        if (Time.time >= _until) { Destroy(gameObject); return; }
        if (_owner != null) transform.position = CursorArt.HeadOf(_owner);
        if (_text != null)
        {
            int dots = Mathf.FloorToInt(Time.time / 0.3f) % 4;
            _text.text = "Generating" + new string('.', dots) + new string(' ', 3 - dots);
        }
    }
}
