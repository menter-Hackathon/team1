using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】メッセージ（Messages）の場面③の出来事「エコー」（2026-11-19。docs/stage-gimmick-variety-a.md 2章 Messages）。
/// ・動かない足場（ふきだし）2枚（強さ 3 は 3枚）のまん中を中心に、小さい青いふきだしが回る（KitMotion.Orbit。半径 2.2・3個・1周 4秒。
///   となりの輪とは逆回り）。予告 1.0（通り道の赤い点線の円）→ 回る 8秒（通り道はうすい青の点線）→ 消える
/// ・輪のまん中（足場の上）に、ピンクのハート（Tapback。KitMotion.Catch）。取ると風船 1つぶんのごほうび（クールタイム 1秒進む）。全部取ると紙吹雪
/// ・上へ流れる足場（blue2）・出たり消えたりする「…」は中心にしない（GimmickA.SteadyPlatforms）。輪どうしは中心を 6マス以上離す。
///   床の左右の端 3マスは、なるべくどの輪も通らない（足りなければゆるめる）【仮】
/// </summary>
public class MessagesEchoEvent : KitTimedEvent
{
    const float Radius = 2.2f, Speed = 90f, Spin = 8f, Warn = 1.0f, MinApart = 6f, EdgeFree = 3f;
    const int Bubbles = 3;

    int _rings, _caught;

    public MessagesEchoEvent() : base("Sent with Echo") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(MessagesKit.Label, stage => new MessagesEchoEvent());

    protected override void Build()
    {
        int want = Level >= 3 ? 3 : 2;
        List<Vector2> centers = PickCenters(want);
        _rings = centers.Count;
        for (int i = 0; i < centers.Count; i++)
        {
            Vector2 c = centers[i];
            float speed = i % 2 == 0 ? Speed : -Speed;
            At(0f, () => Ring(c, speed));
        }
        if (centers.Count > 0) At(0f, () => GameAudio.Event("gm_messages_balloons"));
        EndAt(Warn + Spin + 0.6f);
    }

    List<Vector2> PickCenters(int want)
    {
        Rect a = View;
        List<Bounds> plats = GimmickA.SteadyPlatforms(a, Warn + Spin);
        var picked = new List<Vector2>();
        for (int pass = 0; pass < 2 && picked.Count < want; pass++)
        {
            float margin = pass == 0 ? EdgeFree + Radius : 0f; // 1回目：床の端 3マスを空ける
            var list = new List<Bounds>(plats);
            for (int i = list.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
            foreach (Bounds b in list)
            {
                if (picked.Count >= want) break;
                Vector2 c = new Vector2(b.center.x, b.max.y + 0.9f);
                if (c.x < a.xMin + margin || c.x > a.xMax - margin) continue;
                bool apart = true;
                foreach (Vector2 p in picked) if (Mathf.Abs(p.x - c.x) < MinApart && Mathf.Abs(p.y - c.y) < MinApart) apart = false;
                if (apart) picked.Add(c);
            }
        }
        return picked;
    }

    void Ring(Vector2 c, float speed)
    {
        KitMotionGroup g = KitMotion.Orbit(Owner, c, Radius, speed, Bubbles, Spin, Warn, BubbleSprite, null, 1, 0.7f, Random.Range(0f, 120f));
        Sprite ring = KitMotionArt.Ring(64, true);
        Vector2 size = Vector2.one * Radius * 2f;
        if (g != null)
        {
            KitMotionMark.Create(g.transform, ring, c, size, 0f, KitMotion.Red, KitMotionItem.OrderWarn, g.Warn > 0f ? g.Warn : Warn, "Path");
            At(Clock + Warn, () =>
            {
                if (g != null && !g.IsDone)
                    KitMotionMark.Create(g.transform, ring, c, size, 0f, new Color(0.55f, 0.75f, 1f, 0.35f), KitMotionItem.OrderZone, Spin, "PathSpin");
            });
        }
        // 輪のまん中に、ピンクのハート（取る）
        KitMotion.Catch(Owner, c, c, 1f, OnCatch, TalkArt.HeartIcon, null, 0.7f, Spin, Warn);
    }

    void OnCatch()
    {
        _caught++;
        KitPlayer.AdvanceCooldowns(1f);
        GameAudio.Event("gm_messages_pop");
        if (_caught < _rings) return;
        // 全部取った：紙吹雪
        Vector2 p = KitMotion.PlayerPos;
        Color[] colors = { MessagesArt.BalloonYellow, MessagesArt.BalloonPurple, MessagesArt.Blue, MessagesArt.Green, Color.white };
        FxKit.Fragments(p + Vector2.up * 0.8f, Vector2.up, 16, new[] { MessagesArt.Confetti }, colors, 1f);
        GameAudio.Event("gm_messages_all");
    }

    /// <summary>小さい青いふきだし（14×12。白いふち・右下にしっぽ）</summary>
    static Sprite BubbleSprite
    {
        get
        {
            char[,] g = GimmickA.Grid(14, 12);
            GimmickA.Fill(g, 1, 0, 12, 10, 'W');
            GimmickA.Fill(g, 0, 1, 14, 8, 'W');
            GimmickA.Fill(g, 2, 1, 10, 8, 'B');
            GimmickA.Fill(g, 1, 2, 12, 6, 'B');
            GimmickA.Fill(g, 10, 9, 3, 2, 'W');
            GimmickA.Fill(g, 11, 9, 1, 1, 'B');
            GimmickA.Fill(g, 4, 4, 6, 1, 'L');
            GimmickA.Fill(g, 4, 6, 4, 1, 'L');
            var pal = new Dictionary<char, Color32> { { 'W', new Color32(255, 255, 255, 255) }, { 'B', MessagesArt.Blue }, { 'L', new Color32(0xBF, 0xDC, 0xFF, 255) } };
            return RuntimeSprites.FromAscii("MessagesEchoBubble", GimmickA.Rows(g), pal);
        }
    }
}
