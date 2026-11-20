using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// プレイヤーの頭の上のHPバー（2026-11-20：上の HUD の HP から移した）。
/// ・枠（最大HPで HPBar_N_Frame を選ぶ）の中にマスを並べる。残りは緑、減ったマスは空、残り1のときは最後のマスをオレンジ（点滅なし）
/// ・減ったマスは「白 → 赤・空 を3回」の7コマで点滅してから空になる（1コマ 0.08 秒）。点滅中にまた減ったら、そのマスも別に点滅を始める
/// ・回復したマスはすぐ緑に戻す。HP0 は最後の点滅のあとにバーを消す（ゲームオーバーは今までの流れのまま）
/// プレイヤーのルート（向きで反転する _visualRoot の外）の子に置くので、左を向いてもマスは左から並び、右から減る。
/// HP は Health.Changed を受け取るだけで、HP の計算には手を出さない。
/// シーンを読み込んだら、プレイヤー（PlayerController ＋ プレイヤーの陣営の Health）に自動で付ける。素材は Resources/PlayerHPBarArt
/// </summary>
public class PlayerOverheadHPBar : MonoBehaviour
{
    public const string SortingLayerName = "WorldUI";
    /// <summary>WorldUI のレイヤーが無いときは Default のこの順番で描く（演出の最大 80 より手前）</summary>
    const int FallbackOrder = 1000;

    [SerializeField] Health _health;
    [SerializeField] PlayerHPBarArt _art;

    SpriteRenderer _frame;
    SpriteRenderer[] _pips = new SpriteRenderer[0];
    Coroutine[] _flashes = new Coroutine[0];
    int _builtMax = -1;
    float _topLocal;     // 枠の上の端（バーの原点＝足元からの高さ）
    int _shown;          // 表示に反映した今の HP
    int _activeFlashes;
    bool _warnedMax;

    // ───────── 自動で付ける ─────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToPlayers();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToPlayers();

    /// <summary>シーンにいるプレイヤーにバーを付ける（もう付いていれば何もしない）</summary>
    public static void AttachToPlayers()
    {
        PlayerHPBarArt art = null;
        foreach (PlayerController pc in FindObjectsOfType<PlayerController>(true))
        {
            Health health = pc.GetComponent<Health>();
            if (health == null || health.Team != Team.Player) continue;
            if (pc.GetComponentInChildren<PlayerOverheadHPBar>(true) != null) continue;
            if (art == null) art = Resources.Load<PlayerHPBarArt>(PlayerHPBarArt.ResourcePath);
            if (art == null)
            {
                Debug.LogWarning($"[PlayerOverheadHPBar] Resources/{PlayerHPBarArt.ResourcePath} が見つからないので、頭の上のHPバーを出せません");
                return;
            }
            var go = new GameObject("OverheadHPBar");
            go.layer = pc.gameObject.layer;
            go.transform.SetParent(pc.transform, false);
            var bar = go.AddComponent<PlayerOverheadHPBar>();
            bar._health = health;
            bar._art = art;
        }
    }

    // ───────── 表示 ─────────

    void Awake()
    {
        if (_health == null) _health = GetComponentInParent<Health>();
        if (_art == null) _art = Resources.Load<PlayerHPBarArt>(PlayerHPBarArt.ResourcePath);
    }

    void OnEnable()
    {
        if (_health == null || _art == null) return;
        _health.Changed += OnChanged;
        if (_builtMax != _health.Max) Rebuild(_health.Max);
        _shown = _health.IsDead ? 0 : _health.Current;
        RefreshAll();
    }

    void OnDisable()
    {
        if (_health != null) _health.Changed -= OnChanged;
        // 無効になるとコルーチンは止まるので、点滅は終わったものとして扱う
        for (int i = 0; i < _flashes.Length; i++) _flashes[i] = null;
        _activeFlashes = 0;
    }

    void LateUpdate()
    {
        // 親が反転していても、バーは反転させない（マスを左から並べる）
        Transform parent = transform.parent;
        float sx = parent != null && parent.lossyScale.x < 0f ? -1f : 1f;
        if (transform.localScale.x != sx) transform.localScale = new Vector3(sx, 1f, 1f);

        GameSession session = GameSession.Instance;
        bool hide = session != null && session.IsTransitioning; // 上の HUD と同じく、画面の切り替え中は出さない
        hide |= _health != null && _health.IsDead && _activeFlashes == 0;
        SetVisible(!hide);
    }

    void OnChanged(int current, int max)
    {
        if (max != _builtMax) Rebuild(max);
        int before = _shown;
        _shown = Mathf.Clamp(current, 0, max);

        for (int i = 0; i < _pips.Length; i++)
        {
            if (i < _shown)
            {
                StopFlash(i); // 回復したマスは点滅をやめて、すぐ戻す
                SetPip(i, PipFor(i));
            }
            else if (i < before)
            {
                StartFlash(i); // 今回減ったマス
            }
            else if (_flashes[i] == null)
            {
                SetPip(i, _art.PipEmpty);
            }
        }
    }

    Sprite PipFor(int index)
    {
        if (index >= _shown) return _art.PipEmpty;
        return _shown == 1 ? _art.PipLow : _art.PipFull;
    }

