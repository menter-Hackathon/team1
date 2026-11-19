using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】One more thing…（Keynote のボスの技 P2。2026-11-19。docs/app-kits-1-design.md 13章）。数値はすべて【仮】。
/// 画面が少し暗くなり、ボスにスポットライト。字 One more thing...（0.8秒＝予告。12方向に赤い細い線）
/// → 白い星 12発（毎秒 7マス）→ 15度ずらして、もう12発（2回目も 0.8秒の線を見せる）。
/// 避け方：線と線の間（ボスから 5マス離れれば、すき間は 2.6マス）。離れるほど楽。
/// </summary>
[CreateAssetMenu(fileName = "Keynote_OneMoreThing", menuName = "Team1/Kits/Keynote One More Thing")]
public class KeynoteOneMoreThingAttack : CustomAttack
{
    [Tooltip("星1つの攻撃データ（種類 Projectile。速さ・大きさ・寿命）")]
    [SerializeField] AttackData _star;
    [SerializeField, Min(2)] int _count = 12;
    [SerializeField, Min(1)] int _volleys = 2;
    [Tooltip("2回目からずらす角度（度）")]
    [SerializeField] float _rotate = 15f;
    [Tooltip("予告（秒。短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("暗くする濃さ（0〜1）")]
    [SerializeField, Range(0f, 1f)] float _dim = 0.45f;

    public override bool CanRun(AttackRunner owner, AttackData data) => _star != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Vector2 origin = owner.OriginPoint;
        float total = _volleys * _telegraph + 0.5f;
        KeynoteDim.Spawn(owner, _dim, total);
        // スポットライト（ボスのうしろに、うすい黄の円）
        OfficeFx spot = OfficeFx.Sprite(owner, FxArt.Disc(48), origin, Vector2.one * 5f, total, 4, OfficeKitTools.Fade(KeynoteArt.Spot, 0.28f), 0f);
        // 字
        OfficeFx text = OfficeFx.Create(owner, "Keynote_OneMoreThing_Text", origin + new Vector2(0f, 2.4f), _telegraph + 0.2f, 0f);
        FxKit.AddText(text.transform, "One more thing...", Vector2.zero, 0.42f, Color.white, 62);

        for (int v = 0; v < _volleys; v++)
        {
            var dirs = OfficeKitTools.RadialDirs(_count, 90f + v * _rotate);
            OfficeKitTools.RadialLines(owner, origin, dirs, 24f, _telegraph, 1.2f);
            float end = Time.time + _telegraph;
            while (Time.time < end)
            {
                if (OfficeKitTools.OwnerStopped(owner, data)) yield break;
                yield return null;
            }
            FxKit.Ring(origin, 2.5f, 0.2f, Color.white);
            OfficeKitTools.RadialFire(owner, _star, origin, dirs, _ => KeynoteArt.Star, v * _count);
        }
        yield return new WaitForSeconds(0.3f);
        if (spot != null) spot.End();
    }
}

/// <summary>画面を少し暗くする（カメラについていく黒い板）。ボスの出した物なので、形態変化・正常化で消える</summary>
public class KeynoteDim : CustomEffect
{
    SpriteRenderer _sr;
    float _alpha, _born, _endAt;

    public static KeynoteDim Spawn(AttackRunner owner, float alpha, float seconds)
    {
        Camera cam = Camera.main;
        Vector2 at = cam != null ? (Vector2)cam.transform.position : owner.OriginPoint;
        GameObject go = owner.CreateSpawnedObject("Keynote_Dim", at);
        var d = go.AddComponent<KeynoteDim>();
        d._alpha = alpha;
        d._born = Time.time;
        d._endAt = Time.time + seconds;
        // 背景とタイルの上、ボス・プレイヤー・弾の下（並びの数は【仮】）
        d._sr = AttackRunner.AddVisual(go.transform, PlaceholderSprites.Square, new Color(0f, 0f, 0f, 0f), FxKit.ViewSize() + Vector2.one * 2f, 3, "Dim");
        return d;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null) transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
        float t = Time.time;
        if (t >= _endAt) { Destroy(gameObject); return; }
        float a = Mathf.Min(1f, (t - _born) / 0.2f) * Mathf.Min(1f, (_endAt - t) / 0.3f);
        if (_sr != null) _sr.color = new Color(0f, 0f, 0f, _alpha * a);
    }
}
