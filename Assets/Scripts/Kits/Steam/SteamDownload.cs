using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Steam のボスの技2：ダウンロード（2026-11-19。docs/app-kits-2-design.md 2-3）。
/// 床の上に部屋の幅いっぱいの進みのバー（高さ 0.8マス）が左から右へのびる（1.6秒）。50％から赤く点滅 → 100％の瞬間、バーの上にいると当たる（0.15秒）。
/// ・避け方：100％に合わせてジャンプ。足場の上は安全
/// ・第2形態：2回続ける。2回目は 70％で 0.6秒止まる（Paused）→ 急に進む。赤の時間はどちらも 0.8秒以上
/// ・場に出る物は SteamDownloadEffect（自分で進む。形態変化・正常化で消える）
/// </summary>
[CreateAssetMenu(fileName = "Steam_Download", menuName = "Team1/Kits/Steam Download")]
public class SteamDownload : CustomAttack
{
    [Tooltip("回数（第2形態は 2）")]
    [SerializeField, Min(1)] int _runs = 1;
    [Tooltip("0 → 100％ の秒数")]
    [SerializeField, Min(1.6f)] float _fillTime = 1.6f;
    [Tooltip("2回目に止まる所（0〜1。0 なら止まらない）")]
    [SerializeField, Range(0f, 0.95f)] float _pauseAt = 0.7f;
    [Tooltip("止まる秒数")]
    [SerializeField, Min(0f)] float _pauseTime = 0.6f;
    [Tooltip("止まったあと 100％までの秒数（急に進む）")]
    [SerializeField, Min(0.1f)] float _rushTime = 0.25f;
    [Tooltip("当たる時間")]
    [SerializeField, Min(0.05f)] float _hitWindow = 0.15f;
    [Tooltip("バーの高さ（マス）")]
    [SerializeField, Min(0.3f)] float _barHeight = 0.8f;
    [Tooltip("1回目と2回目の間（秒）")]
    [SerializeField, Min(0f)] float _gap = 0.5f;

    public float FillTime => _fillTime;
    public float PauseTime => _pauseTime;
    public float RushTime => _rushTime;
    public float HitWindow => _hitWindow;
    public float BarHeight => _barHeight;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        for (int i = 0; i < _runs; i++)
        {
            bool pause = i >= 1 && _pauseAt > 0f;
            SteamDownloadEffect e = SteamDownloadEffect.Spawn(owner, data, this, pause ? _pauseAt : -1f);
            if (e == null) yield break;
            owner.NotifyReleased(data);
            float timeout = Time.time + _fillTime + _pauseTime + 2f;
            while (e != null && Time.time < timeout) yield return null;
            if (i < _runs - 1) yield return new WaitForSeconds(_gap);
        }
    }
}

/// <summary>ダウンロードのバー（SteamDownload が出す）。自分で進む</summary>
public class SteamDownloadEffect : CustomEffect
{
    const int TrackOrder = 16, FillOrder = 17, TextOrder = 18;
    const float RedFrom = 0.5f;

    AttackRunner _owner;
    AttackData _data;
    SteamDownload _a;
    float _pauseAt;
    Rect _bar;
    SpriteRenderer _track, _fill, _edgeTop, _flash;
    TextMesh _pct;
    TextMesh _paused;
    Transform _fillT;

    public static SteamDownloadEffect Spawn(AttackRunner owner, AttackData data, SteamDownload attack, float pauseAt)
    {
        if (owner == null || attack == null) return null;
        Rect arena = BattleArena.GetBounds();
        GameObject go = owner.CreateSpawnedObject("Steam_Download", arena.center);
        var e = go.AddComponent<SteamDownloadEffect>();
        e._owner = owner;
        e._data = data;
        e._a = attack;
        e._pauseAt = pauseAt;
        e.Build(arena);
        e.StartCoroutine(e.Run());
        return e;
    }

