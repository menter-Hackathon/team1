using UnityEngine;

/// <summary>
/// スプライトを決まった速さで順番に切り替える、簡単なコマ送りアニメーション。
/// 雑魚・ボスの煙・エフェクトに使う（プレイヤーは Animator を使う）。
/// ・繰り返し：最後のコマの次は最初に戻る
/// ・1回だけ：最後のコマで止まる。「終わったら消す」なら GameObject を消す（エフェクト用）
/// </summary>
public class SpriteFrameAnimation : MonoBehaviour
{
    [SerializeField] SpriteRenderer _renderer;
    [SerializeField] Sprite[] _frames;
    [Tooltip("1秒あたりのコマ数")]
    [SerializeField, Min(0.1f)] float _fps = 8f;
    [SerializeField] bool _loop = true;
    [Tooltip("1回だけのとき、最後のコマまで進んだら GameObject を消す")]
    [SerializeField] bool _destroyWhenDone;
    [Tooltip("繰り返しのとき、始まるコマをばらばらにする（同じ物が並んでもそろって動かないように）")]
    [SerializeField] bool _randomStart;

    float _start;

    public bool IsDone { get; private set; }

    /// <summary>コードから設定する（エフェクトを作るとき）</summary>
    public void Setup(SpriteRenderer target, Sprite[] frames, float fps, bool loop, bool destroyWhenDone, bool randomStart = false)
    {
        _renderer = target;
        _frames = frames;
        _fps = Mathf.Max(0.1f, fps);
        _loop = loop;
        _destroyWhenDone = destroyWhenDone;
        _randomStart = randomStart;
        Restart();
    }

    /// <summary>最初のコマからやり直す</summary>
    public void Restart()
    {
        IsDone = false;
        float length = _frames != null && _frames.Length > 0 ? _frames.Length / _fps : 0f;
        _start = Time.time - (_randomStart && _loop ? Random.value * length : 0f);
        Apply();
    }

    void Awake()
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
    }

    void OnEnable() => Restart();

    void Update() => Apply();

    void Apply()
    {
        if (_renderer == null || _frames == null || _frames.Length == 0) return;
        int index = Mathf.FloorToInt((Time.time - _start) * _fps);
        if (index >= _frames.Length)
        {
            if (_loop) index %= _frames.Length;
            else
            {
                index = _frames.Length - 1;
                if (!IsDone)
                {
                    IsDone = true;
                    if (_destroyWhenDone)
                    {
                        Destroy(gameObject);
                        return;
                    }
                }
            }
        }
        if (index < 0) index = 0;
        _renderer.sprite = _frames[index];
    }
}
