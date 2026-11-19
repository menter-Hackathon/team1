using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】ステージのギミックの「動きの部品」（2026-11-19。docs/stage-gimmick-variety.md 1章の表）。担当0 の物。
// ・どの部品も：予告（赤い印。0.6秒以上・ボス戦は 0.8秒以上。短く渡しても伸ばす）→ 動く → 消える。予告の音は GameAudio.Telegraph（間引きあり）
// ・当たると 1 ダメージ（damage で変える。0 なら当たらない飾り・合図）。プレイヤーの無敵の間は当たらない（ふつうの Hitbox の道）
// ・最初の引数 owner＝片づけ先（KitOwner）：
//     ボスの技  → ボスの AttackRunner（50％・100％で消える）
//     StageRule → Ctx（ctx.Hazard に登録。片づけの表で消える）。危ない物を出す前に ctx.CanSpawnHazard を見るのは呼ぶ側
//     出来事    → KitTimedEvent の Owner（ほかの PaceEvent なら KitOwner.Of(Track, Runner)）
// ・1回の呼び出し＝1つの親の GameObject（予告・本体・弾は全部その子）。返した物の Cancel() で全部消える（知らせは出さない）
// ・見た目：sprite（null なら四角か丸）と color（null なら、絵があれば白、無ければアプリの色）。赤は危ない物の予告だけに使う
// ・時間はスケールあり（Time.time）。止める間は止まる。プレイヤー・ボスがいなくても例外を出さない
// ・x・width は世界の座標（x はまん中）。高さ（EdgeSweep の y・Erupt の height など）は床から。床は KitMotion.FloorY
//
// 使い方の例（出来事の中。KitTimedEvent を継いだ物）
//   KitMotion.Erupt(Owner, PlayerPos.x, 1.6f, 3f);                                    // プレイヤーの下から柱
//   KitMotion.EdgeSweep(Owner, KitSide.Right, 0.6f, speed: 7f, count: 3, gap: 0.8f);   // 右から低い物が 3つ（ジャンプ）
//   KitMotion.Orbit(Owner, KitBoss.Current?.transform, 2.5f, 120f, count: 4, duration: 6f);
//   KitMotion.HitTarget(Owner, new Vector2(0f, 4f), 4f, onHit: () => KitPlayer.Heal());

/// <summary>ステージのギミックの動きの部品（docs/stage-gimmick-variety.md 1章）</summary>
public static class KitMotion
{
    /// <summary>予告の最短（雑魚の時間）</summary>
    public const float MinWarn = 0.6f;
    /// <summary>予告の最短（ボス戦）</summary>
    public const float BossMinWarn = 0.8f;
    /// <summary>予告の基本</summary>
    public const float DefaultWarn = 0.8f;

    /// <summary>危ない物の予告の赤（#FF453A）</summary>
    public static Color Red => KitDress.Red;

    // ───────── 場所 ─────────

    /// <summary>今の画面（ボス戦はボス部屋。1画面のステージは部屋、ほかはカメラ）</summary>
    public static Rect Area
    {
        get
        {
            StageRuleHost h = StageRuleHost.Current;
            if (h != null && h.Context != null)
            {
                Rect r = h.Context.PlayArea;
                if (r.width > 1f && r.height > 1f) return r;
            }
            StagePacer p = StagePacer.Current;
            if (p != null && p.Phase != PacePhase.Done)
            {
                Rect r = p.PlayArea;
                if (r.width > 1f && r.height > 1f) return r;
            }
            Rect arena = BattleArena.GetBounds();
            Transform pl = Player;
            if (BattleArena.Current != null && (pl == null || arena.Contains(pl.position))) return arena;
            return ScreenUi.ViewRect();
        }
    }

