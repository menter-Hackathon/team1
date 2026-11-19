using UnityEngine;

/// <summary>進行とボスへの接近に応じて、壁紙に感染ノイズを重ねる。</summary>
public sealed class DesktopDecay : MonoBehaviour
{
    const int Width = 320;
    const float FramesPerSecond = 12f;

    Texture2D _texture;
    Sprite _sprite;
    Color32[] _source;
    Color32[] _pixels;
    int _height;
    int _frame = -1;
    float _progress;
    float _startedAt;
    Transform _player;
    BossController _boss;
    float _nearDistance;
    float _farDistance;
    float _proximity;
    BossEntrance _entrance;
    EnemyWave _wave;
    float _battleIntensity;
    float _waveProgress;
    float _gaugeProgress;
    float _visibleSeverity;

    // 戦闘中の増加量もステージの深さで制限する。
    // 最初のステージは全条件が重なっても約0.16、最深部で最大0.60。
    float TargetSeverity
    {
        get
        {
            float baseline = Mathf.Lerp(0.028f, 0.10f, _progress);
            float budget = Mathf.Lerp(0.13f, 0.50f, _progress);
            float combat = _waveProgress * 0.15f + _battleIntensity * 0.08f
                + _proximity * 0.27f + _gaugeProgress * 0.50f;
            return baseline + budget * Mathf.Clamp01(combat);
        }
    }
    float Severity => _visibleSeverity;

    public void BindBattle(BossEntrance entrance, EnemyWave wave)
    {
        _entrance = entrance;
        _wave = wave;
    }

    public void SetBattleIntensity(float intensity) => _battleIntensity = Mathf.Clamp01(intensity);
    public void SetGaugeProgress(float progress) => _gaugeProgress = Mathf.Clamp01(progress);
    public void SetWaveProgress(float progress) => _waveProgress = Mathf.Clamp01(progress);

    public void BindProximity(Transform player, BossController boss, float nearDistance, float farDistance)
    {
        _player = player;
        _boss = boss;
        _nearDistance = Mathf.Max(0f, nearDistance);
        _farDistance = Mathf.Max(_nearDistance + 0.01f, farDistance);
    }

    // エディターのプレビューでも同じ強さを確認する。
    public void SetProximity(float proximity) => _proximity = Mathf.Clamp01(proximity);

