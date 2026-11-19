using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】ミュージックのボスの技2：音の波（2026-11-19。docs/app-kits-2-design.md 4-1）。
/// ボスの横の床にスピーカーが立つ。赤い線で高さを見せる（0.8秒）→ 弧の形の弾（高さ 1.5マス）が 0.6秒おきに毎秒 8マスで飛ぶ。
/// ・低い波（床〜1.5）はジャンプ。高い波（2〜3.5）は床にいる（ジャンプしない）。足場の上は高い波に当たる
/// ・第1形態：低・高・低。第2形態：低・高・低・低・高
/// ・横の弾は共通の WallShot（予告の赤い線つき）。絵だけ音の波に差し替える
/// </summary>
[CreateAssetMenu(fileName = "Music_SoundWave", menuName = "Team1/Kits/Music Sound Wave")]
public class MusicSoundWave : CustomAttack
{
    public const float LowY = 0.75f, HighY = 2.75f;

    [Tooltip("波の弾（種類 Projectile。速さ 8・大きさ 0.45×1.5）")]
    [SerializeField] AttackData _wave;
    [Tooltip("並び（L＝低い・H＝高い）")]
    [SerializeField] string _pattern = "LHL";
    [Tooltip("1発ずつの間（秒）")]
    [SerializeField, Min(0.1f)] float _interval = 0.6f;
    [Tooltip("予告（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _wave != null && !string.IsNullOrEmpty(_pattern);

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float floor = WallShot.FloorY(owner, WallShot.AreaFor(owner));
        float bx = owner.transform.position.x;
        float px = owner.TargetPoint.HasValue ? owner.TargetPoint.Value.x : arena.center.x;
        int dir = px >= bx ? 1 : -1;
        float sx = Mathf.Clamp(bx + dir * 1.2f, arena.xMin + 0.6f, arena.xMax - 0.6f);
        float total = (_pattern.Length - 1) * _interval + _telegraph + 0.3f;
        MusicSpeaker.Spawn(owner, new Vector2(sx, floor), dir, total + 0.4f);
        for (int i = 0; i < _pattern.Length; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_interval);
            float h = char.ToUpperInvariant(_pattern[i]) == 'H' ? HighY : LowY;
            WallShot.FireFrom(owner, _wave, new Vector2(sx + dir * 0.6f, floor + h), dir, _telegraph, MusicArt.Wave);
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_telegraph + 0.3f);
    }
}

/// <summary>音の波のスピーカー（飾り。当たらない）。ビートに合わせてふくらむ。形態変化・正常化で消える</summary>
public class MusicSpeaker : CustomEffect
{
    float _until;
    Transform _art;

    public static MusicSpeaker Spawn(AttackRunner owner, Vector2 floorPoint, int dir, float seconds)
    {
        GameObject go = owner.CreateSpawnedObject("Music_Speaker", floorPoint);
        var s = go.AddComponent<MusicSpeaker>();
        s._until = Time.time + seconds;
        Sprite sp = MusicArt.Speaker;
        SpriteRenderer sr = RuntimeSprites.Show(go.transform, sp, new Vector2(0f, sp.bounds.size.y * 0.5f), 14, 1f, "Speaker");
        sr.flipX = dir < 0;
        s._art = sr.transform;
        return s;
    }

    void Update()
    {
        if (Time.time >= _until) { Destroy(gameObject); return; }
        float k = Mathf.Repeat(Time.time, 0.6f) / 0.6f;
        float s = 1f + 0.08f * (1f - k) * (1f - k);
        if (_art != null) _art.localScale = new Vector3(s, 1f + (s - 1f) * 0.5f, 1f);
    }
}
