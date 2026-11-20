using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// チャットのメッセージをためて、一斉に飛ばす（2026-11-19。Slack・Discord・LINE の能力。数値は攻撃データ、見た目は【仮】）。
/// AttackRunner が AttackData（種類 Chat）から作る。1回の「ため」につき1つ。
/// ・止まっている間は、能力のボタンを押すたびにメッセージが1つ増える（最大数まで。クールタイムなし）。
///   いちばん新しいメッセージがいちばん下で、古いものは上へ押し上げられる
/// ・2つ目以降は「続けて送ったメッセージ」の絵。すき間がマイナスなら重ねて、1つにつながって見える（Slack・Discord）
/// ・最後に足してから決まった秒数（1秒）で、全部が一斉に、向いている方へ飛んでいく（足すたびに数え直す）。
///   狙い方が「相手」で狙う相手がいるとき（チャットのボス）は、相手の方へ飛ぶ
/// ・動く（持ち主が少しでも動く）か、最大数まで出したら、もう足せない（このとき AttackRunner の攻撃が終わり、能力のクールタイムが始まる）
/// ・ためている間、メッセージは持ち主に付いてくる
/// </summary>
public class ChatVolley : MonoBehaviour
{
    /// <summary>動いたとみなす距離（ためを始めた位置から、マス）</summary>
    const float MoveTolerance = 0.08f;
    /// <summary>メッセージがすべり上がる速さ（大きいほど速い）</summary>
    const float SlideSharpness = 22f;

    static readonly List<ChatVolley> Active = new List<ChatVolley>();

    AttackRunner _owner;
    AttackData _data;
    readonly List<ChatMessage> _messages = new List<ChatMessage>();
    Vector2 _startPos;
    float _fireAt;
    bool _closed;
    bool _fired;
    int _sent; // このためで出した数（当たって消えても減らない。最大数まで出したら、もう出せない）

    /// <summary>まだメッセージを足せるか（止まっていて、最大数に届いていない）</summary>
    public bool IsOpen => !_closed;
    public int Count => _messages.Count;
    /// <summary>このためで出した数（当たって消えた分も数える）</summary>
    public int Sent => _sent;
    public AttackData Data => _data;

    /// <summary>ためを始めて、最初のメッセージを出す</summary>
    public static ChatVolley Start(AttackRunner owner, AttackData data)
    {
        GameObject go = owner.CreateSpawnedObject($"Chat_{data.name}", owner.transform.position);
        var v = go.AddComponent<ChatVolley>();
        v._owner = owner;
        v._data = data;
        v._startPos = owner.transform.position;
        Active.Add(v);
        v.AddMessage();
        return v;
    }

    /// <summary>この持ち主の、この攻撃のためが、まだメッセージを足せるか（能力の枠を「使える」表示のままにする）</summary>
    public static bool IsOpenFor(AttackRunner owner, AttackData data)
    {
        foreach (var v in Active)
        {
            if (v != null && v._owner == owner && v._data == data && v.IsOpen) return true;
        }
        return false;
    }

    /// <summary>この持ち主の、この攻撃のためがまだ足せるなら、メッセージを1つ足す（足せたら true）</summary>
    public static bool TryAppend(AttackRunner owner, AttackData data)
    {
        foreach (var v in Active)
        {
            if (v != null && v._owner == owner && v._data == data && v.IsOpen)
            {
                v.AddMessage();
                return true;
            }
        }
        return false;
    }

    void AddMessage()
    {
        bool first = _sent == 0;
        Sprite sprite = first ? _data.Sprite : _data.ChatBodySprite;
        Vector2 at = AnchorPoint();
        var msg = ChatMessage.Create(_owner, _data, sprite, _sent, at);
        _sent++;
        if (_owner != null) _owner.NotifyReleased(_data); // 1つ足すたびに「出た瞬間」の音（技ごとの割り当て。2026-11-20）
        _messages.Add(msg);
        _fireAt = Time.time + _data.ChatFireDelay; // 足すたびに数え直す
        Layout(true);
        if (_sent >= _data.ChatMaxMessages) Close(); // 消えた分は数え直さない（ボス戦で連打できないように）
    }

    void Close()
    {
        _closed = true;
        Active.Remove(this);
    }

    void OnDestroy() => Active.Remove(this);

    int Facing => _owner != null ? _owner.Facing : 1;

    Vector2 AnchorPoint()
    {
        Vector2 a = _data.ChatAnchor;
        return (Vector2)_owner.transform.position + new Vector2(Facing * a.x, a.y);
    }

    void Update()
    {
        _messages.RemoveAll(m => m == null);
        if (_owner == null)
        {
            Fire();
            return;
        }
        // 動いたら、もう足せない（クールタイムが始まる）。メッセージは決まった時間に飛んでいく
        if (!_closed && ((Vector2)_owner.transform.position - _startPos).sqrMagnitude > MoveTolerance * MoveTolerance) Close();

        Layout(false);
        if (Time.time >= _fireAt) Fire();
    }

    /// <summary>
    /// メッセージを並べる：いちばん新しいものが一番下（持ち主の前）、古いものほど上。
    /// snapNewest：足したばかりのメッセージを、その場に置く（ほかはすべり上がる）
    /// </summary>
    void Layout(bool snapNewest)
    {
        if (_messages.Count == 0) return;
        Vector2 anchor = AnchorPoint();
        int facing = Facing;
        float k = 1f - Mathf.Exp(-SlideSharpness * Time.deltaTime);
        float y = anchor.y;
        for (int i = _messages.Count - 1; i >= 0; i--)
        {
            ChatMessage m = _messages[i];
            float h = m.Height;
            if (i < _messages.Count - 1) y += h * 0.5f; // 1つ下のメッセージの上の端から、自分の半分だけ上
            var target = new Vector2(anchor.x, y);
            bool snap = snapNewest && i == _messages.Count - 1;
            m.Follow(target, facing, snap ? 1f : k);
            y += h * 0.5f + _data.ChatGap;
        }
    }

    /// <summary>全部のメッセージを、向いている方へ一斉に飛ばす</summary>
    void Fire()
    {
        if (_fired) return;
        _fired = true;
        GameAudio.Event(_owner != null && _owner.Team == Team.Player ? "gm_chat_send" : "gm_chat_send_boss");
        if (!_closed) Close();
        // 向いている方へ。狙い方が「相手」で、狙う相手がいれば（チャットのボス）、まとまったまま相手の方へ飛ぶ
        var dir = new Vector2(Facing, 0f);
        if (_data.Aim == AimMode.Target && _owner != null && _owner.TargetPoint is Vector2 target)
        {
            Vector2 d = target - AnchorPoint();
            if (d.sqrMagnitude > 0.0001f) dir = d.normalized;
        }
        foreach (var m in _messages)
        {
            if (m != null) m.Launch(dir);
        }
        _messages.Clear();
        Destroy(gameObject);
    }
}