    public void Initialize(SpriteRenderer wallpaper, int stageIndex, int stageCount)
    {
        if (wallpaper == null || wallpaper.sprite == null || _texture != null) return;
        Sprite source = wallpaper.sprite;
        if (!source.texture.isReadable)
        {
            Debug.LogWarning("感染ノイズには背景画像の Read/Write Enabled が必要です。", wallpaper);
            return;
        }

        _progress = stageCount > 1 ? Mathf.Clamp01((float)stageIndex / (stageCount - 1)) : 0f;
        _visibleSeverity = TargetSeverity;
        _height = Mathf.Max(1, Mathf.RoundToInt(Width * source.rect.height / source.rect.width));
        _source = new Color32[Width * _height];
        _pixels = new Color32[_source.Length];
        Rect region = source.textureRect;
        Color32[] original = source.texture.GetPixels32();
        for (int y = 0; y < _height; y++)
        {
            int sourceY = Mathf.Min((int)region.yMax - 1, (int)region.y + y * (int)region.height / _height);
            for (int x = 0; x < Width; x++)
            {
                int sourceX = Mathf.Min((int)region.xMax - 1, (int)region.x + x * (int)region.width / Width);
                _source[y * Width + x] = original[sourceY * source.texture.width + sourceX];
            }
        }

        _texture = new Texture2D(Width, _height, TextureFormat.RGBA32, false)
        {
            name = "Desktop infection noise",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        _sprite = Sprite.Create(_texture, new Rect(0, 0, Width, _height),
            source.pivot / source.rect.size, Width / source.bounds.size.x);
        var overlay = new GameObject("Desktop infection noise");
        overlay.transform.SetParent(wallpaper.transform, false);
        var renderer = overlay.AddComponent<SpriteRenderer>();
        renderer.sprite = _sprite;
        renderer.color = wallpaper.color;
        renderer.flipX = wallpaper.flipX;
        renderer.flipY = wallpaper.flipY;
        renderer.sortingLayerID = wallpaper.sortingLayerID;
        renderer.sortingOrder = wallpaper.sortingOrder + 1;
        _startedAt = Time.time;
        Animate(0f);
    }

    void Update()
    {
        bool infected = _boss != null && _boss.State != BossState.Normalized;
        bool bossStarted = infected && ((_entrance != null && _entrance.IsTriggered)
            || _boss.gameObject.activeInHierarchy);
        _battleIntensity = Mathf.MoveTowards(_battleIntensity, bossStarted ? 1f : 0f, Time.deltaTime * 0.65f);
        _waveProgress = Mathf.MoveTowards(_waveProgress,
            infected && _wave != null ? _wave.Progress : 0f, Time.deltaTime * 0.5f);
        _gaugeProgress = Mathf.MoveTowards(_gaugeProgress,
            bossStarted && _boss.Gauge != null ? _boss.Gauge.Ratio : 0f, Time.deltaTime * 0.5f);
        if (_player != null && _boss != null)
        {
            float target = 0f;
            if (bossStarted)
            {
                // 登場前の非表示ボスには反応させない。排出後はウイルス本体が発生源。
                Transform source = _boss.State == BossState.Ejected && _boss.EjectedVirus != null
                    ? _boss.EjectedVirus.transform : _boss.transform;
                float distance = Vector2.Distance(_player.position, source.position);
                target = 1f - Mathf.InverseLerp(_nearDistance, _farDistance, distance);
                target = Mathf.SmoothStep(0f, 1f, target);
            }
            _proximity = Mathf.MoveTowards(_proximity, target, Time.deltaTime * 0.7f);
        }
        // 登場・接近・ゲージ増加が重なっても、見えるノイズ量は急増させない。
        // 雑魚撃破による小さな増加は短く、登場と接近が重なった大きな増加は数秒かける。
        float rate = TargetSeverity > _visibleSeverity ? 0.03f : 0.12f;
        _visibleSeverity = Mathf.MoveTowards(_visibleSeverity, TargetSeverity, Time.deltaTime * rate);
        Animate(Time.time - _startedAt);
    }

    public void Animate(float time, bool force = false)
    {
        if (_texture == null) return;
        // プレビューのスライダー操作時は、指定した状態を直接表示する。
        if (!Application.isPlaying) _visibleSeverity = TargetSeverity;
        int frame = Mathf.FloorToInt(time * FramesPerSecond);
        if (_frame == frame && !force) return;
        _frame = frame;
        System.Array.Clear(_pixels, 0, _pixels.Length);
        // 配置と色は固定し、緩い脈動と数ドットの揺れだけで感染が動く。
        var random = new System.Random(7219);
        float density = Severity;
        for (int y = 2; y < _height; y += 5)
        {
            for (int x = 2; x < Width; x += 12)
            {
                float threshold = (float)random.NextDouble();
                int seed = random.Next(1000);
                int blockWidth = random.Next(6, 27);
                int blockHeight = random.Next(3, 9);
                float opacity = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(threshold, threshold + 0.05f, density));
                opacity *= 0.92f + 0.08f * Mathf.Sin(time * 0.8f + seed);
                if (opacity <= 0f) continue;
                int offset = (seed % 5 - 2) * 3 + Mathf.RoundToInt(Mathf.Sin(time * 0.5f + seed) * 2f);
                for (int dy = 0; dy < blockHeight && y + dy < _height; dy++)
                {
                    // 行ごとに輪郭をずらし、穴や飛び散りのある破損模様にする。
                    int rowShift = Mathf.RoundToInt((Noise01(0, dy, seed) - 0.5f) * 6f);
                    int rowWidth = Mathf.RoundToInt(blockWidth * Mathf.Lerp(0.4f, 1f, Noise01(1, dy, seed)));
                    for (int dx = 0; dx < blockWidth && x + dx < Width; dx++)
                    {
                        float grain = Noise01(dx, dy, seed);
                        float cluster = Noise01(dx / 2, dy / 2, seed + 117);
                        if (dx > rowWidth || grain < 0.2f || cluster < 0.25f) continue;
                        int pixelX = x + dx + rowShift;
                        if (pixelX < 0 || pixelX >= Width) continue;
                        int shiftedX = (pixelX + offset + Width) % Width;
                        Color32 color = _source[(y + dy) * Width + shiftedX];
                        // 紫の染み・緑の粒・マゼンタの色化けを、固定した粒の配置で混ぜる。
                        if (cluster > 0.78f)
                            color = new Color32(140, 235, 75, 210);
                        else if (grain > 0.85f)
                            color = new Color32(204, 45, 161, 195);
                        else if (seed % 3 != 2)
                            color = new Color32(79, 16, 112, 205);
                        else
                            color.a = 165;
                        // 緑の模様を横切る淡い波。点滅させず、常に色が残る。
                        float flow = 0.88f + 0.12f * Mathf.Sin(dx * 0.35f - time * 1.1f + seed);
                        float depth = Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(Severity / 0.5f));
                        color.a = (byte)Mathf.RoundToInt(color.a * opacity * flow * depth);
                        _pixels[(y + dy) * Width + pixelX] = color;
                    }
                }
            }
        }

