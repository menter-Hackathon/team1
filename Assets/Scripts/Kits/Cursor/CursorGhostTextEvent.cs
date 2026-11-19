using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Cursor の場面③の出来事「ゴーストテキスト」（2026-11-19。docs/stage-gimmick-variety-b.md 13章）。数値はすべて【仮】。
/// ・うすい紫がかった灰色の文字のかたまり（横 2.5×縦 0.6。右に「Tab」の札）が、右の端から左へ、ゆっくり波打って流れてくる
///   （KitMotion.Catch の道 KitPath.Sweep・速さ 3.5・振れ幅 ±0.4）。高さは床から 1.2／3.8／5.8 を順に。2.2秒おきに 5個
/// ・取る（触る）と「Tab ✓」：1個ごとに CursorRule の Tab の候補を取ったのと同じごほうび（能力のクールタイムを全部戻す）。予告なし（遅い・良い物）。
///   右の端に「▌」のカーソルが点滅
/// ・その間に、赤っぽいオレンジの行（まちがいのコード。横 3×縦 0.5。左に「✕」）が、左の端から右へ、まっすぐ速く横切る（速さ 11）。
///   高さは「次のゴーストテキストと同じ段」。3秒おきに 4本（ステージで 3〜5本）→ 取りに行くか、待つかを選ぶ
/// ・赤い行の予告：左の端に矢印と赤い線＋行の番号が赤く光る（0.8秒。1本目は 1.0秒）
/// ・出来事の間、CursorRule の Tab の候補は新しく置かれない（CursorRule 側で場面③を見る）
/// </summary>
public class CursorGhostTextEvent : KitTimedEvent
{
    static readonly float[] Lanes = { 1.2f, 3.8f, 5.8f };
    static readonly Vector2 GhostSize = new Vector2(2.5f, 0.6f);
    static readonly Vector2 RowSize = new Vector2(3f, 0.5f);
    const int Ghosts = 5;
    const float GhostStep = 2.2f, GhostSpeed = 3.5f, GhostWave = 0.4f, GhostCatchSize = 2f;
    const float RedStart = 1f, RedSpeed = 11f, FirstWarn = 1f, Warn = 0.8f;
    static readonly Color Ghost = new Color32(0xA8, 0xA2, 0xC0, 0xFF);   // 紫がかった灰色（提案の箱と同じ系統）

    readonly int _stage;
    int _caught;

    public CursorGhostTextEvent(int stage) : base("Cursor Tab")
    {
        _stage = stage;
    }

    protected override void Build()
    {
        for (int i = 0; i < Ghosts; i++)
        {
            int n = i;
            At(i * GhostStep, () => SendGhost(n));
        }
        int reds = GroupBMotion.Scaled(4, _stage);
        float redStep = reds <= 4 ? 3f : 2.4f;
        float lastRed = 0f;
        for (int k = 0; k < reds; k++)
        {
            float t = RedStart + k * redStep;
            int ghost = Mathf.Min(Ghosts - 1, Mathf.CeilToInt(t / GhostStep)); // 次に来るゴーストテキストの段
            float warn = k == 0 ? FirstWarn : Warn;
            int line = 12 + k * 7;
            At(t, () => SendError(ghost, warn, line));
            lastRed = t;
        }
        float redCross = (View.width + 2f * (RowSize.x * 0.5f + 0.3f)) / RedSpeed;
        // ゴーストテキストは遅い（横切るのに 6秒くらい）ので、最後の 1個は流れている途中で終わる（良い物なので、そのまま消える）
        EndAt(Mathf.Max(lastRed + Warn + redCross, (Ghosts - 1) * GhostStep + 3.4f) + 0.2f);
    }

    float LaneY(int ghostIndex) => Floor + Lanes[ghostIndex % Lanes.Length];

