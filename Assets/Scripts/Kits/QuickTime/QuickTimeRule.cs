using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 【仮】QuickTime Player のステージの決まり「再生の速さ」（2026-11-19。docs/app-kits-2-design.md 4-3）。担当A。
/// ・雑魚の 4割に、頭の上の札：`2x`（速い）か `0.5x`（おそい）。その雑魚の速さだけ変える
///   【仮】雑魚1体ずつの速さの入口が無いので、雑魚の部品の速さの欄（ZakoWalker・ZakoFlyer の _speed、ZakoCharger の _chargeSpeed）を
///   リフレクションで1回だけ掛ける（雑魚のコードは変えない。担当0 に入口を頼みたい）
/// ・ボス戦：10秒おきに、ボスの頭の上の札が変わる。`1x` → `2x`（移動 ×1.6・待ち時間 ×0.75）→ `1x` → `0.5x`（移動 ×0.6・待ち時間 ×1.4）。
///   変わる 1秒前に札が点滅。倍率は StatMods（印を出す）。予告の長さ・弾の速さは変えない
/// ・下に再生バー（飾り。QuickTimeBar）。ステージの進み（雑魚の残り）でつまみが右へ動く
/// ・ボスが強くなるギミック（【要相談②】のかっこ内：入れる。上限は 1-2）
/// </summary>
public class QuickTimeRule : StageRule
{
    const float TagChance = 0.4f;
    const float BossPeriod = 10f;
    static readonly string[] BossCycle = { "1x", "2x", "1x", "0.5x" };

    readonly HashSet<int> _seen = new HashSet<int>();
    float _nextScan;
    QuickTimeTag _bossTag;
    int _bossStep;
    float _bossNext;
    bool _blinked;

    string Key => Ctx.Key("speed");

    public override void Begin(StageRuleContext ctx)
    {
        var bar = new GameObject("QuickTimeBar");
        bar.AddComponent<QuickTimeBar>();
        ctx.TrackDecor(bar);
    }

    public override void OnBossAppeared(BossController boss)
    {
        _bossStep = 0;
        _bossNext = Ctx.Elapsed + BossPeriod;
        _blinked = false;
        if (boss != null)
        {
            _bossTag = QuickTimeTag.Attach(boss.transform, BossCycle[0], BossHeight(boss));
            Ctx.TrackDecor(_bossTag.gameObject);
        }
        ApplyBoss();
    }

    public override void Tick(float dt)
    {
        if (!Ctx.InBossFight && Ctx.Elapsed >= _nextScan)
        {
            _nextScan = Ctx.Elapsed + 0.4f;
            ScanZako();
        }
        if (!Ctx.InBossFight || Ctx.Boss == null) return;
        if (!_blinked && Ctx.Elapsed >= _bossNext - 1f)
        {
            _blinked = true;
            if (_bossTag != null) _bossTag.Blink(1f);
            // 次が 2x（危ない方）なら少し高く
            GameAudio.Event("gm_quicktime_blink", BossCycle[(_bossStep + 1) % BossCycle.Length] == "2x" ? 1.15f : 1f);
        }
        if (Ctx.Elapsed >= _bossNext)
        {
            _bossStep = (_bossStep + 1) % BossCycle.Length;
            _bossNext = Ctx.Elapsed + BossPeriod;
            _blinked = false;
            if (_bossTag != null) _bossTag.SetText(BossCycle[_bossStep]);
            ApplyBoss();
            // 速さが変わった音（ボスの登場では鳴らさないので、ApplyBoss の中には入れない）
            string s = BossCycle[_bossStep];
            GameAudio.Event(s == "2x" ? "gm_quicktime_fast" : s == "0.5x" ? "gm_quicktime_slow" : "gm_quicktime_normal");
        }
    }

    void ApplyBoss()
    {
        string s = BossCycle[_bossStep];
        Sprite icon = QuickTimeArt.SpeedIcon;
        if (s == "2x")
        {
            StatMods.Boss.Speed.Set(Key, 1.6f, icon);
            StatMods.Boss.Wait.Set(Key, 0.75f, icon);
        }
        else if (s == "0.5x")
        {
            StatMods.Boss.Speed.Set(Key, 0.6f, icon);
            StatMods.Boss.Wait.Set(Key, 1.4f, icon);
        }
        else StatMods.Clear(Key);
    }

    static float BossHeight(BossController boss)
    {
        Bounds b = boss.BodyBounds;
        return b.max.y - boss.transform.position.y + 0.6f;
    }

    // 新しく出た雑魚の 4割に札を付けて、その雑魚の速さを変える
    void ScanZako()
    {
        foreach (Health h in Object.FindObjectsOfType<Health>())
        {
            if (h == null || h.IsDead || !RewardKit.IsZako(h)) continue;
            if (!_seen.Add(h.GetInstanceID())) continue;
            if (Random.value >= TagChance) continue;
            bool fast = Random.value < 0.5f;
            if (!ScaleSpeed(h.gameObject, fast ? 2f : 0.5f)) continue;
            float height = 1.4f;
            var col = h.GetComponent<BoxCollider2D>();
            if (col != null) height = col.offset.y + col.size.y * 0.5f + 0.5f;
            QuickTimeTag tag = QuickTimeTag.Attach(h.transform, fast ? "2x" : "0.5x", height);
            Ctx.TrackDecor(tag.gameObject);
            GameAudio.Event("gm_quicktime_tag", fast ? 1.25f : 0.8f);
        }
    }

    static readonly (string type, string field)[] SpeedFields =
    {
        ("ZakoWalker", "_speed"), ("ZakoFlyer", "_speed"), ("ZakoCharger", "_chargeSpeed"),
    };

    // 雑魚の部品の速さの欄に掛ける（見つからなければ false。札も付けない）
    static bool ScaleSpeed(GameObject zako, float k)
    {
        bool any = false;
        foreach (var c in zako.GetComponents<MonoBehaviour>())
        {
            if (c == null) continue;
            foreach (var (type, field) in SpeedFields)
            {
                if (c.GetType().Name != type) continue;
                FieldInfo f = c.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f == null || f.FieldType != typeof(float)) continue;
                f.SetValue(c, (float)f.GetValue(c) * k);
                any = true;
            }
        }
        return any;
    }

    public override void End()
    {
        StatMods.Clear(Key);
        if (_bossTag != null) Object.Destroy(_bossTag.gameObject);
        _bossTag = null;
    }
}
