using UnityEngine;

/// <summary>
/// 鳴らし続ける SE（2026-11-20：AI チャットのボスの大技のレーザーなど）。Play で鳴らし始め、Stop を呼ぶまで同じ音をくり返す。
/// ・音は出来事のキー（Maps/*.json の events。gm_…）で決める。GameAudio.TryGetEventClip で読む
/// ・SE は頭と終わりが少しフェードしているので、2つの AudioSource で少し重ねて交互に鳴らし、つなぎ目を消す
/// ・大きさは GameAudio の音量（全体 × SE）に毎フレーム合わせる。時間は実時間（GameAudio と同じ）
/// ・自分だけの GameObject で鳴る（技の物が先に消えても、フェードアウトできる）。Stop(フェード秒) で小さくしてから消える。
/// 　技が途中で止められたときのために、呼んだ側は OnDestroy でも Stop を呼ぶ。シーンが変わると一緒に消える
/// </summary>
public class SeLoop : MonoBehaviour
{
    const float Overlap = 0.08f; // 次の音を、前の音が終わるこの秒数前に始める

    readonly AudioSource[] _src = new AudioSource[2];
    AudioClip _clip;
    float _vol = 1f;
    float _period;
    double _nextAt;
    int _cur;
    float _fadeStart = -1f, _fadeTime;

    /// <summary>鳴らし始める。割り当て・音のファイルが無ければ null（鳴らさない）</summary>
    public static SeLoop Play(string key)
    {
        if (!GameAudio.TryGetEventClip(key, out AudioClip clip, out float vol, out float pitch)) return null;
        var host = new GameObject($"SeLoop_{key}");
        var loop = host.AddComponent<SeLoop>();
        loop._clip = clip;
        loop._vol = vol;
        for (int i = 0; i < 2; i++)
        {
            var s = host.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 0f;
            s.clip = clip;
            s.pitch = pitch;
            loop._src[i] = s;
        }
        loop._period = Mathf.Max(0.1f, clip.length / Mathf.Max(0.1f, pitch) - Overlap);
        loop.ApplyVolume(1f);
        loop._src[0].Play();
        loop._nextAt = Time.unscaledTimeAsDouble + loop._period;
        return loop;
    }

    /// <summary>fade 秒で小さくして止める（0 ならすぐ）</summary>
    public void Stop(float fade = 0.25f)
    {
        if (_fadeStart >= 0f) return;
        if (fade <= 0f) { Remove(); return; }
        _fadeStart = Time.unscaledTime;
        _fadeTime = fade;
    }

    void Update()
    {
        float k = 1f;
        if (_fadeStart >= 0f)
        {
            k = 1f - (Time.unscaledTime - _fadeStart) / _fadeTime;
            if (k <= 0f) { Remove(); return; }
        }
        else if (Time.unscaledTimeAsDouble >= _nextAt)
        {
            // 次の音を、もう1つの AudioSource で鳴らす（前の音の終わりと少し重なる）
            _cur = 1 - _cur;
            _src[_cur].Play();
            _nextAt += _period;
        }
        ApplyVolume(k);
    }

    void ApplyVolume(float k)
    {
        float v = Mathf.Clamp01(_vol * GameAudio.MasterVolume * GameAudio.SeVolume * k);
        foreach (var s in _src) if (s != null) s.volume = v;
    }

    void Remove() => Destroy(gameObject);
}
