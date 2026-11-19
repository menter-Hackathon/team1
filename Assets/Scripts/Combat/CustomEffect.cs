using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 専用の技が出す「自分で進む部品」の土台（2026-11-18。docs/app-boss-skills-design.md 1-2・1-3）。
/// ・AttackRunner.CreateSpawnedObject で作った GameObject に付ける。出した人が攻撃を止めても、自分で最後まで進む
/// ・ボスの形態変化（50%）では、この部品が付いた物が全部消される（AttackRunner.ClearSpawnedWith&lt;CustomEffect&gt;）。
///   正常化・ウイルスの追い出し（100%）では、出した物が全部消される（ClearSpawned）
/// ・消えるときは必ず Restore が呼ばれる（OnDestroy）。地形・プレイヤー・ボスの状態を変えた部品は、ここで元に戻す。
///   2回呼ばれても大丈夫に作る（自分で終わって Destroy したときも、外から消されたときも呼ばれる）
/// ・Lingers が true の部品が場にある間は、ボスは「場に残る技」（AttackData の Lingering）を選ばない（1-3）
/// ・CorruptsFloor が true の部品が場にある間は、「床が文字化けしている間は選ばない」技を選ばない（rm -rf と文字列の雨）
/// </summary>
public abstract class CustomEffect : MonoBehaviour
{
    static readonly List<CustomEffect> Active = new List<CustomEffect>();

    bool _restored;

    /// <summary>場に残る技の効果か（出ている間、ほかの場に残る技を選ばせない）</summary>
    public virtual bool Lingers => false;
    /// <summary>床を使えなくしているか（rm -rf の文字化けの床）</summary>
    public virtual bool CorruptsFloor => false;

    /// <summary>場に残る技の効果が、どこかに出ているか</summary>
    public static bool AnyLingering
    {
        get
        {
            Active.RemoveAll(e => e == null);
            foreach (var e in Active) if (e.Lingers) return true;
            return false;
        }
    }

    /// <summary>床が文字化けしているか</summary>
    public static bool AnyFloorCorrupted
    {
        get
        {
            Active.RemoveAll(e => e == null);
            foreach (var e in Active) if (e.CorruptsFloor) return true;
            return false;
        }
    }

    protected virtual void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
    }

    protected virtual void OnDisable() => Active.Remove(this);

    protected virtual void OnDestroy()
    {
        Active.Remove(this);
        RestoreOnce();
    }

    /// <summary>元に戻す（1回だけ）。自分で終わるときは、Destroy(gameObject) の前に呼ばなくてよい（OnDestroy で呼ばれる）</summary>
    protected void RestoreOnce()
    {
        if (_restored) return;
        _restored = true;
        Restore();
    }

    /// <summary>地形・プレイヤー・ボスの状態を元に戻す。何も変えない部品は空のままでよい</summary>
    protected virtual void Restore() { }
}
