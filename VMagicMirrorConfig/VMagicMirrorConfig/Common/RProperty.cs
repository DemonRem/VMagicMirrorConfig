﻿using System;

namespace Baku.VMagicMirrorConfig
{
    /// <summary>
    /// ReactivePropertyの機能をめちゃくちゃ削減したもの。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class RProperty<T> : NotifiableBase
        where T : IEquatable<T>
    {
        /// <summary>
        /// 初期値と、および値が変化したときのPropertyChanged呼び出し以外の処理を指定してインスタンスを初期化します。
        /// </summary>
        /// <param name="value"></param>
        /// <param name="onChanged"></param>
        public RProperty(T value, Action<T> onChanged)
        {
            _onChanged = onChanged;
            _value = value;
        }

        /// <summary>
        /// 初期値のみを指定してインスタンスを初期化します。
        /// </summary>
        /// <param name="value"></param>
        public RProperty(T value) : this(value, _ => { })
        {
        }


        private readonly Action<T> _onChanged;
        private T _value;

        public T Value
        {
            get => _value;
            set
            {
                if (SetValue(ref _value, value))
                {
                    _onChanged(value);
                }
            }
        }

        /// <summary>
        /// NOTE: nullableなRPropertyに対してValueのSetterを呼びたいときに使う。
        /// コレを使わざるを得ないのは循環参照が発生してるケースの可能性が高いため、ちょっと注意
        /// </summary>
        /// <param name="value"></param>
        public void Set(T value) => Value = value;

        /// <summary>
        /// 値は変更しますが、イベントやコールバックは呼ばない。
        /// Unity側から値を受信し、その値がUI上で表示不要であるような、ごく一部のケースでのみ使う
        /// </summary>
        /// <param name="value"></param>
        public void SilentSet(T value) { _value = value; }
    }
}
