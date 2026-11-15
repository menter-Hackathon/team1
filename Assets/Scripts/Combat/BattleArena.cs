using UnityEngine;

/// <summary>
/// 戦う部屋（1画面）の範囲。ボスの移動先・落下物を落とす位置・画面の上端などの基準にする。
/// シーンに1つ置く。無いときはメインカメラに映っている範囲を使う。
/// 範囲は「部屋の内側」：x は左右の壁の内側、y は床の上面から画面の上端まで。
/// </summary>
public class BattleArena : MonoBehaviour
{
    [Tooltip("部屋の内側の範囲（ワールド座標）。y の最小が床の上面")]
    [SerializeField] Rect _bounds = new Rect(-10f, 0f, 20f, 11.5f);

    /// <summary>今のシーンの部屋。無ければ null</summary>
    public static BattleArena Current { get; private set; }

    public Rect Bounds => _bounds;

    void OnEnable() => Current = this;

    void OnDisable()
    {
        if (Current == this) Current = null;
    }

    /// <summary>部屋の範囲。部屋が無ければメインカメラに映っている範囲</summary>
    public static Rect GetBounds()
    {
        if (Current != null) return Current._bounds;
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return new Rect(-10f, 0f, 20f, 11.5f);
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        return new Rect(c.x - w, c.y - h, w * 2f, h * 2f);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(_bounds.center, _bounds.size);
    }
}
