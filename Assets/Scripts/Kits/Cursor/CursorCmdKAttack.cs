using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】能力 ⌘K（Cursor を倒すと手に入る。2026-11-19。docs/app-kits-2-design.md 3-3）。1回押し。AI まかせ。
/// 押すと、その場で作る能力の7つのひな形（AbilityCatalog の Templates。アセットを作るときに写す）からランダムに1つが出る。
/// 出る前に、灰色のゴーストで 0.15秒だけ形が見える。続けて同じひな形は出ない。ゲージ・ダメージは出たひな形のまま。クールタイムはこの攻撃データ（4秒）。
/// ひな形が1つも無いときは、正面の箱の判定（_fallback）を出す。
/// </summary>
[CreateAssetMenu(fileName = "Cursor_CmdK", menuName = "Team1/Kits/Cursor/CmdK")]
public class CursorCmdKAttack : CustomAttack
{
    [Tooltip("ひな形の攻撃データ（AbilityCatalog の Templates を写す。アセットを作るときに入る）")]
    [SerializeField] AttackData[] _templates;
    [Tooltip("ひな形が無いときの攻撃データ（正面の判定）")]
    [SerializeField] AttackData _fallback;
    [Tooltip("【仮】ゴーストを見せる秒数")]
    [SerializeField, Min(0f)] float _preview = 0.15f;

    [System.NonSerialized] int _last = -1;

    public override bool CanRun(AttackRunner owner, AttackData data) => owner != null && (Count > 0 || _fallback != null);

    int Count => _templates != null ? _templates.Length : 0;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        AttackData pick = Pick();
        try { CursorCmdKGhost.Spawn(owner, pick, _preview); }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        if (_preview > 0f) yield return new WaitForSeconds(_preview);
        if (pick != null && owner != null) yield return owner.Execute(pick);
    }

    AttackData Pick()
    {
        int n = Count;
        if (n == 0) return _fallback;
        int i = Random.Range(0, n);
        if (n > 1 && i == _last) i = (i + 1 + Random.Range(0, n - 1)) % n;
        _last = i;
        return _templates[i] != null ? _templates[i] : _fallback;
    }
}
