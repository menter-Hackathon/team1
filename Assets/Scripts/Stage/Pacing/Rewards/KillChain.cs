using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【仮】連続キル（チェイン）・一掃・まとめ倒し（2026-11-19。docs/stage-pacing-design.md 4-1・4-2。担当3）。
/// ・StagePacer.Started でステージごとに1つ作る（シーンに置かない）。見るのは Health.AnyDied だけ
/// ・雑魚を倒してから 2.5秒以内に次を倒すと、つながる。切れるのは時間切れだけ（ダメージでは切れない）
/// ・つながるほど、倒れた雑魚の位置で、もう1回 HitFx.Play を強く呼ぶ（3〜4：Light、6〜：Heavy）。かけらも足す
/// ・5つながるごとに一掃（8秒に1回まで）：プレイヤーを中心に白い輪（半径 6マス）、中の雑魚にダメージ 1（強い雑魚は1減るだけ）。
///   ヒットストップ 0.12秒・隅にそのアプリのとどめの通知の題名。中に雑魚がいなくても、輪・光・通知は出す（8秒の間あけも使う）
/// ・1回の攻撃（0.15秒以内）で 2体以上：強さを1段上げる・白い光・ヒットストップ 0.08秒
/// ・ボスの登場が始まったら（場面 Done か BossEntrance が動いた）、数えるのをやめて、バッジ・拾える物・シールドを全部片づける
/// 倒れた知らせ（Health.AnyDied）は、ダメージの処理の途中で来るので、数えるだけにして、エフェクトは LateUpdate で出す。
/// 数値はすべて【仮】
/// </summary>
public class KillChain : MonoBehaviour
{
    public const float ChainWindow = 2.5f;
    public const float MultiWindow = 0.15f;
    public const int SweepEvery = 5;
    public const float SweepRadius = 6f;
    public const float SweepCooldown = 8f;
    const float SweepExpand = 0.3f;

    static KillChain _current;

    /// <summary>今のステージの連続キル（無ければ null）</summary>
    public static KillChain Current => _current;
    /// <summary>見返り（バッジ・拾える物・シールド）を出してよいか。ボスの登場が始まったら false</summary>
    public static bool RewardsOpen => _current != null && !_current._closed;

    /// <summary>今つながっている数（0＝つながっていない）</summary>
    public int Count { get; private set; }
    /// <summary>切れるまでの残り（1 → 0）</summary>
    public float TimeLeft01 => Count > 0 ? Mathf.Clamp01(1f - (Time.time - _lastKillAt) / ChainWindow) : 0f;

    struct Kill
    {
        public GameObject Go;
        public Vector2 Point;
        public float Time;
    }

