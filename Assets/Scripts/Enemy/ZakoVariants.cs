using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【仮】新しい雑魚のひな形を、実行時に作る（2026-11-19。docs/stage-pacing-design.md 5章・10-3。担当3）。
/// ・StagePacer.Started で、ZakoSpawner の歩く雑魚のひな形（Template(0)）を複製して作り、AddTemplate で足す（重みは 0：今までの出し方では選ばれない）
/// ・番号を Small・Elite・Flyer・Charger に入れる。作れなかったら -1（担当1 は -1 なら歩く雑魚で代わりに出す）
/// ・シーンとビルダーは変えない。絵は今の雑魚の絵の色・大きさちがい＋小さい飾り（【要相談⑨】は「色・大きさちがい」のまま）
///   小さい雑魚：0.8倍・速さ 1.5倍・ピンク／強い雑魚：1.5倍・HP 3・速さ 0.7倍・アプリの色・アプリのアイコンを抱える・頭の上に HP の赤い点
///   とぶ雑魚：水色・羽／突っ込む雑魚：だいだい色・赤い角
/// 出すときは ZakoSpawner.SpawnNow を通す（雑魚の数・EnemyWave に数える）。番号は出すときに読む（ステージごとに変わる）
/// 数値はすべて【仮】
/// </summary>
public static class ZakoVariants
{
    /// <summary>小さい雑魚（群れ用）の番号（無ければ -1）</summary>
    public static int Small { get; private set; } = -1;
    /// <summary>強い雑魚の番号（無ければ -1）</summary>
    public static int Elite { get; private set; } = -1;
    /// <summary>とぶ雑魚の番号（無ければ -1）</summary>
    public static int Flyer { get; private set; } = -1;
    /// <summary>突っ込む雑魚の番号（無ければ -1）</summary>
    public static int Charger { get; private set; } = -1;

    public const float SmallScale = 0.8f;
    public const float SmallSpeed = 1.5f;
    public const float EliteScale = 1.5f;
    public const float EliteSpeed = 0.7f;
    public const int EliteHp = 3;
    public const float ChargerRun = 3f;

    static readonly Color SmallTint = RuntimeSprites.Hex("FFB0E0");
    static readonly Color FlyerTint = RuntimeSprites.Hex("A8E4FF");
    static readonly Color ChargerTint = RuntimeSprites.Hex("FFB46B");
    static readonly Color EliteFallback = RuntimeSprites.Hex("FFC36B");

    [Serializable]
    class WalkerProbe
    {
        public float _speed = 2f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Clear();
        StagePacer.Started -= OnStarted;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        StagePacer.Started -= OnStarted;
        StagePacer.Started += OnStarted;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Clear(); // 前のステージの番号を残さない
    }

    static void Clear()
    {
        Small = -1;
        Elite = -1;
        Flyer = -1;
        Charger = -1;
    }

    static void OnStarted(StagePacer pacer)
    {
        Clear();
        ZakoSpawner spawner = pacer != null ? pacer.Spawner : null;
        if (spawner == null) return;
        GameObject walker = spawner.Template(0);
        if (walker == null || walker.GetComponent<ZakoWalker>() == null || walker.GetComponent<Health>() == null) return;
        Small = Register(spawner, () => BuildSmall(walker, spawner.transform));
        Elite = Register(spawner, () => BuildElite(walker, spawner.transform, pacer));
        Flyer = Register(spawner, () => BuildFlyer(walker, spawner.transform));
        Charger = Register(spawner, () => BuildCharger(walker, spawner.transform));
    }

