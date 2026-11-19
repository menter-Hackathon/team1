using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Claude の場面③の出来事「アーティファクト」（2026-11-19。docs/stage-gimmick-variety-b.md 2章）。数値はすべて【仮】。
/// ・クリーム色のパネル（横 3×縦 2。Artifacts のパネル）が、奥から大きくなって その場に開く（KitMotion.Pop。弾はばらまかない）
/// ・場所は決めた 6か所（床の左・中・右／下の足場の上の左右／上の段のまん中）から、プレイヤーに近い 1つ＋離れた 1つの 2枚ずつ。
///   予告 0.9秒（1組目は 1.0秒。赤い点線の四角い枠＋まん中で回るオレンジのスパーク）→ 1.2秒開いている → 次の組まで 2.4秒。4組（ステージで 3〜5組）
/// ・2組目のあと、床のまん中（幅 3）にオレンジの光る帯「考える時間」（KitMotion.HoldZone）。中に 2秒いると、能力のクールタイムを全部戻して、
///   残りのパネルは出ない（出来事が早く終わる）。帯の上（床のまん中の場所）には、帯が出ている間パネルを出さない
/// </summary>
public class ClaudeArtifactEvent : KitTimedEvent
{
    /// <summary>パネルが開く場所（部屋のまん中・床から。パネルのまん中）</summary>
    static readonly Vector2[] Spots =
    {
        new Vector2(-6f, 1f), new Vector2(0f, 1f), new Vector2(6f, 1f),
        new Vector2(-3.5f, 4.1f), new Vector2(3.5f, 4.1f), new Vector2(0f, 6.2f),
    };
    const int MiddleFloorSpot = 1;
    static readonly Vector2 PanelSize = new Vector2(3f, 2f);
    const float FirstWarn = 1f, Warn = 0.9f, Life = 1.2f, Step = 2.4f;
    const float FarDistance = 6f;
    const float ZoneWidth = 3f, ZoneSeconds = 2f, ZoneHeight = 3f;

    readonly int _stage;
    KitMotionHold _zone;

    public ClaudeArtifactEvent(int stage) : base("Artifact")
    {
        _stage = stage;
    }

    protected override void Build()
    {
        int groups = GroupBMotion.Scaled(4, _stage);
        for (int g = 0; g < groups; g++)
        {
            int n = g;
            At(g * Step, () => Group(n));
        }
        At(2f * Step - 0.3f, ShowZone); // 2組目が閉じたあと（3組目の少し前）
        EndAt((groups - 1) * Step + Warn + Life + 0.6f);
    }

    Vector2 World(Vector2 spot) => new Vector2(View.center.x + spot.x, Floor + spot.y);

    bool ZoneLive => _zone != null && !_zone.IsDone;

    /// <summary>1組：プレイヤーに近い場所に 1枚＋離れた場所に 1枚</summary>
    void Group(int g)
    {
        Vector2 body = KitMotion.PlayerPos;
        var free = new List<int>();
        for (int i = 0; i < Spots.Length; i++)
        {
            if (ZoneLive && i == MiddleFloorSpot) continue; // 考える時間の帯の上には出さない
            free.Add(i);
        }
        if (free.Count == 0) return;
        int near = free[0];
        foreach (int i in free) if ((World(Spots[i]) - body).sqrMagnitude < (World(Spots[near]) - body).sqrMagnitude) near = i;
        var far = new List<int>();
        foreach (int i in free) if (i != near && (Spots[i] - Spots[near]).magnitude >= FarDistance) far.Add(i);
        int other = far.Count > 0 ? far[Random.Range(0, far.Count)] : -1;
        float warn = g == 0 ? FirstWarn : Warn;
        Open(World(Spots[near]), warn);
        if (other >= 0) Open(World(Spots[other]), warn);
    }

    void Open(Vector2 at, float warn)
    {
        KitMotion.Pop(Owner, at, warn, Life, 0, PanelSprite, Color.white, 1, PanelSize.y, box: PanelSize);
        // 予告のまん中に、回るオレンジのスパーク（アプリの色の印。飾り）
        FxAnim.Sprite(ClaudeArt.Spark(9), at, KitMotionItem.OrderMark, warn, ClaudeArt.Orange, 1f, "ClaudeArtifactSpark").Spin(360f).FadeFrom(0.85f);
    }

    void ShowZone()
    {
        _zone = KitMotion.HoldZone(Owner, View.center.x, ZoneWidth, ZoneSeconds, OnThought, 0f, null, ClaudeArt.Orange, ZoneHeight);
    }

    /// <summary>考える時間が終わった：クールタイムを全部戻して、残りのパネルは出さない</summary>
    void OnThought()
    {
        KitPlayer.RefillCooldowns();
        GameAudio.Event("gm_claude_reset");
        Vector2 p = KitMotion.PlayerPos;
        FxAnim.Sprite(ClaudeArt.Spark(13), p + Vector2.up * 1.2f, 60, 0.6f, ClaudeArt.Orange, 1.4f, "ClaudeThought").Spin(300f).Pop();
        Stop();
    }

    /// <summary>開くパネル（54×36 ドット＝3×2 マス）：クリーム色に、オレンジの見出しの棒と茶色の行（ClaudeArt.ArtifactPanel の大きい版）</summary>
    static Sprite PanelSprite => KitArt.Paint("claude.event.panel", 54, 36, c =>
    {
        Color32 cream = ClaudeArt.Cream, orange = ClaudeArt.Orange, brown = ClaudeArt.Brown, light = ClaudeArt.Light;
        c.RoundFill(0, 0, 54, 36, brown);
        c.Fill(1, 1, 52, 34, cream);
        c.Fill(1, 1, 52, 6, orange);
        c.Fill(3, 3, 2, 2, new Color32(255, 255, 255, 255));
        for (int i = 0; i < 5; i++) c.Fill(4, 11 + i * 5, i % 2 == 0 ? 38 : 28, 2, i % 2 == 0 ? brown : light);
    });
}
