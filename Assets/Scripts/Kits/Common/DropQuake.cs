using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】落下＋左右の波（2026-11-19。docs/app-kits-1-design.md E6 の DropQuake）。担当4（Word）の物。メッセージ（担当1）も使う。
// ・落下物が着地したら、床の上を左右に走る低い波（高さ 0.8・毎秒 9マス）を出す。ジャンプで越える
// ・波は CustomEffect（ボスの形態変化・正常化で消える）。床の端（足場から落ちる所）・壁・画面の端で消える
// ・使い方は3つ
//   1. DropQuake.Attach(落下物, owner, 波の攻撃データ)：今ある FallingObject に「着地したら波」を付ける
//   2. DropQuake.Waves(owner, 波の攻撃データ, 着地点, 半分の幅)：好きな所から、左右に波を出す
//   3. DropQuakeAttack（CustomAttack のアセット）：プレイヤーの位置に N個落とす（波を出さない設定もできる）
// ・波の絵は既定のドット絵（白い山に赤いふち）。Settings.Sprite で差し替えられる（キットの絵）

/// <summary>落下＋左右の波</summary>
public static class DropQuake
{
    /// <summary>波の高さ（マス）</summary>
    public const float DefaultHeight = 0.8f;
    /// <summary>波の速さ（マス/秒）</summary>
    public const float DefaultSpeed = 9f;
    /// <summary>波の幅（マス）</summary>
    public const float DefaultWidth = 0.7f;

    /// <summary>波の設定（どれも省略できる）</summary>
    public struct Settings
    {
        /// <summary>速さ（マス/秒。0 なら 9）</summary>
        public float Speed;
        /// <summary>高さ（マス。0 なら 0.8）</summary>
        public float Height;
        /// <summary>幅（マス。0 なら 0.7）</summary>
        public float Width;
        /// <summary>波の絵（null なら既定の山）</summary>
        public Sprite Sprite;
        /// <summary>左右のどちらに出すか（0＝両方、+1＝右だけ、-1＝左だけ）</summary>
        public int Direction;
        /// <summary>走る距離の上限（マス。0 なら床の端・壁まで）</summary>
        public float MaxDistance;

        public float SpeedOr => Speed > 0f ? Speed : DefaultSpeed;
        public float HeightOr => Height > 0f ? Height : DefaultHeight;
        public float WidthOr => Width > 0f ? Width : DefaultWidth;
    }

    /// <summary>
    /// 着地点から左右に波を出す。groundPoint は床の上の点（y＝床の高さ）。halfGap：落下物の半分の幅（そのふちから出る）。
    /// data は当たったときの数値（ダメージ・ノックバック）。出した波を返す
    /// </summary>
    public static List<DropQuakeWave> Waves(AttackRunner owner, AttackData data, Vector2 groundPoint, float halfGap = 0f, Settings settings = default)
    {
        var list = new List<DropQuakeWave>(2);
        if (owner == null || data == null) return list;
        if (settings.Direction >= 0)
        {
            var w = DropQuakeWave.Spawn(owner, data, new Vector2(groundPoint.x + halfGap, groundPoint.y), 1, settings);
            if (w != null) list.Add(w);
        }
        if (settings.Direction <= 0)
        {
            var w = DropQuakeWave.Spawn(owner, data, new Vector2(groundPoint.x - halfGap, groundPoint.y), -1, settings);
            if (w != null) list.Add(w);
        }
        return list;
    }

    /// <summary>
    /// 落下物に「着地したら波を出す」を付ける。waveData が null なら落下物の攻撃データの数値で当たる。
    /// onLanded：着地したとき（波を出したあと）
    /// </summary>
    public static DropQuakeWatcher Attach(FallingObject falling, AttackRunner owner, AttackData waveData = null, Settings settings = default, Action<FallingObject> onLanded = null)
    {
        if (falling == null || owner == null) return null;
        var w = falling.gameObject.AddComponent<DropQuakeWatcher>();
        w.Setup(falling, owner, waveData != null ? waveData : falling.Data, settings, onLanded);
        return w;
    }

