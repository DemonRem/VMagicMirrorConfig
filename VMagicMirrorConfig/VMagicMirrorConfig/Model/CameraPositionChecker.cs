﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace Baku.VMagicMirrorConfig
{
    /// <summary> Unity側のカメラの位置をポーリングベースで確認するやつ </summary>
    /// <remarks> 1インスタンスはStartとStopを1回ずつ呼び出したら使い終わり、という設計です </remarks>
    class CameraPositionChecker
    {
        public CameraPositionChecker(IMessageSender sender)
        {
            _sender = sender;
            _cts = new CancellationTokenSource();
        }

        private readonly IMessageSender _sender;
        private readonly CancellationTokenSource _cts;

        public void Start(int intervalMillisec, Action<string> onResult) => Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(intervalMillisec, _cts.Token);
                    if (_cts.Token.IsCancellationRequested)
                    {
                        return;
                    }
                    string data = await _sender.QueryMessageAsync(MessageFactory.Instance.CurrentCameraPosition());
                    onResult(data);
                }
                catch(Exception ex)
                {
                    LogOutput.Instance.Write(ex);
                }
            }
        });

        public void Stop() => _cts.Cancel();
    }
}
