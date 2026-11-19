using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Zoom のボスの技「ギャラリービュー」（2026-11-19。docs/app-kits-1-design.md 18章 P1-1）。Custom 小〜中：ZoneStrike（担当3）。
/// 部屋が 横3×縦2 の 6つのタイルに分かれる（黒い線・左下に名札・右上にマイク。下の段＝床から 0〜2.8、上の段＝2.8〜8。足場の上は上の段）→
/// 緑の枠（話している人）が interval 秒おきに count 個のタイルに順に付く → 付いた順に、タイルが赤くなって（予告 0.8秒）、判定 0.4秒。
/// ・選び方：1つ目はプレイヤーのいるタイル。下の段の 3つが全部選ばれることはない（どこかの下のタイルは、いつも安全）
/// ・避け方：枠の付いていないタイルへ。下の段が赤なら、となりのタイルか、足場に乗って上の段へ
/// ・判定の数値（ダメージなど）はこの技の攻撃データ。大きさ・予告・判定の時間は ZoneStrike が区画から作る
/// 第2形態：4タイル
/// </summary>
[CreateAssetMenu(fileName = "Zoom_Gallery", menuName = "Team1/Kits/Zoom Gallery (boss)")]
public class ZoomGalleryAttack : CustomAttack
{
    [Tooltip("打つタイルの数")]
    [SerializeField, Range(1, 5)] int _count = 3;
    [Tooltip("緑の枠が付く間隔（秒）")]
    [SerializeField, Min(0.2f)] float _interval = 0.5f;
    [Tooltip("緑の枠が付いてから赤くなるまで（秒）")]
    [SerializeField, Min(0f)] float _frameLead = 0.5f;
    [Tooltip("下の段の上の端（床から。マス）")]
    [SerializeField, Min(1f)] float _bottomTop = 2.8f;
    [Tooltip("上の段の上の端（床から。マス）")]
    [SerializeField, Min(2f)] float _top = 8f;

    public const int Cols = 3;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        Rect area = ZoneStrike.AreaFor(owner);
        float floor = ZoneStrike.FloorY(owner);
        Rect[] tiles = ZoneStrike.Grid(area, Cols, floor, _bottomTop, _top);
        List<int> picks = Pick(tiles, owner.TargetPoint ?? area.center);

        float total = _frameLead + (picks.Count - 1) * _interval + ZoneStrike.DefaultTelegraph + ZoneStrike.DefaultActive;
        ZoomGalleryGrid.Spawn(owner, area, floor, tiles, picks, _interval, _frameLead, total + 0.2f);
        Color hit = CallKitUtil.Alpha(ZoomKitArt.LeaveRed, 0.55f);
        for (int k = 0; k < picks.Count; k++)
            ZoneStrike.Strike(owner, data, tiles[picks[k]], _frameLead + k * _interval, ZoneStrike.DefaultTelegraph, ZoneStrike.DefaultActive, null, hit);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(total);
    }

    List<int> Pick(Rect[] tiles, Vector2 player)
    {
        var picks = new List<int> { Mathf.Max(0, ZoneStrike.IndexOf(tiles, player)) };
        var rest = new List<int>();
        for (int i = 0; i < tiles.Length; i++) if (!picks.Contains(i)) rest.Add(i);
        int n = Mathf.Min(_count, tiles.Length - 1);
        while (picks.Count < n && rest.Count > 0)
        {
            int k = Random.Range(0, rest.Count);
            int c = rest[k];
            rest.RemoveAt(k);
            int bottoms = 0;
            foreach (int p in picks) if (p < Cols) bottoms++;
            if (c < Cols && bottoms >= Cols - 1) continue; // 下の段は、全部は選ばない
            picks.Add(c);
        }
        return picks;
    }
}

/// <summary>ギャラリービューの格子（黒い線・名札・マイク）と、話している人の緑の枠。形態変化でも消える</summary>
public class ZoomGalleryGrid : CustomEffect
{
    const int GridOrder = 13, TagOrder = 14, FrameOrder = 19;
    float _born, _endAt, _interval, _frameLead;
    readonly List<SpriteRenderer> _fixed = new List<SpriteRenderer>();
    readonly List<float> _fixedA = new List<float>();
    readonly List<SpriteRenderer> _frames = new List<SpriteRenderer>();

