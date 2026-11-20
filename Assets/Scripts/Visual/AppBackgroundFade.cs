using UnityEngine;

/// <summary>
/// アプリの画面の背景（AppBackgrounds）を薄くする（2026-11-20：背景と足場の見分けがつきにくいため）。
/// 背景の SpriteRenderer のアルファを下げ、すぐ後ろ（並び順 −1）に置いた不透明な板の色を透かす。
/// ・後ろの板が無いと、カメラの背景色（アプリごとの Sky。暗い色が多い）が透けて暗くなるので、板を敷く（初期値は白＝明るく薄くなる）
/// ・薄くするのは、アプリの画面の背景を出しているときだけ。ウィンドウのステージの Mac のデスクトップの壁紙・今までの壁紙にはかけない
/// ・背景の絵が後から入れ替わっても（WindowStage がやり直しでアプリの絵に戻すなど）、毎フレーム見て合わせる
/// StageTheme.Apply が付ける。アルファと板の色は StageTheme のインスペクタで決める
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AppBackgroundFade : MonoBehaviour
{
    static Sprite _white;

    SpriteRenderer _bg;
    SpriteRenderer _backing;
    float _alpha = 0.4f;
    Color _backingColor = Color.white;

    /// <summary>背景のアルファ（1 で元のまま。小さいほど薄い）</summary>
    public float Alpha
    {
        get => _alpha;
        set => _alpha = Mathf.Clamp01(value);
    }

    /// <summary>背景の後ろに敷く板の色（透けて見える色。アルファは使わず、いつも不透明）</summary>
    public Color BackingColor
    {
        get => _backingColor;
        set
        {
            _backingColor = new Color(value.r, value.g, value.b, 1f);
            if (_backing != null) _backing.color = _backingColor;
        }
    }

    void Awake()
    {
        _bg = GetComponent<SpriteRenderer>();
    }

    void OnDisable() => SetFaded(false);

    void LateUpdate()
    {
        bool on = ShouldFade();
        SetFaded(on);
        if (!on) return;

        _backing.sortingLayerID = _bg.sortingLayerID;
        _backing.sortingOrder = _bg.sortingOrder - 1;
        Bounds b = _bg.bounds;
        _backing.transform.position = new Vector3(b.center.x, b.center.y, transform.position.z);
        Vector3 parent = transform.lossyScale;
        _backing.transform.localScale = new Vector3(
            b.size.x / (Mathf.Abs(parent.x) > 0.0001f ? parent.x : 1f),
            b.size.y / (Mathf.Abs(parent.y) > 0.0001f ? parent.y : 1f), 1f);
    }

    bool ShouldFade()
    {
        if (_bg == null || !_bg.enabled || _bg.sprite == null || _alpha >= 1f) return false;
        Sprite app = AppBackgrounds.Current;
        if (app == null || _bg.sprite != app) return false;
        return _bg.sprite != WindowStage.Wallpaper(); // Mac のデスクトップの壁紙にはかけない
    }

    void SetFaded(bool on)
    {
        if (_bg == null) return;
        if (on && _backing == null) _backing = MakeBacking();
        Color c = _bg.color;
        float a = on ? _alpha : 1f;
        if (!Mathf.Approximately(c.a, a)) _bg.color = new Color(c.r, c.g, c.b, a);
        if (_backing != null && _backing.enabled != on) _backing.enabled = on;
    }

    SpriteRenderer MakeBacking()
    {
        if (_white == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            _white = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            _white.name = "AppBackgroundBacking";
        }
        var go = new GameObject("BackgroundBacking");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _white;
        sr.color = _backingColor;
        return sr;
    }
}
