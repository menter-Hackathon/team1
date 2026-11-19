using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Firefox の場面③の出来事「しっぽの炎」（2026-11-19。docs/stage-gimmick-variety-b.md 11章）。数値はすべて【仮】。
/// ・オレンジの炎（横 2.5×縦 1.2 のかたまり＋うしろに尾）が、部屋のふちを這って回る（WallCrawl の道 KitPath.Perimeter）。
///   左下の角から：床を右へ → 右の壁を上へ → 天井を左へ → 左の壁を下へ（KitTurn.CCW）。速さ 9（1周 約 6.5秒）。1周半で消える
/// ・3秒たつと、2つ目が、そのとき 1つ目のちょうど反対側（半周先）から、同じ向きで出る（いつも向かい側を走る。1周と 1/4 で消える）。
///   仕様の「右上の角から」は、3秒後には 1つ目がその近くにいて重なるので、半周先の場所にした
/// ・炎の高さは 1.2：床を走ってくるときは、足場へ上がる（下の足場は安全）。壁ぎわに立たない。部屋のまん中の空中もずっと安全
/// ・予告：出る場所で炎がうすく点滅＋進む向きの矢印（1.0秒）。炎の少し前（3マス先）のふちが、うすくオレンジに光る
/// ・5秒たつと、左の上の段（動かない story の足場の上）に紫の盾の的「追跡者をブロック」（4秒）。
///   叩くと、炎が 2つとも消えて（出来事が終わる）、FirefoxRule のピースが 1つ来る（全部入っていれば、能力のクールタイムを全部戻す）。叩かなくても罰はない
/// ・KitMotion.WallCrawl は体が正方形なので、四角い炎を壁に沿って回して動かす（GroupBMotion.Mover）
/// </summary>
public class FirefoxTailEvent : KitTimedEvent
{
    static readonly Vector2 FlameSize = new Vector2(2.5f, 1.2f);
    const float Speed = 9f, FirstWarn = 1f, Warn = 0.8f, SecondAt = 3f;
    const float Laps1 = 1.5f, Laps2 = 1.25f;
    const float GlowAhead = 3f;
    const float TargetAt = 5f, TargetLife = 4f, TargetSize = 1.1f;
    /// <summary>的の場所（部屋のまん中・床から）。Firefox の足場 story（x −4.6・上の面 4.83。動かない）の上</summary>
    static readonly Vector2 TargetSpot = new Vector2(-4.6f, 5.75f);
    const float PuffEvery = 0.05f, PuffLife = 0.33f;

    class Flame
    {
        public KitMotionMover Mover;
        public KitPath Path;
        public SpriteRenderer Glow;
    }

    readonly List<Flame> _flames = new List<Flame>();
    float _puffAt;
    bool _blocked;

    public FirefoxTailEvent(int stage) : base("Firefox") { }

    protected override void Build()
    {
        At(0f, () => Spawn(KitCorner.BottomLeft, 0f, Laps1, FirstWarn));
        At(SecondAt, SpawnOpposite);
        At(TargetAt, ShowTarget);
        float perimeter = Perimeter();
        float end1 = FirstWarn + perimeter * Laps1 / Speed;
        float end2 = SecondAt + Warn + perimeter * Laps2 / Speed;
        EndAt(Mathf.Max(end1, end2) + 0.3f);
    }

    Rect Box => KitMotion.BoxFor(FlameSize.y);

    float Perimeter()
    {
        Rect b = Box;
        return 2f * (b.width + b.height);
    }

