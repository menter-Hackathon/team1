using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】盾ウィンドウ（ShieldWindow）をテストシーンに置く部品。雑魚戦・ボス戦のテストシーン作成ツールから呼ぶ。
/// 2026-11-16 にまさぴ〜が選んだ形。チームの合意はまだ。大きさ・置く場所・色はすべて【仮】。
/// 見た目は四角の絵を並べた仮のもの（枠・タイトルバー・耐久の棒）。デザインが来たら、子の SpriteRenderer の絵を差し替える。
/// </summary>
public static class ShieldWindowSceneBuilder
{
    /// <summary>レイヤー名。プレイヤーの移動・瞬間移動の壁止めには入れないこと</summary>
    public const string LayerName = "BulletBlocker";

    /// <summary>【仮】ウィンドウの大きさ（タイトルバーを含む）</summary>
    static readonly Vector2 WindowSize = new Vector2(2.0f, 1.5f);
    /// <summary>【仮】最初に置く場所（部屋の左上。ボスの移動先・タブの列とは重ならない）</summary>
    public static readonly Vector2 DefaultPosition = new Vector2(-7.5f, 7.5f);

    const float TitleBarHeight = 0.3f;
    const float FrameThickness = 0.08f;
    static readonly Color BodyColor = new Color(0.75f, 0.88f, 1.00f, 0.28f);
    static readonly Color FrameColor = new Color(0.85f, 0.92f, 1.00f, 0.95f);
    static readonly Color TitleBarColor = new Color(0.25f, 0.45f, 0.85f, 0.95f);
    static readonly Color BarBackColor = new Color(0.05f, 0.08f, 0.15f, 0.9f);
    static readonly Color BarColor = new Color(0.40f, 1.00f, 0.55f, 1.00f);

    /// <summary>BulletBlocker レイヤーがあるか（無ければエラーを出す）</summary>
    public static bool CheckLayer()
    {
        if (LayerMask.NameToLayer(LayerName) >= 0) return true;
        Debug.LogError($"レイヤー {LayerName} が見つかりません。Project Settings > Tags and Layers を確認してください。");
        return false;
    }

    /// <summary>敵の弾を止めるレイヤーのマスク（敵の AttackRunner の「弾だけを止める物」に入れる）</summary>
    public static int BlockerMask
    {
        get
        {
            int layer = LayerMask.NameToLayer(LayerName);
            return layer >= 0 ? 1 << layer : 0;
        }
    }

    /// <summary>盾ウィンドウを1枚作る（原点＝ウィンドウの中心）</summary>
    public static ShieldWindow Create(Sprite square, Vector2 position)
    {
        int layer = LayerMask.NameToLayer(LayerName);
        var go = new GameObject("ShieldWindow");
        go.layer = layer;
        go.transform.position = position;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = false;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = WindowSize;
        box.isTrigger = false; // 弾の地形判定はトリガーを含めないので、トリガーにしない

        var visual = new GameObject("Visual").transform;
        visual.gameObject.layer = layer;
        visual.SetParent(go.transform, false);

        float w = WindowSize.x, h = WindowSize.y;
        float titleY = h * 0.5f - TitleBarHeight * 0.5f;
        var renderers = new[]
        {
            Part("Body", visual, Vector2.zero, WindowSize, BodyColor, square, 5, layer),
            Part("TitleBar", visual, new Vector2(0f, titleY), new Vector2(w, TitleBarHeight), TitleBarColor, square, 6, layer),
            Part("Frame_Top", visual, new Vector2(0f, h * 0.5f - FrameThickness * 0.5f), new Vector2(w, FrameThickness), FrameColor, square, 7, layer),
            Part("Frame_Bottom", visual, new Vector2(0f, -h * 0.5f + FrameThickness * 0.5f), new Vector2(w, FrameThickness), FrameColor, square, 7, layer),
            Part("Frame_Left", visual, new Vector2(-w * 0.5f + FrameThickness * 0.5f, 0f), new Vector2(FrameThickness, h), FrameColor, square, 7, layer),
            Part("Frame_Right", visual, new Vector2(w * 0.5f - FrameThickness * 0.5f, 0f), new Vector2(FrameThickness, h), FrameColor, square, 7, layer),
            Part("DurabilityBack", visual, new Vector2(w * 0.5f - 0.6f, titleY), new Vector2(0.9f, 0.12f), BarBackColor, square, 8, layer),
            Part("Durability", visual, new Vector2(w * 0.5f - 0.6f, titleY), new Vector2(0.9f, 0.12f), BarColor, square, 9, layer),
        };

        var shield = go.AddComponent<ShieldWindow>();
        PlayerTestSceneBuilder.SetProps(shield, so =>
        {
            var arr = so.FindProperty("_renderers");
            arr.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.FindProperty("_durabilityBar").objectReferenceValue = renderers[renderers.Length - 1];
        });
        return shield;
    }

    /// <summary>四角の絵を1枚作る。弾（並び順20）より奥、プレイヤー・ボス（10〜12）より手前に描く（半透明なので、すり抜けるのが見える）</summary>
    static SpriteRenderer Part(string name, Transform parent, Vector2 localPos, Vector2 size, Color color, Sprite square, int order, int layer)
    {
        var sr = PlayerTestSceneBuilder.CreateSprite(name, parent, localPos, size, color, square, order); // 並び順は 10 + order
        sr.gameObject.layer = layer;
        return sr;
    }
}
