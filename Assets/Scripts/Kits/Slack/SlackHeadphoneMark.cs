using UnityEngine;

/// <summary>
/// 【仮】ハドルの間、ボスの頭の上に出るヘッドホンの印（2026-11-19）。飾りだけ。ボスについて動く。
/// CustomEffect なので、形態変化・正常化で消える
/// </summary>
public class SlackHeadphoneMark : CustomEffect
{
    AttackRunner _owner;
    float _start, _end;
    Transform _icon;

    public static SlackHeadphoneMark Spawn(AttackRunner owner, float life)
    {
        if (owner == null) return null;
        GameObject go = owner.CreateSpawnedObject("Slack_Headphones", owner.OriginPoint);
        var m = go.AddComponent<SlackHeadphoneMark>();
        m._owner = owner;
        m._start = Time.time;
        m._end = m._start + Mathf.Max(0.2f, life);
        m._icon = TalkArt.ShowNative(go.transform, TalkArt.Headphones, Vector2.zero, 41, 3f, "Icon").transform;
        m.Place();
        return m;
    }

    void Place()
    {
        Bounds b = TalkKitUtil.OwnerBody(_owner);
        float bob = Mathf.Sin((Time.time - _start) * 8f) * 0.08f;
        transform.position = new Vector3(b.center.x, b.max.y + 0.9f + bob, 0f);
    }

    void Update()
    {
        if (_owner == null || Time.time >= _end) { Destroy(gameObject); return; }
        Place();
        float p = Mathf.Clamp01((Time.time - _start) / 0.12f);
        _icon.localScale = Vector3.one * 3f * Mathf.Lerp(0.3f, 1f, p);
    }
}