    /// <summary>
    /// 1つ落とす（影の予告つき）。x は落とす位置（部屋の中に収める）。telegraph 秒後に画面の上から落ちる。
    /// waves＝false なら波を出さない（ただの落下物）
    /// </summary>
    public static FallingObject Drop(AttackRunner owner, AttackData fallData, float x, float telegraph, bool waves = true, AttackData waveData = null, Settings settings = default, int index = 0)
    {
        if (owner == null || fallData == null) return null;
        Rect area = WallShot.AreaFor(owner);
        float half = fallData.ObjectSize.x * 0.5f;
        x = Mathf.Clamp(x, area.xMin + half, area.xMax - half);
        float hangY = area.yMax + fallData.ObjectSize.y * 0.5f; // 画面の上端のすぐ外から
        float fallAt = Time.time + Mathf.Max(0f, telegraph);
        FallingObject f = FallingObject.Spawn(owner, fallData, new Vector2(x, hangY), fallAt, fallAt, true, false, index);
        if (f != null && waves) Attach(f, owner, waveData, settings);
        return f;
    }

    // ───────── 既定の波の絵（12×14。白い山に赤いふち） ─────────

    static readonly string[] WaveArt =
    {
        "............",
        ".....RR.....",
        "....RWWR....",
        "....RWWR....",
        "...RWWWWR...",
        "...RWWWWR...",
        "..RWWLLWWR..",
        "..RWWLLWWR..",
        ".RWWLLLLWWR.",
        ".RWWLLLLWWR.",
        "RWWLLLLLLWWR",
        "RWLLLLLLLLWR",
        "RRRRRRRRRRRR",
        "............",
    };

    /// <summary>既定の波の絵</summary>
    public static Sprite DefaultWaveSprite => RuntimeSprites.FromAscii("DropQuake_Wave", WaveArt, new Dictionary<char, Color32>
    {
        { 'R', KitDress.Red },
        { 'W', new Color32(255, 255, 255, 255) },
        { 'L', new Color32(0xD1, 0xD1, 0xD6, 255) },
    });
}

/// <summary>床の上を走る波（DropQuake が出す）。ボスの形態変化・正常化で消える</summary>
public class DropQuakeWave : CustomEffect
{
    AttackRunner _owner;
    Rigidbody2D _rb;
    Hitbox _hitbox;
    SpriteRenderer _visual;
    int _dir;
    float _speed;
    float _groundY;
    float _height;
    float _width;
    float _traveled;
    float _maxDistance;
    Rect _area;
    float _born;
    bool _dead;

    /// <summary>走る向き（+1 右、-1 左）</summary>
    public int Direction => _dir;

    /// <summary>波を1つ出す。from は床の上の点（波の後ろのふち）</summary>
    public static DropQuakeWave Spawn(AttackRunner owner, AttackData data, Vector2 from, int dir, DropQuake.Settings s)
    {
        if (owner == null || data == null) return null;
        float h = s.HeightOr, w = s.WidthOr;
        int d = dir >= 0 ? 1 : -1;
        Vector2 center = new Vector2(from.x + d * w * 0.5f, from.y + h * 0.5f);
        GameObject go = owner.CreateSpawnedObject($"DropQuake_{data.name}", center);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(w, h);
        var wave = go.AddComponent<DropQuakeWave>();
        wave._owner = owner;
        wave._rb = rb;
        wave._dir = d;
        wave._speed = s.SpeedOr;
        wave._groundY = from.y;
        wave._height = h;
        wave._width = w;
        wave._maxDistance = s.MaxDistance > 0f ? s.MaxDistance : 999f;
        wave._area = WallShot.AreaFor(owner);
        wave._born = Time.time;
        Sprite sprite = s.Sprite != null ? s.Sprite : DropQuake.DefaultWaveSprite;
        wave._visual = AttackRunner.AddVisual(go.transform, sprite, Color.white, new Vector2(w, h), 19);
        if (d < 0) wave._visual.flipX = true;
        wave._baseScaleY = wave._visual.transform.localScale.y;
        wave._hitbox = owner.AddHitbox(go, data, col);
        wave._hitbox.Activate();
        return wave;
    }

