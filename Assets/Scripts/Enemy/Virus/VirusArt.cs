using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】ウイルスの種類の絵と、ひな形の道具（2026-11-20。docs/virus-types-design.md 6章・7章 A）。
/// ・絵：Resources/Virus/Virus_&lt;Type&gt;.png（1コマ 26×24px・横1列）・Nest_&lt;Type&gt;.png（巣）。Tools/art/virus/build_virus.py が描く
/// ・実行時に切り分けて Sprite を作る（取り込みはテクスチャ1枚。Editor/Art/VirusImport.cs）。原点は足元（6章の表）
/// ・ひな形は名前「Template_&lt;Type&gt;」で探す（番号を static で持ち合わない）
/// 数値はすべて【仮】
/// </summary>
public static class VirusArt
{
    /// <summary>安全のためのスイッチ。false なら、新しい種類・巣・割り当てが全部止まる（今までどおり）</summary>
    public static bool Enabled = true;

    public const int FrameW = 26, FrameH = 24;
    public const float PixelsPerUnit = 18f;
    public const string Folder = "Virus";

    /// <summary>1種類分の決まり（6章の表）</summary>
    public struct Spec
    {
        public int CenterPx;      // 目のまん中（左から）
        public int FeetPx;        // 下から何 px が足元（原点）か
        public float Fps;         // 0 以下：止める（コードがコマを入れる）
        public Vector2 HitSize;   // 当たりの幅×高さ（マス）
        public float HitCenterY;  // 当たりのまん中の高さ（マス。足元から）
    }

    static readonly Dictionary<string, Spec> Specs = new Dictionary<string, Spec>
    {
        { "Worm",       new Spec { CenterPx = 12, FeetPx = 1, Fps = 8f,   HitSize = new Vector2(1.1f, 0.45f),  HitCenterY = 0.25f } },
        { "Rootkit",    new Spec { CenterPx = 12, FeetPx = 9, Fps = 0f,   HitSize = new Vector2(0.6f, 0.75f),  HitCenterY = 0.4f } },
        { "Trojan",     new Spec { CenterPx = 12, FeetPx = 1, Fps = 0f,   HitSize = new Vector2(0.8f, 0.8f),   HitCenterY = 0.55f } },
        { "Ransomware", new Spec { CenterPx = 12, FeetPx = 1, Fps = 6f,   HitSize = new Vector2(0.6f, 0.7f),   HitCenterY = 0.4f } },
        { "Spyware",    new Spec { CenterPx = 11, FeetPx = 1, Fps = 4f,   HitSize = new Vector2(0.7f, 0.65f),  HitCenterY = 0.55f } },
        { "Adware",     new Spec { CenterPx = 12, FeetPx = 1, Fps = 2f,   HitSize = new Vector2(0.6f, 0.6f),   HitCenterY = 0.35f } },
        { "Botnet",     new Spec { CenterPx = 12, FeetPx = 1, Fps = 4f,   HitSize = new Vector2(0.55f, 0.55f), HitCenterY = 0.35f } },
        { "Bomb",       new Spec { CenterPx = 12, FeetPx = 1, Fps = 0f,   HitSize = new Vector2(0.65f, 0.65f), HitCenterY = 0.45f } },
        { "Miner",      new Spec { CenterPx = 9,  FeetPx = 1, Fps = 5f,   HitSize = new Vector2(0.6f, 0.6f),   HitCenterY = 0.35f } },
        { "Poly",       new Spec { CenterPx = 12, FeetPx = 1, Fps = 1.5f, HitSize = new Vector2(0.65f, 0.65f), HitCenterY = 0.5f } },
        { "Macro",      new Spec { CenterPx = 12, FeetPx = 1, Fps = 0f,   HitSize = new Vector2(0.6f, 0.85f),  HitCenterY = 0.5f } },
        { "Phish",      new Spec { CenterPx = 7,  FeetPx = 1, Fps = 5f,   HitSize = new Vector2(0.6f, 0.6f),   HitCenterY = 0.35f } },
    };

