using UnityEngine;

/// <summary>
/// 【仮】未読が 3 以上の間、ボスの右上に出る赤いバッジ「99+」（2026-11-19。Slack のギミック）。飾りだけ。ボスについて動き、震える
/// </summary>
public class SlackBossBadge : MonoBehaviour
{
    BossController _boss;

    public static SlackBossBadge Create(BossController boss)
    {
        var go = new GameObject("Slack_BossBadge");
        if (boss != null && boss.gameObject.scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, boss.gameObject.scene);
        var b = go.AddComponent<SlackBossBadge>();
        b._boss = boss;
        var bg = new TalkCanvas(30, 16);
        bg.Round(0, 0, 30, 16, 7, SlackArt.Badge, TalkArt.White);
        TalkArt.ShowNative(go.transform, bg.ToSprite("Slack_Badge99"), Vector2.zero, 42, 1.4f, "Bg");
        TalkArt.Text(go.transform, "99+", Vector2.zero, 0.42f, Color.white, 43);
        b.Place();
        return b;
    }

    void Place()
    {
        if (_boss == null) return;
        Bounds body = _boss.BodyBounds;
        float shake = Mathf.Sin(Time.time * 50f) * 0.06f;
        transform.position = new Vector3(body.max.x - 0.1f + shake, body.max.y - 0.05f, 0f);
    }

    void Update()
    {
        if (_boss == null) { Destroy(gameObject); return; }
        Place();
    }
}
