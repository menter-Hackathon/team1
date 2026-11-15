using System;
using UnityEngine;

/// <summary>
/// プレイヤーのスキル枠の操作（入力 → 所持スキル）と、UI 向けのイベントの窓口。
/// 所持スキルそのもの（SkillLoadout）は、シーンをまたいで引き継げるように別のただのデータにしてある。
/// ラン状態（M5）ができたら、シーンを読み込んだときに Bind(ラン状態の SkillLoadout) を呼ぶ。
/// 呼ばなければ、自分で空の3枠を作る。
///
/// 操作（キーボード／ゲームパッド）
/// ・1／2／3（Y／B／RB）：その枠のスキルを使う（使うと SkillUseRequested が飛ぶ。中身の実行は M3）
/// ・Q／E（LB）：選択中の枠を切り替える
/// ・X 長押し（LT）：選択中の枠のスキルを捨てる
/// ・満杯で新しいスキルを手に入れたとき（入れ替え待ち）：1／2／3 で入れ替える枠を選ぶ。X 長押しで拾わない。
///   入れ替え待ちの間もゲームは止めない
/// </summary>
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerSkills : MonoBehaviour
{
    [Tooltip("捨てるときに X を押し続ける秒数")]
    [SerializeField, Min(0.05f)] float _discardHoldTime = 0.5f;
    [Tooltip("【提案】オンにすると、同じスキルを複数の枠に持てる（自分で作った所持スキルにだけ効く）")]
    [SerializeField] bool _allowDuplicates;

    PlayerInputReader _input;
    PlayerController _controller; // 無くてもよい
    SkillLoadout _loadout;
    float _discardHeldTime;
    bool _discardNeedsRelease; // 1回の長押しで1回だけ捨てる。一度離すまで次は捨てない

    /// <summary>今つながっている所持スキル</summary>
    public SkillLoadout Loadout => _loadout;
    public int Capacity => _loadout.Capacity;
    /// <summary>選択中の枠（0始まり）。X 長押しで捨てる対象</summary>
    public int SelectedSlot { get; private set; }
    public SkillDefinition SelectedSkill => _loadout.Get(SelectedSlot);
    /// <summary>入れ替え待ちのスキル。無ければ null</summary>
    public SkillDefinition Pending => _loadout.Pending;
    public bool HasPending => _loadout.HasPending;
    /// <summary>捨てる長押しの進み具合（0〜1）。UI のゲージ用。捨てる対象が無いときは 0 のまま</summary>
    public float DiscardHoldProgress { get; private set; }

    /// <summary>枠の中身が変わったとき（獲得・入れ替え・捨てる・つなぎ替え）</summary>
    public event Action<SkillSlotChange> SlotChanged;
    /// <summary>入れ替え待ちが始まった（引数＝新しいスキル）／終わった（引数＝null）とき</summary>
    public event Action<SkillDefinition> PendingChanged;
    /// <summary>選択中の枠が変わったとき（引数＝枠の番号）</summary>
    public event Action<int> SelectedSlotChanged;
    /// <summary>スキル枠のボタンが押されて、スキルを使うとき（枠の番号, スキル）。M3 の攻撃の実行はここにつなぐ</summary>
    public event Action<int, SkillDefinition> SkillUseRequested;
    /// <summary>スキルを手に入れようとしたとき（スキル, 結果）。「もう持っている」などの表示用</summary>
    public event Action<SkillDefinition, SkillAcquireResult> AcquireAttempted;

    bool CanOperate => _controller == null || _controller.CanControl; // ライフ0・ステージ遷移中は触らせない

    void Awake()
    {
        _input = GetComponent<PlayerInputReader>();
        _controller = GetComponent<PlayerController>();
        if (_loadout == null) Bind(new SkillLoadout(SkillLoadout.DefaultCapacity) { AllowDuplicates = _allowDuplicates });
    }

    void OnEnable()
    {
        _input.SkillSlotPressed += OnSlotPressed;
        _input.SkillSelectStepped += OnSelectStepped;
    }

    void OnDisable()
    {
        _input.SkillSlotPressed -= OnSlotPressed;
        _input.SkillSelectStepped -= OnSelectStepped;
        ResetDiscardHold();
    }

    void OnDestroy()
    {
        // ラン状態の所持スキルはシーンより長生きするので、つないだままにしない
        if (_loadout != null)
        {
            _loadout.SlotChanged -= OnLoadoutSlotChanged;
            _loadout.PendingChanged -= OnLoadoutPendingChanged;
        }
    }

    /// <summary>
    /// 所持スキルをつなぎ替える（ラン状態から渡すとき用）。
    /// 前の所持スキルから切り替えたときは、全部の枠について Reason＝Loaded の SlotChanged と、PendingChanged を飛ばすので、
    /// UI はそこで表示を作り直せる。
    /// </summary>
    public void Bind(SkillLoadout loadout)
    {
        if (loadout == null) throw new ArgumentNullException(nameof(loadout));
        if (loadout == _loadout) return;

        SkillLoadout old = _loadout;
        if (old != null)
        {
            old.SlotChanged -= OnLoadoutSlotChanged;
            old.PendingChanged -= OnLoadoutPendingChanged;
        }
        _loadout = loadout;
        _loadout.SlotChanged += OnLoadoutSlotChanged;
        _loadout.PendingChanged += OnLoadoutPendingChanged;
        ResetDiscardHold();

        if (SelectedSlot >= _loadout.Capacity) SelectSlot(0);
        if (old == null) return; // 最初につなぐとき（Awake）はイベントを出さない。UI は購読したときに GetSkill で読む

        for (int i = 0; i < _loadout.Capacity; i++)
        {
            SlotChanged?.Invoke(new SkillSlotChange(i, old?.Get(i), _loadout.Get(i), SkillSlotChangeReason.Loaded));
        }
        PendingChanged?.Invoke(_loadout.Pending);
    }

    /// <summary>枠のスキル。空、または番号が範囲外なら null</summary>
    public SkillDefinition GetSkill(int index) => _loadout.Get(index);

    /// <summary>スキルを手に入れる（ボスの撃破報酬・拾えるアイテム・デバッグ用）。満杯なら入れ替え待ちになる</summary>
    public SkillAcquireResult TryAcquire(SkillDefinition skill)
    {
        SkillAcquireResult result = _loadout.TryAcquire(skill);
        AcquireAttempted?.Invoke(skill, result);
        return result;
    }

    /// <summary>選択中の枠を変える</summary>
    public void SelectSlot(int index)
    {
        index = Mathf.Clamp(index, 0, _loadout.Capacity - 1);
        if (index == SelectedSlot) return;
        SelectedSlot = index;
        _discardHeldTime = 0f; // 長押しの途中で枠を変えたら、数え直す
        SelectedSlotChanged?.Invoke(index);
    }

    void OnSlotPressed(int index)
    {
        if (!CanOperate || !_loadout.IsValidIndex(index)) return;

        // 入れ替え待ちの間は、1／2／3 は「入れ替える枠を選ぶ」ボタンになる（スキルは使わない）
        if (_loadout.HasPending)
        {
            _loadout.ReplaceWithPending(index);
            return;
        }

        if (_controller != null && !_controller.IsControllable) return; // のけぞり中は使えない
        SkillDefinition skill = _loadout.Get(index);
        if (skill == null) return;
        SkillUseRequested?.Invoke(index, skill);
    }

    void OnSelectStepped(int step)
    {
        if (!CanOperate) return;
        int n = _loadout.Capacity;
        SelectSlot(((SelectedSlot + step) % n + n) % n); // 端まで行ったら反対側に戻る
    }

    void Update()
    {
        if (!_input.SkillDiscardHeld || !CanOperate)
        {
            ResetDiscardHold();
            return;
        }

        // 入れ替え待ちなら「拾わない」、そうでなければ「選択中の枠を捨てる」。対象が無ければ数えない
        bool hasTarget = _loadout.HasPending || _loadout.Get(SelectedSlot) != null;
        if (_discardNeedsRelease || !hasTarget)
        {
            _discardHeldTime = 0f;
            DiscardHoldProgress = 0f;
            return;
        }

        // ポーズ中のメニューなどで時間が止まっていても数えられるよう、実時間で数える。
        // 読み込みなどで1フレームだけ長くかかっても、一気に長押しが終わらないように1フレーム分の上限を付ける
        _discardHeldTime += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        DiscardHoldProgress = Mathf.Clamp01(_discardHeldTime / _discardHoldTime);
        if (_discardHeldTime < _discardHoldTime) return;

        _discardNeedsRelease = true;
        _discardHeldTime = 0f;
        DiscardHoldProgress = 0f;
        if (_loadout.HasPending) _loadout.DeclinePending();
        else _loadout.Discard(SelectedSlot);
    }

    void ResetDiscardHold()
    {
        _discardHeldTime = 0f;
        _discardNeedsRelease = false;
        DiscardHoldProgress = 0f;
    }

    void OnLoadoutSlotChanged(SkillSlotChange change) => SlotChanged?.Invoke(change);

    void OnLoadoutPendingChanged(SkillDefinition pending)
    {
        // 捨てる長押しの途中で入れ替え待ちが始まった／終わったら、別の物を捨てないように一度離してもらう
        if (_input != null && _input.SkillDiscardHeld) _discardNeedsRelease = true;
        PendingChanged?.Invoke(pending);
    }
}
