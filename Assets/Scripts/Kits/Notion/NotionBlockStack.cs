using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Notion のボスの技「ブロックを積む」（2026-11-19。docs/app-kits-1-design.md 8章 P1-1。担当0）。
/// ボスの横に「/」→ 白いブロック（横 4×縦 1。左に「⋮⋮」）が、0.5秒おきに N個、そのときのプレイヤーの x に、階段のように積まれる
/// （1個目＝床・足場の上、2個目＝その 1つ上、3個目＝さらに上）→ 出てから 4秒残って、足場になる。
/// ・2026-11-19：画面の上から落とすのをやめて「横から引っぱって入れる」（docs/stage-gimmick-variety-b.md 4章）：
///   当たりは出現（FallingObject.SpawnInPlace。出た瞬間だけ当たる）。予告の 0.8秒の間、ボスのいる側の端から、
///   うすいブロックの影（ドラッグ中の影）がその場所まですべってくる（見た目だけ。NotionDragGhost）
/// ・落下物の数値は中の攻撃データ（大きさ・予告・判定の秒数・残る秒数・足場）
/// </summary>
[CreateAssetMenu(fileName = "NotionBlockStack", menuName = "Team1/Kits/Notion/Block Stack")]
public class NotionBlockStack : CustomAttack
{
    [Tooltip("ブロック1個の攻撃データ（種類＝落下物。大きさ・着地後に残る秒数・足場になるか・予告の秒数）")]
    [SerializeField] AttackData _block;
    [Tooltip("積む数")]
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("1個ずつの間（秒）")]
    [SerializeField, Min(0f)] float _interval = 0.5f;
    [Tooltip("ブロックの絵の名前（KitDress）")]
    [SerializeField] string _art = NotionKit.ArtBlock;

    public override bool CanRun(AttackRunner owner, AttackData data) => _block != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_block == null) yield break;
        // ボスの横に「/」（スラッシュのメニュー。飾り）
        Vector2 slash = owner.OriginPoint + new Vector2(owner.Facing * 1.2f, 1.2f);
        FxKit.Text("/", slash, 0.8f, NotionArt.Black, 30, 0.6f, NotionArt.White);

        var objects = new List<FallingObject>();
        Rect arena = BattleArena.GetBounds();
        Vector2 size = _block.ObjectSize;
        float half = size.x * 0.5f;
        float baseY = float.NaN; // 1個目の下の面（床・足場の上）。2個目からはその上に積む
        bool bossOnLeft = owner.OriginPoint.x < arena.center.x;
        for (int i = 0; i < _count; i++)
        {
            if (i > 0 && _interval > 0f) yield return new WaitForSeconds(_interval);
            Vector2 target = owner.TargetPoint ?? owner.OriginPoint;
            float x = Mathf.Clamp(target.x, arena.xMin + half, arena.xMax - half);
            if (float.IsNaN(baseY))
            {
                if (!owner.TryFindGround(x, target.y, out baseY, steadyOnly: true)) baseY = arena.yMin;
            }
            float cy = Mathf.Min(baseY + size.y * (i + 0.5f), arena.yMax - size.y * 0.5f);
            Vector2 center = new Vector2(x, cy);
            float telegraph = Mathf.Max(0.1f, _block.TelegraphTime);
            FallingObject f = FallingObject.SpawnInPlace(owner, _block, center, Time.time + telegraph, i);
            if (f != null)
            {
                KitDress.Swap(f.gameObject, KitDress.Art(_art, i));
                // ボスのいる側の端から、うすい影がすべってくる（予告の間）
                Vector2 from = new Vector2(bossOnLeft ? arena.xMin - half : arena.xMax + half, cy);
                NotionDragGhost.Attach(f.transform, KitDress.Art(_art, i), size, from, center, telegraph);
                objects.Add(f);
            }
            owner.NotifyReleased(data);
        }
        // 全部が出る（判定が終わって足場になる。または消える）まで待つ
        float timeout = Time.time + 10f;
        while (Time.time < timeout && objects.Exists(o => o != null && !o.HasLanded)) yield return null;
    }
}

/// <summary>
/// 【仮】ブロックを積む技の予告の飾り（2026-11-19）：うすいブロックの影が、from（ボスのいる側の端）から to（出る場所）へ、
/// seconds 秒ですべってくる（終わりはゆっくり）。着いたら消える。出すブロック（FallingObject）の子なので、一緒に消える。当たり判定は無い
/// </summary>
public class NotionDragGhost : MonoBehaviour
{
    const int Order = 17;
    Vector2 _from, _to;
    float _born, _seconds;
    SpriteRenderer _sr;

    public static NotionDragGhost Attach(Transform parent, Sprite sprite, Vector2 size, Vector2 from, Vector2 to, float seconds)
    {
        if (parent == null) return null;
        var go = new GameObject("NotionDragGhost");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.position = from;
        var g = go.AddComponent<NotionDragGhost>();
        g._from = from;
        g._to = to;
        g._born = Time.time;
        g._seconds = Mathf.Max(0.1f, seconds);
        g._sr = AttackRunner.AddVisual(go.transform, sprite, new Color(1f, 1f, 1f, 0.35f), size, Order, "Ghost");
        // うしろに、うすい青の影（ドラッグ中の影）
        SpriteRenderer shadow = AttackRunner.AddVisual(go.transform, sprite, new Color(NotionArt.Blue.r / 255f, NotionArt.Blue.g / 255f, NotionArt.Blue.b / 255f, 0.25f), size, Order - 1, "Shadow");
        shadow.transform.localPosition = new Vector3(0.15f, -0.15f, 0f);
        return g;
    }

    void Update()
    {
        float k = Mathf.Clamp01((Time.time - _born) / _seconds);
        float e = 1f - (1f - k) * (1f - k); // 終わりはゆっくり
        transform.position = Vector2.Lerp(_from, _to, e);
        if (k >= 1f) Destroy(gameObject);
    }
}