    StagePacer _pacer;
    BossEntrance _entrance;
    bool _hasEntrance;
    bool _closed;
    ChainBadge _badge;
    Transform _player;
    readonly List<Kill> _pending = new List<Kill>();
    readonly List<Kill> _work = new List<Kill>();
    readonly HashSet<Health> _sweepVictims = new HashSet<Health>();
    float _lastKillAt = float.NegativeInfinity;
    float _lastSweepAt = float.NegativeInfinity;
    float _burstStart = float.NegativeInfinity;
    int _burstCount;
    Coroutine _sweep;
    bool _sweeping; // コルーチンが1回目の yield の前に終わることがあるので、Coroutine の null では見ない

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _current = null;
        StagePacer.Started -= OnStarted;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        StagePacer.Started -= OnStarted;
        StagePacer.Started += OnStarted;
    }

    static void OnStarted(StagePacer pacer)
    {
        try
        {
            if (_current != null) Destroy(_current.gameObject);
            var go = new GameObject("PaceRewards");
            if (pacer != null && pacer.gameObject.scene.IsValid()) SceneManager.MoveGameObjectToScene(go, pacer.gameObject.scene);
            _current = go.AddComponent<KillChain>();
            _current.Init(pacer);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"連続キルを始められません：{e}");
        }
    }

    void Init(StagePacer pacer)
    {
        _pacer = pacer;
        if (_pacer != null) _pacer.PhaseChanged += OnPhaseChanged;
        _entrance = FindObjectOfType<BossEntrance>();
        _hasEntrance = _entrance != null;
        _player = RewardKit.PlayerOf(_pacer);
        _badge = ChainBadge.Create(transform, RewardKit.SkinOf(_pacer), RewardKit.ThemeOf(_pacer), RewardKit.IconOf(_pacer));
    }

    void OnEnable() => Health.AnyDied += OnAnyDied;

    void OnDisable() => Health.AnyDied -= OnAnyDied;

    void OnDestroy()
    {
        if (_pacer != null) _pacer.PhaseChanged -= OnPhaseChanged;
        if (_current == this) _current = null;
    }

    void OnPhaseChanged(PacePhase phase)
    {
        if (phase == PacePhase.Done) Close();
    }

    /// <summary>ボスの登場が始まったか（場面 Done・BossEntrance が動いた。BossEntrance の無いシーンは、ボスが出ているか）</summary>
    bool BossStarted()
    {
        if (_pacer != null && _pacer.Phase == PacePhase.Done) return true;
        if (_hasEntrance) return _entrance == null || _entrance.IsTriggered;
        return HitFx.StageBoss() != null;
    }

    // 倒れた知らせ：ダメージの処理の途中なので、数えるだけ（例外を外に出さない）
    void OnAnyDied(Health h)
    {
        try
        {
            if (_closed || h == null) return;
            if (_sweepVictims.Remove(h)) return; // 一掃で倒れた雑魚は、つながりに数えない（一掃が続けて起きないように）
            if (!RewardKit.IsZako(h)) return;
            _pending.Add(new Kill
            {
                Go = h.gameObject,
                Point = HitFx.CenterOf(h.gameObject, h.transform.position),
                Time = Time.time,
            });
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("KillChain.OnAnyDied", $"連続キルで例外：{e}");
        }
    }

    void LateUpdate()
    {
        try
        {
            if (!_closed && BossStarted()) Close();
            if (_closed)
            {
                _pending.Clear();
                return;
            }
            if (_player == null) _player = RewardKit.PlayerOf(_pacer);

            if (_pending.Count > 0)
            {
                _work.Clear();
                _work.AddRange(_pending);
                _pending.Clear();
                foreach (var k in _work) Process(k);
            }

            if (Count > 0 && Time.time - _lastKillAt > ChainWindow) Break();
            if (_badge != null) _badge.Follow(_player, Count, TimeLeft01);
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("KillChain.LateUpdate", $"連続キルで例外：{e}");
        }
    }

    void Process(Kill k)
    {
        if (Count > 0 && k.Time - _lastKillAt > ChainWindow) Break();
        Count++;
        _lastKillAt = k.Time;

        // まとめ倒し（0.15秒以内に2体目から）
        bool multi = false;
        if (k.Time - _burstStart <= MultiWindow)
        {
            _burstCount++;
            multi = true;
            if (_burstCount == 2) MultiKillFx();
        }
        else
        {
            _burstStart = k.Time;
            _burstCount = 1;
        }

        int tier = TierOf(Count);
        PlayKillFx(k, tier, multi);
        if (_badge != null) _badge.Bump(Count, tier);

        if (Count % SweepEvery == 0 && Time.time - _lastSweepAt >= SweepCooldown && !_sweeping)
        {
            _sweeping = true;
            _sweep = StartCoroutine(Sweep());
        }
    }

    /// <summary>段階：0＝1〜2、1＝3〜4、2＝5〜</summary>
    public static int TierOf(int count) => count >= SweepEvery ? 2 : count >= 3 ? 1 : 0;

    void Break()
    {
        Count = 0;
        _burstCount = 0;
        if (_badge != null) _badge.Break();
    }

    void Close()
    {
        if (_closed) return;
        _closed = true;
        Count = 0;
        _pending.Clear();
        if (_sweep != null) StopCoroutine(_sweep);
        _sweep = null;
        _sweeping = false;
        _sweepVictims.Clear();
        if (_badge != null) _badge.Break();
        Pickup.ClearAll();
        ShieldAura.EndAll();
    }

    // ───────── エフェクト ─────────

    void PlayKillFx(Kill k, int tier, bool multi)
    {
        // 強さ：3〜4＝Light、5〜＝Heavy。まとめ倒しは1段上げる（とどめ Finisher はボスに残すので、Heavy まで）
        int rank = tier == 0 ? 0 : tier == 1 ? 1 : 2;
        if (multi) rank = Mathf.Min(2, rank + 1);
        if (rank == 0) return;
        HitStrength s = rank == 1 ? HitStrength.Light : HitStrength.Heavy;

        Vector2 from = _player != null ? RewardKit.BodyCenter(_player) : k.Point + Vector2.left;
        Vector2 dir = k.Point - from;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        dir.Normalize();
        AppTheme theme = RewardKit.ThemeOf(_pacer);
        // 同じ相手・同じフレームなら、HitFx は強い方だけを出す（自動で出た物より強いときだけ出る）
        HitFx.Play(new HitInfo
        {
            Point = k.Point,
            Direction = dir,
            Strength = s,
            Target = k.Go,
            TargetIsPlayer = false,
            Skin = SkinId.Generic,
            ErrorKind = "",
            Theme = theme,
            AppIcon = RewardKit.IconOf(_pacer),
        });
        // かけらを足す（いつも見える。黄色・白・アプリの色）
        Color main = FxKit.MainOf(theme, Color.white);
        main.a = 1f;
        FxKit.Fragments(k.Point, dir, rank == 1 ? 3 : 5, new[] { FxArt.Square(2), FxArt.Square(3) }, new[] { FxKit.MeleeYellow, Color.white, main });
    }

    void MultiKillFx()
    {
        ScreenFx.WhiteFlash(0.25f, 0.06f);
        HitStop.Request(0.08f);
    }

    // ───────── 一掃 ─────────

    IEnumerator Sweep()
    {
        _lastSweepAt = Time.time;
        Transform player = _player;
        Health ph = player != null ? player.GetComponent<Health>() : null;
        if (player == null || (ph != null && ph.IsDead))
        {
            _sweeping = false;
            yield break;
        }
        Vector2 c = RewardKit.BodyCenter(player);
        List<Health> victims = RewardKit.ZakoInRadius(c, SweepRadius);
        SkinId skin = RewardKit.SkinOf(_pacer);
        AppTheme theme = RewardKit.ThemeOf(_pacer);
        Sprite icon = RewardKit.IconOf(_pacer);
        // 中に雑魚がいなくても、輪・光・通知は出す（5つながったごほうび。8秒の間あけも使う）。止めるのは、倒す物があるときだけ
        SweepFx(c, skin, theme, icon);
        if (victims.Count > 0) HitStop.Request(0.12f);

        float t = 0f;
        int idx = 0;
        while (idx < victims.Count)
        {
            if (_closed || BossStarted()) break;
            t += Time.deltaTime;
            float r = SweepRadius * Mathf.Clamp01(t / SweepExpand);
            while (idx < victims.Count)
            {
                Health h = victims[idx];
                if (h != null && Vector2.Distance(c, HitFx.CenterOf(h.gameObject, h.transform.position)) > r + 0.3f && t < SweepExpand) break;
                idx++;
                StrikeSweep(h, c, skin, theme, icon);
            }
            yield return null;
        }
        _sweeping = false;
    }

    void StrikeSweep(Health h, Vector2 c, SkinId skin, AppTheme theme, Sprite icon)
    {
        if (h == null || h.IsDead || !h.isActiveAndEnabled || !RewardKit.IsZako(h)) return;
        _sweepVictims.Add(h);
        bool elite = h.GetComponent<ZakoElite>() != null;
        RewardKit.Strike(h, 1, c, gameObject, skin, elite ? HitStrength.Heavy : HitStrength.Light, theme, icon);
        if (!h.IsDead) _sweepVictims.Remove(h);
    }

    /// <summary>一掃の見た目：白い光・白い輪とアプリの色の輪・アプリの色のかけら・隅の通知</summary>
    void SweepFx(Vector2 c, SkinId skin, AppTheme theme, Sprite icon)
    {
        Color main = FxKit.MainOf(theme, RuntimeSprites.Hex("8AB4F8"));
        main.a = 1f;
        Color[] accents = FxKit.AccentsOf(theme, main);
        ScreenFx.WhiteFlash(0.35f, 0.08f);
        RewardKit.BigRing(c, SweepRadius, SweepExpand, Color.white);
        RewardKit.BigRing(c, SweepRadius * 0.75f, SweepExpand + 0.05f, main, 0.05f);
        FxKit.Ring(c, 2.5f, 0.15f, Color.white);

        // アプリの色のかけらが、まわりへ飛ぶ
        Sprite[] bits = { FxArt.Square(3), FxArt.Square(2), FxArt.Square(3) };
        Vector2[] dirs = { Vector2.up, new Vector2(1f, 0.4f), new Vector2(-1f, 0.4f), new Vector2(0.6f, -0.6f), new Vector2(-0.6f, -0.6f) };
        foreach (var d in dirs) FxKit.Fragments(c, d, 3, bits, accents);
        StageCamera.Shake(0.12f, 0.25f);

        CornerToast.Show(RewardKit.TitleOf(skin), icon, main);
    }
}