    static int Register(ZakoSpawner spawner, Func<GameObject> build)
    {
        GameObject t = null;
        try
        {
            t = build();
            if (t == null) return -1;
            int index = spawner.AddTemplate(t, 0f);
            if (index < 0)
            {
                UnityEngine.Object.Destroy(t);
                return -1;
            }
            return index;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"新しい雑魚のひな形を作れません：{e}");
            if (t != null) UnityEngine.Object.Destroy(t);
            return -1;
        }
    }

    // ───────── それぞれの雑魚 ─────────

    static GameObject BuildSmall(GameObject walker, Transform parent)
    {
        GameObject go = CloneBase(walker, parent, "Template_Small", SmallScale, SmallTint);
        ScaleWalkerSpeed(go, SmallSpeed);
        return go;
    }

    static GameObject BuildElite(GameObject walker, Transform parent, StagePacer pacer)
    {
        AppTheme theme = RewardKit.ThemeOf(pacer);
        Color tint = Color.Lerp(FxKit.MainOf(theme, EliteFallback), Color.white, 0.35f);
        tint.a = 1f;
        GameObject go = CloneBase(walker, parent, "Template_Elite", EliteScale, tint);

        // HP 3（Health には最大値を変える入口が無いので、TerminalGhost と同じく保存される欄を書き換える。まだ Awake 前なので、出たときに 3 で始まる）
        Health health = go.GetComponent<Health>();
        JsonUtility.FromJsonOverwrite("{\"_max\":" + EliteHp + "}", health);
        ScaleWalkerSpeed(go, EliteSpeed);
        ZakoWalker w = go.GetComponent<ZakoWalker>();
        JsonUtility.FromJsonOverwrite("{\"_knockbackScale\":0.25}", w); // 重いので、押し戻されにくい

        // そのステージのアプリのアイコンを抱える（左右反転しないように、体の直下に置く）
        Sprite icon = RewardKit.IconOf(pacer);
        Transform held;
        float inv = 1f / EliteScale;
        if (icon != null)
        {
            SpriteRenderer sr = RuntimeSprites.Show(go.transform, icon, new Vector2(0f, 0.3f), 12, 1f, "HeldIcon");
            float s = RewardKit.IconScale(icon, 9f) * inv;
            sr.transform.localScale = new Vector3(s, s, 1f);
            held = sr.transform;
        }
        else
        {
            SpriteRenderer sr = RuntimeSprites.Show(go.transform, FxArt.Square(7), new Vector2(0f, 0.3f), 12, inv, "HeldIcon");
            Color c = FxKit.MainOf(theme, EliteFallback);
            c.a = 1f;
            sr.color = c;
            held = sr.transform;
        }

        // 頭の上に、HP の赤い点（3つ。当たるたびに1つ消える）
        var pips = new SpriteRenderer[EliteHp];
        for (int i = 0; i < EliteHp; i++)
        {
            float x = (i - (EliteHp - 1) * 0.5f) * (4f / 18f) * inv;
            pips[i] = RuntimeSprites.Show(go.transform, FxArt.Square(2), new Vector2(x, 0.98f), 12, inv, "Pip" + i);
            pips[i].color = FxKit.EnemyRed;
        }

        var elite = go.AddComponent<ZakoElite>();
        elite.Configure(pips, held);
        return go;
    }

    static GameObject BuildFlyer(GameObject walker, Transform parent)
    {
        GameObject go = CloneBase(walker, parent, "Template_Flyer", 1f, FlyerTint);
        ZakoWalker w = go.GetComponent<ZakoWalker>();
        float speed = ReadWalkerSpeed(w);
        w.enabled = false; // 歩かない（とぶ雑魚が自分で動かす。KinematicBody2D.Move は呼ばない）

        Transform visual = go.transform.Find("Visual");
        if (visual == null) visual = go.transform;
        SpriteRenderer wl = RuntimeSprites.Show(visual, WingUp, new Vector2(-0.42f, 0.55f), 10, 1f, "WingL");
        SpriteRenderer wr = RuntimeSprites.Show(visual, WingUp, new Vector2(0.42f, 0.55f), 10, 1f, "WingR");
        wr.flipX = true;
        var flyer = go.AddComponent<ZakoFlyer>();
        flyer.Configure(speed, wl, wr, WingUp, WingDown);
        return go;
    }

    static GameObject BuildCharger(GameObject walker, Transform parent)
    {
        GameObject go = CloneBase(walker, parent, "Template_Charger", 1f, ChargerTint);
        ZakoWalker w = go.GetComponent<ZakoWalker>();
        float speed = ReadWalkerSpeed(w);

        Transform visual = go.transform.Find("Visual");
        if (visual == null) visual = go.transform;
        RuntimeSprites.Show(visual, Horns, new Vector2(0f, 0.86f), 12, 1f, "Horns");
        var charger = go.AddComponent<ZakoCharger>();
        charger.Configure(speed * ChargerRun);
        return go;
    }

    // ───────── 道具 ─────────

    /// <summary>歩く雑魚のひな形を複製する（隠したまま）。大きさ・色だけ変える（色は今の絵にかけるので、灰色の所に出る）</summary>
    static GameObject CloneBase(GameObject walker, Transform parent, string name, float scale, Color tint)
    {
        GameObject go = UnityEngine.Object.Instantiate(walker, parent);
        go.SetActive(false);
        go.name = name; // ZakoSpawner が「Template_」を外して名前にする
        go.transform.localPosition = walker.transform.localPosition;
        go.transform.localScale = walker.transform.localScale * scale;
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Color c = tint;
            c.a = sr.color.a;
            sr.color = c;
        }
        return go;
    }

    static float ReadWalkerSpeed(ZakoWalker w)
    {
        if (w == null) return 2f;
        var probe = new WalkerProbe();
        try { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(w), probe); }
        catch (Exception) { probe._speed = 2f; }
        return probe._speed > 0.01f ? probe._speed : 2f;
    }

    static void ScaleWalkerSpeed(GameObject go, float mult)
    {
        ZakoWalker w = go.GetComponent<ZakoWalker>();
        if (w == null) return;
        float speed = ReadWalkerSpeed(w) * mult;
        JsonUtility.FromJsonOverwrite("{\"_speed\":" + speed.ToString("0.###", CultureInfo.InvariantCulture) + "}", w);
    }

    // ───────── ドット絵 ─────────

    static readonly Dictionary<char, Color32> WingPalette = new Dictionary<char, Color32>
    {
        { 'W', RuntimeSprites.Hex("F2F2F7") },
        { 'G', RuntimeSprites.Hex("AEAEB2") },
    };

    /// <summary>羽（上げた形。左の羽。右は左右反転）</summary>
    static Sprite WingUp => RuntimeSprites.FromAscii("Pace_WingUp", new[]
    {
        "W.....",
        "WW....",
        "WWW...",
        "GWWWW.",
        ".GGWWW",
    }, WingPalette);

    /// <summary>羽（下げた形）</summary>
    static Sprite WingDown => RuntimeSprites.FromAscii("Pace_WingDown", new[]
    {
        ".GGWWW",
        "GWWWW.",
        "WWW...",
        "WW....",
        "W.....",
    }, WingPalette);

    /// <summary>赤い角（突っ込む雑魚）</summary>
    static Sprite Horns => RuntimeSprites.FromAscii("Pace_Horns", new[]
    {
        "R..........R",
        "RR........RR",
        "DRR......RRD",
        ".DRR....RRD.",
    }, new Dictionary<char, Color32> { { 'R', RuntimeSprites.Hex("FF453A") }, { 'D', RuntimeSprites.Hex("8A1F18") } });
}
