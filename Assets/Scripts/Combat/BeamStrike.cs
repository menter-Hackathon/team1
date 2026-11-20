using System.Collections;
using UnityEngine;

/// <summary>
/// 直線のレーザー（2026-11-19。AI チャットのボス。数値は攻撃データ、見た目は【仮】）。AttackRunner が AttackData（種類 Beam）から作る。
/// 流れ：予告（予告時間）→ レーザー（判定時間。だんだん太くなって、いちばん太い所で少し止まり、細くなって消える）。
/// ・位置と向きは出した時に決まり、あとから変わらない（プレイヤーは予告を見て、安全な所へ動けば避けられる）
/// ・判定はレーザーの太さに合わせる。「判定を出す太さの割合」より細い間は当たらない。1本のレーザーで当たるのは1回まで
/// ・見せ方 Laser：予告は通り道を流れる電流（予告のコマを横に並べる）
/// ・見せ方 CodeStream（大技）：予告は赤い半透明の危険エリア。レーザーの中を大量のコードの文字列が左から右へ流れる
/// 出したあとは自分で進むので、攻撃した人が攻撃を止めても最後まで残る（正常化のときは ClearSpawned で消える）
/// </summary>
public class BeamStrike : MonoBehaviour
{
    static readonly Color DangerColor = new Color(1f, 0.1f, 0.12f, 0.22f);
    static readonly Color DangerEdgeColor = new Color(1f, 0.25f, 0.25f, 0.85f);

    /// <summary>太くなる・いちばん太い・細くなる の時間の割合</summary>
    const float GrowPart = 0.35f, HoldPart = 0.3f;

    AttackData _data;
    Hitbox _hitbox;
    BoxCollider2D _collider;
    SpriteRenderer _beam;
    SpriteRenderer _telegraph;
    SpriteRenderer _edgeTop, _edgeBottom;
    Sprite[] _telegraphFrames;
    float _length;
    float _width;          // 今の太さ
    float _activeStart = -1f;
    bool _hitOn;

    public AttackData Data => _data;
    /// <summary>予告中なら true</summary>
    public bool IsTelegraphing { get; private set; }
    /// <summary>判定を出している間 true</summary>
    public bool IsHitActive => _hitOn;
    /// <summary>今の太さ（マス）</summary>
    public float CurrentWidth => _width;

    /// <summary>レーザーを1本出す。origin から dir の向きに length の長さ</summary>
    public static BeamStrike Spawn(AttackRunner owner, AttackData data, Vector2 origin, Vector2 dir, float length)
    {
        GameObject go = owner.CreateSpawnedObject($"Beam_{data.name}", origin);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;

        var strike = go.AddComponent<BeamStrike>();
        strike._data = data;
        strike._length = length;
        strike._collider = go.AddComponent<BoxCollider2D>();
        strike._collider.isTrigger = true;
        strike._collider.size = new Vector2(length, 0.05f);
        strike._collider.offset = new Vector2(length * 0.5f, 0f);
        strike._hitbox = owner.AddHitbox(go, data, strike._collider);
        strike.BuildVisuals();
        strike.StartCoroutine(strike.Run());
        return strike;
    }

    static Sprite Pixel => GameArt.Instance != null && GameArt.Instance.Pixel != null ? GameArt.Instance.Pixel : PlaceholderSprites.Square;

    bool IsCodeStream => _data.BeamStyle == BeamStyle.CodeStream;

