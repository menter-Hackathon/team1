using System;
using UnityEngine;

/// <summary>
/// 【確認用・仮】KitMotion の部品を1つずつ試す（2026-11-19）。担当0 の物。
/// エディタのメニュー「Tools/Team1/KitMotion の試し」（プレイ中だけ）から呼ぶ。だれの物でもない（KitOwner.None）ので、自分で終わって消える。
/// 本番の流れからは呼ばない
/// </summary>
public class KitMotionDemo : MonoBehaviour
{
    /// <summary>部品の名前（Play の番号の順）</summary>
    public static readonly string[] Names =
    {
        "Erupt", "EdgeSweep", "Diagonal", "Orbit", "Bounce", "FloorWave", "WallCrawl",
        "Pop", "Conveyor", "HitTarget", "Catch", "HoldZone", "SafeSpot",
    };

    int _next;
    float _nextAt;

    /// <summary>index 番目の部品を、プレイヤーのまわりで1回出す</summary>
    public static void Play(int index)
    {
        Rect a = KitMotion.Area;
        float floor = KitMotion.FloorY();
        Vector2 p = KitMotion.PlayerPos;
        KitOwner o = KitOwner.None;
        switch (index)
        {
            case 0: KitMotion.Erupt(o, p.x, 1.6f, 2.8f); break;
            case 1: KitMotion.EdgeSweep(o, KitSide.Right, 0.5f, 7f, count: 3, gap: 0.8f); KitMotion.EdgeSweep(o, KitSide.Left, 3f, 5f, wave: 0.8f); break;
            case 2: KitMotion.Diagonal(o, KitMotion.Corner(KitCorner.TopLeft), 9f, count: 3, spread: 18f); break;
            case 3: KitMotion.Orbit(o, KitMotion.Player, 2.2f, 120f, count: 3, duration: 5f, damage: 0); break;
            case 4: KitMotion.Bounce(o, new Vector2(a.xMin + 1f, a.yMax - 1.5f), new Vector2(1f, -0.7f), 8f, 4); break;
            case 5: KitMotion.FloorWave(o, KitSide.Both, 6f, meet: true); break;
            case 6: KitMotion.WallCrawl(o, KitCorner.BottomLeft, KitTurn.CCW, 9f); break;
            case 7: KitMotion.Pop(o, p + new Vector2(2.5f, 1.5f), life: 1.5f, ringCount: 6); break;
            case 8: KitMotion.Conveyor(o, p.x, 6f, 2.5f, 3f); break;
            case 9:
                KitMotion.HitTarget(o, new Vector2(a.center.x, floor + 3f), 5f, () => Debug.Log("KitMotion：的を叩いた"), () => Debug.Log("KitMotion：的を叩かなかった"),
                    path: KitPath.Orbit(new Vector2(a.center.x, floor + 3f), 1.5f, 60f));
                break;
            case 10:
                KitMotion.Catch(o, KitPath.Sweep(KitSide.Left, floor + 1f, 3.5f, wave: 0.6f), () => Debug.Log("KitMotion：取った"));
                break;
            case 11: KitMotion.HoldZone(o, p.x + 2f, 3f, 2f, () => Debug.Log("KitMotion：立ちきった"), life: 8f); break;
            case 12: KitMotion.SafeSpot(o, p.x + 3f, 2.2f, 1.6f); break;
        }
    }

    /// <summary>全部を 4秒おきに順に出す</summary>
    public static void PlayAll()
    {
        var go = new GameObject("KitMotionDemo");
        go.AddComponent<KitMotionDemo>();
    }

    void Update()
    {
        if (Time.time < _nextAt) return;
        if (_next >= Names.Length)
        {
            Destroy(gameObject);
            return;
        }
        try { Play(_next); }
        catch (Exception e) { Debug.LogException(e); }
        Debug.Log($"KitMotion の試し：{Names[_next]}");
        _next++;
        _nextAt = Time.time + 4f;
    }
}
