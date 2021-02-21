﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Baku.VMagicMirrorConfig
{
    public class WordToMotionSettingViewModel : SettingViewModelBase
    {
        internal WordToMotionSettingViewModel(WordToMotionSettingSync model, IMessageSender sender, IMessageReceiver receiver) : base(sender)
        {
            _model = model;
            Items = new ReadOnlyObservableCollection<WordToMotionItemViewModel>(_items);
            CustomMotionClipNames = new ReadOnlyObservableCollection<string>(_customMotionClipNames);
            Devices = WordToMotionDeviceItem.LoadAvailableItems();

            AddNewItemCommand = new ActionCommand(() => model.AddNewItem());
            OpenKeyAssignmentEditorCommand = new ActionCommand(() => OpenKeyAssignmentEditor());
            ResetByDefaultItemsCommand = new ActionCommand(
                () => SettingResetUtils.ResetSingleCategoryAsync(LoadDefaultItems)
                );

            _model.SelectedDeviceType.PropertyChanged += (_, __) =>
            {
                SelectedDevice = Devices.FirstOrDefault(d => d.Index == _model.SelectedDeviceType.Value);
                EnableWordToMotion.Value = _model.SelectedDeviceType.Value != WordToMotionSetting.DeviceTypes.None;
            };
            SelectedDevice = Devices.FirstOrDefault(d => d.Index == _model.SelectedDeviceType.Value);
            //NOTE: シリアライズ文字列はどのみち頻繁に更新せねばならない
            //(並び替えた時とかもUnityにデータ送るために更新がかかる)ので、そのタイミングを使う
            _model.MidiNoteMapString.PropertyChanged += (_, __) =>
            {
                if (!_model.IsLoading)
                {
                    LoadMidiSettingItems();
                }
            };
            _model.ItemsContentString.PropertyChanged += (_, __) =>
            {
                if (!_model.IsLoading)
                {
                    LoadMotionItems();
                }
            };
            _model.Loaded += (_, __) =>
            {
                LoadMidiSettingItems();
                LoadMotionItems();
            };

            //TODO: この辺のSenderとかReceiverがモデル感あるよね
            _previewDataSender = new WordToMotionItemPreviewDataSender(sender);
            _previewDataSender.PrepareDataSend +=
                (_, __) => _dialogItem?.WriteToModel(_previewDataSender.MotionRequest);
            receiver.ReceivedCommand += OnReceiveCommand;
            MidiNoteReceiver = new MidiNoteReceiver(receiver);
            MidiNoteReceiver.Start();

            LoadDefaultItemsIfInitialStart();

            //TODO: しょっぱなで一回モデルのシリアライズされたデータをロードしようと試みる方が健全かも
        }

        private readonly WordToMotionSettingSync _model;
        private readonly WordToMotionItemPreviewDataSender _previewDataSender;
        private WordToMotionItemViewModel? _dialogItem;

        internal MidiNoteReceiver MidiNoteReceiver { get; }

        /// <summary>直近で読み込んだモデルに指定されている、VRM標準以外のブレンドシェイプ名の一覧を取得します。</summary>
        public IReadOnlyList<string> LatestAvaterExtraClipNames => _latestAvaterExtraClipNames;

        private string[] _latestAvaterExtraClipNames = new string[0];

        private void OnReceiveCommand(object? sender, CommandReceivedEventArgs e)
        {
            if (e.Command != ReceiveMessageNames.ExtraBlendShapeClipNames)
            {
                return;
            }

            //やることは2つ: 
            // - 知らない名前のブレンドシェイプが飛んできたら記憶する
            // - アバターが持ってるExtraなクリップ名はコレですよ、というのを明示的に与える
            _latestAvaterExtraClipNames = e.Args
                .Split(',')
                .Where(n => !string.IsNullOrEmpty(n))
                .ToArray();

            bool hasNewBlendShape = false;
            foreach (var name in _latestAvaterExtraClipNames
                .Where(n => !ExtraBlendShapeClipNames.Contains(n))
                )
            {
                hasNewBlendShape = true;
                ExtraBlendShapeClipNames.Add(name);
            }

            if (hasNewBlendShape)
            {
                //新しい名称のクリップを子要素側に反映
                foreach (var item in _items)
                {
                    item.CheckBlendShapeClipNames();
                }
            }

            foreach (var item in _items)
            {
                item.CheckAvatarExtraClips();
            }
        }

        public async Task InitializeCustomMotionClipNamesAsync()
        {
            var rawClipNames = await SendQueryAsync(MessageFactory.Instance.GetAvailableCustomMotionClipNames());
            var clipNames = rawClipNames.Split('\t');
            foreach (var name in clipNames)
            {
                _customMotionClipNames.Add(name);
            }
        }

        private readonly ObservableCollection<string> _customMotionClipNames = new ObservableCollection<string>();
        public ReadOnlyObservableCollection<string> CustomMotionClipNames { get; }

        public RPropertyMin<bool> EnableWordToMotion { get; } = new RPropertyMin<bool>(true);

        #region デバイスをWord to Motionに割り当てる設定

        public WordToMotionDeviceItem[] Devices { get; }

        private WordToMotionDeviceItem? _selectedDevice = null;
        public WordToMotionDeviceItem? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (_selectedDevice == value)
                {
                    return;
                }
                _selectedDevice = value;
                RaisePropertyChanged();
                _model.SelectedDeviceType.Value = _selectedDevice?.Index ?? WordToMotionSetting.DeviceTypes.None;
            }
        }

        #endregion

        //NOTE: 「UIに出さないけど保存はしたい」系のやつで、キャラロード時にUnityから勝手に送られてくる、という想定
        public List<string> ExtraBlendShapeClipNames { get; set; } = new List<string>();

        public ReadOnlyObservableCollection<WordToMotionItemViewModel> Items { get; }
        private readonly ObservableCollection<WordToMotionItemViewModel> _items
            = new ObservableCollection<WordToMotionItemViewModel>();

        public MidiNoteToMotionMapViewModel MidiNoteMap { get; private set; }
            = new MidiNoteToMotionMapViewModel(MidiNoteToMotionMap.LoadDefault());

        public RPropertyMin<bool> EnablePreview => _model.EnablePreview;

        /// <summary>Word to Motionのアイテム編集を開始した時すぐプレビューを開始するかどうか。普通は即スタートでよい</summary>
        public bool EnablePreviewWhenStartEdit { get; set; } = true;

        /// <summary>モデルが持ってるWord to MotionなりMidiキーマッピングなりの情報をVMにコピーします。</summary>
        public void LoadSerializedItems()
        {
            LoadMotionItems();
            LoadMidiSettingItems();
        }

        private void LoadMotionItems()
        {
            _items.Clear();

            var modelItems = _model.MotionRequests?.Requests;
            if (modelItems == null || modelItems.Length == 0)
            {
                return;
            }

            foreach (var item in modelItems)
            {
                if (item == null)
                {
                    //一応チェックしてるけど本来nullはあり得ない
                    LogOutput.Instance.Write("Receive null MotionRequest");
                    continue;
                }

                //NOTE: 前処理として、この時点で読み込んだモデルに不足なExtraClipがある場合は差し込んでおく
                //これは異バージョンとか考慮した処理です
                foreach (var extraClip in ExtraBlendShapeClipNames)
                {
                    if (!item.ExtraBlendShapeValues.Any(i => i.Name == extraClip))
                    {
                        item.ExtraBlendShapeValues.Add(new BlendShapePairItem()
                        {
                            Name = extraClip,
                            Value = 0,
                        });
                    }
                }

                _items.Add(new WordToMotionItemViewModel(this, item));
            }
        }

        private void LoadMidiSettingItems()
        {
            var midiNoteMapModel = _model.MidiNoteToMotionMap;
            //TODO: ここは個数チェック不要な気がする。モデル側が個数も保証すればいいような
            MidiNoteMap = midiNoteMapModel.Items.Count == 0
                ? new MidiNoteToMotionMapViewModel(MidiNoteToMotionMap.LoadDefault())
                : new MidiNoteToMotionMapViewModel(midiNoteMapModel);
        }


        /// <summary>
        /// <see cref="ItemsContentString"/>に、現在の<see cref="Items"/>の内容をシリアライズした文字列を設定します。
        /// </summary>
        public void SaveItems() => _model.RequestSerializeItems();

        public void Play(WordToMotionItemViewModel item) => _model.Play(item.MotionRequest);

        public void MoveUpItem(WordToMotionItemViewModel item) => _model.MoveUpItem(item.MotionRequest);
        public void MoveDownItem(WordToMotionItemViewModel item) => _model.MoveDownItem(item.MotionRequest);
        //NOTE: 用途的にここでTaskを切る(Modelのレベルで切ると不健全だからね.)
        public async void DeleteItem(WordToMotionItemViewModel item) => await _model.DeleteItem(item.MotionRequest);

        /// <summary>
        /// 指定されたアイテムについて、必要ならアプリの設定から忘却させる処理をします。
        /// </summary>
        /// <param name="blendShapeItem"></param>
        public async void ForgetClip(BlendShapeItemViewModel blendShapeItem)
        {
            string name = blendShapeItem.BlendShapeName;
            var indication = MessageIndication.ForgetBlendShapeClip();
            bool res = await MessageBoxWrapper.Instance.ShowAsyncOnWordToMotionItemEdit(
                indication.Title,
                string.Format(indication.Content, name)
                );
            if (res)
            {
                foreach (var item in _items)
                {
                    item.ForgetClip(name);
                }

                if (ExtraBlendShapeClipNames.Contains(name))
                {
                    ExtraBlendShapeClipNames.Remove(name);
                }
                RequestReload();
            }
        }

        /// <summary>モーション一覧の情報が変わったとき、Unity側に再読み込みをリクエストします。</summary>
        public void RequestReload()
        {
            //NOTE: この結果シリアライズ文字列が変わるとモデル側でメッセージ送信もやってくれる
            SaveItems();
        }

        public ActionCommand OpenKeyAssignmentEditorCommand { get; }

        private void OpenKeyAssignmentEditor()
        {
            //note: 今のところMIDIコン以外は割り当て固定です
            if (_model.SelectedDeviceType.Value != WordToMotionSetting.DeviceTypes.MidiController)
            {
                return;
            }

            var vm = new MidiNoteToMotionEditorViewModel(MidiNoteMap, MidiNoteReceiver);

            SendMessage(MessageFactory.Instance.RequireMidiNoteOnMessage(true));
            var window = new MidiNoteAssignEditorWindow()
            {
                DataContext = vm,
            };
            bool? res = window.ShowDialog();
            SendMessage(MessageFactory.Instance.RequireMidiNoteOnMessage(false));

            if (res != true)
            {
                return;
            }

            MidiNoteMap.Load(vm.Result);
            RequestReload();
        }

        public ActionCommand AddNewItemCommand { get; }

        public ActionCommand ResetByDefaultItemsCommand { get; }

        //このマシン上でこのバージョンのVMagicMirrorが初めて実行されたと推定できるとき、
        //デフォルトのWord To Motion一覧を生成して初期化します。
        public void LoadDefaultItemsIfInitialStart()
        {
            if (!SpecialFilePath.SettingFileExists()) 
            {
                LoadDefaultItems();
            }
        }

        private void LoadDefaultItems()
        {
            ExtraBlendShapeClipNames.Clear();
            //NOTE: 現在ロードされてるキャラがいたら、そのキャラのブレンドシェイプをただちに当て直す
            ExtraBlendShapeClipNames.AddRange(_latestAvaterExtraClipNames);

            _model.LoadDefaultMotionRequests(ExtraBlendShapeClipNames);
        }

        public void EditItemByDialog(WordToMotionItemViewModel item)
        {
            var dialog = new WordToMotionItemEditWindow()
            {
                DataContext = item,
                Owner = SettingWindow.CurrentWindow,
            };

            _dialogItem = item;
           
            EnablePreview.Value = EnablePreviewWhenStartEdit;

            if (dialog.ShowDialog() == true)
            {
                item.SaveChanges();
                RequestReload();
            }
            else
            {
                item.ResetChanges();
            }

            EnablePreviewWhenStartEdit = EnablePreview.Value;
            EnablePreview.Value = false;
            _dialogItem = null;
        }

        public void RequestCustomMotionDoctor() => SendMessage(MessageFactory.Instance.RequestCustomMotionDoctor());
    }

    /// <summary> Word to Motion機能のコントロールに利用できるデバイスの選択肢1つに相当するViewModelです。 </summary>
    public class WordToMotionDeviceItem : ViewModelBase
    {
        private WordToMotionDeviceItem(int index, string displayNameKeySuffix)
        {
            Index = index;
            _displayNameKeySuffix = displayNameKeySuffix;
            LanguageSelector.Instance.LanguageChanged += RefreshDisplayName;
            RefreshDisplayName();
        }

        public int Index { get; }

        private const string DisplayNameKeyPrefix = "WordToMotion_DeviceItem_";
        private readonly string _displayNameKeySuffix;

        private string _displayName = "";
        public string DisplayName
        {
            get => _displayName;
            private set => SetValue(ref _displayName, value);
        }

        internal void RefreshDisplayName() 
            => DisplayName = LocalizedString.GetString(DisplayNameKeyPrefix + _displayNameKeySuffix);

        public static WordToMotionDeviceItem None()
            => new WordToMotionDeviceItem(
                WordToMotionSetting.DeviceTypes.None, "None"
                );

        public static WordToMotionDeviceItem KeyboardTyping()
            => new WordToMotionDeviceItem(
                WordToMotionSetting.DeviceTypes.KeyboardWord, "KeyboardWord"
                );

        public static WordToMotionDeviceItem Gamepad() 
            => new WordToMotionDeviceItem(
                WordToMotionSetting.DeviceTypes.Gamepad, "Gamepad"
                );

        public static WordToMotionDeviceItem KeyboardNumKey()
            => new WordToMotionDeviceItem(
                WordToMotionSetting.DeviceTypes.KeyboardTenKey, "KeyboardTenKey"
                );

        public static WordToMotionDeviceItem MidiController()
            => new WordToMotionDeviceItem(
                WordToMotionSetting.DeviceTypes.MidiController, "MidiController"
                );

        public static WordToMotionDeviceItem[] LoadAvailableItems()
            => new[]
            {
                None(),
                KeyboardTyping(),
                Gamepad(),
                KeyboardNumKey(),
                MidiController(),
            };
    }

}
