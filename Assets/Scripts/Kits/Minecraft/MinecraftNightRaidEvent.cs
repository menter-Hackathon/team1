using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Minecraft のステージの出来事「夜の襲撃」（場面③。2026-11-19。docs/stage-gimmick-variety-c.md 3章）。
/// ・はじめ：夜に固定（MinecraftRule.HoldNight。ふつうの夜と同じ暗さ・雑魚の移動 ×1.3）。終わると時計の昼と夜へ戻る
/// ・Erupt（主）：土から出る緑の四角い物（緑の柱に暗い四角い目が2つ。本物の顔は描かない）。幅 1・高さ 2.4。
///   5回、1回に 2／2／3本。1本はプレイヤーの今の x、ほかは床の x −8〜+8 を 2マスおきの枠にして、まだ使っていない枠から。
///   予告 1.0秒（土がぽろぽろはねる＋赤い割れ目）→ 0.2秒で上がる → 0.9秒止まる（白く点滅）→ 引っこむ。床だけ（足場からは出ない）
/// ・EdgeSweep：矢。5.0秒は左から、10.0秒は右から。高さ 1.0（ジャンプで越える）と 3.8（低い足場の上の人の高さ）を 0.5秒ずらして。
///   速さ 11・まっすぐ。強さ 1 は床の高さの 1本だけ
/// ・設計の「2.5秒おき」の Erupt のうち、矢の回にかかる 2回（6.0・11.0秒）は、矢のあと（6.6・11.6秒）へずらした（矢と Erupt の予告を重ねない）
/// </summary>
public class MinecraftNightRaidEvent : KitTimedEvent
{
    const float MobWidth = 1f, MobHeight = 2.4f, MobWarn = 1f, MobRise = 0.2f, MobHold = 0.9f;
    const float ArrowSpeed = 11f, ArrowWarn = 0.8f, ArrowSize = 1f, ArrowLow = 1f, ArrowHigh = 3.8f;
    static readonly float[] EruptTimes = { 1f, 3.5f, 6.6f, 8.5f, 11.6f };

    readonly List<KitMotionErupt> _mobs = new List<KitMotionErupt>();
    readonly List<KitMotionGroup> _arrows = new List<KitMotionGroup>();
    readonly HashSet<KitMotionItem> _dressed = new HashSet<KitMotionItem>();
    readonly HashSet<int> _usedSlots = new HashSet<int>();
    MinecraftRule _rule;

    public MinecraftNightRaidEvent() : base("Night Raid") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(MinecraftKit.Label, stage => new MinecraftNightRaidEvent());

    protected override void Build()
    {
        _rule = StageRules.Current as MinecraftRule;
        if (_rule != null) _rule.HoldNight(true);
        foreach (float t in EruptTimes) At(t, EruptRound);
        At(5f, () => Arrow(KitSide.Left, ArrowLow));
        if (Level >= 2) At(5.5f, () => Arrow(KitSide.Left, ArrowHigh));
        At(10f, () => Arrow(KitSide.Right, ArrowLow));
        if (Level >= 2) At(10.5f, () => Arrow(KitSide.Right, ArrowHigh));
        EndAt(14f);
    }

    protected override void OnEnd(bool byBoss)
    {
        if (_rule != null) _rule.HoldNight(false);
        _rule = null;
        base.OnEnd(byBoss);
    }

    void EruptRound()
    {
        Rect a = KitMotion.Area;
        float cx = a.center.x;
        int count = Level >= 3 ? 3 : 2;
        float px = Mathf.Clamp(KitMotion.PlayerPos.x, a.xMin + 1f, a.xMax - 1f);
        var xs = new List<float> { px };
        // 2マスおきの枠（x −8〜+8 の 9つ）。プレイヤーの柱から 1.5 マス以内と、使った枠は飛ばす
        var free = new List<int>();
        for (int s = 0; s < 9; s++)
        {
            float x = cx - 8f + s * 2f;
            if (!_usedSlots.Contains(s) && Mathf.Abs(x - px) >= 1.5f && x > a.xMin + 0.8f && x < a.xMax - 0.8f) free.Add(s);
        }
        if (free.Count < count - 1)
        {
            _usedSlots.Clear(); // 全部使ったら、また最初から
            free.Clear();
            for (int s = 0; s < 9; s++)
            {
                float x = cx - 8f + s * 2f;
                if (Mathf.Abs(x - px) >= 1.5f && x > a.xMin + 0.8f && x < a.xMax - 0.8f) free.Add(s);
            }
        }
        for (int i = 1; i < count && free.Count > 0; i++)
        {
            int k = free[Random.Range(0, free.Count)];
            free.Remove(k);
            _usedSlots.Add(k);
            xs.Add(cx - 8f + k * 2f);
        }
        foreach (float x in xs)
        {
            KitMotionErupt e = KitMotion.Erupt(Owner, x, MobWidth, MobHeight, MobWarn, MobRise, MobHold, MobSprite, MinecraftArt.Green);
            if (e == null) continue;
            _mobs.Add(e);
            // 土がぽろぽろはねる（予告の間）
            for (int d = 0; d < 4; d++)
            {
                var dirt = new GameObject("Dirt");
                dirt.transform.SetParent(e.transform, false);
                dirt.transform.position = new Vector2(x + (d - 1.5f) * 0.28f, e.SurfaceY + 0.06f);
                AttackRunner.AddVisual(dirt.transform, PlaceholderSprites.Square, MinecraftArt.Dirt, Vector2.one * 0.14f, KitMotionItem.OrderCrack + 1, "Bit");
                dirt.AddComponent<MinecraftDirtHop>().Setup(MobWarn, d * 0.9f);
            }
        }
    }

