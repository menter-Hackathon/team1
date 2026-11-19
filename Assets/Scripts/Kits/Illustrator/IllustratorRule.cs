using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Illustrator のステージの決まり：拡大・縮小ツール（2026-11-19。docs/app-kits-2-design.md 3-4）。
/// ・雑魚：今ある強い雑魚・小さい雑魚（ZakoVariants の Elite・Small。大きい・おそい・3回で倒れる／小さい・速い）に、
///   バウンディングボックス（8つの白い四角）と大きさの % を付ける。見た目だけ。数は変えない
/// ・ボス戦：12秒おきに、ボスにバウンディングボックスが出て（1.0秒）、大きさが変わる。
///   130%（当てやすい・待ち時間 ×0.85）→ 元 → 75%（当てにくい・待ち時間 ×1.2）→ 元 …。部屋の壁・床にめりこむ大きさにはしない
/// ・片づけ：形態変化（50％）・100％・やり直しで、元の大きさ・倍率に戻す
/// </summary>
public class IllustratorRule : StageRule
{
    const float Interval = 12f, FirstDelay = 6f, BoxTime = 1f, ScaleTime = 0.3f;
    static readonly float[] Cycle = { 1.3f, 1f, 0.75f, 1f };

    readonly HashSet<int> _decorated = new HashSet<int>();
    float _scanAt;

    BossController _boss;
    Vector3 _baseScale = Vector3.one;
    float _timer;
    int _cycle;
    float _target = 1f, _from = 1f, _cur = 1f, _scaleStart = -1f;
    float _boxUntil;
    Transform _box;
    bool _pending;
    float _boxScale0 = 1f;
    string ModKey => Ctx != null ? Ctx.Key("scale") : "illustrator.scale";

    public override void Begin(StageRuleContext ctx) { }

    public override void Tick(float dt)
    {
        if (Time.time >= _scanAt) { _scanAt = Time.time + 0.4f; DecorateZako(); }
        if (_boss == null || !KitBoss.IsFighting(_boss)) return;

        // 次の大きさ（枠を 1.0秒見せてから変える）
        _timer -= dt;
        if (!_pending && _timer <= 0f)
        {
            _pending = true;
            _boxUntil = Time.time + BoxTime;
            ShowBox(Cycle[_cycle % Cycle.Length]);
            GameAudio.Event("gm_illustrator_box");
        }
        if (_pending && Time.time >= _boxUntil)
        {
            _pending = false;
            _from = _cur;
            _target = Cycle[_cycle % Cycle.Length];
            _cycle++;
            _scaleStart = Time.time;
            _timer = Interval;
            ApplyWait(_target);
        }
        if (_box != null) FollowBox();
        if (_box != null && !_pending && Time.time > _scaleStart + ScaleTime + 0.6f) { Object.Destroy(_box.gameObject); _box = null; }

        // 大きさ（壁・床にめりこまない範囲）
        float want = _scaleStart >= 0f ? Mathf.Lerp(_from, _target, Mathf.Clamp01((Time.time - _scaleStart) / ScaleTime)) : _cur;
        _cur = Mathf.Min(want, MaxFit());
        _boss.transform.localScale = _baseScale * _cur;
    }

    public override void OnBossAppeared(BossController boss)
    {
        _boss = boss;
        if (boss == null) return;
        _baseScale = boss.transform.localScale;
        _timer = FirstDelay;
        boss.TransitionStarted += ResetBoss;
    }

    public override void OnBossPhase(int phase)
    {
        ResetBoss();
        _timer = FirstDelay;
    }

    public override void End()
    {
        ResetBoss();
        if (_boss != null) _boss.TransitionStarted -= ResetBoss;
        _boss = null;
    }

    void ResetBoss()
    {
        if (_boss != null) _boss.transform.localScale = _baseScale;
        _cur = _from = _target = 1f;
        _scaleStart = -1f;
        _pending = false;
        _cycle = 0;
        if (_box != null) Object.Destroy(_box.gameObject);
        _box = null;
        StatMods.Boss.Wait.Clear(ModKey);
    }

    void ApplyWait(float scale)
    {
        if (scale > 1.01f) { StatMods.Boss.Wait.Set(ModKey, 0.85f, Ctx != null ? Ctx.Icon : null); GameAudio.Event("gm_illustrator_scale_up"); }
        else if (scale < 0.99f) { StatMods.Boss.Wait.Set(ModKey, 1.2f, Ctx != null ? Ctx.Icon : null); GameAudio.Event("gm_illustrator_scale_down"); }
        else { StatMods.Boss.Wait.Clear(ModKey); GameAudio.Event("gm_illustrator_scale_reset"); }
    }

