using UnityEngine;

/// <summary>
/// 【仮】2点を結ぶ線（Finder の技の狙いの線。2026-11-18）。点線・実線を、ドットを伸ばさずに並べて描く（SpriteDrawMode.Tiled）。
/// 部品ではなく、作った SpriteRenderer を持つだけの入れ物。線の GameObject が消えたあとに呼んでも何もしない
/// </summary>
public class FinderLine
{
    readonly SpriteRenderer _sr;
    float _thickness = FinderArt.Dot;

    FinderLine(SpriteRenderer sr) => _sr = sr;

    /// <summary>線を作る（parent の子）。太さはドット</summary>
    public static FinderLine Create(Transform parent, Color color, bool dotted, int thicknessDots, int sortingOrder, string objectName = "Line")
    {
        var go = new GameObject(objectName);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;
        var line = new FinderLine(sr);
        line.SetStyle(color, dotted, thicknessDots);
        return line;
    }

    /// <summary>色・点線か実線か・太さ（ドット）を変える</summary>
    public void SetStyle(Color color, bool dotted, int thicknessDots)
    {
        if (_sr == null) return;
        _thickness = Mathf.Max(1, thicknessDots) * FinderArt.Dot;
        _sr.sprite = dotted ? FinderArt.DottedLine : FinderArt.SolidLine;
        _sr.drawMode = SpriteDrawMode.Tiled;
        _sr.tileMode = SpriteTileMode.Continuous;
        _sr.color = color;
        _sr.size = new Vector2(Mathf.Max(_sr.size.x, 0.01f), _thickness);
    }

    public Color Tint
    {
        get => _sr != null ? _sr.color : default;
        set { if (_sr != null) _sr.color = value; }
    }

    public bool Visible
    {
        get => _sr != null && _sr.enabled;
        set { if (_sr != null) _sr.enabled = value; }
    }

    /// <summary>from から to へ線を引く（ゲームの世界の位置）</summary>
    public void Set(Vector2 from, Vector2 to)
    {
        if (_sr == null) return;
        Vector2 d = to - from;
        float len = d.magnitude;
        Transform t = _sr.transform;
        t.position = (from + to) * 0.5f;
        t.rotation = Quaternion.Euler(0f, 0f, len > 0.0001f ? Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg : 0f);
        _sr.size = new Vector2(Mathf.Max(len, 0.01f), _thickness);
    }
}
