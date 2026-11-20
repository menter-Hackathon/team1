using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace Team1.Achievements
{
    /// <summary>
    /// プレイ中・正常化したときの解除の知らせ。画面右下に、古い Windows 風の黄色い吹き出し（黒い枠、薄い黄色の背景）を約3秒出す。
    /// 戦闘は止めない（時間は unscaledTime で数え、入力も取らない）。Steam スキルの「実績解除バフ」の紺色のトーストとは見た目を分ける。
    /// 起動時の解除は、起動時スキャン画面に出すので、ここには出さない。
    /// </summary>
    public sealed class AchievementPopup : MonoBehaviour
    {
        const float Width = 236f, Height = 54f, Tail = 7f, SlideTime = 0.15f;

        readonly Queue<AchievementUnlock> _queue = new Queue<AchievementUnlock>();
        AchievementService _service;
        AchievementUnlock _current;
        float _shownAt;
        IDisposable _subscription;

        void Start()
        {
            _service = GetComponent<AchievementService>();
            if (_service == null) return;
            _subscription = _service.Unlocked
                .Where(u => u != null && u.Spec != null && (u.Forced || u.Spec.Trigger != AchievementTrigger.LaunchScan))
                .Subscribe(u => _queue.Enqueue(u));
        }

        void OnDestroy() => _subscription?.Dispose();

        float Seconds => _service != null && _service.Database != null ? _service.Database.PopupSeconds : 3f;

        void Update()
        {
            if (_current != null && Time.unscaledTime - _shownAt >= Seconds) _current = null;
            if (_current == null && _queue.Count > 0)
            {
                _current = _queue.Dequeue();
                _shownAt = Time.unscaledTime;
                GameAudio.Event("ui_decide");
            }
        }

        void OnGUI()
        {
            GUI.depth = -100; // ほかの画面より手前（GUI.depth は戻さない。どのイベントでも同じ値にしておく）
            if (_current == null || Event.current.type != EventType.Repaint) return;
            PixelGui.Begin(false);

            float t = Time.unscaledTime - _shownAt;
            float slide = Mathf.Clamp01(Mathf.Min(t, Seconds - t) / SlideTime); // 出るとき・消えるときに少し動く
            float x = Mathf.Round(PixelGui.Width - Width - 10f);
            float y = Mathf.Round(PixelGui.Height - Height - Tail - 12f + (1f - slide) * 10f);
            var r = new Rect(x, y, Width, Height);

            // 吹き出し（黒い枠・薄い黄色）と、右下の尾
            PixelGui.Fill(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), new Color(0f, 0f, 0f, 0.25f));
            PixelGui.Fill(r, AchievementGui.TooltipLine);
            PixelGui.Fill(PixelGui.Inset(r, 1f), AchievementGui.TooltipBack);
            float tx = r.xMax - 34f;
            for (int i = 0; i < (int)Tail; i++)
            {
                PixelGui.Fill(new Rect(tx, r.yMax - 1f + i, Tail + 1f - i, 1f), AchievementGui.TooltipLine);
                if (Tail - 1f - i > 0f) PixelGui.Fill(new Rect(tx + 1f, r.yMax - 1f + i, Tail - 1f - i, 1f), AchievementGui.TooltipBack);
            }

            AchievementSpec spec = _current.Spec;
            AchievementDefinition def = _service != null ? _service.Definition(spec.Id) : null;
            AchievementGui.Icon(new Rect(r.x + 8f, r.y + 9f, 36f, 36f), def, spec.Rank, true, _service != null ? _service.Frame(spec.Rank) : null);
            // 実績一覧・スキャン画面と同じく短く：名前（灰）・褒める一言・達成したアプリだけ
            float tx2 = r.x + 52f, tw = r.width - 58f;
            bool hasApp = !string.IsNullOrEmpty(_current.App);
            float top = hasApp ? r.y + 5f : r.y + 11f; // アプリが無いときは2行を上下のまん中に
            PixelGui.TextFit(new Rect(tx2, top, tw, 15f), spec.Name, 12, new Color32(90, 90, 90, 255));
            PixelGui.TextFit(new Rect(tx2, top + 15f, tw, 16f), string.IsNullOrEmpty(spec.Praise) ? spec.Name : spec.Praise, 12, AchievementGui.TooltipLine);
            if (hasApp) PixelGui.TextFit(new Rect(tx2, top + 31f, tw, 15f), $"{_current.App}で達成！", 12, new Color32(0, 84, 190, 255));

            PixelGui.End();
        }
    }
}
