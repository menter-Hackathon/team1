using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Steam のステージの出来事「実績チャレンジ」（場面③。2026-11-19。docs/stage-gimmick-variety-c.md 7章）。
/// ・Orbit ＋ HitTarget（主）：回るトロフィー。部屋のまん中（床から 3.5）のまわりを、半径 3.2・50°/秒で 3つ（120°おき）が 12秒回る。
///   的は水色のふちの白い丸にトロフィー（黄色はプレイヤーの色なので小さく）。当たっても痛くない。叩くたびにその場に 1/3・2/3
/// ・3つ全部：Achievement Unlocked「Trophy Hunter」（SteamRule.UnlockPlayer。今の実績と同じ通知・8秒の強化・1ステージに1回）。時間切れは何も起きない
/// ・Orbit（当たる物）：同じ輪の上を -50% の値札が 1／2／2つ、逆回り（40°/秒）。予告 1.0秒（輪のうすい水色の点線＋出る点で赤く点滅）
/// ・輪の外・輪のまん中はいつも安全。的は低い足場・まん中の足場のそばを通る
/// </summary>
public class SteamTrophyEvent : KitTimedEvent
{
    const float CenterHeight = 3.5f, Radius = 3.2f, TrophySpeed = 50f, TrophyTime = 12f, TrophySize = 1f;
    const float TagSpeed = -40f, TagWarn = 1f, TagTime = 11.5f, TagW = 1.4f, TagH = 0.56f;
    const int Trophies = 3;
    const string AchievementName = "Trophy Hunter";

    int _hits;
    GameObject _ring;

    public SteamTrophyEvent() : base("Challenge") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(SteamKit.Label, stage => new SteamTrophyEvent());

    Vector2 Center
    {
        get
        {
            Rect a = KitMotion.Area;
            return new Vector2(a.center.x, KitMotion.FloorY() + CenterHeight);
        }
    }

    protected override void Build()
    {
        At(1f, ShowRing);
        At(1f, SpawnTrophies);
        At(1f, SpawnTags);
        At(13.5f, () => { if (_ring != null) Object.Destroy(_ring); });
        EndAt(14f);
    }

    /// <summary>輪のうすい水色の点線（当たらない飾り。的と値札の通る道）</summary>
    void ShowRing()
    {
        _ring = new GameObject("SteamTrophyRing");
        Track(_ring);
        Vector2 c = Center;
        _ring.transform.position = c;
        const int dots = 48;
        Color col = new Color(SteamArt.Blue.r / 255f, SteamArt.Blue.g / 255f, SteamArt.Blue.b / 255f, 0.35f);
        for (int i = 0; i < dots; i++)
        {
            float a = Mathf.PI * 2f * i / dots;
            SpriteRenderer sr = AttackRunner.AddVisual(_ring.transform, PlaceholderSprites.Square, col, Vector2.one * 2f * KitDress.Px, KitMotionItem.OrderZone, "Dot");
            sr.transform.position = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius;
        }
    }

    void SpawnTrophies()
    {
        Vector2 c = Center;
        for (int i = 0; i < Trophies; i++)
        {
            float start = 90f + 360f * i / Trophies;
            KitPath path = KitPath.Orbit(c, Radius, TrophySpeed, start, TrophyTime);
            KitMotionTarget t = KitMotion.HitTarget(Owner, path.At(0f), TrophyTime, OnTrophy, null, KitMotionArt.Disc(18), Color.white, TrophySize, 1, path, 0.5f);
            if (t == null || t.Body == null) continue;
            AttackRunner.AddVisual(t.Body, KitMotionArt.Ring(18), SteamArt.Blue, Vector2.one * TrophySize, KitMotionItem.OrderBody + 1, "Rim");
            AttackRunner.AddVisual(t.Body, SteamArt.Trophy, Color.white, Vector2.one * TrophySize * 0.6f, KitMotionItem.OrderBody + 1, "Trophy");
        }
    }

    void OnTrophy()
    {
        _hits++;
        Vector2 at = KitMotion.PlayerPos + Vector2.up * 1.2f;
        FxKit.Text($"{_hits}/{Trophies}", at, 0.38f, (Color)SteamArt.Blue, FxKit.OrderToast - 2, 0.7f, (Color)SteamArt.Navy);
        GameAudio.Event("gm_steam_ach_player", 0.8f + 0.1f * _hits);
        if (_hits < Trophies) return;
        var rule = StageRules.Current as SteamRule;
        if (rule == null || !rule.UnlockPlayer(AchievementName))
        {
            KitPlayer.RefillCooldowns(); // 決まりが無い・もう解除していた：クールタイムを戻す
            KitToast.Show(AchievementName, SteamArt.TrophyIcon, (Color)SteamArt.Blue);
        }
    }

    void SpawnTags()
    {
        int n = Level >= 2 ? 2 : 1;
        // 値札は、的と的の間（30・150・270°）から。1つなら下（270°）
        float[] starts = n == 1 ? new[] { 270f } : new[] { 30f, 150f };
        Sprite tag = SteamArt.PriceTag("-50%");
        foreach (float s in starts)
        {
            KitMotionGroup g = KitMotion.Orbit(Owner, Center, Radius, TagSpeed, 1, TagTime, TagWarn, tag, null, 1, TagW, s);
            if (g == null) continue;
            // 値札の形（横長）に直す：Orbit は四角い大きさで描くので、絵の縦横を戻し、当たりも細長く
            foreach (var item in g.Items)
            {
                if (!(item is KitMotionMover m)) continue;
                if (m.Visual != null && m.Visual.sprite != null)
                {
                    Vector2 native = m.Visual.sprite.bounds.size;
                    float k = TagW / Mathf.Max(0.001f, native.x);
                    m.Visual.transform.localScale = new Vector3(k, k, 1f);
                }
                if (m.Body != null && m.Body.TryGetComponent(out BoxCollider2D box)) box.size = new Vector2(TagW * 0.85f, TagH * 0.8f);
            }
        }
    }
}
