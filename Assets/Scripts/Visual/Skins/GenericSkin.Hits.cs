using UnityEngine;

// Generic のスキンの、当たった絵・エラーの絵・通知（2026-11-18。docs/app-skill-visuals-design.md 10章の下の表。担当1）。
// 枠の絵（Dress）は GenericSkin.Frames.cs（担当4）。
// ・当たった絵：アプリの色の輪と、白い星。大は、アイコン（12ドット）が 1個、上へ跳ねる
// ・かけら：アプリの色の四角（3色まで）
// ・エラーの絵：虹色の待ちカーソル（16ドットの円。6色の扇形。0.4秒で1回転）
// ・ダウン：Mac の終了のダイアログ（アイコン＋ quit unexpectedly.、Ignore・青い Reopen）。待ちカーソルを 0.4秒出してから（2026-11-19【仮】docs/mac-look-design.md 2章 5）
// ・通知の題名：Cleaned
// アプリの色・アイコンが無いとき（テスト用のシーン）でも出す（白っぽい青と、白い四角）
public partial class GenericSkin
{
    static readonly Color Fallback = RuntimeSprites.Hex("8AB4F8");

    public override string NotificationTitle => "Cleaned";

    public override bool Hit(HitInfo info)
    {
        HitStrength s = info.Strength;
        Color main = FxKit.MainOf(info.Theme, Fallback);
        main.a = 1f;
        Color[] accents = FxKit.AccentsOf(info.Theme, Fallback);
        Sprite[] bits = { FxArt.Square(3), FxArt.Square(2), FxArt.Square(3) };

        if (s == HitStrength.Small)
        {
            FxKit.Fragments(info.Point, info.Direction, 1, bits, accents);
            return true;
        }

        Vector2 p = FxKit.PictureAnchor(info);
        bool big = FxKit.IsBig(s);
        float scale = FxKit.PictureScale(s);
        float life = FxKit.PictureLife(s);

        // アプリの色の輪（小 12ドット、大 22ドット）が広がる
        Sprite[] ring = FxArt.RingFrames(big ? 11 : 6);
        FxAnim.Sprite(ring[0], p, FxKit.OrderHitPicture, life, main, scale, "GenericRing")
            .Frames(ring, ring.Length / (life * 0.6f), false).Rise(0.4f).FadeFrom(0.5f);
        // 白い星（輪のまん中）
        FxAnim.Sprite(FxArt.Star4, p, FxKit.OrderHitPicture, life, Color.white, (big ? 1.6f : 1f) * scale, "GenericStar")
            .Pop().Rise(0.4f).Spin(big ? 120f : 60f).FadeFrom(0.45f);
        // きらめき 2つ（アプリの差し色）
        for (int i = 0; i < (big ? 3 : 1); i++)
        {
            Vector2 off = new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(0.1f, 0.5f)) * scale;
            FxAnim.Sprite(FxArt.Twinkle, p + off, FxKit.OrderHitPicture, life * 0.8f, accents[(i + 1) % accents.Length], 1f, "GenericTwinkle")
                .Pop().Delay(0.04f * i).Rise(0.3f).FadeFrom(0.3f);
        }
        // 大：アプリのアイコン（12ドット）が1個、上へ跳ねる
        if (big)
        {
            Sprite icon = info.AppIcon;
            FxAnim a = icon != null
                ? FxAnim.Sprite(icon, p, FxKit.OrderHitPicture + 1, 0.55f, Color.white, IconScale(icon, 12f) * (s == HitStrength.Finisher ? 1.5f : 1f), "GenericIcon")
                : FxAnim.Sprite(FxArt.Square(10), p, FxKit.OrderHitPicture + 1, 0.55f, main, 1f, "GenericIcon");
            float side = info.Direction.x >= 0f ? 1f : -1f;
            a.Velocity(new Vector2(-side * 1.6f, 7.5f)).Gravity(26f).Pop().FadeFrom(0.7f);
        }
        FxKit.Fragments(info.Point, info.Direction, FxKit.FragmentCount(s), bits, accents);
        return true;
    }

    public override bool Error(HitInfo info)
    {
        Vector2 p = FxKit.PictureAnchor(info);
        if (info.Strength == HitStrength.Down)
        {
            QuitUnexpectedly(p, info);
            return true;
        }
        // 虹色の待ちカーソル（0.4秒で1回転＝8コマを 20コマ/秒）
        Sprite[] ball = FxArt.BeachBall;
        FxAnim.Sprite(ball[0], p, FxKit.OrderHitPicture, 0.4f, Color.white, 1f, "BeachBall")
            .Frames(ball, 20f).Pop().Rise(0.15f).FadeFrom(0.7f);
        return true;
    }

    /// <summary>
    /// ダウン：虹色の待ちカーソルを 0.4秒 → Mac の終了のダイアログ（アイコン＋ quit unexpectedly.、Ignore・青い Reopen。注意の三角）。
    /// 2倍にしない（幅 9マスまで）。画面のまん中寄り（2026-11-19【仮】）
    /// </summary>
    static void QuitUnexpectedly(Vector2 p, HitInfo info)
    {
        const float wait = 0.4f;
        Sprite icon = info.AppIcon;
        if (icon == null)
        {
            Color main = FxKit.MainOf(info.Theme, Fallback);
            main.a = 1f;
            icon = PixelIcon.Placeholder(main, 16, $"Generic_DownIcon_{ColorUtility.ToHtmlStringRGB(main)}");
        }
        FxAnim a = FxAnim.Create("QuitUnexpectedly", p, 1.2f);
        Transform dlg = MacUi.PlaceAlert(a.transform, Vector2.zero, icon, "quit unexpectedly.", new[] { "Ignore", "Reopen" }, FxKit.OrderHitPicture + 1);
        // 画面の中に収める（HUD の下まで）
        Vector2 size = new Vector2(8f, 2.4f);
        Transform body = dlg.Find("Body");
        if (body != null && body.TryGetComponent(out SpriteRenderer sr) && sr.sprite != null) size = sr.sprite.bounds.size;
        Rect v = ScreenUi.ViewRect();
        float hw = size.x * 0.5f + 0.3f, hh = size.y * 0.5f + 0.3f;
        a.transform.position = new Vector2(
            Mathf.Clamp(p.x, v.xMin + hw, Mathf.Max(v.xMin + hw, v.xMax - hw)),
            Mathf.Clamp(p.y, v.yMin + hh, Mathf.Max(v.yMin + hh, v.yMax - hh - 1.2f)));
        a.Refresh();
        a.Pop().Delay(wait).FadeFrom(0.8f);
        // 先に、ダイアログが出る所に待ちカーソル
        Sprite[] ball = FxArt.BeachBall;
        FxAnim.Sprite(ball[0], a.transform.position, FxKit.OrderHitPicture + 3, wait + 0.05f, Color.white, 1f, "BeachBall")
            .Frames(ball, MacUi.BeachBallFps).Pop().FadeFrom(0.85f);
    }

    /// <summary>アイコンを、長い辺が px ドットになる倍率</summary>
    static float IconScale(Sprite icon, float px)
    {
        float native = Mathf.Max(icon.rect.width, icon.rect.height);
        return native > 0f ? px / native * (icon.pixelsPerUnit / 18f) : 1f;
    }
}

/// <summary>【仮】絵のコマを回し続ける（虹色の待ちカーソルなど。FxAnim の子に付ける）</summary>
public class FxSpinFrames : MonoBehaviour
{
    Sprite[] _frames;
    float _fps;
    float _age;
    SpriteRenderer _sr;

    public void Setup(Sprite[] frames, float fps)
    {
        _frames = frames;
        _fps = fps;
        _sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (_sr == null || _frames == null || _frames.Length == 0) return;
        _age += Time.deltaTime;
        _sr.sprite = _frames[Mathf.FloorToInt(_age * _fps) % _frames.Length];
    }
}
