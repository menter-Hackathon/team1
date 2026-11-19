using UnityEngine;

/// <summary>連鎖数を頭の斜め後ろへ表示。頭上中央はスキルのアプリアイコン用に空ける。</summary>
public class ChainBadge : MonoBehaviour
{
    const float Px = 1f / 18f;
    const float PopTime = 0.22f;
    readonly TextMesh[] _outline = new TextMesh[4];
    Transform _root;
    TextMesh _text;
    SpriteRenderer _timer;
    Color _color;
    int _level;
    float _nextSpark;
    float _remaining = 1f, _popAge = 1f, _fadeAge = -1f;
    bool _visible, _burstPending;

    public static ChainBadge Create(Transform parent, SkinId skin, AppTheme theme, Sprite icon)
    {
        var go = new GameObject("ChainCounter");
        go.transform.SetParent(parent, false);
        var b = go.AddComponent<ChainBadge>();
        b._color = Color.Lerp(FxKit.MainOf(theme, Color.cyan), Color.white, 0.5f);
        b._color.a = 1f;
        b._root = new GameObject("Counter").transform;
        b._root.SetParent(go.transform, false);
        for (int i = 0; i < b._outline.Length; i++)
        {
            var shadow = WorldText.Create(b._root, "", 0.36f, Color.black, RewardKit.OrderBadge, false);
            shadow.transform.localPosition = new Vector2(i < 2 ? (i == 0 ? -Px : Px) : 0f,
                i >= 2 ? (i == 2 ? -Px : Px) : 0f);
            b._outline[i] = shadow;
        }
        b._text = WorldText.Create(b._root, "", 0.36f, b._color, RewardKit.OrderBadge + 1, false);
        b._text.fontStyle = FontStyle.Bold;
        foreach (var shadow in b._outline) shadow.fontStyle = FontStyle.Bold;
        b._timer = RuntimeSprites.Show(b._root, FxArt.Square(1), new Vector2(0f, -0.25f),
            RewardKit.OrderBadge + 1, 1f, "ChainTime");
        b._root.gameObject.SetActive(false);
        return b;
    }

    public void Bump(int count, int tier)
    {
        if (count < 2) return;
        string label = "×" + Mathf.Min(count, 999);
        _text.text = label;
        foreach (var shadow in _outline) shadow.text = label;
        _level = Mathf.Clamp(count - 2, 0, 3);
        _nextSpark = Time.time + 0.35f;
        _remaining = 1f;
        _popAge = 0f;
        _fadeAge = -1f;
        _visible = true;
        _burstPending = true;
        _root.gameObject.SetActive(true);
    }

    public void Break()
    {
        if (!_visible) return;
        _visible = false;
        _burstPending = false;
        _fadeAge = 0f;
    }

    public void Follow(Transform player, int count, float timeLeft01)
    {
        if (player == null) { _root.gameObject.SetActive(false); return; }
        var controller = player.GetComponent<PlayerController>();
        int facing = controller != null ? controller.FacingDirection : 1;
        Vector2 head = FxKit.HeadOf(player.gameObject, (Vector2)player.position + Vector2.up);
        // 顔の後ろ側へ寄せ、頭上中央のスキルアイコンと離す。文字自体は左右反転しない。
        Vector2 pos = FxKit.ClampToView(head + new Vector2(-facing * 1.05f, 0.12f), 1f);
        transform.position = new Vector3(Mathf.Round(pos.x / Px) * Px, Mathf.Round(pos.y / Px) * Px, 0f);
        if (_visible) _remaining = timeLeft01;
        if (_burstPending)
        {
            _burstPending = false;
            int n = 2 + _level * 3;
            for (int i = 0; i < n; i++)
            {
                float angle = (i * 360f / n + 25f) * Mathf.Deg2Rad;
                Vector2 off = new Vector2(Mathf.Cos(angle) * (0.36f + _level * 0.07f), Mathf.Sin(angle) * 0.25f);
                Sprite sprite = _level >= 2 && i % 2 == 0 ? FxArt.Twinkle : FxArt.Square(2);
                FxAnim.Sprite(sprite, pos + off, RewardKit.OrderBadge + 2, 0.25f + _level * 0.06f,
                    i % 3 == 0 ? Color.white : ChainColor(), 0.5f + _level * 0.08f, "ChainSpark")
                    .Pop().Rise(0.15f + _level * 0.12f).FadeFrom(0.35f);
            }
            if (_level >= 2) FxKit.Ring(pos, _level == 3 ? 1.45f : 0.95f, 0.25f, ChainColor());
            if (_level == 3) FxKit.Ring(pos, 1.1f, 0.3f, Color.white, 0.08f);
        }
    }

    void Update()
    {
        if (_root == null || !_root.gameObject.activeSelf) return;
        _popAge += Time.deltaTime;
        float fade = 1f;
        if (_fadeAge >= 0f)
        {
            _fadeAge += Time.deltaTime;
            fade = 1f - Mathf.Clamp01(_fadeAge / 0.25f);
            if (fade <= 0f) { _root.gameObject.SetActive(false); return; }
        }
        float p = Mathf.Clamp01(_popAge / PopTime);
        float bounce = Mathf.Sin(p * Mathf.PI);
        float size = (1f + _level * 0.025f) * (1f + bounce * (0.16f + _level * 0.07f));
        _root.localScale = Vector3.one * size;
        _root.localPosition = Vector3.up * (bounce * 0.15f + (1f - fade) * 0.25f);
        // ×5以降はゆっくり傾いて光る。画面全体の点滅は使わない。
        float tilt = _level == 3 ? Mathf.Sin(_popAge * 6f) * 4f : bounce * (_level % 2 == 0 ? -3f : 3f);
        _root.localRotation = Quaternion.Euler(0f, 0f, tilt);
        if (_visible && _level == 3 && Time.time >= _nextSpark)
        {
            _nextSpark = Time.time + 0.4f;
            Vector2 off = new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(-0.15f, 0.2f));
            FxAnim.Sprite(FxArt.Twinkle, (Vector2)transform.position + off, RewardKit.OrderBadge + 2,
                0.3f, ChainColor(), 0.45f, "ChainEmber").Pop().Rise(0.25f).FadeFrom(0.3f);
        }
        Color color = ChainColor();
        color = Color.Lerp(color, Color.white, bounce * 0.75f);
        color.a = fade;
        WorldText.SetColor(_text, color);
        foreach (var shadow in _outline) WorldText.SetColor(shadow, new Color(0.04f, 0.04f, 0.08f, fade));
        _timer.color = color;
        float width = 0.6f * _remaining;
        _timer.transform.localScale = new Vector3(Mathf.Max(0.001f, width / Px), 1f, 1f);
        _timer.transform.localPosition = new Vector3(-0.3f + width * 0.5f, -0.25f, 0f);
    }

    Color ChainColor()
    {
        switch (_level)
        {
            case 1: return new Color(1f, 0.87f, 0.2f); // ×3：黄
            case 2: return new Color(1f, 0.48f, 0.12f); // ×4：オレンジ
            case 3: return new Color(1f, 0.25f, 0.55f); // ×5〜：ピンク、二重リングと残光
            default: return new Color(0.35f, 0.9f, 1f); // ×2：水色
        }
    }

}