    void Build(Rect arena)
    {
        float left = arena.xMin + 0.2f, right = arena.xMax - 0.2f;
        _bar = new Rect(left, arena.yMin, right - left, _a.BarHeight);
        transform.position = _bar.center;
        _track = FxKit.Box(transform, Vector2.zero, _bar.size, new Color32(0x17, 0x1A, 0x21, 0xD8), TrackOrder, "Track");
        _edgeTop = FxKit.Box(transform, new Vector2(0f, _bar.height * 0.5f), new Vector2(_bar.width, 2f * KitDress.Px), (Color)SteamArt.Edge, TrackOrder, "Edge");
        var fillRoot = new GameObject("FillRoot");
        fillRoot.layer = gameObject.layer;
        fillRoot.transform.SetParent(transform, false);
        fillRoot.transform.localPosition = new Vector3(-_bar.width * 0.5f, 0f, 0f);
        _fillT = fillRoot.transform;
        _fill = FxKit.Box(_fillT, new Vector2(0.5f, 0f), new Vector2(1f, _bar.height - 4f * KitDress.Px), (Color)SteamArt.Blue, FillOrder, "Fill");
        _fillT.localScale = new Vector3(0.001f, 1f, 1f);
        // 左の下向き矢印と、右の数字
        RuntimeSprites.Show(transform, SteamArt.DownArrow, new Vector2(-_bar.width * 0.5f + 0.45f, 0f), TextOrder, 1f, "Arrow");
        _pct = WorldText.Create(transform, "0%", 0.45f, Color.white, TextOrder, true, TextAnchor.MiddleRight, "Pct");
        _pct.transform.localPosition = new Vector3(_bar.width * 0.5f - 0.15f, 0.02f, 0f);
        _flash = FxKit.Box(transform, Vector2.zero, _bar.size + new Vector2(0f, 0.2f), new Color(1f, 1f, 1f, 0f), TextOrder + 1, "Flash");
    }

    IEnumerator Run()
    {
        float t = 0f; // 0〜1
        float fill = _a.FillTime;
        bool paused = false;
        while (t < 1f)
        {
            if (Stopped()) { Destroy(gameObject); yield break; }
            if (!paused && _pauseAt > 0f && t >= _pauseAt)
            {
                paused = true;
                t = _pauseAt;
                Show(t, true);
                _paused = WorldText.Create(transform, "PAUSED", 0.45f, (Color)SteamArt.Blue, TextOrder, true, TextAnchor.MiddleCenter, "Paused");
                _paused.transform.localPosition = new Vector3(0f, _bar.height * 0.5f + 0.45f, 0f);
                float until = Time.time + _a.PauseTime;
                while (Time.time < until)
                {
                    if (Stopped()) { Destroy(gameObject); yield break; }
                    Show(t, true);
                    yield return null;
                }
                if (_paused != null) Destroy(_paused.gameObject);
                // 急に進む
                float rest = 1f - t;
                float speed = rest / Mathf.Max(0.05f, _a.RushTime);
                while (t < 1f)
                {
                    if (Stopped()) { Destroy(gameObject); yield break; }
                    t = Mathf.Min(1f, t + speed * Time.deltaTime);
                    Show(t, false);
                    yield return null;
                }
                break;
            }
            t = Mathf.Min(1f, t + Time.deltaTime / fill);
            Show(t, false);
            yield return null;
        }

        // 100％：当たる
        Show(1f, false);
        _fill.color = Color.white;
        _pct.text = "100%";
        var hitGo = new GameObject("Hit");
        hitGo.layer = gameObject.layer;
        hitGo.transform.SetParent(transform, false);
        var rb = hitGo.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var box = hitGo.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = _bar.size;
        if (_owner != null)
        {
            Hitbox hb = _owner.AddHitbox(hitGo, _data, box);
            hb.Activate();
            _owner.NotifyReleased(_data);
        }
        float end = Time.time + _a.HitWindow;
        while (Time.time < end)
        {
            _flash.color = new Color(1f, 1f, 1f, 0.85f * (end - Time.time) / _a.HitWindow);
            yield return null;
        }
        Destroy(hitGo);
        // うすくなって消える
        float fade = 0.25f, start = Time.time;
        while (Time.time - start < fade)
        {
            float k = 1f - (Time.time - start) / fade;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>()) { Color c = sr.color; c.a = Mathf.Min(c.a, k); sr.color = c; }
            WorldText.SetColor(_pct, new Color(1f, 1f, 1f, k));
            yield return null;
        }
        Destroy(gameObject);
    }

    void Show(float t, bool paused)
    {
        _fillT.localScale = new Vector3(Mathf.Max(0.001f, t * _bar.width), 1f, 1f);
        _pct.text = Mathf.FloorToInt(t * 100f) + "%";
        bool red = t >= RedFrom;
        bool blink = Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
        Color fill = red ? (blink ? (Color)SteamArt.Red : new Color(1f, 0.55f, 0.5f)) : (Color)SteamArt.Blue;
        if (paused) fill = (Color)SteamArt.Red;
        _fill.color = fill;
        _edgeTop.color = red ? (Color)SteamArt.Red : (Color)SteamArt.Edge;
        _track.color = red ? new Color(0.35f, 0.06f, 0.06f, 0.85f) : new Color32(0x17, 0x1A, 0x21, 0xD8);
    }

    bool Stopped() => _owner == null || !_owner.isActiveAndEnabled || _owner.Current != _data;
}
