using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Steam の「実績解除」の通知（2026-11-19。docs/app-kits-2-design.md 2-3・1-4）。
/// ・プレイヤーの実績は右下（金のトロフィー）、ボスの実績は左下（紫）。本物の Steam と同じく下からすべり出る
/// ・同時に1つ。続いたら順番に出す（出た瞬間に onShown を呼ぶ＝効果はそこで入る）
/// ・カメラの子（ゲームの世界の絵）。プレイヤー・ボスが後ろに重なっている間は 4割まですかす
/// ・能力（トロフィー）からも呼ぶので、ステージの決まりに頼らずに動く。シーンが変わると消える
/// </summary>
public class SteamToast : MonoBehaviour
{
    const float InTime = 0.2f, StayTime = 2.4f, OutTime = 0.25f;
    const float Margin = 0.3f;

    struct Item
    {
        public string Name;
        public bool Boss;
        public (Sprite icon, string text)[] Badges;
        public Action OnShown;
    }

    static SteamToast _host;
    readonly Queue<Item> _queue = new Queue<Item>();
    Transform _card;
    SpriteRenderer _sr;
    Vector2 _size;
    bool _boss;
    float _age = -1f;
    string _showing;

    /// <summary>通知を出す（出ていれば順番を待つ）。同じ名前がもう待っていれば足さない</summary>
    public static void Show(string name, bool boss, (Sprite icon, string text)[] badges, Action onShown = null)
    {
        if (string.IsNullOrEmpty(name)) return;
        SteamToast h = Host();
        if (h == null)
        {
            onShown?.Invoke();
            return;
        }
        if (h._showing == name && h._age >= 0f && h._age < InTime + StayTime * 0.5f)
        {
            onShown?.Invoke();
            return;
        }
        foreach (var q in h._queue)
        {
            if (q.Name == name) return;
        }
        h._queue.Enqueue(new Item { Name = name, Boss = boss, Badges = badges, OnShown = onShown });
    }

    /// <summary>待っている通知と、出ている通知を全部消す（100％・ステージの終わり）</summary>
    public static void ClearAll()
    {
        if (_host != null) Destroy(_host.gameObject);
        _host = null;
    }

    /// <summary>出ているか・待っているか</summary>
    public static bool Busy => _host != null && (_host._age >= 0f || _host._queue.Count > 0);

    static SteamToast Host()
    {
        if (_host != null) return _host;
        Transform root = FxKit.CameraChild("SteamToast");
        if (root == null) return null;
        _host = root.gameObject.AddComponent<SteamToast>();
        return _host;
    }

    void OnDestroy()
    {
        if (_host == this) _host = null;
    }

    void Update()
    {
        float dt = Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f; // ゲームが止まっている間は進めない
        if (_age < 0f)
        {
            if (_queue.Count == 0) return;
            Begin(_queue.Dequeue());
        }
        _age += dt;
        if (_age >= InTime + StayTime + OutTime)
        {
            if (_card != null) Destroy(_card.gameObject);
            _card = null;
            _age = -1f;
            _showing = null;
            return;
        }
        Place();
    }

    void Begin(Item item)
    {
        if (_card != null) Destroy(_card.gameObject);
        _boss = item.Boss;
        _showing = item.Name;
        GameAudio.Event("toast");
        Sprite s = SteamArt.Toast(item.Name, item.Boss, item.Badges);
        var go = new GameObject("Card");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        _card = go.transform;
        _sr = go.AddComponent<SpriteRenderer>();
        _sr.sprite = s;
        _sr.sortingOrder = FxKit.OrderToast;
        _size = s.bounds.size;
        _age = 0f;
        try { item.OnShown?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
        Place();
    }

    void Place()
    {
        if (_card == null) return;
        Vector2 view = FxKit.ViewSize();
        float k;
        if (_age < InTime) k = _age / InTime;
        else if (_age > InTime + StayTime) k = 1f - (_age - InTime - StayTime) / OutTime;
        else k = 1f;
        k = Mathf.Clamp01(k);
        float e = 1f - (1f - k) * (1f - k);
        float x = _boss ? -view.x * 0.5f + Margin + _size.x * 0.5f : view.x * 0.5f - Margin - _size.x * 0.5f;
        float yShown = -view.y * 0.5f + Margin + _size.y * 0.5f;
        float y = Mathf.Lerp(yShown - _size.y - Margin, yShown, e);
        x = Mathf.Round(x * 18f) / 18f;
        y = Mathf.Round(y * 18f) / 18f;
        _card.localPosition = new Vector3(x, y, 0f);

        // 後ろにプレイヤー・ボスがいれば、すかす
        float alpha = Covered(_card.position, _size) ? 0.4f : 1f;
        if (_sr != null) _sr.color = new Color(1f, 1f, 1f, alpha);
    }

    Transform _player;

    bool Covered(Vector2 center, Vector2 size)
    {
        var r = new Rect(center - size * 0.5f, size);
        if (_player == null && Time.frameCount % 30 == 0) _player = KitPlayer.Transform;
        Transform p = _player;
        if (p != null && r.Contains((Vector2)p.position + Vector2.up * 0.5f)) return true;
        BossController b = HitFx.StageBoss();
        if (b != null && b.isActiveAndEnabled)
        {
            Bounds bb = b.BodyBounds;
            if (r.Overlaps(new Rect(bb.min, bb.size))) return true;
        }
        return false;
    }
}
