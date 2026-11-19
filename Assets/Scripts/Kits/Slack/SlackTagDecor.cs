using UnityEngine;

/// <summary>
/// 【仮】リアクションの雨の札の飾り（2026-11-19）。札（FallingObject）に付ける。
/// ・落ちる所の影の上に赤い「！」（影と同じく、着地で消える）
/// ・札が見えている間、右下に数字（1 → 2 と増える。飾り）
/// </summary>
public class SlackTagDecor : MonoBehaviour
{
    FallingObject _f;
    TextMesh _count;
    SpriteRenderer _countBg;
    float _appearAt;

    public static void Attach(FallingObject f, float appearAt)
    {
        if (f == null) return;
        var d = f.gameObject.AddComponent<SlackTagDecor>();
        d._f = f;
        d._appearAt = appearAt;
        Transform shadow = f.transform.Find("Shadow");
        if (shadow != null)
        {
            var bang = TalkArt.ShowNative(shadow, TalkArt.Bang, new Vector2(0f, 0.55f), 18, 1.5f, "Bang");
            bang.gameObject.AddComponent<SlackBlink>();
        }
        Vector2 s = f.Data != null ? f.Data.ObjectSize : Vector2.one * 1.5f;
        var root = TalkArt.Child(f.transform, "Count", (Vector2)f.transform.position + new Vector2(s.x * 0.42f, -s.y * 0.38f));
        d._countBg = TalkArt.Show(root, TalkArt.Disc(10), Vector2.zero, new Vector2(0.75f, 0.75f), (Color)SlackArt.PillEdge, 21, "Bg");
        d._count = TalkArt.Text(root, "1", Vector2.zero, 0.4f, Color.white, 22);
        d.SetVisible(false);
    }

    void SetVisible(bool on)
    {
        if (_countBg != null) _countBg.enabled = on;
        if (_count != null) _count.gameObject.SetActive(on);
    }

    void Update()
    {
        if (_f == null) return;
        bool on = _f.IsVisible && !_f.HasLanded;
        SetVisible(on);
        if (on && _count != null) _count.text = Time.time - _appearAt > 0.3f ? "2" : "1";
    }
}

/// <summary>【仮】赤い「！」の点滅（2026-11-19）</summary>
public class SlackBlink : MonoBehaviour
{
    SpriteRenderer _sr;
    void Awake() => _sr = GetComponent<SpriteRenderer>();
    void Update()
    {
        if (_sr != null) _sr.enabled = Mathf.FloorToInt(Time.time / 0.15f) % 2 == 0;
    }
}
