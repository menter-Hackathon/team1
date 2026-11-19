using UnityEngine;

/// <summary>
/// 【仮】Slack のボスのメッセージ（画面の上の灰色の吹き出し。リアクションの雨で出す。2026-11-19）。飾りだけ（当たらない）。
/// 上から少し下りて出て、時間が来たらうすくなって消える。CustomEffect なので、形態変化・正常化で消える
/// </summary>
public class SlackMessageBar : CustomEffect
{
    const int Order = 30;

    float _start, _end;
    SpriteRenderer _sr;
    TextMesh _channel;
    Vector2 _pos;

    public static SlackMessageBar Spawn(AttackRunner owner, Vector2 center, Vector2 size, float life)
    {
        if (owner == null) return null;
        GameObject go = owner.CreateSpawnedObject("Slack_MessageBar", center);
        var m = go.AddComponent<SlackMessageBar>();
        m._pos = center;
        m._start = Time.time;
        m._end = m._start + Mathf.Max(0.3f, life);
        int w = Mathf.RoundToInt(size.x * 18f), h = Mathf.RoundToInt(size.y * 18f);
        m._sr = TalkArt.ShowNative(go.transform, SlackArt.MessageBar(w, h), Vector2.zero, Order, 1f, "Bar");
        // チャンネルの名前（作り物）：吹き出しの左上の外に小さく
        m._channel = TalkArt.Text(go.transform, "#general", new Vector2(-size.x * 0.5f + 0.1f, size.y * 0.5f + 0.3f), 0.4f, Color.white, Order + 1, true, TextAnchor.MiddleLeft);
        return m;
    }

    void Update()
    {
        float now = Time.time;
        if (now >= _end) { Destroy(gameObject); return; }
        float inT = Mathf.Clamp01((now - _start) / 0.15f);
        float outT = Mathf.Clamp01((_end - now) / 0.25f);
        float a = Mathf.Min(inT, outT);
        transform.position = new Vector3(_pos.x, _pos.y + (1f - inT) * 0.6f, 0f);
        _sr.color = new Color(1f, 1f, 1f, a);
        WorldText.SetColor(_channel, new Color(1f, 1f, 1f, a * 0.85f));
    }
}
