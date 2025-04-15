using System;
using System.Collections.Generic;
using System.Linq;
using static CriWare.CriAtomEx;
using CriWare;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CRISound
{
    public class CuePlayAtomExPlayer
    {
        static CuePlayAtomExPlayer _instance = new();
        public static CuePlayAtomExPlayer Instance => _instance;

        private const int SoundTypeCount = 3;
        private SoundPlayer[] _soundPlayer = new SoundPlayer[SoundTypeCount];

        private SoundPlayer _bgmPlayer;
        private SEPlayerWith3D _sePlayer;
        private SoundPlayer _voicePlayer;

        private CuePlayAtomExPlayer()
        {
            _soundPlayer[(int)SoundType.BGM] = _bgmPlayer = new SoundPlayer(SoundType.BGM);
            _soundPlayer[(int)SoundType.SE] = _sePlayer = new SEPlayerWith3D();
            _soundPlayer[(int)SoundType.Voice] = _voicePlayer = new SoundPlayer(SoundType.Voice);
        }

        public static SEPlayerWith3D SE => _instance._sePlayer;

        private CueInfo[] _cueInfoList;
        private CriAtomExPlayer _atomExPlayer;
        private CriAtomExAcb _atomExAcb;

        private const int AtomSourceBuffer = 10;

        private bool _isReady = false;
        private readonly Dictionary<string, SoundDic> _soundDic = new();
        private List<Tuple<SoundType, string, string>> _defaultSoundList = new();

        public bool IsReady => _isReady;

        public static void Initialize()
        {
            Debug.Log("Initializing CuePlayAtomExPlayer");
            _instance.LoadCueSheet();
        }

        private void OnDestroy()
        {
            foreach (var player in _soundPlayer)
            {
                player.Dispose();
            }
        }
        
        private async void LoadCueSheet()
        {
            // CRIAtomの処理
            var criAtom = GameObject.FindObjectOfType<CriAtom>();
            // CRIが見つからなければ動的に生成する
            if (criAtom == null)
            {
                _isReady = false;
                // CRIのロード(あとでAddressableに変更)
                var obj = Resources.Load<GameObject>("CRIObject");
                GameObject.Instantiate(obj);
                criAtom = GameObject.FindObjectOfType<CriAtom>();
            }

            if (_isReady)
                return;

            // cueシートファイルのロード待ち
            await UniTask.WaitUntil(() => criAtom.cueSheets.All(cs => cs.IsLoading == false));

            // cue情報の取得
            foreach (var sheet in criAtom.cueSheets)
            {
                _soundDic.Add(sheet.name, new SoundDic(sheet.acb));
            }

            _isReady = true;

            foreach (var player in _soundPlayer)
            {
                player.SetUp();
                player.SetVolume(1.0f);
            }

            foreach (var s in _defaultSoundList)
            {
                _soundPlayer[(int)s.Item1].Play(s.Item2, s.Item3);
            }

            _defaultSoundList.Clear();
        }

        public void PlayQueue(SoundType type, string acb, string name)
        {
            _instance._defaultSoundList.Add(new Tuple<SoundType, string, string>(type, acb, name));
        }

        private class SoundDic
        {
            private CriAtomExAcb _atomExAcb;
            private Dictionary<string, CueInfo> _cueInfoDic = new();

            public SoundDic(CriAtomExAcb acb)
            {
                _atomExAcb = acb;
                foreach (var cueInfo in acb.GetCueInfoList())
                {
                    _cueInfoDic.Add(cueInfo.name, cueInfo);
                }
            }

            public CriAtomExAcb GetAcb()
            {
                return _atomExAcb;
            }

            public CueInfo GetCueInfo(string cueName)
            {
                return _cueInfoDic[cueName];
            }
        }

        public class SoundPlayer
        {
            private SoundType _type;
            private float _volume = 1.0f;
            protected CriAtomExPlayer _atomExPlayer;

            public bool IsPlaying => _atomExPlayer.GetStatus() == CriAtomExPlayer.Status.Playing;

            public CriAtomExPlayer Player => _atomExPlayer;

            public SoundPlayer(SoundType type)
            {
                _type = type;
            }

            public virtual void SetUp()
            {
                _atomExPlayer = new CriAtomExPlayer();
            }

            public virtual void Dispose()
            {
                _atomExPlayer.Dispose();
            }

            public virtual void SetVolume(float volume)
            {
                _volume = volume;
                _atomExPlayer.SetVolume(_volume);
            }

            public virtual CriAtomExPlayback Play(string cueSheet, string cueName, float delay = 0.0f)
            {
                if (!_instance.IsReady)
                {
                    _instance.PlayQueue(_type, cueSheet, cueName);
                    return default;
                }

                CueInfo info = _instance._soundDic[cueSheet].GetCueInfo(cueName);
                _atomExPlayer.SetCue(_instance._soundDic[cueSheet].GetAcb(), info.id);
                _atomExPlayer.SetPreDelayTime(delay);
                return _atomExPlayer.Start();
            }

            public virtual void Stop()
            {
                _atomExPlayer.Stop();
            }
        }

        public class SEPlayerWith3D : SoundPlayer
        {
            /// <summary>3Dサウンド再生用(単一)</summary>
            public class Sound3D
            {
                protected CriAtomEx3dSource _source = new();
                protected CriAtomExPlayer _atomExPlayer3D = new();

                public bool IsBusy => _atomExPlayer3D.GetStatus() == CriAtomExPlayer.Status.Playing;

                public void Dispose()
                {
                    _atomExPlayer3D.Dispose();
                    _source.Dispose();
                }

                public void Play3D(Vector3 playPos, string cueSheet, string cueName)
                {
                    _source.SetPosition(playPos.x, playPos.y, playPos.z);
                    _source.Update();

                    CueInfo info = _instance._soundDic[cueSheet].GetCueInfo(cueName);
                    _atomExPlayer3D.SetCue(_instance._soundDic[cueSheet].GetAcb(), info.id);
                    _atomExPlayer3D.SetPanType(CriAtomEx.PanType.Pos3d);
                    _atomExPlayer3D.Set3dSource(_source);
                    _atomExPlayer3D.UpdateAll();
                    _atomExPlayer3D.Start();
                }
            }

            private CriAtomEx3dListener _listener;
            Sound3D[] _sound3Ds = new Sound3D[AtomSourceBuffer];

            public SEPlayerWith3D() : base(SoundType.SE)
            {
            }

            public override void SetUp()
            {
                _listener = new CriAtomEx3dListener();
                _atomExPlayer = new CriAtomExPlayer();
                for (int i = 0; i < AtomSourceBuffer; ++i)
                {
                    _sound3Ds[i] = new Sound3D();
                }
            }

            public override void Dispose()
            {
                base.Dispose();
                for (int i = 0; i < AtomSourceBuffer; ++i)
                {
                    _sound3Ds[i].Dispose();
                }
            }

            private Sound3D GetPlayer()
            {
                for (int i = 0; i < AtomSourceBuffer; ++i)
                {
                    if (_sound3Ds[i].IsBusy) continue;
                    return _sound3Ds[i];
                }

                return null;
            }

            public Sound3D Play3D(Vector3 playPos, string cueSheet, string cueName)
            {
                Sound3D player = GetPlayer();
                if (player == null)
                {
                    Debug.LogWarning("3D音声の再生上限です");
                    return null;
                }

                player.Play3D(playPos, cueSheet, cueName);
                return player;
            }
        }
    }
}