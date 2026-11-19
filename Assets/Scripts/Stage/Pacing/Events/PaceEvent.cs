using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】出来事の型（雨・柱床・群れ）の共通の部分（2026-11-19。docs/stage-pacing-design.md 3章・8章）。担当2 の物。
// ・始まり：画面のふちが 0.3秒赤く光る → 隅に通知（アプリのアイコン＋短い文字。飾り）→ 0.8秒後に最初の予告（EventShow）
// ・終わり：出した物をすべて消し、白い光を 0.06秒
// ・ボスの登場（PacePhase.Done）で、呼ばれなくても自分で全部片づける（白い光は出さない）
// ・ダメージを受けてから 1秒は、新しい予告を出さない（シールドの無敵の間は、ふつうに出す。Damaged を見る）
// ・どの出来事も、決まった秒数で必ず終わる（MaxSeconds）

/// <summary>出来事の型の共通の部分（RainEvent・SweepEvent・SwarmEvent が継ぐ）</summary>
public abstract class PaceEvent : StageEvent
{
    /// <summary>赤いふちと通知から、最初の予告までの秒数</summary>
    public const float LeadTime = 0.8f;
    /// <summary>ダメージを受けてから、新しい予告を出さない秒数</summary>
    public const float HurtQuiet = 1f;

    /// <summary>始まり（赤いふち・通知）と終わり（白い光）を出すか。重ねる出来事（ComboEvent の中）は false</summary>
    public bool Announce { get; set; } = true;
    /// <summary>最初の予告までの秒数（重ねる出来事は長くする）</summary>
    public float StartDelay { get; set; } = LeadTime;
    /// <summary>隅の通知の文字（飾り。無ければ出さない）</summary>
    public string Toast { get; set; }
    /// <summary>この秒数を過ぎたら、何があっても終わる（始まりから数える）</summary>
    public virtual float MaxSeconds => 16f;
    /// <summary>終わったか</summary>
    public bool IsEnded => _ended;

    protected StagePacer Pacer { get; private set; }
    /// <summary>危ない物を出す AttackRunner（群れは使わない）</summary>
    protected AttackRunner Runner { get; private set; }
    protected int Strength { get; private set; } = 2;
    /// <summary>出してよい雑魚の数（群れだけ）</summary>
    protected int Budget { get; private set; }
    /// <summary>危ない物を出すか（AttackRunner が要るか）</summary>
    protected virtual bool NeedsRunner => true;
    /// <summary>ダメージのすぐあと（新しい予告を出さない）</summary>
    protected bool Quiet => Time.time - _hurtAt < HurtQuiet;

    bool _begun, _ended, _ownRunner;
    float _age;
    float _hurtAt = -99f;
    Health _playerHealth;
    readonly List<GameObject> _marks = new List<GameObject>();