    void RefreshAll()
    {
        for (int i = 0; i < _pips.Length; i++)
        {
            StopFlash(i);
            SetPip(i, PipFor(i));
        }
    }

    void SetPip(int i, Sprite s)
    {
        if (_pips[i].sprite != s) _pips[i].sprite = s;
    }

    // ───────── 点滅 ─────────

    void StartFlash(int i)
    {
        if (_flashes[i] != null) return; // もう点滅中なら最後まで続ける
        if (!isActiveAndEnabled) { SetPip(i, _art.PipEmpty); return; }
        _activeFlashes++;
        _flashes[i] = StartCoroutine(Flash(i));
    }

    void StopFlash(int i)
    {
        if (_flashes[i] == null) return;
        StopCoroutine(_flashes[i]);
        _flashes[i] = null;
        _activeFlashes = Mathf.Max(0, _activeFlashes - 1);
    }

    IEnumerator Flash(int i)
    {
        var wait = new WaitForSeconds(_art.FlashFrameSeconds);
        SetPip(i, _art.PipFlashWhite);
        yield return wait;
        for (int n = 0; n < 3; n++)
        {
            SetPip(i, _art.PipFlashRed);
            yield return wait;
            SetPip(i, _art.PipEmpty);
            yield return wait;
        }
        _flashes[i] = null;
        _activeFlashes = Mathf.Max(0, _activeFlashes - 1);
    }

    // ───────── 組み立て ─────────

    void Rebuild(int max)
    {
        max = Mathf.Max(1, max);
        if (!_art.HasFrameFor(max) && !_warnedMax)
        {
            _warnedMax = true;
            Debug.LogWarning($"[PlayerOverheadHPBar] 最大HP {max} に合う枠がありません（{PlayerHPBarArt.MinFrameMax}〜{_art.MaxFrameMax} だけ）。近い枠で代わりに出します");
        }

        for (int i = 0; i < _flashes.Length; i++) StopFlash(i);
        if (_frame != null) Destroy(_frame.gameObject);
        foreach (SpriteRenderer p in _pips) if (p != null) Destroy(p.gameObject);

        float dot = _art.DotSize;
        Sprite frameSprite = _art.FrameFor(max);
        // 枠の幅はどれも奇数ドットで、プレイヤーの絵（13ドット）と同じく真ん中が半ドットの位置になる → 中央にそろえればドットの格子がそろう
        float width = frameSprite != null ? frameSprite.bounds.size.x : (4 * max + 3) * dot;
        float height = frameSprite != null ? frameSprite.bounds.size.y : 6 * dot;
        float left = -width * 0.5f;
        float top = (_art.HeadTopDots + _art.GapDots) * dot + height;

        _topLocal = top;
        _frame = MakeRenderer("Frame", frameSprite, new Vector3(left, top, 0f), 0);
        _pips = new SpriteRenderer[max];
        _flashes = new Coroutine[max];
        _activeFlashes = 0;
        for (int i = 0; i < max; i++)
        {
            // 1マス目は枠の左上から (2, 2) ドット。マスは 3×2、間隔 4（マス3＋すき間1）
            var pos = new Vector3(left + (2 + 4 * i) * dot, top - 2 * dot, 0f);
            _pips[i] = MakeRenderer("Pip" + (i + 1), _art.PipEmpty, pos, 1);
        }
        _builtMax = max;
    }

    SpriteRenderer MakeRenderer(string name, Sprite sprite, Vector3 localPos, int order)
    {
        var go = new GameObject(name);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        int layerId = SortingLayer.NameToID(SortingLayerName);
        if (SortingLayer.IsValid(layerId) && layerId != 0)
        {
            sr.sortingLayerID = layerId;
            sr.sortingOrder = order;
        }
        else
        {
            sr.sortingOrder = FallbackOrder + order;
        }
        return sr;
    }

    void SetVisible(bool on)
    {
        if (_frame != null && _frame.enabled != on) _frame.enabled = on;
        foreach (SpriteRenderer p in _pips) if (p != null && p.enabled != on) p.enabled = on;
    }

    // ───────── ほかの演出から使う ─────────

    /// <summary>元のドット絵の1ドットのワールドでの長さ</summary>
    public float DotSize => _art != null ? _art.DotSize : 1f / 18f;

    /// <summary>
    /// バーが見えていれば、枠の上の端の真ん中（ワールド座標）を返す。
    /// 頭の上に出す演出（スキルのアイコンなど）をバーと重ねないために使う
    /// </summary>
    public bool TryGetTop(out Vector2 top)
    {
        top = default;
        if (!isActiveAndEnabled || _frame == null || !_frame.enabled) return false;
        top = transform.TransformPoint(new Vector3(0f, _topLocal, 0f));
        return true;
    }

    // ───────── 確認用 ─────────

    [ContextMenu("確認用：ダメージ 1")]
    void DebugDamage() => DebugPlayerHealthKeys.Damage(_health);

    [ContextMenu("確認用：回復 1")]
    void DebugHeal() => DebugPlayerHealthKeys.Heal(_health);
}
