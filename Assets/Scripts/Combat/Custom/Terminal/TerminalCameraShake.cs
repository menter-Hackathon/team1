using UnityEngine;

/// <summary>
/// 【仮】画面のゆれ（2026-11-18：Terminal の再起動・南京錠が閉まる瞬間。docs/app-boss-skills-design.md 4-2・4-4）。
/// StageCamera の無いカメラ（1画面のステージ）用。メインカメラに付けて、LateUpdate の最後にカメラを少しずらし、次のフレームの Update で元に戻す。
/// カメラを動かす部品（StageCamera など）は、ずらす前の位置を見て動くので、ゆれが残らない
/// </summary>
[DefaultExecutionOrder(10000)]
public class TerminalCameraShake : MonoBehaviour
{
    Vector3 _applied;
    float _start;
    float _duration;
    float _amplitude;

    /// <summary>
    /// 画面をゆらす。amplitude はマス。今のゆれより強ければ差し替える。
    /// カメラに StageCamera が付いていれば、そちらの入口（StageCamera.Shake）を使う。
    /// 1画面のステージ（Stage_Desktop。Terminal のステージ）のカメラには StageCamera が無いので、そのときだけ自分でゆらす（2つ重ねてゆらさない）
    /// </summary>
    public static void Shake(float duration, float amplitude)
    {
        Camera cam = Camera.main;
        if (cam == null || duration <= 0f || amplitude <= 0f) return;
        if (cam.TryGetComponent(out StageCamera _))
        {
            StageCamera.Shake(amplitude, duration);
            return;
        }
        if (!cam.TryGetComponent(out TerminalCameraShake shake)) shake = cam.gameObject.AddComponent<TerminalCameraShake>();
        shake.Begin(duration, amplitude);
    }

    void Begin(float duration, float amplitude)
    {
        if (amplitude < CurrentAmplitude()) return;
        _start = Time.time;
        _duration = duration;
        _amplitude = amplitude;
    }

    float CurrentAmplitude()
    {
        if (_duration <= 0f) return 0f;
        float k = 1f - Mathf.Clamp01((Time.time - _start) / _duration);
        return _amplitude * k;
    }

    void Update() => RemoveOffset();

    void LateUpdate()
    {
        RemoveOffset();
        float a = CurrentAmplitude();
        if (a <= 0.001f) return;
        Vector2 o = Random.insideUnitCircle * a;
        _applied = new Vector3(o.x, o.y, 0f);
        transform.position += _applied;
    }

    void OnDisable() => RemoveOffset();

    void RemoveOffset()
    {
        if (_applied == Vector3.zero) return;
        transform.position -= _applied;
        _applied = Vector3.zero;
    }
}
