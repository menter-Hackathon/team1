using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Blender のステージの出来事「G・S」（場面③。2026-11-19。docs/stage-gimmick-variety-c.md 2章）。
/// ・前半（G＝つかんで動かす）：Conveyor（主）。床ぜんぶが 2.5マス/秒で右へ流れる → 止まって矢印が点滅 → 左へ流れる。足場の上は流れない（休める）
/// ・後半（S＝大きさを変える）：Pop。2.0秒おきに 3回、プレイヤーの今の場所と、低い足場の上の 1か所（強さ 3 は床の上にもう1か所）に、
///   オレンジの小さい点線の四角が当たる大きさ（2.2）へ広がる予告 → 立方体・球・サルの頭の代わりの丸が奥から大きくなって出る → 0.7秒で消える
/// ・設計は 16秒だったが、StagePacer の打ち切り（20秒）とダメージの止まりがあるので 13.5秒に詰めた（Conveyor 2.7＋2.2秒・Pop の間 2.0秒）
/// ・BlenderRule の立方体の出方（BlenderGrowFrame・BlenderPopIn）も、このファイルの部品を使う
/// </summary>
public class BlenderGrabScaleEvent : KitTimedEvent
{
    const float ConveyorSpeed = 2.5f, ConveyorWarn = 0.8f, ConveyorWidth = 18f;
    const float PopEvery = 2f, PopWarn = 1f, PopLife = 0.7f, PopSize = 2.2f;

    public BlenderGrabScaleEvent() : base("Grab / Scale") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(BlenderKit.Label, stage => new BlenderGrabScaleEvent());

    protected override void Build()
    {
        At(0f, () => ShowKey("G"));
        At(0f, () => Conveyor(ConveyorSpeed, 2.7f));   // 右へ（0.8〜3.5秒）
        At(3.5f, () => Conveyor(-ConveyorSpeed, 2.2f)); // 止まって矢印の点滅（0.8秒）→ 左へ（4.3〜6.5秒）
        At(6.5f, () => ShowKey("S"));
        Every(7f, PopEvery, 3, PopRound);
        EndAt(13.5f);
    }

    void Conveyor(float speed, float seconds)
    {
        Rect a = KitMotion.Area;
        KitMotion.Conveyor(Owner, a.center.x, Mathf.Min(ConveyorWidth, a.width - 1f), speed, seconds, ConveyorWarn, BlenderArt.Select);
    }

    /// <summary>左下に小さいキーの札（0.6秒。飾り）</summary>
    void ShowKey(string letter)
    {
        Rect a = KitMotion.Area;
        var go = new GameObject("BlenderEventKey");
        go.transform.position = new Vector2(a.xMin + 1.2f, KitMotion.FloorY() + 1.2f);
        BlenderArt.Key(go.transform, letter, Vector2.zero, KitMotionItem.OrderMark);
        Track(go);
        Object.Destroy(go, 0.6f);
    }

    void PopRound(int round)
    {
        float floor = KitMotion.FloorY();
        Vector2 player = KitMotion.PlayerPos;
        var spots = new List<Vector2> { player };
        // 低い足場の上（プレイヤーのいない方）
        if (FindLowPlatform(player, out Vector2 onPlat)) spots.Add(onPlat);
        // 強さ 3：床の上にもう1か所（ほかから 3マス以上）
        if (Level >= 3)
        {
            Rect a = KitMotion.Area;
            for (int i = 0; i < 10; i++)
            {
                var p = new Vector2(Random.Range(a.xMin + 1.5f, a.xMax - 1.5f), floor + PopSize * 0.5f);
                bool ok = true;
                foreach (var s in spots) if (Vector2.Distance(s, p) < 3f) ok = false;
                if (!ok) continue;
                spots.Add(p);
                break;
            }
        }
        for (int i = 0; i < spots.Count; i++) PopShape(spots[i], (round + i) % 3);
    }

