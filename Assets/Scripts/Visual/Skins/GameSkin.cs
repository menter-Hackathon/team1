using UnityEngine;
using K = OfficeGameChatKit;

/// <summary>
/// 【仮】Game のスキン（2026-11-18。docs/app-skill-visuals-design.md 6章）。Steam・Minecraft Launcher・Epic Games Launcher。
/// ・能力：ボム（6-1。GameFrames.Bomb・Blast）。Game のプレイヤーの投げ物・円は、どれもボムで描く（1-7 の終わり）
/// ・代用ボスの技（6-2。GameFrames）
/// ・当たった絵（6-3）：黄色のぎざぎざの星に HIT!（2秒以内に続けて当てると x2 x3 …）。エラーの絵：割れるハート。ダウン：GAME OVER。
///   通知の題名：Achievement unlocked
/// 見た目だけ。当たり判定・秒数・ダメージは変えない
/// </summary>
public class GameSkin : AppSkin
{
    public override SkinId Id => SkinId.Game;
    public override string NotificationTitle => "Achievement unlocked";

    public override bool Dress(AttackFrame frame, DressPhase phase, DressInfo info) =>
        K.Safe(() => DressCore(frame, phase, info), $"Game.{frame}.{phase}");

    static bool DressCore(AttackFrame frame, DressPhase phase, DressInfo info)
    {
        if (info.Target == null) return false;
        if (frame == AttackFrame.None) frame = K.FrameFromName(info.Data);
        if (!info.IsEnemy)
        {
            // プレイヤー：ボム（と、Game に当たった投げ物・円のひな形）だけ。ほかは Generic
            if (frame == AttackFrame.Thrown) return GameFrames.Bomb(phase, info);
            if (frame == AttackFrame.Circle) return GameFrames.Blast(phase, info);
            return false;
        }
        switch (frame)
        {
            case AttackFrame.SmallShot: return GameFrames.Buttons(phase, info);
            case AttackFrame.LongShot: return GameFrames.Missile(phase, info);
            case AttackFrame.Floor: return GameFrames.LavaFloor(phase, info);
            case AttackFrame.Pillar: return GameFrames.LightPillar(phase, info);
            case AttackFrame.Window: return GameFrames.AchievementWindow(phase, info);
            case AttackFrame.Falling: return GameFrames.LibraryFall(phase, info);
            case AttackFrame.TopRow: return GameFrames.DownloadRow(phase, info);
            case AttackFrame.Warp: return GameFrames.LoadingWarp(phase, info);
        }
        return false;
    }

    public override bool Hit(HitInfo info) => K.Safe(() => GameHits.Hit(info), "Game.Hit");
    public override bool Error(HitInfo info) => K.Safe(() => GameHits.Error(info), "Game.Error");
}

