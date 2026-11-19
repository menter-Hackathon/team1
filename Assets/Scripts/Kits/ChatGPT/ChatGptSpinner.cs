using UnityEngine;

/// <summary>
/// 【仮】回る印（2026-11-19。ChatGPT の Regenerate の矢印など）。担当3 の物。
/// 絵を1枚出して回し、seconds 秒で消える。出るときはポンと大きくなる。CustomEffect なので形態変化（50％）で消える
/// </summary>
public class ChatGptSpinner : CustomEffect
{
    const int Order = 33;

    SpriteRenderer _sr;
    float _start;
    float _end;
    float _spin;
    float _scale = 1f;

    public void Setup(Sprite sprite, float seconds, float degreesPerSecond, float scale = 1f)
    {
        _sr = RuntimeSprites.Show(transform, sprite, Vector2.zero, Order, 1f, "Spin");
        _start = Time.time;
        _end = _start + Mathf.Max(0.1f, seconds);
        _spin = degreesPerSecond;
        _scale = scale;
        Apply();
    }

    void Update()
    {
        if (Time.time >= _end)
        {
            Destroy(gameObject);
            return;
        }
        Apply();
    }

    void Apply()
    {
        if (_sr == null) return;
        float age = Time.time - _start;
        float pop = age < 0.1f ? Mathf.Lerp(0.2f, 1.15f, age / 0.1f) : age < 0.16f ? Mathf.Lerp(1.15f, 1f, (age - 0.1f) / 0.06f) : 1f;
        float s = _scale * pop;
        _sr.transform.localScale = new Vector3(s, s, 1f);
        _sr.transform.localRotation = Quaternion.Euler(0f, 0f, _spin * age);
        float left = _end - Time.time;
        Color c = Color.white;
        c.a = left < 0.2f ? left / 0.2f : 1f;
        _sr.color = c;
    }
}