    /// <summary>巣の1コマの大きさ（px）と原点（下から何 px か）</summary>
    static readonly Dictionary<string, (int w, int h, int feet)> NestSpecs = new Dictionary<string, (int, int, int)>
    {
        { "Worm", (30, 20, 0) },     // 下の辺が付く面
        { "Rootkit", (44, 16, 12) }, // 下から 12px が床の線
    };

    static readonly Dictionary<string, Sprite[]> FrameCache = new Dictionary<string, Sprite[]>();
    static readonly Dictionary<string, Sprite[]> NestCache = new Dictionary<string, Sprite[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Enabled = true;
        FrameCache.Clear();
        NestCache.Clear();
    }

    /// <summary>その種類の決まりがあるか</summary>
    public static bool Has(string type) => !string.IsNullOrEmpty(type) && Specs.ContainsKey(type);

    /// <summary>その種類の決まり（無ければ false）</summary>
    public static bool TryGetSpec(string type, out Spec spec)
    {
        spec = default;
        return !string.IsNullOrEmpty(type) && Specs.TryGetValue(type, out spec);
    }

    /// <summary>"Virus/Virus_&lt;type&gt;" を切り分けたコマ（原点は足元）。無ければ null</summary>
    public static Sprite[] Frames(string type)
    {
        if (!TryGetSpec(type, out Spec spec)) return null;
        if (FrameCache.TryGetValue(type, out Sprite[] cached)) return cached;
        Sprite[] frames = Slice($"{Folder}/Virus_{type}", "Virus_" + type, FrameW, FrameH,
            new Vector2((spec.CenterPx + 0.5f) / FrameW, spec.FeetPx / (float)FrameH));
        FrameCache[type] = frames;
        return frames;
    }

    /// <summary>"Virus/Nest_&lt;type&gt;"（Worm 30×20、Rootkit 44×16）。無ければ null</summary>
    public static Sprite[] NestFrames(string type)
    {
        if (string.IsNullOrEmpty(type) || !NestSpecs.TryGetValue(type, out var ns)) return null;
        if (NestCache.TryGetValue(type, out Sprite[] cached)) return cached;
        Sprite[] frames = Slice($"{Folder}/Nest_{type}", "Nest_" + type, ns.w, ns.h, new Vector2(0.5f, ns.feet / (float)ns.h));
        NestCache[type] = frames;
        return frames;
    }

    static Sprite[] Slice(string path, string name, int fw, int fh, Vector2 pivot)
    {
        Texture2D tex = Resources.Load<Texture2D>(path);
        if (tex == null || tex.width < fw || tex.height < fh) return null;
        tex.filterMode = FilterMode.Point;
        int count = tex.width / fw;
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            frames[i] = Sprite.Create(tex, new Rect(i * fw, 0, fw, fh), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
            frames[i].name = $"{name}_{i}";
        }
        return frames;
    }

    /// <summary>
    /// spawner.Template(baseIndex) を spawner の子に複製する（止めたまま）。名前は "Template_&lt;type&gt;"。
    /// 子 Visual/Art の色だけ白にもどす（強い雑魚の HP の赤い点などは変えない）。作れなければ null
    /// </summary>
    public static GameObject Clone(ZakoSpawner spawner, int baseIndex, string type)
    {
        if (spawner == null) return null;
        GameObject src = spawner.Template(baseIndex);
        if (src == null) return null;
        GameObject go = Object.Instantiate(src, spawner.transform);
        go.SetActive(false);
        go.name = "Template_" + type; // ZakoSpawner が「Template_」を外して名前にする（"<type>_番号"）
        go.transform.localPosition = src.transform.localPosition;
        go.transform.localRotation = src.transform.localRotation;
        go.transform.localScale = src.transform.localScale;
        SpriteRenderer art = ArtOf(go);
        if (art != null) art.color = Color.white;
        return go;
    }