    /// <summary>形 0：立方体（四角）・1：球（丸）・2：サルの頭の代わり（丸に耳）。オレンジのふち</summary>
    void PopShape(Vector2 at, int shape)
    {
        KitMotionPop pop = shape == 0
            ? KitMotion.Pop(Owner, at, PopWarn, PopLife, 0, null, BlenderArt.Object, 1, PopSize, box: Vector2.one * PopSize)
            : KitMotion.Pop(Owner, at, PopWarn, PopLife, 0, null, BlenderArt.Object, 1, PopSize);
        if (pop == null || pop.Body == null) return;
        Sprite shapeSprite = shape == 0 ? PlaceholderSprites.Square : PlaceholderSprites.Circle;
        AttackRunner.AddVisual(pop.Body, shapeSprite, BlenderArt.Select, Vector2.one * (PopSize + 0.18f), KitMotionItem.OrderBody - 1, "Outline");
        if (shape == 2)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                AttackRunner.AddVisual(pop.Body, PlaceholderSprites.Circle, BlenderArt.Select, Vector2.one * 0.95f, KitMotionItem.OrderBody - 1, "EarRim").transform.localPosition = new Vector2(side * 1.15f, 0.35f);
                AttackRunner.AddVisual(pop.Body, PlaceholderSprites.Circle, BlenderArt.Object, Vector2.one * 0.8f, KitMotionItem.OrderBody, "Ear").transform.localPosition = new Vector2(side * 1.15f, 0.35f);
            }
            foreach (float side in new[] { -1f, 1f })
                AttackRunner.AddVisual(pop.Body, PlaceholderSprites.Circle, BlenderArt.Eye, Vector2.one * 0.32f, KitMotionItem.OrderBody + 1, "Eye").transform.localPosition = new Vector2(side * 0.4f, 0.2f);
        }
        // S で大きくする見た目：小さい点線の四角が、当たる大きさへ広がる（予告の間）
        BlenderGrowFrame.Create(pop.transform, at, 0.5f, PopSize, PopWarn, BlenderArt.Select);
    }

    /// <summary>低い足場（動かない）の上のまん中。プレイヤーが乗っている足場は避ける。無ければ部屋の左右 3.5 の床から 3 の高さ</summary>
    static bool FindLowPlatform(Vector2 player, out Vector2 spot)
    {
        Rect a = KitMotion.Area;
        float floor = KitMotion.FloorY();
        var cols = Physics2D.OverlapAreaAll(new Vector2(a.xMin, floor + 1.8f), new Vector2(a.xMax, floor + 4f), LayerMask.GetMask("OneWayPlatform"));
        var tops = new List<Vector2>();
        foreach (var c in cols)
        {
            if (c == null || c.isTrigger || AppStagePlatforms.IsUnsteady(c)) continue;
            Bounds b = c.bounds;
            if (b.size.x < 1.5f || Mathf.Abs(b.center.x - a.center.x) > 6f) continue; // 端の足場・ボタンは使わない
            tops.Add(new Vector2(b.center.x, b.max.y));
        }
        if (tops.Count == 0)
        {
            tops.Add(new Vector2(a.center.x - 3.5f, floor + 3f));
            tops.Add(new Vector2(a.center.x + 3.5f, floor + 3f));
        }
        // プレイヤーから遠い順に試す（同じ場所に2つ出さない）
        tops.Sort((p, q) => Mathf.Abs(q.x - player.x).CompareTo(Mathf.Abs(p.x - player.x)));
        Vector2 t = tops[Random.Range(0, Mathf.Min(2, tops.Count))];
        spot = new Vector2(t.x, t.y + PopSize * 0.5f);
        if (Vector2.Distance(spot, player) < 2.4f) spot = new Vector2(tops[0].x, tops[0].y + PopSize * 0.5f);
        return Vector2.Distance(spot, player) >= 2.4f;
    }
}

/// <summary>
/// 【仮】オレンジの点線の四角が、from から to の大きさへ広がる（seconds 秒。最後の 0.3秒は点滅して消える）。当たらない飾り（2026-11-19）。
/// Blender の「S で大きくする」見た目：場面③の Pop の予告と、BlenderRule の立方体が戻るときの予告に使う
/// </summary>
public class BlenderGrowFrame : MonoBehaviour
{
    const int PerSide = 5;
    readonly List<SpriteRenderer> _dashes = new List<SpriteRenderer>();
    Vector2 _native = Vector2.one;
    float _from, _to, _seconds, _born;
    Color _color;

