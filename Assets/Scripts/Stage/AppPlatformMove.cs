using UnityEngine;

/// <summary>move：今の場所と (dx, dy) ずらした場所を往復（period 秒で1往復。offset でずらす）</summary>
[DefaultExecutionOrder(-200)]
public class AppPlatformMove : AppPlatform
{
    public override bool IsSteady => false;
    public override bool IsSolidFor(float seconds) => false; // 動くので、決めた場所にはいない

    void FixedUpdate()
    {
        float period = AppStageParams.Pos(P.period, 4f, 0.5f);
        float t = Time.fixedTime - StartFixedTime + AppStageParams.Num(P.offset);
        float s = 0.5f - 0.5f * Mathf.Cos(t / period * Mathf.PI * 2f); // はしでゆっくり
        MoveBody(Home + new Vector2(AppStageParams.Num(P.dx), AppStageParams.Num(P.dy)) * s, true);
    }
}
