using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Spotify のボスの技3（第2形態）：再生キュー（2026-11-19。docs/app-kits-2-design.md 4-2）。
/// ボスの上に 5つのアイコンの列（↑＝高い弾・↓＝低い弾・●＝ねらう弾）→ シャッフル（0.5秒）→ 1.0秒止まって見せる
/// → 左から順に 0.5秒おきに撃つ（撃つアイコンが緑に光る。どの弾も赤い線の予告 0.8秒）。
/// ・避け方：列を読む。低い弾はジャンプ、高い弾は床にいる、ねらう弾は線から出る
/// </summary>
[CreateAssetMenu(fileName = "Spotify_Queue", menuName = "Team1/Kits/Spotify Queue")]
public class SpotifyQueue : CustomAttack
{
    [Tooltip("横の弾（↑・↓。種類 Projectile）")]
    [SerializeField] AttackData _lane;
    [Tooltip("ねらう弾（●。種類 Projectile）")]
    [SerializeField] AttackData _aimed;
    [SerializeField, Min(1)] int _count = 5;
    [SerializeField, Min(0f)] float _shuffleTime = 0.5f;
    [SerializeField, Min(0f)] float _showTime = 1.0f;
    [SerializeField, Min(0.1f)] float _interval = 0.5f;
    [Tooltip("予告（短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _lane != null && _aimed != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        var kinds = new int[_count];
        // 3種類が少なくとも1つずつ入るように
        for (int i = 0; i < _count; i++) kinds[i] = i < 3 ? i : Random.Range(0, 3);
        for (int i = _count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
        }
        SpotifyQueueRow row = SpotifyQueueRow.Spawn(owner, _count);
        float t0 = Time.time;
        while (Time.time - t0 < _shuffleTime)
        {
            for (int i = 0; i < _count; i++) row.Set(i, Random.Range(0, 3), false);
            yield return new WaitForSeconds(0.08f);
        }
        for (int i = 0; i < _count; i++) row.Set(i, kinds[i], false);
        yield return new WaitForSeconds(_showTime);

        Rect arena = BattleArena.GetBounds();
        float floor = WallShot.FloorY(owner, WallShot.AreaFor(owner));
        for (int i = 0; i < _count; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_interval);
            row.Set(i, kinds[i], true);
            float bx = owner.transform.position.x;
            float px = owner.TargetPoint.HasValue ? owner.TargetPoint.Value.x : arena.center.x;
            int dir = px >= bx ? 1 : -1;
            if (kinds[i] == 2)
            {
                owner.StartCoroutine(Aimed(owner, data));
            }
            else
            {
                float h = kinds[i] == 0 ? WallShot.HighY : WallShot.LowY;
                float sx = Mathf.Clamp(bx + dir * 1f, arena.xMin + 0.4f, arena.xMax - 0.4f);
                WallShot.FireFrom(owner, _lane, new Vector2(sx, floor + h), dir, _telegraph, SpotifyArt.Note, p => Upright(p));
            }
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_telegraph + 0.3f);
        if (row != null) Destroy(row.gameObject);
    }

    IEnumerator Aimed(AttackRunner owner, AttackData data)
    {
        Vector2 from = owner.OriginPoint;
        Vector2 to = owner.TargetPoint ?? from + Vector2.down;
        Vector2 dir = (to - from).sqrMagnitude > 0.01f ? (to - from).normalized : Vector2.down;
        KitDress.Line(owner, from, from + dir * 30f, _telegraph);
        yield return new WaitForSeconds(_telegraph);
        if (owner == null || !owner.isActiveAndEnabled) yield break;
        Projectile p = Projectile.Spawn(owner, _aimed, from, dir);
        if (p != null)
        {
            KitDress.Swap(p.gameObject, SpotifyArt.Note);
            Upright(p);
        }
        owner.NotifyReleased(data);
    }

    static void Upright(Projectile p)
    {
        if (p == null) return;
        SpriteRenderer v = KitDress.FindVisual(p.transform);
        if (v != null) v.gameObject.AddComponent<SpotifyUpright>();
    }
}

/// <summary>弾が回っても、絵だけは上を向いたままにする</summary>
public class SpotifyUpright : MonoBehaviour
{
    void LateUpdate() => transform.rotation = Quaternion.identity;
}

/// <summary>ボスの上のアイコンの列（再生キュー）。形態変化・正常化で消える</summary>
public class SpotifyQueueRow : CustomEffect
{
    SpriteRenderer[] _icons;
    SpriteRenderer[] _glow;
    Transform _boss;

    public static SpotifyQueueRow Spawn(AttackRunner owner, int count)
    {
        GameObject go = owner.CreateSpawnedObject("Spotify_QueueRow", owner.transform.position);
        var r = go.AddComponent<SpotifyQueueRow>();
        r._boss = owner.transform;
        r._icons = new SpriteRenderer[count];
        r._glow = new SpriteRenderer[count];
        float step = 0.85f;
        FxKit.Box(go.transform, Vector2.zero, new Vector2(step * count + 0.2f, 0.95f), new Color(0.07f, 0.07f, 0.07f, 0.9f), 58, "Back");
        for (int i = 0; i < count; i++)
        {
            Vector2 at = new Vector2((i - (count - 1) * 0.5f) * step, 0f);
            r._glow[i] = FxKit.Box(go.transform, at, new Vector2(0.8f, 0.8f), (Color)SpotifyArt.Green, 59, "Glow" + i);
            r._glow[i].enabled = false;
            r._icons[i] = RuntimeSprites.Show(go.transform, SpotifyArt.QueueIcon(0), at, 60, 1f, "Icon" + i);
        }
        r.LateUpdate();
        return r;
    }

    public void Set(int i, int kind, bool lit)
    {
        if (_icons == null || i < 0 || i >= _icons.Length) return;
        _icons[i].sprite = SpotifyArt.QueueIcon(kind);
        if (lit) for (int k = 0; k < _glow.Length; k++) _glow[k].enabled = k == i;
    }

    void LateUpdate()
    {
        if (_boss == null) return;
        BossController b = _boss.GetComponent<BossController>();
        Vector3 top = b != null ? new Vector3(b.BodyBounds.center.x, b.BodyBounds.max.y + 0.7f, 0f) : _boss.position + Vector3.up * 2f;
        transform.position = top;
    }
}
