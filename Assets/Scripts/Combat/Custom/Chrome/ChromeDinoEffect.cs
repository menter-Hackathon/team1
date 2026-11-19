using UnityEngine;

/// <summary>
/// 【仮】恐竜ランの恐竜（2026-11-19。docs/app-feedback-v2-design.md 2-4）。ChromeDinoRun が出す。
/// 床の上を走り、触れた相手に1回ずつ当たる（Hitbox を1回だけ出しっぱなし＝同じ相手には1回）。
/// 床の高さは毎フレーム真下を調べて合わせる（段差は上り下りする。穴の上はそのままの高さで走り抜ける）。
/// 壁（地形）に当たったら、目が × になって止まり、0.3秒で消える。走り切ったら、うすくなって消える
/// </summary>
public class ChromeDinoEffect : MonoBehaviour
{
    const float FrameTime = 0.08f;
    const float WallStopTime = 0.3f;
    const float FadeTime = 0.2f;
    const float AppearTime = 0.08f;
    const int Order = 22;

    AttackRunner _owner;
    ChromeDinoRun _cfg;
    Rigidbody2D _rb;
    Hitbox _hitbox;
    SpriteRenderer _art;
    Sprite[] _frames;
    Sprite _dead;
    Transform _artRoot;
    int _dir;
    float _born;
    float _ran;
    float _groundY;
    float _jumpAt = -1f;
    bool _jumped;
    float _stopAt = -1f;
    float _fadeAt = -1f;
    float _nextDust;

    public static ChromeDinoEffect Spawn(AttackRunner owner, AttackData data, ChromeDinoRun cfg, Vector2 feet, int dir)
    {
        if (owner == null || data == null || cfg == null) return null;
        GameObject go = owner.CreateSpawnedObject("ChromeDino", feet);
        var fx = go.AddComponent<ChromeDinoEffect>();
        fx._owner = owner;
        fx._cfg = cfg;
        fx._dir = dir >= 0 ? 1 : -1;
        fx._groundY = feet.y;
        fx._born = Time.time;
        fx.Build(data);
        return fx;
    }

    void Build(AttackData data)
    {
        Vector2 size = _cfg.Size;
        _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.useFullKinematicContacts = true;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // 判定（恐竜の大きさ。足元が原点）
        var box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;
        box.offset = new Vector2(0f, size.y * 0.5f);
        _hitbox = _owner.AddHitbox(gameObject, data, box, true);
        _hitbox.Activate(); // 1回だけ出す：同じ相手には1回だけ当たる（ボスのゲージは +12 が1回）

        // 絵（右向きの絵。左へ走るときは返す）
        _frames = new[] { ChromeArt.Dino(0, Color.white), ChromeArt.Dino(1, Color.white) };
        _dead = ChromeArt.Dino(3, Color.white);
        _artRoot = BrowserDevFx.Child(transform, "Art", new Vector2(0f, size.y * 0.5f));
        _art = BrowserDevFx.Show(_artRoot, _frames[0], Vector2.zero, Order, 1f, "Dino");
        _art.flipX = _dir < 0;

        // 現れるとき：足元に地面の線が 2マスぶん「スッ」と引かれる
        GameObject line = BrowserDevFx.Loose("DinoGround", new Vector2(transform.position.x - (_dir > 0 ? 0.6f : 1.4f), _groundY));
        Transform lineRoot = ChromeArt.GroundLine(line.transform, Vector2.zero, 2f, Order - 1);
        BrowserDevFx.Animate(line, 0.35f, (e, k) =>
        {
            float grow = Mathf.Clamp01(e / 0.1f);
            lineRoot.localScale = new Vector3(grow, 1f, 1f);
            if (_dir < 0) lineRoot.localPosition = new Vector3(2f * (1f - grow), 0f, 0f);
            foreach (var sr in lineRoot.GetComponentsInChildren<SpriteRenderer>()) sr.color = BrowserDevFx.WithAlpha(ChromeArt.Grey, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
        });
        SkinDresser.Adopt(new DressInfo { Owner = _owner, Data = data, Target = gameObject }, line);
    }

    void FixedUpdate()
    {
        if (_rb == null) return;
        float dt = Time.fixedDeltaTime;
        Vector2 p = _rb.position;
        if (_stopAt >= 0f || _fadeAt >= 0f)
        {
            _rb.MovePosition(p);
            return;
        }

        float step = _cfg.Speed * dt;
        // 壁（地形）：体の高さで、前を調べる
        if (HitsWall(p, step))
        {
            Stop();
            return;
        }
        float x = p.x + _dir * step;
        _ran += step;

        // 床の高さ（穴の上は今の高さのまま）
        if (FindGround(x, _groundY, out float gy)) _groundY = gy;

        // 前方の高い所にいる相手を見て、1回だけ跳ぶ
        if (!_jumped && SeesHighTarget(new Vector2(x, _groundY)))
        {
            _jumped = true;
            _jumpAt = Time.time;
        }
        float lift = 0f;
        if (_jumpAt >= 0f)
        {
            float t = (Time.time - _jumpAt) / _cfg.JumpTime;
            if (t < 1f) lift = 4f * _cfg.JumpHeight * t * (1f - t);
            else _jumpAt = -1f;
        }
        _rb.MovePosition(new Vector2(x, _groundY + lift));

        if (_ran >= _cfg.Distance) BeginFade();
    }

    void Update()
    {
        if (_art == null) return;
        float now = Time.time;
        float e = now - _born;
        if (_stopAt >= 0f)
        {
            // ゲームオーバーの顔で止まる → 消える
            _art.sprite = _dead;
            float k = (now - _stopAt) / WallStopTime;
            if (k >= 1f) Destroy(gameObject);
            else if (k > 0.7f) _art.color = new Color(1f, 1f, 1f, 1f - (k - 0.7f) / 0.3f);
            return;
        }
        if (_fadeAt >= 0f)
        {
            float k = (now - _fadeAt) / FadeTime;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            _art.color = new Color(1f, 1f, 1f, 1f - k);
        }
        // 足を 0.08秒ごとに入れ替え（跳んでいる間は止める）
        _art.sprite = _jumpAt >= 0f ? _frames[0] : _frames[Mathf.FloorToInt(e / FrameTime) % 2];
        // 現れるとき：後ろから走りこむ（0.08秒）
        float inK = Mathf.Clamp01(e / AppearTime);
        _artRoot.localPosition = new Vector3(-_dir * 0.6f * (1f - inK), _cfg.Size.y * 0.5f, 0f);
        if (_fadeAt < 0f) _art.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.3f, 1f, inK));

