using UnityEngine;

/// <summary>
/// 【仮】⌘K の出る前のゴースト（2026-11-19）。CursorCmdKAttack が出す。頭の上に ⌘ K のキー、出る所に灰色の形。少しで消える。
/// </summary>
public class CursorCmdKGhost : MonoBehaviour
{
    const int Order = 24;
    const float Life = 0.45f;

    float _born;
    SpriteRenderer[] _srs;

    public static CursorCmdKGhost Spawn(AttackRunner owner, AttackData pick, float preview)
    {
        if (owner == null) return null;
        Vector2 body = RewardKit.BodyCenter(owner.transform);
        GameObject go = owner.CreateSpawnedObject("CursorCmdK", body);
        var g = go.AddComponent<CursorCmdKGhost>();
        g._born = Time.time;
        g.Build(owner, pick, body);
        return g;
    }

    void Build(AttackRunner owner, AttackData pick, Vector2 body)
    {
        // 頭の上に ⌘ K
        RuntimeSprites.Show(transform, MacUi.KeySprite('⌘'), new Vector2(-0.3f, 1.25f), Order + 2, 1f, "KeyCmd");
        RuntimeSprites.Show(transform, MacUi.KeySprite('K'), new Vector2(0.3f, 1.25f), Order + 2, 1f, "KeyK");
        // 出る所の灰色の形
        if (pick != null)
        {
            int f = owner.Facing >= 0 ? 1 : -1;
            Color ghost = CursorArt.A(CursorArt.Ghost, 0.45f);
            switch (pick.Kind)
            {
                case AttackKind.Area:
                {
                    Vector2 size = pick.Shape == AreaShape.Circle ? new Vector2(pick.AreaSize.x, pick.AreaSize.x) : pick.AreaSize;
                    size = Vector2.Min(size, new Vector2(8f, 8f));
                    Vector2 c = new Vector2(f * pick.ForwardDistance, size.y * 0.5f - 0.5f);
                    CursorArt.Box(transform, c, size, ghost, Order, "Shape");
                    break;
                }
                case AttackKind.Projectile:
                    CursorArt.Dotted(transform, new Vector2(f * 2f, 0f), 4f, 3, 0f, CursorArt.A(CursorArt.Ghost, 0.8f), Order);
                    break;
                default:
                    CursorArt.Box(transform, new Vector2(f * 1.5f, 0f), new Vector2(1f, 1f), ghost, Order, "Shape");
                    break;
            }
        }
        _srs = GetComponentsInChildren<SpriteRenderer>();
    }

    void Update()
    {
        float k = (Time.time - _born) / Life;
        if (k >= 1f) { Destroy(gameObject); return; }
        if (_srs == null) return;
        float a = k < 0.5f ? 1f : 1f - (k - 0.5f) * 2f;
        foreach (var sr in _srs)
        {
            if (sr == null) continue;
            Color c = sr.color;
            c.a = Mathf.Min(c.a, a);
            sr.color = c;
        }
    }
}
