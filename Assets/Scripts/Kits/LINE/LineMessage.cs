using System;
using UnityEngine;

/// <summary>
/// 【仮】LINE の叩ける吹き出し（メッセージ。2026-11-19。docs/app-kits-1-design.md 4章）。ギミック「既読スルー」とボスの技「怒りのメッセージ」で使う。
/// 流れ：吹き出しが「ポン」と出る（緑）→ readDelay 秒たつと左下に「既読」→ 赤くなって blink 秒点滅（爆発の範囲に赤い輪）→ 直径 4 の爆発。
/// ・近接か能力で叩く＝返信：✓ が出て消える（爆発しない）。onDone(返信したか)
/// ・readDelay が 0 なら、最初から赤い（怒りのメッセージ）
/// ・攻撃する人（ボスか、ギミックの危ない物の係）の出した物。CustomEffect なので、形態変化・正常化で消える
/// </summary>
public class LineMessage : CustomEffect
{
    public static readonly Vector2 Size = new Vector2(2.2f, 1.5f);
    const int Order = 16;

    AttackRunner _owner;
    AttackData _data;
    float _diameter;
    float _born, _readAt, _blinkAt, _boomAt;
    KitProp _prop;
    SpriteRenderer _flash;
    TextMesh _read;
    TalkRing _ring;
    Action<LineMessage, bool> _onDone;
    bool _angry, _done;

    /// <summary>返信されたか、爆発したか</summary>
    public bool IsDone => _done;

    /// <summary>
    /// 吹き出しを1つ出す。ground：置く床の点（y＝床の高さ）。readDelay：「既読」になるまで（0 で最初から赤い）、blink：赤く点滅する秒数（予告）。
    /// data：爆発の攻撃データ（ダメージ・ノックバック）
    /// </summary>
    public static LineMessage Spawn(AttackRunner owner, AttackData data, Vector2 ground, float readDelay, float blink, float diameter, Action<LineMessage, bool> onDone = null)
    {
        if (owner == null || data == null) return null;
        Vector2 center = ground + new Vector2(0f, Size.y * 0.5f + 0.05f);
        GameObject go = owner.CreateSpawnedObject("Line_Message", center);
        var m = go.AddComponent<LineMessage>();
        m._owner = owner;
        m._data = data;
        m._diameter = diameter;
        m._onDone = onDone;
        m._born = Time.time;
        m._readAt = m._born + Mathf.Max(0f, readDelay);
        m._blinkAt = m._readAt + (readDelay > 0f ? 0.4f : 0f);
        m._boomAt = m._blinkAt + Mathf.Max(0.8f, blink);
        m._angry = readDelay <= 0f;
        m.Build(center);
        return m;
    }

    void Build(Vector2 center)
    {
        _prop = KitProp.Spawn(_owner, center, LineArt.Bubble(_angry), Size, OnReplied);
        _prop.transform.SetParent(transform, true);
        _prop.DestroyOnBreak = true;
        _prop.Visual.sortingOrder = Order;
        // 赤い点滅（KitProp は本体の色を毎フレーム決めるので、上に重ねた白い絵を点ける・消す）
        _flash = TalkArt.Show(transform, LineArt.BubbleMask, Vector2.zero, Size, TalkArt.Fade(TalkArt.Red, 0.6f), Order + 1, "Flash");
        _flash.enabled = false;
        _read = TalkArt.Text(transform, "既読", new Vector2(-Size.x * 0.5f - 0.05f, -Size.y * 0.5f + 0.3f), 0.67f, new Color(0.25f, 0.25f, 0.28f, 1f), Order + 2, false, TextAnchor.MiddleRight);
        _read.gameObject.SetActive(false);
        transform.localScale = Vector3.one * 0.2f;
    }

    void Update()
    {
        if (_done) return;
        float now = Time.time;
        // 「ポン」と出る
        float pop = (now - _born) / 0.12f;
        float s = pop < 0.7f ? Mathf.Lerp(0.2f, 1.15f, pop / 0.7f) : pop < 1f ? Mathf.Lerp(1.15f, 1f, (pop - 0.7f) / 0.3f) : 1f;
        transform.localScale = new Vector3(s, s, 1f);

        if (now >= _readAt && !_read.gameObject.activeSelf && !_angry)
        {
            _read.gameObject.SetActive(true);
            GameAudio.Event("gm_line_read");
        }
        if (now >= _blinkAt && _ring == null)
        {
            // 赤くなる：爆発の範囲に赤い輪（予告）→ 爆発（TalkRing が判定を持つ）
            if (!_angry && _prop != null) _prop.SetSprite(LineArt.Bubble(true), Color.white);
            _angry = true;
            _ring = TalkRing.Spawn(_owner, _data, transform.position, _diameter, 0f, _boomAt - now, 0.25f, TalkBurst.Explosion, (Color)LineArt.AngryFill);
        }
        if (now >= _blinkAt && _flash != null) _flash.enabled = Mathf.FloorToInt(now / 0.1f) % 2 == 0;
        if (now >= _boomAt)
        {
            // 爆発した（判定は TalkRing）。吹き出しは消える
            Finish(false);
        }
    }

    void OnReplied(KitProp prop)
    {
        if (_done) return;
        // 返信：✓ が出て消える。爆発の予告も消す
        if (_ring != null) Destroy(_ring.gameObject);
        FxAnim a = FxAnim.Sprite(LineArt.Replied, (Vector2)transform.position + Vector2.up * 0.9f, 47, 0.6f, null, 2f, "Line_Replied");
        a.Rise(0.5f).Pop().FadeFrom(0.6f);
        FxAnim.Sprite(LineArt.Bubble(false), transform.position, Order, 0.25f, new Color(1f, 1f, 1f, 0.8f), Size.x / (40f / 18f), "Line_Pop").Grow(1.3f).FadeFrom(0f);
        Finish(true);
    }

    void Finish(bool replied)
    {
        if (_done) return;
        _done = true;
        try { _onDone?.Invoke(this, replied); }
        catch (Exception e) { Debug.LogException(e); }
        Destroy(gameObject);
    }

    protected override void Restore()
    {
        // 外から消された（形態変化・正常化・ボスの登場）：爆発の予告も消す
        if (!_done && _ring != null) Destroy(_ring.gameObject);
    }
}
