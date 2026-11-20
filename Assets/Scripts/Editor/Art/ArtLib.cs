using UnityEditor;
using UnityEngine;

/// <summary>
/// シーンの作成ツールが使う、絵を置く道具。
/// 当たり判定は今までどおり「1×1 の四角を拡大した物」に付いているので、その物の絵は外し、絵は大きさ1の子に置く
/// （判定の大きさ・位置を変えないため）。
/// </summary>
internal static class ArtLib
{
    public const float Ppu = GameArt.PixelsPerUnit;
    public static readonly Color Sky = new Color32(0x42, 0x9f, 0xec, 0xff);

    /// <summary>地形の絵の種類。Ground は一番下の床（絵だけ下へ長くして、カメラが下を映しても切れないようにする）</summary>
    public enum BlockKind { Floor, Wall, Platform, Ground, WindowPlatform }

    /// <summary>そのままの大きさの絵を置く</summary>
    public static SpriteRenderer AddArt(Transform parent, string name, Sprite sprite, Vector3 localPos, int order, int layer)
    {
        var go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>9スライス（縁はそのまま、間を並べる）で大きさを指定して置く</summary>
    public static SpriteRenderer AddSized(Transform parent, string name, Sprite sprite, Vector3 localPos, Vector2 size, int order, int layer)
    {
        var sr = AddArt(parent, name, sprite, localPos, order, layer);
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;
        return sr;
    }

    /// <summary>コマ送りを付ける</summary>
    public static SpriteFrameAnimation Animate(SpriteRenderer sr, Sprite[] frames, float fps, bool loop = true, bool randomStart = true)
    {
        var anim = sr.gameObject.AddComponent<SpriteFrameAnimation>();
        PlayerTestSceneBuilder.SetProps(anim, so =>
        {
            so.FindProperty("_renderer").objectReferenceValue = sr;
            var arr = so.FindProperty("_frames");
            arr.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            so.FindProperty("_fps").floatValue = fps;
            so.FindProperty("_loop").boolValue = loop;
            so.FindProperty("_randomStart").boolValue = randomStart;
        });
        if (frames.Length > 0) sr.sprite = frames[0];
        return anim;
    }

    /// <summary>物に付いている仮の四角の絵を外す（当たり判定・Transform はそのまま）</summary>
    public static void RemoveRenderer(Component c)
    {
        var sr = c != null ? c.GetComponent<SpriteRenderer>() : null;
        if (sr != null) Object.DestroyImmediate(sr);
    }

    /// <summary>地形のブロック（1×1 を size に拡大した物）に絵を付ける</summary>
    public static void DecorateBlock(GameObject block, Vector2 size, BlockKind kind)
    {
        RemoveRenderer(block.transform);
        var holder = new GameObject("Art");
        holder.layer = block.layer;
        holder.transform.SetParent(block.transform, false);
        holder.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f); // 親の拡大を打ち消して、大きさ1にする
        switch (kind)
        {
            case BlockKind.Floor:
            case BlockKind.Ground:
                // 上の端から下へ並べる（絵の原点が上の端）。一番下の床は、カメラが下まで映しても切れないように、絵だけ下へ長くする
                holder.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                float extra = kind == BlockKind.Ground ? 8f : 0f;
                AddSized(holder.transform, "Floor", ArtAssets.LoadSprite("Generated/Stage/Tile_Floor.png"), Vector3.zero, new Vector2(size.x, size.y + extra), 1, block.layer);
                break;
            case BlockKind.Wall:
                AddSized(holder.transform, "Wall", ArtAssets.LoadSprite("Generated/Stage/Tile_Wall.png"), Vector3.zero, size, 1, block.layer);
                break;
            case BlockKind.WindowPlatform:
                // 開いたり閉じたりするウィンドウの足場（絵の原点は上の端＝乗る面）
                holder.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                AddSized(holder.transform, "Window", ArtAssets.LoadSprite("Generated/Stage/Platform_Window.png"), Vector3.zero, new Vector2(size.x, 8f / Ppu), 2, block.layer);
                break;
            case BlockKind.Platform:
                // 絵の原点は乗る面（フォルダのタブの下）。判定の上の端にそろえる
                holder.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                AddSized(holder.transform, "Folder", ArtAssets.LoadSprite("Generated/Stage/Platform_Folder.png"), Vector3.zero, new Vector2(size.x, 6f / Ppu), 2, block.layer);
                break;
        }
    }

    /// <summary>カメラに背景（デスクトップの壁紙）を付ける。カメラが動いても後ろに付いてくる</summary>
    public static void CreateBackground(Camera cam)
    {
        cam.backgroundColor = Sky;
        var bg = AddArt(cam.transform, "Background", ArtAssets.LoadSprite("Generated/Background/Bg_Desktop.png"), new Vector3(0f, 0f, 20f), -100, 0);
        bg.gameObject.name = "Background";
    }