    void BuildVisuals()
    {
        // 大技のレーザーはボス（10〜13）・プレイヤー（15）の後ろ、ふつうのレーザーは手前
        int beamOrder = IsCodeStream ? 8 : 19;
        _beam = AttackRunner.AddVisual(transform, _data.Sprite != null ? _data.Sprite : Pixel, _data.Color, new Vector2(_length, 0.1f), beamOrder, "BeamVisual");
        _beam.transform.localPosition = new Vector3(_length * 0.5f, 0f, 0f);
        _beam.enabled = false;

        if (IsCodeStream)
        {
            // 赤い半透明の危険エリア（レーザーと同じ太さ）と、上下の縁
            _telegraph = AttackRunner.AddVisual(transform, Pixel, DangerColor, new Vector2(_length, _data.BeamWidth), 7, "Danger");
            _telegraph.transform.localPosition = new Vector3(_length * 0.5f, 0f, 0f);
            float edge = 2f / GameArt.PixelsPerUnit;
            _edgeTop = AttackRunner.AddVisual(transform, Pixel, DangerEdgeColor, new Vector2(_length, edge), 7, "DangerEdgeTop");
            _edgeTop.transform.localPosition = new Vector3(_length * 0.5f, _data.BeamWidth * 0.5f, 0f);
            _edgeBottom = AttackRunner.AddVisual(transform, Pixel, DangerEdgeColor, new Vector2(_length, edge), 7, "DangerEdgeBottom");
            _edgeBottom.transform.localPosition = new Vector3(_length * 0.5f, -_data.BeamWidth * 0.5f, 0f);
        }
        else
        {
            // 通り道を流れる電流（コマを横に並べる）。コマが無ければ細い線
            _telegraphFrames = _data.TelegraphFrames;
            if (_telegraphFrames.Length > 0 && _telegraphFrames[0] != null)
            {
                var go = new GameObject("Spark");
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(_length * 0.5f, 0f, 0f);
                _telegraph = go.AddComponent<SpriteRenderer>();
                _telegraph.sprite = _telegraphFrames[0];
                _telegraph.drawMode = SpriteDrawMode.Tiled;
                _telegraph.tileMode = SpriteTileMode.Continuous;
                _telegraph.size = new Vector2(_length, _telegraphFrames[0].bounds.size.y);
                _telegraph.sortingOrder = 18;
            }
            else
            {
                Color c = _data.Color;
                c.a *= 0.6f;
                _telegraph = AttackRunner.AddVisual(transform, Pixel, c, new Vector2(_length, 2f / GameArt.PixelsPerUnit), 18, "Line");
                _telegraph.transform.localPosition = new Vector3(_length * 0.5f, 0f, 0f);
            }
        }
        SetTelegraphVisible(false);
    }

    void SetTelegraphVisible(bool on)
    {
        if (_telegraph != null) _telegraph.enabled = on;
        if (_edgeTop != null) _edgeTop.enabled = on;
        if (_edgeBottom != null) _edgeBottom.enabled = on;
    }

    IEnumerator Run()
    {
        // ── 予告 ──
        IsTelegraphing = true;
        SetTelegraphVisible(true);
        float end = Time.time + _data.TelegraphTime;
        while (Time.time < end)
        {
            float remain = end - Time.time;
            if (IsCodeStream)
            {
                // 赤いエリアはゆっくり明滅し、最後の0.5秒は速く点滅する
                float pulse = remain > 0.5f ? 0.75f + 0.25f * Mathf.Sin(Time.time * 6f) : (Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0 ? 1f : 0.4f);
                Color c = DangerColor;
                c.a *= pulse;
                _telegraph.color = c;
                Color e = DangerEdgeColor;
                e.a *= pulse;
                _edgeTop.color = _edgeBottom.color = e;
            }
            else if (_telegraphFrames != null && _telegraphFrames.Length > 0)
            {
                // 電流が流れる（コマを送る）。最後の0.3秒は点滅
                _telegraph.sprite = _telegraphFrames[Mathf.FloorToInt(Time.time * 18f) % _telegraphFrames.Length];
                _telegraph.enabled = remain > 0.3f || Mathf.FloorToInt(Time.time / 0.06f) % 2 == 0;
            }
            else
            {
                _telegraph.enabled = remain > 0.3f || Mathf.FloorToInt(Time.time / 0.06f) % 2 == 0;
            }
            yield return null;
        }
        IsTelegraphing = false;
        SetTelegraphVisible(false);

        // ── レーザー（太くなって、少し止まり、細くなる） ──
        _beam.enabled = true;
        _activeStart = Time.time;
        GameAudio.Event(IsCodeStream ? "gm_aichat_codestream_fire" : "gm_aichat_beam_fire");
        if (IsCodeStream) _loop = SeLoop.Play("gm_aichat_codestream_loop"); // 大技：レーザーが消えるまで、うなりを鳴らし続ける
        float duration = Mathf.Max(0.05f, _data.ActiveTime);
        while (Time.time - _activeStart < duration)
        {
            float t = (Time.time - _activeStart) / duration;
            float k = t < GrowPart ? EaseOut(t / GrowPart)
                : t < GrowPart + HoldPart ? 1f
                : 1f - EaseIn((t - GrowPart - HoldPart) / (1f - GrowPart - HoldPart));
            SetWidth(_data.BeamWidth * k);
            // 判定は、ある程度太い間だけ出す。1本のレーザーで当たるのは1回まで（Activate は最初に太くなったときだけ）
            bool on = k >= _data.BeamHitThreshold;
            if (on && !_hitOn && !_hitbox.IsActive) _hitbox.Activate();
            if (!on && _hitOn) _hitbox.Deactivate();
            _hitOn = on;
            yield return null;
        }
        _hitbox.Deactivate();
        _hitOn = false;
        StopLoop();
        Destroy(gameObject);
    }

