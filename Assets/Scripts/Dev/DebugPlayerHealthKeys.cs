using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 【確認用・仮】頭の上のHPバーを確かめるキー（2026-11-20）。
/// ・F9：プレイヤーに 1 ダメージ（被弾後の無敵中でも、残り2以上なら 1 減らして、続けて当たったときの点滅を確かめられる）
/// ・F10：プレイヤーを 1 回復
/// エディタと開発用のビルドだけで動く（本番のビルドでは動かない）。DebugInvincibility と同じ作り
/// </summary>
public class DebugPlayerHealthKeys : MonoBehaviour
{
    InputAction _damage;
    InputAction _heal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        if (FindObjectOfType<DebugPlayerHealthKeys>() != null) return;
        var go = new GameObject("DebugPlayerHealthKeys");
        DontDestroyOnLoad(go);
        go.AddComponent<DebugPlayerHealthKeys>();
    }

    void Awake()
    {
        _damage = new InputAction("DebugPlayerDamage", InputActionType.Button, "<Keyboard>/f9");
        _heal = new InputAction("DebugPlayerHeal", InputActionType.Button, "<Keyboard>/f10");
    }

    void OnEnable()
    {
        _damage.Enable();
        _heal.Enable();
    }

    void OnDisable()
    {
        _damage.Disable();
        _heal.Disable();
    }

    void OnDestroy()
    {
        _damage.Dispose();
        _heal.Dispose();
    }

    void Update()
    {
        if (_damage.WasPressedThisFrame()) Damage(FindPlayerHealth());
        if (_heal.WasPressedThisFrame()) Heal(FindPlayerHealth());
    }

    static Health FindPlayerHealth()
    {
        foreach (PlayerController pc in FindObjectsOfType<PlayerController>())
        {
            Health h = pc.GetComponent<Health>();
            if (h != null && h.Team == Team.Player) return h;
        }
        return null;
    }

    /// <summary>ふつうの被弾として 1 当てる。無敵などで止められ、残りが2以上なら、値だけ 1 減らす</summary>
    public static void Damage(Health h)
    {
        if (h == null || h.IsDead) return;
        var info = new DamageInfo { Amount = 1, SourceTeam = Team.Enemy, HitPoint = h.transform.position };
        if (!h.TryTakeDamage(info) && h.Current > 1) h.SetCurrent(h.Current - 1);
    }

    public static void Heal(Health h)
    {
        if (h != null) h.Heal(1);
    }
}
