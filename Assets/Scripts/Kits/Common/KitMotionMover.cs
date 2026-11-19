using UnityEngine;

/// <summary>
/// 【仮】道すじ（KitPath）に沿って動く物（2026-11-19。KitMotion の EdgeSweep・Diagonal・Orbit・Bounce・FloorWave・WallCrawl・Pop の弾）。担当0 の物。
/// ・予告（Warn 秒）の間は、道の最初の場所でうすく点滅する（ghost のとき。false なら隠しておく）。判定は切ってある
/// ・そのあと道に沿って動き、道の終わりで消える。判定は動いている間だけ
/// </summary>
public class KitMotionMover : KitMotionItem
{
    KitPath _path;
    bool _ghost;
    bool _moving;

    /// <summary>道すじ</summary>
    public KitPath Path => _path;
    /// <summary>動き出してからの秒数（予告の間は 0）</summary>
    public float MoveTime => Mathf.Max(0f, Age - Warn);

    internal void Setup(KitPath path, float warn, bool ghost)
    {
        _path = path ?? KitPath.Stay(transform.position, 0f);
        Warn = Mathf.Max(0f, warn);
        _ghost = ghost;
        transform.position = _path.At(0f);
        if (Warn > 0f)
        {
            if (Visual != null) Visual.enabled = _ghost;
            Ghost(_ghost);
        }
        else Begin();
    }

    void Begin()
    {
        _moving = true;
        if (Visual != null) Visual.enabled = true;
        Ghost(false);
        SetHarm(true);
    }

    protected override bool Step(float age)
    {
        if (_path == null) return false;
        if (age < Warn)
        {
            transform.position = _path.At(0f); // 動く中心（Orbit）なら、予告の間も付いていく
            if (_ghost) Ghost(true);
            return true;
        }
        if (!_moving) Begin();
        float t = age - Warn;
        transform.position = _path.At(t);
        return t < _path.Duration;
    }
}
