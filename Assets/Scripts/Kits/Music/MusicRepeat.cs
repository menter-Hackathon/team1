using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】ミュージックの能力：リピート（2026-11-19。docs/app-kits-2-design.md 4-1）。クールタイム 5秒。
/// 最後に使った、ほかの能力をもう1回出す（その能力のクールタイムは使わない）。頭の上に回る矢印の印。
/// ・ゲージは元の 0.7倍（攻撃データの欄で持つ能力だけ。専用の技（Custom）の能力は、中の数値のまま）
/// ・まだ何も使っていない・出せない能力（2回押し・ロックオンで相手がいない）なら、音符を扇に 3発（ゲージ 4×3）
/// ・最後に使った能力は MusicKit が覚える（PlayerSkillExecutor.SkillUsed）
/// </summary>
[CreateAssetMenu(fileName = "Music_Repeat", menuName = "Team1/Kits/Music Repeat")]
public class MusicRepeat : CustomAttack
{
    [Tooltip("代わりの音符（種類 Projectile。扇の数・角度もこちら）")]
    [SerializeField] AttackData _notes;
    [Tooltip("もう1回出す能力のゲージの倍率")]
    [SerializeField, Range(0.1f, 1f)] float _gaugeRate = 0.7f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        MusicKit.Watch();
        MusicRepeatIcon.Show(owner.transform);
        if (TryRepeat(owner)) yield break;
        Fan(owner);
        owner.NotifyReleased(data);
    }

    bool TryRepeat(AttackRunner owner)
    {
        SkillDefinition last = MusicKit.LastSkill;
        if (last == null || last.Attack == null) return false;
        AttackData a = last.Attack;
        if (a.Kind == AttackKind.Melee) return false;
        if (a.Kind == AttackKind.Custom)
        {
            if (a.Custom == null || a.Custom is MusicRepeat) return false;
            if (a.Custom.DefersCooldown(owner) || a.Custom.IsCharged(owner)) return false; // 2回押しの途中は出さない
        }
        else
        {
            a = a.CreateScaledCopy(AttackStat.GaugeGain, a.GaugeGain * _gaugeRate);
            SkinDresser.Remember(a, SkinTables.ForSkill(last), last.Theme, last.Icon);
        }
        return owner.Run(a);
    }

    void Fan(AttackRunner owner)
    {
        if (_notes == null) return;
        Vector2 origin = owner.OriginPoint + new Vector2(owner.Facing * _notes.SpawnOffset.x, _notes.SpawnOffset.y);
        Vector2 baseDir = new Vector2(owner.Facing, 0f);
        int n = Mathf.Max(1, _notes.ProjectileCount);
        for (int i = 0; i < n; i++)
        {
            float ang = n > 1 ? -_notes.SpreadAngle * 0.5f + _notes.SpreadAngle * i / (n - 1) : 0f;
            Projectile p = Projectile.Spawn(owner, _notes, origin, Quaternion.Euler(0f, 0f, ang) * baseDir, i);
            if (p == null) continue;
            KitDress.Swap(p.gameObject, MusicArt.Note(i));
            SpriteRenderer v = KitDress.FindVisual(p.transform);
            if (v != null) v.gameObject.AddComponent<MusicUpright>();
        }
    }
}

/// <summary>リピートの印（頭の上で回る矢印。0.5秒）</summary>
public class MusicRepeatIcon : MonoBehaviour
{
    Transform _follow;
    float _start;
    SpriteRenderer _sr;

    public static void Show(Transform follow)
    {
        if (follow == null) return;
        var go = new GameObject("Music_RepeatIcon");
        var m = go.AddComponent<MusicRepeatIcon>();
        m._follow = follow;
        m._start = Time.time;
        m._sr = RuntimeSprites.Show(go.transform, MusicArt.Repeat, Vector2.zero, 60, 1f, "Icon");
        m._sr.color = MusicArt.Pink;
        m.LateUpdate();
    }

    void LateUpdate()
    {
        float a = Time.time - _start;
        if (a > 0.5f || _follow == null) { Destroy(gameObject); return; }
        transform.position = _follow.position + new Vector3(0f, 1.6f, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, -Mathf.Round(a * 720f / 45f) * 45f);
        _sr.color = new Color(_sr.color.r, _sr.color.g, _sr.color.b, a > 0.35f ? (0.5f - a) / 0.15f : 1f);
    }
}
