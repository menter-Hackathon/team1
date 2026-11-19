// 【仮・つなぎ目】ステージの出来事（アプリごとの事件。2026-11-19。docs/stage-pacing-design.md 3章・10-2）。
// 名前と形だけを先に決めた物。中身・表は担当2（出来事ともや）が作る。

/// <summary>出来事の型（雨・柱床・群れ）が継ぐ</summary>
public abstract class StageEvent
{
    /// <summary>true（群れ）：場面③の雑魚は出来事が SpawnNow で出す。false：StagePacer が出す</summary>
    public virtual bool SpawnsOwnZako => false;
    /// <summary>始める。strength：1＝弱い、2＝ふつう、3＝強い。zakoBudget：出来事が出してよい雑魚の数（SpawnsOwnZako が false なら 0）</summary>
    public abstract void Begin(StagePacer pacer, AttackRunner hazardRunner, int strength, int zakoBudget);
    /// <summary>毎フレーム。false を返したら終わり</summary>
    public abstract bool Tick(float dt);
    /// <summary>出した物を全部消す（途中で呼ばれてもよい）</summary>
    public abstract void End();
}
