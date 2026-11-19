using UnityEngine;

/// <summary>
/// 選択ツールの演出（2026-11-19。Photoshop のボスが雑魚を出す前。攻撃判定はない。見た目・秒数は【仮】）。
/// ・ボスからマウスカーソルが伸びてきて、選ぶ四角の左上へ動く
/// ・そこから右下へドラッグして、点線の四角（選択範囲）が広がる。点線は流れる
/// ・少し待って、終わり（IsDone）。AttackRunner が、四角の下の真ん中に雑魚を出す。そのあと四角は点滅して消える
/// </summary>
public class MarqueeSelect : MonoBehaviour
{
    const float MoveTime = 0.5f;
    const float DragTime = 0.4f;
    const float HoldTime = 0.35f;
    const float VanishTime = 0.3f;
    const float LineWidth = 2f / 18f;

    Vector2 _from;
    Rect _rect;
    float _start;
    SpriteRenderer _cursor;
    readonly SpriteRenderer[] _edges = new SpriteRenderer[4];
    Sprite[] _dash;

    /// <summary>選び終わったら true（ここで雑魚を出す）</summary>
    public bool IsDone => Time.time - _start >= MoveTime + DragTime + HoldTime;
    /// <summary>雑魚を出す位置（四角の下の真ん中）</summary>
    public Vector2 SpawnPoint => new Vector2(_rect.center.x, _rect.yMin);

    /// <summary>from（ボスの位置）から、rect を選ぶ演出を始める</summary>
    public static MarqueeSelect Spawn(AttackRunner owner, Vector2 from, Rect rect)
    {
        GameObject go = owner.CreateSpawnedObject("MarqueeSelect", from);
        var m = go.AddComponent<MarqueeSelect>();
        m._from = from;
        m._rect = rect;
        m._start = Time.time;
        GameArt art = GameArt.Instance;
        m._dash = art != null && art.MarqueeDash != null && art.MarqueeDash.Length > 0 ? art.MarqueeDash : null;
        for (int i = 0; i < 4; i++)
        {
            var e = new GameObject($"Edge_{i}");
            e.layer = go.layer;
            e.transform.SetParent(go.transform, false);
            var sr = e.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 44;
            if (m._dash != null)
            {
                sr.sprite = m._dash[0];
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
            }
            else
            {
                sr.sprite = PlaceholderSprites.Square;
                sr.color = Color.white;
            }
            sr.enabled = false;
            m._edges[i] = sr;
        }
        var c = new GameObject("Cursor");
        c.layer = go.layer;
        c.transform.SetParent(go.transform, false);
        m._cursor = c.AddComponent<SpriteRenderer>();
        m._cursor.sprite = art != null && art.MouseCursor != null ? art.MouseCursor : PlaceholderSprites.Square;
        m._cursor.sortingOrder = 46;
        m._cursor.transform.position = from;
        return m;
    }

    void Update()
    {
        float age = Time.time - _start;
        var topLeft = new Vector2(_rect.xMin, _rect.yMax);
        var bottomRight = new Vector2(_rect.xMax, _rect.yMin);

        if (age < MoveTime)
        {
            // カーソルが伸びてくる（残像を少し残す）
            float k = Mathf.SmoothStep(0f, 1f, age / MoveTime);
            _cursor.transform.position = Vector2.Lerp(_from, topLeft, k);
            if (Mathf.FloorToInt(age / 0.04f) % 2 == 0) Ghost();
            return;
        }

        // ドラッグして四角が広がる
        float d = Mathf.Clamp01((age - MoveTime) / DragTime);
        Vector2 corner = Vector2.Lerp(topLeft, bottomRight, 1f - (1f - d) * (1f - d));
        _cursor.transform.position = corner;
        SetBox(Rect.MinMaxRect(topLeft.x, corner.y, corner.x, topLeft.y), age);

        float after = age - (MoveTime + DragTime + HoldTime);
        if (after >= 0f)
        {
            // 選び終わったら、四角が点滅して消える。カーソルも消える
            _cursor.enabled = false;
            bool on = Mathf.FloorToInt(after / 0.06f) % 2 == 0;
            foreach (var e in _edges) e.enabled = on;
            if (after >= VanishTime) Destroy(gameObject);
        }
    }

    /// <summary>点線の四角（4本の辺）。点線は 0.12秒ごとにずれて流れる</summary>
    void SetBox(Rect r, float age)
    {
        Sprite dash = _dash != null ? _dash[Mathf.FloorToInt(age / 0.12f) % _dash.Length] : null;
        float w = Mathf.Max(0.05f, r.width), h = Mathf.Max(0.05f, r.height);
        Place(_edges[0], new Vector2(r.center.x, r.yMax), new Vector2(w, LineWidth), 0f, dash);
        Place(_edges[1], new Vector2(r.center.x, r.yMin), new Vector2(w, LineWidth), 0f, dash);
        Place(_edges[2], new Vector2(r.xMin, r.center.y), new Vector2(h, LineWidth), 90f, dash);
        Place(_edges[3], new Vector2(r.xMax, r.center.y), new Vector2(h, LineWidth), 90f, dash);
    }

    static void Place(SpriteRenderer sr, Vector2 center, Vector2 size, float angle, Sprite dash)
    {
        sr.enabled = true;
        sr.transform.position = center;
        sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        if (dash != null)
        {
            sr.sprite = dash;
            sr.size = size;
            sr.transform.localScale = Vector3.one;
        }
        else
        {
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
        }
    }

    void Ghost()
    {
        var go = new GameObject("CursorGhost");
        go.transform.position = _cursor.transform.position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _cursor.sprite;
        sr.sortingOrder = 45;
        sr.color = new Color(1f, 1f, 1f, 0.4f);
        go.AddComponent<FadeAndDestroy>().Setup(0.15f);
    }
}
