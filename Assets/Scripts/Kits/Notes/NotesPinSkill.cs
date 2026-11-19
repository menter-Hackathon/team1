using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】メモの能力「ピン留め」（2026-11-19。docs/app-kits-1-design.md 14章。担当0）。
/// ・画びょうを正面に投げる（弾の数値は中の攻撃データ：毎秒 14マス、届く距離 9マス、ゲージ +8、雑魚は倒れる）
/// ・当たったボスは 2秒、その場にピンで留まる（移動しない・次の技を始めない。KitBoss.Stun。ボスの上に画びょうの絵）。
///   技の途中のボスは、技が終わってから 2秒。続けて当てても伸びない（残りの時間が 2秒に戻るだけ）
/// </summary>
[CreateAssetMenu(fileName = "NotesPinSkill", menuName = "Team1/Kits/Notes/Pin Skill")]
public class NotesPinSkill : CustomAttack
{
    [Tooltip("画びょうの攻撃データ（種類＝弾）")]
    [SerializeField] AttackData _pin;
    [Tooltip("ボスを留める秒数")]
    [SerializeField, Min(0.1f)] float _stun = 2f;
    [Tooltip("画びょうの絵の名前（KitDress）")]
    [SerializeField] string _art = NotesKit.ArtPin;

    public override bool CanRun(AttackRunner owner, AttackData data) => _pin != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_pin == null) yield break;
        Vector2 origin = owner.OriginPoint + new Vector2(owner.Facing * _pin.SpawnOffset.x, _pin.SpawnOffset.y);
        Projectile p = Projectile.Spawn(owner, _pin, origin, new Vector2(owner.Facing, 0f));
        if (p != null)
        {
            KitDress.Swap(p.gameObject, KitDress.Art(_art));
            Hitbox hb = p.GetComponent<Hitbox>();
            float stun = _stun;
            if (hb != null) hb.Hit += (target, info) =>
            {
                if (target is VirusGauge g && g != null) NotesPinHold.Apply(g.GetComponent<BossController>(), stun);
            };
        }
        owner.NotifyReleased(data);
    }
}

/// <summary>ボスに刺さった画びょう（ボスの子）。技の途中なら終わるまで待ってから、決めた秒数ボスを留める</summary>
public class NotesPinHold : MonoBehaviour
{
    BossController _boss;
    float _seconds;
    float _endAt = -1f;

    /// <summary>ボスを留める（もう刺さっていれば、残りの時間を戻すだけ）</summary>
    public static void Apply(BossController boss, float seconds)
    {
        if (boss == null || boss.State != BossState.Fighting) return;
        NotesPinHold hold = boss.GetComponentInChildren<NotesPinHold>();
        if (hold == null)
        {
            var go = new GameObject("NotesPin");
            go.transform.SetParent(boss.transform, false);
            go.transform.position = new Vector3(boss.BodyBounds.center.x, boss.BodyBounds.max.y + 0.35f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = NotesArt.PinDown();
            sr.sortingOrder = 40;
            go.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            hold = go.AddComponent<NotesPinHold>();
            hold._boss = boss;
        }
        hold._seconds = seconds;
        hold._endAt = -1f; // 技が終わったら（今終わっていれば、すぐ）留め始める
    }

    void Update()
    {
        if (_boss == null || _boss.State != BossState.Fighting) { Destroy(gameObject); return; }
        if (_boss.InMove)
        {
            _boss.Stun(_seconds); // 技の途中：終わってから数えるので、毎フレーム戻しておく
            _endAt = -1f;
            return;
        }
        if (_endAt < 0f)
        {
            _boss.Stun(_seconds);
            _endAt = Time.time + _seconds;
        }
        if (Time.time >= _endAt) Destroy(gameObject);
    }
}
