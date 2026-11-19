using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】GitHub Desktop のステージの決まり「マージの衝突」（2026-11-19。docs/app-kits-2-design.md 2-2）。
/// ・背景に2本のブランチの線（main 青・virus 紫）。丸が左から右へ流れる。雑魚を倒すたび、上の線にコミットの丸が1つ増える
/// ・12秒おき（ボス戦は 15秒おき）に2本が合流する：合流の柱（幅 4マス。x＝-6・0・6 のうちプレイヤーに一番近い所）に
///   赤い枠と &lt;&lt;&lt;&lt;&lt;&lt;&lt;・&gt;&gt;&gt;&gt;&gt;&gt;&gt;（1.2秒）→ 柱が赤く光る（床から天井まで。0.4秒）
/// ・柱はプレイヤーにも雑魚にも当たる（雑魚は倒れる。倒した数に入る）。ボスの技の途中・場に残る技の間は出さない（ctx.CanSpawnHazard）
/// </summary>
public class GitHubDesktopRule : StageRule
{
    const float ZakoInterval = 12f;
    const float BossInterval = 15f;
    const float FirstDelay = 8f;

    float _timer = FirstDelay;
    AttackData _data;
    GitHubBranchLines _lines;
    bool _hooked;

    public override void Begin(StageRuleContext ctx)
    {
        _data = GitHubDesktopArt.RuntimeData("Attack_GitHubDesktop_MergeColumn");
        Transform cam = FxKit.CameraChild("GitHubBranchLines");
        if (cam != null)
        {
            _lines = cam.gameObject.AddComponent<GitHubBranchLines>();
            ctx.TrackDecor(cam.gameObject);
        }
        Health.AnyDied += OnAnyDied;
        _hooked = true;
    }

    public override void Tick(float dt)
    {
        _timer -= dt;
        if (_timer > 0f) return;
        if (!Ctx.CanSpawnHazard || Ctx.HazardCount > 0) return; // 出せるまで待つ
        if (SpawnColumn()) _timer = Ctx.InBossFight ? BossInterval : ZakoInterval;
    }

    public override void OnBossAppeared(BossController boss) => _timer = BossInterval * 0.6f;

    public override void OnBossPhase(int phase) => _timer = Mathf.Max(_timer, 5f);

    public override void End()
    {
        if (_hooked) Health.AnyDied -= OnAnyDied;
        _hooked = false;
        if (_lines != null) Object.Destroy(_lines.gameObject);
        _lines = null;
    }

    void OnAnyDied(Health h)
    {
        if (h == null || !RewardKit.IsZako(h)) return;
        if (_lines != null) _lines.AddCommit();
        GameAudio.Event("gm_github_commit");
    }

    bool SpawnColumn()
    {
        AttackRunner runner = Ctx.Hazard;
        if (runner == null || Ctx.Player == null) return false;
        Rect area = Ctx.PlayArea;
        float px = Ctx.Player.position.x;
        float best = area.center.x;
        float bestD = float.MaxValue;
        foreach (float off in new[] { -6f, 0f, 6f })
        {
            float x = area.center.x + off;
            float d = Mathf.Abs(x - px);
            if (d < bestD) { bestD = d; best = x; }
        }
        GitHubMergeColumn col = GitHubMergeColumn.Spawn(runner, _data, best, area, Ctx.Skin, Ctx.Theme, Ctx.Icon);
        if (col == null) return false;
        Ctx.TrackHazard(col.gameObject);
        if (_lines != null) _lines.Merge();
        GameAudio.Event("gm_github_merge_warn");
        return true;
    }
}

/// <summary>【仮】合流の柱（マージの衝突の危ない物。2026-11-19）。GitHubDesktopRule が出す</summary>
public class GitHubMergeColumn : CustomEffect
{
    const int Order = 16;
    const float Telegraph = 1.2f;
    const float Active = 0.4f;
    const float Width = 4f;

    AttackRunner _owner;
    Hitbox _hit;
    SpriteRenderer _fill;
    readonly List<SpriteRenderer> _edges = new List<SpriteRenderer>();
    readonly List<TextMesh> _texts = new List<TextMesh>();
    readonly HashSet<Health> _struck = new HashSet<Health>();
    readonly List<Collider2D> _overlap = new List<Collider2D>();
    Rect _rect;
    float _start;
    bool _fired;
    SkinId _skin;
    AppTheme _theme;
    Sprite _icon;

