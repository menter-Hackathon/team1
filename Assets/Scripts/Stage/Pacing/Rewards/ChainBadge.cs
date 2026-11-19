using UnityEngine;

/// <summary>
/// 【仮】連続キルのバッジ（2026-11-19。docs/stage-pacing-design.md 4-1。担当3）。プレイヤーの頭の上に出す。
/// ・白いふち・黄色の小さい札。左にそのアプリのアイコン、右に飾りの数字（x3。【要相談⑤】は「出す」のまま）
/// ・札の形はアプリらしく（RewardKit.BadgeShapeOf：Chat は吹き出し、Browser はタブ、Dev・Terminal は角ばった窓、Game は角を落とした札）
/// ・下の細い線（アプリの色）が、切れるまでの残りの時間（説明の文字は出さない）
/// ・つながるたびにポンとはねる。段階が上がると1段大きくなる（3〜4：1.15倍、5〜：1.3倍＋きらめき）。切れると、しぼんで消える
/// 2体目から出す（1体目は出さない）。KillChain が毎フレーム Follow を呼ぶ
/// </summary>
public class ChainBadge : MonoBehaviour
{
    const float Px = 1f / 18f;
    const float TextHeight = 0.4f;
    const int BodyPx = 13;
    const int Pad = 3;
    const int IconPx = 9;
    const float HeadGap = 0.6f;
    const float PopTime = 0.15f;
    const float ShrinkTime = 0.2f;

    AppTheme _theme;
    Sprite _icon;
    BadgeShape _shape;
    Transform _root;
    TextMesh _text;
    SpriteRenderer _timer;
    float _timerLeft;
    float _timerWidth;
    int _builtLen = -1;
    int _tier;
    bool _visible;
    float _popAge = 1f;
    float _shrinkAge = -1f;
    float _nextTwinkle;

    /// <summary>バッジを作る（最初は出さない）</summary>
    public static ChainBadge Create(Transform parent, SkinId skin, AppTheme theme, Sprite icon)
    {
        var go = new GameObject("ChainBadge");
        go.transform.SetParent(parent, false);
        var b = go.AddComponent<ChainBadge>();
        b._theme = theme;
        b._icon = icon;
        b._shape = RewardKit.BadgeShapeOf(skin);
        b._root = new GameObject("Root").transform;
        b._root.SetParent(go.transform, false);
        b._root.gameObject.SetActive(false);
        return b;
    }

    /// <summary>つながった（2体目から出す）</summary>
    public void Bump(int count, int tier)
    {
        if (count < 2) return;
        string label = "x" + Mathf.Min(count, 999);
        if (label.Length != _builtLen) Build(label);
        else if (_text != null) SetLabel(label);
        bool up = tier > _tier;
        _tier = tier;
        _visible = true;
        _shrinkAge = -1f;
        _popAge = 0f;
        _root.gameObject.SetActive(true);
        if (up && tier >= 1) TwinkleBurst(tier == 2 ? 5 : 3);
    }

    /// <summary>切れた（しぼんで消える）</summary>
    public void Break()
    {
        _tier = 0;
        if (!_visible) return;
        _visible = false;
        _shrinkAge = 0f;
    }

    /// <summary>プレイヤーの頭の上に合わせる（毎フレーム）。timeLeft01：切れるまでの残り（1 → 0）</summary>
    public void Follow(Transform player, int count, float timeLeft01)
    {
        if (player == null)
        {
            _root.gameObject.SetActive(false);
            return;
        }
        Vector2 head = FxKit.HeadOf(player.gameObject, (Vector2)player.position + Vector2.up) + Vector2.up * HeadGap;
        head = FxKit.ClampToView(head, 1.4f);
        transform.position = new Vector3(Mathf.Round(head.x / Px) * Px, Mathf.Round(head.y / Px) * Px, 0f);
        if (_visible) _timerLeft = timeLeft01;
        if (_timer != null)
        {
            float w = Mathf.Max(0f, _timerWidth * _timerLeft);
            _timer.transform.localScale = new Vector3(Mathf.Max(0.001f, w / Px), 2f, 1f);
            Vector3 p = _timer.transform.localPosition;
            _timer.transform.localPosition = new Vector3(-_timerWidth * 0.5f + w * 0.5f, p.y, p.z);
            _timer.enabled = w > 0.001f;
        }
    }

