using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】メッセージのギミックの風船の組（2026-11-19）。風船（KitProp。痛くない）を N個、床からゆっくり上げる。
/// 近接か能力で割ると onPop。全部割ると onAll。画面の上へ出た風船は消える。全部無くなったら自分も消える。
/// 置いた物の数の上限（4）にかからないように、風船は全部この組の子にして、組を1つだけ覚えさせる
/// </summary>
public class MessagesBalloonGroup : MonoBehaviour
{
    static readonly Vector2 BalloonSize = new Vector2(1f, 1.65f);
    const float HitHeight = 1.1f;

    readonly List<KitProp> _balloons = new List<KitProp>();
    readonly List<Rigidbody2D> _bodies = new List<Rigidbody2D>();
    readonly List<float> _startAt = new List<float>();
    readonly List<Color> _colors = new List<Color>();
    float _speed;
    float _topY;
    int _total, _popped;
    Action _onPop, _onAll;

    public int Popped => _popped;
    public int Total => _total;

    public static MessagesBalloonGroup Spawn(Rect area, int count, float speed, Action onPop, Action onAll)
    {
        var go = new GameObject("Messages_Balloons");
        var g = go.AddComponent<MessagesBalloonGroup>();
        g._speed = speed;
        g._topY = area.yMax + 1f;
        g._onPop = onPop;
        g._onAll = onAll;
        g._total = count;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float x = Mathf.Lerp(area.xMin + 1.5f, area.xMax - 1.5f, t) + UnityEngine.Random.Range(-0.6f, 0.6f);
            float floor = TalkKitUtil.FloorY(x, area.yMin + 2.4f);
            // 床の上に出して、少しずつ遅らせて上げる（同じ高さに並ばないように）
            Vector2 pos = new Vector2(x, floor + 0.9f);
            KitProp prop = KitProp.Spawn(pos, MessagesArt.Balloon(i), new Vector2(BalloonSize.x, HitHeight), g.OnBroken);
            prop.SetSprite(MessagesArt.Balloon(i));
            // 絵はひもまで（判定は丸の所だけ）
            prop.Visual.transform.localScale = new Vector3(BalloonSize.x / (18f / 18f), BalloonSize.y / (30f / 18f), 1f);
            prop.Visual.transform.localPosition = new Vector3(0f, -(BalloonSize.y - HitHeight) * 0.5f, 0f);
            prop.transform.SetParent(go.transform, true);
            g._balloons.Add(prop);
            g._bodies.Add(prop.GetComponent<Rigidbody2D>());
            g._startAt.Add(Time.time + (i % 2) * 0.6f + i * 0.15f);
            g._colors.Add((Color)(i % 3 == 0 ? MessagesArt.BalloonRed : i % 3 == 1 ? MessagesArt.BalloonYellow : MessagesArt.BalloonPurple));
        }
        return g;
    }

    void OnBroken(KitProp prop)
    {
        _popped++;
        Vector2 at = prop.transform.position;
        int idx = _balloons.IndexOf(prop);
        Color col = idx >= 0 ? _colors[idx] : Color.white;
        // 割れた：色の点が散る
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            FxAnim.Sprite(TalkArt.Disc(3), at, 30, 0.35f, col, 1f, "Pop")
                .Velocity(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4f).FadeFrom(0.3f);
        }
        FxAnim.Sprite(TalkArt.Ring(24, 2), at, 30, 0.2f, Color.white, 1f, "PopRing").Grow(1.8f).FadeFrom(0f);
        try { _onPop?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
        if (_popped >= _total)
        {
            try { _onAll?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    void FixedUpdate()
    {
        bool any = false;
        for (int i = 0; i < _balloons.Count; i++)
        {
            KitProp p = _balloons[i];
            if (p == null || p.IsBroken) continue;
            any = true;
            if (Time.time < _startAt[i]) continue;
            Rigidbody2D rb = _bodies[i];
            Vector2 pos = rb != null ? rb.position : (Vector2)p.transform.position;
            float sway = Mathf.Sin(Time.time * 2f + i) * 0.4f * Time.fixedDeltaTime;
            Vector2 next = pos + new Vector2(sway, _speed * Time.fixedDeltaTime);
            if (rb != null) rb.MovePosition(next);
            else p.transform.position = next;
            if (next.y > _topY) Destroy(p.gameObject);
        }
        if (!any) Destroy(gameObject);
    }
}