    public static GitHubMergeColumn Spawn(AttackRunner owner, AttackData data, float x, Rect area, SkinId skin, AppTheme theme, Sprite icon)
    {
        if (owner == null || data == null) return null;
        var rect = Rect.MinMaxRect(x - Width * 0.5f, area.yMin, x + Width * 0.5f, area.yMax);
        GameObject go = owner.CreateSpawnedObject("GitHubMergeColumn", rect.center);
        var c = go.AddComponent<GitHubMergeColumn>();
        c._owner = owner;
        c._rect = rect;
        c._start = Time.time;
        c._skin = skin;
        c._theme = theme;
        c._icon = icon;
        c.Build(data);
        return c;
    }

    void Build(AttackData data)
    {
        Vector2 size = _rect.size;
        Color red = GitHubDesktopArt.Danger;
        _fill = GitHubDesktopArt.Box(transform, Vector2.zero, size, GitHubDesktopArt.A(red, 0.14f), Order, "Fill");
        float t = 2f * GitHubDesktopArt.Px;
        _edges.Add(GitHubDesktopArt.Box(transform, new Vector2(-size.x * 0.5f + t * 0.5f, 0f), new Vector2(t, size.y), red, Order + 1, "EdgeL"));
        _edges.Add(GitHubDesktopArt.Box(transform, new Vector2(size.x * 0.5f - t * 0.5f, 0f), new Vector2(t, size.y), red, Order + 1, "EdgeR"));
        _texts.Add(GitHubDesktopArt.Text(transform, "<<<<<<<", new Vector2(0f, size.y * 0.5f - 2.8f), 0.45f, red, Order + 2));
        _texts.Add(GitHubDesktopArt.Text(transform, "=======", new Vector2(0f, 0f), 0.45f, GitHubDesktopArt.A(Color.white, 0.8f), Order + 2));
        _texts.Add(GitHubDesktopArt.Text(transform, ">>>>>>>", new Vector2(0f, -size.y * 0.5f + 1.6f), 0.45f, red, Order + 2));
        _hit = GitHubDesktopArt.AddHit(_owner, data, transform, Vector2.zero, size);
    }

    void Update()
    {
        float e = Time.time - _start;
        if (e < Telegraph)
        {
            bool on = GitHubDesktopArt.Blink(Telegraph - e, 0.15f, 0.05f);
            foreach (var sr in _edges) if (sr != null) sr.enabled = on;
            if (_fill != null) _fill.enabled = on;
            return;
        }
        if (e < Telegraph + Active)
        {
            if (!_fired)
            {
                _fired = true;
                GameAudio.Event("gm_github_merge_fire");
                if (_hit != null) _hit.Activate();
                if (_fill != null)
                {
                    _fill.enabled = true;
                    _fill.color = GitHubDesktopArt.A(GitHubDesktopArt.Danger, 0.6f);
                }
                foreach (var sr in _edges) if (sr != null) { sr.enabled = true; sr.color = Color.white; }
                foreach (var tm in _texts) if (tm != null) tm.gameObject.SetActive(false);
            }
            StrikeZako();
            return;
        }
        Destroy(gameObject);
    }

    /// <summary>柱の中の雑魚を倒す（1体に1回）</summary>
    void StrikeZako()
    {
        var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
        filter.SetLayerMask(LayerMask.GetMask("Enemy"));
        _overlap.Clear();
        Physics2D.OverlapBox(_rect.center, _rect.size, 0f, filter, _overlap);
        foreach (var col in _overlap)
        {
            if (col == null) continue;
            Health h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead || !_struck.Add(h) || !RewardKit.IsZako(h)) continue;
            RewardKit.Strike(h, 99, _rect.center, gameObject, _skin, HitStrength.Heavy, _theme, _icon);
        }
    }
}

/// <summary>【仮】背景の2本のブランチの線（main 青・virus 紫。2026-11-19）。カメラの子。丸が左から右へ流れる</summary>
public class GitHubBranchLines : MonoBehaviour
{
    const int Order = 0; // 部屋の中（-50）より前、床・壁のタイル（1）より後ろ
    const float Speed = 0.8f;
    const int MaxDots = 24;

