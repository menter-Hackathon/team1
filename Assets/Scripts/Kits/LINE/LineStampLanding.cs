using UnityEngine;

/// <summary>【仮】スタンプが着地したとき、小さいハートが飛ぶ（飾り。2026-11-19）。落下物（FallingObject）に付ける</summary>
public class LineStampLanding : MonoBehaviour
{
    FallingObject _f;
    bool _done;

    void Awake() => _f = GetComponent<FallingObject>();

    void Update()
    {
        if (_done || _f == null || !_f.HasLanded) return;
        _done = true;
        Hearts(transform.position + Vector3.down * (_f.Data != null ? _f.Data.ObjectSize.y * 0.3f : 0.5f), 5);
    }

    /// <summary>ハートを n 個、上へはじけさせる</summary>
    public static void Hearts(Vector2 at, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Lerp(30f, 150f, n > 1 ? i / (float)(n - 1) : 0.5f) * Mathf.Deg2Rad;
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(3f, 5f);
            FxAnim.Sprite(TalkArt.HeartIcon, at, 23, 0.6f, null, 1f, "Line_Heart").Velocity(v).Gravity(9f).FadeFrom(0.5f);
        }
    }
}
