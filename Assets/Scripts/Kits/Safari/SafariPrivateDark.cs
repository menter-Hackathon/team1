using UnityEngine;

/// <summary>
/// 【仮】プライベートブラウズの進行（2026-11-19）。SafariPrivateAttack が出し、あとは自分で進む。場に残る技の効果（Lingers）。
/// 暗くする板（カメラについてくる）→ ボスの体をうすく（BossController.SetVisibility）・目だけ出す → 決まった時に赤い線 → 明るい弾 → 明るく戻る。
/// 消えるときは必ず、ボスの見え方を戻す（Restore）。
/// </summary>
public class SafariPrivateDark : CustomEffect
{
    const int OverlayOrder = 9; // 床・壁のタイル（1）・足場（2）より前、ボス・雑魚・プレイヤー（10〜）より後ろ
    const int EyesOrder = 30;
    const float FadeTime = 0.25f;
    const float BossAlpha = 0.06f;

    AttackRunner _owner;
    SafariPrivateAttack _cfg;
    BossController _boss;
    float _start;
    SpriteRenderer _overlay;
    SpriteRenderer _eyes;
    TextMesh _label;
    int _lined;
    int _fired;
    Vector2[] _dirs;
    Vector2[] _froms;

    public override bool Lingers => true;

    public static SafariPrivateDark Spawn(AttackRunner owner, SafariPrivateAttack cfg)
    {
        if (owner == null || cfg == null) return null;
        GameObject go = owner.CreateSpawnedObject("SafariPrivate", owner.OriginPoint);
        var d = go.AddComponent<SafariPrivateDark>();
        d._owner = owner;
        d._cfg = cfg;
        d._boss = owner.GetComponent<BossController>();
        d._start = Time.time;
        d._dirs = new Vector2[cfg.Shots];
        d._froms = new Vector2[cfg.Shots];
        d.Build();
        return d;
    }

    void Build()
    {
        Vector2 v = FxKit.ViewSize();
        _overlay = SafariArt.Box(transform, Vector2.zero, v + new Vector2(4f, 4f), new Color(0.05f, 0.05f, 0.08f, 0f), OverlayOrder, "Dark");
        _eyes = RuntimeSprites.Show(transform, SafariArt.Eyes(), Vector2.zero, EyesOrder, 1.6f, "Eyes");
        _label = SafariArt.Text(transform, "Private", Vector2.zero, 0.45f, new Color(1f, 1f, 1f, 0f), EyesOrder);
        Follow();
    }

    void Follow()
    {
        Camera cam = Camera.main;
        if (cam != null && _overlay != null)
        {
            Vector2 c = cam.transform.position;
            _overlay.transform.position = c;
            Vector2 v = FxKit.ViewSize();
            if (_label != null) _label.transform.position = new Vector3(c.x, c.y + v.y * 0.5f - 2.4f, -0.001f);
        }
        if (_eyes != null) _eyes.transform.position = EyesPoint();
    }

    Vector2 EyesPoint()
    {
        if (_boss != null)
        {
            Bounds b = _boss.BodyBounds;
            return new Vector2(b.center.x, b.center.y + b.extents.y * 0.25f);
        }
        return _owner != null ? _owner.OriginPoint : (Vector2)transform.position;
    }

    void Update()
    {
        float e = Time.time - _start;
        float dur = _cfg.Duration;
        if (e >= dur)
        {
            Destroy(gameObject);
            return;
        }
        // 暗さ：入る 0.25秒・出る 0.25秒
        float a = Mathf.Min(Mathf.Clamp01(e / FadeTime), Mathf.Clamp01((dur - e) / FadeTime));
        if (_overlay != null) _overlay.color = new Color(0.05f, 0.05f, 0.08f, 0.78f * a);
        if (_label != null) WorldText.SetColor(_label, new Color(1f, 1f, 1f, 0.7f * a));
        if (_boss != null) _boss.SetVisibility(Mathf.Lerp(1f, BossAlpha, a));
        if (_eyes != null)
        {
            // まばたき
            bool blink = Mathf.Repeat(e, 1.6f) > 1.48f;
            _eyes.color = new Color(1f, 1f, 1f, blink ? 0f : a);
        }
        Follow();

        // ねらう弾：予告の線 → 弾
        for (int i = _lined; i < _cfg.Shots; i++)
        {
            float at = _cfg.FirstDelay + i * _cfg.Interval;
            if (e < at) break;
            _froms[i] = EyesPoint();
            Vector2 t = _owner != null && _owner.TargetPoint is Vector2 tp ? tp : _froms[i] + Vector2.left;
            _dirs[i] = (t - _froms[i]).sqrMagnitude > 0.0001f ? (t - _froms[i]).normalized : Vector2.left;
            KitDress.Line(_owner, _froms[i], _froms[i] + _dirs[i] * 30f, _cfg.LineTime);
            _lined = i + 1;
        }
        for (int i = _fired; i < _lined; i++)
        {
            float at = _cfg.FirstDelay + i * _cfg.Interval + _cfg.LineTime;
            if (e < at) break;
            Projectile p = Projectile.Spawn(_owner, _cfg.Bullet, _froms[i], _dirs[i], i);
            SafariArt.Dress(p, SafariArt.BrightShot(), _dirs[i]);
            if (p != null) FxKit.Ring(_froms[i], 1.2f, 0.12f, SafariArt.Cyan);
            _owner.NotifyReleased(_cfg.Bullet);
            _fired = i + 1;
        }
    }

    protected override void Restore()
    {
        if (_boss != null) _boss.SetVisibility(1f);
    }
}
