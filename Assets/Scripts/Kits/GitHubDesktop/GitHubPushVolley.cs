using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Push origin の進行（2026-11-19）。GitHubPushAttack が出し、あとは自分で進む。
/// ↑N のバッジと予告の線（KitDress.Line）→ 時間どおりにコミットの丸（弾）を出す。枝分かれのときは、幹の丸が分かれ目で 3つに分かれる。
/// ボスの形態変化・正常化で消える（CustomEffect。出した弾は弾として消える）。
/// </summary>
public class GitHubPushVolley : CustomEffect
{
    const int Order = 24;

    struct Shot
    {
        public float At;
        public bool Trunk;
        public int Index;
    }

    AttackRunner _owner;
    GitHubPushAttack _cfg;
    Vector2 _origin;
    Vector2 _dir;
    Vector2 _branchPoint;
    float _start;
    float _branchDelay;
    readonly List<Shot> _shots = new List<Shot>();
    readonly List<(Projectile p, float at)> _trunks = new List<(Projectile, float)>();

    /// <summary>技の長さ（秒）</summary>
    public float Length { get; private set; }

    public static GitHubPushVolley Spawn(AttackRunner owner, GitHubPushAttack cfg)
    {
        if (owner == null || cfg == null || cfg.Bullet == null) return null;
        Vector2 origin = owner.OriginPoint;
        GameObject go = owner.CreateSpawnedObject("GitHubPush", origin);
        var v = go.AddComponent<GitHubPushVolley>();
        v.Setup(owner, cfg, origin);
        return v;
    }

    void Setup(AttackRunner owner, GitHubPushAttack cfg, Vector2 origin)
    {
        _owner = owner;
        _cfg = cfg;
        _origin = origin;
        _start = Time.time;
        Vector2 dir = new Vector2(owner.Facing, 0f);
        if (owner.TargetPoint is Vector2 t && (t - origin).sqrMagnitude > 0.0001f) dir = (t - origin).normalized;
        _dir = dir;
        float speed = Mathf.Max(0.5f, cfg.Bullet.Speed);
        float len = Mathf.Min(40f, speed * cfg.Bullet.Lifetime);

        // 予告の線（実際の弾道と同じ）
        if (cfg.Branch)
        {
            _branchPoint = origin + dir * cfg.BranchAt;
            _branchDelay = cfg.BranchAt / speed;
            KitDress.Line(owner, origin, _branchPoint, cfg.Telegraph);
            foreach (float a in Angles()) KitDress.Line(owner, _branchPoint, _branchPoint + Rotate(dir, a) * len, cfg.Telegraph);
        }
        else KitDress.Line(owner, origin, origin + dir * len, cfg.Telegraph);

        for (int k = 0; k < cfg.Count; k++) _shots.Add(new Shot { At = cfg.Telegraph + k * cfg.Interval, Trunk = cfg.Branch, Index = k });
        Length = cfg.Telegraph + (cfg.Count - 1) * cfg.Interval + (cfg.Branch ? _branchDelay : 0f) + 0.1f;

        // ボスの頭の上に ↑N（枝分かれは 3本ぶん）
        int n = cfg.Branch ? cfg.Count * 3 : cfg.Count;
        Vector2 head = GitHubDesktopArt.HeadOf(owner);
        Transform badge = new GameObject("PushBadge").transform;
        badge.gameObject.layer = gameObject.layer;
        badge.SetParent(transform, false);
        badge.position = head;
        float w = 1.5f;
        GitHubDesktopArt.Box(badge, Vector2.zero, new Vector2(w, 0.7f), GitHubDesktopArt.Navy, Order, "Bg");
        GitHubDesktopArt.Box(badge, new Vector2(0f, -0.35f + GitHubDesktopArt.Px), new Vector2(w, 2f * GitHubDesktopArt.Px), GitHubDesktopArt.Blue, Order + 1, "Line");
        RuntimeSprites.Show(badge, GitHubDesktopArt.UpArrow(), new Vector2(-0.35f, 0f), Order + 1, 1f, "Arrow");
        GitHubDesktopArt.Text(badge, n.ToString(), new Vector2(0.25f, 0f), 0.45f, Color.white, Order + 1);
    }

    IEnumerable<float> Angles()
    {
        yield return _cfg.BranchAngle;
        yield return 0f;
        yield return -_cfg.BranchAngle;
    }

    static Vector2 Rotate(Vector2 v, float deg) => Quaternion.Euler(0f, 0f, deg) * v;

    void Update()
    {
        float e = Time.time - _start;
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            Shot s = _shots[i];
            if (e < s.At) continue;
            _shots.RemoveAt(i);
            Projectile p = Fire(_origin, _dir, s.Index);
            if (s.Trunk && p != null) _trunks.Add((p, Time.time + _branchDelay));
            else if (s.Trunk) _trunks.Add((null, Time.time + _branchDelay));
        }
        for (int i = _trunks.Count - 1; i >= 0; i--)
        {
            var (p, at) = _trunks[i];
            if (Time.time < at) continue;
            _trunks.RemoveAt(i);
            if (p != null) Destroy(p.gameObject);
            int k = 0;
            foreach (float a in Angles()) Fire(_branchPoint, Rotate(_dir, a), k++);
        }
        if (_shots.Count == 0 && _trunks.Count == 0 && e >= Length) Destroy(gameObject);
    }

    Projectile Fire(Vector2 from, Vector2 dir, int index)
    {
        if (_owner == null) return null;
        Projectile p = Projectile.Spawn(_owner, _cfg.Bullet, from, dir, index);
        if (p != null) KitDress.Swap(p.gameObject, GitHubDesktopArt.Commit());
        _owner.NotifyReleased(_cfg.Bullet);
        return p;
    }
}
