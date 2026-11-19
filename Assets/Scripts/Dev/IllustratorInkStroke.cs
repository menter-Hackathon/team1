using UnityEngine;

/// <summary>既存キャラクターと同じドット密度で、下描きとインクを直接ラスタライズする。</summary>
public sealed class IllustratorInkStroke : MonoBehaviour
{
    public sealed class DamageGroup { public bool Hit; }
    public Vector3[] Points { get; private set; }
    public bool Friendly { get; private set; }
    public DamageGroup Group { get; private set; }
    public bool Finished { get; private set; }
    public bool Dangerous { get; private set; }
    public int VisiblePoints { get; private set; }
    public float Radius { get; private set; }
    IllustratorPixelCanvas _canvas;
    IllustratorBrushTip _brush;
    Color32 _color, _dark, _guide;
    float _born, _warning, _draw, _hold, _fade;
    int _width, _frame = -1;
    bool _wasWarning = true;

    public void Initialize(Vector3[] points, Color color, bool friendly,
        float warning, float draw, float hold, DamageGroup group = null)
    {
        Points = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++) Points[i] = IllustratorPixelCanvas.Snap(points[i]);
        Friendly = friendly; Group = group ?? new DamageGroup();
        _warning = warning; _draw = draw; _hold = hold; _fade = friendly ? 0.18f : 0.16f;
        _born = Time.time;
        _width = friendly ? 4 : 3;
        Radius = _width / (IllustratorPixelCanvas.Ppu * 2f);
        _color = color;
        _dark = Color.Lerp(color, new Color32(30, 19, 42, 255), 0.72f);
        _guide = Color.Lerp(color, new Color32(110, 104, 126, 255), 0.4f);
        _canvas = new IllustratorPixelCanvas(transform, Points, 0.3f, friendly ? 24 : 18);
        _brush = IllustratorBrushTip.Create(transform, color, friendly ? 28 : 22);
        Tick();
    }

    public void Tick()
    {
        float elapsed = Time.time - _born, active = elapsed - _warning;
        Finished = active >= _draw + _hold + _fade;
        Dangerous = active >= 0f && active < _draw + _hold;
        bool warning = active < 0f;
        int frame = Mathf.FloorToInt(elapsed * 12f);
        if (frame == _frame && warning == _wasWarning) return;
        _frame = frame; _wasWarning = warning;
        _canvas.Clear();
        if (warning)
        {
            VisiblePoints = 0;
            _canvas.Path(Points, Points.Length, 1, _guide, 9);
            for (int i = 0; i < 3; i++)
            {
                Vector3 point = Points[i * (Points.Length - 1) / 2];
                _canvas.Box(point, 3, _dark);
                _canvas.Box(point, 1, _color);
            }
            _brush.Renderer.enabled = false;
        }
        else
        {
            VisiblePoints = Mathf.Clamp(2 + Mathf.FloorToInt(Mathf.Clamp01(active / _draw) * (Points.Length - 2)), 2, Points.Length);
            float fade = 1f - Mathf.Clamp01((active - _draw - _hold) / _fade);
            int coverage = Mathf.CeilToInt(fade * 16f);
            _canvas.Path(Points, Points.Length, 1, _guide, 3);
            _canvas.Path(Points, VisiblePoints, _width + 2, _dark, coverage);
            _canvas.Path(Points, VisiblePoints, _width, _color, coverage);
            _brush.Renderer.enabled = active < _draw;
            if (_brush.Renderer.enabled)
            {
                _brush.transform.position = Points[VisiblePoints - 1];
                _brush.SetHeading(Points[VisiblePoints - 1] - Points[VisiblePoints - 2]);
            }
        }
        _canvas.Apply();
    }

    void OnDestroy() { _canvas?.Dispose(); }
}