    /// <summary>ゴーストテキスト（良い物）：右の端から、ゆっくり波打って</summary>
    void SendGhost(int index)
    {
        float y = LaneY(index);
        KitPath path = KitPath.Sweep(KitSide.Right, y, GhostSpeed, GhostWave, 4f, GhostSize.x * 0.5f + 0.4f, View);
        KitMotionCatch c = KitMotion.Catch(Owner, path, OnTab, GhostSprite, Color.white, GhostCatchSize);
        if (c != null && c.Visual != null)
        {
            // 触る広さは広め（2×2）、絵は 2.5×0.6 に戻す
            Vector2 native = c.Visual.sprite.bounds.size;
            c.Visual.transform.localScale = new Vector3(GhostSize.x / Mathf.Max(0.01f, native.x), GhostSize.y / Mathf.Max(0.01f, native.y), 1f);
            Transform key = CursorArt.TabKey(c.Body, new Vector2(GhostSize.x * 0.5f + 0.45f, 0f), KitMotionItem.OrderBody + 1, Ghost);
            key.localScale = Vector3.one * 0.6f;
        }
        // 右の端に、点滅するカーソル「▌」
        KitMotionMark cursor = KitMotionMark.Create(null, PlaceholderSprites.Square, new Vector2(View.xMax - 0.4f, y), new Vector2(0.14f, 0.6f), 0f,
            CursorArt.Purple, KitMotionItem.OrderMark, 1f, "Cursor_Caret");
        if (cursor != null) Track(cursor.gameObject);
        GameAudio.Event("gm_cursor_tab_appear");
    }

    void OnTab()
    {
        _caught++;
        KitPlayer.RefillCooldowns();
        GameAudio.Event("gm_cursor_accept");
        Vector2 p = KitMotion.PlayerPos;
        FxKit.Text("Tab", p + new Vector2(-0.3f, 1.3f), 0.42f, Color.white, 60, 0.7f, CursorArt.Purple).Rise(0.4f);
        FxAnim.Sprite(FxArt.Check, p + new Vector2(0.55f, 1.3f), 61, 0.7f, CursorArt.Green, 1.4f, "Cursor_TabCheck").Pop().Rise(0.4f);
    }

    /// <summary>まちがいのコードの行（危ない物）：左の端から右へ、まっすぐ速く。高さは次のゴーストテキストと同じ段</summary>
    void SendError(int ghostIndex, float warn, int line)
    {
        float y = LaneY(ghostIndex);
        KitMotionMover m = GroupBMotion.SweepRect(Owner, KitSide.Left, y, RedSpeed, RowSize, ErrorSprite, Color.white, warn, objectName: "Cursor_ErrorRow");
        if (m == null) return;
        // 左の端の、行の番号が赤く光る（背景の行の番号の所）
        FxKit.Text(line.ToString(), new Vector2(View.xMin + 0.45f, y + 0.45f), 0.36f, CursorArt.Red, KitMotionItem.OrderMark, m.Warn, Color.black);
    }

    /// <summary>ゴーストテキスト（45×11 ドット）：うすい文字の棒（白で作る。色は少し紫がかった灰色）</summary>
    static Sprite GhostSprite => KitArt.Paint("cursor.event.ghost", 45, 11, c =>
    {
        Color32 ink = new Color32(0xA8, 0xA2, 0xC0, 0xC8), faint = new Color32(0xA8, 0xA2, 0xC0, 0x70);
        c.Fill(0, 0, 45, 11, new Color32(0x30, 0x2C, 0x40, 0x60));
        c.Fill(2, 3, 9, 2, ink);
        c.Fill(13, 3, 14, 2, ink);
        c.Fill(29, 3, 12, 2, faint);
        c.Fill(2, 7, 18, 2, faint);
        c.Fill(22, 7, 10, 2, ink);
    });

    /// <summary>まちがいのコードの行（54×9 ドット＝3×0.5 マス）：赤っぽいオレンジの帯に、左に「✕」</summary>
    static Sprite ErrorSprite => KitArt.Paint("cursor.event.error", 54, 9, c =>
    {
        Color32 row = new Color32(0xFF, 0x6B, 0x3D, 0xFF), dark = new Color32(0x8A, 0x24, 0x10, 0xFF), white = new Color32(255, 255, 255, 255);
        c.Fill(0, 0, 54, 9, dark);
        c.Fill(1, 1, 52, 7, row);
        for (int i = 0; i < 5; i++) { c.Px(2 + i, 2 + i, white); c.Px(6 - i, 2 + i, white); } // ✕
        c.Fill(11, 3, 16, 2, dark);
        c.Fill(29, 3, 10, 2, dark);
        c.Fill(41, 3, 8, 2, dark);
    });
}
