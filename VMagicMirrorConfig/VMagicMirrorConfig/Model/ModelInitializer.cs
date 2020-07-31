﻿using System;

namespace Baku.VMagicMirrorConfig
{
    class ModelInitializer : IDisposable
    {
        public ModelInitializer()
        {
            var mmfClient = new MmfClient();
            MessageSender = mmfClient;
            MessageReceiver = mmfClient;
            CameraPositionChecker = new CameraPositionChecker(MessageSender);
            UnityAppCloser = new UnityAppCloser();
            UnityAppCloser.Initialize(MessageReceiver);
        }

        public void StartObserveRoutine()
        {
            new AppExitFromUnityMessage().Initialize(MessageReceiver);
            MessageReceiver.Start();
        }

        public IMessageSender MessageSender { get; } 
        public IMessageReceiver MessageReceiver { get; } 
        public CameraPositionChecker CameraPositionChecker { get; }
        public UnityAppCloser UnityAppCloser { get; }

        public void Dispose()
        {
            MessageReceiver.Stop();
            CameraPositionChecker.Stop();
        }
    }
}