    public sealed override void Begin(StagePacer pacer, AttackRunner hazardRunner, int strength, int zakoBudget)
    {
        if (_begun) return;
        _begun = true;
        Pacer = pacer;
        Strength = Mathf.Clamp(strength, 1, 3);
        Budget = SpawnsOwnZako ? Mathf.Max(0, zakoBudget) : 0;
        if (NeedsRunner) Runner = PrepareRunner(hazardRunner, pacer, out _ownRunner);
        if (pacer != null) pacer.PhaseChanged += OnPhaseChanged;
        HookPlayer();
        if (Announce) EventShow.Intro(pacer, Toast);
        try
        {
            OnBegin();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"出来事 {GetType().Name} の始まりで例外：{e}");
            Finish(false);
        }
    }

    public sealed override bool Tick(float dt)
    {
        if (_ended) return false;
        if (Pacer == null || Pacer.Phase == PacePhase.Done)
        {
            Finish(true);
            return false;
        }
        if (NeedsRunner && Runner == null) return false; // 危ない物を出せない（プレハブが無い・消えた）
        _age += dt;
        if (_age >= MaxSeconds) return false;
        if (_age < StartDelay) return true;
        try
        {
            return OnTick(dt);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"出来事 {GetType().Name} で例外：{e}");
            return false;
        }
    }

    public sealed override void End() => Finish(false);

    /// <summary>始まったときに1回（Pacer・Runner・Strength・Budget が入っている）</summary>
    protected virtual void OnBegin() { }
    /// <summary>最初の予告の時刻から、毎フレーム。false を返したら終わり</summary>
    protected abstract bool OnTick(float dt);
    /// <summary>終わるときに1回（byBoss：ボスの登場で終わった）。Runner の物と Track した物は、このあと消す</summary>
    protected virtual void OnEnd(bool byBoss) { }

    /// <summary>自分で作った飾り（予告の帯・タブの列など）を覚える。終わるときに消す</summary>
    protected void Track(GameObject go)
    {
        if (go == null) return;
        _marks.RemoveAll(g => g == null);
        _marks.Add(go);
    }

    /// <summary>今の画面（StagePacer.PlayArea。無ければカメラ）</summary>
    protected Rect Area
    {
        get
        {
            Rect r = Pacer != null ? Pacer.PlayArea : default;
            return r.width > 1f && r.height > 1f ? r : ScreenUi.ViewRect();
        }
    }

    /// <summary>プレイヤーの足元（いなければ画面の真ん中）</summary>
    protected Vector2 PlayerPos
    {
        get
        {
            Transform p = PlayerTransform();
            return p != null ? (Vector2)p.position : Area.center;
        }
    }

    /// <summary>床（Ground）の上面の高さ。画面の下の方を、何か所か真下へ調べて、いちばん低い所</summary>
    protected float FloorY(Rect area)
    {
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

    Transform PlayerTransform()
    {
        if (Pacer != null && Pacer.Player != null) return Pacer.Player;
        PlayerController pc = UnityEngine.Object.FindObjectOfType<PlayerController>();
        return pc != null ? pc.transform : null;
    }

    void HookPlayer()
    {
        Transform p = PlayerTransform();
        if (p == null || !p.TryGetComponent(out _playerHealth)) return;
        _playerHealth.Damaged += OnPlayerDamaged;
    }

    void OnPlayerDamaged(DamageInfo info) => _hurtAt = Time.time;

    void OnPhaseChanged(PacePhase phase)
    {
        if (phase == PacePhase.Done) Finish(true);
    }

    void Finish(bool byBoss)
    {
        if (_ended) return;
        _ended = true;
        if (Pacer != null) Pacer.PhaseChanged -= OnPhaseChanged;
        if (_playerHealth != null) _playerHealth.Damaged -= OnPlayerDamaged;
        try
        {
            OnEnd(byBoss);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"出来事 {GetType().Name} の終わりで例外：{e}");
        }
        foreach (var go in _marks) if (go != null) UnityEngine.Object.Destroy(go);
        _marks.Clear();
        if (Runner != null)
        {
            Runner.ClearSpawned(); // 落下物・判定・スキンが預けた飾り（SkinDresser.Adopt）・予告の帯
            if (_ownRunner) UnityEngine.Object.Destroy(Runner.gameObject);
        }
        if (_begun && Announce && !byBoss) EventShow.Outro();
    }

    /// <summary>
    /// 出来事用の AttackRunner を用意する（StagePacer が渡した物。無ければ自分でプレハブから作る）。
    /// ステージのスキン・色・アイコンと、狙う相手を入れる（入れてから出すと、部品がスキンの絵を付ける）
    /// </summary>
    static AttackRunner PrepareRunner(AttackRunner given, StagePacer pacer, out bool own)
    {
        own = false;
        AttackRunner runner = given;
        if (runner == null)
        {
            runner = PaceAssets.CreateRunner();
            own = runner != null;
        }
        if (runner == null) return null;
        SkinId skin = pacer != null ? pacer.Skin : SkinId.Generic;
        if (pacer != null && pacer.IsFinal && skin == SkinId.None) skin = SkinId.Terminal;
        runner.Skin = skin == SkinId.None ? SkinId.Generic : skin;
        runner.SkinTheme = pacer != null ? pacer.Theme : null;
        runner.SkinIcon = pacer != null ? pacer.Icon : null;
        if (pacer != null && pacer.Player != null) runner.Target = pacer.Player;
        return runner;
    }
}

/// <summary>【仮】出来事の始まりと終わりの見せ方（3-1。文字の説明はしない）</summary>
public static class EventShow
{
    /// <summary>始まり：画面のふちが 0.3秒赤く光り、隅に通知（アプリのアイコン＋短い文字。飾り）</summary>
    public static void Intro(StagePacer pacer, string toast)
    {
        ScreenFx.EdgeRed(0.6f, 0.35f, 0.3f);
        if (string.IsNullOrEmpty(toast)) return;
        Sprite icon = pacer != null ? pacer.Icon : null;
        Color edge = FxKit.MainOf(pacer != null ? pacer.Theme : null, FxKit.EnemyRed);
        CornerToast.Show(toast, icon, edge);
    }

    /// <summary>終わり：白い光 0.06秒</summary>
    public static void Outro() => ScreenFx.WhiteFlash(0.4f, 0.06f);
}
