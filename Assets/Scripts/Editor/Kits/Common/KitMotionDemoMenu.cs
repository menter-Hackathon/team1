using UnityEditor;

/// <summary>
/// 【確認用・仮】KitMotion の部品を、プレイ中に1つずつ出すメニュー（2026-11-19。担当0 の物）。
/// 「Tools/Team1/KitMotion の試し/…」。プレイ中だけ押せる。プレイヤーのまわりに出る（だれの物でもないので、自分で消える）
/// </summary>
public static class KitMotionDemoMenu
{
    const string Root = "Tools/Team1/KitMotion の試し/";

    [MenuItem(Root + "全部を順に（4秒おき）")] static void All() => KitMotionDemo.PlayAll();
    [MenuItem(Root + "全部を順に（4秒おき）", true)] static bool AllOk() => EditorApplication.isPlaying;

    [MenuItem(Root + "Erupt（下から）")] static void P0() => KitMotionDemo.Play(0);
    [MenuItem(Root + "EdgeSweep（横から）")] static void P1() => KitMotionDemo.Play(1);
    [MenuItem(Root + "Diagonal（角から）")] static void P2() => KitMotionDemo.Play(2);
    [MenuItem(Root + "Orbit（回る）")] static void P3() => KitMotionDemo.Play(3);
    [MenuItem(Root + "Bounce（跳ねる）")] static void P4() => KitMotionDemo.Play(4);
    [MenuItem(Root + "FloorWave（床を走る）")] static void P5() => KitMotionDemo.Play(5);
    [MenuItem(Root + "WallCrawl（壁を這う）")] static void P6() => KitMotionDemo.Play(6);
    [MenuItem(Root + "Pop（その場に出る）")] static void P7() => KitMotionDemo.Play(7);
    [MenuItem(Root + "Conveyor（床が動く）")] static void P8() => KitMotionDemo.Play(8);
    [MenuItem(Root + "HitTarget（叩く）")] static void P9() => KitMotionDemo.Play(9);
    [MenuItem(Root + "Catch（取る）")] static void P10() => KitMotionDemo.Play(10);
    [MenuItem(Root + "HoldZone（立つ）")] static void P11() => KitMotionDemo.Play(11);
    [MenuItem(Root + "SafeSpot（逃げる）")] static void P12() => KitMotionDemo.Play(12);

    [MenuItem(Root + "Erupt（下から）", true)] static bool V0() => EditorApplication.isPlaying;
    [MenuItem(Root + "EdgeSweep（横から）", true)] static bool V1() => EditorApplication.isPlaying;
    [MenuItem(Root + "Diagonal（角から）", true)] static bool V2() => EditorApplication.isPlaying;
    [MenuItem(Root + "Orbit（回る）", true)] static bool V3() => EditorApplication.isPlaying;
    [MenuItem(Root + "Bounce（跳ねる）", true)] static bool V4() => EditorApplication.isPlaying;
    [MenuItem(Root + "FloorWave（床を走る）", true)] static bool V5() => EditorApplication.isPlaying;
    [MenuItem(Root + "WallCrawl（壁を這う）", true)] static bool V6() => EditorApplication.isPlaying;
    [MenuItem(Root + "Pop（その場に出る）", true)] static bool V7() => EditorApplication.isPlaying;
    [MenuItem(Root + "Conveyor（床が動く）", true)] static bool V8() => EditorApplication.isPlaying;
    [MenuItem(Root + "HitTarget（叩く）", true)] static bool V9() => EditorApplication.isPlaying;
    [MenuItem(Root + "Catch（取る）", true)] static bool V10() => EditorApplication.isPlaying;
    [MenuItem(Root + "HoldZone（立つ）", true)] static bool V11() => EditorApplication.isPlaying;
    [MenuItem(Root + "SafeSpot（逃げる）", true)] static bool V12() => EditorApplication.isPlaying;
}
