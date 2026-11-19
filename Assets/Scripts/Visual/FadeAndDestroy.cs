using UnityEngine;

/// <summary>だんだん薄くなって消える（残像など）</summary>
public class FadeAndDestroy : MonoBehaviour
{
    SpriteRenderer _sr;
    float _start;
    float _life = 0.2f;
    float _alpha;

    public void Setup(float life)
    {
        _sr = GetComponent<SpriteRenderer>();
        _life = Mathf.Max(0.01f, life);
        _start = Time.time;
        _alpha = _sr != null ? _sr.color.a : 1f;
    }

    void Update()
    {
        float t = (Time.time - _start) / _life;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        if (_sr != null)
        {
            Color c = _sr.color;
            c.a = _alpha * (1f - t);
            _sr.color = c;
        }
    }
}
