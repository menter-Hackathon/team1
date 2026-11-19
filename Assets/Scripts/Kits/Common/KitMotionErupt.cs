using UnityEngine;

/// <summary>
/// 【仮】下からせり上がる物（KitMotion.Erupt が作る。2026-11-19）。担当0 の物。
/// ・予告：床に光る割れ目（点滅）＋危ない範囲の赤い帯（PaceZoneMark）。当たらない物（damage 0）は色の帯
/// ・本体は床の下に隠れていて（マスクで床より上だけ見せる）、rise 秒でせり上がり、hold 秒止まって、rise 秒で引っこむ
/// ・当たるのは、せり上がりと止まっている間（引っこむ間は当たらない）
/// </summary>
public class KitMotionErupt : KitMotionItem
{
    float _height, _rise, _hold;
    bool _risen, _sinking;

    /// <summary>床の高さ</summary>
    public float SurfaceY { get; private set; }
    /// <summary>出ている（せり上がり〜止まっている）間か</summary>
    public bool IsUp => _risen && !_sinking && !IsDone;

    internal void Setup(float x, float width, float height, float surface, float warn, float rise, float hold, Sprite sprite, Color color, int damage, Color warnColor)
    {
        _height = height;
        _rise = rise;
        _hold = hold;
        SurfaceY = surface;
        Warn = warn;
        transform.position = new Vector3(x, surface, 0f);
        bool harm = damage > 0;

        // 予告：危ない範囲の帯と、床の光る割れ目
        if (harm) KitMotion.WarnBand(transform, x - width * 0.5f, x + width * 0.5f, surface, height, warn, rise + hold);
        else KitMotionMark.Create(transform, PlaceholderSprites.Square, new Vector2(x, surface + height * 0.5f), new Vector2(width, height), 0f,
            new Color(warnColor.r, warnColor.g, warnColor.b, 0.18f), OrderZone, warn, "Band");
        Color glow = harm ? Color.Lerp(KitMotion.Red, new Color(1f, 0.85f, 0.3f), 0.35f) : warnColor;
        KitMotionMark.Create(transform, PlaceholderSprites.Square, new Vector2(x, surface + KitDress.Px), new Vector2(width, KitDress.Px * 2f), 0f, glow, OrderCrack, warn, "CrackLine");
        float tile = 12f * KitDress.Px;
        int n = Mathf.Max(1, Mathf.RoundToInt(width / tile));
        float step = width / n;
        for (int i = 0; i < n; i++)
        {
            float cx = x - width * 0.5f + step * (i + 0.5f);
            KitMotionMark crack = KitMotionMark.Create(transform, KitMotionArt.Crack, new Vector2(cx, surface), new Vector2(step, 5f * KitDress.Px), 0f, glow, OrderCrack, warn, "Crack");
            if (crack.Renderer != null) crack.Renderer.flipX = i % 2 == 1;
        }

        // 本体（床の下に隠す。床より上だけ見えるマスク）
        MakeBody(sprite, color, new Vector2(width, height), false, damage, 0.85f);
        // 判定の幅は、両はしを 0.15 ずつだけ引く（広い柱で、赤い帯のふちに当たらない所ができないように）
        if (Body.TryGetComponent(out BoxCollider2D hit)) hit.size = new Vector2(Mathf.Max(0.1f, width - Mathf.Min(0.3f, width * 0.3f)), height * 0.9f);
        Body.localPosition = new Vector3(0f, -height * 0.5f, 0f);
        Visual.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        Visual.enabled = false;
        var maskGo = new GameObject("Reveal");
        maskGo.layer = gameObject.layer;
        maskGo.transform.SetParent(transform, false);
        maskGo.transform.localPosition = new Vector3(0f, height, 0f);
        var mask = maskGo.AddComponent<SpriteMask>();
        mask.sprite = PlaceholderSprites.Square;
        Vector2 native = mask.sprite.bounds.size;
        maskGo.transform.localScale = new Vector3((width + 0.4f) / Mathf.Max(native.x, 0.001f), height * 2f / Mathf.Max(native.y, 0.001f), 1f);
    }

    protected override bool Step(float age)
    {
        if (Body == null) return false;
        if (age < Warn) return true;
        float t = age - Warn;
        if (!_risen)
        {
            _risen = true;
            if (Visual != null) Visual.enabled = true;
            SetHarm(true);
            GameAudio.Event("zone_fire");
        }
        float low = -_height * 0.5f, high = _height * 0.5f, y;
        if (t < _rise)
        {
            float k = t / _rise;
            y = Mathf.Lerp(low, high, 1f - (1f - k) * (1f - k));
        }
        else if (t < _rise + _hold) y = high;
        else if (t < _rise * 2f + _hold)
        {
            if (!_sinking)
            {
                _sinking = true;
                SetHarm(false);
            }
            y = Mathf.Lerp(high, low, (t - _rise - _hold) / _rise);
        }
        else return false;
        Body.localPosition = new Vector3(0f, y, 0f);
        return true;
    }
}
