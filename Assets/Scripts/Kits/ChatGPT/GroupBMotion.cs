using UnityEngine;

/// <summary>
/// 【仮】グループ B（docs/stage-gimmick-variety-b.md）の出来事・決まりで使う、KitMotion の足りない所の道具（2026-11-19）。
/// ・KitMotion の EdgeSweep・Bounce などは体が正方形なので、四角い体（行・ブロック・スライド・炎）を道すじで動かす物をここで作る
///   （KitMotion の内側の道具 Make・BuildBody・Setup・WarnArrow・WarnLine を、同じアセンブリから使う。Kits/Common は変えない）
/// ・ステージの番号（1〜3）で数を変える：2ステージ目の数が base（1ステージ目は −1・最低 1、3ステージ目は +1）
/// ・座標の目安：部屋のまん中の x と床の高さからのずれ（Tools/art/appbg の足場の表は、部屋のまん中 x 0・床 y 0）
/// </summary>
public static class GroupBMotion
{
    /// <summary>ステージの番号で数を変える（2ステージ目＝base。1ステージ目は −1・最低 1、3ステージ目は +1）</summary>
    public static int Scaled(int baseCount, int stage) => Mathf.Max(1, baseCount + Mathf.Clamp(stage, 1, 3) - 2);

    /// <summary>
    /// 危ない物（四角い体 size＝横×縦）を、道すじ path に沿って動かす。予告の間（warn 秒。危ない物は 0.6秒以上）は
    /// 最初の場所にいて、ghost なら うすく点滅、そうでなければ隠しておく。予告の印（矢印・線）は呼ぶ側
    /// </summary>
    public static KitMotionMover Mover(KitOwner owner, KitPath path, Vector2 size, Sprite sprite, Color color, float warn, bool ghost,
        int damage = 1, string objectName = "GroupBMover", float hitScale = 0.85f, bool circle = false)
    {
        path = path ?? KitPath.Stay(KitMotion.Area.center, 0f);
        var m = KitMotion.Make<KitMotionMover>(owner, null, objectName, path.At(0f));
        m.BuildBody(sprite, color, size, circle, damage, hitScale);
        m.Setup(path, KitMotion.WarnFor(m.Owner, warn, damage > 0), ghost);
        if (damage > 0) KitMotion.Safe(GameAudio.Telegraph);
        return m;
    }

    /// <summary>
    /// 横から：四角い物（size＝横×縦）が、side の端の外から反対の端の外まで横切る（worldY は世界の高さ。wave は上下の振れ幅）。
    /// 予告：入る端の矢印と、その高さの赤い線（wave があれば、うすい帯）。良い物（damage 0）は color の印
    /// </summary>
    public static KitMotionMover SweepRect(KitOwner owner, KitSide side, float worldY, float speed, Vector2 size, Sprite sprite, Color color,
        float warn = KitMotion.DefaultWarn, float wave = 0f, float waveLength = 4f, int damage = 1, string objectName = "GroupBSweep")
    {
        Rect a = KitMotion.Area;
        bool fromLeft = side != KitSide.Right;
        KitPath path = KitPath.Sweep(fromLeft ? KitSide.Left : KitSide.Right, worldY, speed, wave, waveLength, size.x * 0.5f + 0.3f, a);
        var m = KitMotion.Make<KitMotionMover>(owner, null, objectName, path.At(0f));
        bool harm = damage > 0;
        float w = KitMotion.WarnFor(m.Owner, warn, harm);
        m.BuildBody(sprite, color, size, false, damage, 0.85f);
        Color wc = KitMotion.WarnColor(harm, color);
        // 予告の印（本体は予告の間、画面の外で止まっているので、子にしても動かない。予告が終わると消える）
        KitMotion.WarnArrow(m.transform, new Vector2(fromLeft ? a.xMin + 0.55f : a.xMax - 0.55f, worldY), fromLeft ? Vector2.right : Vector2.left, w, wc);
        if (wave <= 0.01f) KitMotion.WarnLine(m.transform, new Vector2(a.xMin, worldY), new Vector2(a.xMax, worldY), w, wc);
        else KitMotionMark.Create(m.transform, PlaceholderSprites.Square, new Vector2(a.center.x, worldY), new Vector2(a.width, wave * 2f + size.y),
            0f, new Color(wc.r, wc.g, wc.b, 0.14f), KitMotionItem.OrderWarn, w, "WaveBand");
        m.Setup(path, w, false);
        if (harm) KitMotion.Safe(GameAudio.Telegraph);
        return m;
    }

    /// <summary>
    /// 跳ねて入る（重さのある弧）：from から、横の速さ vx（＋で右）で落ちて、床（floorY＋half）で跳ねる。
    /// heights：跳ねるたびの高さ（床から体のまん中まで。だんだん低く）。左右の壁（area の内側 half）で跳ね返る。最後の弧の終わりで道が終わる。
    /// landings に、床に着く秒数（動き出してから）と x を入れる（印・飾り用。null なら入れない）
    /// </summary>
    public static KitPath HopPath(Vector2 from, float vx, float gravity, float[] heights, float floorY, float half, Rect area,
        System.Collections.Generic.List<Vector2> landings = null)
    {
        float g = Mathf.Max(1f, gravity);
        float ground = floorY + half;
        float L = area.xMin + half, R = area.xMax - half;
        // 区切り：最初の落ち（from の高さから、上向きの速さ 0）→ heights の弧
        int n = heights != null ? heights.Length : 0;
        var starts = new float[n + 2];   // 各区切りの始まりの秒数
        var vy0 = new float[n + 1];      // 各区切りの始まりの上向きの速さ
        var y0 = new float[n + 1];       // 各区切りの始まりの高さ
        float drop = Mathf.Max(0.05f, from.y - ground);
        y0[0] = from.y;
        vy0[0] = 0f;
        starts[0] = 0f;
        starts[1] = Mathf.Sqrt(2f * drop / g);
        for (int i = 0; i < n; i++)
        {
            float h = Mathf.Max(0.1f, heights[i]);
            float v = Mathf.Sqrt(2f * g * h);
            y0[i + 1] = ground;
            vy0[i + 1] = v;
            starts[i + 2] = starts[i + 1] + 2f * v / g;
        }
        float total = starts[n + 1];
        if (landings != null)
        {
            for (int i = 1; i <= n + 1; i++) landings.Add(new Vector2(starts[i], Fold(from.x + vx * starts[i], L, R, vx >= 0f)));
        }
        return new KitPath(t =>
        {
            int k = 0;
            while (k < n && t >= starts[k + 1]) k++;
            float tt = t - starts[k];
            float y = Mathf.Max(ground, y0[k] + vy0[k] * tt - 0.5f * g * tt * tt);
            return new Vector2(Fold(from.x + vx * t, L, R, vx >= 0f), y);
        }, total);
    }

    /// <summary>
    /// 左右の壁で跳ね返る x（x は跳ね返らないときの x）。movingRight：最初の向き。
    /// 入ってくる前（右へ進むなら left より左、左へ進むなら right より右）は、そのまま
    /// </summary>
    public static float Fold(float x, float left, float right, bool movingRight = true)
    {
        if (!movingRight) return -Fold(-x, -right, -left, true);
        float w = right - left;
        if (w <= 0.01f || x < left) return x;
        float u = Mathf.Repeat(x - left, 2f * w);
        return u <= w ? left + u : right - (u - w);
    }

    /// <summary>今のステージのギミック（そのアプリの StageRule。無ければ null）</summary>
    public static T Rule<T>() where T : StageRule => StageRules.Current as T;
}
