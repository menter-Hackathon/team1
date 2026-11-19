using UnityEngine;

/// <summary>
/// アプリボス（Office・Unity・Steam）の見た目のコマ送り。判定には関わらない。素材は AppBoss/&lt;App&gt;/ の README のとおり。
/// ・Idle（5コマ・8FPS・繰り返し）
/// ・Attack（6コマ・10FPS）：技を出し始めたとき（BossController.MoveStarted）。F3 でスキルが出るように、技の予備動作を 0.2 秒にしておく
/// ・Hit（3コマ・12FPS）：ゲージが増えたとき。攻撃中は割り込まない
/// ・Purify（10コマ・10FPS）：ゲージ100%でウイルスが追い出されたとき（BossController.Ejected）。
///   追い出しを使わないボスは、正常化したとき。最後まで再生したら、正常化後のアイコンに変える
/// 右向きが基準。プレイヤーが左にいるときは flipX で反転する。
/// 色（点滅・形態変化）は BossController が _renderers に入れたこのスプライトに付ける。
/// </summary>
public class AppBossAnimator : MonoBehaviour
{
    public enum Clip { Idle, Attack, Hit, Purify, Normal }

    [SerializeField] BossController _boss;
    [SerializeField] SpriteRenderer _renderer;
    [SerializeField] Sprite[] _idle;
    [SerializeField, Min(0.1f)] float _idleFps = 8f;
    [SerializeField] Sprite[] _attack;
    [SerializeField, Min(0.1f)] float _attackFps = 10f;
    [SerializeField] Sprite[] _hit;
    [SerializeField, Min(0.1f)] float _hitFps = 12f;
    [SerializeField] Sprite[] _purify;
    [SerializeField, Min(0.1f)] float _purifyFps = 10f;
    [Tooltip("正常化したあとのアイコン（AppIcon_<App>_Normal）。空ならボスの設定の「正常化後のアイコン」")]
    [SerializeField] Sprite _normalIcon;

    VirusGauge _gauge;
    AttackRunner _runner;
    float _clipStart;

    public Clip Current { get; private set; } = Clip.Idle;

    void Awake()
    {
        if (_boss == null) _boss = GetComponentInParent<BossController>();
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        if (_boss != null)
        {
            _gauge = _boss.GetComponent<VirusGauge>();
            _runner = _boss.GetComponent<AttackRunner>();
        }
    }

    void OnEnable()
    {
        if (_boss != null)
        {
            _boss.MoveStarted += OnMoveStarted;
            _boss.Ejected += OnPurify;
            _boss.Normalized += OnNormalized;
        }
        if (_gauge != null)
        {
            _gauge.Gained += OnGained;
            _gauge.Changed += OnGaugeChanged;
        }
        Play(Clip.Idle);
    }

    void OnDisable()
    {
        if (_boss != null)
        {
            _boss.MoveStarted -= OnMoveStarted;
            _boss.Ejected -= OnPurify;
            _boss.Normalized -= OnNormalized;
        }
        if (_gauge != null)
        {
            _gauge.Gained -= OnGained;
            _gauge.Changed -= OnGaugeChanged;
        }
    }

    void OnMoveStarted(AttackData move)
    {
        if (Current == Clip.Purify || Current == Clip.Normal) return;
        Play(Clip.Attack);
    }

    void OnGained(DamageInfo info)
    {
        if (Current == Clip.Idle) Play(Clip.Hit); // 攻撃中・正常化中は割り込まない
    }

    void OnPurify() => Play(Clip.Purify);

    void OnNormalized(SkillDefinition reward)
    {
        if (Current != Clip.Purify && Current != Clip.Normal) Play(Clip.Purify);
    }

    void OnGaugeChanged(int current, int max)
    {
        // やり直し（ゲージが 0 に戻った）で、元の姿に戻す
        if (current == 0 && (Current == Clip.Purify || Current == Clip.Normal)) Play(Clip.Idle);
    }

    /// <summary>コマ送りを切り替える</summary>
    public void Play(Clip clip)
    {
        Current = clip;
        _clipStart = Time.time;
        Apply();
    }

    void Update()
    {
        if (_runner != null && _renderer != null && Current != Clip.Normal)
        {
            _renderer.flipX = _runner.Facing < 0;
        }
        Apply();
    }

    void Apply()
    {
        if (_renderer == null) return;
        float t = Time.time - _clipStart;
        switch (Current)
        {
            case Clip.Idle:
                Set(_idle, _idleFps, t, true);
                break;
            case Clip.Attack:
                if (!Set(_attack, _attackFps, t, false)) Play(Clip.Idle);
                break;
            case Clip.Hit:
                if (!Set(_hit, _hitFps, t, false)) Play(Clip.Idle);
                break;
            case Clip.Purify:
                if (!Set(_purify, _purifyFps, t, false)) Play(Clip.Normal);
                break;
            case Clip.Normal:
                Sprite icon = _normalIcon != null ? _normalIcon : _boss != null ? _boss.NormalizedIcon : null;
                if (icon != null) _renderer.sprite = icon;
                _renderer.flipX = false;
                break;
        }
    }

    /// <summary>コマを入れる。1回だけのコマ送りが終わっていたら false</summary>
    bool Set(Sprite[] frames, float fps, float t, bool loop)
    {
        if (frames == null || frames.Length == 0) return loop;
        if (!loop && t >= SpriteFrames.Duration(frames.Length, fps)) return false;
        int i = SpriteFrames.Index(t, fps, frames.Length, loop);
        if (frames[i] != null) _renderer.sprite = frames[i];
        return true;
    }
}
