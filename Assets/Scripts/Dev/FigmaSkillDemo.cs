using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>共通の部屋でFigmaの付箋・選択ハンドルを試す。本編の対応表は変更しない。</summary>
public sealed class FigmaSkillDemo : MonoBehaviour, IWarpable
{
    [SerializeField] PlayerController _player;
    [SerializeField] VirusGauge _boss;
    [SerializeField] BoxCollider2D _bossBody;
    [SerializeField] Camera _camera;
    [SerializeField] Texture2D _bossBodySheet, _bossFaceSheet, _bossHandsSheet;
    readonly List<FigmaDesignAttack> _attacks = new List<FigmaDesignAttack>();
    Health _health;
    Collider2D _playerBody;
    Hitbox _contact;
    DemoAppBossVisual _visual;
    Sprite _icon;
    Texture2D _iconTexture;
    SkillDefinition _skill;
    Vector3 _playerStart, _bossStart, _warpTarget;
    float _readyAt, _nextAttack, _nextWarp, _manualAt, _warpAt, _nextSticky;
    int _stickyLane = -1;
    bool _auto = true, _invincible, _controls, _warping, _warped;
    bool CanFight => _health != null && !_health.IsDead && !_boss.IsFull;
    bool Enraged => _boss.Ratio >= 0.5f;
    public Bounds BodyBounds => new Bounds(_bossBody.transform.TransformPoint(_bossBody.offset),
        Vector2.Scale(_bossBody.size, _bossBody.transform.lossyScale));

    public void SetVisibility(float alpha) { if (_visual != null) _visual.Visibility = Mathf.Clamp01(alpha); }
    public void SetContactEnabled(bool enabled)
    {
        bool on = enabled && !_boss.IsFull;
        _bossBody.enabled = on;
        if (_contact != null) { if (on) _contact.Activate(); else _contact.Deactivate(); }
    }
    public void Teleport(Vector2 position)
    {
        _boss.transform.position = new Vector3(position.x, position.y, 0);
        Physics2D.SyncTransforms();
    }

    void Start()
    {
        _health = _player.GetComponent<Health>(); _playerBody = _player.GetComponent<Collider2D>();
        _playerStart = _player.transform.position; _bossStart = _boss.transform.position;
        _contact = _boss.GetComponent<Hitbox>();
        if (_contact == null) _contact = _boss.gameObject.AddComponent<Hitbox>();
        _contact.SetOwner(Team.Enemy, 1 << _player.gameObject.layer, false); _contact.SetArea(_bossBody);
        CreateIcon();
        var art = new GameObject("Figma boss art"); art.transform.SetParent(_boss.transform, false);
        _visual = art.AddComponent<DemoAppBossVisual>();
        _visual.Initialize(_boss, _bossBodySheet, _bossFaceSheet, _bossHandsSheet, _icon);
        _skill = SkillDefinition.CreateRuntime("付箋トラップ", _icon, SkillCategory.Creative, null, null);
        var hud = _camera.GetComponentInChildren<PixelHud>();
        if (hud != null) hud.BindTest(_boss, _icon, _skill, () => Mathf.Clamp01((_readyAt - Time.time) / 3.5f));
        _health.Died += OnPlayerDied; _boss.Filled += OnRestored;
        ResetDemo();
    }

    void CreateIcon()
    {
        _iconTexture = new Texture2D(24, 24, TextureFormat.RGBA32, false)
            { name = "Figma pixel icon", filterMode = FilterMode.Point };
        var pixels = new Color32[24 * 24];
        Color32[] colors = { new Color32(242,78,30,255), new Color32(255,114,98,255),
            new Color32(162,89,255,255),new Color32(26,188,254,255),new Color32(10,207,131,255) };
        int[] xs = { 8,15,8,15,8 }, ys = { 20,20,13,13,6 };
        for (int shape=0;shape<colors.Length;shape++)
            for(int y=0;y<24;y++) for(int x=0;x<24;x++)
            {
                int dx=x-xs[shape],dy=y-ys[shape];
                bool circle=dx*dx+dy*dy<=12;
                bool join=(shape==0||shape==2) && x>=xs[shape] && x<=11 && Mathf.Abs(dy)<=3;
                bool lowerJoin=shape==4 && x>=5 && x<=11 && y>=6 && y<=9;
                if(circle||join||lowerJoin) pixels[y*24+x]=colors[shape];
            }
        _iconTexture.SetPixels32(pixels); _iconTexture.Apply();
        _icon = Sprite.Create(_iconTexture,new Rect(0,0,24,24),Vector2.one*0.5f,GameArt.PixelsPerUnit);
        _icon.name = "Figma";
    }