        // 後ろに小さい土けむり（灰色の点 2〜3個。床にいる間だけ）
        if (now >= _nextDust && _jumpAt < 0f && _fadeAt < 0f)
        {
            _nextDust = now + 0.1f;
            Sprite dot = BrowserDevFx.Square(2);
            Vector2 at = new Vector2(transform.position.x - _dir * 0.5f, _groundY + 0.08f);
            BrowserDevFx.Burst(at, new Vector2(-_dir, 0.6f), Random.Range(2, 4), i => dot, i => BrowserDevFx.WithAlpha(ChromeArt.Grey, 0.8f), Order - 1, 30f, 1.5f, 3f, 0.15f, 0.25f, 4f, false);
        }
    }

    void Stop()
    {
        if (_stopAt >= 0f) return;
        _stopAt = Time.time;
        if (_hitbox != null) _hitbox.Deactivate();
    }

    void BeginFade()
    {
        if (_fadeAt >= 0f) return;
        _fadeAt = Time.time;
        if (_hitbox != null) _hitbox.Deactivate();
    }

    bool HitsWall(Vector2 p, float step)
    {
        if (_owner == null) return false;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(_owner.SolidMask);
        var hits = new RaycastHit2D[4];
        float h = _cfg.Size.y;
        // 体の中ほどと、上のほう（低い段差は床の高さ合わせで上る）
        for (int i = 0; i < 2; i++)
        {
            Vector2 from = new Vector2(p.x, p.y + h * (i == 0 ? 0.55f : 0.9f));
            int n = Physics2D.Raycast(from, new Vector2(_dir, 0f), filter, hits, _cfg.Size.x * 0.5f + step);
            for (int j = 0; j < n; j++)
            {
                if (hits[j].distance <= 0f) continue;
                if (hits[j].collider.attachedRigidbody == _rb) continue;
                return true;
            }
        }
        return false;
    }

    /// <summary>x の真下の床（今の床から 0.6マス上〜2.5マス下の間）。無ければ false（穴）</summary>
    bool FindGround(float x, float currentY, out float groundY)
    {
        groundY = currentY;
        if (_owner == null) return false;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(_owner.GroundMask);
        var hits = new RaycastHit2D[4];
        int n = Physics2D.Raycast(new Vector2(x, currentY + 0.6f), Vector2.down, filter, hits, 3.1f);
        for (int i = 0; i < n; i++)
        {
            if (hits[i].distance <= 0f) continue;
            if (hits[i].collider.attachedRigidbody == _rb) continue;
            groundY = hits[i].point.y;
            return true;
        }
        return false;
    }

    /// <summary>前方 3マス以内に、床から 1.5マスより高い相手（浮いているボス・とぶ雑魚）がいるか</summary>
    bool SeesHighTarget(Vector2 feet)
    {
        if (_owner == null) return false;
        float look = _cfg.JumpLook;
        Vector2 center = new Vector2(feet.x + _dir * look * 0.5f, feet.y + _cfg.JumpTrigger + 3f);
        Vector2 size = new Vector2(look, 6f);
        var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
        filter.SetLayerMask(_owner.TargetLayers);
        var results = new Collider2D[12];
        int n = Physics2D.OverlapBox(center, size, 0f, filter, results);
        for (int i = 0; i < n; i++)
        {
            if (!results[i].TryGetComponent(out Hurtbox hb)) continue;
            if (hb.Owner == null || hb.Owner.Team == Team.Player) continue;
            if (results[i].bounds.min.y > feet.y + _cfg.JumpTrigger) return true;
        }
        return false;
    }
}
