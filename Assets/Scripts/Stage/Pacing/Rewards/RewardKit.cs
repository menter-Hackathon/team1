using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】見返り（連続キル・一掃・拾える物）の共通の道具（2026-11-19。docs/stage-pacing-design.md 4章。担当3）。
// ・雑魚か（ボス・分身・逃げるウイルス本体は雑魚ではない）
// ・そのステージのアプリの見た目（スキン・色・アイコン・とどめの通知の題名）。StagePacer が無ければ Generic
// ・雑魚にダメージを入れて、当たったエフェクトを出す（Hitbox を通らないので、自分で HitFx.Play を呼ぶ）
// ・コードの中のドット絵（バッジの形・拾える物・輪）
// 数値・形・色はすべて【仮】

/// <summary>連続キルのバッジの形（アプリらしさ）</summary>
public enum BadgeShape { Pill, Bubble, Tab, Bracket, Chamfer }

public static class RewardKit
{
    // 重なる順番（敵の絵 11・プレイヤー 15・当たった絵 47〜49・隅の通知 70）
    public const int OrderPickup = 14;
    public const int OrderShield = 17;
    public const int OrderBadge = 60;

    public static readonly Color BadgeYellow = RuntimeSprites.Hex("FFD60A");
    public static readonly Color BadgeText = RuntimeSprites.Hex("1C1C1E");

