using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】メモのボスの技「鍵をかける」（第2形態。2026-11-19。docs/app-kits-1-design.md 14章 P2。担当0。Terminal の chmod の帯が見本）。
/// ・プレイヤーの位置に、黄色い紙の縦の帯（幅 4マス、床から天井まで）と、上に鍵の印（開いている）→ 0.9秒後、鍵が閉まる
/// ・その瞬間に帯の中にいたら、選んでいる能力の枠が 4秒使えない（KitPlayer.Seal。枠に鍵の印）。ダメージは無い
/// ・予告：帯のふちが赤く点滅（0.9秒）。帯は動かない。避け方：帯から歩いて出る（2マス）
/// ・封印の間は場に残る技の効果（ボスは「鍵をかける」をもう一度選ばない。攻撃データの「場に残る技」をオンにする）
/// </summary>
[CreateAssetMenu(fileName = "NotesLockAttack", menuName = "Team1/Kits/Notes/Lock")]
public class NotesLockAttack : CustomAttack
{
    [Tooltip("帯の幅（マス）")]
    [SerializeField, Min(0.5f)] float _width = 4f;
    [Tooltip("鍵が閉まるまで（秒。予告）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.9f;
    [Tooltip("能力の枠が使えない秒数")]
    [SerializeField, Min(0.1f)] float _seal = 4f;
    [Tooltip("オン：選んでいる枠1つだけ。オフ：全部の枠")]
    [SerializeField] bool _selectedOnly = true;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Rect arena = BattleArena.GetBounds();
        float x = Mathf.Clamp((owner.TargetPoint ?? owner.OriginPoint).x, arena.xMin + _width * 0.5f, arena.xMax - _width * 0.5f);
        GameObject go = owner.CreateSpawnedObject("NotesLock", new Vector2(x, arena.center.y));
        go.AddComponent<NotesLockZone>().Setup(owner, x, _width, arena, _telegraph, _seal, _selectedOnly);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_telegraph);
    }
}

/// <summary>「鍵をかける」の帯（NotesLockAttack が作る）。封印をかけたら、封印が解けるまで残る（場に残る技の効果）</summary>
public class NotesLockZone : CustomEffect
{
    const float FadeTime = 0.3f;

    AttackRunner _owner;
    float _xMin, _xMax;
    float _lockAt;
    float _endAt = float.PositiveInfinity;
    float _sealSeconds;
    bool _selectedOnly;
    bool _locked;
    bool _sealed;
    SpriteRenderer _band, _edgeL, _edgeR, _lock;

    public override bool Lingers => true;

    internal void Setup(AttackRunner owner, float x, float width, Rect arena, float telegraph, float seal, bool selectedOnly)
    {
        _owner = owner;
        _xMin = x - width * 0.5f;
        _xMax = x + width * 0.5f;
        _lockAt = Time.time + telegraph;
        _sealSeconds = seal;
        _selectedOnly = selectedOnly;
        float h = arena.height;
        Color paper = NotesArt.Yellow;
        paper.a = 0.28f;
        _band = AttackRunner.AddVisual(transform, PlaceholderSprites.Square, paper, new Vector2(width, h), 16, "Band");
        _edgeL = Edge(new Vector2(-width * 0.5f, 0f), h);
        _edgeR = Edge(new Vector2(width * 0.5f, 0f), h);
        _lock = RuntimeSprites.Show(transform, NotesArt.Lock(true), new Vector2(0f, h * 0.5f - 1.2f), 30, 1.5f, "Lock");
    }

    SpriteRenderer Edge(Vector2 local, float h)
    {
        var go = new GameObject("Edge");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        return AttackRunner.AddVisual(go.transform, PlaceholderSprites.Square, KitDress.Red, new Vector2(3f * KitDress.Px, h), 17, "Red");
    }

    void Update()
    {
        float now = Time.time;
        if (!_locked)
        {
            bool on = Mathf.FloorToInt(now / 0.1f) % 2 == 0 || _lockAt - now > 0.45f;
            if (_edgeL != null) _edgeL.enabled = on;
            if (_edgeR != null) _edgeR.enabled = on;
            if (now >= _lockAt) Lock();
            return;
        }
        if (now >= _endAt)
        {
            if (_sealed) GameAudio.Event("gm_notes_unlock");
            Destroy(gameObject);
            return;
        }
        // 封印の間は、帯をうすくして鍵だけ残す
        if (_band != null)
        {
            Color c = _band.color;
            c.a = Mathf.MoveTowards(c.a, _sealed ? 0.12f : 0f, Time.deltaTime);
            _band.color = c;
        }
    }

    void Lock()
    {
        _locked = true;
        if (_edgeL != null) _edgeL.enabled = false;
        if (_edgeR != null) _edgeR.enabled = false;
        if (_lock != null) _lock.sprite = NotesArt.Lock(false);
        FxKit.Ring(_lock != null ? (Vector2)_lock.transform.position : (Vector2)transform.position, 1.2f, 0.2f, Color.white);
        Transform target = _owner != null ? _owner.Target : null;
        float px = target != null ? target.position.x : float.NaN;
        if (!float.IsNaN(px) && px >= _xMin && px <= _xMax)
        {
            KitPlayer.Seal(_sealSeconds, _selectedOnly);
            GameAudio.Event("gm_notes_locked");
            _sealed = true;
            _endAt = Time.time + _sealSeconds;
        }
        else _endAt = Time.time + FadeTime;
    }

    // 形態変化・正常化で消されたら、かけた封印も解く
    protected override void Restore()
    {
        if (_sealed && Time.time < _endAt) KitPlayer.Unseal();
    }
}
