using UnityEngine;

/// <summary>
/// 【仮】Premiere Pro のステージの「編集画面全体」の割り付け（2026-11-20。docs/premiere-edit-screen-design.md 1章・8章）。
/// 背景の絵（Tools/art/appbg/apps/PremierePro.json）の数を、ここ 1か所に持つ。ステージの再生ヘッド・ボスの技の再生ヘッド・書き出しのウィンドウ・モニターの映像が読む。
/// ・上の段（py 22〜75）：素材の一覧・プログラムモニター（映像の四角 px 120,24 80×45）・エフェクトの一覧。下の段：ルーラー（py 78〜87）と V2・V1・A1・A2
/// ・有効になる条件（Active）：スイッチ Enabled がオン ＋ 背景の絵のラベルが PremierePro ＋ アプリの UI の足場が出ている。どれか欠けたら今の数（部屋の上の端 − 1.9）
/// ・切り戻し：Enabled = false で、背景の絵と足場の表を前の物（ラベル PremiereProTimeline。全部がタイムラインの絵）にし、モニター・合図も出さない
/// 座標：背景の絵は 320×180 ドット・1マス 15 ドットで、カメラに付いて画面いっぱい（床の上の面 y 0 ＝ py 172.5、部屋の横のまん中＝ px 160。render.py と同じ）
/// </summary>
public static class PremiereEditScreen
{
    /// <summary>【仮】false にすると、前の Premiere Pro のステージ（全部がタイムライン）に戻る</summary>
    public static bool Enabled = true;

    /// <summary>背景の絵のラベル（AppBackgrounds）</summary>
    public const string Label = "PremierePro";
    /// <summary>切り戻しのときに読む、前の絵と足場の表のラベル</summary>
    public const string FallbackLabel = "PremiereProTimeline";

    /// <summary>ルーラーの上の端（背景の絵の py）。前の絵は py 31（部屋の上の端 − 1.9 で読んでいた）</summary>
    public const float RulerTopPy = 78f;
    const float FloorPy = 172.5f;

    // 背景の絵の座標（ドット）
    public const float VideoX = 120f, VideoY = 24f, VideoW = 80f, VideoH = 45f;
    public const float ScrubY = 71f, ScrubH = 2f;
    public const float TimecodeX = 99f, TimecodeY = 70f;
    public const float ExportX = 266f, ExportY = 52f;
    const float Ppu = 15f;

    /// <summary>今のステージが編集画面全体の割り付けか</summary>
    public static bool Active =>
        Enabled && AppBackgrounds.Current != null && AppBackgrounds.CurrentLabel == Label
        && AppStagePlatforms.Active && AppStagePlatforms.Label == Label;

    /// <summary>Resources から読むときのラベル（スイッチがオフなら、Premiere Pro は前の絵と表）</summary>
    public static string ResourceLabel(string label) => !Enabled && label == Label ? FallbackLabel : label;

    /// <summary>ルーラーの上の端（ワールドの y）。割り付けが無いときは「部屋の上の端 − fallbackInset」</summary>
    public static float RulerTop(Rect arena, float fallbackInset) => Active ? WorldFromPx(arena, 160f, RulerTopPy).y : arena.yMax - fallbackInset;

    /// <summary>白い柱の予告（赤い点線・うすい帯）を伸ばす上の端。割り付けが無いときは fallback（ルーラーの下の端）</summary>
    public static float MarkerTop(Rect arena, float fallback) => Active ? arena.yMax : fallback;

    /// <summary>背景の絵の座標（ドット）→ ワールド（部屋の床と横のまん中から。画面のゆれは入らない）</summary>
    public static Vector2 WorldFromPx(Rect arena, float px, float py) =>
        new Vector2(arena.center.x + (px - 160f) / Ppu, arena.yMin + (FloorPy - py) / Ppu);

    /// <summary>背景の絵の座標（ドット）→ 背景の絵の中の位置（絵のまん中が 0,0。マス）</summary>
    public static Vector2 LocalFromPx(float px, float py) => new Vector2((px - 160f) / Ppu, (90f - py) / Ppu);

    /// <summary>書き出しのウィンドウのまん中（右上のパネルの上。ワールド）</summary>
    public static Vector2 ExportCenter(Rect arena) => WorldFromPx(arena, ExportX, ExportY);

    /// <summary>ドット → マス</summary>
    public static float Units(float px) => px / Ppu;
}