    void ResetDemo()
    {
        ClearAttacks(); _boss.ResetGauge(); Teleport(_bossStart);
        SetVisibility(1); SetContactEnabled(true);
        _health.ResetHealth(); _player.CanControl=true; _player.SetVelocity(Vector2.zero);
        _player.GetComponent<KinematicBody2D>().Warp(_playerStart);
        _readyAt=0; _nextAttack=Time.time+2; _nextWarp=Time.time+9;
        _nextSticky=Time.time+1f; _stickyLane=-1;
        _manualAt=0; _warping=false;
    }

    void OnPlayerDied() { ClearAttacks(); SetContactEnabled(false); SetVisibility(1); _warping=false; }
    void OnRestored() { ClearAttacks(); SetContactEnabled(false); SetVisibility(1); _warping=false; Vfx.Smoke(_boss.transform.position); }

    void Update()
    {
        var kb=Keyboard.current;
        if(kb!=null&&kb.f1Key.wasPressedThisFrame) _controls=!_controls;
        if(kb!=null&&kb.rKey.wasPressedThisFrame) ResetDemo();
        if(_invincible&&!_health.IsDead) _health.SetInvincible(0.1f);
        if(!CanFight) return;
        if(kb!=null)
        {
            if(kb.oKey.wasPressedThisFrame) StickyNotes();
            if(kb.pKey.wasPressedThisFrame) Selection();
            if(kb.iKey.wasPressedThisFrame) BeginWarp();
            if(kb.digit1Key.wasPressedThisFrame) UseSkill();
        }
        // 付箋は選択ハンドル・移動と独立して、ゆっくり貼り続ける。
        if(_auto&&Time.time>=_nextSticky)
        {
            StickyNotes();
            _nextSticky=Time.time+(Enraged?1.1f:1.35f);
        }
        if(_auto&&!_warping)
        {
            if(Time.time>=_nextWarp) BeginWarp();
            if(!_warping&&Time.time>=_nextAttack)
            {
                Selection();
            }
        }
        UpdateWarp();
        for(int i=_attacks.Count-1;i>=0;i--)
        {
            FigmaDesignAttack attack=_attacks[i]; attack.Tick();
            if(attack.Finished) { Destroy(attack.gameObject); _attacks.RemoveAt(i); continue; }
            if(attack.Friendly&&!_bossBody.enabled) continue;
            Bounds target=attack.Friendly?BodyBounds:_playerBody.bounds;
            if(attack.Friendly&&attack.TryAttachTo(_boss.transform,target)) continue;
            if(!attack.Intersects(target)) continue;
            var info=new DamageInfo { Amount=attack.Friendly?2:1,GaugeGain=attack.Friendly?18:0,
                SourceTeam=attack.Friendly?Team.Player:Team.Enemy,HitPoint=target.center,
                Knockback=new Vector2(Mathf.Sign(target.center.x-attack.Anchor.x)*4f,4f),HitStun=0.2f };
            bool hit=attack.Friendly?_boss.TryTakeDamage(info):_health.TryTakeDamage(info);
            if(hit)
            {
                attack.Hit=true; Vfx.HitSpark(target.center);
                if(!CanFight) return;
            }
        }
    }

