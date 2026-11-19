using UnityEngine;

/// <summary>
/// 【仮】メールの場面③の出来事「一斉送信」（Reply All。2026-11-19。docs/stage-gimmick-variety-a.md 2章 Mail）。
/// ・封筒が右の壁から左へ、波を打ちながら（振れ幅 1.2マス）飛ぶ。3通を 0.5秒おきに 1列で（強さ 2・3 は 4通・5通）。列は 3回【仮】
///   高さは 床から 1.5 → 3.5 → 1.5（強さ 3 は最後の列が左から）。予告 1.0（壁の矢印と、波の通り道のうすい赤の帯）→ 毎秒 6マス。列の間は 2.8秒
/// ・危ない封筒は迷惑メールの絵（MailArt.SpamEnvelope。紫の「！」・文字化けの帯）。GimmickA.SweepBox
/// ・各列の最後の 1通は、白い封筒に青い帯（ふつうのメール。当たらない。KitMotion.Catch で同じ波の道を飛ぶ）。
///   触ると、全部の能力を 2秒進める（決まりのごほうび＝全回復より小さく）
/// ・決まりの新着メール（MailRule）は、出来事の間は出さない（今のまま）
/// </summary>
public class MailReplyAllEvent : KitTimedEvent
{
    const float Warn = 1.0f, Speed = 6f, Wave = 1.2f, WaveLength = 4f, Gap = 0.5f, LineEvery = 2.8f, Reward = 2f;
    static readonly Vector2 EnvelopeSize = new Vector2(1.0f, 0.69f);
    static readonly float[] Heights = { 1.5f, 3.5f, 1.5f };

    public MailReplyAllEvent() : base("Reply All") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(MailKit.Label, stage => new MailReplyAllEvent());

    protected override void Build()
    {
        int perLine = Level <= 1 ? 3 : Level == 2 ? 4 : 5;
        for (int line = 0; line < Heights.Length; line++)
        {
            float y = Heights[line];
            KitSide side = Level >= 3 && line == Heights.Length - 1 ? KitSide.Left : KitSide.Right;
            At(line * LineEvery, () => Line(side, y, perLine));
        }
        float cross = (View.width + 2f) / Speed;
        EndAt((Heights.Length - 1) * LineEvery + Warn + (perLine - 1) * Gap + cross + 0.2f);
    }

    void Line(KitSide side, float y, int count)
    {
        // 危ない封筒（count − 1 通）
        GimmickA.SweepBox(Owner, side, y, EnvelopeSize, Speed, count - 1, Gap, Wave, Warn, MailArt.SpamEnvelope, null, 1, WaveLength);
        GameAudio.Event("gm_mail_new", 1.15f);
        // 最後の 1通：ふつうのメール（同じ波の道。当たらない・取れる）
        float at = Clock + Warn + (count - 1) * Gap;
        At(at, () =>
        {
            float wy = KitMotion.FloorY() + y;
            KitPath path = KitPath.Sweep(side, wy, Speed, Wave, WaveLength, EnvelopeSize.x * 0.5f + 0.5f, KitMotion.Area);
            KitMotionCatch c = KitMotion.Catch(Owner, path, OnGood, MailArt.Envelope, null, 0.9f);
            if (c != null && c.Visual != null)
            {
                Vector3 s = c.Visual.transform.localScale;
                c.Visual.transform.localScale = new Vector3(s.x, s.y * EnvelopeSize.y / EnvelopeSize.x, 1f); // 封筒の形（横長）
            }
        });
    }

    static void OnGood()
    {
        KitPlayer.AdvanceCooldowns(Reward);
        GameAudio.Event("gm_mail_open_good");
    }
}
