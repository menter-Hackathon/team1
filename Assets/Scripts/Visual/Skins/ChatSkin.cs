using UnityEngine;
using UnityEngine.SceneManagement;
using K = OfficeGameChatKit;

/// <summary>
/// 【仮】Chat のスキン（2026-11-18。docs/app-skill-visuals-design.md 7章）。Slack・Discord・LINE・メッセージ・Teams・Zoom（ボスのとき）。
/// ・代用ボスの技（7-1。ChatFrames）
/// ・その場で作る能力（1-7 の Chat の行。ChatFrames）：Chat のアプリにはカテゴリの能力が無いので、ひな形の能力がここで描かれる
/// ・当たった絵（7-2）：既読のチェック2つ（大はリアクションが弧を描いて飛ぶ）。エラーの絵：頭の右上に赤いバッジ（受けた回数）。
///   ダウン：バッジが 99+ になって大きくはじける。通知の題名：All read
/// Zoom の能力（7-3）は担当2（ZoomSkin）。見た目だけ。当たり判定・秒数・ダメージは変えない
/// </summary>
public class ChatSkin : AppSkin
{
    public override SkinId Id => SkinId.Chat;
    public override string NotificationTitle => "All read";

    public override bool Dress(AttackFrame frame, DressPhase phase, DressInfo info) =>
        K.Safe(() => DressCore(frame, phase, info), $"Chat.{frame}.{phase}");

    static bool DressCore(AttackFrame frame, DressPhase phase, DressInfo info)
    {
        if (info.Target == null) return false;
        if (frame == AttackFrame.None) frame = K.FrameFromName(info.Data);
        if (!info.IsEnemy)
        {
            // その場で作る能力（1-7）。棒グラフ・ボムの攻撃データの複製でも、Chat の絵で描く
            switch (frame)
            {
                case AttackFrame.Pillar: return ChatFrames.MentionPillar(phase, info);
                case AttackFrame.SmallShot: return ChatFrames.BadgeShot(phase, info);
                case AttackFrame.Thrown: return ChatFrames.Plane(phase, info);
                case AttackFrame.Floor: return ChatFrames.UnreadFloor(phase, info);
                case AttackFrame.Circle: return ChatFrames.BubblePop(phase, info);
                case AttackFrame.Paint: return ChatFrames.BubblePaint(phase, info);
            }
            return false;
        }
        switch (frame)
        {
            case AttackFrame.SmallShot: return ChatFrames.BadgeShot(phase, info);
            case AttackFrame.LongShot: return ChatFrames.MessageShot(phase, info);
            case AttackFrame.Floor: return ChatFrames.UnreadFloor(phase, info);
            case AttackFrame.Pillar: return ChatFrames.MentionPillar(phase, info);
            case AttackFrame.Window: return ChatFrames.BigBubble(phase, info);
            case AttackFrame.Falling: return ChatFrames.StampFall(phase, info);
            case AttackFrame.TopRow: return ChatFrames.NotifyRow(phase, info);
            case AttackFrame.Warp: return ChatFrames.StatusWarp(phase, info);
        }
        return false;
    }

    public override bool Hit(HitInfo info) => K.Safe(() => ChatHits.Hit(info), "Chat.Hit");
    public override bool Error(HitInfo info) => K.Safe(() => ChatHits.Error(info), "Chat.Error");
}

