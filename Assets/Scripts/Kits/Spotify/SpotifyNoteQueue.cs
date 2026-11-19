using System.Collections.Generic;
using UnityEngine;
using System.Collections;

/// <summary>
/// 【仮】Spotify の能力：再生キュー（2026-11-19。docs/app-kits-2-design.md 4-2）。クールタイム 4秒・ゲージ 5×3。
/// 頭の上に音符が 3つ並び、0.5秒おきに、いちばん近い敵へ勝手に飛ぶ。その間、自分は自由に動ける。
/// 敵がいなければ正面へ飛ぶ。ステージの決まりに頼らない
/// </summary>
[CreateAssetMenu(fileName = "Spotify_NoteQueue", menuName = "Team1/Kits/Spotify Note Queue")]
public class SpotifyNoteQueue : CustomAttack
{
    [Tooltip("音符の弾（種類 Projectile。ダメージ・ゲージはこちら）")]
    [SerializeField] AttackData _note;
    [SerializeField, Min(1)] int _count = 3;
    [SerializeField, Min(0.05f)] float _interval = 0.5f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _note != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        GameObject go = owner.CreateSpawnedObject("Spotify_NoteQueue", owner.transform.position);
        var q = go.AddComponent<SpotifyNoteHolder>();
        q.Setup(owner, _note, _count, _interval);
        owner.NotifyReleased(data);
        yield break;
    }
}

/// <summary>頭の上の音符の列。0.5秒おきに1つずつ、いちばん近い敵へ撃つ。撃ち終わったら消える</summary>
public class SpotifyNoteHolder : MonoBehaviour
{
    AttackRunner _owner;
    AttackData _note;
    float _interval;
    float _next;
    readonly List<SpriteRenderer> _left = new List<SpriteRenderer>();

    public void Setup(AttackRunner owner, AttackData note, int count, float interval)
    {
        _owner = owner;
        _note = note;
        _interval = interval;
        _next = Time.time + interval;
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer sr = RuntimeSprites.Show(transform, SpotifyArt.Note, new Vector2((i - (count - 1) * 0.5f) * 0.6f, 0f), 40, 1f, "Note" + i);
            _left.Add(sr);
        }
        Follow();
    }

    void LateUpdate()
    {
        if (_owner == null || !_owner.isActiveAndEnabled) { Destroy(gameObject); return; }
        Follow();
        float bob = Mathf.Sin(Time.time * 8f) * 0.06f;
        for (int i = 0; i < _left.Count; i++) if (_left[i] != null) _left[i].transform.localPosition = new Vector3(_left[i].transform.localPosition.x, (i % 2 == 0 ? bob : -bob), 0f);
        if (Time.time < _next || _left.Count == 0) return;
        _next = Time.time + _interval;
        SpriteRenderer sr = _left[0];
        _left.RemoveAt(0);
        Vector2 from = sr != null ? (Vector2)sr.transform.position : (Vector2)transform.position;
        if (sr != null) Destroy(sr.gameObject);
        Fire(from);
        if (_left.Count == 0) Destroy(gameObject, 0.05f);
    }

    void Follow()
    {
        if (_owner != null) transform.position = _owner.transform.position + new Vector3(0f, 1.7f, 0f);
    }

    void Fire(Vector2 from)
    {
        Vector2 dir = new Vector2(_owner.Facing, 0f);
        List<Hurtbox> targets = LockOnStrike.FindTargets(_owner);
        if (targets.Count > 0 && targets[0] != null && targets[0].Collider != null)
        {
            Vector2 d = (Vector2)targets[0].Collider.bounds.center - from;
            if (d.sqrMagnitude > 0.01f) dir = d.normalized;
        }
        Projectile p = Projectile.Spawn(_owner, _note, from, dir);
        if (p == null) return;
        KitDress.Swap(p.gameObject, SpotifyArt.Note);
        SpriteRenderer v = KitDress.FindVisual(p.transform);
        if (v != null) v.gameObject.AddComponent<SpotifyUpright>();
        _owner.NotifyReleased(_note);
    }
}
