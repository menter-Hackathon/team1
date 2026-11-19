using UnityEngine;

/// <summary>
/// 【仮】画面共有の枠（2026-11-19。Teams のボスの技）。赤い点線（予告）→ 実線と中の判定 → うすくなって消える。
/// 攻撃する人の出した物。CustomEffect なので、形態変化・正常化で消える
/// </summary>
public class TeamsShareFrame : CustomEffect
{
    const int Order = 17;

    Hitbox _hitbox;
    SpriteRenderer _dotted, _solid, _fill;
    Transform _tag;
    float _hitAt, _endAt, _goneAt;
    bool _hitOn, _hitDone;

    public static TeamsShareFrame Spawn(AttackRunner owner, AttackData data, Vector2 center, Vector2 size, float telegraph, float active)
    {
        if (owner == null || data == null) return null;
        GameObject go = owner.CreateSpawnedObject("Teams_ShareFrame", center);
        var f = go.AddComponent<TeamsShareFrame>();
        f._hitAt = Time.time + telegraph;
        f._endAt = f._hitAt + active;
        f._goneAt = f._endAt + 0.25f;
        var box = TalkKitUtil.AddTrigger<BoxCollider2D>(go);
        box.size = size;
        f._hitbox = owner.AddHitbox(go, data, box);
        int w = Mathf.RoundToInt(size.x * 18f), h = Mathf.RoundToInt(size.y * 18f);
        f._fill = TalkArt.Show(go.transform, PlaceholderSprites.Square, Vector2.zero, size, TalkArt.Fade(TalkArt.Red, 0.1f), Order, "Fill");
        f._dotted = TalkArt.ShowNative(go.transform, TalkArt.Frame(w, h, 3, true), Vector2.zero, Order + 1, 1f, "Dotted");
        f._dotted.color = (Color)TeamsArt.ShareRed;
        f._solid = TalkArt.ShowNative(go.transform, TalkArt.Frame(w, h, 3, false), Vector2.zero, Order + 1, 1f, "Solid");
        f._solid.color = (Color)TeamsArt.ShareRed;
        f._solid.enabled = false;
        // 枠の上の札（共有中の印。英語の短い言葉）
        f._tag = TalkArt.Child(go.transform, "Tag", center + new Vector2(0f, size.y * 0.5f + 0.35f));
        var pill = new TalkCanvas(64, 14);
        pill.Round(0, 0, 64, 14, 4, TeamsArt.ShareRed);
        TalkArt.ShowNative(f._tag, pill.ToSprite("Teams_SharePill14"), Vector2.zero, Order + 2, 1f, "Pill");
        TalkArt.Text(f._tag, "Sharing", Vector2.zero, 0.4f, Color.white, Order + 3);
        return f;
    }

    void Update()
    {
        float now = Time.time;
        if (now < _hitAt)
        {
            float left = _hitAt - now;
            _dotted.enabled = left > 0.3f || Mathf.FloorToInt(now / 0.08f) % 2 == 0;
            return;
        }
        if (!_hitOn && !_hitDone)
        {
            _hitOn = true;
            _dotted.enabled = false;
            _solid.enabled = true;
            _fill.color = TalkArt.Fade(TalkArt.Red, 0.35f);
            _hitbox.Activate();
        }
        if (_hitOn && now >= _endAt)
        {
            _hitOn = false;
            _hitDone = true;
            _hitbox.Deactivate();
        }
        if (_hitDone)
        {
            float k = Mathf.Clamp01((_goneAt - now) / 0.25f);
            _fill.color = TalkArt.Fade(TalkArt.Red, 0.25f * k);
            _solid.color = TalkArt.Fade((Color)TeamsArt.ShareRed, k);
            if (now >= _goneAt) Destroy(gameObject);
        }
    }
}