    public static ZoomGalleryGrid Spawn(AttackRunner owner, Rect area, float floor, Rect[] tiles, List<int> picks, float interval, float frameLead, float seconds)
    {
        GameObject go = owner.CreateSpawnedObject("Zoom_GalleryGrid", Vector2.zero);
        go.transform.position = Vector3.zero;
        var g = go.AddComponent<ZoomGalleryGrid>();
        g._born = Time.time;
        g._endAt = Time.time + seconds;
        g._interval = interval;
        g._frameLead = frameLead;
        g.Build(area, floor, tiles, picks);
        return g;
    }

    void Build(Rect area, float floor, Rect[] tiles, List<int> picks)
    {
        float top = 0f;
        foreach (var t in tiles) top = Mathf.Max(top, t.yMax);
        Color line = new Color(0.04f, 0.04f, 0.05f, 0.85f);
        // 縦の線
        for (int c = 1; c < ZoomGalleryAttack.Cols; c++)
        {
            float x = area.xMin + area.width * c / ZoomGalleryAttack.Cols;
            AddFixed(ZoomKitArt.Put(transform, PlaceholderSprites.Square, new Vector2(x, (floor + top) * 0.5f), new Vector2(3f * CallArt.Px, top - floor), line, GridOrder, "VLine"), line.a);
        }
        // 横の線（段の境目と、上の端）
        var ys = new HashSet<float>();
        foreach (var t in tiles) { if (t.yMin > floor + 0.01f) ys.Add(t.yMin); ys.Add(t.yMax); }
        foreach (float y in ys)
            AddFixed(ZoomKitArt.Put(transform, PlaceholderSprites.Square, new Vector2(area.center.x, y), new Vector2(area.width, 3f * CallArt.Px), line, GridOrder, "HLine"), line.a);
        // タイルごとの名札（左下）とマイク（右上）
        for (int i = 0; i < tiles.Length; i++)
        {
            Rect t = tiles[i];
            AddFixed(ZoomKitArt.Put(transform, ZoomArt.NameTag(), new Vector2(t.xMin + 0.55f, t.yMin + 0.3f), new Vector2(12f, 5f) * CallArt.Px * 1.5f, new Color(1f, 1f, 1f, 0.8f), TagOrder, "Tag"), 0.8f);
            AddFixed(ZoomKitArt.Put(transform, ZoomArt.Mic(), new Vector2(t.xMax - 0.35f, t.yMax - 0.35f), new Vector2(3f, 4f) * CallArt.Px * 2f, new Color(1f, 1f, 1f, 0.8f), TagOrder, "Mic"), 0.8f);
        }
        // 話している人の緑の枠（順に付く）
        foreach (int p in picks)
        {
            Rect t = tiles[p];
            int wp = Mathf.Clamp(Mathf.RoundToInt(t.width * CallArt.Ppu), 4, 300);
            int hp = Mathf.Clamp(Mathf.RoundToInt(t.height * CallArt.Ppu), 4, 300);
            SpriteRenderer f = ZoomKitArt.Put(transform, CallArt.Frame(wp, hp, 3), t.center, t.size, ZoomKitArt.Green, FrameOrder, "Speaking");
            f.enabled = false;
            _frames.Add(f);
        }
        Apply();
    }

    void AddFixed(SpriteRenderer sr, float a)
    {
        _fixed.Add(sr);
        _fixedA.Add(a);
    }

    void Update()
    {
        if (Time.time >= _endAt)
        {
            Destroy(gameObject);
            return;
        }
        Apply();
    }

    void Apply()
    {
        float t = Time.time - _born;
        float k = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((_endAt - Time.time) / 0.25f);
        for (int i = 0; i < _fixed.Count; i++)
        {
            if (_fixed[i] == null) continue;
            Color c = _fixed[i].color;
            c.a = _fixedA[i] * k;
            _fixed[i].color = c;
        }
        for (int i = 0; i < _frames.Count; i++)
        {
            if (_frames[i] == null) continue;
            float on = i * _interval;
            float off = on + _frameLead + ZoneStrike.DefaultTelegraph + ZoneStrike.DefaultActive;
            _frames[i].enabled = t >= on && t < off;
        }
    }
}
