using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 当たったときのエフェクト（2026-11-18。docs/app-skill-visuals-design.md 1-2〜1-4）。
/// ・入口は Play(HitInfo) の1つ。攻撃の判定（Hitbox）が当てたときは、ここが自動で呼ぶ（Hitbox.AnyHit）。
///   Hitbox を通らない当たり（zip圧縮の吸い込みなど）は、持ち主が自分で Play を呼ぶ。
///   同じ相手・同じフレームの2回目からは何もしない（判定の Hit の中で自分で Play を呼んだら、そちらが先に出る）
/// ・出す物：スキンの当たった絵・エラーの絵（無ければ Generic）、相手の光り方、白い輪、ヒットストップ、ゆれ、画面のふちの赤・全体の白、ボスのウィンドウの乱れ
///   近接（スキン無し）は白い4方向の星。雑魚（ウイルス）の体当たりは赤い「！」の三角（1-4）
/// ・とどめのゲージ100%（VirusGauge.AnyFilled）で、隅の通知（1-5）。Terminal は通知の代わりに [Process completed]（13-1）
/// 当たり判定・ダメージ・秒数は変えない（ヒットストップの分だけ、実時間で伸びる）
/// </summary>
public static class HitFx
{
    static int _frame = -1;
    static readonly Dictionary<int, HitStrength> _playedThisFrame = new Dictionary<int, HitStrength>();
    static readonly Dictionary<int, float> _lastHit = new Dictionary<int, float>();
    static readonly Dictionary<int, HitStrength> _lastStrength = new Dictionary<int, HitStrength>();
    static readonly Dictionary<long, float> _lockOnHit = new Dictionary<long, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _frame = -1;
        _playedThisFrame.Clear();
        _lastHit.Clear();
        _lastStrength.Clear();
        _lockOnHit.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        Hitbox.AnyHit -= OnAnyHit;
        Hitbox.AnyHit += OnAnyHit;
        VirusGauge.AnyFilled -= OnGaugeFilled;
        VirusGauge.AnyFilled += OnGaugeFilled;
    }

    /// <summary>当たったときのエフェクトを出す（スキンの当たった絵・エラーの絵、かけら、光、ヒットストップ、ゆれ）</summary>
    public static void Play(HitInfo info)
    {
        try
        {
            if (Time.frameCount != _frame)
            {
                _frame = Time.frameCount;
                _playedThisFrame.Clear();
            }
            // 同じ相手・同じフレームの2回目からは出さない（ボムの弾と爆発のように、あとの方が強いときだけ出す）
            if (info.Target != null)
            {
                int id = info.Target.GetInstanceID();
                if (_playedThisFrame.TryGetValue(id, out HitStrength played) && Rank(info.Strength) <= Rank(played)) return;
                _playedThisFrame[id] = info.Strength;
            }
            if (info.TargetIsPlayer) PlayHurt(info);
            else PlayHit(info);
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("HitFx.Play", $"当たったエフェクトで例外：{e}");
        }
    }

    // ───────── 判定が当たった（Hitbox.AnyHit）→ 入れる物を作る ─────────

    static void OnAnyHit(Hitbox hitbox, IDamageable target, DamageInfo damage, bool first)
    {
        try
        {
            var comp = target as Component;
            if (comp == null || hitbox == null) return;
            GameObject go = comp.gameObject;
            bool toPlayer = target.Team == Team.Player;
            var info = new HitInfo
            {
                Point = damage.HitPoint,
                Target = go,
                TargetIsPlayer = toPlayer,
                Skin = damage.Skin,
                ErrorKind = damage.ErrorKind ?? "",
                Data = damage.Data,
                Theme = hitbox.SkinTheme,
                AppIcon = hitbox.SkinIcon,
            };
            Vector2 dir = CenterOf(go, damage.HitPoint) - hitbox.AreaCenter;
            if (dir.sqrMagnitude < 0.0001f) dir = new Vector2(damage.Knockback.x >= 0f ? 1f : -1f, 0f);
            info.Direction = dir.normalized;

            if (toPlayer)
            {
                info.Strength = target is Health h && h.IsDead ? HitStrength.Down : HitStrength.Hurt;
                if (info.Skin == SkinId.None) ResolveUnskinnedHurt(ref info, hitbox, damage);
            }
            else
            {
                info.Strength = StrengthOf(hitbox, target, go, damage, first);
            }
            Play(info);
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("HitFx.OnAnyHit", $"当たったエフェクトの準備で例外：{e}");
        }
    }

    /// <summary>強さの決め方（1-2 の表）</summary>
    static HitStrength StrengthOf(Hitbox hitbox, IDamageable target, GameObject go, DamageInfo damage, bool first)
    {
        float now = Time.time;
        int id = go.GetInstanceID();
        // とどめ：ゲージが 100% になった一撃・逃げるウイルス本体を倒した一撃
        if (target is VirusGauge g && g.IsFull) return HitStrength.Finisher;
        if (target is Health h && h.IsDead && BossOfEjected(go) != null) return HitStrength.Finisher;

        AttackData data = hitbox.Data;
        // もとの強さ：近接は軽い。ゲージ増加 8 以上・雑魚へのダメージ 3 以上は重い。ロックオンの1本目は重い（7-3）
        HitStrength strength;
        if (hitbox.Skin == SkinId.None && data == null) strength = HitStrength.Light;
        else strength = (target is VirusGauge ? damage.GaugeGain >= 8 : damage.Amount >= 3) ? HitStrength.Heavy : HitStrength.Light;

        // 小：0.15秒以内に続けて（前より強い一撃は除く。ボムの弾のすぐあとの爆発など）
        bool small = _lastHit.TryGetValue(id, out float last) && now - last < 0.15f
                     && _lastStrength.TryGetValue(id, out HitStrength prev) && Rank(strength) <= Rank(prev);
        if (data != null && !first && data.PersistDuration > 0f) small = true;    // 残り続ける床・塗りの2回目から
        if (data != null && data.Kind == AttackKind.LockOn)
        {
            long key = ((long)id << 32) ^ (uint)data.GetInstanceID();               // ロックオンの、同じ相手への2本目から
            bool again = _lockOnHit.TryGetValue(key, out float t) && now - t < 3f;
            _lockOnHit[key] = now;
            if (again) small = true;
            else strength = HitStrength.Heavy;
        }
        _lastHit[id] = now;
        _lastStrength[id] = small ? HitStrength.Small : strength;
        return small ? HitStrength.Small : strength;
    }

    /// <summary>強さの順（小 < 軽い < 重い < とどめ。被弾・ダウンは別の並び）</summary>
    static int Rank(HitStrength s)
    {
        switch (s)
        {
            case HitStrength.Small: return 0;
            case HitStrength.Light: return 1;
            case HitStrength.Heavy: return 2;
            case HitStrength.Finisher: return 3;
            case HitStrength.Hurt: return 1;
            case HitStrength.Down: return 3;
            default: return 0;
        }
    }

    /// <summary>
    /// スキンが入っていない当たり（プレイヤーが受けた）：ボスの体当たりはボスのスキン（種類 Contact）。
    /// 雑魚（ウイルス）の体当たり・弾は、スキン無しのまま種類 Virus（赤い「！」の三角）。ほかは、そのステージのボスのスキン
    /// </summary>
    static void ResolveUnskinnedHurt(ref HitInfo info, Hitbox hitbox, DamageInfo damage)
    {
        GameObject src = damage.Source != null ? damage.Source : hitbox.gameObject;
        BossController boss = src.GetComponentInParent<BossController>();
        Projectile proj = src.GetComponent<Projectile>();
        if (boss == null && proj != null && proj.Owner != null) boss = proj.Owner.GetComponent<BossController>();
        if (boss != null)
        {
            SetLook(ref info, SkinDresser.LookOf(boss.Runner, null));
            if (hitbox.Data == null) info.ErrorKind = SkinTables.Kinds.Contact;
            return;
        }
        bool fromVirus = proj != null || src.GetComponentInParent<Health>() != null;
        if (fromVirus)
        {
            info.ErrorKind = SkinTables.Kinds.Virus;
            return;
        }
        BossController stageBoss = StageBoss();
        if (stageBoss != null) SetLook(ref info, SkinDresser.LookOf(stageBoss.Runner, null));
    }

    static void SetLook(ref HitInfo info, SkinDresser.Look look)
    {
        info.Skin = look.Skin;
        if (info.Theme == null) info.Theme = look.Theme;
        if (info.AppIcon == null) info.AppIcon = look.Icon;
    }

    // ───────── プレイヤーの攻撃が敵に当たった ─────────

    static void PlayHit(HitInfo info)
    {
        HitStrength s = info.Strength;
        bool finisher = s == HitStrength.Finisher;
        bool heavy = s == HitStrength.Heavy || finisher;

        // 1. 当たった絵（スキン → Generic）。近接（スキン無し）は白い星
        if (info.Skin == SkinId.None) MeleeSpark(info);
        else if (!TryHit(info.Skin, info) && info.Skin != SkinId.Generic) TryHit(SkinId.Generic, info);

        // 2. 相手の光り方（白く）
        float flash = s == HitStrength.Light ? 0.05f : s == HitStrength.Heavy ? 0.08f : finisher ? 0.12f : 0f;
        if (flash > 0f) FlashOverlay.Play(info.Target, Color.white, Color.white, flash);

        // 3. 重い・とどめ：白い輪・ヒットストップ・ゆれ・ボスのウィンドウの乱れ
        if (heavy)
        {
            FxKit.Ring(info.Point, finisher ? 3.75f : 2.5f, 0.15f, Color.white);
            if (finisher)
            {
                FxKit.Ring(info.Point, 2.5f, 0.15f, Color.white, 0.06f);
                ScreenFx.WhiteFlash(0.4f, 0.06f);
            }
            HitStop.Request(finisher ? 0.12f : 0.05f);
            StageCamera.Shake(finisher ? 0.15f : 0.06f, finisher ? 0.3f : 0.12f);
            BossArmorVisual armor = info.Target != null ? info.Target.GetComponentInChildren<BossArmorVisual>() : null;
            if (armor != null && armor.Window != null)
            {
                armor.Glitch(0.06f);
                FxKit.VirusDots(armor.Window.bounds.center, info.Direction, UnityEngine.Random.Range(3, 7)); // ウイルスがはがれる
            }
        }

        // 4. Terminal のボスに当てたとき：zsh のジョブの行（[1] + done）を積む（13-1）
        BossController boss = BossOf(info.Target);
        if (boss != null && SkinDresser.LookOf(boss.Runner, null).Skin == SkinId.Terminal) TerminalSkin.OnBossHit(boss, info);
    }

    /// <summary>近接の当たった絵（1-4）：白い4方向の星（12ドット）＋黄色のかけら 3個＋ウイルスの黒いドット 2個。0.2秒</summary>
    static void MeleeSpark(HitInfo info)
    {
        HitStrength s = info.Strength;
        Vector2 p = FxKit.PictureAnchor(info);
        if (s == HitStrength.Small)
        {
            FxKit.Fragments(info.Point, info.Direction, 1, null, new[] { FxKit.MeleeYellow });
            return;
        }
        float scale = 1.25f * FxKit.PictureScale(s);
        FxAnim.Sprite(FxArt.Star4, p, FxKit.OrderHitPicture, 0.2f, Color.white, scale, "MeleeStar").Pop().Spin(180f).FadeFrom(0.45f);
        FxAnim.Sprite(FxArt.Twinkle, p + new Vector2(0.25f, 0.2f), FxKit.OrderHitPicture, 0.16f, FxKit.MeleeYellow, 1f, "MeleeTwinkle").Pop().FadeFrom(0.3f);
        int n = s == HitStrength.Finisher ? 12 : s == HitStrength.Heavy ? 6 : 3;
        FxKit.Fragments(info.Point, info.Direction, n, new[] { FxArt.Square(2), FxArt.Square(3) }, new[] { FxKit.MeleeYellow, Color.white, FxKit.MeleeYellow });
        FxKit.VirusDots(info.Point, info.Direction, 2);
    }

    // ───────── 敵の攻撃がプレイヤーに当たった ─────────

    static void PlayHurt(HitInfo info)
    {
        bool down = info.Strength == HitStrength.Down;

        // 1. エラーの絵（スキン → Generic）。雑魚（ウイルス）は赤い「！」の三角
        if (info.Skin == SkinId.None) VirusWarning(info);
        else if (!TryError(info.Skin, info) && info.Skin != SkinId.Generic) TryError(SkinId.Generic, info);

        // 2. 赤いかけら・光り方・ヒットストップ・ゆれ・画面のふちの赤
        FxKit.Fragments(info.Point, info.Direction, down ? 8 : 4, new[] { FxArt.Square(3), FxArt.Square(2) }, new[] { FxKit.EnemyRed, FxKit.EnemyRed, Color.white });
        if (down)
        {
            StageCamera.Shake(0.15f, 0.3f); // ダウンは止めない（ゲームオーバーの処理とぶつかるので）
        }
        else
        {
            FlashOverlay.Play(info.Target, FxKit.EnemyRed, Color.white, 0.12f);
            HitStop.Request(0.08f);
            StageCamera.Shake(0.1f, 0.2f);
        }
        ScreenFx.EdgeRed(0.5f, down ? 0.45f : 0.3f, down ? 0.3f : 0.15f);
    }

    /// <summary>雑魚（ウイルス）の体当たり：赤い「！」の三角（12ドット）を頭の上に</summary>
    static void VirusWarning(HitInfo info)
    {
        Vector2 p = FxKit.PictureAnchor(info);
        float scale = info.Strength == HitStrength.Down ? 2.4f : 1.2f;
        FxAnim.Sprite(FxArt.WarningTriangle, p, FxKit.OrderHitPicture, info.Strength == HitStrength.Down ? 0.9f : 0.4f, Color.white, scale, "VirusWarning")
            .Pop().Rise(0.3f).FadeFrom(0.6f);
    }

    // ───────── とどめ（ゲージ 100%）：隅の通知 ─────────

    static void OnGaugeFilled(VirusGauge gauge)
    {
        try
        {
            if (gauge == null) return;
            BossController boss = gauge.GetComponent<BossController>();
            SkinDresser.Look look = boss != null ? SkinDresser.LookOf(boss.Runner, null) : default;
            if (look.Skin == SkinId.Terminal)
            {
                TerminalSkin.ProcessCompleted(); // Terminal は通知の代わりに [Process completed]（13-1）
                return;
            }
            string title = null;
            AppSkin skin = AppSkins.Get(look.Skin);
            try { title = skin != null ? skin.NotificationTitle : null; }
            catch (Exception) { title = null; }
            if (string.IsNullOrEmpty(title))
            {
                AppSkin generic = AppSkins.Get(SkinId.Generic);
                title = generic != null ? generic.NotificationTitle : null;
            }
            if (string.IsNullOrEmpty(title)) title = "Cleaned";
            Sprite icon = look.Icon != null ? look.Icon : boss != null && boss.Profile != null ? boss.Profile.Icon : null;
            CornerToast.Show(title, icon, look.Theme != null ? look.Theme.Main : Color.white);
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("HitFx.OnGaugeFilled", $"隅の通知で例外：{e}");
        }
    }

    // ───────── 道具 ─────────

    static bool TryHit(SkinId id, HitInfo info)
    {
        AppSkin skin = AppSkins.Get(id);
        if (skin == null) return false;
        try { return skin.Hit(info); }
        catch (Exception e)
        {
            SkinDresser.LogOnce($"{id}.Hit", $"スキン {id} の Hit で例外：{e}");
            return false;
        }
    }

    static bool TryError(SkinId id, HitInfo info)
    {
        AppSkin skin = AppSkins.Get(id);
        if (skin == null) return false;
        try { return skin.Error(info); }
        catch (Exception e)
        {
            SkinDresser.LogOnce($"{id}.Error", $"スキン {id} の Error で例外：{e}");
            return false;
        }
    }

    /// <summary>体の真ん中（移動用の当たり判定。無ければ被弾範囲・位置）</summary>
    public static Vector2 CenterOf(GameObject go, Vector2 fallback)
    {
        if (go == null) return fallback;
        Collider2D trigger = null;
        foreach (var col in go.GetComponentsInChildren<Collider2D>())
        {
            if (!col.enabled) continue;
            if (!col.isTrigger) return col.bounds.center;
            if (trigger == null && col.GetComponent<Hurtbox>() != null) trigger = col;
        }
        return trigger != null ? (Vector2)trigger.bounds.center : (Vector2)go.transform.position;
    }

    /// <summary>当たった相手のボス（ボスの体・逃げるウイルス本体）。ボスでなければ null</summary>
    public static BossController BossOf(GameObject target)
    {
        if (target == null) return null;
        BossController b = target.GetComponentInParent<BossController>();
        return b != null ? b : BossOfEjected(target);
    }

    static BossController BossOfEjected(GameObject virus)
    {
        if (virus == null) return null;
        foreach (var b in UnityEngine.Object.FindObjectsOfType<BossController>())
        {
            if (b.EjectedVirus != null && b.EjectedVirus.gameObject == virus) return b;
        }
        return null;
    }

    static BossController _stageBoss;
    static float _nextFind;

    /// <summary>そのステージのボス（出ている物）。無ければ null</summary>
    public static BossController StageBoss()
    {
        if (_stageBoss != null && _stageBoss.isActiveAndEnabled) return _stageBoss;
        if (Time.unscaledTime < _nextFind) return null;
        _nextFind = Time.unscaledTime + 0.5f;
        _stageBoss = UnityEngine.Object.FindObjectOfType<BossController>();
        return _stageBoss;
    }
}