    /// <summary>カメラに画面表示（ハート・能力の枠・ボスのゲージ）を付ける</summary>
    public static PixelHud CreateHud(Camera cam, GameObject player, StageDirector director)
    {
        var go = new GameObject("Hud");
        go.transform.SetParent(cam.transform, false);
        var hud = go.AddComponent<PixelHud>();
        PlayerTestSceneBuilder.SetProps(hud, so =>
        {
            so.FindProperty("_camera").objectReferenceValue = cam;
            if (player != null)
            {
                so.FindProperty("_skills").objectReferenceValue = player.GetComponent<PlayerSkills>();
                so.FindProperty("_executor").objectReferenceValue = player.GetComponent<PlayerSkillExecutor>();
            }
            so.FindProperty("_director").objectReferenceValue = director;
        });
        return hud;
    }

    // ───────── ボス（B：ウイルスがアプリを着ている。2026-11-17 決定） ─────────

    // 絵の配置（ボスの中心＝当たり判定の中心からのずれ、ドット）。体の絵 92×80 は、当たり判定 72×54 の中心が絵の (46, 47)
    const float BodyOffsetPx = 7f;    // 体・手の絵の中心は、当たり判定の中心より 7 ドット上
    const float WindowOffsetPx = -5f; // 着ているウィンドウは 5 ドット下
    const float FaceOffsetPx = 25f;   // 顔（目）は 25 ドット上（ウィンドウの上からのぞく）

    /// <summary>
    /// ボス B の見た目を作る：ウイルスの体（Chrome は浮く、VS Code は立つ）・抱えたウィンドウ（ひび3段階）・手・顔。
    /// 色は BossArmorVisual が全部受け持つ（BossController の「見た目は別の部品」をオンにすること）
    /// </summary>
    public static BossArmorVisual DecorateBossB(BossController boss, string bossName)
    {
        int layer = boss.gameObject.layer;
        bool browser = bossName.Contains("Chrome");
        var art = new GameObject("Art");
        art.layer = layer;
        art.transform.SetParent(boss.transform, false);

        Sprite[] bodyFrames = ArtAssets.LoadFrames(browser ? "Generated/Bosses/BossB_Body_Float.png" : "Generated/Bosses/BossB_Body_Stand.png");
        var body = AddArt(art.transform, "Body", null, new Vector3(0f, BodyOffsetPx / Ppu, 0f), 10, layer);
        Animate(body, bodyFrames, 6f);
        Sprite[] windowStages = ArtAssets.LoadFrames(browser ? "Generated/Bosses/BossB_Window_Browser.png" : "Generated/Bosses/BossB_Window_Editor.png");
        var window = AddArt(art.transform, "Window", windowStages.Length > 0 ? windowStages[0] : null, new Vector3(0f, WindowOffsetPx / Ppu, 0f), 12, layer);
        var hands = AddArt(art.transform, "Hands", ArtAssets.LoadSprite("Generated/Bosses/BossB_Hands.png"), new Vector3(0f, BodyOffsetPx / Ppu, 0f), 13, layer);
        Sprite[] faceFrames = ArtAssets.LoadFrames("Generated/Bosses/BossB_Face.png");
        var face = AddArt(art.transform, "Face", faceFrames.Length > 0 ? faceFrames[0] : null, new Vector3(0f, FaceOffsetPx / Ppu, 0f), 13, layer);

        var visual = art.AddComponent<BossArmorVisual>();
        PlayerTestSceneBuilder.SetProps(visual, so =>
        {
            so.FindProperty("_boss").objectReferenceValue = boss;
            so.FindProperty("_body").objectReferenceValue = body;
            so.FindProperty("_window").objectReferenceValue = window;
            so.FindProperty("_hands").objectReferenceValue = hands;
            so.FindProperty("_face").objectReferenceValue = face;
            SetSprites(so.FindProperty("_windowStages"), windowStages);
            SetSprites(so.FindProperty("_faceFrames"), faceFrames);
        });
        return visual;
    }

    static void SetSprites(SerializedProperty arr, Sprite[] sprites)
    {
        arr.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    // ───────── 小物 ─────────

    /// <summary>1ドットの白い絵を拡大して、色の付いた四角にする（盾のタイトルバー・耐久の棒など）</summary>
    public static SpriteRenderer AddPixelRect(Transform parent, string name, Vector3 localPos, Vector2 size, Color color, int order, int layer)
    {
        var sr = AddArt(parent, name, ArtAssets.LoadSprite("Generated/UI/Hud_Pixel.png"), localPos, order, layer);
        sr.transform.localScale = new Vector3(size.x * Ppu, size.y * Ppu, 1f);
        sr.color = color;
        return sr;
    }
}
