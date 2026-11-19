// Generic のスキン（表に無いアプリ。いつでも使える予備。docs/app-skill-visuals-design.md 10章）。
// partial で2つのファイルに分ける：枠の絵（Dress）は GenericSkin.Frames.cs（担当4）、当たった絵・エラーの絵（Hit・Error・通知）は GenericSkin.Hits.cs（担当1）。
public partial class GenericSkin : AppSkin
{
    public override SkinId Id => SkinId.Generic;
}
