using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】プレイヤーの位置に、interval 秒おきに count 個落とす（2026-11-19。担当2：Discord のスタンプ連打・Zoom の退出ボタン）。
/// 今の落下物（Spawn）は「くり返し」を見ないので、ここで1個ずつ出す。
/// ・落とす物の数値（大きさ・落ちる速さ・予告の秒数＝影）は中の攻撃データ（種類＝落下物）
/// ・中の攻撃データの名前に枠の語（ErrorWindowFall）があれば、スキンの絵が付く（Discord：大きいスタンプ）。
///   無ければ art（KitDress.RegisterArt の名前）の絵に差し替える（Zoom：赤い退出のボタン）
/// ・影の上に赤い「！」を足す（危ない物は赤い予告）
/// </summary>
[CreateAssetMenu(fileName = "Call_DropRepeat", menuName = "Team1/Kits/Call Drop Repeat")]
public class CallDropRepeat : CustomAttack
{
    [Tooltip("落とす物（種類＝落下物）。予告の秒数・大きさ・落ちる速さはこちら")]
    [SerializeField] AttackData _inner;
    [Tooltip("落とす数")]
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("落とす間隔（秒）")]
    [SerializeField, Min(0f)] float _interval = 0.4f;
    [Tooltip("差し替える絵の名前（空ならそのまま）")]
    [SerializeField] string _art;
    [Tooltip("影の上に赤い「！」を出す")]
    [SerializeField] bool _exclaim = true;

    public override bool CanRun(AttackRunner owner, AttackData data) => _inner != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (_inner == null || owner == null) yield break;
        Rect arena = BattleArena.GetBounds();
        float half = _inner.ObjectSize.x * 0.5f;
        float hangY = arena.yMax + _inner.ObjectSize.y * 0.5f;
        FallingObject last = null;
        for (int i = 0; i < _count; i++)
        {
            if (i > 0 && _interval > 0f) yield return new WaitForSeconds(_interval);
            float x = Mathf.Clamp((owner.TargetPoint ?? owner.OriginPoint).x, arena.xMin + half, arena.xMax - half);
            float fallAt = Time.time + Mathf.Max(0.8f, _inner.TelegraphTime);
            last = FallingObject.Spawn(owner, _inner, new Vector2(x, hangY), fallAt, fallAt, true, false, i);
            if (last == null) continue;
            if (!string.IsNullOrEmpty(_art)) KitDress.Swap(last.gameObject, KitDress.Art(_art, i));
            if (_exclaim) CallKitUtil.AddExclaim(last);
            owner.NotifyReleased(data);
        }
        // 最後の1個が着地するまで待つ
        float timeout = Time.time + 6f;
        while (last != null && !last.HasLanded && Time.time < timeout) yield return null;
    }

}

/// <summary>【仮】担当2 のキットで共通の小さい絵（2026-11-19）</summary>
public static class CallArt
{
    static readonly Color32 R = new Color32(0xFF, 0x45, 0x3A, 0xFF);
    static readonly Color32 W = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

    /// <summary>赤い「！」（5×9。白いふち）</summary>
    public static Sprite Exclaim => RuntimeSprites.FromAscii("Call_Exclaim", new[]
    {
        ".WWW.",
        "WRRRW",
        "WRRRW",
        "WRRRW",
        ".WRW.",
        ".WRW.",
        ".WWW.",
        ".WRW.",
        ".WWW.",
    }, new System.Collections.Generic.Dictionary<char, Color32> { { 'R', R }, { 'W', W } });

    /// <summary>白い円（d ドット）</summary>
    public static Sprite Disc(int d) => FxArt.Disc(d);

    /// <summary>白い輪（d ドット、太さ t ドット）</summary>
    public static Sprite Ring(int d, int t)
    {
        d = Mathf.Clamp(d, 4, 96);
        t = Mathf.Clamp(t, 1, d / 2);
        string key = $"Call_Ring{d}_{t}";
        var rows = new string[d];
        float r = d * 0.5f;
        for (int y = 0; y < d; y++)
        {
            var line = new char[d];
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                line[x] = dist <= r && dist >= r - t ? 'W' : '.';
            }
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii(key, rows, new System.Collections.Generic.Dictionary<char, Color32> { { 'W', W } });
    }

    /// <summary>白い四角のふち（w×h ドット、太さ t）</summary>
    public static Sprite Frame(int w, int h, int t)
    {
        w = Mathf.Clamp(w, 3, 400);
        h = Mathf.Clamp(h, 3, 400);
        string key = $"Call_Frame{w}x{h}_{t}";
        var rows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var line = new char[w];
            for (int x = 0; x < w; x++) line[x] = x < t || y < t || x >= w - t || y >= h - t ? 'W' : '.';
            rows[y] = new string(line);
        }
        return RuntimeSprites.FromAscii(key, rows, new System.Collections.Generic.Dictionary<char, Color32> { { 'W', W } });
    }

    /// <summary>角の丸い白い四角（w×h ドット）</summary>
    public static Sprite Round(int w, int h, int corner) => MacUi.RoundRectSprite(w, h, corner);

    /// <summary>1マスのドット数</summary>
    public const float Ppu = 18f;
    public const float Px = 1f / 18f;
}
