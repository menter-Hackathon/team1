using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】能力 リーダー表示（Safari を倒すと手に入る。2026-11-19。docs/app-kits-2-design.md 4-4）。1回押し。身を守る能力。
/// 画面の敵の弾を全部消す（KitBullets）。半径 5マスの雑魚に当たる。ボスが半径 5マスにいればゲージ（攻撃データの共通の欄 8）。
/// 画面が 0.3秒、白い紙の色になる（文章の行の灰色の棒）。
/// </summary>
[CreateAssetMenu(fileName = "Safari_Reader", menuName = "Team1/Kits/Safari/Reader")]
public class SafariReaderAttack : CustomAttack
{
    [Tooltip("【仮】当たる半径（マス）")]
    [SerializeField, Min(0.5f)] float _radius = 5f;
    [Tooltip("【仮】判定の時間（秒）")]
    [SerializeField, Min(0.05f)] float _hitTime = 0.15f;
    [Tooltip("【仮】紙の色の秒数")]
    [SerializeField, Min(0f)] float _flash = 0.3f;

    public override bool CanRun(AttackRunner owner, AttackData data) => owner != null;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        try { Fire(owner, data); }
        catch (System.Exception e) { Debug.LogException(e); }
        owner.NotifyReleased(data);
        yield break;
    }

    void Fire(AttackRunner owner, AttackData data)
    {
        Vector2 c = RewardKit.BodyCenter(owner.transform);
        Rect view = ScreenUi.ViewRect();
        KitBullets.ClearEnemyProjectiles(view.center, Mathf.Max(view.width, view.height));

        // 半径の判定（円）
        GameObject go = owner.CreateSpawnedObject("SafariReader", c);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = _radius;
        Hitbox hit = owner.AddHitbox(go, data, col);
        hit.Activate();
        Destroy(go, _hitTime);

        // 見た目：画面が白い紙の色になる（カメラの子）
        Transform cam = FxKit.CameraChild("SafariReaderPaper");
        if (cam != null)
        {
            Vector2 v = FxKit.ViewSize();
            var fx = cam.gameObject.AddComponent<FxAnim>();
            SafariArt.Box(cam, Vector2.zero, v + Vector2.one, SafariArt.A(SafariArt.Paper, 0.75f), 55, "Paper");
            // 文章の行（灰色の棒）
            for (int i = 0; i < 7; i++)
            {
                float w = i % 3 == 2 ? 6f : 9f;
                SafariArt.Box(cam, new Vector2(-4.5f + w * 0.5f, 3f - i * 0.9f), new Vector2(w, 0.18f), SafariArt.A(MacUi.Gray1, 0.7f), 56, "Line");
            }
            SafariArt.Box(cam, new Vector2(-1.5f, 4.1f), new Vector2(6f, 0.35f), SafariArt.A(MacUi.Gray5, 0.8f), 56, "Title");
            SetLife(fx, _flash);
        }
        FxKit.Ring(c, _radius * 2f, 0.25f, Color.white);
        FxKit.Ring(c, _radius * 1.2f, 0.2f, SafariArt.Blue, 0.05f);
    }

    static void SetLife(FxAnim fx, float life)
    {
        // FxAnim は Create で寿命を決める作りなので、ここではうすくなる所だけ決めて、自分で消す
        fx.FadeFrom(0.3f).Refresh();
        Destroy(fx.gameObject, Mathf.Max(0.05f, life));
    }
}
