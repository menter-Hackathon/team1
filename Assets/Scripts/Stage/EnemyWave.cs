using System;
using UnityEngine;

/// <summary>
/// 部屋の雑魚ウイルスの集まり（仕様書 2.2：雑魚との戦闘 → 全員倒すとアプリボスが出現）。
/// 全員倒したら Cleared を出す。ボスの登場（BossEntrance）や、M5 のステージ進行はこれを見る。
/// </summary>
public class EnemyWave : MonoBehaviour
{
    [Tooltip("倒すべき敵。空なら子の Health をすべて使う")]
    [SerializeField] Health[] _enemies;

    bool _cleared;

    public int Total => _enemies != null ? _enemies.Length : 0;
    /// <summary>まだ倒していない敵の数</summary>
    public int Remaining { get; private set; }
    public bool IsCleared => _cleared;

    /// <summary>残りの数が変わったとき（残り, 全体）。UI 用</summary>
    public event Action<int, int> RemainingChanged;
    /// <summary>全員倒したとき（1回だけ）</summary>
    public event Action Cleared;

    void Awake()
    {
        if (_enemies == null || _enemies.Length == 0) _enemies = GetComponentsInChildren<Health>(true);
        Remaining = 0;
        foreach (var e in _enemies)
        {
            if (e != null && !e.IsDead) Remaining++;
        }
    }

    void OnEnable()
    {
        foreach (var e in _enemies)
        {
            if (e != null) e.Died += OnEnemyDied;
        }
    }

    void OnDisable()
    {
        foreach (var e in _enemies)
        {
            if (e != null) e.Died -= OnEnemyDied;
        }
    }

    void Start()
    {
        if (Remaining == 0) Clear(); // 敵がいない部屋は最初からクリア
    }

    void OnEnemyDied()
    {
        if (_cleared) return;
        Remaining = Mathf.Max(0, Remaining - 1);
        RemainingChanged?.Invoke(Remaining, Total);
        if (Remaining == 0) Clear();
    }

    void Clear()
    {
        if (_cleared) return;
        _cleared = true;
        Cleared?.Invoke();
    }
}
