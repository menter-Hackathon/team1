using UnityEngine;

/// <summary>
/// 【仮】能力 Revert の状態（2026-11-19）。GitHubRevertAttack がプレイヤーに実行時に足す。
/// 置いたコミットの丸と、丸とプレイヤーを結ぶ紫の点線を持つ。丸は時間が来たら消える（次の1回目からやり直し）。
/// </summary>
public class GitHubRevertHolder : MonoBehaviour
{
    const int Order = 21;

    AttackRunner _owner;
    GitHubRevertAttack _cfg;
    GameObject _commit;
    SpriteRenderer _commitArt;
    SpriteRenderer _line;
    Vector2 _commitFeet;
    float _expireAt;

    /// <summary>丸を置いているか</summary>
    public bool HasCommit => _commit != null;
    /// <summary>最後に戻ったフレーム（クールタイムを始めるかの答えに使う）</summary>
    public int LastRevertFrame { get; private set; } = -1;

    public static GitHubRevertHolder GetOrAdd(AttackRunner owner, GitHubRevertAttack cfg)
    {
        var h = owner.GetComponent<GitHubRevertHolder>();
        if (h == null) h = owner.gameObject.AddComponent<GitHubRevertHolder>();
        h._owner = owner;
        h._cfg = cfg;
        return h;
    }

    /// <summary>今いる場所（空中なら真下の床・足場）に丸を置く</summary>
    public void Place()
    {
        Clear();
        Vector2 feet = Feet();
        float gy = feet.y;
        // 2026-11-20：前は体の真ん中の 1本の線で床を探していたので、段・壁の上の端（体の半分が外に出ている所）や、壁のすぐ横の空中で置くと、
        // 丸が下の床に落ち、戻ったときに体が段・壁にめり込んで動けなくなっていた。体の箱ごと下へ動かして、体が収まる床を探す。立っているなら今の場所
        var body = GetComponent<KinematicBody2D>();
        if ((body == null || !body.IsGrounded) && TryFindLanding(feet, out float g)) gy = g;
        _commitFeet = new Vector2(feet.x, gy);
        _commit = _owner.CreateSpawnedObject("GitHubRevertCommit", _commitFeet + Vector2.up * 0.5f);
        _commitArt = RuntimeSprites.Show(_commit.transform, GitHubDesktopArt.RevertCommit(), Vector2.zero, Order + 1, 1f, "Commit");
        _line = GitHubDesktopArt.Dotted(_commit.transform, Vector2.zero, 0.1f, 1, 0f, GitHubDesktopArt.A(GitHubDesktopArt.Purple, 0.85f), Order);
        _expireAt = Time.time + _cfg.Keep;
        FxKit.Ring(_commit.transform.position, 1.2f, 0.15f, GitHubDesktopArt.Purple);
    }

