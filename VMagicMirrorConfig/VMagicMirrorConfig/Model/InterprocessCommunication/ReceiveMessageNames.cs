﻿namespace Baku.VMagicMirrorConfig
{
    static class ReceiveMessageNames
    {
        public const string CloseConfigWindow = nameof(CloseConfigWindow);
        public const string SetCalibrationFaceData = nameof(SetCalibrationFaceData);
        public const string SetBlendShapeNames = nameof(SetBlendShapeNames);
        public const string AutoAdjustResults = nameof(AutoAdjustResults);
        public const string AutoAdjustEyebrowResults = nameof(AutoAdjustEyebrowResults);

        public const string ExtraBlendShapeClipNames = nameof(ExtraBlendShapeClipNames);

        public const string MidiNoteOn = nameof(MidiNoteOn);

        public const string VRoidModelLoadCompleted = nameof(VRoidModelLoadCompleted);
        public const string VRoidModelLoadCanceled = nameof(VRoidModelLoadCanceled);
    }
}
