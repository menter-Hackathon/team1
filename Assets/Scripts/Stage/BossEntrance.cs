using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 雑魚を全員倒したら、アプリボスを登場させる（仕様書 2.2 の4）。
/// ・ボスは最初は隠しておく（非表示）
/// ・EnemyWave.Cleared の少し後に表示し、点滅しながら出てくる。点滅の間は体当たりの判定なし
///   （プレイヤーがボスの出る場所にいても、出てきた瞬間には当たらない）
/// ・攻撃を始めるまでの時間は、ボスのプロフィールの「開始までの待ち時間」
/// </summary>
public class BossEntrance : MonoBehaviour
{
    [SerializeField] EnemyWave _wave;
    [SerializeField] BossController _boss;
    [Tooltip("雑魚を全員倒してから、ボスが出てくるまでの秒数")]
    [SerializeField, Min(0f)] float _delay = 1f;
    [Tooltip("出てくるときに点滅する秒数（この間は体当たりなし）")]
    [SerializeField, Min(0f)] float _blinkTime = 0.8f;
    [Tooltip("オン：雑魚ではなく、ボス部屋の入口（BossRoomGate）が Trigger を呼んだら出てくる（横スクロール・ウィンドウ移動のステージ）")]
    [SerializeField] bool _waitForGate;

    bool _triggered;

    public BossController Boss => _boss;
    /// <summary>雑魚ではなく、ボス部屋の入口で出てくるか（横スクロール・ウィンドウ移動）</summary>
    public bool WaitsForGate => _waitForGate;
    /// <summary>雑魚を全員倒して、ボスの登場が始まったか</summary>
    public bool IsTriggered => _triggered;
    /// <summary>ボスが出てきて、戦える状態になったか</summary>
    public bool HasAppeared { get; private set; }

    /// <summary>ボスの登場が始まったとき（「ボス出現！」の表示用）</summary>
    public event Action<BossController> Appearing;
    /// <summary>点滅が終わって、体当たりが有効になったとき</summary>
    public event Action<BossController> Appeared;

    void Awake()
    {
        if (_boss != null) _boss.gameObject.SetActive(false);
    }

    /// <summary>出すボスを決める（M5：ステージごとにプレハブから作ったボスを渡す）。雑魚を全員倒す前に呼ぶ</summary>
    public void SetBoss(BossController boss)
    {
        _boss = boss;
        if (_boss != null && !_triggered) _boss.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (_wave != null) _wave.Cleared += OnCleared;
    }

    void OnDisable()
    {
        if (_wave != null) _wave.Cleared -= OnCleared;
    }

    void Start()
    {
        if (_waitForGate) return;
        if (_wave == null || _wave.IsCleared) OnCleared();
    }

    /// <summary>ボスの登場を始める（ボス部屋の入口から呼ぶ）</summary>
    public void Trigger() => OnCleared();

    void OnCleared()
    {
        if (_triggered || _boss == null) return;
        _triggered = true;
        StartCoroutine(Appear());
    }

    IEnumerator Appear()
    {
        if (_delay > 0f) yield return new WaitForSeconds(_delay);

        Appearing?.Invoke(_boss);
        GameAudio.BossAppearing(_boss);
        _boss.gameObject.SetActive(true);
        yield return null; // ボスの Start（最初の状態に戻す）が終わるのを待つ
        _boss.SetContactEnabled(false);

        for (float t = 0f; t < _blinkTime; t += Time.deltaTime)
        {
            _boss.SetVisibility(Mathf.FloorToInt(t / 0.1f) % 2 == 0 ? 1f : 0.3f);
            yield return null;
        }
        _boss.SetVisibility(1f);
        _boss.SetContactEnabled(true);
        HasAppeared = true;
        Appeared?.Invoke(_boss);
    }
}
