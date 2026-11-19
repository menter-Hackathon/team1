using UnityEngine;

// 【仮】Mac の部品を OnGUI で描く（2026-11-19。docs/mac-look-design.md 1-10 の (c)）。
// ・PixelGui.Begin() のあと（仮想の画面 640×360 のドット）で呼ぶ。数はゲームの世界の絵と同じ
// ・色は PixelGui.Fill（リニアの色空間の直しつき）で塗る

public static partial class MacUi
{
    /// <summary>【仮】OnGUI の Mac の部品（デスクトップ・Spotlight・Premiere の窓・再起動・Terminal の帯）</summary>
    public static class Gui
    {
        /// <summary>
        /// ウィンドウを描く（影・角の丸いふち・タイトルバー・左上の信号機・まん中の題名・下の線）。中の面の四角を返す。
        /// corner を入れなければ、幅 80 以上は 3、それより小さいと 2。lightD で信号機の丸を大きくできる（タイトルバーの高さ − 2 まで）
        /// </summary>
        public static Rect Window(Rect r, string title, MacLook look, bool active = true, int corner = -1, Color? edge = null, Color? titleColor = null, int lightD = -1)
        {
            r = Snap(r);
            int c = corner >= 0 ? corner : CornerFor(Mathf.RoundToInt(r.width));
            int th = TitleHeightFor(Mathf.RoundToInt(r.height));
            RoundFill(new Rect(r.x, r.y + ShadowY, r.width, r.height), c, ShadowColor(look));
            RoundFill(r, c, edge ?? Edge(look));
            RoundFill(Inset(r, 1f), Mathf.Max(0, c - 1), WindowBg(look));
            // タイトルバー（上の角は丸いまま）
            Rect bar = new Rect(r.x + 1f, r.y + 1f, r.width - 2f, th);
            RoundTop(bar, Mathf.Max(0, c - 1), TitleBg(look));
            PixelGui.Fill(new Rect(r.x + 1f, r.y + 1f + th, r.width - 2f, 1f), TitleLine(look));
            if (r.width < TinyWindowW) LightsTiny(r.x + 2f, r.y + 1f + (th - 1) / 2, active);
            else if (lightD > LightD)
            {
                // 大きい信号機（担当5 のお願い。タイトルバーに入る大きさまで）
                int d = Mathf.Min(lightD, th - 2);
                Lights(r.x + LightX, r.y + 1f + (th - d) / 2, d, active);
            }
            else Lights(r.x + LightX, r.y + 1f + (th - LightD) / 2, active);
            if (!string.IsNullOrEmpty(title))
            {
                Rect tr = new Rect(r.x + 16f, r.y + 1f, r.width - 32f, th + 1f);
                Color ink = titleColor ?? Ink(look);
                if (IsAsciiText(title)) PixelGui.TextEn(tr, title, 8, ink, TextAnchor.MiddleCenter);
                else PixelGui.Text(tr, title, 12, ink, TextAnchor.MiddleCenter);
            }
            return new Rect(r.x + 1f, r.y + 2f + th, r.width - 2f, r.height - 3f - th);
        }

        /// <summary>信号機（3×3 の丸 3つ）。x は左の丸の左の端、y は上の端</summary>
        public static void Lights(float x, float y, bool active = true, bool redOnly = false)
        {
            Color[] cols =
            {
                active ? LightR : LightOff,
                active && !redOnly ? LightY : LightOff,
                active && !redOnly ? LightG : LightOff,
            };
            for (int i = 0; i < 3; i++) Dot3(x + i * (LightD + LightGap), y, cols[i]);
        }

        /// <summary>
        /// 大きさを変えた信号機（担当5 のお願い：メニューの大きなウィンドウ用）。d は丸の直径（3 で Lights と同じ）。間は gap（無ければ d/3、1 以上）。
        /// x は左の丸の左の端、y は上の端。丸のふちのドットは 5割すかす
        /// </summary>
        public static void Lights(float x, float y, int d, bool active = true, bool redOnly = false, int gap = -1)
        {
            d = Mathf.Max(1, d);
            if (d == LightD && gap < 0)
            {
                Lights(x, y, active, redOnly);
                return;
            }
            int g = gap >= 0 ? gap : Mathf.Max(1, d / 3);
            Color[] cols =
            {
                active ? LightR : LightOff,
                active && !redOnly ? LightY : LightOff,
                active && !redOnly ? LightG : LightOff,
            };
            for (int i = 0; i < 3; i++)
            {
                float ox = Mathf.Round(x) + i * (d + g);
                StampDisc((px, py, c) => PixelGui.Fill(new Rect(ox + px, Mathf.Round(y) + py, 1f, 1f), c), 0, 0, d, cols[i]);
            }
        }

        /// <summary>大きさを変えた信号機の幅（3つと間 2つ）</summary>
        public static float LightsWidth(int d, int gap = -1) => d * 3 + (gap >= 0 ? gap : Mathf.Max(1, d / 3)) * 2;

