using UnityEngine;

// 【仮】出来事「群れ」（Browser）の画面の上のタブの動き（2026-11-19。docs/stage-pacing-design.md 3-3）。担当2 の物。
// 出るとき：0 → 115% → 100% にふくらむ（Delay 秒待ってから）。雑魚が落ちている間は、小さく上下にゆれる。
// Close：横に縮んで消える（タブを閉じる感じ）。文字は出さない。

/// <summary>画面の上のタブ（群れの出口）の出る・ゆれる・閉じる</summary>
public class PaceTabPop : MonoBehaviour
{
    /// <summary>出るまで待つ秒数</summary>
    public float Delay;

    float _age;
    float _closeAt = -1f;
    Vector3 _base;
    bool _ready;

    /// <summary>横に縮んで消える</summary>
    public void Close()
    {
        if (_closeAt < 0f) _closeAt = _age;
    }

    void Update()
    {
        if (!_ready)
        {
            _ready = true;
            _base = transform.position;
        }
        _age += Time.deltaTime;
        if (_closeAt >= 0f)
        {
            float k = Mathf.Clamp01((_age - _closeAt) / 0.15f);
            transform.localScale = new Vector3(1f - k, 1f, 1f);
            if (k >= 1f) Destroy(gameObject);
            return;
        }
        float t = _age - Delay;
        if (t < 0f)
        {
            transform.localScale = Vector3.zero;
            return;
        }
        float p = t / 0.12f;
        float s = p < 0.6f ? Mathf.Lerp(0f, 1.15f, p / 0.6f) : p < 1f ? Mathf.Lerp(1.15f, 1f, (p - 0.6f) / 0.4f) : 1f;
        transform.localScale = new Vector3(s, s, 1f);
        // 小さくゆれる（1ドット。ドットにそろえる）
        float bob = Mathf.Round(Mathf.Sin(t * 6f + _base.x) * 1.2f) * FxKit.Pixel;
        transform.position = _base + new Vector3(0f, bob, 0f);
    }
}
