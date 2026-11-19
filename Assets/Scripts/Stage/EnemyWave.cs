using System;
using UnityEngine;

/// <summary>
/// 部屋の雑魚ウイルスの集まり（仕様書 2.2：雑魚との戦闘 → 全員倒すとアプリボスが出現）。
/// 全員倒したら Cleared を出す。ボスの登場（BossEntrance）や、M5 のステージ進行はこれを見る。
/// あとから出てくる雑魚（ZakoSpawner）は AddEnemy で数に入れる。出し終えるまでは Hold で、クリアを待たせる（2026-11-17）
/// </summary>
public class EnemyWave : MonoBehaviour
{
    [Tooltip("倒すべき敵。空なら子の Health をすべて使う")]
    [SerializeField] Health[] _enemies;

    bool _cleared;
    int _holds;
    int _total;
    int _initial;
    int _expectedExtra;
    int _killed;

    public int Total => _total;
    /// <summary>倒した数</summary>
    public int Killed => _killed;
    /// <summary>ボスまでに倒す数（最初の雑魚＋あとから出てくる予定の数）</summary>
    public int Expected => Mathf.Max(_initial + _expectedExtra, _total);
    /// <summary>ボスまでの進み具合（0〜1。画面右上の「ボスが出るまでのゲージ」用。2026-11-17）</summary>
    public float Progress => _cleared ? 1f : Mathf.Clamp01(_killed / (float)Mathf.Max(1, Expected));
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
        _total = _enemies.Length;
        _initial = _enemies.Length;
    }

    /// <summary>あとから出てくる予定の数を足す（ZakoSpawner のノルマ）</summary>
    public void Expect(int count) => _expectedExtra += Mathf.Max(0, count);

    /// <summary>あとから出てきた雑魚を数に入れる</summary>
    public void AddEnemy(Health enemy)
    {
        if (enemy == null || _cleared || enemy.IsDead) return;
        enemy.Died += OnEnemyDied;
        _total++;
        Remaining++;
        RemainingChanged?.Invoke(Remaining, Total);
    }

    /// <summary>クリアを待たせる（雑魚を出し終えるまで）。Release と組で呼ぶ</summary>
    public void Hold() => _holds++;

    /// <summary>待たせるのをやめる。残りが0なら、ここでクリア</summary>
    public void Release()
    {
        _holds = Mathf.Max(0, _holds - 1);
        if (_holds == 0 && Remaining == 0) Clear();
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
        if (Remaining == 0 && _holds == 0) Clear(); // 敵がいない部屋は最初からクリア
    }

    void OnEnemyDied()
    {
        if (_cleared) return;
        _killed++;
        Remaining = Mathf.Max(0, Remaining - 1);
        RemainingChanged?.Invoke(Remaining, Total);
        if (Remaining == 0 && _holds == 0) Clear();
    }

    void Clear()
    {
        if (_cleared) return;
        _cleared = true;
        Cleared?.Invoke();
        if (Time.timeSinceLevelLoad > 1f) GameAudio.Event("wave_clear"); // 始まった瞬間から空の集まりでは鳴らさない
    }
}