    /// <summary>center（世界）に出す。parent があればその子（一緒に消える）</summary>
    public static BlenderGrowFrame Create(Transform parent, Vector2 center, float from, float to, float seconds, Color color, int order = KitMotionItem.OrderMark)
    {
        var go = new GameObject("BlenderGrowFrame");
        if (parent != null)
        {
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
        }
        go.transform.position = center;
        var f = go.AddComponent<BlenderGrowFrame>();
        f._from = from;
        f._to = to;
        f._seconds = Mathf.Max(0.05f, seconds);
        f._born = Time.time;
        f._color = color;
        for (int i = 0; i < PerSide * 4; i++)
            f._dashes.Add(AttackRunner.AddVisual(go.transform, PlaceholderSprites.Square, color, Vector2.one * 0.1f, order, "Dash"));
        if (f._dashes.Count > 0 && f._dashes[0].sprite != null) f._native = f._dashes[0].sprite.bounds.size;
        f.Layout(0f);
        return f;
    }

    void Update()
    {
        float k = (Time.time - _born) / _seconds;
        if (k >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        Layout(k);
        bool on = _seconds * (1f - k) > 0.3f || Mathf.FloorToInt(Time.time / 0.06f) % 2 == 0;
        foreach (var d in _dashes) if (d != null) d.enabled = on;
    }

    void Layout(float k)
    {
        float e = 1f - (1f - k) * (1f - k); // はじめ速く、終わりゆっくり
        float size = Mathf.Lerp(_from, _to, e);
        float half = size * 0.5f;
        float len = size / PerSide * 0.55f;
        float thick = 2f * KitDress.Px;
        for (int side = 0; side < 4; side++)
        {
            for (int j = 0; j < PerSide; j++)
            {
                SpriteRenderer sr = _dashes[side * PerSide + j];
                if (sr == null) continue;
                float u = -half + size * (j + 0.5f) / PerSide;
                Vector2 pos;
                Vector2 dim;
                switch (side)
                {
                    case 0: pos = new Vector2(u, half); dim = new Vector2(len, thick); break;
                    case 1: pos = new Vector2(half, -u); dim = new Vector2(thick, len); break;
                    case 2: pos = new Vector2(-u, -half); dim = new Vector2(len, thick); break;
                    default: pos = new Vector2(-half, u); dim = new Vector2(thick, len); break;
                }
                Transform t = sr.transform;
                t.localPosition = pos;
                t.localScale = new Vector3(dim.x / Mathf.Max(0.001f, _native.x), dim.y / Mathf.Max(0.001f, _native.y), 1f);
            }
        }
    }
}

/// <summary>
/// 【仮】Default Cube（Kit3DFaller）が、奥から大きくなって出る見た目（0.2秒：小さい → 少し大きい → 元の大きさ）。2026-11-19。
/// 見た目（Kit3DVisual の倍率）だけ。当たり・置く場所・落ち方は変えない
/// </summary>
public class BlenderPopIn : MonoBehaviour
{
    const float Time0 = 0.2f;
    Kit3DFaller _faller;
    float _born;

    public static void Attach(Kit3DFaller faller)
    {
        if (faller == null || faller.Visual == null) return;
        var p = faller.gameObject.AddComponent<BlenderPopIn>();
        p._faller = faller;
        p._born = Time.time;
        faller.Visual.SetScale(0.15f);
    }

    void Update()
    {
        if (_faller == null || _faller.Visual == null)
        {
            Destroy(this);
            return;
        }
        float k = (Time.time - _born) / Time0;
        if (k >= 1f)
        {
            _faller.Visual.SetScale(1f);
            Destroy(this);
            return;
        }
        float s = k < 0.7f ? Mathf.Lerp(0.15f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f);
        _faller.Visual.SetScale(s);
    }
}