/// <summary>【仮】Chat の当たった絵・エラーの絵（7-2）。絵だけを出す（ゆれ・ヒットストップ・白い輪は HitFx）</summary>
public static class ChatHits
{
    // 受けた回数（このボス戦。シーンが変わるか、40秒あいたら 0 に戻す）【要相談 6：まさぴ〜が「出す」と決めた】
    static int _hurt;
    static float _lastHurt = -100f;
    static int _scene = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _hurt = 0;
        _lastHurt = -100f;
        _scene = -1;
    }

    public static bool Hit(HitInfo info)
    {
        Color app = ChatArt.App(info.Theme);
        Vector2 at = K.HitSpot(info);
        Vector2 dir = K.HitDir(info);
        Sprite[] bits = ChatArt.Bits(app);
        if (info.Strength == HitStrength.Small)
        {
            K.Debris(at, dir, bits, 1, 48);
            return true;
        }
        bool big = info.Strength != HitStrength.Light;
        float scale = info.Strength == HitStrength.Finisher ? 1.5f : big ? 1.3f : 1f;
        float life = info.Strength == HitStrength.Light ? 0.22f : info.Strength == HitStrength.Heavy ? 0.32f : 0.45f;
        // 既読のチェック2つ
        var fx = K.Fx("ChatHit", at, life + 0.05f);
        var node = K.Node(fx.transform, "Read", Vector2.zero);
        K.Show(node, ChatArt.Read(app), Vector2.zero, 47, "Read");
        fx.Play(K.PopRise(fx, node, life, 0.4f, 0.08f, scale));
        if (big)
        {
            // リアクション（ハート・親指・星）が 3個、上へ弧を描いて飛ぶ
            Sprite[] r = ChatArt.Reactions;
            for (int i = 0; i < 3; i++)
            {
                var rf = K.Fx("ChatReaction", at, 0.6f);
                var sr = K.Show(rf.transform, r[i], Vector2.zero, 48, "Reaction");
                float side = (i - 1) * 2.2f;
                rf.Play(Arc(rf, sr, new Vector2(side + Random.Range(-0.4f, 0.4f), 6.5f + Random.Range(0f, 1.5f)), 0.55f, scale));
            }
        }
        int count = info.Strength == HitStrength.Light ? 3 : info.Strength == HitStrength.Heavy ? 6 : 12;
        K.Debris(at, dir, bits, count, 48, info.Strength == HitStrength.Finisher ? 70f : 40f);
        return true;
    }

    static System.Collections.IEnumerator Arc(OfficeGameChatFx fx, SpriteRenderer sr, Vector2 v, float life, float scale)
    {
        float start = fx.Now, last = start;
        while (fx != null)
        {
            float now = fx.Now, dt = now - last;
            last = now;
            v.y -= 16f * dt;
            fx.transform.position += (Vector3)(v * dt);
            float k = Mathf.Clamp01((now - start) / life);
            float s = K.Pop(now - start, 0.08f) * scale;
            fx.transform.localScale = new Vector3(s, s, 1f);
            K.SetAlpha(sr, 1f - k * k);
            yield return null;
        }
    }

    /// <summary>エラーの絵：頭の右上に赤いバッジ（このボス戦で受けた回数）。ダウン：バッジが 99+ になって、大きくはじける</summary>
    public static bool Error(HitInfo info)
    {
        int scene = SceneManager.GetActiveScene().handle;
        if (scene != _scene || Time.time - _lastHurt > 40f) _hurt = 0;
        _scene = scene;
        _lastHurt = Time.time;
        _hurt = Mathf.Min(99, _hurt + 1);

        Bounds body = K.BodyOf(info.Target, info.Point);
        Vector3 offset = new Vector3(body.extents.x * 0.7f + 0.15f, body.extents.y + 0.35f, 0f);
        Transform follow = info.Target != null ? info.Target.transform : null;
        Vector3 basePos = follow != null ? follow.position : (Vector3)info.Point;
        Vector3 centerOffset = (Vector3)(Vector2)body.center - basePos;
        bool down = info.Strength == HitStrength.Down;
        string text = down ? "99+" : _hurt.ToString();
        float life = down ? 0.9f : 0.7f;
        var fx = K.Fx(down ? "ChatDown" : "ChatError", (Vector2)(basePos + centerOffset + offset), life + 0.05f, null, down);
        if (follow != null && !down) fx.Follow(follow, centerOffset + offset);
        var node = K.Node(fx.transform, "Badge", Vector2.zero);
        K.Show(node, ChatArt.Badge(text, ChatArt.BadgeRed, Color.white), Vector2.zero, 49, "Badge");
        fx.Play(K.PopRise(fx, node, life, 0.1f, 0.1f, down ? 2f : 1.2f));
        if (down)
        {
            // 大きくはじける
            var bits = new[] { K.DiscSprite(4, ChatArt.BadgeRed), K.DiscSprite(3, ChatArt.BadgeRed), K.DiscSprite(2, Color.white) };
            K.Debris(fx.transform.position, Vector2.up, bits, 10, 48, 180f, 4f, 9f, 16f, 0.4f, 0.7f, true);
        }
        return true; // 赤いかけら（被弾）は HitFx が出す
    }
}
