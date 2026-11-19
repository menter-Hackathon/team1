using System;
using UnityEngine;

// 【仮】KitMotion の部品を「だれの物として出すか」（片づけ先。2026-11-19。docs/stage-gimmick-variety.md 1章）。担当0 の物。
// ・AttackRunner（ボスの技・ctx.Hazard・出来事の Runner）：その人の出した物として登録する。ボスなら 50％・100％で消える。
//   当たったときの絵（アプリのスキン）も、その人のスキンになる
// ・StageRuleContext（StageRule の ctx）：ctx.Hazard に登録（無ければ ctx.TrackHazard）。片づけの表で消える
//   ※ 危ない物を出す前に ctx.CanSpawnHazard を見るのは、呼ぶ側（KitMotion の中では見ない。場面③の出来事では false のため）
// ・出来事（PaceEvent）の中：KitOwner.Of(Track, Runner)。KitTimedEvent を継げば Owner に入っている
// ・Transform：その子にする（親が消えると一緒に消える）
// ・KitOwner.None：だれの物でもない（自分で終わって消える。試し用）

/// <summary>KitMotion の部品の持ち主（片づけ先）。AttackRunner・StageRuleContext・Transform・Action&lt;GameObject&gt; からそのまま変換できる</summary>
public struct KitOwner
{
    /// <summary>出した物として登録する人（危ない物の判定もこの人の陣営・スキンで作る）。null なら Track・Parent</summary>
    public AttackRunner Runner;
    /// <summary>覚えてもらう関数（PaceEvent.Track・ctx.TrackHazard など）。Runner が無いときだけ使う</summary>
    public Action<GameObject> Track;
    /// <summary>親（あれば、その子にする）</summary>
    public Transform Parent;

    /// <summary>だれの物でもない（自分で終わって消える）</summary>
    public static KitOwner None => default;

    /// <summary>ボスの物か（予告を 0.8秒以上にする）</summary>
    public bool IsBoss => Runner != null && Runner.GetComponent<BossController>() != null;

    /// <summary>覚えてもらう関数と、出した物として登録する人（どちらも省略できる）。出来事の中：KitOwner.Of(Track, Runner)</summary>
    public static KitOwner Of(Action<GameObject> track, AttackRunner runner = null) => new KitOwner { Track = track, Runner = runner };

    public static implicit operator KitOwner(AttackRunner runner) => new KitOwner { Runner = runner };

    public static implicit operator KitOwner(Transform parent) => new KitOwner { Parent = parent };

    public static implicit operator KitOwner(Action<GameObject> track) => new KitOwner { Track = track };

    public static implicit operator KitOwner(StageRuleContext ctx)
    {
        if (ctx == null) return default;
        return new KitOwner { Runner = ctx.Hazard, Track = go => ctx.TrackHazard(go) };
    }
}