    /// <summary>床（Ground）の上面の高さ。今の画面の下の方を何か所か調べて、いちばん低い所（無ければ画面の下）</summary>
    public static float FloorY()
    {
        Rect area = Area;
        int ground = LayerMask.GetMask("Ground");
        float best = float.PositiveInfinity;
        for (int i = 0; i < 7; i++)
        {
            float x = Mathf.Lerp(area.xMin + 1f, area.xMax - 1f, i / 6f);
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, area.yMin + 2.4f), Vector2.down, 6f, ground);
            if (hit.collider != null && hit.distance > 0.01f) best = Mathf.Min(best, hit.point.y);
        }
        if (float.IsInfinity(best)) best = Mathf.Max(area.yMin + 0.2f, BattleArena.GetBounds().yMin);
        return best;
    }

    /// <summary>x の位置の床の高さ（無ければ FloorY()）</summary>
    public static float FloorY(float x)
    {
        Rect area = Area;
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, area.yMin + 2.4f), Vector2.down, 6f, LayerMask.GetMask("Ground"));
        if (hit.collider != null && hit.distance > 0.01f) return hit.point.y;
        return FloorY();
    }

    /// <summary>大きさ size の物の「まん中」が動ける範囲（左右の壁・床・天井の内側）。Bounce・WallCrawl の道に使う</summary>
    public static Rect BoxFor(float size, Rect? area = null)
    {
        Rect a = area ?? Area;
        float floor = area.HasValue ? a.yMin : FloorY();
        float h = size * 0.5f;
        float w = Mathf.Max(0.1f, a.width - size);
        float ht = Mathf.Max(0.1f, a.yMax - floor - size);
        return new Rect(a.xMin + h, floor + h, w, ht);
    }

    /// <summary>画面の角の点（床の上。inset だけ内側）。Diagonal の from に使う</summary>
    public static Vector2 Corner(KitCorner corner, float inset = 0.6f)
    {
        Rect a = Area;
        float floor = FloorY();
        float x = corner == KitCorner.BottomLeft || corner == KitCorner.TopLeft ? a.xMin + inset : a.xMax - inset;
        float y = corner == KitCorner.BottomLeft || corner == KitCorner.BottomRight ? floor + inset : a.yMax - inset;
        return new Vector2(x, y);
    }

    static Transform _player;
    static float _playerLookAt = -99f;

    /// <summary>プレイヤー（いなければ null。探すのは 0.5秒に1回まで）</summary>
    public static Transform Player
    {
        get
        {
            if (_player != null && _player.gameObject.activeInHierarchy) return _player;
            if (Time.unscaledTime - _playerLookAt < 0.5f) return null;
            _playerLookAt = Time.unscaledTime;
            StageRuleHost h = StageRuleHost.Current;
            if (h != null && h.Context != null && h.Context.Player != null) _player = h.Context.Player;
            else
            {
                PlayerController pc = UnityEngine.Object.FindObjectOfType<PlayerController>();
                _player = pc != null ? pc.transform : null;
            }
            return _player != null && _player.gameObject.activeInHierarchy ? _player : null;
        }
    }

    /// <summary>プレイヤーの体のまん中（いなければ画面のまん中）</summary>
    public static Vector2 PlayerPos
    {
        get
        {
            Transform p = Player;
            return p != null ? CenterOf(p) : Area.center;
        }
    }

    /// <summary>プレイヤーの体（移動用の箱）の範囲。いなければ false</summary>
    public static bool PlayerBounds(out Bounds bounds)
    {
        bounds = default;
        Transform p = Player;
        if (p == null) return false;
        if (p.TryGetComponent(out BoxCollider2D box) && box.enabled)
        {
            bounds = box.bounds;
            return true;
        }
        bounds = new Bounds(p.position + Vector3.up * 0.5f, new Vector3(0.6f, 1f, 1f));
        return true;
    }

    /// <summary>物の体のまん中（トリガーでないコライダーがあればその中心。無ければ位置）</summary>
    public static Vector2 CenterOf(Transform t)
    {
        if (t == null) return Area.center;
        foreach (var col in t.GetComponents<Collider2D>())
            if (col != null && col.enabled && !col.isTrigger) return col.bounds.center;
        return t.position;
    }

    /// <summary>ボス戦の間か（ステージの係が知っていればそれ。無ければ StagePacer がボスの場面か）</summary>
    public static bool InBossFight
    {
        get
        {
            StageRuleHost h = StageRuleHost.Current;
            if (h != null && h.Context != null) return h.Context.InBossFight;
            StagePacer p = StagePacer.Current;
            return p != null && p.Phase == PacePhase.Done;
        }
    }

    /// <summary>今のステージのアプリの色（無ければ赤）</summary>
    public static Color AppColor
    {
        get
        {
            StageRuleHost h = StageRuleHost.Current;
            AppTheme theme = h != null && h.Context != null ? h.Context.Theme : null;
            if (theme == null && StagePacer.Current != null) theme = StagePacer.Current.Theme;
            Color c = FxKit.MainOf(theme, Red);
            c.a = 1f;
            return c;
        }
    }

    /// <summary>良い物（取る・叩く・立つ・安全）の色：アプリの色。赤に近ければ緑</summary>
    public static Color GoodColor
    {
        get
        {
            Color c = AppColor;
            Color r = Red;
            if (Mathf.Abs(c.r - r.r) + Mathf.Abs(c.g - r.g) + Mathf.Abs(c.b - r.b) < 0.45f) return new Color32(0x34, 0xC7, 0x59, 0xFF);
            return c;
        }
    }

    // ───────── 部品：下から・横から・斜め・回る・跳ねる・床・壁 ─────────

    /// <summary>
    /// 下から：床（surfaceY を渡せばその高さ）の x（まん中）・width の範囲に、光る割れ目と赤い帯の予告（warn 秒）→
    /// 物（width×height）が下からせり上がる（rise 秒）→ hold 秒止まる → 引っこむ。当たるのは、せり上がりと止まっている間
    /// </summary>
    public static KitMotionErupt Erupt(KitOwner owner, float x, float width, float height, float warn = DefaultWarn, float rise = 0.15f, float hold = 0.5f,
        Sprite sprite = null, Color? color = null, int damage = 1, float? surfaceY = null)
        => MakeErupt(owner, null, x, width, height, warn, rise, hold, sprite, color, damage, surfaceY);

    /// <summary>
    /// 横から：画面の左か右の端に矢印と赤い線の予告（高さ y＝床から）→ 物が反対の端まで横切る。
    /// count 個を gap 秒おき。wave：上下の振れ幅（0 ならまっすぐ）。side＝Both なら左右から同時に
    /// </summary>
    public static KitMotionGroup EdgeSweep(KitOwner owner, KitSide side, float y, float speed = 8f, int count = 1, float gap = 0.6f, float wave = 0f,
        float warn = DefaultWarn, Sprite sprite = null, Color? color = null, int damage = 1, float size = 0.8f, float waveLength = 4f)
    {
        Rect a = Area;
        float wy = FloorY() + y;
        var g = Make<KitMotionGroup>(owner, null, "KitEdgeSweep", a.center);
        bool harm = damage > 0;
        float w = WarnFor(g.Owner, warn, harm);
        count = Mathf.Max(1, count);
        gap = Mathf.Max(0f, gap);
        float lane = w + (count - 1) * gap; // 予告を出しておく秒数（最後の物が出るまで）
        Color wc = WarnColor(harm, color);
        Color bc = BodyColor(sprite, color);
        foreach (KitSide s in Lanes(side))
        {
            KitSide lan = s;
            bool fromLeft = lan == KitSide.Left;
            WarnArrow(g.transform, new Vector2(fromLeft ? a.xMin + 0.55f : a.xMax - 0.55f, wy), fromLeft ? Vector2.right : Vector2.left, lane, wc);
            if (wave <= 0.01f) WarnLine(g.transform, new Vector2(a.xMin, wy), new Vector2(a.xMax, wy), lane, wc);
            else KitMotionMark.Create(g.transform, PlaceholderSprites.Square, new Vector2(a.center.x, wy), new Vector2(a.width, wave * 2f + size),
                0f, new Color(wc.r, wc.g, wc.b, 0.14f), KitMotionItem.OrderWarn, lane, "WaveBand");
            for (int i = 0; i < count; i++)
            {
                g.At(w + i * gap, () =>
                {
                    KitPath path = KitPath.Sweep(lan, wy, speed, wave, waveLength, size, a);
                    var m = Make<KitMotionMover>(owner, g, "Sweep", path.At(0f));
                    m.BuildBody(sprite, bc, Vector2.one * size, sprite == null, damage);
                    m.Setup(path, 0f, false);
                });
            }
        }
        g.MinLife = lane;
        if (harm) Safe(GameAudio.Telegraph);
        return g;
    }

    /// <summary>
    /// 斜め・角から：from（画面の角は KitMotion.Corner）から、予告の線（今のプレイヤーの場所へ）→ 飛んでくる（画面の外まで）。
    /// count 本を spread 度ずつ扇に広げる。gap 秒ずつずらして撃つ（0 なら同時）
    /// </summary>
    public static KitMotionGroup Diagonal(KitOwner owner, Vector2 from, float speed = 10f, int count = 1, float spread = 15f,
        float warn = DefaultWarn, Sprite sprite = null, Color? color = null, int damage = 1, float size = 0.6f, float gap = 0f)
    {
        Rect a = Area;
        Vector2 aim = PlayerPos - from;
        if (aim.sqrMagnitude < 0.01f) aim = Vector2.down;
        float baseDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        var g = Make<KitMotionGroup>(owner, null, "KitDiagonal", from);
        bool harm = damage > 0;
        float w = WarnFor(g.Owner, warn, harm);
        count = Mathf.Max(1, count);
        gap = Mathf.Max(0f, gap);
        Color wc = WarnColor(harm, color);
        Color bc = BodyColor(sprite, color);
        KitMotionMark.Create(g.transform, KitMotionArt.Disc(9), from, Vector2.one * 0.5f, 0f, wc, KitMotionItem.OrderWarn, w + (count - 1) * gap, "From");
        for (int i = 0; i < count; i++)
        {
            float deg = baseDeg + (i - (count - 1) * 0.5f) * spread;
            Vector2 dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
            float at = w + i * gap;
            WarnLine(g.transform, from, from + dir * KitPath.ExitDistance(from, dir, a), at, wc);
            g.At(at, () =>
            {
                KitPath path = KitPath.Out(from, dir, speed, a, size + 0.5f);
                var m = Make<KitMotionMover>(owner, g, "Diagonal", from);
                m.BuildBody(sprite, bc, Vector2.one * size, sprite == null, damage);
                m.Setup(path, 0f, false);
            });
        }
        g.MinLife = w + (count - 1) * gap;
        if (harm) Safe(GameAudio.Telegraph);
        return g;
    }

    /// <summary>
    /// 回る：center（ボス・足場・置いた物。動いてもよい。体のまん中）のまわりを、count 個が等しい間で回る（duration 秒）。
    /// speed：度/秒（＋で反時計回り）。予告の間は、その場所でうすく点滅する。reverseAt 秒で逆回り（0 ならしない）
    /// </summary>
    public static KitMotionGroup Orbit(KitOwner owner, Transform center, float radius, float speed = 90f, int count = 3, float duration = 6f,
        float warn = DefaultWarn, Sprite sprite = null, Color? color = null, int damage = 1, float size = 0.7f, float startDeg = 0f, float reverseAt = 0f)
    {
        count = Mathf.Max(1, count);
        return OrbitCore(owner, i => KitPath.Orbit(center, radius, speed, startDeg + 360f * i / count, duration, reverseAt),
            count, warn, sprite, color, damage, size);
    }

    /// <summary>回る：決まった点のまわり（ほかは Transform の形と同じ）</summary>
    public static KitMotionGroup Orbit(KitOwner owner, Vector2 center, float radius, float speed = 90f, int count = 3, float duration = 6f,
        float warn = DefaultWarn, Sprite sprite = null, Color? color = null, int damage = 1, float size = 0.7f, float startDeg = 0f, float reverseAt = 0f)
    {
        count = Mathf.Max(1, count);
        return OrbitCore(owner, i => KitPath.Orbit(center, radius, speed, startDeg + 360f * i / count, duration, reverseAt),
            count, warn, sprite, color, damage, size);
    }

    static KitMotionGroup OrbitCore(KitOwner owner, Func<int, KitPath> pathOf, int count, float warn, Sprite sprite, Color? color, int damage, float size)
    {
        KitPath first = pathOf(0);
        var g = Make<KitMotionGroup>(owner, null, "KitOrbit", first.At(0f));
        bool harm = damage > 0;
        float w = WarnFor(g.Owner, warn, harm);
        Color bc = BodyColor(sprite, color);
        for (int i = 0; i < count; i++)
        {
            KitPath path = i == 0 ? first : pathOf(i);
            var m = Make<KitMotionMover>(owner, g, "Orbit", path.At(0f));
            m.BuildBody(sprite, bc, Vector2.one * size, sprite == null, damage);
            m.Setup(path, w, true);
        }
        if (harm) Safe(GameAudio.Telegraph);
        return g;
    }

    /// <summary>
    /// 跳ねる：from から dir の向きへ、画面の壁・床・天井で跳ね返りながら進む（bounces 回跳ねたら画面の外へ出て消える）。
    /// 予告：その場でうすく点滅＋最初に跳ねる所までの赤い線
    /// </summary>
    public static KitMotionMover Bounce(KitOwner owner, Vector2 from, Vector2 dir, float speed = 8f, int bounces = 3,
        float warn = DefaultWarn, Sprite sprite = null, Color? color = null, int damage = 1, float size = 0.8f)
    {
        Rect box = BoxFor(size);
        KitPath path = KitPath.Bounce(from, dir, speed, bounces, box);
        Vector2 p0 = path.At(0f);
        var m = Make<KitMotionMover>(owner, null, "KitBounce", p0);
        bool harm = damage > 0;
        float w = WarnFor(m.Owner, warn, harm);
        m.BuildBody(sprite, BodyColor(sprite, color), Vector2.one * size, sprite == null, damage);
        Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : new Vector2(1f, 1f).normalized;
        WarnLine(m.transform, p0, p0 + d * KitPath.ExitDistance(p0, d, box), w, WarnColor(harm, color));
        m.Setup(path, w, true);
        if (harm) Safe(GameAudio.Telegraph);
        return m;
    }

    /// <summary>
    /// 床を走る：床の端から、低い壁（width×height）が反対の端まで走ってくる（ジャンプで越える）。
    /// 予告：床ぜんぶに低い赤い帯＋端の矢印。side＝Both で左右から。meet なら、まん中でぶつかって消える。
    /// fromX を渡すと、端からではなく、その x から外へ走る（side は向き：Left＝左へ、Right＝右へ、Both＝両方へ）
    /// </summary>
    public static KitMotionGroup FloorWave(KitOwner owner, KitSide side, float speed = 7f, float height = 0.8f,
        float warn = DefaultWarn, Sprite sprite = null, Color? color = null, int damage = 1, float width = 0.7f, bool meet = false, float? surfaceY = null,
        float? fromX = null)
    {
        Rect a = Area;
        float floor = surfaceY ?? FloorY();
        var g = Make<KitMotionGroup>(owner, null, "KitFloorWave", new Vector2(a.center.x, floor));
        bool harm = damage > 0;
        float w = WarnFor(g.Owner, warn, harm);
        Color wc = WarnColor(harm, color);
        Sprite s = sprite != null ? sprite : color.HasValue ? null : DropQuake.DefaultWaveSprite;
        Color bc = BodyColor(s, color);
        if (harm) WarnBand(g.transform, a.xMin, a.xMax, floor, height, w, 0f);
        float y = floor + height * 0.5f;
        foreach (KitSide lane in Lanes(side))
        {
            Vector2 start, end;
            if (fromX.HasValue)
            {
                bool toLeft = lane == KitSide.Left; // x から外へ
                Vector2 outward = toLeft ? Vector2.left : Vector2.right;
                WarnArrow(g.transform, new Vector2(fromX.Value + outward.x * 0.6f, floor + height + 0.35f), outward, w, wc);
                start = new Vector2(fromX.Value, y);
                end = new Vector2(toLeft ? a.xMin - width : a.xMax + width, y);
            }
            else
            {
                bool fromLeft = lane == KitSide.Left;
                WarnArrow(g.transform, new Vector2(fromLeft ? a.xMin + 0.55f : a.xMax - 0.55f, floor + height + 0.35f), fromLeft ? Vector2.right : Vector2.left, w, wc);
                start = new Vector2(fromLeft ? a.xMin - width : a.xMax + width, y);
                float endX = meet && side == KitSide.Both ? a.center.x + (fromLeft ? -width * 0.5f : width * 0.5f) : fromLeft ? a.xMax + width : a.xMin - width;
                end = new Vector2(endX, y);
            }
            g.At(w, () =>
            {
                KitPath path = KitPath.Line(start, end, speed);
                var m = Make<KitMotionMover>(owner, g, "FloorWave", start);
                m.BuildBody(s, bc, new Vector2(width, height), false, damage, 0.85f);
                m.Setup(path, 0f, false);
            });
        }
        g.MinLife = w;
        if (harm) Safe(GameAudio.Telegraph);
        return g;
    }

    /// <summary>
    /// 壁を這う：start の角から、床・壁・天井のふちに沿って laps 周する（dir：CW＝画面で見て時計回り。左下から床を右へ行くのは CCW）。
    /// 予告：角でうすく点滅＋進む向きの矢印
    /// </summary>
    public static KitMotionMover WallCrawl(KitOwner owner, KitCorner start, KitTurn dir = KitTurn.CW, float speed = 8f,
        float warn = DefaultWarn, Sprite sprite = null, Color? color = null, int damage = 1, float size = 0.8f, float laps = 1f)
    {
        Rect box = BoxFor(size);
        KitPath path = KitPath.Perimeter(box, start, dir, speed, laps);
        Vector2 p0 = path.At(0f);
        var m = Make<KitMotionMover>(owner, null, "KitWallCrawl", p0);
        bool harm = damage > 0;
        float w = WarnFor(m.Owner, warn, harm);
        m.BuildBody(sprite, BodyColor(sprite, color), Vector2.one * size, false, damage);
        Vector2 ahead = path.At(Mathf.Min(0.25f, path.Duration)) - p0;
        Vector2 d = ahead.sqrMagnitude > 1e-6f ? ahead.normalized : Vector2.right;
        WarnArrow(m.transform, p0 + d * (size * 0.5f + 0.45f), d, w, WarnColor(harm, color));
        m.Setup(path, w, true);
        if (harm) Safe(GameAudio.Telegraph);
        return m;
    }

    // ───────── 部品：その場に出る・床が動く ─────────

    /// <summary>
    /// その場に出る：point に点線の丸の予告（warn 秒）→ 物が奥から大きくなって出る → life 秒で縮んで消える。
    /// ringCount：出た時に小さい弾を輪にばらまく（ringAtEnd なら消える時）。hitTime：出てから当たる秒数（0 なら出ている間ずっと）。
    /// box：四角い物（ウィンドウ・板）の大きさ（渡すと size のかわりにこの大きさで、予告は点線の四角い枠）。
    /// 返した物の Burst(数) で、好きな時に輪を出せる・Close() で早く閉じる
    /// </summary>
    public static KitMotionPop Pop(KitOwner owner, Vector2 point, float warn = DefaultWarn, float life = 1.2f, int ringCount = 0,
        Sprite sprite = null, Color? color = null, int damage = 1, float size = 1.2f, float ringSpeed = 6f, bool ringAtEnd = false, float hitTime = 0f,
        Vector2? box = null)
    {
        var p = Make<KitMotionPop>(owner, null, "KitPop", point);
        bool harm = damage > 0 || ringCount > 0;
        float w = WarnFor(p.Owner, warn, harm);
        p.Setup(point, w, Mathf.Max(0.1f, life), Mathf.Max(0, ringCount), ringSpeed, ringAtEnd, Mathf.Max(0f, hitTime),
            sprite, BodyColor(sprite, color), damage, Mathf.Max(0.2f, size), WarnColor(harm, color),
            box.HasValue ? Vector2.Max(box.Value, new Vector2(0.2f, 0.2f)) : (Vector2?)null);
        if (harm) Safe(GameAudio.Telegraph);
        return p;
    }

    /// <summary>
    /// 床が動く：床（surfaceY）の x（まん中）・width の範囲が、duration 秒、横に流れる（speed：毎秒のマス。＋で右、－で左）。
    /// 上に立つプレイヤー・雑魚を運ぶ（当たらない）。予告：矢印が点滅しながらゆっくり流れる（warn 秒）
    /// </summary>
    public static KitMotionConveyor Conveyor(KitOwner owner, float x, float width, float speed, float duration,
        float warn = DefaultWarn, Color? color = null, float? surfaceY = null)
    {
        float surface = surfaceY ?? FloorY(x);
        var c = Make<KitMotionConveyor>(owner, null, "KitConveyor", new Vector2(x, surface));
        c.Setup(x, Mathf.Max(0.5f, width), speed, Mathf.Max(0.1f, duration), WarnFor(c.Owner, warn, true), color ?? GoodColor, surface);
        Safe(GameAudio.Telegraph);
        return c;
    }

    // ───────── 部品：行動を促す（叩く・取る・立つ・逃げる） ─────────

    /// <summary>
    /// 叩く：point に的が出る（warn を渡すと、先に点線の丸）→ life 秒の間にプレイヤーの攻撃（近接・能力）が hits 回当たると onHit、
    /// 時間切れで onMiss。path を渡すと、その道を動く（KitPath.Orbit で回る・KitPath.Sweep で流れる など）。的は Body・Prop
    /// </summary>
    public static KitMotionTarget HitTarget(KitOwner owner, Vector2 point, float life, Action onHit, Action onMiss = null,
        Sprite sprite = null, Color? color = null, float size = 1f, int hits = 1, KitPath path = null, float warn = 0f)
    {
        Vector2 at = path != null ? path.At(0f) : point;
        var t = Make<KitMotionTarget>(owner, null, "KitHitTarget", at);
        t.Setup(at, Mathf.Max(0f, life), onHit, onMiss, sprite, color ?? (sprite != null ? Color.white : GoodColor), Mathf.Max(0.3f, size), Mathf.Max(1, hits), path, Mathf.Max(0f, warn));
        return t;
    }

    /// <summary>
    /// 取る：良い物が from から to へ毎秒 speed マスで流れてきて、着いたら linger 秒残る（最後の 1秒は点滅）。
    /// プレイヤーが触ると onCatch（1回）。from と to が同じなら、その場に浮かぶ
    /// </summary>
    public static KitMotionCatch Catch(KitOwner owner, Vector2 from, Vector2 to, float speed, Action onCatch,
        Sprite sprite = null, Color? color = null, float size = 0.8f, float linger = 2f, float warn = 0f)
        => Catch(owner, KitPath.Line(from, to, speed).Then(Mathf.Max(0f, linger)), onCatch, sprite, color, size, warn);

    /// <summary>取る：良い物が道すじ path を動く（どの向きからでも。KitPath.Sweep・Arc・Bounce など）。触ると onCatch</summary>
    public static KitMotionCatch Catch(KitOwner owner, KitPath path, Action onCatch, Sprite sprite = null, Color? color = null, float size = 0.8f, float warn = 0f)
    {
        path = path ?? KitPath.Stay(PlayerPos, 3f);
        var c = Make<KitMotionCatch>(owner, null, "KitCatch", path.At(0f));
        c.Setup(path, onCatch, sprite, color ?? (sprite != null ? Color.white : GoodColor), Mathf.Max(0.2f, size), Mathf.Max(0f, warn));
        return c;
    }

    /// <summary>
    /// 立つ：床の x（まん中）・width に光る帯（高さ height）→ その中に seconds 秒続けていると onDone（出ると数え直し。上に丸い進みの印）。
    /// life 秒たっても終わらなければ onFail（life 0 ならずっと出ている）
    /// </summary>
    public static KitMotionHold HoldZone(KitOwner owner, float x, float width, float seconds, Action onDone,
        float life = 10f, Action onFail = null, Color? color = null, float height = 3f, float? surfaceY = null, float warn = 0f)
    {
        float surface = surfaceY ?? FloorY(x);
        var h = Make<KitMotionHold>(owner, null, "KitHoldZone", new Vector2(x, surface));
        h.Setup(x, Mathf.Max(0.5f, width), Mathf.Max(0.1f, seconds), onDone, Mathf.Max(0f, life), onFail, color ?? GoodColor, Mathf.Max(0.5f, height), surface, Mathf.Max(0f, warn));
        return h;
    }

    /// <summary>
    /// 逃げる：床の x（まん中）・width だけ「安全」の印（warn 秒）→ それ以外に危ない物。
    /// then を渡さなければ、ほかの床ぜんぶから Erupt（高さ height。印の終わりにちょうどせり上がる）。
    /// then を渡すと、warn 秒後に then(安全な範囲) を呼ぶ（その中で出す物にも予告が付く）。印は then のあと stay 秒残る
    /// </summary>
    public static KitMotionSafe SafeSpot(KitOwner owner, float x, float width, float warn = 1.5f, Action<Rect> then = null,
        Color? color = null, int damage = 1, float height = 3f, float stay = 1f, Sprite sprite = null, Color? hazardColor = null, float? surfaceY = null)
    {
        float surface = surfaceY ?? FloorY(x);
        var s = Make<KitMotionSafe>(owner, null, "KitSafeSpot", new Vector2(x, surface));
        s.Setup(x, Mathf.Max(0.5f, width), WarnFor(s.Owner, warn, true), then, color ?? GoodColor, damage, Mathf.Max(0.3f, height), Mathf.Max(0f, stay),
            sprite, hazardColor, surface);
        Safe(GameAudio.Telegraph);
        return s;
    }

    // ───────── まとめて消す ─────────

    /// <summary>center から radius の中の、KitMotion の危ない動く物（弾など）を消す（HitTarget の「弾が消える」など）。消した数</summary>
    public static int ClearHazards(Vector2 center, float radius) => KitMotionItem.ClearHarmful(center, radius);

    /// <summary>KitMotion の危ない動く物を全部消す。消した数</summary>
    public static int ClearHazards() => KitMotionItem.ClearHarmful(null, 0f);

    // ───────── 内側の道具 ─────────

    internal static KitMotionErupt MakeErupt(KitOwner owner, KitMotionItem host, float x, float width, float height, float warn, float rise, float hold,
        Sprite sprite, Color? color, int damage, float? surfaceY)
    {
        float surface = surfaceY ?? FloorY(x);
        var e = Make<KitMotionErupt>(owner, host, "KitErupt", new Vector2(x, surface));
        bool harm = damage > 0;
        e.Setup(x, Mathf.Max(0.2f, width), Mathf.Max(0.2f, height), surface, WarnFor(e.Owner, warn, harm), Mathf.Max(0.03f, rise), Mathf.Max(0f, hold),
            sprite, BodyColor(sprite, color), damage, WarnColor(harm, color));
        if (harm) Safe(GameAudio.Telegraph);
        return e;
    }

    /// <summary>
    /// 部品の GameObject を作る。host があればその子（持ち主は host と同じ。登録しない）。
    /// 無ければ親：owner.Runner に登録（無ければ owner.Track）、owner.Parent の子
    /// </summary>
    internal static T Make<T>(KitOwner owner, KitMotionItem host, string objectName, Vector2 pos) where T : KitMotionItem
    {
        GameObject go;
        if (host != null)
        {
            owner = host.Owner;
            go = new GameObject(objectName);
            go.layer = host.gameObject.layer;
            go.transform.SetParent(host.transform, false);
            go.transform.position = pos;
        }
        else if (owner.Runner != null) go = owner.Runner.CreateSpawnedObject(objectName, pos);
        else
        {
            go = new GameObject(objectName);
            go.transform.position = pos;
        }
        if (host == null && owner.Parent != null) go.transform.SetParent(owner.Parent, true);
        var item = go.AddComponent<T>();
        item.Owner = owner;
        if (host == null && owner.Runner == null && owner.Track != null) Safe(() => owner.Track(go));
        if (host is KitMotionGroup g) g.Add(item);
        return item;
    }

    static readonly Dictionary<int, AttackData> Datas = new Dictionary<int, AttackData>();

    /// <summary>ダメージの数値（ノックバック (6,5)・のけぞり 0.25。ダメージごとに1つ作って使い回す）</summary>
    internal static AttackData DataFor(int damage)
    {
        damage = Mathf.Max(1, damage);
        if (Datas.TryGetValue(damage, out AttackData d) && d != null) return d;
        d = ZoneStrike.MakeData("Attack_Stage_Motion", damage);
        Datas[damage] = d;
        return d;
    }

    /// <summary>
    /// 当たり判定を付ける（damage 0 なら付けない）。owner.Runner があればその人の判定（スキンの当たった絵つき）、
    /// 無ければ敵の陣営でプレイヤーに当てる判定。重なっている間ずっと当たる（続けての当たりはプレイヤーの無敵で防ぐ）。切ってある
    /// </summary>
    internal static Hitbox AddHurt(GameObject go, KitOwner owner, Vector2 size, bool circle, int damage)
    {
        if (damage <= 0 || go == null) return null;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        Collider2D col;
        if (circle)
        {
            var c = go.AddComponent<CircleCollider2D>();
            c.radius = Mathf.Max(0.05f, Mathf.Min(size.x, size.y) * 0.5f);
            col = c;
        }
        else
        {
            var b = go.AddComponent<BoxCollider2D>();
            b.size = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
            col = b;
        }
        col.isTrigger = true;
        AttackData data = DataFor(damage);
        Hitbox hb;
        if (owner.Runner != null) hb = owner.Runner.AddHitbox(go, data, col, false);
        else
        {
            hb = go.AddComponent<Hitbox>();
            hb.Deactivate(); // 付けた時点で入っているので、すぐ切る
            hb.SetArea(col);
            hb.SetOwner(Team.Enemy, LayerMask.GetMask("Player"), false);
            hb.Setup(data);
        }
        hb.Deactivate();
        return hb;
    }

    /// <summary>予告の秒数（危ない物は 0.6秒以上・ボス戦は 0.8秒以上）</summary>
    internal static float WarnFor(KitOwner owner, float warn, bool harmful)
    {
        if (!harmful) return Mathf.Max(0f, warn);
        float min = owner.IsBoss || InBossFight ? BossMinWarn : MinWarn;
        return Mathf.Max(min, warn);
    }

    /// <summary>予告の色（危ない物は赤。ほかは渡した色か白）</summary>
    internal static Color WarnColor(bool harmful, Color? color)
    {
        if (harmful) return Red;
        Color c = color ?? Color.white;
        c.a = 1f;
        return c;
    }

    /// <summary>本体の色（渡した色。無ければ、絵があれば白、無ければアプリの色）</summary>
    internal static Color BodyColor(Sprite sprite, Color? color) => color ?? (sprite != null ? Color.white : AppColor);

    static IEnumerable<KitSide> Lanes(KitSide side)
    {
        if (side != KitSide.Right) yield return KitSide.Left;
        if (side != KitSide.Left) yield return KitSide.Right;
    }

    /// <summary>予告の細い線（seconds 秒。最後の 0.3秒は点滅）。parent の子</summary>
    internal static GameObject WarnLine(Transform parent, Vector2 from, Vector2 to, float seconds, Color color, float widthPx = 2f)
    {
        Vector2 d = to - from;
        float len = d.magnitude;
        if (len < 0.01f || parent == null) return null;
        var go = new GameObject("WarnLine");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.position = (from + to) * 0.5f;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        go.AddComponent<KitLine>().Setup(new Vector2(len, Mathf.Max(1f, widthPx) * KitDress.Px), color, seconds);
        return go;
    }

    /// <summary>予告の赤い帯（床から height。PaceZoneMark）。parent の子</summary>
    internal static void WarnBand(Transform parent, float xMin, float xMax, float floorY, float height, float telegraph, float active)
    {
        PaceZoneMark m = PaceZoneMark.Show(null, xMin, xMax, floorY, Mathf.Max(0.1f, height), Mathf.Max(0.05f, telegraph), Mathf.Max(0f, active));
        if (m != null && parent != null) m.transform.SetParent(parent, true);
    }

    /// <summary>予告の矢印（pos に、dir の向き）。parent の子</summary>
    internal static KitMotionMark WarnArrow(Transform parent, Vector2 pos, Vector2 dir, float seconds, Color color)
    {
        float deg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        return KitMotionMark.Create(parent, KitMotionArt.Arrow, pos, new Vector2(0.75f, 0.75f), deg, color, KitMotionItem.OrderMark, seconds, "Arrow");
    }

    /// <summary>例外で止めずに呼ぶ</summary>
    internal static void Safe(Action a)
    {
        if (a == null) return;
        try { a(); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
