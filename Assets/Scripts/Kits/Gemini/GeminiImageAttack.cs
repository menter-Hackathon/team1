using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】画像生成（2026-11-19。Gemini の第2形態の技。docs/app-kits-1-design.md 7章 P2）。担当3 の物。
/// 大きい絵（額ぶち。4×3マス）が、そのときのプレイヤーの足元（床・足場の上に、絵の下の辺を合わせる）に 2枚（0.6秒おき）、
/// その場に「生成される」（予告 0.8秒：額ぶちの点線の枠＋中にモザイクが下から 1段ずつ埋まる）→ 出てから 3秒残って足場になる。
/// ・2026-11-19：画面の上から落とすのをやめて、その場に出す（FallingObject.SpawnInPlace。Claude のパネルと同じ呼び方。
///   docs/stage-gimmick-variety-b.md 3章）。当たるのは出た瞬間（中の攻撃データの判定の秒数）だけ
/// ・絵は中の攻撃データ（落下物）を FallingObject で出し、額ぶちの絵に差し替える（1枚ずつ、そのときのプレイヤーを狙う）
/// </summary>
[CreateAssetMenu(fileName = "Gemini_Image", menuName = "Team1/Kits/Gemini Image")]
public class GeminiImageAttack : CustomAttack
{
    [Tooltip("出す絵（落下物の攻撃データ。大きさ・予告・判定の秒数・残る秒数・足場）")]
    [SerializeField] AttackData _frame;
    [Tooltip("【仮】枚数")]
    [SerializeField, Min(1)] int _count = 2;
    [Tooltip("【仮】間隔（秒）")]
    [SerializeField, Min(0.1f)] float _interval = 0.6f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _frame != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_frame == null) yield break;
        Rect arena = BattleArena.GetBounds();
        Vector2 size = _frame.ObjectSize;
        float half = size.x * 0.5f;
        var objects = new List<FallingObject>();
        // ボスの上に星（「生成中」）
        Bounds body = GeminiArt.BodyOf(owner);
        FxAnim.Sprite(GeminiArt.Star4(13), new Vector2(body.center.x, body.max.y + 0.8f), 33, _count * _interval + 0.4f, Color.white, 1.4f, "GeminiGenerate").Spin(300f).Pop().FadeFrom(0.8f);
        for (int i = 0; i < _count; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_interval);
            Vector2 target = owner.TargetPoint ?? owner.OriginPoint;
            float x = Mathf.Clamp(target.x, arena.xMin + half, arena.xMax - half);
            // プレイヤーの足元の床・足場（動く・消える足場は飛ばす）。絵の下の辺をそこに合わせる
            if (!owner.TryFindGround(x, target.y, out float ground, steadyOnly: true)) ground = arena.yMin;
            Vector2 center = new Vector2(x, Mathf.Min(ground + size.y * 0.5f, arena.yMax - size.y * 0.5f));
            float telegraph = Mathf.Max(0.1f, _frame.TelegraphTime);
            FallingObject f = FallingObject.SpawnInPlace(owner, _frame, center, Time.time + telegraph, i);
            if (f != null)
            {
                KitDress.Swap(f.gameObject, GeminiArt.ImageFrame);
                GeminiMosaicFill.Attach(f.transform, center, size, telegraph);
                objects.Add(f);
            }
            owner.NotifyReleased(data);
        }
        float timeout = Time.time + 10f;
        while (Time.time < timeout && objects.Exists(o => o != null && !o.HasLanded)) yield return null;
    }
}

/// <summary>
/// 【仮】画像生成の予告の飾り（2026-11-19）：額ぶちの中に、モザイク（青・紫・ピンクの小さい四角）が下から 1段ずつ埋まる。
/// 出る時刻で消える。出す絵（FallingObject）の子なので、絵と一緒に消える。当たり判定は無い
/// </summary>
public class GeminiMosaicFill : MonoBehaviour
{
    const int Cols = 8, Rows = 6;
    const int Order = 17; // 予告の枠（18）・絵（19）より後ろ

    float _born, _seconds;
    Vector2 _size;
    int _rows;

    public static GeminiMosaicFill Attach(Transform parent, Vector2 center, Vector2 size, float seconds)
    {
        if (parent == null) return null;
        var go = new GameObject("GeminiMosaic");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        var m = go.AddComponent<GeminiMosaicFill>();
        m._born = Time.time;
        m._seconds = Mathf.Max(0.1f, seconds);
        m._size = size;
        return m;
    }

    void Update()
    {
        float k = (Time.time - _born) / _seconds;
        if (k >= 1f) { Destroy(gameObject); return; }
        int want = Mathf.Min(Rows, Mathf.FloorToInt(k * (Rows + 1)));
        while (_rows < want) AddRow(_rows++);
    }

    void AddRow(int row)
    {
        Vector2 cell = new Vector2(_size.x / Cols, _size.y / Rows);
        for (int c = 0; c < Cols; c++)
        {
            int pick = (c * 7 + row * 3) % 3;
            Color col = pick == 0 ? GeminiArt.Blue : pick == 1 ? GeminiArt.Purple : GeminiArt.Pink;
            col.a = 0.45f;
            Vector2 local = new Vector2(-_size.x * 0.5f + cell.x * (c + 0.5f), -_size.y * 0.5f + cell.y * (row + 0.5f));
            FxKit.Box(transform, local, cell * 0.86f, col, Order, "Cell");
        }
    }
}
