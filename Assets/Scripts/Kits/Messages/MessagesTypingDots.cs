using UnityEngine;

/// <summary>
/// 【仮】ボスの頭の上の「…」（入力中。2026-11-19。メッセージのキット）。飾りだけ。ボスについて動く。
/// CustomEffect なので、形態変化・正常化で消える
/// </summary>
public class MessagesTypingDots : CustomEffect
{
    AttackRunner _owner;
    float _start, _end;
    SpriteRenderer _sr;

    public static MessagesTypingDots Spawn(AttackRunner owner, float life)
    {
        if (owner == null) return null;
        GameObject go = owner.CreateSpawnedObject("Messages_Typing", owner.OriginPoint);
        var d = go.AddComponent<MessagesTypingDots>();
        d._owner = owner;
        d._start = Time.time;
        d._end = d._start + Mathf.Max(0.2f, life);
        d._sr = TalkArt.ShowNative(go.transform, MessagesArt.TypingBubble(0), Vector2.zero, 41, 2f, "Dots");
        d.Place();
        return d;
    }

    void Place()
    {
        Bounds b = TalkKitUtil.OwnerBody(_owner);
        transform.position = new Vector3(b.max.x + 0.4f, b.max.y + 0.7f, 0f);
    }

    void Update()
    {
        if (_owner == null || Time.time >= _end) { Destroy(gameObject); return; }
        Place();
        _sr.sprite = MessagesArt.TypingBubble(Mathf.FloorToInt((Time.time - _start) / 0.15f));
    }
}