    /// <summary>今の位置で、壁・床にめりこまない一番大きい倍率</summary>
    float MaxFit()
    {
        if (_boss == null) return 1f;
        Rect a = BattleArena.GetBounds();
        Bounds b = VisualBounds(_boss);
        float s = Mathf.Max(0.05f, _cur);
        Vector2 root = _boss.transform.position;
        Vector2 off = ((Vector2)b.center - root) / s;
        Vector2 ext = (Vector2)b.extents / s;
        float best = 2f;
        for (float t = 2f; t >= 1f; t -= 0.05f)
        {
            Vector2 c = root + off * t;
            Vector2 e = ext * t;
            best = t;
            if (c.x - e.x >= a.xMin && c.x + e.x <= a.xMax && c.y - e.y >= a.yMin) break;
        }
        return Mathf.Max(1f, best);
    }

    void ShowBox(float next)
    {
        if (_box != null) Object.Destroy(_box.gameObject);
        _box = new GameObject("AiBossBox").transform;
        Ctx.TrackDecor(_box.gameObject);
        Bounds b = VisualBounds(_boss);
        _box.position = b.center;
        _boxScale0 = _cur;
        IllustratorArt.BoundingBox(_box, (Vector2)b.size + new Vector2(0.3f, 0.3f), 44);
        TextMesh t = WorldText.Create(_box, Mathf.RoundToInt(next * 100f) + "%", 0.4f, Color.white, 46, true, TextAnchor.MiddleCenter, "Pct");
        t.transform.localPosition = new Vector3(0f, -b.size.y * 0.5f - 0.5f, 0f);
        var bg = RuntimeSprites.Show(_box, FxArt.Rect(Mathf.RoundToInt(2.2f * 18f), 10), new Vector2(0f, -b.size.y * 0.5f - 0.5f), 45, 1f, "PctBg");
        bg.color = IllustratorArt.BoxBlue;
    }

    void FollowBox()
    {
        if (_box == null || _boss == null) return;
        _box.position = VisualBounds(_boss).center;
        _box.localScale = Vector3.one * (_cur / Mathf.Max(0.05f, _boxScale0)); // ボスと一緒に大きさが変わる
    }

    /// <summary>ボスの見た目の範囲（絵を全部合わせた物。被弾範囲より少し大きい）。絵が無ければ被弾範囲</summary>
    internal static Bounds VisualBounds(BossController boss)
    {
        bool any = false;
        Bounds b = default;
        foreach (var sr in boss.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr == null || !sr.enabled || sr.sprite == null) continue;
            if (!any) { b = sr.bounds; any = true; }
            else b.Encapsulate(sr.bounds);
        }
        return any ? b : boss.BodyBounds;
    }

    // ───────── 雑魚 ─────────

    void DecorateZako()
    {
        foreach (var w in Object.FindObjectsOfType<ZakoWalker>())
        {
            if (w == null || !_decorated.Add(w.GetInstanceID())) continue;
            string n = w.gameObject.name;
            bool elite = n.Contains("Elite") || w.GetComponent<ZakoElite>() != null;
            string pct = elite ? Mathf.RoundToInt(ZakoVariants.EliteScale * 100f) + "%"
                : n.Contains("Small") ? Mathf.RoundToInt(ZakoVariants.SmallScale * 100f) + "%" : null;
            if (pct == null) continue;
            Bounds b = default;
            bool any = false;
            foreach (var sr in w.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!any) { b = sr.bounds; any = true; } else b.Encapsulate(sr.bounds);
            }
            if (!any) continue;
            var root = new GameObject("AiZakoBox").transform;
            root.position = b.center;
            IllustratorArt.BoundingBox(root, (Vector2)b.size + new Vector2(0.15f, 0.15f), 29);
            TextMesh t = WorldText.Create(root, pct, 0.3f, IllustratorArt.BoxBlue, 30, true, TextAnchor.MiddleCenter, "Pct");
            t.transform.localPosition = new Vector3(0f, -b.size.y * 0.5f - 0.3f, 0f);
            root.SetParent(w.transform, true);
        }
    }
}
