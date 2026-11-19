using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Unity の技3（第2形態）「Missing Shader」（2026-11-19。docs/app-kits-2-design.md 2-1）。担当A。
/// 流れ：床を 5つに分けた区画（幅4マス）のうち 3つに赤い枠（1.0秒）→ ピンク #FF00FF になり 2.5秒残る → 元に戻る。
/// ・当たるのは、ピンクの床に立っている間（床から 1マスの高さまで）。足場は変わらない
/// ・プレイヤーのいる区画は必ず入る。となりの1つは必ず空ける（歩いて 1.0秒以内に安全な区画へ行ける）
/// ・上に小さく Console の1行（赤い印と `Shader error`）
/// ・場に残る技（攻撃データの「場に残る技」をオン）。ボスの形態変化・正常化で消える
/// </summary>
[CreateAssetMenu(fileName = "UnityMissingShader_", menuName = "Team1/Kits/Unity/Missing Shader")]
public class UnityMissingShaderAttack : CustomAttack
{
    [SerializeField, Min(2)] int _zones = 5;
    [SerializeField, Min(1)] int _pink = 3;
    [Tooltip("赤い枠の秒数（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 1f;
    [SerializeField, Min(0.1f)] float _duration = 2.5f;

    public override bool CanRun(AttackRunner owner, AttackData data) => !UnityMissingShaderEffect.Active;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float w = arena.width / _zones;
        Vector2 player = owner.TargetPoint ?? arena.center;
        int p = Mathf.Clamp(Mathf.FloorToInt((player.x - arena.xMin) / w), 0, _zones - 1);
        var pink = PickZones(p);
        GameObject root = owner.CreateSpawnedObject("UnityMissingShader", arena.center);
        var e = root.AddComponent<UnityMissingShaderEffect>();
        e.Setup(owner, data, arena, w, pink, _telegraph, _duration);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_telegraph + 0.2f);
    }

    // プレイヤーの区画は必ず。となりの1つは必ず空ける
    List<int> PickZones(int p)
    {
        var result = new List<int> { p };
        var neighbors = new List<int>();
        if (p - 1 >= 0) neighbors.Add(p - 1);
        if (p + 1 < _zones) neighbors.Add(p + 1);
        int safe = neighbors.Count > 0 ? neighbors[Random.Range(0, neighbors.Count)] : -1;
        var rest = new List<int>();
        for (int i = 0; i < _zones; i++) if (i != p && i != safe) rest.Add(i);
        while (result.Count < Mathf.Min(_pink, _zones - 1) && rest.Count > 0)
        {
            int k = Random.Range(0, rest.Count);
            result.Add(rest[k]);
            rest.RemoveAt(k);
        }
        return result;
    }
}

/// <summary>ピンクの床（自分で進む。場に残る技）</summary>
public class UnityMissingShaderEffect : CustomEffect
{
    static int _active;

    /// <summary>どこかに出ているか（重ねない）</summary>
    public static bool Active => _active > 0;

    public override bool Lingers => true;

    readonly List<GameObject> _frames = new List<GameObject>();
    readonly List<GameObject> _plates = new List<GameObject>();
    readonly List<Hitbox> _hitboxes = new List<Hitbox>();
    GameObject _console;
    float _hitAt, _endAt, _nextTick;
    bool _hot;

    protected override void OnEnable()
    {
        base.OnEnable();
        _active++;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _active = Mathf.Max(0, _active - 1);
    }

    internal void Setup(AttackRunner owner, AttackData data, Rect arena, float zoneW, List<int> zones, float telegraph, float duration)
    {
        _hitAt = Time.time + telegraph;
        _endAt = _hitAt + duration;
        float floor = arena.yMin;
        float sumX = 0f;
        foreach (int z in zones)
        {
            float cx = arena.xMin + zoneW * (z + 0.5f);
            sumX += cx;
            // 予告：赤い枠（床の帯）
            var f = Child("Frame", new Vector2(cx, floor + 0.35f));
            var fs = RuntimeSprites.Show(f.transform, UnityArt.FrameBox(Mathf.RoundToInt((zoneW - 0.2f) * 18f), 14), Vector2.zero, 18, 1f, "Frame");
            fs.color = KitDress.Red;
            AttackRunner.AddVisual(f.transform, PlaceholderSprites.Square, new Color(1f, 0.27f, 0.23f, 0.22f), new Vector2(zoneW - 0.3f, 0.6f), 17, "Fill");
            _frames.Add(f);

            // 本体：べたのピンク（床の上面）と、うすいピンクの柱（当たる高さ）
            var plate = Child("Plate", new Vector2(cx, floor));
            AttackRunner.AddVisual(plate.transform, PlaceholderSprites.Square, UnityArt.Missing, new Vector2(zoneW, 0.7f), 16, "Pink").transform.localPosition = new Vector3(0f, -0.25f, 0f);
            AttackRunner.AddVisual(plate.transform, PlaceholderSprites.Square, new Color(1f, 0f, 1f, 0.22f), new Vector2(zoneW, 1.1f), 16, "Glow").transform.localPosition = new Vector3(0f, 0.55f, 0f);
            plate.SetActive(false);
            _plates.Add(plate);

            var hgo = Child("Hit", new Vector2(cx, floor + 0.5f));
            var rb = hgo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var box = hgo.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(zoneW - 0.15f, 1f);
            _hitboxes.Add(owner.AddHitbox(hgo, data, box, true));
        }

        // Console の1行（赤い印と Shader error）
        if (zones.Count > 0)
        {
            _console = Child("Console", new Vector2(sumX / zones.Count, floor + 5.6f));
            AttackRunner.AddVisual(_console.transform, PlaceholderSprites.Square, new Color(0.16f, 0.16f, 0.16f, 0.9f), new Vector2(5.6f, 0.7f), 19, "Bar");
            RuntimeSprites.Show(_console.transform, UnityArt.ErrorIcon, new Vector2(-2.45f, 0f), 20, 1f, "Icon");
            WorldText.Create(_console.transform, "Shader error", 0.4f, UnityArt.AxisX, 21, true, TextAnchor.MiddleLeft).transform.localPosition = new Vector3(-2.05f, 0.02f, -0.001f);
            _console.SetActive(false);
        }
    }

    GameObject Child(string name, Vector2 at)
    {
        var go = new GameObject(name);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.position = at;
        return go;
    }

    void Update()
    {
        float now = Time.time;
        if (!_hot)
        {
            float left = _hitAt - now;
            bool on = left > 0.3f || Mathf.FloorToInt(now / 0.08f) % 2 == 0;
            foreach (var f in _frames) if (f != null) f.SetActive(on);
            if (left > 0f) return;
            _hot = true;
            foreach (var f in _frames) if (f != null) Destroy(f);
            foreach (var p in _plates) if (p != null) p.SetActive(true);
            if (_console != null) _console.SetActive(true);
            _nextTick = now;
        }
        if (now >= _endAt) { Destroy(gameObject); return; }
        // 立っている間、0.5秒ごとに当たり直す（被弾のあとの無敵 1.0秒があるので、続けて減らない）
        if (now >= _nextTick)
        {
            _nextTick = now + 0.5f;
            foreach (var h in _hitboxes) if (h != null) h.Activate();
        }
    }
}
