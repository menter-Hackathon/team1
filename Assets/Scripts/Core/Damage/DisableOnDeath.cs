using System.Collections;
using UnityEngine;

/// <summary>
/// 倒されたら少し待って非表示にする（敵・壊せる物用）。
/// M4 のスキル獲得などは Health.Died を別に受け取って行う。
/// </summary>
[RequireComponent(typeof(Health))]
public class DisableOnDeath : MonoBehaviour
{
    [Tooltip("倒されてから消えるまでの秒数")]
    [SerializeField, Min(0f)] float _delay = 0.15f;

    Health _health;

    void Awake() => _health = GetComponent<Health>();
    void OnEnable() => _health.Died += OnDied;
    void OnDisable() => _health.Died -= OnDied;

    void OnDied() => StartCoroutine(DisableLater());

    IEnumerator DisableLater()
    {
        yield return new WaitForSeconds(_delay);
        gameObject.SetActive(false);
    }
}
