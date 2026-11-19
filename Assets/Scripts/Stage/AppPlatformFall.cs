using UnityEngine;

/// <summary>fall：プレイヤーが乗ると delay 秒ゆれて落ちる（消える）→ respawn 秒で元の場所に戻る</summary>
public class AppPlatformFall : AppPlatform
{
    const float DropTime = 0.35f;
    const float ShakePx = 1f / 15f;
    enum State { Idle, Shaking, Fallen }
    State _state;
    float _until, _droppedAt;
    Vector3 _artHome;

    public override bool IsSteady => false;
    public override bool IsSolidFor(float seconds) => _state == State.Idle;

    protected override void OnSetup()
    {
        if (Art != null) _artHome = Art.transform.localPosition;
    }

    void Update()
    {
        switch (_state)
        {
            case State.Idle:
                if (PlayerStandsOn())
                {
                    _state = State.Shaking;
                    _until = Time.time + AppStageParams.Pos(P.delay, 0.8f);
                }
                break;
            case State.Shaking:
                if (Art != null) Art.transform.localPosition = _artHome + Vector3.right * (Mathf.FloorToInt(Time.time / 0.05f) % 2 == 0 ? ShakePx : -ShakePx);
                if (Time.time >= _until)
                {
                    _state = State.Fallen;
                    _droppedAt = Time.time;
                    _until = Time.time + AppStageParams.Pos(P.respawn, 3f, 0.5f);
                    SetSolid(false);
                    GameAudio.Event("ui_plat_fall");
                }
                break;
            case State.Fallen:
                float k = Mathf.Clamp01((Time.time - _droppedAt) / DropTime);
                if (Art != null) Art.transform.localPosition = _artHome + Vector3.down * (k * k * 1.2f);
                SetAlpha(k < 1f ? 1f - k : GhostAlpha * 0.6f); // 落ちたあとは、戻る場所にうすい形だけ
                if (k >= 1f && Art != null) Art.transform.localPosition = _artHome;
                if (Time.time >= _until)
                {
                    _state = State.Idle;
                    if (Art != null) Art.transform.localPosition = _artHome;
                    SetAlpha(1f);
                    SetSolid(true);
                }
                break;
        }
    }
}
