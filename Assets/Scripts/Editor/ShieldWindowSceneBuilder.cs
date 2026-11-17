using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仮】盾ウィンドウ（ShieldWindow）をテストシーンに置く部品。雑魚戦・ボス戦のテストシーン作成ツールから呼ぶ。
/// 2026-11-16 にまさぴ〜が選んだ形。チームの合意はまだ。大きさ・置く場所・色はすべて【仮】。
/// 見た目は、ボスと同じウィンドウの枠の絵（半透明）に、タイトルバーと耐久の棒。デザインが来たら、子の SpriteRenderer の絵を差し替える。
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
    static readonly Color TitleBarColor = new Color32(0x42, 0x9f, 0xec, 0xe6); // 背景の空と同じ青
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

        // 見た目：半透明のウィンドウ（ボスと同じ枠の絵）に、青いタイトルバーと耐久の棒。
        // 並び順は弾（20）より奥、プレイヤー・ボスより手前（半透明なので、すり抜けるのが見える）
        float w = WindowSize.x, h = WindowSize.y;
        float px = 1f / ArtLib.Ppu;
        float titleY = h * 0.5f - 4f * px - TitleBarHeight * 0.5f;
        var body = ArtLib.AddSized(visual, "Body", ArtAssets.LoadSprite("Generated/UI/Win_Frame.png"), Vector3.zero, WindowSize, 16, layer);
        body.color = new Color(1f, 1f, 1f, 0.55f);
        var titleBar = ArtLib.AddPixelRect(visual, "TitleBar", new Vector3(0f, titleY, 0f), new Vector2(w - 8f * px, TitleBarHeight), TitleBarColor, 17, layer);
        var barBack = ArtLib.AddPixelRect(visual, "DurabilityBack", new Vector3(w * 0.5f - 0.6f, titleY, 0f), new Vector2(0.9f, 0.12f), BarBackColor, 18, layer);
        var bar = ArtLib.AddPixelRect(visual, "Durability", new Vector3(w * 0.5f - 0.6f, titleY, 0f), new Vector2(0.9f, 0.12f), BarColor, 19, layer);
        var renderers = new[] { body, titleBar, barBack, bar };

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
}
