using UnityEngine;

/// <summary>
/// 瞬間移動で現れる場所の予告（点滅するウィンドウの点線の枠と、まん中の文字カーソル）。AttackRunner が作る。
/// startDelay 秒たってから表示し、endDelay 秒たったら消える（どちらも作った時刻から）。
/// </summary>
public class WarpTelegraph : MonoBehaviour
{
    static readonly Color FrameColor = new Color(1f, 1f, 1f, 0.8f);

    SpriteRenderer _frame;
    SpriteRenderer _cursor;
    float _showAt;
    float _hideAt;

    public bool IsShowing => _frame != null && Time.time >= _showAt && Time.time < _hideAt;
    public float ShowTime => _showAt;

    /// <summary>body は今の体の範囲、offset は移動量（予告は移動先に出す）</summary>
    public static WarpTelegraph Spawn(AttackRunner owner, Bounds body, Vector2 offset, float startDelay, float endDelay)
    {
        GameObject go = owner.CreateSpawnedObject("WarpTelegraph", (Vector2)body.center + offset);
        var t = go.AddComponent<WarpTelegraph>();
        GameArt art = GameArt.Instance;
        Sprite frame = art != null && art.TelegraphFrame != null ? art.TelegraphFrame : PlaceholderSprites.Frame;
        t._frame = AttackRunner.AddVisual(go.transform, frame, FrameColor, body.size, 25, "Frame");
        t._frame.enabled = false;
        if (art != null && art.WarpCursor != null)
        {
            // エディタの文字カーソル（縦棒）。体の高さの6割
            t._cursor = AttackRunner.AddVisual(go.transform, art.WarpCursor, Color.white, new Vector2(art.WarpCursor.bounds.size.x, body.size.y * 0.6f), 26, "Cursor");
            t._cursor.enabled = false;
        }
        t._showAt = Time.time + startDelay;
        t._hideAt = Time.time + endDelay;
        return t;
    }

    void Update()
    {
        float now = Time.time;
        if (now >= _hideAt)
        {
            Destroy(gameObject);
            return;
        }
        bool on = now >= _showAt && Mathf.FloorToInt(now / 0.1f) % 2 == 0;
        _frame.enabled = on;
        if (_cursor != null) _cursor.enabled = now >= _showAt && Mathf.FloorToInt(now / 0.2f) % 2 == 0;
    }
}
