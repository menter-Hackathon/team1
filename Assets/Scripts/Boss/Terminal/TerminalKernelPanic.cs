using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】カーネル・パニックの背景（2026-11-18：Terminal の第2形態。docs/app-boss-skills-design.md 4-4・5章）。
/// Mac のカーネル・パニック画面のように、真っ黒に小さい文字がびっしり。
/// ・空・雲・丘（壁紙）の上に、真っ黒 #000000 の面をかぶせる（壁紙より手前、床・足場より奥）。カメラの背景の色も黒
/// ・飾りの文字：灰 #48484A（2026-11-19：Mac のパニックは黒に白い字。暗い緑 #0F3D1E から変えた。docs/mac-look-design.md 2章 11）、
///   5ドットの小さい字を、びっしり並べて、ゆっくり上へ流す（当たらない）
/// ・床の上の線・足場のふち：緑 #33FF66 を、うすい灰色 #C8C8C8 に変える（赤にはしない。赤は「危ない」の予告だけに使う）
/// 消えるときに（Destroy）、床・足場の絵とカメラの色を元に戻す
/// </summary>
public class TerminalKernelPanic : MonoBehaviour
{
    // ───────── 見た目【仮】 ─────────
    const float RowHeight = 5f / 18f; // 5ドットの字
    const float RowSpacing = 0.36f;
    const float ScrollSpeed = 0.25f;  // マス/秒
    const int CoverOrder = -99;       // 壁紙（-100）のすぐ手前
    const int TextOrder = -98;
    static readonly Color32 LineGreen = new Color32(0x33, 0xff, 0x66, 0xff);
    static readonly Color32 LineGray = new Color32(0xc8, 0xc8, 0xc8, 0xff);

    /// <summary>それらしい英字（個人の情報はふくめない）</summary>
    static readonly string[] Lines =
    {
        "panic(cpu 0 caller 0x0): kernel trap",
        "BSD process name: virus",
        "System uptime in nanoseconds: 0",
        "Backtrace (CPU 0), Frame : Return Address",
        "Kernel Extensions in backtrace:",
        "last loaded kext: com.virus.driver",
        "Debugger called: panic",
        "Mac OS version: Not yet set",
        "Kernel version: Darwin Kernel",
        "Panicked task: pid 0: kernel_task",
        "trap number: 14, page fault",
        "Fatal exception in process: virus",
    };

    class Swap
    {
        public SpriteRenderer Renderer;
        public Sprite Original;
        public Sprite Replaced;
    }

    readonly List<Transform> _rows = new List<Transform>();
    readonly List<Swap> _swaps = new List<Swap>();
    readonly Dictionary<Sprite, Sprite> _made = new Dictionary<Sprite, Sprite>();
    readonly List<Object> _owned = new List<Object>();
    float _top;
    float _span;
    Camera _cam;
    Color _camColor;
    bool _restored;

    /// <summary>カーネル・パニックの背景を出す（消すときは、この GameObject を Destroy）</summary>
    public static TerminalKernelPanic Create()
    {
        var go = new GameObject("TerminalKernelPanic");
        var panic = go.AddComponent<TerminalKernelPanic>();
        panic.Build();
        return panic;
    }

    /// <summary>飾りの文字の色【仮】（Mac の灰。床の塗り替えの緑とは別）</summary>
    static readonly Color RowColor = MacUi.Gray3;

    void Build()
    {
        Rect view = ScreenUi.ViewRect();
        transform.position = new Vector3(view.center.x, view.center.y, 0f);

        // 真っ黒の面（画面のゆれでもはみ出さないよう、大きめ）
        TerminalArt.Box(transform, Vector2.zero, view.size * 1.6f + Vector2.one * 2f, TerminalArt.Black, CoverOrder, "Black");

        // 飾りの文字（毎回同じ並び）
        var rng = new System.Random(20261118);
        float left = view.xMin - 1f;
        _top = view.yMax + 1f;
        float bottom = view.yMin - 1f;
        int rows = Mathf.CeilToInt((_top - bottom) / RowSpacing);
        _span = rows * RowSpacing;
        int chars = Mathf.CeilToInt((view.width + 2f) / RowHeight) + 4;
        for (int i = 0; i < rows; i++)
        {
            TextMesh tm = WorldText.Create(transform, MakeLine(rng, chars), RowHeight, RowColor, TextOrder,
                true, TextAnchor.MiddleLeft, "Row");
            tm.richText = false;
            tm.alignment = TextAlignment.Left;
            tm.transform.position = new Vector3(left, _top - i * RowSpacing, 0f);
            _rows.Add(tm.transform);
        }

        // 壁紙の外（画面の端）も黒
        _cam = Camera.main;
        if (_cam != null)
        {
            _camColor = _cam.backgroundColor;
            _cam.backgroundColor = TerminalArt.Black;
        }

        RecolorTerrain();
    }

