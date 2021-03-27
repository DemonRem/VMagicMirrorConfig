﻿using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace Baku.VMagicMirrorConfig
{
    public class SaveLoadDataViewModel : ViewModelBase
    {
        internal SaveLoadDataViewModel(SaveFileManager model, bool isLoadMode, Action actToClose)
        {
            _model = model;
            _actToClose = actToClose;
            Items = new ReadOnlyObservableCollection<SaveLoadFileItemViewModel>(_items);
            CancelCommand = new ActionCommand(CloseDialog);
            IsLoadMode = isLoadMode;
            Refresh();
        }

        private readonly SaveFileManager _model;
        private readonly Action _actToClose;

        private readonly ObservableCollection<SaveLoadFileItemViewModel> _items 
            = new ObservableCollection<SaveLoadFileItemViewModel>();
        public ReadOnlyObservableCollection<SaveLoadFileItemViewModel> Items { get; }

        public bool IsLoadMode { get; }

        public ActionCommand CancelCommand { get; }

        //デフォルトではキャラロードだけ有効にして、「同じモデルで服が違うのをパッと切り替えます」みたいなUXを重視しておく。
        public RProperty<bool> LoadCharacterWhenSettingLoaded { get; } = new RProperty<bool>(true);
        public RProperty<bool> LoadNonCharacterWhenSettingLoaded { get; } = new RProperty<bool>(false);

        private void Refresh()
        {
            _items.Clear();
            for (int i = 0; i <= SaveFileManager.FileCount; i++)
            {
                var meta = SettingFileOverview.CreateOverviewFromFile(SpecialFilePath.GetSaveFilePath(i), i);
                _items.Add(new SaveLoadFileItemViewModel(
                    IsLoadMode, i == _model.LatestLoadedFileIndex, meta, this
                    ));
            }
        }

        public async Task ExecuteLoad(int index)
        {
            if (index < 0 || index > SaveFileManager.FileCount || !_model.CheckFileExist(index))
            {
                return;
            }

            //NOTE: オートセーブのデータに関しても、ここを通るケースではスロットのファイルと同格に扱う事に注意
            var indication = MessageIndication.ConfirmSettingFileLoad();
            var result = await MessageBoxWrapper.Instance.ShowAsync(
                indication.Title,
                string.Format(indication.Content, index),
                MessageBoxWrapper.MessageBoxStyle.OKCancel
                );

            if (!result)
            {
                return;
            }

            //NOTE: コケる事も考えられるんだけど判別がムズいんですよね…
            SnackbarWrapper.Enqueue(string.Format(
                LocalizedString.GetString("SettingFile_LoadCompleted"), index
                ));

            _model.LoadSetting(
                index, LoadCharacterWhenSettingLoaded.Value, LoadNonCharacterWhenSettingLoaded.Value, false
                );
            _actToClose();
        }

        public async Task ExecuteSave(int index)
        {
            if (index <= 0 || index > SaveFileManager.FileCount)
            {
                return;
            }

            //上書き保存があり得るので確認を挟む。
            //初セーブの場合は上書きにならないが、「次から上書きになるで」の意味で出しておく
            var indication = MessageIndication.ConfirmSettingFileSave();
            var result = await MessageBoxWrapper.Instance.ShowAsync(
                indication.Title,
                string.Format(indication.Content, index),
                MessageBoxWrapper.MessageBoxStyle.OKCancel
                );
            if (!result)
            {
                return;
            }

            _model.SaveCurrentSetting(index);

            //ファイルレベルの処理なので流石にスナックバーくらい出しておく(ロードとかインポート/エクスポートも同様)
            SnackbarWrapper.Enqueue(string.Format(
                LocalizedString.GetString("SettingFile_SaveCompleted"), index
                ));

            //ロードと違い、ダイアログは閉じず、代わりに更新時刻が変わった所を見に行く
            _ = Application.Current.Dispatcher.BeginInvoke(new Action(() => Refresh()));
        }

        private void CloseDialog() => _actToClose();
    }
}
