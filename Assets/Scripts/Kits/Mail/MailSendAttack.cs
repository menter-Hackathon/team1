using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】メールのボスの技「送信」（2026-11-19。docs/app-kits-1-design.md 15章 P1-1）。
/// ボスの横に紙飛行機が 3つ並ぶ（0.8秒。それぞれからプレイヤーへ赤い細い線。線はプレイヤーを追い、撃つ少し前に止まる）→
/// 0.35秒おきに 3発（毎秒 10マス）。止まった線の向きに飛ぶ。避け方：歩き続ける。または、ジャンプ。
/// ・弾の数値（速さ・大きさ）は、この技の攻撃データの「弾」の欄。絵は白い紙飛行機
/// </summary>
[CreateAssetMenu(fileName = "Mail_Send", menuName = "Team1/Kits/Mail Send")]
public class MailSendAttack : CustomAttack
{
    [Tooltip("紙飛行機の数")]
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("並んでから 1発目までの予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("撃つ間隔（秒）")]
    [SerializeField, Min(0.05f)] float _interval = 0.35f;
    [Tooltip("撃つ何秒前に、線が止まるか")]
    [SerializeField, Min(0.1f)] float _lockBefore = 0.3f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        var w = owner.GetComponent<IWarpable>();
        Bounds body = w != null ? w.BodyBounds : new Bounds(owner.transform.position, Vector3.one * 2f);
        Vector2 target = owner.TargetPoint ?? (Vector2)body.center;
        int side = target.x >= body.center.x ? 1 : -1;
        var planes = new List<MailAimedPlane>();
        for (int i = 0; i < _count; i++)
        {
            // ボスの横に、縦に並べる
            float dy = (i - (_count - 1) * 0.5f) * 0.75f;
            Vector2 from = new Vector2(side > 0 ? body.max.x + 0.6f : body.min.x - 0.6f, body.center.y + dy);
            float fireAt = Time.time + _telegraph + i * _interval;
            planes.Add(MailAimedPlane.Spawn(owner, data, from, fireAt, fireAt - Mathf.Min(_lockBefore, _telegraph - 0.2f)));
        }
        yield return new WaitForSeconds(_telegraph + (_count - 1) * _interval + 0.1f);
    }
}

/// <summary>並んで狙う紙飛行機（予告の赤い線つき）。時刻が来たら弾を出して消える。形態変化でも消える</summary>
public class MailAimedPlane : CustomEffect
{
    const int PlaneOrder = 25, LineOrder = 17;
    const float LineLength = 24f;
    AttackRunner _owner;
    AttackData _data;
    Vector2 _from, _dir = Vector2.right;
    float _fireAt, _lockAt, _born;
    SpriteRenderer _plane, _line;

    public static MailAimedPlane Spawn(AttackRunner owner, AttackData data, Vector2 from, float fireAt, float lockAt)
    {
        GameObject go = owner.CreateSpawnedObject("Mail_AimedPlane", from);
        var p = go.AddComponent<MailAimedPlane>();
        p._owner = owner;
        p._data = data;
        p._from = from;
        p._fireAt = fireAt;
        p._lockAt = lockAt;
        p._born = Time.time;
        p._plane = MailArt.Put(go.transform, MailArt.Plane, Vector2.zero, new Vector2(1f, 0.67f), Color.white, PlaneOrder, "Plane");
        var lineGo = new GameObject("Line");
        lineGo.transform.SetParent(go.transform, false);
        p._line = AttackRunner.AddVisual(lineGo.transform, PlaceholderSprites.Square, KitDress.Red, new Vector2(LineLength, 2f * KitDress.Px), LineOrder, "Red");
        p.Aim();
        return p;
    }

    void Aim()
    {
        if (Time.time < _lockAt && _owner != null && _owner.TargetPoint is Vector2 t)
        {
            Vector2 d = t - _from;
            if (d.sqrMagnitude > 0.01f) _dir = d.normalized;
        }
        float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        if (_plane != null)
        {
            _plane.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            MailArt.Upright(_plane, _dir);
        }
        if (_line != null)
        {
            Transform lt = _line.transform.parent;
            lt.position = _from + _dir * (LineLength * 0.5f + 0.4f);
            lt.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    void Update()
    {
        // 出るとき：少し浮かび上がる
        float appear = Mathf.Clamp01((Time.time - _born) / 0.15f);
        transform.position = _from + Vector2.down * (1f - appear) * 0.3f;
        Aim();
        if (_line != null)
        {
            bool locked = Time.time >= _lockAt;
            _line.enabled = !locked || Mathf.FloorToInt(Time.time / 0.06f) % 2 == 0;
        }
        if (Time.time < _fireAt) return;
        if (_owner != null && _data != null)
        {
            Projectile p = Projectile.Spawn(_owner, _data, _from, _dir);
            if (p != null)
            {
                KitDress.Swap(p.gameObject, MailArt.Plane);
                MailArt.Upright(p.gameObject, _dir);
            }
            _owner.NotifyReleased(_data);
        }
        Destroy(gameObject);
    }
}
