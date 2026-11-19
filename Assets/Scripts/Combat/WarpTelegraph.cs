using UnityEngine;

/// <summary>
/// 瞬間移動で現れる場所の予告（点滅するウィンドウの点線の枠と、まん中の文字カーソル）。AttackRunner が作る。
/// startDelay 秒たってから表示し、endDelay 秒たったら消える（どちらも作った時刻から）。
/// 2026-11-18（スキルの見た目 3-5）：スキンに場面を知らせる（Warp 枠）。Target＝この物（現れる所）、Direction＝移動量。
/// ・予告＝作った瞬間（ボスはまだ元の所）、本体＝現れ始める瞬間（ボスはもう移った所）、消える＝予告が終わる瞬間
/// ・スキンが絵を付けたら、文字カーソルはスキンの物に替わる（呼んだ側がかくす）。点線の枠は今のまま出す（0-3：予告は同じ場所・大きさ・秒数）
/// ・どのスキンも付けなければ、今までの見た目のまま
/// </summary>
public class WarpTelegraph : MonoBehaviour
{
    static readonly Color FrameColor = new Color(1f, 1f, 1f, 0.8f);

    SpriteRenderer _frame;
    SpriteRenderer _cursor;
    float _showAt;
    float _hideAt;
    // スキン（2026-11-18）
    AttackRunner _owner;
    AttackData _data;
    Vector2 _bodySize;
    Vector2 _offset;
    float _appearAt;
    float _appearTime;
    bool _dressed;
    bool _toldActive;
    bool _toldVanish;

    public bool IsShowing => _frame != null && Time.time >= _showAt && Time.time < _hideAt;
    public float ShowTime => _showAt;

    /// <summary>body は今の体の範囲、offset は移動量（予告は移動先に出す）</summary>
    public static WarpTelegraph Spawn(AttackRunner owner, Bounds body, Vector2 offset, float startDelay, float endDelay)
    {
        body = UsableBody(owner, body);
        GameObject go = owner.CreateSpawnedObject("WarpTelegraph", (Vector2)body.center + offset);
        var t = go.AddComponent<WarpTelegraph>();
        GameArt art = GameArt.Instance;
        Sprite frame = art != null && art.TelegraphFrame != null ? art.TelegraphFrame : PlaceholderSprites.Frame;
        t._frame = AttackRunner.AddVisual(go.transform, frame, FrameColor, body.size, 25, "Frame");
        t._frame.enabled = false;
        if (art != null && art.WarpCursor != null)
        {
            // エディタの文字カーソル（縦棒）。体の高さの6割
            t._cursor = AttackRunner.AddVisual(go.transform, art.WarpCursor, Color.white, new Vector2(art.WarpCursor.bounds.size.x, body.size.y * 0.6f), 26, "Cursor");
            t._cursor.enabled = false;
        }
        t._showAt = Time.time + startDelay;
        t._hideAt = Time.time + endDelay;
        t.TellSkinTelegraph(owner, body.size, offset, endDelay);
        return t;
    }

    /// <summary>
    /// 【仮】体の範囲が空のとき（呼ぶ直前に被弾範囲の当たり判定を切ると、範囲が空になることがある）、
    /// ボスのウィンドウの絵 → 移動用の当たり判定 → 持ち主の位置の 1×1 の順に代わりを使う（2026-11-18）
    /// </summary>
    static Bounds UsableBody(AttackRunner owner, Bounds body)
    {
        if (body.size.x > 0.05f && body.size.y > 0.05f) return body;
        if (owner == null) return body;
        BossArmorVisual armor = owner.GetComponentInChildren<BossArmorVisual>();
        if (armor != null && armor.Window != null && armor.Window.bounds.size.x > 0.05f) return armor.Window.bounds;
        foreach (var col in owner.GetComponents<Collider2D>())
        {
            if (col.enabled && !col.isTrigger && col.bounds.size.x > 0.05f) return col.bounds;
        }
        return new Bounds(owner.transform.position, Vector3.one);
    }

    /// <summary>
    /// スキンに予告を知らせる。今の攻撃データ（瞬間移動）から、現れ始める時刻を求める。
    /// 攻撃データが瞬間移動でない（専用の技の中で動いた）ときは、知らせない
    /// </summary>
    void TellSkinTelegraph(AttackRunner owner, Vector2 bodySize, Vector2 offset, float endDelay)
    {
        _owner = owner;
        _data = owner != null ? owner.Current : null;
        _bodySize = bodySize;
        _offset = offset;
        if (_data == null || _data.Kind != AttackKind.Warp) return;
        _appearTime = _data.AppearTime;
        float appearDelay = Mathf.Clamp(_data.VanishTime + _data.AppearDelay, 0f, endDelay);
        _appearAt = Time.time + appearDelay;
        _dressed = SkinDresser.Dress(owner, _data, gameObject, DressPhase.Telegraph, _cursor, bodySize, 0, offset, 1, appearDelay);
    }

    void TellSkin(DressPhase phase, float duration)
    {
        if (!_dressed) return;
        SkinDresser.Dress(_owner, _data, gameObject, phase, null, _bodySize, 0, _offset, 1, duration);
    }

    void Update()
    {
        float now = Time.time;
        if (_dressed && !_toldActive && now >= _appearAt)
        {
            _toldActive = true;
            TellSkin(DressPhase.Active, _appearTime);
        }
        if (now >= _hideAt)
        {
            if (!_toldVanish)
            {
                _toldVanish = true;
                TellSkin(DressPhase.Vanish, 0f);
            }
            Destroy(gameObject);
            return;
        }
        bool on = now >= _showAt && Mathf.FloorToInt(now / 0.1f) % 2 == 0;
        _frame.enabled = on;
        if (_cursor != null) _cursor.enabled = now >= _showAt && Mathf.FloorToInt(now / 0.2f) % 2 == 0;
    }
}
