using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】キットの場面③の出来事を、時間の表で手早く書くための土台（2026-11-19。docs/stage-gimmick-variety.md 2章）。担当0 の物。
// ・PaceEvent を継ぐ（始まりの赤いふち・隅の通知・終わりの白い光・ボスの登場での片づけ・ダメージのあと 1秒は新しい予告を出さない は今までどおり）
// ・Build() の中で At(秒, …)・Every(…) を並べる。秒は「最初の予告の時刻」（通知から 0.8秒後）から数える
// ・ダメージを受けて 1秒の間は、表の時計を止める（その間に新しい予告を出さない）
// ・Owner を KitMotion の部品に渡す（この出来事の物になり、終わり・ボスの登場で全部消える）
// ・長さ：EndAt(秒) で決める（無ければ最後の手順＋2.5秒）。StagePacer は 1画面のステージで 20秒・ゲートのステージで 12秒で打ち切る
// ・登録：キットのファイルで
//     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//     static void Register() => StageEvents.Register("Slack", stage => new SlackThreadEvent());
//
// 例（KitMotionSampleEvent も見る）
//   public class SlackThreadEvent : KitTimedEvent
//   {
//       public SlackThreadEvent() : base("Thread") { }
//       protected override void Build()
//       {
//           Every(0f, 1.6f, 4, i => KitMotion.Erupt(Owner, PlayerPos.x, 1.6f, 2.8f));
//           At(3f, () => KitMotion.HitTarget(Owner, new Vector2(View.center.x, Floor + 4f), 3f, onHit: () => KitPlayer.RefillCooldowns()));
//           EndAt(9f);
//       }
//   }

/// <summary>時間の表で動くキットの出来事（場面③）</summary>
public class KitTimedEvent : PaceEvent
{
    class Timed
    {
        public float At;
        public Action Act;
        public bool Done;
    }

    readonly List<Timed> _steps = new List<Timed>();
    readonly Action<KitTimedEvent> _build;
    float _clock;
    float _length = -1f;
    bool _stopped;
    AttackRunner _runner;
    bool _ownRunner;

    /// <summary>toast：隅の通知の文字（無ければ出さない）。build：継がずに表を書くとき（Build のかわり）</summary>
    public KitTimedEvent(string toast = null, Action<KitTimedEvent> build = null)
    {
        Toast = toast;
        _build = build;
    }

    // 危ない物の AttackRunner は自分で用意する（プレハブが無くても、出来事を止めない。そのときは持ち主なしの判定になる）
    protected override bool NeedsRunner => false;

    /// <summary>この秒数で必ず終わる（表の長さ＋ゆとり。ダメージで時計が止まった分も入る）</summary>
    public override float MaxSeconds => StartDelay + Length + 4f;

    /// <summary>KitMotion の部品に渡す持ち主（この出来事の物になり、終わりで全部消える）</summary>
    public KitOwner Owner => KitOwner.Of(Track, _runner);
    /// <summary>今の画面</summary>
    public Rect View => Area;
    /// <summary>床の高さ</summary>
    public float Floor => FloorY(Area);
    /// <summary>強さ（1＝弱い〜3＝強い）</summary>
    public int Level => Strength;
    /// <summary>表の時計（最初の予告の時刻から。ダメージのあとは止まる）</summary>
    public float Clock => _clock;
    /// <summary>表の長さ（秒）</summary>
    public float Length
    {
        get
        {
            if (_length >= 0f) return _length;
            float last = 0f;
            foreach (var s in _steps) last = Mathf.Max(last, s.At);
            return last + 2.5f;
        }
    }
    /// <summary>プレイヤーの足元（いなければ画面のまん中）。体のまん中は KitMotion.PlayerPos</summary>
    public new Vector2 PlayerPos => base.PlayerPos;

    /// <summary>seconds 秒に step をする</summary>
    public KitTimedEvent At(float seconds, Action step)
    {
        if (step != null) _steps.Add(new Timed { At = Mathf.Max(0f, seconds), Act = step });
        return this;
    }

    /// <summary>start 秒から interval 秒おきに count 回、step(何回目か 0〜) をする</summary>
    public KitTimedEvent Every(float start, float interval, int count, Action<int> step)
    {
        if (step == null) return this;
        for (int i = 0; i < count; i++)
        {
            int n = i;
            At(start + interval * i, () => step(n));
        }
        return this;
    }

    /// <summary>表の長さを決める（この秒数で終わる）</summary>
    public KitTimedEvent EndAt(float seconds)
    {
        _length = Mathf.Max(0f, seconds);
        return this;
    }

    /// <summary>早く終わる（次のフレームで。出した物は全部消える）。HitTarget の onHit で「出来事が終わる」など</summary>
    public void Stop() => _stopped = true;

    /// <summary>表を書く（At・Every・EndAt）。始まった時に1回（Pacer・Strength・画面が分かっている）</summary>
    protected virtual void Build() { }

    /// <summary>毎フレーム（表の時計）。表のほかに、自分で見たい物があれば</summary>
    protected virtual void OnFrame(float clock) { }

    protected override void OnBegin()
    {
        _runner = PaceAssets.CreateRunner();
        _ownRunner = _runner != null;
        if (_runner != null)
        {
            SkinId skin = Pacer != null ? Pacer.Skin : SkinId.Generic;
            _runner.Skin = skin == SkinId.None ? SkinId.Generic : skin;
            _runner.SkinTheme = Pacer != null ? Pacer.Theme : null;
            _runner.SkinIcon = Pacer != null ? Pacer.Icon : null;
            if (Pacer != null && Pacer.Player != null) _runner.Target = Pacer.Player;
        }
        Build();
        if (_build != null) _build(this);
    }

    protected override bool OnTick(float dt)
    {
        if (_stopped) return false;
        if (!Quiet) _clock += dt;
        for (int i = 0; i < _steps.Count; i++) // 手順の中で At を足してもよい
        {
            Timed s = _steps[i];
            if (s.Done || _clock < s.At) continue;
            s.Done = true;
            try { s.Act(); }
            catch (Exception e) { Debug.LogException(e); }
            if (_stopped) return false;
        }
        try { OnFrame(_clock); }
        catch (Exception e) { Debug.LogException(e); }
        return !_stopped && _clock < Length;
    }

    protected override void OnEnd(bool byBoss)
    {
        if (_runner == null) return;
        _runner.Cancel();
        _runner.ClearSpawned();
        if (_ownRunner) UnityEngine.Object.Destroy(_runner.gameObject);
        _runner = null;
    }
}