    SeLoop _loop;

    void StopLoop()
    {
        if (_loop != null) _loop.Stop(0.3f);
        _loop = null;
    }

    void OnDestroy() => StopLoop(); // 技が途中で止められた（ボスの形態変化・正常化）ときも止める

    static float EaseOut(float x) => 1f - (1f - x) * (1f - x);
    static float EaseIn(float x) => x * x;

    void SetWidth(float w)
    {
        _width = Mathf.Max(0.01f, w);
        _collider.size = new Vector2(_length, _width);
        // ふつうのレーザーは、ゆらめき（0.04秒ごとに明るさを切り替える）
        Color c = _data.Color;
        if (!IsCodeStream && Mathf.FloorToInt(Time.time / 0.04f) % 2 == 1) c = new Color(c.r * 0.85f, c.g * 0.85f, c.b * 0.9f, c.a);
        _beam.color = c;
        Vector2 native = _beam.sprite.bounds.size;
        _beam.transform.localScale = new Vector3(_length / Mathf.Max(native.x, 0.0001f), _width / Mathf.Max(native.y, 0.0001f), 1f);
    }

    // ───────── 大技：レーザーの中を流れるコード ─────────

    static readonly string[] CodeLines =
    {
        "def analyze(user):", "  pos = user.position", "  return predict(pos)", "for i in range(9999):", "  laser.fire(target)",
        "import neural_net as nn", "model.generate(prompt)", "if (hp < 50) mode = 'ultra';", "while (true) { think(); }",
        "tokens += 4096", "attn = softmax(Q @ K.T)", "loss.backward()", "optimizer.step()", "0x7f3a 0x9c21 0x00ff 0xdead",
        "print('Hello, user!')", "const answer = await llm(q);", "self.attention(x, mask)", "return {\"result\": True}",
        "SELECT * FROM players;", "git push --force", "sudo rm -rf /virus", "<think> ... </think>", "beam.width *= 2",
    };

    /// <summary>流れる速さ（マス/秒）。2026-11-19 に 5 から速くした</summary>
    const float CodeSpeed = 13f;

    void OnGUI()
    {
        if (!IsCodeStream || _activeStart < 0f || _beam == null || !_beam.enabled) return;
        Camera cam = Camera.main;
        if (cam == null || PixelGui.FontEn == null) return;
        float ratio = Mathf.Clamp01(_width / Mathf.Max(0.01f, _data.BeamWidth));
        if (ratio < 0.15f) return;

        PixelGui.Begin(false);
        float scale = PixelGui.Scale;
        // レーザーの帯（横向きの大技だけ。ワールド → 画面の座標）
        Vector3 a = cam.WorldToScreenPoint(transform.TransformPoint(new Vector3(0f, _width * 0.5f, 0f)));
        Vector3 b = cam.WorldToScreenPoint(transform.TransformPoint(new Vector3(_length, -_width * 0.5f, 0f)));
        var band = Rect.MinMaxRect(Mathf.Min(a.x, b.x) / scale, (Screen.height - Mathf.Max(a.y, b.y)) / scale,
                                   Mathf.Max(a.x, b.x) / scale, (Screen.height - Mathf.Min(a.y, b.y)) / scale);
        float unitPx = Mathf.Abs(cam.WorldToScreenPoint(transform.position + Vector3.right).x - cam.WorldToScreenPoint(transform.position).x) / scale;
        float elapsed = Time.time - _activeStart;
        // 文字の先頭は左から流れ込んでくる
        float front = band.x + elapsed * CodeSpeed * unitPx;

        GUI.BeginGroup(band);
        const float lineH = 10f;
        int rows = Mathf.Max(1, Mathf.FloorToInt(band.height / lineH));
        float top = (band.height - rows * lineH) * 0.5f;
        var color = new Color(1f, 1f, 1f, 0.9f * ratio);
        for (int row = 0; row < rows; row++)
        {
            string line = CodeLines[(row * 7 + 3) % CodeLines.Length];
            float lineW = line.Length * 8f + 24f; // Press Start 2P は 8 ドットで1文字
            float speedMul = 0.8f + 0.4f * ((row * 37) % 10) / 10f;
            float offset = (elapsed * CodeSpeed * unitPx * speedMul + row * 53f) % lineW;
            float limit = front - band.x;
            for (float x = -lineW + offset; x < band.width && x < limit; x += lineW)
            {
                PixelGui.TextEn(new Rect(x, top + row * lineH, lineW, lineH), line, 8, color);
            }
        }
        GUI.EndGroup();
        PixelGui.End();
    }
}