    bool BeginAttack()
    {
        if(!CanFight||_warping||Time.time<_manualAt||_attacks.Count>=16) return false;
        _manualAt=Time.time+0.35f; _nextAttack=Time.time+(Enraged?2.8f:3.4f);
        return true;
    }
    FigmaDesignAttack NewAttack(string name)
    {
        var attack=new GameObject(name).AddComponent<FigmaDesignAttack>(); _attacks.Add(attack); return attack;
    }
    void StickyNotes()
    {
        if(!CanFight||_attacks.Count>=20) return;
        int count=0;
        foreach(var attack in _attacks)
            if(attack.Kind==FigmaDesignAttack.DesignKind.StickyNote&&!attack.Finished) count++;
        if(count>=5) return;
        int lane=_stickyLane<0?Random.Range(0,3):Random.Range(0,2);
        if(_stickyLane>=0&&lane>=_stickyLane) lane++;
        for(int attempt=0;attempt<12;attempt++)
        {
            Vector2 position=FigmaDesignAttack.StickyPosition(lane,Random.value,Random.value);
            position=FigmaDesignAttack.ClampSpawnPoint(position,Vector2.one*0.8f);
            if(!PlacementIsClear(new Bounds(position,new Vector3(1.6f,1.6f,0)))) continue;
            bool crowded=false;
            foreach(var attack in _attacks)
                if(attack.Kind==FigmaDesignAttack.DesignKind.StickyNote&&!attack.Finished&&
                    Vector2.Distance(position,attack.Anchor)<2f) { crowded=true; break; }
            if(crowded) continue;
            NewAttack("FigJam sticky note").InitializeSticky(position,Random.Range(0,4),0);
            _stickyLane=lane;
            return;
        }
    }
    void Selection()
    {
        if(!BeginAttack()) return;
        Vector2 target=_playerBody.bounds.center;
        if(!TrySelectionPosition(target,out target)) { _nextAttack=Time.time+1f; return; }
        NewAttack("Figma selection handles").InitializeSelection(target);
        _nextAttack=Time.time+4.5f;
    }

    bool PlacementIsClear(Bounds area)
    {
        if(FigmaDesignAttack.OverlapsEnemy(area,BodyBounds)) return false;
        if(_warping)
        {
            Bounds destination=BodyBounds;
            destination.center+=_warpTarget-_boss.transform.position;
            if(FigmaDesignAttack.OverlapsEnemy(area,destination)) return false;
        }
        foreach(var hit in Physics2D.OverlapBoxAll(area.center,(Vector2)area.size+Vector2.one*1.2f,0,
            LayerMask.GetMask("Enemy")))
        {
            var enemy=hit.GetComponentInParent<IDamageable>();
            if(enemy!=null&&enemy.Team==Team.Enemy) return false;
        }
        return true;
    }

    bool TrySelectionPosition(Vector2 desired,out Vector2 result)
    {
        Vector2 half=FigmaDesignAttack.SelectionHalfSize;
        result=FigmaDesignAttack.ClampSpawnPoint(desired,half);
        if(PlacementIsClear(new Bounds(result,half*2f))) return true;
        float best=float.PositiveInfinity;
        for(int row=0;row<7;row++) for(int column=0;column<11;column++)
        {
            Vector2 point=new Vector2(Mathf.Lerp(-9.7f+half.x,9.7f-half.x,column/10f),
                Mathf.Lerp(0.15f+half.y,8.55f-half.y,row/6f));
            point=FigmaDesignAttack.ClampSpawnPoint(point,half);
            float distance=(point-desired).sqrMagnitude;
            if(distance>=best||!PlacementIsClear(new Bounds(point,half*2f))) continue;
            best=distance; result=point;
        }
        return !float.IsPositiveInfinity(best);
    }
    void UseSkill()
    {
        if(!CanFight||!_player.IsControllable||Time.time<_readyAt||_attacks.Count>20) return;
        int face=_player.FacingDirection;
        Vector2 center=_playerBody.bounds.center;
        Vector2 position=center+new Vector2(face*2f,0.1f);
        position.x=Mathf.Clamp(position.x,-9f,9f);
        NewAttack("Player sticky trap").InitializePlayerSticky(position);
        _readyAt=Time.time+3.5f;
    }