    void FixedUpdate()
    {
        if (_dead || _rb == null) return;
        float step = _speed * Time.fixedDeltaTime;
        Vector2 pos = _rb.position;
        float nextX = pos.x + _dir * step;
        float front = nextX + _dir * _width * 0.5f;
        // 画面の端・壁・床の端（下の床が下がる所）で消える
        bool end = front < _area.xMin || front > _area.xMax || _traveled >= _maxDistance;
        if (!end && _owner != null)
        {
            if (!_owner.TryFindGround(front, _groundY + 0.3f, out float g) || Mathf.Abs(g - _groundY) > 0.25f) end = true;
            else if (Physics2D.OverlapPoint(new Vector2(front, _groundY + _height * 0.5f), _owner.SolidMask) != null) end = true;
        }
        if (end)
        {
            Die();
            return;
        }
        _traveled += step;
        _rb.MovePosition(new Vector2(nextX, pos.y));
    }

    void Update()
    {
        if (_visual == null) return;
        // 少しはずむ（高さは判定と同じまま、絵だけ上下に伸び縮み）
        float t = (Time.time - _born) * 14f;
        float sy = 1f + 0.08f * Mathf.Sin(t);
        Vector3 s = _visual.transform.localScale;
        _visual.transform.localScale = new Vector3(s.x, _baseScaleY * sy, 1f);
        _visual.transform.localPosition = new Vector3(0f, (sy - 1f) * _height * 0.5f, 0f);
    }

    float _baseScaleY = 1f;

    void Die()
    {
        if (_dead) return;
        _dead = true;
        if (_hitbox != null) _hitbox.Deactivate();
        // 小さいかけらを散らして消える
        if (_visual != null)
        {
            FxAnim.Sprite(FxArt.Square(2), transform.position, 21, 0.3f, KitDress.Red).Velocity(new Vector2(-_dir * 2f, 4f)).Gravity(20f);
            FxAnim.Sprite(FxArt.Square(2), transform.position, 21, 0.3f, Color.white).Velocity(new Vector2(_dir * 1f, 5f)).Gravity(20f);
        }
        Destroy(gameObject);
    }
}

/// <summary>落下物の着地を見張って、波を出す（DropQuake.Attach が付ける）</summary>
public class DropQuakeWatcher : MonoBehaviour
{
    FallingObject _falling;
    AttackRunner _owner;
    AttackData _data;
    DropQuake.Settings _settings;
    Action<FallingObject> _onLanded;
    bool _done;

    internal void Setup(FallingObject f, AttackRunner owner, AttackData data, DropQuake.Settings s, Action<FallingObject> onLanded)
    {
        _falling = f;
        _owner = owner;
        _data = data;
        _settings = s;
        _onLanded = onLanded;
    }

    void FixedUpdate() => Check();
    void Update() => Check();

    void Check()
    {
        if (_done || _falling == null || !_falling.HasLanded) return;
        _done = true;
        if (_owner == null || !_owner.isActiveAndEnabled) return; // 持ち主が止まっていたら（正常化など）出さない
        Vector2 ground = new Vector2(_falling.transform.position.x, _falling.GroundY);
        float half = _falling.Data != null ? _falling.Data.ObjectSize.x * 0.5f : 0f;
        DropQuake.Waves(_owner, _data, ground, half, _settings);
        // 着地の土ぼこり（白い輪）
        FxKit.Ring(ground + Vector2.up * 0.2f, Mathf.Max(1.5f, half * 2f), 0.2f, new Color(1f, 1f, 1f, 0.8f));
        try { _onLanded?.Invoke(_falling); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
