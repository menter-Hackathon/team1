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
        if (_owner.TryFindGround(feet.x, feet.y + 0.3f, out float g) && feet.y - g < 30f) gy = g;
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
        Vector2 from = transform.position;
        Vector2 pivot = from - Feet(); // 位置と足元のずれ（足元を丸の床に合わせる）
        Vector2 to = _commitFeet + pivot + Vector2.up * 0.02f;
        Vector2 c0 = RewardKit.BodyCenter(transform);
        Vector2 c1 = c0 + (to - from);

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
        foreach (var col in GetComponents<Collider2D>())
        {
            if (!col.isTrigger && col.enabled) return new Vector2(col.bounds.center.x, col.bounds.min.y);
        }
        return transform.position;
    }
}
