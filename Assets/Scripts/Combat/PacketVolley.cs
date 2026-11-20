using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// パケット（2026-11-19。ブラウザの能力。数値は攻撃データ、見た目は【仮】）。AttackRunner が AttackData（種類 Packet）から作る。
/// ・持ち主のまわりの敵を1体ロックオンする（前にいる敵を優先して、近い敵）。敵の頭の上に IP アドレスを出す
/// ・棒状のパケットを決まった数、少しずつずらして撃つ。最初は扇に広がり、ロックオンした敵へ曲がって飛ぶ（PacketShot）
/// ・ロックオンは変えない：途中で敵が倒されても、その敵が最後にいた所へ飛ぶ
/// ・ロックオンした敵が倒れたら（パケット以外で倒れても）、応答のパケットが敵のいた所から持ち主へ飛んできて、回復する（ResponsePacket）
/// ・敵がいなければ、前へまっすぐ撃つ（回復はない）
/// ・ブラウザのボスも使う（2026-11-19）：狙う相手はプレイヤー。IP アドレスを見せてから（ロックオンの秒数）撃ち始める。回復はなし
/// </summary>
public class PacketVolley : MonoBehaviour
{
    AttackRunner _owner;
    AttackData _data;
    Collider2D _target;
    Health _targetHealth;
    Vector2 _lastTargetPos;
    string _ip;
    int _fired;
    float _nextShot;
    bool _responded;
    float _endAt = float.PositiveInfinity;
    readonly List<PacketShot> _shots = new List<PacketShot>();
    readonly Collider2D[] _found = new Collider2D[24];

    /// <summary>全部撃ち終わったら true（AttackRunner の攻撃はここで終わる）</summary>
    public bool IsFired => _fired >= _data.ProjectileCount;
    /// <summary>ロックオンした敵（いなければ null）</summary>
    public Collider2D Target => _target;
    /// <summary>ロックオンした敵が、今いる（または最後にいた）位置</summary>
    public Vector2 TargetPoint
    {
        get
        {
            if (_target != null && _target.enabled && _target.gameObject.activeInHierarchy) _lastTargetPos = _target.bounds.center;
            return _lastTargetPos;
        }
    }

    public static PacketVolley Spawn(AttackRunner owner, AttackData data)
    {
        GameObject go = owner.CreateSpawnedObject($"Packets_{data.name}", owner.OriginPoint);
        var v = go.AddComponent<PacketVolley>();
        v._owner = owner;
        v._data = data;
        v.LockOn();
        v._nextShot = Time.time + data.PacketLockTime; // IP アドレスを見せてから撃ち始める
        return v;
    }

    Vector2 OwnerCenter
    {
        get
        {
            foreach (var col in _owner.GetComponents<Collider2D>())
            {
                if (!col.isTrigger && col.enabled) return col.bounds.center;
            }
            return _owner.transform.position;
        }
    }

    /// <summary>ロックオンする敵：範囲の中で、前にいる敵を優先して、近い敵</summary>
    void LockOn()
    {
        Vector2 center = OwnerCenter;
        var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
        filter.SetLayerMask(_owner.TargetLayers);
        int n = Physics2D.OverlapCircle(center, _data.PacketRange, filter, _found);
        float best = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            Collider2D c = _found[i];
            if (!c.TryGetComponent(out Hurtbox h) || h.Owner == null || h.Owner.Team == _owner.Team) continue;
            if (h.Owner is Health hp && hp.IsDead) continue;
            if (h.Owner is VirusGauge g && g.IsFull) continue;
            Vector2 d = (Vector2)c.bounds.center - center;
            float score = d.magnitude + (Mathf.Sign(d.x) != _owner.Facing ? 4f : 0f); // 後ろの敵は、少し遠い扱い
            if (score < best)
            {
                best = score;
                _target = c;
                _targetHealth = h.Owner as Health;
            }
        }
        _lastTargetPos = _target != null ? (Vector2)_target.bounds.center : center + new Vector2(_owner.Facing * _data.PacketRange, 0f);
        if (_target != null)
        {
            // 敵ごとに決まった IP アドレス（同じ敵なら同じ）
            uint hsh = (uint)_target.GetInstanceID() * 2654435761u;
            _ip = $"192.168.{(hsh >> 8) % 256}.{2 + (hsh >> 16) % 252}";
            if (_targetHealth != null) _targetHealth.Died += OnTargetDied;
        }
    }

    void OnDestroy()
    {
        if (_targetHealth != null) _targetHealth.Died -= OnTargetDied;
    }

    void OnTargetDied()
    {
        if (_responded) return;
        _responded = true;
        // 応答のパケットが、敵のいた所から持ち主へ飛んでくる（届くと回復）
        if (_data.PacketHeal > 0) ResponsePacket.Spawn(_owner, _data, TargetPoint);
    }

    void Update()
    {
        // 決まった数を、少しずつずらして撃つ（最初は扇に広がる）
        while (!IsFired && Time.time >= _nextShot)
        {
            int n = _data.ProjectileCount;
            float angle = n > 1 ? -_data.SpreadAngle * 0.5f + _data.SpreadAngle * _fired / (n - 1) : 0f;
            Vector2 baseDir = new Vector2(_owner.Facing, 0.35f).normalized;
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDir;
            Vector2 from = OwnerCenter + new Vector2(_owner.Facing * 0.4f, 0.1f);
            _shots.Add(PacketShot.Spawn(_owner, _data, this, from, dir, _target != null));
            GameAudio.Event(_owner.Team == Team.Player ? "gm_browser_packet_shot" : "gm_browser_packet_shot_boss");
            _fired++;
            _nextShot = Time.time + _data.PacketInterval;
        }

        // 撃ち終わって、パケットが全部消えたら、少し待って終わる（そのあと敵が倒れても、回復はしない）
        _shots.RemoveAll(s => s == null);
        if (IsFired && _shots.Count == 0 && _endAt == float.PositiveInfinity) _endAt = Time.time + 0.4f;
        if (Time.time >= _endAt) Destroy(gameObject);
    }

    void OnGUI()
    {
        if (_target == null || string.IsNullOrEmpty(_ip) || _responded || PixelGui.FontEn == null) return;
        if (!_target.enabled || !_target.gameObject.activeInHierarchy) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        PixelGui.Begin(false);
        float scale = PixelGui.Scale;
        Bounds b = _target.bounds;
        Vector3 sp = cam.WorldToScreenPoint(new Vector3(b.center.x, b.max.y + 0.35f, 0f));
        float w = _ip.Length * 8f + 8f, h = 12f;
        var box = new Rect(sp.x / scale - w * 0.5f, (Screen.height - sp.y) / scale - h, w, h);
        bool blink = Mathf.FloorToInt(Time.time / 0.15f) % 4 != 0;
        PixelGui.Fill(box, new Color(0.05f, 0.1f, 0.2f, 0.85f));
        PixelGui.Fill(new Rect(box.x, box.yMax - 1f, box.width, 1f), new Color32(0x42, 0x85, 0xf4, 0xff));
        PixelGui.TextEn(new Rect(box.x + 4f, box.y + 2f, w - 8f, 8f), _ip, 8, blink ? Color.white : new Color(0.6f, 0.85f, 1f));
        PixelGui.End();
    }
}