    static BossController[] _bosses = Array.Empty<BossController>();
    static float _nextBossFind = -1f;
    static PlayerController _player;
    static float _nextPlayerFind = -1f;
    static readonly List<Collider2D> _overlap = new List<Collider2D>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _bosses = Array.Empty<BossController>();
        _nextBossFind = -1f;
        _player = null;
        _nextPlayerFind = -1f;
    }

    // ───────── 雑魚か ─────────

    /// <summary>
    /// 雑魚か：敵の陣営で、雑魚の部品（歩く・撃つ・とぶ・突っ込む・強い）を持ち、ボスの体の一部でも、逃げるウイルス本体でもない。
    /// 分身（TerminalGhost）・壊せる物は雑魚の部品を持たないので入らない
    /// </summary>
    public static bool IsZako(Health h)
    {
        if (h == null || h.Team != Team.Enemy) return false;
        GameObject go = h.gameObject;
        bool zako = go.GetComponent<ZakoWalker>() != null || go.GetComponent<ZakoShooter>() != null
                    || go.GetComponent<ZakoFlyer>() != null || go.GetComponent<ZakoCharger>() != null || go.GetComponent<ZakoElite>() != null;
        if (!zako) return false;
        if (go.GetComponentInParent<BossController>() != null) return false;
        foreach (var b in Bosses())
        {
            if (b != null && b.EjectedVirus == h) return false; // 追い出されたウイルス本体（歩く雑魚の部品を持つことがある）
        }
        return true;
    }

    static BossController[] Bosses()
    {
        if (Time.unscaledTime >= _nextBossFind)
        {
            _bosses = UnityEngine.Object.FindObjectsOfType<BossController>();
            _nextBossFind = Time.unscaledTime + 0.5f;
        }
        return _bosses;
    }

    /// <summary>center から radius マスの中にいる雑魚（生きている物）。近い順</summary>
    public static List<Health> ZakoInRadius(Vector2 center, float radius)
    {
        var found = new List<Health>();
        var dist = new Dictionary<Health, float>();
        var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
        filter.SetLayerMask(LayerMask.GetMask("Enemy"));
        _overlap.Clear();
        Physics2D.OverlapCircle(center, radius, filter, _overlap);
        foreach (var col in _overlap)
        {
            if (col == null) continue;
            Health h = col.GetComponentInParent<Health>();
            if (h == null || dist.ContainsKey(h) || h.IsDead || !h.isActiveAndEnabled || !IsZako(h)) continue;
            float d = Vector2.Distance(center, HitFx.CenterOf(h.gameObject, h.transform.position));
            if (d > radius + 0.5f) continue;
            dist[h] = d;
            found.Add(h);
        }
        found.Sort((a, b) => dist[a].CompareTo(dist[b]));
        return found;
    }

    /// <summary>
    /// 雑魚にダメージを入れて、当たったエフェクトを出す（一掃・シールドの体当たり）。入ったら true。
    /// ゲージは増やさない（ボスには使わない）
    /// </summary>
    public static bool Strike(Health h, int amount, Vector2 from, GameObject source, SkinId skin, HitStrength fx, AppTheme theme, Sprite icon)
    {
        if (h == null || h.IsDead || !h.isActiveAndEnabled) return false;
        Vector2 c = HitFx.CenterOf(h.gameObject, h.transform.position);
        Vector2 dir = c - from;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        dir.Normalize();
        var info = new DamageInfo
        {
            Amount = Mathf.Max(1, amount),
            GaugeGain = 0,
            SourceTeam = Team.Player,
            Knockback = new Vector2(dir.x >= 0f ? 5f : -5f, 3f),
            HitStun = 0.2f,
            HitPoint = c,
            Source = source,
            Skin = skin,
            ErrorKind = "",
            Data = null,
        };
        if (!h.TryTakeDamage(info)) return false;
        HitFx.Play(new HitInfo
        {
            Point = c,
            Direction = dir,
            Strength = fx,
            Target = h.gameObject,
            TargetIsPlayer = false,
            Skin = skin,
            ErrorKind = "",
            Theme = theme,
            AppIcon = icon,
        });
        return true;
    }

    // ───────── プレイヤー ─────────

    /// <summary>プレイヤー（StagePacer の物。無ければ探す）。無ければ null</summary>
    public static Transform PlayerOf(StagePacer pacer)
    {
        if (pacer != null && pacer.Player != null) return pacer.Player;
        if (_player != null) return _player.transform;
        if (Time.unscaledTime < _nextPlayerFind) return null;
        _nextPlayerFind = Time.unscaledTime + 0.5f;
        _player = UnityEngine.Object.FindObjectOfType<PlayerController>();
        return _player != null ? _player.transform : null;
    }

    /// <summary>体の真ん中（移動用の当たり判定）</summary>
    public static Vector2 BodyCenter(Transform t) => t != null ? HitFx.CenterOf(t.gameObject, (Vector2)t.position + Vector2.up * 0.5f) : Vector2.zero;

    /// <summary>体の移動用の当たり判定（無ければ null）</summary>
    public static Collider2D BodyOf(Transform t)
    {
        if (t == null) return null;
        foreach (var col in t.GetComponents<Collider2D>())
        {
            if (col != null && col.enabled && !col.isTrigger) return col;
        }
        return null;
    }

    // ───────── そのステージのアプリの見た目 ─────────

    public static SkinId SkinOf(StagePacer p) => p != null && p.Skin != SkinId.None ? p.Skin : SkinId.Generic;
    public static AppTheme ThemeOf(StagePacer p) => p != null ? p.Theme : null;
    public static Sprite IconOf(StagePacer p) => p != null ? p.Icon : null;

    /// <summary>とどめの通知の題名（AppSkin.NotificationTitle）。無ければ Generic の題名（Terminal も。とどめの [Process completed] は使わない）</summary>
    public static string TitleOf(SkinId skin)
    {
        string title = null;
        try
        {
            AppSkin s = AppSkins.Get(skin);
            title = s != null ? s.NotificationTitle : null;
            if (string.IsNullOrEmpty(title))
            {
                AppSkin g = AppSkins.Get(SkinId.Generic);
                title = g != null ? g.NotificationTitle : null;
            }
        }
        catch (Exception)
        {
            title = null;
        }
        return string.IsNullOrEmpty(title) ? "Cleaned" : title;
    }

    /// <summary>バッジの形（アプリらしさ）：Chat は吹き出し、Browser はタブ、Dev・Terminal は角ばった窓、Game は角を落とした札、ほかは丸い札</summary>
    public static BadgeShape BadgeShapeOf(SkinId skin)
    {
        switch (skin)
        {
            case SkinId.Chat: return BadgeShape.Bubble;
            case SkinId.Zoom: return BadgeShape.Bubble;
            case SkinId.Browser: return BadgeShape.Tab;
            case SkinId.Dev: return BadgeShape.Bracket;
            case SkinId.Terminal: return BadgeShape.Bracket;
            case SkinId.Game: return BadgeShape.Chamfer;
            case SkinId.Premiere: return BadgeShape.Chamfer;
            default: return BadgeShape.Pill;
        }
    }

    // ───────── ドット絵 ─────────

    /// <summary>
    /// バッジの板（白いふち 1ドット・黄色・上は明るく下は暗い）。w×h ドット。吹き出しは下に 3ドットのしっぽが付く（h に入る）
    /// </summary>
    public static Sprite BadgeSprite(BadgeShape shape, int w, int h)
    {
        w = Mathf.Clamp(w, 8, 64);
        h = Mathf.Clamp(h, 8, 32);
        var rows = new string[h];
        int bodyH = shape == BadgeShape.Bubble ? h - 3 : h;
        for (int y = 0; y < h; y++)
        {
            var line = new char[w];
            for (int x = 0; x < w; x++)
            {
                if (!BadgeInside(shape, x, y, w, h, bodyH)) { line[x] = '.'; continue; }
                bool edge = !BadgeInside(shape, x - 1, y, w, h, bodyH) || !BadgeInside(shape, x + 1, y, w, h, bodyH)
                            || !BadgeInside(shape, x, y - 1, w, h, bodyH) || !BadgeInside(shape, x, y + 1, w, h, bodyH);
                if (edge) line[x] = 'W';
                else if (y >= bodyH - 2) line[x] = 'S';
                else if (y <= 1) line[x] = 'H';
                else line[x] = 'Y';
            }
            rows[y] = new string(line);
        }
        var palette = new Dictionary<char, Color32>
        {
            { 'W', new Color32(255, 255, 255, 255) },
            { 'Y', RuntimeSprites.Hex("FFD60A") },
            { 'H', RuntimeSprites.Hex("FFE866") },
            { 'S', RuntimeSprites.Hex("D9A800") },
        };
        return RuntimeSprites.FromAscii($"Pace_Badge_{shape}_{w}x{h}", rows, palette);
    }

    static bool BadgeInside(BadgeShape shape, int x, int y, int w, int h, int bodyH)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return false;
        float px = x + 0.5f, py = y + 0.5f;
        switch (shape)
        {
            case BadgeShape.Pill:
                return InRound(px, py, w, h, h * 0.5f);
            case BadgeShape.Bubble:
                if (y < bodyH) return InRound(px, py, w, bodyH, bodyH * 0.5f);
                {
                    // しっぽ（左下。下へ行くほど細い）
                    int k = y - bodyH;
                    int left = 4 + k;
                    return x >= left && x <= 7;
                }
            case BadgeShape.Tab:
                // 上の角だけ丸い（ブラウザのタブ）
                if (py < 3f) return InRound(px, py, w, 6, 3f);
                return true;
            case BadgeShape.Chamfer:
                {
                    const int c = 2;
                    return x + y >= c && (w - 1 - x) + y >= c && x + (h - 1 - y) >= c && (w - 1 - x) + (h - 1 - y) >= c;
                }
            default:
                return true; // Bracket：角ばった窓
        }
    }

    static bool InRound(float px, float py, float w, float h, float r)
    {
        float cx = Mathf.Clamp(px, r, w - r);
        float cy = Mathf.Clamp(py, r, h - r);
        float dx = px - cx, dy = py - cy;
        return dx * dx + dy * dy <= r * r + 0.01f;
    }

    /// <summary>拾える物の絵（アプリのアイコンの形。白いふち。16ドット）。回復＝緑の電池、シールド＝青い盾</summary>
    public static Sprite PickupSprite(PickupKind kind)
    {
        if (kind == PickupKind.Heal)
        {
            return RuntimeSprites.FromAscii("Pace_Pickup_Heal", new[]
            {
                "..WWWWWWWWWWWW..",
                ".WLLLLLLLLLLLLW.",
                "WLLLLLLWWLLLLLLW",
                "WGGGGWWWWWWGGGGW",
                "WGGGGWGGGGWGGGGW",
                "WGGGGWGGGGWGGGGW",
                "WGGGGWWWWWWGGGGW",
                "WGGGGWWWWWWGGGGW",
                "WGGGGWWWWWWGGGGW",
                "WGGGGWWWWWWGGGGW",
                "WGGGGWWWWWWGGGGW",
                "WGGGGWWWWWWGGGGW",
                "WDDDDWWWWWWDDDDW",
                "WDDDDDDDDDDDDDDW",
                ".WDDDDDDDDDDDDW.",
                "..WWWWWWWWWWWW..",
            }, new Dictionary<char, Color32>
            {
                { 'W', new Color32(255, 255, 255, 255) },
                { 'G', RuntimeSprites.Hex("30D158") },
                { 'L', RuntimeSprites.Hex("7EE69A") },
                { 'D', RuntimeSprites.Hex("1F9A40") },
            });
        }
        return RuntimeSprites.FromAscii("Pace_Pickup_Shield", new[]
        {
            "..WWWWWWWWWWWW..",
            ".WLLLLLLLLLLLLW.",
            "WLLLWWWWWWWWLLLW",
            "WBBBWWLLWWWWBBBW",
            "WBBBWWLWWWWWBBBW",
            "WBBBWWLWWWWWBBBW",
            "WBBBWWWWWWWWBBBW",
            "WBBBBWWWWWWBBBBW",
            "WBBBBWWWWWWBBBBW",
            "WBBBBBWWWWBBBBBW",
            "WBBBBBBWWBBBBBBW",
            "WDDDDDDDDDDDDDDW",
            "WDDDDDDDDDDDDDDW",
            "WDDDDDDDDDDDDDDW",
            ".WDDDDDDDDDDDDW.",
            "..WWWWWWWWWWWW..",
        }, new Dictionary<char, Color32>
        {
            { 'W', new Color32(255, 255, 255, 255) },
            { 'B', RuntimeSprites.Hex("0A84FF") },
            { 'L', RuntimeSprites.Hex("64B5FF") },
            { 'D', RuntimeSprites.Hex("0060C8") },
        });
    }

    /// <summary>緑の「＋」（白いふち。7ドット。回復したとき頭の上に浮く）</summary>
    public static Sprite PlusSprite => RuntimeSprites.FromAscii("Pace_Plus", new[]
    {
        "..WWW..",
        "..WGW..",
        "WWWGWWW",
        "WGGGGGW",
        "WWWGWWW",
        "..WGW..",
        "..WWW..",
    }, new Dictionary<char, Color32> { { 'W', new Color32(255, 255, 255, 255) }, { 'G', RuntimeSprites.Hex("30D158") } });

    /// <summary>白い輪（半径 r ドット・太さ t ドット）。シールドの輪</summary>
    public static Sprite RingSprite(int r, float t)
    {
        r = Mathf.Clamp(r, 3, 40);
        int d = r * 2 + 2;
        var rows = new string[d];
        float c = d * 0.5f;
        for (int y = 0; y < d; y++)
        {
            var line = new char[d];
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                line[x] = dist <= r && dist >= r - t ? 'W' : '.';
            }
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii($"Pace_Ring{r}_{t:0.0}", rows, new Dictionary<char, Color32> { { 'W', new Color32(255, 255, 255, 255) } });
    }

    /// <summary>
    /// 大きい輪が広がる（半径 0 → radius マス）。FxKit.Ring は半径 2マスまでなので、輪のコマを大きく出す（ドットは太くなる）
    /// </summary>
    public static void BigRing(Vector2 point, float radius, float seconds, Color color, float delay = 0f)
    {
        const int maxPx = 40;
        Sprite[] frames = FxArt.RingFrames(maxPx);
        float scale = radius / (maxPx / RuntimeSprites.PixelsPerUnit);
        FxAnim.Sprite(frames[0], point, FxKit.OrderRing, seconds + 0.04f, color, scale, "PaceRing")
            .Frames(frames, frames.Length / Mathf.Max(0.01f, seconds), false).FadeFrom(0.6f).Delay(delay);
    }

    /// <summary>アイコンの大きさの倍率（絵の大きい方の辺を px ドットにする）</summary>
    public static float IconScale(Sprite icon, float px)
    {
        if (icon == null) return 1f;
        float native = Mathf.Max(icon.bounds.size.x, icon.bounds.size.y);
        return native > 0.001f ? px / RuntimeSprites.PixelsPerUnit / native : 1f;
    }
}