    void Arrow(KitSide side, float height)
    {
        KitMotionGroup g = KitMotion.EdgeSweep(Owner, side, height, ArrowSpeed, 1, 0f, 0f, ArrowWarn,
            side == KitSide.Left ? ArrowRight : ArrowLeft, null, 1, ArrowSize);
        if (g != null) _arrows.Add(g);
    }

    protected override void OnFrame(float clock)
    {
        // 止まっている間、白く点滅
        _mobs.RemoveAll(e => e == null || e.IsDone);
        foreach (var e in _mobs)
        {
            if (e.Visual == null) continue;
            bool up = e.IsUp && e.Age > e.Warn + MobRise;
            e.Visual.color = up && Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0 ? Color.white : (Color)MinecraftArt.Green;
        }
        // 矢：当たりを細長く（絵の矢の形に合わせる）
        _arrows.RemoveAll(g => g == null || g.IsDone);
        foreach (var g in _arrows)
            foreach (var item in g.Items)
            {
                if (!(item is KitMotionMover m) || !_dressed.Add(m)) continue;
                if (m.Body != null && m.Body.TryGetComponent(out BoxCollider2D box)) box.size = new Vector2(ArrowSize * 0.9f, ArrowSize * 0.3f);
            }
        _dressed.RemoveWhere(x => x == null);
    }

    static readonly Dictionary<char, Color32> MobPal = new Dictionary<char, Color32>
    {
        { 'W', new Color32(255, 255, 255, 255) }, // 緑で塗る所
        { 'L', new Color32(205, 205, 205, 255) }, // 少し暗い緑（まだら）
        { 'K', new Color32(30, 42, 30, 255) },    // 目
    };

    /// <summary>緑の四角い物（10×24。白い所を緑で塗る。上の方に暗い四角い目が2つ）</summary>
    static Sprite MobSprite => RuntimeSprites.FromAscii("MinecraftEvent_Mob", new[]
    {
        "WWLWWWWLWW",
        "WWWWWLWWWW",
        "WKKWWWWKKW",
        "WKKWWWWKKW",
        "WWWWLWWWWW",
        "LWWWWWWWLW",
        "WWWLWWWWWW",
        "WWWWWWLWWW",
        "WLWWWWWWWW",
        "WWWWWWWWLW",
        "WWWWLWWWWW",
        "WWWWWWWWWW",
        "LWWWWWLWWW",
        "WWWWWWWWWW",
        "WWLWWWWWWL",
        "WWWWWWWWWW",
        "WWWWLWWWWW",
        "WLWWWWWWWW",
        "WWWWWWWLWW",
        "WWWWWWWWWW",
        "WWWLWWWWWW",
        "WWWWWWWWLW",
        "LWWWWWWWWW",
        "WWWWWLWWWW",
    }, MobPal);

    static readonly Dictionary<char, Color32> ArrowPal = new Dictionary<char, Color32>
    {
        { 'W', new Color32(240, 240, 240, 255) }, // 白い棒
        { 'G', new Color32(150, 150, 150, 255) }, // 灰色の矢じり
        { 'F', new Color32(200, 200, 200, 255) }, // 羽
    };

    /// <summary>矢（右向き。14×14 の中に、白い棒と灰色の矢じり）</summary>
    static Sprite ArrowRight => RuntimeSprites.FromAscii("MinecraftEvent_ArrowR", ArrowRows(false), ArrowPal);
    /// <summary>矢（左向き）</summary>
    static Sprite ArrowLeft => RuntimeSprites.FromAscii("MinecraftEvent_ArrowL", ArrowRows(true), ArrowPal);

    static string[] ArrowRows(bool flip)
    {
        string[] rows =
        {
            "..............",
            "..............",
            "..............",
            "..............",
            "...........G..",
            "F..........GG.",
            "FFWWWWWWWWWGGG",
            "F..........GG.",
            "...........G..",
            "..............",
            "..............",
            "..............",
            "..............",
            "..............",
        };
        if (!flip) return rows;
        var r = new string[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            char[] c = rows[i].ToCharArray();
            System.Array.Reverse(c);
            r[i] = new string(c);
        }
        return r;
    }
}

/// <summary>【仮】予告の間、床の上で小さくはねる土のかけら（seconds 秒で消える。飾り）</summary>
public class MinecraftDirtHop : MonoBehaviour
{
    float _born, _life, _phase;
    Vector3 _base;

    public void Setup(float seconds, float phase)
    {
        _born = Time.time;
        _life = seconds;
        _phase = phase;
        _base = transform.localPosition;
    }

    void Update()
    {
        float t = Time.time - _born;
        if (t >= _life)
        {
            Destroy(gameObject);
            return;
        }
        float hop = Mathf.Abs(Mathf.Sin(t * 11f + _phase)) * 0.22f;
        transform.localPosition = _base + new Vector3(0f, Mathf.Round(hop * 18f) / 18f, 0f);
    }
}
