// 【仮】Generic のスキンの枠の絵（2026-11-18。担当4。docs/app-skill-visuals-design.md 10章の上の表・1-7 の Generic の行）。
// ここは Dress だけ。当たった絵・エラーの絵・通知の題名は GenericSkin.Hits.cs（担当1）。
// 絵の中身は GenericFrameShapes.cs（GenericFrameDresser と GenericFx〜）、道具は GenericFrameKit.cs。
// 枠は共通の表（SkinTables.FrameOf）から来る。None のときは、攻撃データの名前から決める（GenericFrameKit.Resolve）。
public partial class GenericSkin
{
    public override bool Dress(AttackFrame frame, DressPhase phase, DressInfo info)
        => GenericFrameDresser.Dress(GenericFrameKit.Resolve(frame, info.Data), phase, info);
}
