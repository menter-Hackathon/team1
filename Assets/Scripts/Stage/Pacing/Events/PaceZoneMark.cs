using UnityEngine;

// 【仮】出来事「柱・床」の予告の帯（2026-11-19。docs/stage-pacing-design.md 3-2・8章）。担当2 の物。
// 危ない範囲（床から判定の高さまで）を、うすい赤で塗り、上の端に赤い線を引く。予告の間は点滅して、最後の 0.3秒は速く点滅する。
// 判定の間は、ごくうすい赤のまま残す（スキンの絵と判定の大きさがずれても、当たる所が分かるように）。終わったら自分で消える。
// スキンが予告の絵を断っても（今の絵のまま）、予告がはっきり見えるように足す。文字は出さない。

/// <summary>柱・床の予告の帯（危ない所をうすい赤で塗る）</summary>
public class PaceZoneMark : MonoBehaviour
{
    const int Order = 16; // 影・予告と同じくらい（雑魚・プレイヤーより後ろ）

    SpriteRenderer _fill;
    SpriteRenderer _edge;
    float _telegraph;
    float _active;
    float _age;

    /// <summary>
    /// 帯を1つ出す。xMin〜xMax、床の高さ floorY から height だけ。telegraph 秒の予告のあと、active 秒だけうすく残って消える。
    /// owner があれば、その出した物として登録する（ClearSpawned で消える）
    /// </summary>
    public static PaceZoneMark Show(AttackRunner owner, float xMin, float xMax, float floorY, float height, float telegraph, float active)
    {
        var go = new GameObject("PaceZoneMark");
        go.transform.position = new Vector3((xMin + xMax) * 0.5f, floorY, 0f);
        var m = go.AddComponent<PaceZoneMark>();
        float w = Mathf.Max(0.1f, xMax - xMin);
        Color red = FxKit.EnemyRed;
        m._fill = FxKit.Box(go.transform, new Vector2(0f, height * 0.5f), new Vector2(w, height), new Color(red.r, red.g, red.b, 0.22f), Order, "Fill");
        m._edge = FxKit.Box(go.transform, new Vector2(0f, height - FxKit.Pixel), new Vector2(w, FxKit.Pixel * 2f), new Color(red.r, red.g, red.b, 0.9f), Order + 1, "Edge");
        m._telegraph = Mathf.Max(0.05f, telegraph);
        m._active = Mathf.Max(0f, active);
        if (owner != null) owner.Register(go);
        m.Apply();
        return m;
    }

    void Update()
    {
        _age += Time.deltaTime;
        if (_age >= _telegraph + _active)
        {
            Destroy(gameObject);
            return;
        }
        Apply();
    }

    void Apply()
    {
        float fillA, edgeA;
        if (_age < _telegraph)
        {
            // 予告：ゆっくり明滅。最後の 0.3秒は速く点滅（来る瞬間が分かる）
            float left = _telegraph - _age;
            bool fast = left < 0.3f;
            float wave = fast ? (Mathf.FloorToInt(_age / 0.06f) % 2 == 0 ? 1f : 0.35f) : 0.65f + 0.35f * Mathf.Sin(_age * 9f);
            fillA = 0.22f * wave;
            edgeA = 0.9f * (fast ? wave : 1f);
        }
        else
        {
            fillA = 0.1f;
            edgeA = 0.35f;
        }
        SetAlpha(_fill, fillA);
        SetAlpha(_edge, edgeA);
    }

    static void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }
}
