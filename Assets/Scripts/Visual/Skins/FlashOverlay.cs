using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】相手の絵を、1色にして重ねる（当たったときの光り方。2026-11-18。docs/app-skill-visuals-design.md 1-3）。
/// ・元の絵（SpriteRenderer）の子に、同じ絵を「形だけ・1色」で描く物を重ねる。元の絵はさわらない（点滅・色を変える部品とぶつからない）
/// ・1色にするのは、文字（TextMesh）の材質のシェーダー（絵の形＝アルファだけを使い、色は頂点の色）。無ければ何もしない
/// ・ボスは、着ているウィンドウだけ（煙と顔は白くしない）。プレイヤーは 赤 → 白
/// 時間は Time.deltaTime（ヒットストップの間は止まって見える）
/// </summary>
public class FlashOverlay : MonoBehaviour
{
    static Material _material;
    static bool _materialTried;

    SpriteRenderer _source;
    SpriteRenderer _overlay;
    Color _first, _second;
    float _time, _age;

    /// <summary>1色の絵の材質（無ければ null）</summary>
    static Material SilhouetteMaterial
    {
        get
        {
            if (_material != null || _materialTried) return _material;
            _materialTried = true;
            Shader shader = null;
            Font f = PixelGui.FontEn;
            if (f != null && f.material != null) shader = f.material.shader;
            if (shader == null) shader = Shader.Find("GUI/Text Shader");
            if (shader == null)
            {
                Font b = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (b != null && b.material != null) shader = b.material.shader;
            }
            if (shader == null) return null;
            _material = new Material(shader) { name = "FxSilhouette", hideFlags = HideFlags.DontSave };
            return _material;
        }
    }

    /// <summary>1色の重ね絵が使えるか（使えないとき、BossArmorVisual は今までの光り方に戻す）</summary>
    public static bool Available => SilhouetteMaterial != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _material = null;
        _materialTried = false;
    }

    /// <summary>
    /// 相手 target を光らせる（seconds 秒。前半 first、後半 second の色）。
    /// ボス（BossArmorVisual）は着ているウィンドウだけ。ほかは、見えている子の絵ぜんぶ
    /// </summary>
    public static void Play(GameObject target, Color first, Color second, float seconds)
    {
        if (target == null || seconds <= 0f || SilhouetteMaterial == null) return;
        foreach (SpriteRenderer sr in RenderersOf(target)) PlayOn(sr, first, second, seconds);
    }

    /// <summary>絵1枚を光らせる</summary>
    public static void PlayOn(SpriteRenderer source, Color first, Color second, float seconds)
    {
        if (source == null || SilhouetteMaterial == null) return;
        // 同じ絵に重ねている物があれば、時間をのばす
        foreach (Transform child in source.transform)
        {
            if (child.TryGetComponent(out FlashOverlay existing) && existing._source == source)
            {
                existing._first = first;
                existing._second = second;
                existing._time = Mathf.Max(existing._time - existing._age, seconds);
                existing._age = 0f;
                return;
            }
        }
        var go = new GameObject("FlashOverlay");
        go.layer = source.gameObject.layer;
        go.transform.SetParent(source.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.01f); // 同じ並び順のまま、少し手前
        var fo = go.AddComponent<FlashOverlay>();
        fo._source = source;
        fo._overlay = go.AddComponent<SpriteRenderer>();
        fo._overlay.sharedMaterial = SilhouetteMaterial;
        fo._first = first;
        fo._second = second;
        fo._time = seconds;
        fo.Sync();
    }

    /// <summary>光らせる絵を選ぶ</summary>
    static IEnumerable<SpriteRenderer> RenderersOf(GameObject target)
    {
        var armor = target.GetComponentInChildren<BossArmorVisual>();
        if (armor != null)
        {
            if (armor.Window != null) yield return armor.Window;
            yield break;
        }
        foreach (var sr in target.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr == null || sr.sprite == null || !sr.enabled || sr.forceRenderingOff) continue;
            if (sr.GetComponent<FlashOverlay>() != null) continue;
            if (sr.sortingOrder >= 40) continue; // 頭の上の印・エフェクトは光らせない
            yield return sr;
        }
    }

    void LateUpdate()
    {
        _age += Time.deltaTime;
        if (_source == null || _age >= _time)
        {
            Destroy(gameObject);
            return;
        }
        Sync();
    }

    void Sync()
    {
        if (_overlay == null || _source == null) return;
        _overlay.sprite = _source.sprite;
        _overlay.flipX = _source.flipX;
        _overlay.flipY = _source.flipY;
        _overlay.drawMode = _source.drawMode;
        if (_source.drawMode != SpriteDrawMode.Simple) _overlay.size = _source.size;
        _overlay.sortingLayerID = _source.sortingLayerID;
        _overlay.sortingOrder = _source.sortingOrder;
        _overlay.enabled = _source.sprite != null && !_source.forceRenderingOff && _source.color.a > 0.01f;
        Color c = _age < _time * 0.5f ? _first : _second;
        _overlay.color = c; // SpriteRenderer の色は、リニアの色空間でも Unity が直して渡す
    }
}
