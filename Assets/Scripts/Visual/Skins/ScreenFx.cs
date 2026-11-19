using UnityEngine;

/// <summary>
/// 【仮】画面全体にかける絵（2026-11-18。docs/app-skill-visuals-design.md 1-2）。カメラの子（ゲームの世界の絵）で出す。自動の撮影に写る。
/// ・画面のふちを赤く（被弾：幅 0.5マス、3割、0.15秒）
/// ・画面全体を白く（とどめ：4割、0.06秒）
/// 重なる順番は 80（いちばん手前）
/// </summary>
public static class ScreenFx
{
    /// <summary>画面のふちを赤くする</summary>
    public static void EdgeRed(float width = 0.5f, float alpha = 0.3f, float seconds = 0.15f)
    {
        Transform root = FxKit.CameraChild("ScreenFx_EdgeRed");
        if (root == null) return;
        Vector2 v = FxKit.ViewSize();
        Color c = FxKit.EnemyRed;
        c.a = alpha;
        float hw = v.x * 0.5f, hh = v.y * 0.5f;
        // 外側を濃く、内側をうすく（2段）
        for (int step = 0; step < 2; step++)
        {
            float w = width * (step == 0 ? 0.5f : 1f);
            Color cc = c;
            cc.a *= step == 0 ? 1f : 0.5f;
            FxKit.Box(root, new Vector2(0f, hh - w * 0.5f), new Vector2(v.x, w), cc, FxKit.OrderScreen);
            FxKit.Box(root, new Vector2(0f, -hh + w * 0.5f), new Vector2(v.x, w), cc, FxKit.OrderScreen);
            FxKit.Box(root, new Vector2(-hw + w * 0.5f, 0f), new Vector2(w, v.y - w * 2f), cc, FxKit.OrderScreen);
            FxKit.Box(root, new Vector2(hw - w * 0.5f, 0f), new Vector2(w, v.y - w * 2f), cc, FxKit.OrderScreen);
        }
        Animate(root, seconds);
    }

    /// <summary>画面全体を白くする</summary>
    public static void WhiteFlash(float alpha = 0.4f, float seconds = 0.06f)
    {
        Transform root = FxKit.CameraChild("ScreenFx_White");
        if (root == null) return;
        Vector2 v = FxKit.ViewSize();
        FxKit.Box(root, Vector2.zero, v + Vector2.one, new Color(1f, 1f, 1f, alpha), FxKit.OrderScreen);
        Animate(root, seconds);
    }

    static void Animate(Transform root, float seconds)
    {
        // カメラの子なので、動かさずにうすくするだけ
        root.gameObject.AddComponent<ScreenFxLife>().Seconds = seconds;
    }
}

/// <summary>【仮】カメラの子の絵を、時間でうすくして消す（FxAnim の寿命の代わり）</summary>
public class ScreenFxLife : MonoBehaviour
{
    public float Seconds = 0.1f;
    float _age;
    SpriteRenderer[] _srs;
    float[] _alpha;
    TextMesh[] _tms;
    Color[] _tmBase;

    void Start()
    {
        _srs = GetComponentsInChildren<SpriteRenderer>();
        _alpha = new float[_srs.Length];
        for (int i = 0; i < _srs.Length; i++) _alpha[i] = _srs[i].color.a;
        _tms = GetComponentsInChildren<TextMesh>();
        _tmBase = new Color[_tms.Length];
        for (int i = 0; i < _tms.Length; i++) _tmBase[i] = FxKit.TextColor(_tms[i]);
    }

    void Update()
    {
        _age += Time.deltaTime;
        float t = Mathf.Clamp01(_age / Mathf.Max(0.01f, Seconds));
        float k = t < 0.35f ? 1f : 1f - (t - 0.35f) / 0.65f;
        for (int i = 0; i < _srs.Length; i++)
        {
            if (_srs[i] == null) continue;
            Color c = _srs[i].color;
            c.a = _alpha[i] * k;
            _srs[i].color = c;
        }
        if (k < 1f)
        {
            for (int i = 0; i < _tms.Length; i++)
            {
                if (_tms[i] == null) continue;
                Color c = _tmBase[i];
                c.a *= k;
                WorldText.SetColor(_tms[i], c);
            }
        }
        if (t >= 1f) Destroy(gameObject);
    }
}
