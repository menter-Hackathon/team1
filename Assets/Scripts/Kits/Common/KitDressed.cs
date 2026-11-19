using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】包み（2026-11-19。docs/app-kits-1-design.md E6・20-4 の「差し替え」の技）。担当0 の物。
/// 中に入れた攻撃データ（枠の語を名前に入れない）を今の部品で出し、返ってきた物の絵を KitDress の絵（名前で登録した物）に差し替える。
/// ・オプション：弾の通り道に、予告の間だけ赤い細い線（「予告の線」）。線を出すときは、線の向きのまま撃つ（予告と弾がずれない）
/// ・予告の線を出さないときは、中の攻撃データの「予備動作」だけ待ってから出す（包みの攻撃データの予備動作は 0 にする）
/// ・作り方：Editor の KitAssetBuilder.Dressed(...)（中の攻撃データ・絵の名前・予告の線）
/// </summary>
[CreateAssetMenu(fileName = "KitDressed_", menuName = "Team1/Kits/Kit Dressed")]
public class KitDressed : CustomAttack
{
    [Tooltip("中の攻撃データ（弾・指定位置の判定・落下物）。名前に枠の語を入れない")]
    [SerializeField] AttackData _inner;
    [Tooltip("差し替える絵の名前（KitDress.RegisterArt で登録した物）。空なら下の絵")]
    [SerializeField] string _art;
    [Tooltip("差し替える絵（アセットの絵を使うとき。ふつうは空で、上の名前を使う）")]
    [SerializeField] Sprite _sprite;
    [Tooltip("オン：絵に色を掛ける")]
    [SerializeField] bool _useTint;
    [SerializeField] Color _tint = Color.white;
    [Tooltip("オン：弾の通り道に、予告の間だけ赤い細い線（弾のときだけ）")]
    [SerializeField] bool _line;
    [Tooltip("予告の線の秒数（0.8 が基本。短くしない）")]
    [SerializeField, Min(0f)] float _lineSeconds = 0.8f;
    [Tooltip("予告の線の長さ（マス）。0 なら画面の端まで")]
    [SerializeField, Min(0f)] float _lineLength;

    /// <summary>中の攻撃データ</summary>
    public AttackData Inner => _inner;
    /// <summary>絵の名前</summary>
    public string ArtKey => _art;

    public override bool CanRun(AttackRunner owner, AttackData data) => _inner != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_inner == null) yield break;
        if (_line && _inner.Kind == AttackKind.Projectile)
        {
            yield return LinedProjectiles(owner);
            yield break;
        }
        if (_inner.Windup > 0f) yield return new WaitForSeconds(_inner.Windup);
        AttackData inner = _inner;
        int count = 0;
        // Spawned は、部品が中身（絵・判定）を作る前に来るので、出した物の Start（作り終わったあと）で差し替える
        Action<GameObject> dress = go => { if (go != null) go.AddComponent<KitDressLater>().Init(this, inner, count++); };
        owner.Spawned += dress;
        try { yield return owner.Execute(_inner); }
        finally { owner.Spawned -= dress; }
    }

    // 線の向きを決めてから予告し、その向きのまま撃つ
    IEnumerator LinedProjectiles(AttackRunner owner)
    {
        AttackData d = _inner;
        Vector2 origin = owner.OriginPoint + new Vector2(owner.Facing * d.SpawnOffset.x, d.SpawnOffset.y);
        Vector2 baseDir = new Vector2(owner.Facing, 0f);
        if (d.Aim == AimMode.Target && owner.TargetPoint is Vector2 target)
        {
            Vector2 t = target - origin;
            if (t.sqrMagnitude > 0.0001f) baseDir = t.normalized;
        }
        int n = Mathf.Max(1, d.ProjectileCount);
        var dirs = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            float angle = n > 1 ? -d.SpreadAngle * 0.5f + d.SpreadAngle * i / (n - 1) : 0f;
            dirs.Add(Quaternion.Euler(0f, 0f, angle) * baseDir);
        }
        float len = _lineLength > 0f ? _lineLength : Mathf.Max(4f, d.Speed * d.Lifetime);
        len = Mathf.Min(len, 40f);
        foreach (var dir in dirs) KitDress.Line(owner, origin, origin + dir * len, _lineSeconds);
        if (_lineSeconds > 0f) yield return new WaitForSeconds(_lineSeconds);

        for (int r = 0; r < Mathf.Max(1, d.RepeatCount); r++)
        {
            if (r > 0 && d.RepeatInterval > 0f) yield return new WaitForSeconds(d.RepeatInterval);
            for (int i = 0; i < dirs.Count; i++)
            {
                Projectile p = Projectile.Spawn(owner, d, origin, dirs[i], r * n + i);
                if (p != null) Dress(p.gameObject, r * n + i);
            }
            owner.NotifyReleased(d);
        }
    }

    /// <summary>出した物を差し替える（中の攻撃データの物だけ）。KitDressLater が呼ぶ</summary>
    internal void DressSpawned(GameObject go, AttackData inner, int index)
    {
        if (go == null) return;
        if (go.TryGetComponent(out Projectile p)) { if (p.Data != inner) return; }
        else if (go.TryGetComponent(out AreaStrike a)) { if (a.Data != inner) return; }
        else if (go.TryGetComponent(out FallingObject f)) { if (f.Data != inner) return; }
        else return;
        Dress(go, index);
    }

    void Dress(GameObject go, int index)
    {
        Sprite s = !string.IsNullOrEmpty(_art) ? KitDress.Art(_art, index) : null;
        if (s == null) s = _sprite;
        if (s == null) return; // 絵がまだ無ければ、今の絵のまま
        KitDress.Swap(go, s, _useTint ? _tint : (Color?)null);
    }
}

/// <summary>出した物の絵を、作り終わったあと（Start）で差し替える（KitDressed が付ける。差し替えたら自分を消す）</summary>
public class KitDressLater : MonoBehaviour
{
    KitDressed _dress;
    AttackData _inner;
    int _index;

    internal void Init(KitDressed dress, AttackData inner, int index)
    {
        _dress = dress;
        _inner = inner;
        _index = index;
    }

    void Start()
    {
        if (_dress != null) _dress.DressSpawned(gameObject, _inner, _index);
        Destroy(this);
    }
}
