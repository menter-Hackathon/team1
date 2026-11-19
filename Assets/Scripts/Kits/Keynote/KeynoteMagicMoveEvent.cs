using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Keynote の場面③の出来事「マジックムーブ」（2026-11-19。docs/stage-gimmick-variety-b.md 9章）。数値はすべて【仮】。
/// ・図形（青い四角・丸。直径 1.2）が画面の角に出る → その時のプレイヤーの場所へ予告の線（追いかけない）→ 線の上を飛んで、反対側へ抜ける
///   （KitMotion.Diagonal・速さ 13）。角は 左上 → 右下 → 右上 → 左下 の順（下の角からは、床すれすれを斜め上へ）。
///   2.0秒おきに 5個（ステージで 4〜6個）。最後の 2個は、同じ角から 2本（20度ひらく）
/// ・予告 0.9秒（1個目は 1.0秒）。飛ぶときは、うしろに残像（「動いている途中」）
/// ・3個目のあと、まん中の床（幅 3）に、上からのスポットライト（白い光の柱。飾り）＋ KitMotion.HoldZone。
///   中に 1.5秒いると、拍手：KeynoteRule の拍手のメーターを進める（できなければ、クールタイムを全部戻す）。中にいる間も線は来る
/// </summary>
public class KeynoteMagicMoveEvent : KitTimedEvent
{
    static readonly KitCorner[] Corners = { KitCorner.TopLeft, KitCorner.BottomRight, KitCorner.TopRight, KitCorner.BottomLeft };
    const float Speed = 13f, Size = 1.2f, Step = 2f, FirstWarn = 1f, Warn = 0.9f, Spread = 20f;
    const float SpotWidth = 3f, SpotSeconds = 1.5f, SpotHeight = 3f;
    const float ApplaudAmount = 0.3f;
    const float TrailEvery = 0.04f, TrailLife = 0.25f;

    readonly int _stage;
    readonly List<KitMotionGroup> _groups = new List<KitMotionGroup>();
    float _trailAt;
    int _count;

    public KeynoteMagicMoveEvent(int stage) : base("Magic Move")
    {
        _stage = stage;
    }

    protected override void Build()
    {
        _count = GroupBMotion.Scaled(5, _stage);
        for (int i = 0; i < _count; i++)
        {
            int n = i;
            At(i * Step, () => Fly(n));
        }
        At(2f * Step + 0.6f, ShowSpotlight); // 3個目のあと
        float cross = new Vector2(View.width, View.height).magnitude / Speed;
        EndAt((_count - 1) * Step + Warn + cross + 0.2f);
    }

    void Fly(int index)
    {
        KitCorner corner = Corners[index % Corners.Length];
        bool pair = index >= _count - 2 && _count >= 3; // 最後の 2個は、同じ角から 2本
        float warn = index == 0 ? FirstWarn : Warn;
        Sprite sprite = index % 2 == 0 ? SquareSprite : KeynoteArt.Ball(22);
        KitMotionGroup g = KitMotion.Diagonal(Owner, KitMotion.Corner(corner, 0.7f), Speed, pair ? 2 : 1, pair ? Spread : 0f, warn, sprite,
            index % 2 == 0 ? KeynoteArt.Blue : (Color)new Color32(0x00, 0x6C, 0xFF, 0xFF), 1, Size);
        if (g != null) _groups.Add(g);
    }

    void ShowSpotlight()
    {
        KitMotionHold hold = KitMotion.HoldZone(Owner, View.center.x, SpotWidth, SpotSeconds, OnApplause, 0f, null, KeynoteArt.Spot, SpotHeight);
        if (hold == null) return;
        // 上からのスポットライト（白い光の柱。飾り。帯と一緒に消える）
        float h = Mathf.Max(1f, View.yMax - hold.Zone.yMin);
        FxKit.Box(hold.transform, new Vector2(0f, h * 0.5f), new Vector2(SpotWidth, h), new Color(1f, 1f, 0.9f, 0.1f), KitMotionItem.OrderZone - 1, "Spotlight");
        FxKit.Box(hold.transform, new Vector2(0f, h * 0.5f), new Vector2(SpotWidth * 0.55f, h), new Color(1f, 1f, 0.9f, 0.08f), KitMotionItem.OrderZone - 1, "SpotlightCore");
    }

    void OnApplause()
    {
        KeynoteRule rule = GroupBMotion.Rule<KeynoteRule>();
        if (rule == null || !rule.Applaud(ApplaudAmount)) KitPlayer.RefillCooldowns();
        Vector2 p = KitMotion.PlayerPos;
        for (int i = 0; i < 10; i++)
        {
            Color c = KeynoteArt.Confetti[i % KeynoteArt.Confetti.Length];
            FxAnim.Sprite(FxArt.Square(3), p + new Vector2(Random.Range(-1f, 1f), 1.5f), 45, 0.8f, c, 1f, "Keynote_Confetti")
                .Velocity(new Vector2(Random.Range(-2f, 2f), Random.Range(2f, 5f))).Gravity(9f).Spin(Random.Range(-360f, 360f));
        }
    }

    protected override void OnFrame(float clock)
    {
        _groups.RemoveAll(g => g == null || g.IsDone);
        if (Time.time < _trailAt) return;
        _trailAt = Time.time + TrailEvery;
        foreach (KitMotionGroup g in _groups)
        {
            foreach (KitMotionItem it in g.Items)
            {
                if (it == null || it.IsDone || it.Warning || it.Visual == null || it.Body == null) continue;
                Color c = it.Visual.color;
                c.a = 0.3f;
                FxAnim.Sprite(it.Visual.sprite, it.Body.position, KitMotionItem.OrderBody - 1, TrailLife, c, Size * 18f / Mathf.Max(1f, it.Visual.sprite.rect.width), "Keynote_Trail").FadeFrom(0f);
            }
        }
    }

    /// <summary>角の少し丸い四角（22×22 ドット。白で作る。色は SpriteRenderer で付ける）</summary>
    static Sprite SquareSprite => KitArt.Paint("keynote.event.square", 22, 22, c =>
    {
        c.RoundFill(0, 0, 22, 22, new Color32(184, 184, 204, 255));
        c.Fill(2, 2, 18, 18, new Color32(255, 255, 255, 255));
        c.Fill(4, 4, 5, 3, new Color32(255, 255, 255, 255));
    });
}