    /// <summary>丸の場所へ戻り、通り道の敵に当たる</summary>
    public void Revert(AttackData data)
    {
        // ボス部屋の内と外をまたいで戻らない（門が閉じたあとに外へ出ないように）。またぐときは、今の場所に置き直す（クールタイムは始めない）
        Rect arena = BattleArena.GetBounds();
        if (arena.Contains(RewardKit.BodyCenter(transform)) != arena.Contains(_commitFeet + Vector2.up * 0.5f))
        {
            Place();
            return;
        }
        // 2026-11-20：丸の所に体が収まらない（あとから置いた箱・閉じた門・壁などが来た）ときは、近くの空いている所へずらす。
        // 体が床・壁にめり込むと、KinematicBody2D がどの向きにも動けなくなるため。どこにも空きが無ければ、今の場所に置き直す（クールタイムは始めない）
        Vector2 feetTo = _commitFeet + Vector2.up * 0.02f;
        if (!TryFindRoom(ref feetTo, arena, arena.Contains(RewardKit.BodyCenter(transform))))
        {
            Place();
            return;
        }
        // 2026-11-20：位置と足元のずれは、体の箱の形から出す。前は transform.position - Feet() で出していたが、
        // transform は補間で物理の位置より遅れるので、ジャンプで上がっている途中に戻ると、そのずれの分だけ床にめり込んで動けなくなっていた
        Vector2 to = feetTo - FeetOffset();
        Vector2 c0 = RewardKit.BodyCenter(transform);
        Vector2 c1 = feetTo + Vector2.up * BodyBounds().extents.y;

        var body = GetComponent<KinematicBody2D>();
        if (body != null) body.Warp(to);
        else transform.position = to;
        var pc = GetComponent<PlayerController>();
        if (pc != null) pc.SetVelocity(Vector2.zero);
        var health = GetComponent<Health>();
        if (health != null && _cfg.Invincible > 0f) health.SetInvincible(_cfg.Invincible);

        // 通り道の判定（回した箱）
        Vector2 d = c1 - c0;
        float len = d.magnitude;
        float angle = len > 0.01f ? Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg : 0f;
        Vector2 mid = (c0 + c1) * 0.5f;
        GameObject hitGo = _owner.CreateSpawnedObject("GitHubRevertPath", mid);
        hitGo.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        Hitbox hit = GitHubDesktopArt.AddHit(_owner, data, hitGo.transform, Vector2.zero, new Vector2(len + _cfg.Width, _cfg.Width));
        hit.Activate();
        Destroy(hitGo, _cfg.HitTime);

        // 見た目：紫の線がうすくなって消える・Revert の小さい行・輪
        FxAnim flash = FxAnim.Create("GitHubRevertFlash", mid, 0.3f);
        flash.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        FxKit.Box(flash.transform, Vector2.zero, new Vector2(len + 0.4f, 4f * GitHubDesktopArt.Px), GitHubDesktopArt.Purple, Order, "Line");
        FxKit.Box(flash.transform, Vector2.zero, new Vector2(len + 0.4f, 1f * GitHubDesktopArt.Px), Color.white, Order + 1, "Core");
        flash.FadeFrom(0f).Refresh();
        FxKit.Text("Revert", c1 + Vector2.up * 1.1f, 0.45f, Color.white, Order + 2, 0.7f, GitHubDesktopArt.Purple).Rise(0.4f);
        FxKit.Ring(c1, 1.8f, 0.2f, GitHubDesktopArt.Purple);

        Clear();
        LastRevertFrame = Time.frameCount;
    }

    void Update()
    {
        if (_commit == null) return;
        float left = _expireAt - Time.time;
        if (left <= 0f)
        {
            Clear();
            return;
        }
        if (_commitArt != null) _commitArt.enabled = left > 1f || Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
        if (_line != null)
        {
            Vector2 a = _commit.transform.position;
            Vector2 b = RewardKit.BodyCenter(transform);
            Vector2 d = b - a;
            float len = d.magnitude;
            _line.enabled = len > 0.6f;
            _line.transform.position = (a + b) * 0.5f;
            _line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            _line.size = new Vector2(Mathf.Max(0.05f, len), GitHubDesktopArt.Px);
        }
    }

    void Clear()
    {
        if (_commit != null) Destroy(_commit);
        _commit = null;
        _commitArt = null;
        _line = null;
    }

    void OnDestroy() => Clear();

    Vector2 Feet()
    {
        Bounds b = BodyBounds();
        return new Vector2(b.center.x, b.min.y);
    }

    // ───────── 体が収まるか（2026-11-20） ─────────

    // 体の箱を内側へ縮めて調べる幅（床・壁にふれているだけは数えない）。KinematicBody2D のすき間（0.015）より小さくする
    // （大きいと、ここでは重ならないのに、KinematicBody2D からは重なって見えて動けない所を「空き」と答えてしまう）
    const float FitInset = 0.01f;
    static readonly RaycastHit2D[] CastHits = new RaycastHit2D[8];
    static readonly Collider2D[] OverlapHits = new Collider2D[8];