    /// <summary>炎を 1つ：start の角から、offset 秒ぶん先の場所から回り始める</summary>
    void Spawn(KitCorner start, float offset, float laps, float warn)
    {
        float lapTime = Perimeter() / Speed;
        KitPath loop = KitPath.Perimeter(Box, start, KitTurn.CCW, Speed, laps + 2f); // offset のぶん長めに
        float duration = lapTime * laps;
        KitPath path = new KitPath(t => loop.At(t + offset), duration);
        KitMotionMover m = GroupBMotion.Mover(Owner, path, FlameSize, FirefoxArt.Flame(0), Color.white, warn, true, 1, "Firefox_TailFlame", 0.8f);
        if (m == null) return;
        Orient(m, path, 0f);
        // 先の火の玉（飾り）
        if (m.Body != null)
        {
            SpriteRenderer head = AttackRunner.AddVisual(m.Body, FirefoxArt.Fireball, Color.white, Vector2.one * 1.1f, KitMotionItem.OrderBody + 1, "Head");
            head.transform.localPosition = new Vector3(FlameSize.x * 0.45f, 0.1f, 0f);
        }
        // 予告：進む向きの矢印
        Vector2 p0 = path.At(0f);
        Vector2 ahead = path.At(Mathf.Min(0.25f, duration)) - p0;
        Vector2 d = ahead.sqrMagnitude > 1e-6f ? ahead.normalized : Vector2.right;
        KitMotion.WarnArrow(m.transform, p0 + d * (FlameSize.x * 0.5f + 0.5f), d, m.Warn, KitMotion.Red);
        // 3マス先のふちの光（飾り。この出来事の物）
        var glowGo = new GameObject("Firefox_EdgeGlow");
        Track(glowGo);
        SpriteRenderer glow = AttackRunner.AddVisual(glowGo.transform, KitMotionArt.Disc(18), new Color(1f, 0.44f, 0.22f, 0.3f), new Vector2(1.6f, 1.6f), KitMotionItem.OrderZone, "Glow");
        glowGo.transform.position = p0;
        _flames.Add(new Flame { Mover = m, Path = path, Glow = glow });
    }

    /// <summary>2つ目：1つ目のちょうど反対側（半周先）から</summary>
    void SpawnOpposite()
    {
        Flame first = _flames.Find(f => f.Mover != null && !f.Mover.IsDone);
        float lapTime = Perimeter() / Speed;
        float firstAt = first != null ? first.Mover.MoveTime : 0f;
        float warn = KitMotion.WarnFor(Owner, Warn, true);
        // 1つ目が動き出してから（2つ目が動き出す時）の秒数＋半周
        Spawn(KitCorner.BottomLeft, firstAt + warn + lapTime * 0.5f, Laps2, warn);
    }

    /// <summary>炎の向き：進む向きに合わせて回す（床では上向き・右の壁では左向き … 根元がいつも壁につく）</summary>
    static void Orient(KitMotionMover m, KitPath path, float t)
    {
        if (m == null || m.Body == null || path == null) return;
        Vector2 a = path.At(t), b = path.At(t + 0.05f);
        Vector2 d = b - a;
        if (d.sqrMagnitude < 1e-6f) return;
        m.Body.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    void ShowTarget()
    {
        if (_blocked) return;
        Vector2 at = new Vector2(View.center.x + TargetSpot.x, Floor + TargetSpot.y);
        KitMotion.HitTarget(Owner, at, TargetLife, Block, null, FirefoxArt.Shield, Color.white, TargetSize, 1, null, 0.4f);
    }

    /// <summary>追跡者をブロック：炎が 2つとも消えて、ピースが 1つ来る</summary>
    void Block()
    {
        _blocked = true;
        foreach (Flame f in _flames)
        {
            if (f.Mover == null || f.Mover.IsDone) continue;
            if (!f.Mover.Warning && f.Mover.Body != null) FxKit.Ring(f.Mover.Body.position, 2.4f, 0.25f, (Color)FirefoxArt.Purple);
            f.Mover.Cancel();
        }
        FirefoxRule rule = GroupBMotion.Rule<FirefoxRule>();
        if (rule == null || !rule.GrantPiece()) KitPlayer.RefillCooldowns();
        GameAudio.Event("gm_firefox_boss_addon_end");
        Stop();
    }

    protected override void OnFrame(float clock)
    {
        bool puff = Time.time >= _puffAt;
        if (puff) _puffAt = Time.time + PuffEvery;
        int frame = Mathf.FloorToInt(Time.time / 0.12f);
        foreach (Flame f in _flames)
        {
            KitMotionMover m = f.Mover;
            if (m == null || m.IsDone)
            {
                if (f.Glow != null) f.Glow.enabled = false;
                continue;
            }
            float t = m.MoveTime;
            Orient(m, f.Path, t);
            if (m.Visual != null) m.Visual.sprite = FirefoxArt.Flame(frame);
            if (f.Glow != null)
            {
                f.Glow.transform.parent.position = f.Path.At(t + GlowAhead / Speed);
                f.Glow.enabled = true;
            }
            // うしろの尾（炎のかけらが残って消える。飾り）
            if (puff && !m.Warning && m.Body != null)
                FxAnim.Sprite(FirefoxArt.Fireball, m.Body.position, KitMotionItem.OrderBody - 1, PuffLife, new Color(1f, 1f, 1f, 0.7f), 1.1f, "Firefox_Tail").FadeFrom(0f).Grow(0.4f);
        }
    }
}
