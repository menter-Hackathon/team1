using UnityEngine;

/// <summary>
/// 【仮】ブレイクアウトルームの壁（2026-11-19。Teams のギミック）。床から上がる固い板（レイヤー Ground）。
/// 最初は赤い点線の予告だけ（固くない）。高さ 0 の間は判定なし。出来事が Track して、終わりで消す
/// </summary>
public class TeamsWall : MonoBehaviour
{
    const int Order = 8;

    BoxCollider2D _col;
    SpriteRenderer _body, _mark;
    Vector2 _foot;
    Vector2 _size;
    float _height;
    bool _blink;
    Vector3 _bodyScale;

    /// <summary>今の高さ（マス）</summary>
    public float Height => _height;

    public static TeamsWall Create(Vector2 foot, Vector2 size)
    {
        var go = new GameObject("Teams_BreakoutWall");
        int ground = LayerMask.NameToLayer("Ground");
        if (ground >= 0) go.layer = ground;
        go.transform.position = foot;
        var w = go.AddComponent<TeamsWall>();
        w._foot = foot;
        w._size = size;
        w._col = go.AddComponent<BoxCollider2D>();
        w._col.enabled = false;
        w._body = TalkArt.Show(go.transform, TeamsArt.Wall, new Vector2(0f, size.y * 0.5f), size, Color.white, Order, "Body");
        w._body.enabled = false;
        w._bodyScale = w._body.transform.localScale;
        int wp = Mathf.Max(6, Mathf.RoundToInt(size.x * 18f)), hp = Mathf.RoundToInt(size.y * 18f);
        w._mark = TalkArt.ShowNative(go.transform, TalkArt.Frame(wp, hp, 2, true), new Vector2(0f, size.y * 0.5f), Order + 1, 1f, "Mark");
        w._mark.color = TalkArt.Red;
        return w;
    }

    /// <summary>予告の赤い点線を出す・消す</summary>
    public void ShowMark(bool on)
    {
        if (_mark != null) _mark.enabled = on;
    }

    /// <summary>高さを変える（下の端は床のまま）</summary>
    public void SetHeight(float h)
    {
        _height = Mathf.Clamp(h, 0f, _size.y);
        bool on = _height > 0.05f;
        _col.enabled = on;
        _body.enabled = on;
        if (!on) return;
        _col.size = new Vector2(_size.x, _height);
        _col.offset = new Vector2(0f, _height * 0.5f);
        // 絵は床から伸びる（床の下にははみ出さない）
        float k = _height / _size.y;
        _body.transform.localScale = new Vector3(_bodyScale.x, _bodyScale.y * k, 1f);
        _body.transform.localPosition = new Vector3(0f, _height * 0.5f, 0f);
    }

    /// <summary>点滅（閉じる前）</summary>
    public void Blink(bool on) => _blink = on;

    void Update()
    {
        if (_mark != null && _mark.enabled) _mark.color = TalkArt.Fade(TalkArt.Red, Mathf.FloorToInt(Time.time / 0.12f) % 2 == 0 ? 1f : 0.4f);
        if (_body != null) _body.color = _blink && Mathf.FloorToInt(Time.time / 0.12f) % 2 == 0 ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
    }
}