    void Update()
    {
        if (_root == null || !_root.gameObject.activeSelf) return;
        float dt = Time.deltaTime;
        float tierScale = _tier >= 2 ? 1.3f : _tier == 1 ? 1.15f : 1f;
        float s = tierScale;
        if (_shrinkAge >= 0f)
        {
            _shrinkAge += dt;
            float k = Mathf.Clamp01(_shrinkAge / ShrinkTime);
            s = Mathf.Lerp(1f, 0f, k * k);
            if (k >= 1f)
            {
                _shrinkAge = -1f;
                _root.gameObject.SetActive(false);
                return;
            }
        }
        else if (_popAge < PopTime)
        {
            _popAge += dt;
            float p = Mathf.Clamp01(_popAge / PopTime);
            // ポンとはねる：1 → 1.35 → 1
            s *= p < 0.4f ? Mathf.Lerp(1f, 1.35f, p / 0.4f) : Mathf.Lerp(1.35f, 1f, (p - 0.4f) / 0.6f);
        }
        _root.localScale = new Vector3(s, s, 1f);

        // 5〜：まわりにアプリの色のきらめき
        if (_visible && _tier >= 2 && Time.time >= _nextTwinkle)
        {
            _nextTwinkle = Time.time + 0.35f;
            TwinkleBurst(1);
        }
    }

    // ───────── 作る ─────────

    void Build(string label)
    {
        for (int i = _root.childCount - 1; i >= 0; i--) Destroy(_root.GetChild(i).gameObject);
        _builtLen = label.Length;

        int textPx = Mathf.CeilToInt(label.Length * TextHeight * 18f);
        bool hasIcon = true;
        int w = Pad + (hasIcon ? IconPx + 2 : 0) + textPx + Pad + 1;
        int h = _shape == BadgeShape.Bubble ? BodyPx + 3 : BodyPx;
        float bodyOffset = _shape == BadgeShape.Bubble ? 1.5f * Px : 0f; // しっぽの分、札のまん中は上
        float hw = w * Px * 0.5f;

        // 板（下にずらして、札のまん中が原点に来るように）
        SpriteRenderer body = RuntimeSprites.Show(_root, RewardKit.BadgeSprite(_shape, w, h), new Vector2(0f, -bodyOffset), RewardKit.OrderBadge, 1f, "Body");
        body.color = Color.white;

        // アイコン（無ければアプリの色の四角）
        float iconX = -hw + (Pad + IconPx * 0.5f) * Px;
        if (_icon != null)
        {
            SpriteRenderer sr = RuntimeSprites.Show(_root, _icon, new Vector2(iconX, 0f), RewardKit.OrderBadge + 1, 1f, "Icon");
            float sc = RewardKit.IconScale(_icon, IconPx);
            sr.transform.localScale = new Vector3(sc, sc, 1f);
        }
        else
        {
            Color main = FxKit.MainOf(_theme, RuntimeSprites.Hex("8AB4F8"));
            main.a = 1f;
            SpriteRenderer sq = RuntimeSprites.Show(_root, FxArt.Square(IconPx - 2), new Vector2(iconX, 0f), RewardKit.OrderBadge + 1, 1f, "Icon");
            sq.color = main;
        }

        // 飾りの数字（濃い灰色。黄色の上で読める）
        float textX = -hw + (Pad + IconPx + 2) * Px;
        _text = WorldText.Create(_root, label, TextHeight, RewardKit.BadgeText, RewardKit.OrderBadge + 2, true, TextAnchor.MiddleLeft, "Label");
        _text.transform.localPosition = new Vector3(textX, Px * 0.5f, -0.001f);

        // 残りの時間の線（札の下。うすい地＋アプリの色）
        _timerWidth = (w - 6) * Px;
        float lineY = -(BodyPx * 0.5f + 2f) * Px - (_shape == BadgeShape.Bubble ? 3f * Px : 0f);
        SpriteRenderer bg = RuntimeSprites.Show(_root, FxArt.Square(1), new Vector2(0f, lineY), RewardKit.OrderBadge, 1f, "TimerBg");
        bg.transform.localScale = new Vector3(_timerWidth / Px, 2f, 1f);
        bg.color = new Color(0f, 0f, 0f, 0.45f);
        Color line = FxKit.MainOf(_theme, Color.white);
        line.a = 1f;
        _timer = RuntimeSprites.Show(_root, FxArt.Square(1), new Vector2(0f, lineY), RewardKit.OrderBadge + 1, 1f, "Timer");
        _timer.transform.localScale = new Vector3(_timerWidth / Px, 2f, 1f);
        _timer.color = Color.Lerp(line, Color.white, 0.15f);
    }

    void SetLabel(string label)
    {
        if (_text != null) _text.text = label;
    }

    void TwinkleBurst(int n)
    {
        Color[] acc = FxKit.AccentsOf(_theme, RewardKit.BadgeYellow);
        float hw = Mathf.Max(0.6f, _builtLen * TextHeight * 0.5f + 0.4f);
        for (int i = 0; i < n; i++)
        {
            Vector2 off = new Vector2(Random.Range(-hw, hw), Random.Range(-0.35f, 0.45f));
            FxAnim.Sprite(FxArt.Twinkle, (Vector2)transform.position + off, RewardKit.OrderBadge + 3, 0.3f, i % 2 == 0 ? Color.white : acc[i % acc.Length], 1f, "BadgeTwinkle")
                .Pop().Rise(0.25f).Delay(0.03f * i).FadeFrom(0.4f);
        }
    }
}