    // 体の箱（動くときの当たり判定＝トリガーでないコライダー）
    Bounds BodyBounds()
    {
        foreach (var col in GetComponents<Collider2D>())
        {
            if (!col.isTrigger && col.enabled) return col.bounds;
        }
        return new Bounds(transform.position + Vector3.up * 0.5f, new Vector3(0.7f, 1f, 0f));
    }

    // 位置（原点）から見た足元のずれ（体の箱の形から。プレイヤーは足元が原点なので 0）
    Vector2 FeetOffset()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box == null) return Vector2.zero;
        Vector2 s = transform.lossyScale;
        s = new Vector2(Mathf.Abs(s.x), Mathf.Abs(s.y));
        return Vector2.Scale(box.offset, s) - new Vector2(0f, box.size.y * s.y * 0.5f);
    }

    Vector2 InsetSize(Bounds b) => Vector2.Max((Vector2)b.size - Vector2.one * (FitInset * 2f), Vector2.one * 0.05f);

    // 足元を feet に置いたとき、体が床・壁（Ground）に重ならないか。すり抜け床は重なっても出られるので数えない
    bool Fits(Vector2 feet)
    {
        Bounds b = BodyBounds();
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(LayerMask.GetMask("Ground"));
        int n = Physics2D.OverlapBox(feet + Vector2.up * b.extents.y, InsetSize(b), 0f, filter, OverlapHits);
        Rigidbody2D self = GetComponent<Rigidbody2D>();
        for (int i = 0; i < n; i++)
        {
            Collider2D c = OverlapHits[i];
            if (c != null && (self == null || c.attachedRigidbody != self)) return false;
        }
        return true;
    }

    // 空中から体の箱をまっすぐ下へ動かして、乗る床（Ground・上から乗るすり抜け床）の上面を探す（KinematicBody2D の着地と同じ考え方）
    bool TryFindLanding(Vector2 feet, out float groundY)
    {
        groundY = feet.y;
        Bounds b = BodyBounds();
        int oneWay = LayerMask.GetMask("OneWayPlatform");
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(LayerMask.GetMask("Ground") | oneWay);
        int n = Physics2D.BoxCast(feet + Vector2.up * b.extents.y, InsetSize(b), 0f, Vector2.down, filter, CastHits, 30f);
        Rigidbody2D self = GetComponent<Rigidbody2D>();
        float best = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            RaycastHit2D h = CastHits[i];
            if (h.collider == null || (self != null && h.collider.attachedRigidbody == self)) continue;
            if (h.distance <= 0f) continue; // 始めから重なっている物（下から抜けている途中のすり抜け床など）
            if ((oneWay & (1 << h.collider.gameObject.layer)) != 0 && h.normal.y < 0.5f) continue; // すり抜け床は上から乗るときだけ
            best = Mathf.Min(best, h.distance);
        }
        if (float.IsPositiveInfinity(best)) return false;
        groundY = feet.y + FitInset - best; // 縮めた箱の下の端が床に着いた高さ
        return true;
    }

    // 足元 feet に体が収まらなければ、近くの空いている所を探す（横へ少し → 上へ）。ボス部屋の内と外はまたがない
    bool TryFindRoom(ref Vector2 feet, Rect arena, bool inArena)
    {
        for (float up = 0f; up <= 3.01f; up += 0.25f)
        {
            for (float side = 0f; side <= 0.751f; side += 0.125f)
            {
                for (int s = 0; s < (side > 0f ? 2 : 1); s++)
                {
                    Vector2 p = feet + new Vector2(s == 0 ? side : -side, up);
                    if (arena.Contains(p + Vector2.up * 0.5f) != inArena || !Fits(p)) continue;
                    feet = p;
                    return true;
                }
            }
        }
        return false;
    }
}
