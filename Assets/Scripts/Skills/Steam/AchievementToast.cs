using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 実績解除の通知（画面の隅）。Steam_ToastBG の上に Steam_Trophy のコマ送りを置き、「実績解除」と実績名を UI Text で出す。
/// 下からスライドインして、AchievementPassive の「通知を出しておく秒数」（約3秒）で消える。
/// 続けて解除したときは、上に積んで出す。キャンバスは最初の通知のときに作る（シーンに置かなくてよい）。
/// </summary>
public class AchievementToast : MonoBehaviour
{
    /// <summary>素材のドットを何倍で出すか（基準の画面 1280×720 で）</summary>
    const float PixelScale = 1.5f;
    const float Margin = 16f;
    const float SlideTime = 0.25f;

    static Canvas _canvas;
    static readonly List<AchievementToast> Active = new List<AchievementToast>();

    RectTransform _rect;
    float _height;

    /// <summary>今出ている通知の数（テスト用）</summary>
    public static int ActiveCount
    {
        get
        {
            Active.RemoveAll(t => t == null);
            return Active.Count;
        }
    }

    /// <summary>通知を出す</summary>
    public static AchievementToast Show(AchievementPassive passive, string achievementName)
    {
        if (passive == null) return null;
        EnsureCanvas();
        Active.RemoveAll(t => t == null);

        Sprite bg = passive.ToastBackground;
        Vector2 size = bg != null ? bg.rect.size * PixelScale : new Vector2(378f, 99f);

        var go = new GameObject("AchievementToast", typeof(RectTransform));
        go.transform.SetParent(_canvas.transform, false);
        var rect = (RectTransform)go.transform;
        float side = passive.ToastOnRight ? 1f : 0f;
        rect.anchorMin = rect.anchorMax = new Vector2(side, 0f);
        rect.pivot = new Vector2(side, 0f);
        rect.sizeDelta = size;

        var bgImage = go.AddComponent<Image>();
        bgImage.sprite = bg;
        bgImage.color = bg != null ? Color.white : new Color(0.1f, 0.12f, 0.18f, 0.92f);

        // トロフィー（左）
        Sprite[] trophy = passive.TrophyFrames;
        Image trophyImage = null;
        if (trophy != null && trophy.Length > 0 && trophy[0] != null)
        {
            var t = new GameObject("Trophy", typeof(RectTransform));
            t.transform.SetParent(go.transform, false);
            var tr = (RectTransform)t.transform;
            Vector2 tsize = trophy[0].rect.size * PixelScale;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.sizeDelta = tsize;
            tr.anchoredPosition = new Vector2((size.y - tsize.y) * 0.5f, 0f);
            trophyImage = t.AddComponent<Image>();
            trophyImage.sprite = trophy[0];
        }

        // 文字（右側の空き）
        float textLeft = size.y + 4f;
        AddText(go.transform, "Title", passive.ToastTitle, passive.TitleColor, Mathf.RoundToInt(size.y * 0.2f), textLeft, new Vector2(0f, 0.5f), new Vector2(1f, 0.95f), FontStyle.Normal);
        AddText(go.transform, "Name", achievementName, passive.NameColor, Mathf.RoundToInt(size.y * 0.26f), textLeft, new Vector2(0f, 0.05f), new Vector2(1f, 0.55f), FontStyle.Bold);

        var toast = go.AddComponent<AchievementToast>();
        toast._rect = rect;
        toast._height = size.y;
        float baseY = Margin;
        foreach (var other in Active) baseY += other._height + 6f; // 出ている通知の上に積む
        Active.Add(toast);
        toast.StartCoroutine(toast.Run(baseY, passive.ToastOnRight ? -Margin : Margin, passive.ToastDuration, trophyImage, trophy, passive.TrophyFrameRate));
        return toast;
    }

    static void AddText(Transform parent, string name, string text, Color color, int fontSize, float left, Vector2 anchorMin, Vector2 anchorMax, FontStyle style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = new Vector2(left, 0f);
        r.offsetMax = new Vector2(-8f, 0f);
        var label = go.AddComponent<Text>();
        label.font = LoadFont();
        label.text = text;
        label.color = color;
        label.fontSize = Mathf.Max(10, fontSize);
        label.fontStyle = style;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
    }

    static Font LoadFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { /* 古い Unity */ }
        if (f == null)
        {
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return f;
    }

    static void EnsureCanvas()
    {
        if (_canvas != null) return;
        var go = new GameObject("AchievementToastCanvas");
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    IEnumerator Run(float y, float x, float duration, Image trophy, Sprite[] frames, float fps)
    {
        float hidden = -_height - 4f;
        float start = Time.unscaledTime;
        float end = start + Mathf.Max(duration, SlideTime * 2f);
        while (Time.unscaledTime < end)
        {
            float t = Time.unscaledTime - start;
            float left = end - Time.unscaledTime;
            float k = t < SlideTime ? t / SlideTime : left < SlideTime ? left / SlideTime : 1f;
            k = 1f - (1f - k) * (1f - k); // なめらかに止まる
            _rect.anchoredPosition = new Vector2(x, Mathf.Lerp(hidden, y, k));
            if (trophy != null && frames != null && frames.Length > 0)
            {
                trophy.sprite = frames[SpriteFrames.Index(t, fps, frames.Length, false)];
            }
            yield return null;
        }
        Active.Remove(this);
        Destroy(gameObject);
    }

    void OnDestroy() => Active.Remove(this);
}