    float _width;
    float _yMain;
    float _yVirus;
    readonly List<SpriteRenderer> _dots = new List<SpriteRenderer>();
    readonly List<bool> _onMain = new List<bool>();
    SpriteRenderer _bridge;
    float _bridgeUntil;
    float _nextAuto;

    void Start()
    {
        Vector2 view = FxKit.ViewSize();
        _width = view.x + 1f;
        _yMain = view.y * 0.5f - 3.1f;
        _yVirus = _yMain - 0.7f;
        Color main = GitHubDesktopArt.A(GitHubDesktopArt.Blue, 0.55f);
        Color virus = GitHubDesktopArt.A(GitHubDesktopArt.Purple, 0.55f);
        GitHubDesktopArt.Box(transform, new Vector2(0f, _yMain), new Vector2(_width, 2f * GitHubDesktopArt.Px), main, Order, "Main");
        GitHubDesktopArt.Box(transform, new Vector2(0f, _yVirus), new Vector2(_width, 2f * GitHubDesktopArt.Px), virus, Order, "Virus");
        GitHubDesktopArt.Text(transform, "main", new Vector2(-view.x * 0.5f + 0.3f, _yMain + 0.35f), 0.4f, GitHubDesktopArt.A(Color.white, 0.6f), Order + 1, TextAnchor.MiddleLeft);
        GitHubDesktopArt.Text(transform, "virus", new Vector2(-view.x * 0.5f + 0.3f, _yVirus - 0.35f), 0.4f, GitHubDesktopArt.A(Color.white, 0.6f), Order + 1, TextAnchor.MiddleLeft);
        _bridge = GitHubDesktopArt.Box(transform, new Vector2(0f, (_yMain + _yVirus) * 0.5f), new Vector2(2f * GitHubDesktopArt.Px, _yMain - _yVirus), GitHubDesktopArt.A(GitHubDesktopArt.Purple, 0.8f), Order, "Bridge");
        _bridge.enabled = false;
        // 最初から何個か流しておく（virus の線）
        for (int i = 0; i < 5; i++) AddDot(false, -_width * 0.5f + i * _width / 5f);
        _nextAuto = Time.time + 2.5f;
    }

    /// <summary>上の線（main）にコミットの丸を足す（雑魚を倒したとき）</summary>
    public void AddCommit() => AddDot(true, -_width * 0.5f);

    /// <summary>合流の印（紫の線で2本をつなぐ。1.6秒）</summary>
    public void Merge() => _bridgeUntil = Time.time + 1.6f;

    void AddDot(bool main, float x)
    {
        if (_dots.Count >= MaxDots)
        {
            if (_dots[0] != null) Destroy(_dots[0].gameObject);
            _dots.RemoveAt(0);
            _onMain.RemoveAt(0);
        }
        SpriteRenderer sr = RuntimeSprites.Show(transform, GitHubDesktopArt.SmallCommit(!main), new Vector2(x, main ? _yMain : _yVirus), Order + 1, 1f, "Dot");
        sr.color = new Color(1f, 1f, 1f, 0.8f);
        _dots.Add(sr);
        _onMain.Add(main);
    }

    void Update()
    {
        float dx = Speed * Time.deltaTime;
        for (int i = _dots.Count - 1; i >= 0; i--)
        {
            SpriteRenderer sr = _dots[i];
            if (sr == null)
            {
                _dots.RemoveAt(i);
                _onMain.RemoveAt(i);
                continue;
            }
            Vector3 p = sr.transform.localPosition;
            p.x += dx;
            if (p.x > _width * 0.5f) p.x -= _width; // 右の端まで来たら、左へ戻す
            sr.transform.localPosition = p;
        }
        if (Time.time >= _nextAuto)
        {
            _nextAuto = Time.time + 3f;
            AddDot(false, -_width * 0.5f); // virus の線は勝手に進む
        }
        if (_bridge != null)
        {
            bool on = Time.time < _bridgeUntil;
            _bridge.enabled = on;
            if (on)
            {
                Vector3 p = _bridge.transform.localPosition;
                p.x = Mathf.Repeat(Time.time * 4f, _width) - _width * 0.5f;
                _bridge.transform.localPosition = p;
            }
        }
    }
}
