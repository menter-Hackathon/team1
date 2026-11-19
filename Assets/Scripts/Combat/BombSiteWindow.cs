using UnityEngine;

/// <summary>
/// 爆弾のサイトのウィンドウ（2026-11-19。ブラウザのボス。数値は攻撃データ、見た目は【仮】）。AttackRunner が AttackData（種類 BombSite）から作る。
/// ・ブラウザのウィンドウ（爆弾のマークのサイト）が、ぽんと開く。ウィンドウそのものには判定がない
/// ・下の読み込みバーが、最初の2割ほどをゆっくり（予告時間）ためてから、残りをさっと（一気にためる秒数）ためる
/// ・読み込み終わったら、その場で爆発する（直径「形の大きさ」の x の円。判定時間のあいだ、1回だけ当たる）。ウィンドウは破片になって消える
/// </summary>
public class BombSiteWindow : MonoBehaviour
{
    const float OpenTime = 0.15f;
    static readonly Color BarBack = new Color32(0x3c, 0x40, 0x43, 0xff);
    static readonly Color BarFill = new Color32(0x42, 0x85, 0xf4, 0xff);
    static readonly Color BarDone = new Color32(0xea, 0x43, 0x35, 0xff);

    AttackData _data;
    SpriteRenderer _window;
    SpriteRenderer _barBack, _barFill;
    SpriteRenderer _blast;
    Hitbox _hitbox;
    Vector2 _size;
    float _start;
    bool _exploded;
    float _explodedAt;

    public static BombSiteWindow Spawn(AttackRunner owner, AttackData data, Vector2 center)
    {
        GameObject go = owner.CreateSpawnedObject($"BombSite_{data.name}", center);
        var w = go.AddComponent<BombSiteWindow>();
        w._data = data;
        w._start = Time.time;
        w._size = data.ObjectSize;

        // ウィンドウ（判定なし）
        w._window = AttackRunner.AddVisual(go.transform, data.Sprite, Color.white, w._size, 26, "Window");
        // 読み込みバー（ウィンドウの下のほう）
        Sprite px = GameArt.Instance != null && GameArt.Instance.Pixel != null ? GameArt.Instance.Pixel : PlaceholderSprites.Square;
        float barW = w._size.x * 0.8f, barH = 3f / GameArt.PixelsPerUnit;
        float barY = -w._size.y * 0.5f + 4f / GameArt.PixelsPerUnit;
        w._barBack = AttackRunner.AddVisual(go.transform, px, BarBack, new Vector2(barW, barH), 27, "BarBack");
        w._barBack.transform.localPosition = new Vector3(0f, barY, 0f);
        w._barFill = AttackRunner.AddVisual(go.transform, px, BarFill, new Vector2(0.01f, barH), 28, "BarFill");
        w._barFill.transform.localPosition = new Vector3(-barW * 0.5f, barY, 0f);

        // 爆発（最後に出す。判定は円）
        float d = Mathf.Max(0.5f, data.AreaSize.x);
        var blastGo = new GameObject("Blast");
        blastGo.layer = go.layer;
        blastGo.transform.SetParent(go.transform, false);
        var rb = blastGo.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = blastGo.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = d * 0.5f;
        w._hitbox = owner.AddHitbox(blastGo, data, col);
        w._blast = AttackRunner.AddVisual(blastGo.transform, data.BombBlastSprite != null ? data.BombBlastSprite : PlaceholderSprites.Circle,
            data.BombBlastSprite != null ? Color.white : new Color(1f, 0.6f, 0.2f, 0.8f), new Vector2(d, d), 29, "BlastArt");
        w._blast.enabled = false;

        go.transform.localScale = Vector3.one * 0.3f;
        return w;
    }

    void Update()
    {
        float age = Time.time - _start;
        if (!_exploded)
        {
            // ぽんと開く
            float o = Mathf.Clamp01(age / OpenTime);
            transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, 1f - (1f - o) * (1f - o));

            // 読み込みバー：最初の2割ほどをゆっくり → 残りを一気に
            float slow = Mathf.Max(0.05f, _data.TelegraphTime);
            float p = age < slow
                ? _data.BombSlowPortion * (age / slow)
                : Mathf.Lerp(_data.BombSlowPortion, 1f, Mathf.Clamp01((age - slow) / _data.BombFillTime));
            SetBar(p, p >= 1f ? BarDone : BarFill);
            if (age >= slow + _data.BombFillTime) Explode();
            return;
        }

        // 爆発：広がりながら薄くなる
        float k = Mathf.Clamp01((Time.time - _explodedAt) / Mathf.Max(0.05f, _data.ActiveTime));
        _blast.transform.localScale = _blastBaseScale * (0.7f + 0.4f * k);
        Color c = _blast.color;
        c.a = 1f - k * k;
        _blast.color = c;
        if (k >= 1f)
        {
            _hitbox.Deactivate();
            Destroy(gameObject);
        }
    }

    Vector3 _blastBaseScale;

    void SetBar(float p, Color color)
    {
        float barW = _size.x * 0.8f, barH = 3f / GameArt.PixelsPerUnit;
        Vector2 native = _barFill.sprite.bounds.size;
        float w = Mathf.Max(0.001f, barW * Mathf.Clamp01(p));
        _barFill.transform.localScale = new Vector3(w / Mathf.Max(native.x, 0.0001f), barH / Mathf.Max(native.y, 0.0001f), 1f);
        _barFill.transform.localPosition = new Vector3(-barW * 0.5f + w * 0.5f, _barFill.transform.localPosition.y, 0f);
        _barFill.color = color;
    }

    void Explode()
    {
        _exploded = true;
        _explodedAt = Time.time;
        transform.localScale = Vector3.one;
        _window.enabled = _barBack.enabled = _barFill.enabled = false;
        _blast.enabled = true;
        _blastBaseScale = _blast.transform.localScale;
        _hitbox.Activate();
        Vfx.Shards(transform.position);
        Vfx.SmokeSmall(transform.position);
    }
}