        // 帯の場所は固定し、ゆっくり横に揺らす。ボス戦では追加の帯が浮かぶ。
        for (int band = 0; band < 9; band++)
        {
            int y = (23 + band * 47) % _height;
            int offset = (band % 2 == 0 ? 5 : -5) + Mathf.RoundToInt(Mathf.Sin(time * 0.45f + band) * 2f);
            int startX = (31 + band * 97) % Width;
            float strength = Mathf.Clamp01(Severity * 1.8f);
            if (band >= 6) strength *= _battleIntensity;
            for (int dx = 0; dx < Width / 3; dx++)
            {
                if (Noise01(dx / 3, band, 509) < 0.38f) continue;
                int x = (startX + dx) % Width;
                Color32 color = _source[y * Width + (x + offset + Width) % Width];
                color.a = (byte)Mathf.RoundToInt(125f * strength);
                _pixels[y * Width + x] = color;
            }
        }
        DrawInterference(time);
        _texture.SetPixels32(_pixels);
        _texture.Apply(false, false);
    }

    void DrawInterference(float time)
    {
        // 約5秒おきに短い「ザザッ」。画面全体は明滅させず、細い断片だけを流す。
        const float interval = 5.2f;
        const float duration = 0.8f;
        float phase = Mathf.Repeat(time, interval);
        if (phase >= duration) return;
        int cycle = Mathf.FloorToInt(time / interval);
        int tick = Mathf.FloorToInt(phase * 9f);
        float envelope = Mathf.Sin(phase / duration * Mathf.PI);
        envelope *= envelope;
        float intensity = Mathf.Clamp01(0.04f + Severity * 1.6f);
        for (int strip = 0; strip < 7; strip++)
        {
            int seed = cycle * 31 + strip * 173;
            int y = Mathf.Min(_height - 1, (int)(Noise01(0, strip, cycle + 651) * _height));
            int startX = (int)(Noise01(1, strip, cycle + 651) * Width);
            int length = 30 + (int)(Noise01(2, strip, cycle + 651) * 70f);
            int offset = (strip % 2 == 0 ? 1 : -1) * (4 + tick % 3 * 3);
            for (int dx = 0; dx < length; dx++)
            {
                float grain = Noise01(dx / 2, tick, seed);
                if (grain < 0.35f) continue;
                int x = (startX + dx + tick * (strip % 2 == 0 ? 3 : -3) + Width) % Width;
                int shiftedX = (x + offset + Width) % Width;
                Color32 color = _source[y * Width + shiftedX];
                if (grain > 0.88f) color = new Color32(143, 212, 106, 255);
                else if (grain > 0.75f) color = new Color32(137, 42, 167, 255);
                float edgeFade = Mathf.Sin((dx + 1f) / (length + 1f) * Mathf.PI);
                color.a = (byte)Mathf.RoundToInt(155f * envelope * intensity * edgeFade);
                int index = y * Width + x;
                _pixels[index] = Color.Lerp((Color)_pixels[index], (Color)color, envelope);
            }
        }
    }

    // 時間を含めない粒模様。毎フレームのランダム再配置によるチラつきを防ぐ。
    static float Noise01(int x, int y, int seed)
    {
        unchecked
        {
            uint hash = (uint)(x * 374761393 + y * 668265263 + seed * 144269);
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            hash ^= hash >> 16;
            return (hash & 0xffffu) / 65535f;
        }
    }

    void OnDestroy()
    {
        if (Application.isPlaying)
        {
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
        }
        else
        {
            if (_sprite != null) DestroyImmediate(_sprite);
            if (_texture != null) DestroyImmediate(_texture);
        }
    }
}
