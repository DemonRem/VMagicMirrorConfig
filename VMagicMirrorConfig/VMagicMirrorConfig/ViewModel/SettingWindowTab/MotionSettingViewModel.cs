﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Baku.VMagicMirrorConfig
{
    public class MotionSettingViewModel : SettingViewModelBase
    {
        internal MotionSettingViewModel(MotionSettingSync model, IMessageSender sender, IMessageReceiver receiver) : base(sender)
        {
            _model = model;

            ResetFaceBasicSettingCommand = new ActionCommand(
                () => SettingResetUtils.ResetSingleCategoryAsync(ResetFaceBasicSetting)
                );
            ResetFaceEyeSettingCommand = new ActionCommand(
                () => SettingResetUtils.ResetSingleCategoryAsync(_model.ResetFaceEyeSetting)
                );
            ResetFaceBlendShapeSettingCommand = new ActionCommand(
                () => SettingResetUtils.ResetSingleCategoryAsync(_model.ResetFaceBlendShapeSetting)
                );
            ResetArmMotionSettingCommand = new ActionCommand(
                 () => SettingResetUtils.ResetSingleCategoryAsync(_model.ResetArmSetting)
                 );
            ResetHandMotionSettingCommand = new ActionCommand(
                () => SettingResetUtils.ResetSingleCategoryAsync(_model.ResetHandSetting)
                );
            ResetWaitMotionSettingCommand = new ActionCommand(
                () => SettingResetUtils.ResetSingleCategoryAsync(_model.ResetWaitMotionSetting)
                );

            CalibrateFaceCommand = new ActionCommand(() => SendMessage(MessageFactory.Instance.CalibrateFace()));

            ShowMicrophoneVolume = new RProperty<bool>(false, b =>
            {
                SendMessage(MessageFactory.Instance.SetMicrophoneVolumeVisibility(b));
                if (!b)
                {
                    MicrophoneVolumeValue.Value = 0;
                }
            });

            _model.EyeBoneRotationScale.PropertyChanged += (_, __) => UpdateEyeRotRangeText();
            _model.EnableLipSync.PropertyChanged += (_, __) =>
            {
                if (!_model.EnableLipSync.Value)
                {
                    ShowMicrophoneVolume.Value = false;
                }
            };

            //両方trueのときだけポインターを表示したいので、それのチェック
            _model.EnablePresenterMotion.PropertyChanged += (_, __) => UpdatePointerVisibility();
            _model.ShowPresentationPointer.PropertyChanged += (_, __) => UpdatePointerVisibility();
            //通常発生しないが、VMの初期化時点でポインター表示が必要ならそうする
            UpdatePointerVisibility();

            UpdateEyeRotRangeText();

            receiver.ReceivedCommand += OnReceivedCommand;
            ShowInstallPathWarning.Value = InstallPathChecker.HasMultiByteCharInInstallPath();
        }

        private readonly MotionSettingSync _model;

        private void OnReceivedCommand(object? sender, CommandReceivedEventArgs e)
        {
            switch (e.Command)
            {
                case ReceiveMessageNames.SetCalibrationFaceData:
                    //NOTE: Unity側がすでにこの値を把握しているので、投げ返す必要がない
                    _model.CalibrateFaceData.SilentSet(e.Args);
                    break;
                case ReceiveMessageNames.AutoAdjustResults:
                    _model.SetAutoAdjustResults(e.Args);
                    break;
                case ReceiveMessageNames.MicrophoneVolumeLevel:
                    if (ShowMicrophoneVolume.Value && int.TryParse(e.Args, out int i))
                    {
                        MicrophoneVolumeValue.Value = i;
                    }
                    break;
                case ReceiveMessageNames.ExtraBlendShapeClipNames:
                    try
                    {
                        //いちおう信頼はするけどIPCだし…みたいな書き方。FaceSwitchと同じ
                        var names = e.Args
                            .Split(',')
                            .Where(w => !string.IsNullOrEmpty(w))
                            .ToArray();
                        _blendShapeNameStore.Refresh(names);
                    }
                    catch(Exception ex)
                    {
                        LogOutput.Instance.Write(ex);
                    }
                    break;
                default:
                    break;
            }
        }

        public async Task InitializeDeviceNamesAsync()
        {
            string microphones = await SendQueryAsync(MessageFactory.Instance.MicrophoneDeviceNames());
            Application.Current.MainWindow.Dispatcher.Invoke(() =>
            {
                _writableMicrophoneDeviceNames.Clear();
                foreach (var deviceName in microphones.Split('\t'))
                {
                    _writableMicrophoneDeviceNames.Add(deviceName);
                }
            });

            string cameras = await SendQueryAsync(MessageFactory.Instance.CameraDeviceNames());
            Application.Current.MainWindow.Dispatcher.Invoke(() =>
            {
                _writableCameraDeviceNames.Clear();
                foreach (var deviceName in cameras.Split('\t'))
                {
                    _writableCameraDeviceNames.Add(deviceName);
                }
            });
        }

        #region Full Body 

        public RProperty<bool> EnableNoHandTrackMode => _model.EnableNoHandTrackMode;

        #endregion

        #region Face

        public RProperty<bool> EnableFaceTracking => _model.EnableFaceTracking;

        public RProperty<bool> ShowInstallPathWarning { get; } = new RProperty<bool>(false);

        public RProperty<bool> AutoBlinkDuringFaceTracking => _model.AutoBlinkDuringFaceTracking;
        public RProperty<bool> EnableBodyLeanZ => _model.EnableBodyLeanZ;
        public RProperty<bool> EnableBlinkAdjust => _model.EnableBlinkAdjust;
        public RProperty<bool> EnableVoiceBasedMotion => _model.EnableVoiceBasedMotion;
        public RProperty<bool> DisableFaceHorizontalFlip => _model.DisableFaceTrackingHorizontalFlip;
        public RProperty<bool> EnableWebCamHighPowerMode => _model.EnableWebCamHighPowerMode;
        public RProperty<bool> EnableImageBasedHandTracking => _model.EnableImageBasedHandTracking;

        public RProperty<string> CameraDeviceName => _model.CameraDeviceName;

        private readonly ObservableCollection<string> _writableCameraDeviceNames
            = new ObservableCollection<string>();
        private ReadOnlyObservableCollection<string>? _cameraDeviceNames = null;
        public ReadOnlyObservableCollection<string> CameraDeviceNames
            => _cameraDeviceNames ??= new ReadOnlyObservableCollection<string>(_writableCameraDeviceNames);

        public ActionCommand CalibrateFaceCommand { get; }

        public RProperty<string> CalibrateFaceData => _model.CalibrateFaceData;
        public RProperty<int> FaceDefaultFun => _model.FaceDefaultFun;

        private readonly FaceMotionBlendShapeNameStore _blendShapeNameStore = new FaceMotionBlendShapeNameStore();
        public ReadOnlyObservableCollection<string> BlendShapeNames => _blendShapeNameStore.BlendShapeNames;

        //TODO: この2つはnullableにしたうえでモデルとうまくくっつけたい、ような。ほんとはnull自体不許可にしたいけど…
        private string? _faceNeutralClip = "";
        public string? FaceNeutralClip
        {
            get => _faceNeutralClip;
            set
            {
                if (_faceNeutralClip != value)
                {
                    _faceNeutralClip = value;
                    RaisePropertyChanged();
                    _blendShapeNameStore.Refresh(FaceNeutralClip, FaceOffsetClip);
                    SendMessage(MessageFactory.Instance.FaceNeutralClip(FaceNeutralClip ?? ""));
                }
            }
        }

        private string? _faceOffsetClip = "";
        public string? FaceOffsetClip
        {
            get => _faceOffsetClip;
            set
            {
                if (_faceOffsetClip != value)
                {
                    _faceOffsetClip = value;
                    RaisePropertyChanged();
                    _blendShapeNameStore.Refresh(FaceNeutralClip, FaceOffsetClip);
                    SendMessage(MessageFactory.Instance.FaceOffsetClip(FaceOffsetClip ?? ""));
                }
            }
        }

        #endregion

        #region Eye

        public RProperty<bool> UseLookAtPointNone => _model.UseLookAtPointNone;
        public RProperty<bool> UseLookAtPointMousePointer => _model.UseLookAtPointMousePointer;
        public RProperty<bool> UseLookAtPointMainCamera => _model.UseLookAtPointMainCamera;
        public RProperty<int> EyeBoneRotationScale => _model.EyeBoneRotationScale;

        //NOTE: ちょっと作法が悪いけど、「-7.0 ~ +7.0」のようなテキストでViewにわたす
        private const double EyeRotDefaultRange = 7.0;
        private string _eyeRotRangeText = $"-{EyeRotDefaultRange:0.00} ~ +{EyeRotDefaultRange:0.00}";
        public string EyeRotRangeText
        {
            get => _eyeRotRangeText;
            private set => SetValue(ref _eyeRotRangeText, value);
        }
        private void UpdateEyeRotRangeText()
        {
            double range = EyeRotDefaultRange * EyeBoneRotationScale.Value * 0.01;
            EyeRotRangeText =  $"-{range:0.00} ~ +{range:0.00}";
        }

        #endregion

        #region Mouth

        public RProperty<bool> EnableLipSync => _model.EnableLipSync;
        public RProperty<string> LipSyncMicrophoneDeviceName => _model.LipSyncMicrophoneDeviceName;
        public RProperty<int> MicrophoneSensitivity => _model.MicrophoneSensitivity;

        public RProperty<bool> ShowMicrophoneVolume { get; }

        //NOTE: 0 ~ 20が無音、21~40が適正、41~50がデカすぎになる。これはUnity側がそういう整形をしてくれる
        public RProperty<int> MicrophoneVolumeValue { get; } = new RProperty<int>(0);  

        private readonly ObservableCollection<string> _writableMicrophoneDeviceNames
            = new ObservableCollection<string>();
        private ReadOnlyObservableCollection<string>? _microphoneDeviceNames = null;
        public ReadOnlyObservableCollection<string> MicrophoneDeviceNames
            => _microphoneDeviceNames ??= new ReadOnlyObservableCollection<string>(_writableMicrophoneDeviceNames);

        #endregion

        #region Arm

        public RProperty<bool> EnableHidArmMotion => _model.EnableHidArmMotion;
        public RProperty<bool> EnableHidRandomTyping => _model.EnableHidRandomTyping;
        public RProperty<bool> EnableShoulderMotionModify => _model.EnableShoulderMotionModify;
        public RProperty<bool> EnableHandDownTimeout => _model.EnableHandDownTimeout;
        public RProperty<int> WaistWidth => _model.WaistWidth;
        public RProperty<int> ElbowCloseStrength => _model.ElbowCloseStrength;

        public RProperty<bool> EnableFpsAssumedRightHand => _model.EnableFpsAssumedRightHand;

        public RProperty<bool> EnablePresenterMotion => _model.EnablePresenterMotion;
        public RProperty<bool> ShowPresentationPointer => _model.ShowPresentationPointer;
        public RProperty<int> PresentationArmRadiusMin => _model.PresentationArmRadiusMin;

        private void UpdatePointerVisibility() 
            => LargePointerController.Instance.UpdateVisibility(_model.PointerVisible);

        #endregion

        #region Hand

        public RProperty<int> LengthFromWristToTip => _model.LengthFromWristToTip;
        public RProperty<int> HandYOffsetBasic => _model.HandYOffsetBasic;
        public RProperty<int> HandYOffsetAfterKeyDown => _model.HandYOffsetAfterKeyDown;

        #endregion

        #region Wait

        public RProperty<bool> EnableWaitMotion => _model.EnableWaitMotion;
        public RProperty<int> WaitMotionScale => _model.WaitMotionScale;
        public RProperty<int> WaitMotionPeriod => _model.WaitMotionPeriod;

        #endregion

        #region Reset API

        public ActionCommand ResetFaceBasicSettingCommand { get; }
        public ActionCommand ResetFaceEyeSettingCommand { get; }
        public ActionCommand ResetFaceBlendShapeSettingCommand { get; }
        public ActionCommand ResetArmMotionSettingCommand { get; }
        public ActionCommand ResetHandMotionSettingCommand { get; }
        public ActionCommand ResetWaitMotionSettingCommand { get; }

        private void ResetFaceBasicSetting()   
        {
            _model.ResetFaceBasicSetting();
            //NOTE: 保存されない値だけど一応やる
            ShowMicrophoneVolume.Value = false;
        }

        #endregion
    }


    /// <summary>
    /// ブレンドシェイプクリップ名を一覧保持するクラスです。
    /// ExTrackerとクラスが分かれてるのは、クリップ名の持ち方がちょっと違うためです。
    /// </summary>
    public class FaceMotionBlendShapeNameStore
    {
        public FaceMotionBlendShapeNameStore()
        {
            BlendShapeNames = new ReadOnlyObservableCollection<string>(_blendShapeNames);
            var defaultNames = LoadDefaultNames();
            for (int i = 0; i < defaultNames.Length; i++)
            {
                _blendShapeNames.Add(defaultNames[i]);
            }
        }

        private readonly ObservableCollection<string> _blendShapeNames = new ObservableCollection<string>();
        /// <summary> UIに表示するのが妥当と考えられるブレンドシェイプクリップ名の一覧です。 </summary>
        public ReadOnlyObservableCollection<string> BlendShapeNames { get; }

        //Unityで読み込まれたキャラクターのブレンドシェイプ名の一覧です。
        //NOTE: この値は標準ブレンドシェイプ名を含んでいてもいなくてもOK。ただし現行動作では標準ブレンドシェイプ名は含まない。
        private string[] _avatarClipNames = new string[0];

        //設定ファイルから読み込んだ設定で使われていたブレンドシェイプ名の一覧。
        //NOTE: この値に標準ブレンドシェイプ名とそうでないのが混在することがあるが、それはOK
        private string[] _settingUsedNames = new string[0];

        /// <summary>
        /// ロードされたVRMの標準以外のブレンドシェイプ名を指定して、名前一覧を更新します。
        /// </summary>
        /// <param name="avatarBlendShapeNames"></param>
        public void Refresh(string[] avatarBlendShapeNames)
        {
            //なんとなく正格評価しておく(値コピーの方が安心なので…
            _avatarClipNames = avatarBlendShapeNames.ToArray();
            RefreshInternal();
        }

        /// <summary>
        /// ファイルからロードされたはずの設定を参照し、その中で使われているブレンドシェイプ名を参考にして名前一覧を更新します。
        /// </summary>
        /// <param name="neutralClipName"></param>
        /// <param name="offsetClipName"></param>
        public void Refresh(string? neutralClipName, string? offsetClipName)
        {
            //NOTE: Refreshの挙動上、ここで""だけを2つ入れたりしても大丈夫。
            _settingUsedNames = new string[] { neutralClipName ?? "", offsetClipName ?? "" };
            RefreshInternal();
        }

        private void RefreshInternal()
        {
            //理想の並び: デフォルトのやつ一覧、今ロードしたVRMにある名前一覧、(今ロードしたVRMにはないけど)設定で使ってる名前一覧
            var newNames = LoadDefaultNames().ToList();
            int defaultSetLength = newNames.Count;
            foreach (var nameInModel in _avatarClipNames)
            {
                if (!newNames.Contains(nameInModel))
                {
                    newNames.Add(nameInModel);
                }
            }

            foreach (var nameInSetting in _settingUsedNames)
            {
                if (!newNames.Contains(nameInSetting))
                {
                    newNames.Add(nameInSetting);
                }
            }

            var newNameArray = newNames.ToArray();

            //NOTE: ここポイントで、既存要素は消さないよう慎重に並べ替えます(消すとOC<T>の怒りを買ってUI側の要素選択に悪影響が出たりするので…)
            for (int i = defaultSetLength; i < newNameArray.Length; i++)
            {
                if (_blendShapeNames.Contains(newNameArray[i]))
                {
                    int currentIndex = _blendShapeNames.IndexOf(newNameArray[i]);
                    if (currentIndex != i)
                    {
                        //もう入ってる値だが、場所を入れ替えたいケース
                        _blendShapeNames.Move(currentIndex, i);
                    }
                }
                else
                {
                    //そもそも入ってないケース
                    _blendShapeNames.Insert(i, newNameArray[i]);
                }
            }

            //OC<T>側のほうが配列が長い場合、ハミ出た分は余計なやつなので消しちゃってOK
            while (_blendShapeNames.Count > newNameArray.Length)
            {
                _blendShapeNames.RemoveAt(newNameArray.Length);
            }
        }

        private static string[] LoadDefaultNames()
        {
            return new string[]
            {
                //「なし」があるのが大事。これによって、条件に合致しても何のブレンドシェイプを起動しない！という事ができる。
                "",
                "Joy",
                "Angry",
                "Sorrow",
                "Fun",

                "A",
                "I",
                "U",
                "E",
                "O",

                "Neutral",
                "Blink",
                "Blink_L",
                "Blink_R",

                "LookUp",
                "LookDown",
                "LookLeft",
                "LookRight",
            };
        }
    }

}
