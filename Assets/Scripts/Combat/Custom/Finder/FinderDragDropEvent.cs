using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Finder の場面③の出来事「ドラッグ＆ドロップ」（2026-11-19。docs/stage-gimmick-variety-a.md 2章 Finder）。FinderKit が登録する（スキンの雨 Copying... と入れ替え）。
/// ・画面の上の左右の角と、左右の壁のまん中（4点）から、順番に 1つずつ、ファイルのアイコン（灰色の書類・半透明 70％＝つかんでいる途中）が
///   その時のプレイヤーの場所へ飛ぶ（KitMotion.Diagonal）。同じ所から続けて出さない
/// ・予告 0.9（赤い線）→ 毎秒 9マス。間は 1.6／1.3／1.0秒、数 6／8／10（強さ 1／2／3）【仮】。9.1秒より後には出さない
/// ・4秒目と 8秒目：青いフォルダが左の壁から右へ、下の段の足場の少し上をゆっくり横切る（KitMotion.Catch。毎秒 3マス）。
///   触ると、ゴミ箱に 2体ぶん入る（FinderTrash.Feed。「ゴミ箱を空にする」に近づく）。乗ると落ちる足場（MEMO）の高さは通らない
/// ・飛んだファイルはゴミ箱の数に入れない（落下物ではないので、ゴミ箱は吸わない）。隅の通知は Moving...
/// </summary>
public class FinderDragDropEvent : KitTimedEvent
{
    const float Warn = 0.9f, Speed = 9f, Size = 0.8f, LastShot = 9.1f, FolderSpeed = 3f, FolderSize = 0.95f;
    const int FolderDocs = 2;

    public FinderDragDropEvent() : base("Moving...") { }

    protected override void Build()
    {
        int count = Level <= 1 ? 6 : Level == 2 ? 8 : 10;
        float interval = Level <= 1 ? 1.6f : Level == 2 ? 1.3f : 1.0f;
        int last = -1;
        for (int i = 0; i < count; i++)
        {
            float at = i * interval;
            if (at > LastShot) break;
            int from = Random.Range(0, 4);
            if (from == last) from = (from + 1 + Random.Range(0, 3)) % 4; // 同じ所から続けて出さない
            last = from;
            int point = from;
            At(at, () => Throw(point));
        }
        At(4f, Folder);
        At(8f, Folder);
        EndAt(11.2f);
    }

    /// <summary>出る所：0＝左上の角、1＝右上の角、2＝左の壁のまん中、3＝右の壁のまん中</summary>
    Vector2 FromPoint(int i)
    {
        Rect a = View;
        float floor = Floor;
        float midY = floor + (a.yMax - floor) * 0.5f;
        switch (i)
        {
            case 0: return KitMotion.Corner(KitCorner.TopLeft, 0.8f);
            case 1: return KitMotion.Corner(KitCorner.TopRight, 0.8f);
            case 2: return new Vector2(a.xMin + 0.6f, midY);
            default: return new Vector2(a.xMax - 0.6f, midY);
        }
    }

    void Throw(int point)
    {
        KitMotion.Diagonal(Owner, FromPoint(point), Speed, 1, 0f, Warn, DocSprite, new Color(1f, 1f, 1f, 0.7f), 1, Size);
    }

    void Folder()
    {
        Rect a = View;
        float floor = Floor;
        float y = floor + GimmickA.LowTier(a, floor, 3f) + FolderSize * 0.5f + 0.1f; // 下の段の足場の少し上（MEMO の高さは通らない）
        KitPath path = KitPath.Sweep(KitSide.Left, y, FolderSpeed, 0f, 4f, 1f, a);
        KitMotion.Catch(Owner, path, () =>
        {
            FinderTrash trash = FinderTrash.Current;
            if (trash != null) trash.Feed(KitMotion.PlayerPos, FolderDocs);
            else KitPlayer.AdvanceCooldowns(1f);
        }, FolderSprite, Color.white, FolderSize);
    }

    /// <summary>灰色の書類（12×14。右上が折れている・文字の行）</summary>
    static Sprite DocSprite
    {
        get
        {
            char[,] g = GimmickA.Grid(12, 14);
            GimmickA.Fill(g, 0, 0, 9, 14, 'O');
            GimmickA.Fill(g, 0, 3, 12, 11, 'O');
            GimmickA.Fill(g, 1, 1, 7, 12, 'W');
            GimmickA.Fill(g, 1, 4, 10, 9, 'W');
            GimmickA.Fill(g, 8, 1, 1, 3, 'F');
            GimmickA.Fill(g, 8, 3, 3, 1, 'F');
            for (int r = 0; r < 4; r++) GimmickA.Fill(g, 3, 5 + r * 2, r % 2 == 0 ? 6 : 4, 1, 'L');
            var pal = new Dictionary<char, Color32>
            {
                { 'O', RuntimeSprites.Hex("6E6E73") }, { 'W', RuntimeSprites.Hex("D1D1D6") }, { 'F', RuntimeSprites.Hex("AEAEB2") }, { 'L', RuntimeSprites.Hex("8E8E93") },
            };
            return RuntimeSprites.FromAscii("FinderDragDoc", GimmickA.Rows(g), pal);
        }
    }

    /// <summary>青いフォルダ（16×12。Finder の青）</summary>
    static Sprite FolderSprite
    {
        get
        {
            char[,] g = GimmickA.Grid(16, 12);
            GimmickA.Fill(g, 0, 0, 7, 3, 'D');
            GimmickA.Fill(g, 0, 2, 16, 10, 'D');
            GimmickA.Fill(g, 1, 3, 14, 1, 'L');
            GimmickA.Fill(g, 1, 4, 14, 7, 'B');
            GimmickA.Fill(g, 1, 4, 14, 1, 'L');
            var pal = new Dictionary<char, Color32>
            {
                { 'D', RuntimeSprites.Hex("2F7FC4") }, { 'B', RuntimeSprites.Hex("5AB0F0") }, { 'L', RuntimeSprites.Hex("8FCBF7") },
            };
            return RuntimeSprites.FromAscii("FinderDragFolder", GimmickA.Rows(g), pal);
        }
    }
}
