using UnityEngine;
using UnityEngine.SceneManagement; // Scene

/// <summary>
/// 【仮】乗れる足場を「厚みのある板」に見せる（2026-11-20。まさぴ〜の依頼：背景と足場の見分けがつきにくい）。
/// 背景（アプリの画面の絵）は AppBackgroundTone で少し沈め、乗れる足場だけにこの板の形を付ける。背景の中の同じ形の飾り（板の形が無い）は乗れないと分かる。
/// 足場の絵の四角のまわりに、箱を4枚足す（数はどれも【仮】。1ドット＝背景の絵の 1 ドット＝1/15 マス）：
///   外光：白 うすく（α0.18。リニアの色空間で、絵の色どうしの α0.27 くらいに見える）。左右 2・下 4 ドット広げる。暗いアプリでも形が抜ける
///   縁：濃い紫 #1E0830。左右 1・下 3 ドット広げる
///   厚み：紫 #6A2A8C。絵のすぐ下に 2 ドット
///   上の白い線：白。絵のいちばん上の 1 ドット（乗る面。上へは広げないので、キャラクターの足とそろう）
/// ・重なる順番：外光・縁・厚みは絵より後ろ（絵 2 なら −2・−1・0。どの足場でも同じなので、隣の足場の外光が縁にかぶらない）、上の白い線は絵の手前（3）
/// ・乗れない間（当たり判定が切れている：blink の消えている間・fall の落ちたあと・ほかの決まりが切った間）は、全部消す（「板＝乗れる」）
/// ・絵の濃さ（点滅・うすくなる）と、絵の位置（fall の揺れ）に毎フレームついていく。足場が動けば、子なのでいっしょに動く
/// ・付けるもの：アプリの UI の足場（AppStagePlatforms。ボタンには付けない）と、シーンに置いたフォルダ・ウィンドウの足場（DressSceneBlocks）。
///   ウィンドウのステージ（Mac のウィンドウ）・キットのギミックが出す足場には付けない
/// ・止めるときは Enabled = false
/// </summary>
public class PlatformSlab : MonoBehaviour
{
    /// <summary>【仮】false にすると、板の形を付けない（今までの足場の見た目）</summary>
    public static bool Enabled = true;

    const float Px = 1f / 15f;
    static readonly Color Halo = new Color(1f, 1f, 1f, 0.18f);
    static readonly Color Frame = new Color32(0x1e, 0x08, 0x30, 0xff);
    static readonly Color Under = new Color32(0x6a, 0x2a, 0x8c, 0xff);
    static readonly Color Lip = Color.white;

    SpriteRenderer _art;
    Collider2D _col;
    SpriteRenderer[] _parts;
    float[] _alphas;
    Vector3 _offset;
    float _shown = -1f;

    /// <summary>足場の絵 art のまわりに板の形を付ける（col：乗れるかを見る当たり判定。無ければいつも乗れるとみなす）</summary>
    public static PlatformSlab Attach(SpriteRenderer art, Collider2D col)
    {
        if (!Enabled || art == null || art.sprite == null) return null;
        Transform host = art.transform.parent;
        if (host == null) return null;
        Bounds b = art.bounds;
        if (b.size.x < Px * 2f || b.size.y < Px) return null;

        var go = new GameObject("Slab"); // "Art" ではない名前（ワーム・巣は子の "Art" を探す）
        go.layer = art.gameObject.layer;
        go.transform.SetParent(host, false);
        Vector3 s = host.lossyScale;
        go.transform.localScale = new Vector3(Inverse(s.x), Inverse(s.y), 1f); // 親の拡大を打ち消して、子の大きさをマスで書けるようにする
        go.transform.position = new Vector3(b.center.x, b.center.y, art.transform.position.z);

        var slab = go.AddComponent<PlatformSlab>();
        slab._art = art;
        slab._col = col;
        slab._offset = go.transform.position - art.transform.position;
        float w = b.size.x, h = b.size.y;
        int order = art.sortingOrder;
        slab._parts = new[]
        {
            slab.Part(new Vector2(0f, -2f * Px), new Vector2(w + 4f * Px, h + 4f * Px), Halo, order - 4, "Halo"),
            slab.Part(new Vector2(0f, -1.5f * Px), new Vector2(w + 2f * Px, h + 3f * Px), Frame, order - 3, "Frame"),
            slab.Part(new Vector2(0f, -h * 0.5f - 1f * Px), new Vector2(w, 2f * Px), Under, order - 2, "Under"),
            slab.Part(new Vector2(0f, h * 0.5f - 0.5f * Px), new Vector2(w, 1f * Px), Lip, order + 1, "Lip"),
        };
        slab._alphas = new float[slab._parts.Length];
        for (int i = 0; i < slab._parts.Length; i++) slab._alphas[i] = slab._parts[i].color.a;
        slab.Sync();
        return slab;
    }

    /// <summary>
    /// シーンに置いたフォルダ・ウィンドウの足場（StageLayoutsBuilder の Folder・BlinkingWindow。絵の名前が "Folder"・"Window"）に板の形を付ける。
    /// StageDirector の Awake で、アプリの足場を作ったあとに呼ぶ（かくしたフォルダは数えない）。付けた数を返す
    /// </summary>
    public static int DressSceneBlocks(Scene scene)
    {
        if (!Enabled) return 0;
        int layer = LayerMask.NameToLayer("OneWayPlatform");
        if (layer < 0) return 0;
        int count = 0;
        foreach (BoxCollider2D col in FindObjectsOfType<BoxCollider2D>())
        {
            if (col == null || col.isTrigger || col.gameObject.layer != layer || col.gameObject.scene != scene) continue;
            if (col.GetComponent<AppPlatform>() != null || col.GetComponentInChildren<PlatformSlab>(true) != null) continue;
            foreach (SpriteRenderer sr in col.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.gameObject.name != "Folder" && sr.gameObject.name != "Window") continue;
                if (Attach(sr, col) != null) count++;
                break;
            }
        }
        return count;
    }

    SpriteRenderer Part(Vector2 localPos, Vector2 size, Color color, int order, string objectName)
    {
        SpriteRenderer sr = FxKit.Box(transform, localPos, size, color, order, objectName);
        sr.sortingLayerID = _art.sortingLayerID;
        return sr;
    }

    void LateUpdate()
    {
        if (_art == null)
        {
            Destroy(gameObject);
            return;
        }
        Sync();
    }

    void Sync()
    {
        transform.position = _art.transform.position + _offset; // fall の揺れ・落ちにもついていく
        bool solid = _col == null || (_col.enabled && _col.gameObject.activeInHierarchy);
        float a = solid && _art.enabled && _art.gameObject.activeInHierarchy ? _art.color.a : 0f;
        if (Mathf.Abs(a - _shown) < 0.002f) return;
        _shown = a;
        for (int i = 0; i < _parts.Length; i++)
        {
            SpriteRenderer p = _parts[i];
            if (p == null) continue;
            p.enabled = a > 0.001f;
            Color c = p.color;
            c.a = _alphas[i] * a;
            p.color = c;
        }
    }

    static float Inverse(float v) => Mathf.Abs(v) > 0.0001f ? 1f / v : 1f;
}
