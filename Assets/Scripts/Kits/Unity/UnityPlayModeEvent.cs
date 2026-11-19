using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Unity のステージの出来事「Play Mode」（場面③。2026-11-19。docs/stage-gimmick-variety-c.md 1章）。
/// ・はじめ：画面がうすく青く（Play Mode の色。10％）・上のまん中に再生の印が青く光る（13秒で戻る）
/// ・Diagonal（主）：移動のギズモの矢印（X 赤・Y 緑・Z 青を順に）。上の左右の角・左右の壁のまん中（高さ 4）の4か所から順に、
///   プレイヤーへ予告の線（0.8秒）→ 速さ 9 で飛ぶ。2.2秒おきに 4／5／6本。強さ 3 は最後だけ左右の角から同時に 2本
/// ・Erupt：矢印の合間（4.3秒・8.7秒。矢印の 2.2秒おきのまん中）に、床の格子からプリミティブ（灰色の柱。幅 1.6・高さ 2.2）。
///   プレイヤーの x と、そこから ±4 の片方（強さ 3 は両方）。予告 1.0秒 → 0.25秒で上がる → 0.8秒止まる。床だけ（足場の上には出ない）
/// ・UnityRule の 3D の物体は、場面③の間は出ない（ctx.CanSpawnHazard が false。決まりの側は直さない）
/// ・矢印は KitMotion.Diagonal。向きを飛ぶ向きに回し、当たりを細長くする（Diagonal は絵を回さないので、出来事の側で）
/// </summary>
public class UnityPlayModeEvent : KitTimedEvent
{
    const float ArrowEvery = 2.2f, ArrowSpeed = 9f, ArrowWarn = 0.8f, ArrowSize = 0.9f, WallHeight = 4f;
    const float EruptWidth = 1.6f, EruptHeight = 2.2f, EruptWarn = 1f, EruptRise = 0.25f, EruptHold = 0.8f, EruptSide = 4f;
    static readonly Color Tint = new Color(0.24f, 0.52f, 0.92f, 0.1f);

    readonly List<KitMotionGroup> _arrows = new List<KitMotionGroup>();
    readonly HashSet<KitMotionItem> _dressed = new HashSet<KitMotionItem>();
    SpriteRenderer _tint;
    float _tintTarget;

    public UnityPlayModeEvent() : base("Play Mode") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(UnityKit.Label, stage => new UnityPlayModeEvent());

    protected override void Build()
    {
        int n = Level >= 3 ? 6 : Level == 2 ? 5 : 4;
        At(0f, ShowPlayMode);
        Every(1f, ArrowEvery, n, i => Shoot(i, Level >= 3 && i == n - 1));
        At(1f + ArrowEvery * 1.5f, EruptRound); // 4.3秒
        At(1f + ArrowEvery * 3.5f, EruptRound); // 8.7秒
        At(13f, () => _tintTarget = 0f);
        EndAt(14f);
    }

    /// <summary>Play Mode の見た目：画面のうすい青と、上の再生の印</summary>
    void ShowPlayMode()
    {
        Transform cam = FxKit.CameraChild("UnityPlayModeTint");
        if (cam != null)
        {
            Vector2 view = FxKit.ViewSize();
            _tint = AttackRunner.AddVisual(cam, PlaceholderSprites.Square, new Color(Tint.r, Tint.g, Tint.b, 0f), view + Vector2.one * 4f, 9, "Tint");
            _tintTarget = Tint.a;
            Track(cam.gameObject);
        }
        Rect a = KitMotion.Area;
        KitMotionMark play = KitMotionMark.Create(null, KitMotionArt.Arrow, new Vector2(a.center.x, a.yMax - 1.2f), Vector2.one * 0.75f, 0f,
            UnityArt.AxisZ, KitMotionItem.OrderMark, 13f, "UnityPlayButton");
        Track(play.gameObject);
    }

    void Shoot(int i, bool both)
    {
        Color c = (i % 3) == 0 ? UnityArt.AxisX : (i % 3) == 1 ? UnityArt.AxisY : UnityArt.AxisZ;
        if (both)
        {
            Fire(KitMotion.Corner(KitCorner.TopLeft), c);
            Fire(KitMotion.Corner(KitCorner.TopRight), c);
            return;
        }
        Rect a = KitMotion.Area;
        float wallY = KitMotion.FloorY() + WallHeight;
        Vector2 from = (i % 4) switch
        {
            0 => KitMotion.Corner(KitCorner.TopLeft),
            1 => KitMotion.Corner(KitCorner.TopRight),
            2 => new Vector2(a.xMin + 0.6f, wallY),
            _ => new Vector2(a.xMax - 0.6f, wallY),
        };
        Fire(from, c);
    }

    void Fire(Vector2 from, Color c)
    {
        KitMotionGroup g = KitMotion.Diagonal(Owner, from, ArrowSpeed, 1, 0f, ArrowWarn, GizmoArrow, c, 1, ArrowSize);
        if (g != null) _arrows.Add(g);
    }

    void EruptRound()
    {
        Rect a = KitMotion.Area;
        float lo = a.xMin + 1f, hi = a.xMax - 1f;
        float px = Mathf.Clamp(KitMotion.PlayerPos.x, lo, hi);
        var xs = new List<float> { px };
        bool leftOk = px - EruptSide >= lo, rightOk = px + EruptSide <= hi;
        if (Level >= 3)
        {
            if (leftOk) xs.Add(px - EruptSide);
            if (rightOk) xs.Add(px + EruptSide);
        }
        else if (leftOk && rightOk) xs.Add(px + (Random.value < 0.5f ? -EruptSide : EruptSide));
        else if (leftOk) xs.Add(px - EruptSide);
        else if (rightOk) xs.Add(px + EruptSide);
        foreach (float x in xs)
            KitMotion.Erupt(Owner, x, EruptWidth, EruptHeight, EruptWarn, EruptRise, EruptHold, null, UnityArt.Object);
    }

    protected override void OnFrame(float clock)
    {
        if (_tint != null)
        {
            Color c = _tint.color;
            c.a = Mathf.MoveTowards(c.a, _tintTarget, Time.deltaTime * 0.2f);
            _tint.color = c;
        }
        // 飛び出した矢印：飛ぶ向きに回して、当たりを細長くする
        _arrows.RemoveAll(g => g == null || g.IsDone);
        foreach (var g in _arrows)
        {
            foreach (var item in g.Items)
            {
                if (!(item is KitMotionMover m) || m.Path == null || !_dressed.Add(m)) continue;
                Vector2 d = m.Path.At(0.1f) - m.Path.At(0f);
                if (d.sqrMagnitude > 1e-6f) m.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                if (m.Body != null && m.Body.TryGetComponent(out BoxCollider2D box)) box.size = new Vector2(ArrowSize * 0.85f, ArrowSize * 0.32f);
            }
        }
        _dressed.RemoveWhere(x => x == null);
    }

    /// <summary>移動のギズモの矢印（右向き。14×14 の中に、細い軸と三角の頭。白で、色は Diagonal の色で塗る）</summary>
    static Sprite GizmoArrow => RuntimeSprites.FromAscii("UnityEvent_GizmoArrow", new[]
    {
        "..............",
        "..............",
        "..............",
        "..........X...",
        "..........XX..",
        "..........XXX.",
        "XXXXXXXXXXXXXX",
        "XXXXXXXXXXXXXX",
        "..........XXX.",
        "..........XX..",
        "..........X...",
        "..............",
        "..............",
        "..............",
    }, new Dictionary<char, Color32> { { 'X', new Color32(255, 255, 255, 255) } });
}
