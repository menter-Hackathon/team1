using UnityEngine;

/// <summary>
/// 瞬間移動で現れる場所の予告（点滅するウィンドウの薄い枠）。AttackRunner が作る。
/// startDelay 秒たってから表示し、endDelay 秒たったら消える（どちらも作った時刻から）。
/// </summary>
public class WarpTelegraph : MonoBehaviour
{
    static readonly Color FrameColor = new Color(1f, 1f, 1f, 0.8f);

    SpriteRenderer _frame;
    float _showAt;
    float _hideAt;

    public bool IsShowing => _frame != null && Time.time >= _showAt && Time.time < _hideAt;
    public float ShowTime => _showAt;

    /// <summary>body は今の体の範囲、offset は移動量（予告は移動先に出す）</summary>
    public static WarpTelegraph Spawn(AttackRunner owner, Bounds body, Vector2 offset, float startDelay, float endDelay)
    {
        GameObject go = owner.CreateSpawnedObject("WarpTelegraph", (Vector2)body.center + offset);
        var t = go.AddComponent<WarpTelegraph>();
        t._frame = AttackRunner.AddVisual(go.transform, PlaceholderSprites.Frame, FrameColor, body.size, 25, "Frame");
        t._frame.enabled = false;
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
        _frame.enabled = now >= _showAt && Mathf.FloorToInt(now / 0.1f) % 2 == 0;
    }
}