    /// <summary>
    /// 子 Visual/Art の絵をその種類のコマにする（fps が 0 以下なら止めて、コマ 0 を入れる）。
    /// Visual/Body（体当たり・被弾の箱）の大きさと位置を 6 章の表にする。絵が無ければ false（呼んだ側はひな形を捨てる）
    /// </summary>
    public static bool Dress(GameObject template, string type, float fps)
    {
        if (template == null || !TryGetSpec(type, out Spec spec)) return false;
        Sprite[] frames = Frames(type);
        if (frames == null || frames.Length == 0) return false;
        SpriteRenderer art = ArtOf(template);
        if (art == null) return false;

        art.transform.localPosition = Vector3.zero; // 原点が足元の絵なので、足元に置く
        var anim = art.GetComponent<SpriteFrameAnimation>();
        if (fps > 0f)
        {
            if (anim == null) anim = art.gameObject.AddComponent<SpriteFrameAnimation>();
            anim.Setup(art, frames, fps, true, false, true);
            anim.enabled = true;
        }
        else if (anim != null)
        {
            anim.enabled = false;
        }
        art.sprite = frames[0];

        Transform body = BodyOf(template);
        if (body != null)
        {
            body.localScale = new Vector3(spec.HitSize.x, spec.HitSize.y, 1f);
            body.localPosition = new Vector3(0f, spec.HitCenterY, 0f);
            body.localRotation = Quaternion.identity;
        }
        return true;
    }

    /// <summary>名前 "Template_&lt;type&gt;" のひな形の番号。無ければ -1（TemplateCount まで調べる）</summary>
    public static int IndexOf(ZakoSpawner spawner, string type)
    {
        if (spawner == null || string.IsNullOrEmpty(type)) return -1;
        string want = "Template_" + type;
        int n = spawner.TemplateCount;
        for (int i = 0; i < n; i++)
        {
            GameObject t = spawner.Template(i);
            if (t != null && t.name == want) return i;
        }
        return -1;
    }

    /// <summary>spawner.AliveList の中で、名前が "&lt;type&gt;_" で始まる数</summary>
    public static int AliveOf(ZakoSpawner spawner, string type)
    {
        if (spawner == null || string.IsNullOrEmpty(type)) return 0;
        string prefix = type + "_";
        int n = 0;
        IReadOnlyList<Health> list = spawner.AliveList;
        for (int i = 0; i < list.Count; i++)
        {
            Health h = list[i];
            if (h != null && !h.IsDead && h.gameObject.name.StartsWith(prefix, System.StringComparison.Ordinal)) n++;
        }
        return n;
    }

    /// <summary>その雑魚の種類（名前 "&lt;type&gt;_番号" から。種類でなければ null）</summary>
    public static string TypeOf(GameObject zako)
    {
        if (zako == null) return null;
        string n = zako.name;
        int us = n.IndexOf('_');
        if (us <= 0) return null;
        string t = n.Substring(0, us);
        return Has(t) ? t : null;
    }

    /// <summary>雑魚の絵（子 Visual/Art。無ければ Visual の下の最初の SpriteRenderer）</summary>
    public static SpriteRenderer ArtOf(GameObject zako)
    {
        if (zako == null) return null;
        Transform visual = zako.transform.Find("Visual");
        if (visual == null) return null;
        Transform art = visual.Find("Art");
        if (art != null && art.TryGetComponent(out SpriteRenderer sr)) return sr;
        return visual.GetComponentInChildren<SpriteRenderer>(true);
    }

    /// <summary>体当たり・被弾の箱（子 Visual/Body）。無ければ null</summary>
    public static Transform BodyOf(GameObject zako)
    {
        if (zako == null) return null;
        Transform visual = zako.transform.Find("Visual");
        return visual != null ? visual.Find("Body") : null;
    }
}
