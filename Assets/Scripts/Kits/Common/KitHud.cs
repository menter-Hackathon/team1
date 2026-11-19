using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 【仮】HUD のライフの下の小さい印とメーター（2026-11-19。docs/app-kits-1-design.md E6）・右上の通知。担当0 の物。
// ・KitPips：アプリのアイコン 8ドット＋上向き・下向きの矢印（文字なし）。StatMods に icon を渡して足した倍率も、ここに出る
//   プレイヤーの印はライフの下、ボスの印（SetBoss。StatMods の Boss・Zako の倍率も）はボスのゲージの下（2026-11-19：担当C の頼み）
// ・KitMeter：細いメーター（0〜1。アイコン 8ドット＋棒）。数える物（未読・使用上限など）
// ・描くのは PixelHud（OnGUI）。シーンが変わると全部消える。100％でも StageRuleHost が全部消す
// ・KitToast：右上の通知（CornerToast を呼ぶだけ。題名は英語の短い言葉）

/// <summary>HUD のライフの下の小さい印</summary>
public static class KitPips
{
    /// <summary>印1つ</summary>
    public struct Item
    {
        public string Key;
        public Sprite Icon;
        /// <summary>矢印の向き（true＝上）</summary>
        public bool Up;
        /// <summary>プレイヤーに有利か（矢印の色：緑／赤）</summary>
        public bool Good;
    }

    static readonly List<Item> Items = new List<Item>();
    static readonly List<Item> BossItems = new List<Item>();
    static readonly List<StatMods.Pip> StatBuffer = new List<StatMods.Pip>();

    /// <summary>印を出す（ライフの下。同じ鍵は上書き）</summary>
    public static void Set(string key, Sprite icon, bool up, bool good = true) => Put(Items, key, icon, up, good);

    /// <summary>ボスの印を出す（ボスのゲージの下。同じ鍵は上書き）。good：プレイヤーに有利か（ボスが強くなるなら false）</summary>
    public static void SetBoss(string key, Sprite icon, bool up, bool good = false) => Put(BossItems, key, icon, up, good);

    static void Put(List<Item> list, string key, Sprite icon, bool up, bool good)
    {
        if (string.IsNullOrEmpty(key)) return;
        list.RemoveAll(i => i.Key == key);
        list.Add(new Item { Key = key, Icon = icon, Up = up, Good = good });
    }

    /// <summary>印を消す（ライフの下・ボスの両方から）</summary>
    public static void Clear(string key)
    {
        Items.RemoveAll(i => i.Key == key);
        BossItems.RemoveAll(i => i.Key == key);
    }

    /// <summary>ボスの印を消す</summary>
    public static void ClearBoss(string key) => BossItems.RemoveAll(i => i.Key == key);

    /// <summary>全部消す（ボスの印も）</summary>
    public static void ClearAll()
    {
        Items.Clear();
        BossItems.Clear();
    }

    /// <summary>ライフの下に出す印（StatMods のプレイヤーの倍率の印も入る）</summary>
    public static void Collect(List<Item> into) => CollectFrom(Items, false, into);

    /// <summary>ボスのゲージの下に出す印（StatMods のボス・雑魚の倍率の印も入る）</summary>
    public static void CollectBoss(List<Item> into) => CollectFrom(BossItems, true, into);

    static void CollectFrom(List<Item> list, bool enemySide, List<Item> into)
    {
        into.AddRange(list);
        StatBuffer.Clear();
        StatMods.CollectPips(StatBuffer, enemySide);
        foreach (var p in StatBuffer)
        {
            if (list.Exists(i => i.Key == p.Key)) continue;
            into.Add(new Item { Key = p.Key, Icon = p.Icon, Up = p.Up, Good = p.Good });
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        ClearAll();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ClearAll();
}

/// <summary>HUD のライフの下の細いメーター</summary>
public static class KitMeter
{
    /// <summary>メーター1つ</summary>
    public struct Item
    {
        public string Key;
        /// <summary>0〜1</summary>
        public float Ratio;
        public Color Color;
        public Sprite Icon;
        /// <summary>点滅させるか（あふれそう・危ない）</summary>
        public bool Blink;
    }

    static readonly List<Item> Items = new List<Item>();

    /// <summary>メーターを出す・値を変える（同じ鍵は上書き）</summary>
    public static void Set(string key, float ratio, Color color, Sprite icon = null, bool blink = false)
    {
        if (string.IsNullOrEmpty(key)) return;
        var item = new Item { Key = key, Ratio = Mathf.Clamp01(ratio), Color = color, Icon = icon, Blink = blink };
        int i = Items.FindIndex(x => x.Key == key);
        if (i >= 0) Items[i] = item;
        else Items.Add(item);
    }

    /// <summary>メーターを消す</summary>
    public static void Clear(string key) => Items.RemoveAll(i => i.Key == key);

    /// <summary>全部消す</summary>
    public static void ClearAll() => Items.Clear();

    /// <summary>出すメーター</summary>
    public static void Collect(List<Item> into) => into.AddRange(Items);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Items.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Items.Clear();
}

/// <summary>右上の通知（Mac の通知）</summary>
public static class KitToast
{
    /// <summary>
    /// 右上に通知を出す（前の通知は入れ替わる）。title は英語の短い言葉。
    /// icon が null なら今のステージのアプリのアイコン、edge はアイコンが無いときの色
    /// </summary>
    public static void Show(string title, Sprite icon = null, Color? edge = null)
    {
        if (string.IsNullOrEmpty(title)) return;
        if (icon == null)
        {
            StageRuleHost h = StageRuleHost.Current;
            if (h != null && h.Context != null) icon = h.Context.Icon;
            if (icon == null && StagePacer.Current != null) icon = StagePacer.Current.Icon;
        }
        Color c = edge ?? (StagePacer.Current != null && StagePacer.Current.Theme != null ? StagePacer.Current.Theme.Main : MacUi.Blue);
        try { CornerToast.Show(title, icon, c); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