    void BeginWarp()
    {
        if(!CanFight||_warping) return;
        bool found=false;
        int firstHeight=Random.Range(0,2);
        for(int i=0;i<6;i++)
        {
            Vector3 candidate=new Vector3(i<2?(_boss.transform.position.x>0?-6f:6f):i<4?0f:
                (_boss.transform.position.x>0?6f:-6f),((i+firstHeight)&1)==0?1.25f:4.2f,0);
            Bounds body=BodyBounds; body.center+=candidate-_boss.transform.position;
            bool overlaps=false;
            foreach(var attack in _attacks)
                if(!attack.Friendly&&!attack.Finished&&FigmaDesignAttack.OverlapsEnemy(attack.PlacementBounds,body))
                { overlaps=true; break; }
            if(overlaps) continue;
            _warpTarget=candidate; found=true; break;
        }
        if(!found) { _nextWarp=Time.time+2f; return; }
        _warping=true; _warped=false; _warpAt=Time.time;
        _nextWarp=Time.time+Random.Range(8f,11f); _nextAttack=Mathf.Max(_nextAttack,Time.time+2f);
    }
    void UpdateWarp()
    {
        if(!_warping) return;
        float t=Time.time-_warpAt;
        if(t>=1.4f) { SetVisibility(1); SetContactEnabled(true); _warping=false; return; }
        if(t>=0.65f) SetContactEnabled(false);
        if(t<0.85f) SetVisibility(1-Mathf.InverseLerp(0.65f,0.85f,t));
        else
        {
            if(!_warped) { Teleport(_warpTarget); _warped=true; }
            SetVisibility(Mathf.InverseLerp(1.1f,1.35f,t));
        }
    }
    void ClearAttacks() { foreach(var attack in _attacks) if(attack!=null) Destroy(attack.gameObject); _attacks.Clear(); }

    void OnGUI()
    {
        if(_health==null) return;
        PixelGui.Begin(false);
        if(_warping)
        {
            Vector3 point=_camera.WorldToScreenPoint(_warpTarget);
            Vector2 size=(Vector2)BodyBounds.size*(Screen.height/(_camera.orthographicSize*2f))/PixelGui.Scale;
            PixelGui.DashedBorder(new Rect(point.x/PixelGui.Scale-size.x/2,PixelGui.Height-point.y/PixelGui.Scale-size.y/2,size.x,size.y),PixelGui.Lilac);
        }
        if(!CanFight) PixelGui.Text(new Rect(10,PixelGui.Height-40,180,30),"R：リセット",12,PixelGui.White);
        if(_controls)
        {
            float y=PixelGui.Height-78; PixelGui.Fill(new Rect(0,y,PixelGui.Width,78),PixelGui.Ink);
            PixelGui.Text(new Rect(12,y+3,PixelGui.Width-24,20),"A/D 移動　Space ジャンプ　Shift ダッシュ　J 攻撃　1 付箋トラップ　R リセット",12,PixelGui.White);
            if(GUI.Button(new Rect(12,y+27,145,25),"O：付箋を貼る")) StickyNotes();
            if(GUI.Button(new Rect(165,y+27,125,25),"P：選択ハンドル")) Selection();
            if(GUI.Button(new Rect(298,y+27,110,25),"I：ワープ")) BeginWarp();
            _auto=GUI.Toggle(new Rect(12,y+54,110,24),_auto,"自動行動");
            _invincible=GUI.Toggle(new Rect(140,y+54,140,24),_invincible,"無敵で試す");
        }
        PixelGui.End();
    }
    void OnDestroy()
    {
        if(_health!=null) _health.Died-=OnPlayerDied;
        if(_boss!=null) _boss.Filled-=OnRestored;
        ClearAttacks();
        if(_visual!=null) Destroy(_visual.gameObject);
        if(_skill!=null) Destroy(_skill);
        if(_icon!=null) Destroy(_icon);
        if(_iconTexture!=null) Destroy(_iconTexture);
    }
}