    static string MakeLine(System.Random rng, int length)
    {
        var sb = new System.Text.StringBuilder(length + 48);
        while (sb.Length < length)
        {
            if (rng.Next(3) == 0)
            {
                sb.Append("0x");
                for (int k = 0; k < 16; k++) sb.Append("0123456789abcdef"[rng.Next(16)]);
            }
            else
            {
                sb.Append(Lines[rng.Next(Lines.Length)]);
            }
            sb.Append("  ");
        }
        return sb.ToString(0, length);
    }

    void Update()
    {
        float dy = ScrollSpeed * Time.deltaTime;
        foreach (var row in _rows)
        {
            if (row == null) continue;
            Vector3 p = row.position;
            p.y += dy;
            if (p.y > _top) p.y -= _span;
            row.position = p;
        }
    }

    // ───────── 床・足場の色 ─────────

    /// <summary>床・足場（レイヤー Ground・OneWayPlatform）の絵の、緑 #33FF66 のドットを灰色にする</summary>
    void RecolorTerrain()
    {
        int ground = LayerMask.NameToLayer("Ground");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        foreach (var sr in FindObjectsOfType<SpriteRenderer>())
        {
            if (sr == null || sr.sprite == null) continue;
            int layer = sr.gameObject.layer;
            if (layer != ground && layer != oneWay) continue;
            Sprite replaced = Recolored(sr.sprite);
            if (replaced == null) continue;
            _swaps.Add(new Swap { Renderer = sr, Original = sr.sprite, Replaced = replaced });
            sr.sprite = replaced;
        }
    }

    /// <summary>緑のドットを灰色にした複製。緑が無い・読めない絵なら null</summary>
    Sprite Recolored(Sprite src)
    {
        if (_made.TryGetValue(src, out Sprite done)) return done;
        Sprite made = null;
        Texture2D tex = src.texture;
        if (tex != null && tex.isReadable)
        {
            Rect r = src.textureRect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            Color[] c = tex.GetPixels(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), w, h);
            var px = new Color32[c.Length];
            bool any = false;
            for (int i = 0; i < c.Length; i++)
            {
                Color32 p = c[i];
                if (p.a > 0 && Near(p, LineGreen))
                {
                    px[i] = new Color32(LineGray.r, LineGray.g, LineGray.b, p.a);
                    any = true;
                }
                else px[i] = p;
            }
            if (any)
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = $"{src.name}_Panic",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                t.SetPixels32(px);
                t.Apply();
                Vector2 pivot = new Vector2(src.pivot.x / src.rect.width, src.pivot.y / src.rect.height);
                made = Sprite.Create(t, new Rect(0, 0, w, h), pivot, src.pixelsPerUnit, 0, SpriteMeshType.FullRect, src.border);
                made.name = t.name;
                _owned.Add(t);
                _owned.Add(made);
            }
        }
        _made[src] = made;
        return made;
    }

    static bool Near(Color32 a, Color32 b) =>
        Mathf.Abs(a.r - b.r) <= 3 && Mathf.Abs(a.g - b.g) <= 3 && Mathf.Abs(a.b - b.b) <= 3;

    // ───────── 戻す ─────────

    void OnDestroy() => RestoreAll();

    /// <summary>床・足場の絵とカメラの色を元に戻す（1回だけ。シーンを閉じるときも呼ばれるので、消えた物は飛ばす）</summary>
    void RestoreAll()
    {
        if (_restored) return;
        _restored = true;
        foreach (var s in _swaps)
        {
            if (s.Renderer != null && s.Renderer.sprite == s.Replaced) s.Renderer.sprite = s.Original;
        }
        _swaps.Clear();
        if (_cam != null) _cam.backgroundColor = _camColor;
        foreach (var o in _owned)
        {
            if (o != null) Destroy(o);
        }
        _owned.Clear();
    }
}
