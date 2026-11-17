using UnityEngine;

/// <summary>
/// 1回だけ再生して消えるエフェクトを出す（煙・火花など）。絵は GameArt から読む。絵が無ければ何もしない。
/// 並び順：煙は敵（10〜12）より手前、弾（20）と同じくらい。火花は一番手前
/// </summary>
public static class Vfx
{
    /// <summary>コマのエフェクトを pos に出す。出せなければ null</summary>
    public static SpriteRenderer Play(Sprite[] frames, Vector2 pos, float fps, int sortingOrder, Color? color = null, bool flipX = false)
    {
        if (frames == null || frames.Length == 0 || frames[0] == null) return null;
        var go = new GameObject("Vfx");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;
        sr.color = color ?? Color.white;
        sr.flipX = flipX;
        var anim = go.AddComponent<SpriteFrameAnimation>();
        anim.Setup(sr, frames, fps, loop: false, destroyWhenDone: true);
        return sr;
    }

    /// <summary>ウイルスの煙（大）</summary>
    public static void Smoke(Vector2 pos) => Play(GameArt.Instance != null ? GameArt.Instance.Smoke : null, pos, 14f, 30);

    /// <summary>ウイルスの煙（小）</summary>
    public static void SmokeSmall(Vector2 pos) => Play(GameArt.Instance != null ? GameArt.Instance.SmokeSmall : null, pos, 16f, 30);

    /// <summary>ボスのウィンドウの破片</summary>
    public static void Shards(Vector2 pos) => Play(GameArt.Instance != null ? GameArt.Instance.Shards : null, pos, 14f, 31);

    /// <summary>きらめき</summary>
    public static void Sparkle(Vector2 pos) => Play(GameArt.Instance != null ? GameArt.Instance.Sparkle : null, pos, 12f, 46);

    /// <summary>攻撃が当たったときの火花</summary>
    public static void HitSpark(Vector2 pos) => Play(GameArt.Instance != null ? GameArt.Instance.HitSpark : null, pos, 24f, 45);

    /// <summary>新しいタブ（瞬間移動）のウィンドウ。theme があれば、タイトルバーをその色にする</summary>
    public static void TabOpen(Vector2 pos, AppTheme theme = null)
    {
        Sprite[] frames = GameArt.Instance != null ? GameArt.Instance.TabOpen : null;
        if (theme != null) frames = ThemeRecolor.TabOpen(frames, theme);
        Play(frames, pos, 20f, 44);
    }
}