/// <summary>【仮】Game の当たった絵・エラーの絵（6-3）。絵だけを出す（ゆれ・ヒットストップ・白い輪は HitFx）</summary>
public static class GameHits
{
    // コンボ（同じボス戦で 2秒以内に続けて当てると数える。最大 x99）【要相談 6：まさぴ〜が「出す」と決めた】
    static float _lastHit = -10f;
    static int _combo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _lastHit = -10f;
        _combo = 0;
    }

    public static bool Hit(HitInfo info)
    {
        Vector2 at = K.HitSpot(info);
        Vector2 dir = K.HitDir(info);
        Sprite[] bits = GameSkinArt.Bits();
        if (info.Strength == HitStrength.Small)
        {
            K.Debris(at, dir, bits, 1, 48);
            return true;
        }
        if (Time.time - _lastHit > 2f) _combo = 0;
        _lastHit = Time.time;
        _combo = Mathf.Min(99, _combo + 1);

        bool big = info.Strength != HitStrength.Light;
        float scale = info.Strength == HitStrength.Finisher ? 1.5f : 1f;
        float life = info.Strength == HitStrength.Light ? 0.22f : info.Strength == HitStrength.Heavy ? 0.32f : 0.45f;
        var fx = K.Fx("GameHit", at, life + 0.05f);
        var node = K.Node(fx.transform, "Star", Vector2.zero);
        int d = big ? 24 : 14;
        K.Show(node, GameSkinArt.Star(d, GameSkinArt.Yellow, GameSkinArt.Orange), Vector2.zero, 47, "Star");
        K.Show(node, K.Text("HIT!", Color.white, big ? 2 : 1, K.Hex("3A2A00")), Vector2.zero, 48, "Hit");
        if (_combo >= 2)
        {
            // x2 x3 …（字は1割ずつ大きく、x5 まで）
            float cs = 1f + 0.1f * Mathf.Min(4, _combo - 2);
            var c = K.Node(node, "Combo", new Vector2((d * 0.5f + 6f) * K.Dot, (d * 0.5f) * K.Dot));
            c.localScale = new Vector3(cs, cs, 1f);
            K.Show(c, K.Text("x" + _combo, GameSkinArt.Yellow, 2, K.Hex("3A2A00")), Vector2.zero, 49, "Text");
        }
        fx.Play(K.PopRise(fx, node, life, 0.4f, 0.08f, scale));
        K.Debris(at, dir, bits, info.Strength == HitStrength.Light ? 3 : info.Strength == HitStrength.Heavy ? 6 : 12, 48,
            info.Strength == HitStrength.Finisher ? 70f : 40f);
        return true;
    }

    /// <summary>エラーの絵：ハートが割れる（頭の上）。ダウン：GAME OVER を小さく（画面のまん中寄り）</summary>
    public static bool Error(HitInfo info)
    {
        Bounds body = K.BodyOf(info.Target, info.Point);
        Vector2 head = new Vector2(body.center.x, body.max.y + 0.6f);
        bool down = info.Strength == HitStrength.Down;
        Vector2 at = down ? Vector2.Lerp(head, K.View().center, 0.5f) : head;
        float life = down ? 1.2f : 0.45f;
        var fx = K.Fx(down ? "GameDown" : "GameError", at, life + 0.05f, null, down);
        fx.Play(HeartBreak(fx, down ? 2f : 1f, life));
        if (down)
        {
            var t = K.Node(fx.transform, "GameOver", new Vector2(0f, -0.9f));
            K.Show(t, K.Text("GAME OVER", Color.white, 2, K.Hex("D7261E")), Vector2.zero, 49, "Text");
            fx.Play(K.PopRise(fx, t, life, 0.15f, 0.1f));
        }
        return true; // 赤いかけらは HitFx が出す
    }

    static System.Collections.IEnumerator HeartBreak(OfficeGameChatFx fx, float scale, float life)
    {
        var root = K.Node(fx.transform, "Heart", Vector2.zero);
        root.localScale = new Vector3(scale, scale, 1f);
        var whole = K.Show(root, GameSkinArt.Heart, Vector2.zero, 49, "Whole");
        var l = K.Show(root, GameSkinArt.HeartHalf(true), Vector2.zero, 49, "L");
        var r = K.Show(root, GameSkinArt.HeartHalf(false), Vector2.zero, 49, "R");
        l.enabled = r.enabled = false;
        float start = fx.Now;
        while (fx != null)
        {
            float age = fx.Now - start;
            float s = K.Pop(age, 0.08f) * scale;
            root.localScale = new Vector3(s, s, 1f);
            if (age > 0.12f)
            {
                // 左右に分かれて、少し落ちる
                whole.enabled = false;
                l.enabled = r.enabled = true;
                float k = Mathf.Clamp01((age - 0.12f) / Mathf.Max(0.05f, life - 0.12f));
                l.transform.localPosition = new Vector3(-0.25f * k, -0.2f * k * k, 0f);
                r.transform.localPosition = new Vector3(0.25f * k, -0.2f * k * k, 0f);
                l.transform.localRotation = Quaternion.Euler(0f, 0f, 25f * k);
                r.transform.localRotation = Quaternion.Euler(0f, 0f, -25f * k);
                K.SetAlpha(l, 1f - k * k);
                K.SetAlpha(r, 1f - k * k);
            }
            yield return null;
        }
    }
}
