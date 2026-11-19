using UnityEngine;

/// <summary>
/// 【仮】隅の通知（とどめのとき。2026-11-18。docs/app-skill-visuals-design.md 1-5。まさぴ〜：題名つきで出す）。
/// 2026-11-19【仮】Mac の通知の形と場所にした（docs/mac-look-design.md 1-4・2章 1）：
/// ・明るい地（MacUi.BannerBg）・ふち無し・影・角 3ドット。左にアプリのアイコン（16ドット）、右に太字の題名1行（#1C1C1E。黒いふちどり無し）。右上に時刻の代わりの棒
/// ・画面の右上：右の端から 0.35マス、上の端から 2.3マス下（HUD の STAGE とボスのゲージの下）
/// ・右からすべり込む（0.15秒）→ 1.6秒とどまる → 右へ出ていく（0.2秒）。大きさは 5.5×1.5マス（題名が長ければ横に伸ばす）
/// ・プレイヤー・ボスが後ろに重なっている間は、4割まですかす
/// カメラの子（ゲームの世界の絵）。自動の撮影に写る。呼び方（Show）は今のまま。edge はアイコンが無いときの代わりの色にだけ使う
/// </summary>
public class CornerToast : MonoBehaviour
{
    const float Height = 1.5f;
    const float MinWidth = 5.5f;
    const float Margin = 0.35f;
    /// <summary>上の端からの位置（マス）【仮】</summary>
    const float TopOffset = 2.3f;
    const float InTime = 0.15f, StayTime = 1.6f, OutTime = 0.2f;
    const float Px = 1f / 18f;

    static CornerToast _current;

    float _width;
    float _age;
    SpriteRenderer[] _srs;
    float[] _srAlpha;
    TextMesh[] _tms;
    Color[] _tmBase;
    float _fade = 1f;

    /// <summary>通知を出す（前の通知が出ていれば、入れ替える）</summary>
    public static void Show(string title, Sprite icon, Color edge)
    {
        if (string.IsNullOrEmpty(title)) return;
        Transform root = FxKit.CameraChild("CornerToast");
        if (root == null) return;
        if (_current != null) Destroy(_current.gameObject);
        _current = root.gameObject.AddComponent<CornerToast>();
        _current.Build(title, icon, edge);
        GameAudio.Event("toast");
    }

    void Build(string title, Sprite icon, Color edge)
    {
        // 幅：左 5 ＋ アイコン 16 ＋ 5 ＋ 題名 ＋ 右 8（ドット）。5.5マスより小さくしない
        int hPx = Mathf.RoundToInt(Height * 18f);
        int textPx = Mathf.CeilToInt(title.Length * MacUi.CharPx);
        int wPx = Mathf.Max(Mathf.RoundToInt(MinWidth * 18f), 5 + MacUi.IconPx + 5 + textPx + 8);
        _width = wPx * Px;

        if (icon == null)
        {
            // アイコンが無いときは、アプリの色の角の丸い四角
            edge.a = 1f;
            icon = MacUi.RoundRectSprite(MacUi.IconPx, MacUi.IconPx, 3);
            Transform b = MacUi.PlaceBanner(transform, Vector2.zero, new Vector2(wPx, hPx), icon, title, null, FxKit.OrderToast);
            Transform ic = b.Find("Icon");
            if (ic != null && ic.TryGetComponent(out SpriteRenderer isr)) isr.color = edge;
        }
        else MacUi.PlaceBanner(transform, Vector2.zero, new Vector2(wPx, hPx), icon, title, null, FxKit.OrderToast);

        _srs = GetComponentsInChildren<SpriteRenderer>();
        _srAlpha = new float[_srs.Length];
        for (int i = 0; i < _srs.Length; i++) _srAlpha[i] = _srs[i].color.a;
        _tms = GetComponentsInChildren<TextMesh>();
        _tmBase = new Color[_tms.Length];
        for (int i = 0; i < _tms.Length; i++) _tmBase[i] = FxKit.TextColor(_tms[i]);
        Place();
    }

    void OnDestroy()
    {
        if (_current == this) _current = null;
    }

    void Update()
    {
        _age += Time.unscaledDeltaTime * (Time.timeScale > 0f ? 1f : 0f); // ゲームが止まっている間（ゲームオーバー・暗転）は進めない
        if (_age >= InTime + StayTime + OutTime)
        {
            Destroy(gameObject);
            return;
        }
        Place();

        // プレイヤー・ボスが後ろに重なっている間は、4割まですかす
        float want = SomethingBehind() ? 0.4f : 1f;
        _fade = Mathf.MoveTowards(_fade, want, Time.unscaledDeltaTime * 6f);
        for (int i = 0; i < _srs.Length; i++)
        {
            if (_srs[i] == null) continue;
            Color c = _srs[i].color;
            c.a = _srAlpha[i] * _fade;
            _srs[i].color = c;
        }
        for (int i = 0; i < _tms.Length; i++)
        {
            if (_tms[i] == null) continue;
            Color c = _tmBase[i];
            c.a *= _fade;
            WorldText.SetColor(_tms[i], c);
        }
    }

    /// <summary>画面の右上（ボスのゲージの下）。右からすべり込んで、右へ出ていく</summary>
    void Place()
    {
        Vector2 view = FxKit.ViewSize();
        float restX = view.x * 0.5f - Margin - _width * 0.5f;
        float outX = view.x * 0.5f + _width * 0.5f + 0.2f;
        float y = view.y * 0.5f - TopOffset - Height * 0.5f;
        float x;
        if (_age < InTime)
        {
            float k = _age / InTime;
            x = Mathf.Lerp(outX, restX, 1f - (1f - k) * (1f - k));
        }
        else if (_age < InTime + StayTime) x = restX;
        else
        {
            float k = (_age - InTime - StayTime) / OutTime;
            x = Mathf.Lerp(restX, outX, k * k);
        }
        // ドットにそろえる
        x = Mathf.Round(x / Px) * Px;
        y = Mathf.Round(y / Px) * Px;
        Vector3 p = transform.localPosition;
        transform.localPosition = new Vector3(x, y, p.z);
    }

    bool SomethingBehind()
    {
        Vector2 c = transform.position;
        PlayerController player = _player != null ? _player : (_player = FindObjectOfType<PlayerController>());
        if (player != null)
        {
            Vector2 pp = (Vector2)player.transform.position + Vector2.up * 0.5f;
            if (Mathf.Abs(pp.x - c.x) < _width * 0.5f + 0.4f && Mathf.Abs(pp.y - c.y) < Height * 0.5f + 0.6f) return true;
        }
        if (_boss == null && Time.unscaledTime >= _nextBossFind)
        {
            _nextBossFind = Time.unscaledTime + 0.5f;
            _boss = FindObjectOfType<BossController>();
        }
        if (_boss != null && _boss.isActiveAndEnabled)
        {
            Bounds b = _boss.BodyBounds;
            var mine = new Bounds(c, new Vector3(_width, Height, 10f));
            b.extents = new Vector3(b.extents.x, b.extents.y, 10f);
            if (mine.Intersects(b)) return true;
        }
        return false;
    }

    PlayerController _player;
    BossController _boss;
    float _nextBossFind;
}
