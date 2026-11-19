using UnityEngine;
using K = OfficeGameChatKit;

/// <summary>
/// 【仮】Office のスキン（2026-11-18。docs/app-skill-visuals-design.md 4章）。Word・Excel・PowerPoint・Keynote・Notion など。
/// ・能力：棒グラフ（4-1。OfficeBarGraph）。Office のプレイヤーの柱は、どれも棒グラフで描く（1-7 の終わり）
/// ・代用ボスの技（4-2。OfficeFrames）：円グラフの切れ端・折れ線・スペルチェックの波線・赤字の棒グラフ・「保存しますか？」・スライド・シートのタブ・セルの切り取り
/// ・当たった絵（4-3）：セルの選択の緑の枠。エラーの絵：#REF!。ダウン：Mac の強制終了のウィンドウ（2026-11-19）。通知の題名：Saved
/// 見た目だけ。当たり判定・秒数・ダメージは変えない
/// </summary>
public class OfficeSkin : AppSkin
{
    public override SkinId Id => SkinId.Office;
    public override string NotificationTitle => "Saved";

    public override bool Dress(AttackFrame frame, DressPhase phase, DressInfo info) =>
        K.Safe(() => DressCore(frame, phase, info), $"Office.{frame}.{phase}");

    static bool DressCore(AttackFrame frame, DressPhase phase, DressInfo info)
    {
        if (info.Target == null) return false;
        if (frame == AttackFrame.None) frame = K.FrameFromName(info.Data);
        if (!info.IsEnemy)
        {
            // プレイヤー：棒グラフ（と、Office に当たった柱のひな形）だけ。ほかは Generic
            if (frame == AttackFrame.Pillar || K.NameIs(info.Data, "BarGraph")) return OfficeBarGraph.Dress(phase, info);
            return false;
        }
        switch (frame)
        {
            case AttackFrame.SmallShot: return OfficeFrames.Pie(phase, info);
            case AttackFrame.LongShot: return OfficeFrames.LineShot(phase, info);
            case AttackFrame.Floor: return OfficeFrames.SpellFloor(phase, info);
            case AttackFrame.Pillar: return OfficeFrames.RedBar(phase, info);
            case AttackFrame.Window: return OfficeFrames.SaveWindow(phase, info);
            case AttackFrame.Falling: return OfficeFrames.SlideFall(phase, info);
            case AttackFrame.TopRow: return OfficeFrames.SheetTab(phase, info);
            case AttackFrame.Warp: return OfficeFrames.CutWarp(phase, info);
        }
        return false;
    }

    public override bool Hit(HitInfo info) => K.Safe(() => OfficeHits.Hit(info), "Office.Hit");
    public override bool Error(HitInfo info) => K.Safe(() => OfficeHits.Error(info), "Office.Error");
}

/// <summary>【仮】Office の当たった絵・エラーの絵（4-3）。絵だけを出す（ゆれ・ヒットストップ・白い輪は HitFx）</summary>
public static class OfficeHits
{
    /// <summary>当たった絵：相手のまわりにセルの選択の緑の枠が「カチッ」と付く。大は枠の上に緑のチェック</summary>
    public static bool Hit(HitInfo info)
    {
        Vector2 at = K.HitSpot(info);
        Vector2 dir = K.HitDir(info);
        var bits = new[] { OfficeArt.CellBit, OfficeArt.BarBit(OfficeArt.Series(0, info.Theme)), OfficeArt.BarBit(OfficeArt.Orange), OfficeArt.BarBit(OfficeArt.Yellow) };
        switch (info.Strength)
        {
            case HitStrength.Small:
                K.Debris(at, dir, bits, 1, 48);
                return true;
            case HitStrength.Light:
                Frame(at, 16, 10, false, 1f, 0.2f);
                K.Debris(at, dir, bits, 3, 48);
                return true;
            case HitStrength.Heavy:
                Frame(at, 26, 16, true, 1f, 0.3f);
                K.Debris(at, dir, bits, 6, 48);
                return true;
            case HitStrength.Finisher:
                Frame(at, 26, 16, true, 1.5f, 0.45f);
                K.Debris(at, dir, bits, 12, 48, 70f);
                return true;
        }
        return false;
    }

    static void Frame(Vector2 at, int w, int h, bool check, float scale, float life)
    {
        var fx = K.Fx("OfficeHit", at, life + 0.05f);
        var node = K.Node(fx.transform, "Frame", Vector2.zero);
        K.Show(node, OfficeArt.CellFrame(w, h, OfficeArt.Select, true), Vector2.zero, 47, "Cell");
        if (check) K.Show(node, OfficeArt.CheckOutlined(OfficeArt.Select), new Vector2(0f, (h * 0.5f + 5f) * K.Dot), 48, "Check");
        // 大きめから「カチッ」と縮んで付く
        fx.Play(K.PopRise(fx, node, life, 0.4f, 0.06f, scale, 1.35f));
    }

    /// <summary>
    /// エラーの絵：白いセルに赤い字 #REF!（頭の上）。
    /// ダウン：Mac の強制終了のウィンドウ（Force Quit・アプリのアイコンと赤い Not Responding・上に待ちカーソル。画面のまん中寄り）。
    /// 2倍にしない（幅 9マスまで。2026-11-19 docs/mac-look-design.md 23・1-9）
    /// </summary>
    public static bool Error(HitInfo info)
    {
        Bounds body = K.BodyOf(info.Target, info.Point);
        Vector2 head = new Vector2(body.center.x, body.max.y + 0.6f);
        // 赤いかけらは HitFx が出す（ここは絵だけ）
        if (info.Strength == HitStrength.Down)
        {
            Rect view = K.View();
            Vector2 at = Vector2.Lerp(head, view.center, 0.5f);
            var fx = K.Fx("OfficeDown", at, 1.4f, null, true);
            var node = K.Node(fx.transform, "Dialog", Vector2.zero);
            MacUi.PlaceForceQuit(node, Vector2.zero, info.AppIcon, 48);
            K.KeepInView(fx.transform, node, 0.3f, 0.2f);
            fx.Play(K.PopRise(fx, node, 1.4f, 0.2f, 0.12f));
            return true;
        }
        var efx = K.Fx("OfficeError", head, 0.45f);
        var n = K.Node(efx.transform, "Cell", Vector2.zero);
        K.Show(n, OfficeArt.ErrorCell("#REF!"), Vector2.zero, 49, "Cell");
        efx.Play(K.PopRise(efx, n, 0.4f, 0.25f, 0.1f));
        return true;
    }
}
