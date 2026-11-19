using UnityEngine;

/// <summary>ドットの投げ縄が閉じてから、網点のインクが内側を塗る。</summary>
public sealed class IllustratorInkFill : MonoBehaviour
{
    public Vector3[] Polygon { get; private set; }
    public bool Finished { get; private set; }
    public bool Dangerous { get; private set; }
    public Vector2 Center { get; private set; }
    IllustratorPixelCanvas _canvas;
    bool[] _inside;
    float[] _distances;
    Color32 _color, _shade, _guide;
    float _born, _warning, _hold;
    int _frame = -1;
    bool _lasso, _wasDangerous, _wasWarning = true;
    readonly Vector3[] _cursor = new Vector3[16];
    readonly Vector3[] _tail = new Vector3[3];

    public void Initialize(Vector2 center, float width, Color color, float warning, float hold, int seed,
        float height = 0.44f, bool lasso = false)
    {
        Center = IllustratorPixelCanvas.Snap(center);
        _born = Time.time; _warning = warning; _hold = hold; _lasso = lasso;
        _color = color;
        _shade = Color.Lerp(color, new Color32(36, 24, 49, 255), 0.22f);
        _guide = Color.Lerp(color, new Color32(110, 104, 126, 255), 0.35f);
        Polygon = new Vector3[48];
        for (int i = 0; i < Polygon.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / Polygon.Length;
            float edge = 1f + (lasso ? 0.11f : 0.05f) * Mathf.Sin(angle * 3f + seed);
            Polygon[i] = IllustratorPixelCanvas.Snap(Center + new Vector2(Mathf.Cos(angle) * width * 0.5f * edge,
                Mathf.Sin(angle) * height * 0.5f * (lasso ? edge : 1f)));
        }
        _canvas = new IllustratorPixelCanvas(transform, Polygon, 0.5f, 14);
        _inside = new bool[_canvas.Pixels.Length];
        _distances = new float[_inside.Length];
        for (int y = 0; y < _canvas.Height; y++)
            for (int x = 0; x < _canvas.Width; x++)
            {
                int index = y * _canvas.Width + x;
                Vector3 world = _canvas.World(x, y);
                _inside[index] = IllustratorGeometry.Contains(Polygon, world);
                float dx = (world.x - Center.x) / (width * 0.5f), dy = (world.y - Center.y) / (height * 0.5f);
                _distances[index] = Mathf.Sqrt(dx * dx + dy * dy);
            }
        Tick();
    }

    public void Tick()
    {
        float elapsed = Time.time - _born, active = elapsed - _warning;
        Finished = active >= _hold + 0.35f;
        Dangerous = active >= 0.22f && active < _hold;
        bool warning = active < 0f;
        int frame = Mathf.FloorToInt(elapsed * 12f);
        if (frame == _frame && Dangerous == _wasDangerous && warning == _wasWarning) return;
        _frame = frame; _wasDangerous = Dangerous; _wasWarning = warning;
        _canvas.Clear();
        float fade = 1f - Mathf.Clamp01((active - _hold) / 0.35f);
        float spread = warning ? 1f : Mathf.Clamp01(active / 0.22f);
        int coverage = warning ? 1 : Mathf.CeilToInt(11f * fade);
        for (int y = 0; y < _canvas.Height; y++)
            for (int x = 0; x < _canvas.Width; x++)
            {
                int index = y * _canvas.Width + x;
                if (!_inside[index] || (spread < 1f && _distances[index] > spread)) continue;
                _canvas.Dot(x, y, (x + y) % 5 == 0 ? _shade : _color, coverage);
            }
        float drawing = Mathf.Clamp01(elapsed / (_warning * 0.7f));
        int visible = warning ? Mathf.Clamp(2 + Mathf.FloorToInt(drawing * (Polygon.Length - 2)), 2, Polygon.Length) : Polygon.Length;
        _canvas.Path(Polygon, visible, warning ? 1 : 2, warning ? _guide : _color,
            warning ? 10 : Mathf.CeilToInt(fade * 16f), visible == Polygon.Length);
        if (_lasso && warning)
        {
            float head = drawing * Polygon.Length;
            int index = Mathf.FloorToInt(head) % Polygon.Length;
            Vector3 point = IllustratorPixelCanvas.Snap(Vector3.Lerp(Polygon[index], Polygon[(index + 1) % Polygon.Length], head - Mathf.Floor(head)));
            for (int i = 0; i < _cursor.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / _cursor.Length;
                _cursor[i] = point + new Vector3(Mathf.Cos(angle) * 3f, Mathf.Sin(angle) * 2f, 0) / IllustratorPixelCanvas.Ppu;
            }
            _tail[0] = point + new Vector3(-2, -1, 0) / IllustratorPixelCanvas.Ppu;
            _tail[1] = point + new Vector3(-4, -3, 0) / IllustratorPixelCanvas.Ppu;
            _tail[2] = point + new Vector3(-3, -4, 0) / IllustratorPixelCanvas.Ppu;
            var dark = new Color32(38, 26, 48, 255);
            _canvas.Path(_cursor, _cursor.Length, 3, dark, 16, true);
            _canvas.Path(_tail, _tail.Length, 3, dark);
            _canvas.Path(_cursor, _cursor.Length, 1, _color, 16, true);
            _canvas.Path(_tail, _tail.Length, 1, _color);
        }
        _canvas.Apply();
    }

    void OnDestroy() { _canvas?.Dispose(); }
}
