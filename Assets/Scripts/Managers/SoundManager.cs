using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        //UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [SerializeField] AudioSource _bgmAudioSource;
    [SerializeField] AudioSource _seAudioSource;
    [SerializeField] List<BGMData> _bgmSoundDatas;
    [SerializeField] List<SEData> _seSoundDatas;

    BGMData _currentBGM;
    SEData _currentSE;

    [Range(0, 1)] public float masterVolume = 1;
    [Range(0, 1)] public float bgmMasterVolume = 1;
    [Range(0, 4)] public float seMasterVolume = 2;

    private void Start()
    {
        // AdjustmentBGM();
        // AdjustmentSE();
    }

    /// <summary>
    /// BGMを流すメソッド
    /// </summary>
    /// <param name="bgm">呼びたいBGM</param>
    public void PlayBGM(BGMData.BGMTYPE bgm)
    {
        BGMData data = _bgmSoundDatas.Find(data => data.bgm == bgm);
        _bgmAudioSource.clip = data.audioClip;
        _bgmAudioSource.volume = data.volume * bgmMasterVolume * masterVolume;
        _bgmAudioSource.loop = true;
        _bgmAudioSource.Play();
        _currentBGM = data;
        AdjustmentBGM();
        AdjustmentSE();
    }

    public void StopBGM()
    {
        _bgmAudioSource.Stop();
    }


    /// <summary>
    /// 音量調整
    /// </summary>
    public void AdjustmentBGM()
    {
        if (_currentBGM == null) return; // BGMが設定されていない場合は何もしない
        _bgmAudioSource.volume = _currentBGM.volume * bgmMasterVolume * masterVolume;
    }

    public void AdjustmentSE()
    {
        if (_currentSE == null) return; // SEが設定されていない場合は何もしない
        _seAudioSource.volume = _currentSE.volume * seMasterVolume * masterVolume;
    }

    /// <summary>
    /// SEを流すメソッド
    /// </summary>
    /// <param name="se"></param>
    public void PlaySE(SEData.SETYPE se)
    {
        SEData data = _seSoundDatas.Find(data => data.se == se);
        _currentSE = data;
        _seAudioSource.volume = data.volume * seMasterVolume * masterVolume;
        _seAudioSource.PlayOneShot(data.audioClip);
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = volume;
        AdjustmentBGM();
        AdjustmentSE();
    }
    public void SetBGMVolume(float volume)
    {
        bgmMasterVolume = volume;
        AdjustmentBGM();
    }
    public void SetSEVolume(float volume)
    {
        seMasterVolume = volume;
        AdjustmentSE();
    }

    [System.Serializable]
    public class BGMData
    {
        public enum BGMTYPE
        {
            //ここの部分がラベルになる
            Title = 0,
            SkillSelect = 1,
            Mating = 2,
            InGame = 3,
            CV_InGame = 4,
        }

        public BGMTYPE bgm;
        public AudioClip audioClip;
        [Range(0, 1)] public float volume = 1f;
    }

    [System.Serializable]
    public class SEData
    {
        public enum SETYPE
        {
            //ここの部分がラベルになる
            CrystalBreak = 0,
            CrystalDamage = 1,
            ShingleShot = 2,
            Beam = 3,
            PlayerDamage = 4,
            SceneMove = 5,
            Button = 6,
            SelectMove = 7,
            Stealth = 8,
            Winner = 9,
            Loser = 10,
            HomingShot = 11,
            Missile = 12,
            Invincible = 13,
            CV_Spike = 14,
            CV_GlassCrack = 15,
            CV_Miss = 16,
            CV_GlassShatter = 17,
            CV_Clear = 18,
            CV_CursorAppear = 19,
        }

        public SETYPE se;
        public AudioClip audioClip;
        [Range(0, 1)] public float volume = 1f;
    }
}