        /// <summary>小さい信号機（1ドットの点 3つ）</summary>
        public static void LightsTiny(float x, float y, bool active = true)
        {
            PixelGui.Fill(new Rect(x, y, 1f, 1f), active ? LightR : LightOff);
            PixelGui.Fill(new Rect(x + 2f, y, 1f, 1f), active ? LightY : LightOff);
            PixelGui.Fill(new Rect(x + 4f, y, 1f, 1f), active ? LightG : LightOff);
        }

        static void Dot3(float x, float y, Color c)
        {
            Color half = new Color(c.r, c.g, c.b, c.a * 0.5f);
            PixelGui.Fill(new Rect(x + 1f, y, 1f, 3f), c);
            PixelGui.Fill(new Rect(x, y + 1f, 1f, 1f), c);
            PixelGui.Fill(new Rect(x + 2f, y + 1f, 1f, 1f), c);
            PixelGui.Fill(new Rect(x, y, 1f, 1f), half);
            PixelGui.Fill(new Rect(x + 2f, y, 1f, 1f), half);
            PixelGui.Fill(new Rect(x, y + 2f, 1f, 1f), half);
            PixelGui.Fill(new Rect(x + 2f, y + 2f, 1f, 1f), half);
        }

        /// <summary>
        /// メニューバー（画面の上の端から端。地は Light1 の 80%、下に Light4 の線）。
        /// 左：印（リンゴの代わり）→ 太字のアプリ名 → menus。右：Wi-Fi・電池・スイッチの印 → clock
        /// </summary>
        public static void MenuBar(Rect r, string appName, string[] menus, string clock, Sprite mark = null, float alpha = 1f)
        {
            r = Snap(r);
            PixelGui.Fill(r, new Color(Light1.r, Light1.g, Light1.b, 0.8f * alpha));
            PixelGui.Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), new Color(Light4.r, Light4.g, Light4.b, alpha));
            Color ink = new Color(Gray6.r, Gray6.g, Gray6.b, alpha);
            bool tall = r.height >= 14f;
            int jaSize = tall ? 12 : 8;
            float cy = r.y + (r.height - 1f - 7f) * 0.5f;
            float x = r.x + 6f;
            Sprite(new Rect(x, Mathf.Round(cy), 7f, 7f), mark != null ? mark : Mark(7), ink);
            x += 7f + 6f;
            if (!string.IsNullOrEmpty(appName))
            {
                float w = TextWidthPx(appName, jaSize);
                Rect ar = new Rect(x, r.y, w + 2f, r.height - 1f);
                DrawText(ar, appName, jaSize, ink, TextAnchor.MiddleLeft);
                DrawText(new Rect(ar.x + 1f, ar.y, ar.width, ar.height), appName, jaSize, ink, TextAnchor.MiddleLeft); // 太字
                x += w + 8f;
            }
            if (menus != null)
            {
                foreach (string m in menus)
                {
                    if (string.IsNullOrEmpty(m)) continue;
                    float w = TextWidthPx(m, jaSize);
                    if (x + w > r.xMax - 120f) break;
                    DrawText(new Rect(x, r.y, w + 2f, r.height - 1f), m, jaSize, ink, TextAnchor.MiddleLeft);
                    x += w + 7f;
                }
            }
            // 右：時刻 → 印 3つ（右から）
            float rx = r.xMax - 6f;
            if (!string.IsNullOrEmpty(clock))
            {
                float w = TextWidthPx(clock, jaSize);
                DrawText(new Rect(rx - w - 2f, r.y, w + 2f, r.height - 1f), clock, jaSize, ink, TextAnchor.MiddleRight);
                rx -= w + 8f;
            }
            for (int i = 2; i >= 0; i--)
            {
                rx -= 7f;
                Sprite(new Rect(rx, Mathf.Round(cy), 7f, 7f), StatusIcon(i), ink);
                rx -= 5f;
            }
        }

        /// <summary>Dock の板（角を 4ドット欠く。地は白 28%、ふちは白 60%）。中の四角を返す</summary>
        public static Rect Dock(Rect r)
        {
            r = Snap(r);
            RoundFill(Inset(r, 1f), 3, DockFill);
            RoundEdge(r, 4, DockEdge);
            return Inset(r, 1f);
        }

        /// <summary>Dock の起動中の点（2ドットの丸。cx がまん中）</summary>
        public static void DockDot(float cx, float y)
        {
            PixelGui.Fill(new Rect(Mathf.Round(cx - 1f), y, 2f, 2f), Gray6);
        }

        /// <summary>Dock の縦の区切りの線</summary>
        public static void DockDivider(float x, float y, float h)
        {
            PixelGui.Fill(new Rect(Mathf.Round(x), y, 1f, h), new Color(0f, 0f, 0f, 0.25f));
        }

        /// <summary>進みのバー（両端を 1ドット丸める）。t は 0〜1。fill が無ければ青</summary>
        public static void Progress(Rect r, float t, MacLook look = MacLook.Dark, Color? fill = null)
        {
            r = Snap(r);
            RoundFill(r, 1, ProgressTrough(look));
            float w = Mathf.Round(r.width * Mathf.Clamp01(t));
            if (w <= 0f) return;
            RoundFill(new Rect(r.x, r.y, Mathf.Max(2f, w), r.height), w >= 3f ? 1 : 0, fill ?? Blue);
        }

        /// <summary>矢印のカーソル（黒い矢印に白いふち。tip が先の位置）</summary>
        public static void Arrow(Vector2 tip)
        {
            Sprite(new Rect(Mathf.Round(tip.x), Mathf.Round(tip.y), 10f, 15f), MacUi.Arrow(), Color.white);
        }

        /// <summary>角を r 段欠いた四角を塗る</summary>
        public static void RoundFill(Rect rc, int r, Color color)
        {
            if (rc.width <= 0f || rc.height <= 0f) return;
            r = Mathf.Clamp(r, 0, Mathf.FloorToInt(Mathf.Min(rc.width, rc.height) * 0.5f));
            for (int i = 0; i < r; i++)
            {
                float inset = r - i;
                PixelGui.Fill(new Rect(rc.x + inset, rc.y + i, rc.width - inset * 2f, 1f), color);
                PixelGui.Fill(new Rect(rc.x + inset, rc.yMax - 1f - i, rc.width - inset * 2f, 1f), color);
            }
            PixelGui.Fill(new Rect(rc.x, rc.y + r, rc.width, rc.height - r * 2f), color);
        }

        /// <summary>角を r 段欠いた四角のふち（1ドット）</summary>
        public static void RoundEdge(Rect rc, int r, Color color)
        {
            if (rc.width <= 2f || rc.height <= 2f) return;
            r = Mathf.Clamp(r, 0, Mathf.FloorToInt(Mathf.Min(rc.width, rc.height) * 0.5f) - 1);
            PixelGui.Fill(new Rect(rc.x + r, rc.y, rc.width - r * 2f, 1f), color);
            PixelGui.Fill(new Rect(rc.x + r, rc.yMax - 1f, rc.width - r * 2f, 1f), color);
            PixelGui.Fill(new Rect(rc.x, rc.y + r, 1f, rc.height - r * 2f), color);
            PixelGui.Fill(new Rect(rc.xMax - 1f, rc.y + r, 1f, rc.height - r * 2f), color);
            for (int i = 1; i < r; i++)
            {
                float d = r - i;
                PixelGui.Fill(new Rect(rc.x + d, rc.y + i, 1f, 1f), color);
                PixelGui.Fill(new Rect(rc.xMax - 1f - d, rc.y + i, 1f, 1f), color);
                PixelGui.Fill(new Rect(rc.x + d, rc.yMax - 1f - i, 1f, 1f), color);
                PixelGui.Fill(new Rect(rc.xMax - 1f - d, rc.yMax - 1f - i, 1f, 1f), color);
            }
        }

        /// <summary>上の角だけ丸い四角を塗る（タイトルバー）</summary>
        public static void RoundTop(Rect rc, int r, Color color)
        {
            if (rc.width <= 0f || rc.height <= 0f) return;
            for (int i = 0; i < r; i++)
            {
                float inset = r - i;
                PixelGui.Fill(new Rect(rc.x + inset, rc.y + i, rc.width - inset * 2f, 1f), color);
            }
            PixelGui.Fill(new Rect(rc.x, rc.y + r, rc.width, rc.height - r), color);
        }

        /// <summary>スプライトを描く（点のまま。tint で塗る）</summary>
        public static void Sprite(Rect r, Sprite s, Color tint)
        {
            if (s == null || Event.current == null || Event.current.type != EventType.Repaint) return;
            Texture2D tex = s.texture;
            if (tex == null) return;
            if (tex.filterMode != FilterMode.Point) tex.filterMode = FilterMode.Point;
            Rect tr = s.textureRect;
            Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            Color saved = GUI.color;
            GUI.color = PixelGui.ToGui(tint);
            GUI.DrawTextureWithTexCoords(r, tex, uv, true);
            GUI.color = saved;
        }

        static Rect Inset(Rect r, float d) => new Rect(r.x + d, r.y + d, r.width - d * 2f, r.height - d * 2f);
        static Rect Snap(Rect r) => new Rect(Mathf.Round(r.x), Mathf.Round(r.y), Mathf.Round(r.width), Mathf.Round(r.height));

        static bool IsAsciiText(string s)
        {
            foreach (char ch in s) if (ch > 126) return false;
            return true;
        }

        static float TextWidthPx(string s, int jaSize)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            return IsAsciiText(s) ? s.Length * 8f : PixelGui.Measure(s, PixelGui.Style(false, jaSize));
        }

        static void DrawText(Rect r, string s, int jaSize, Color c, TextAnchor anchor)
        {
            if (IsAsciiText(s)) PixelGui.TextEn(r, s, 8, c, anchor);
            else PixelGui.Text(r, s, jaSize, c, anchor);
        }
    }
}
