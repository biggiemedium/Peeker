using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Peeker.Settings
{
    /// <summary>
    /// Non-generic base for <see cref="Setting{T}"/>.
    /// Java gets heterogeneous <c>List&lt;Setting&gt;</c> for free through type erasure;
    /// C# generics are reified, so a shared base type is required for a
    /// <see cref="SettingsHolder"/> to store settings of mixed value types.
    /// UI code should work against this type and use the Boxed* members.
    /// </summary>
    public abstract class Setting
    {
        public string Name { get; set; }

        public string Description { get; set; } = string.Empty;

        /// <summary>Raised when this specific setting's value changes.</summary>
        public event Action<Setting> ValueChanged;

        /// <summary>
        /// Raised when any setting anywhere changes. This is the stand-in for
        /// <c>LeapFrog.EVENT_BUS.post(new SettingUpdateEvent(this))</c>.
        /// Subscribe once at client init; remember to unsubscribe on unload.
        /// </summary>
        public static event Action<Setting> AnyValueChanged;

        public abstract Type ValueType { get; }

        /// <summary>Type-erased accessor for UI / config code.</summary>
        public abstract object BoxedValue { get; set; }

        /// <summary>Null when no minimum was supplied.</summary>
        public abstract object BoxedMin { get; }

        /// <summary>Null when no maximum was supplied.</summary>
        public abstract object BoxedMax { get; }

        /// <summary>Null when this setting is not a combo box.</summary>
        public abstract IReadOnlyList<object> BoxedComboBox { get; }

        public abstract bool IsVisible { get; }

        public abstract void ResetToDefault();

        public bool HasRange => BoxedMin != null && BoxedMax != null;

        public bool HasComboBox => BoxedComboBox != null && BoxedComboBox.Count > 0;

        /// <summary>
        /// Mirrors the Java version, but tests the declared type rather than the
        /// boxed instance so it still works when the current value is null.
        /// </summary>
        public bool IsNumberSetting
        {
            get
            {
                Type t = Nullable.GetUnderlyingType(ValueType) ?? ValueType;
                return t == typeof(double) || t == typeof(float) || t == typeof(decimal)
                    || t == typeof(int) || t == typeof(long) || t == typeof(short)
                    || t == typeof(byte) || t == typeof(sbyte)
                    || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort);
            }
        }

        public bool IsBoolSetting => (Nullable.GetUnderlyingType(ValueType) ?? ValueType) == typeof(bool);

        public bool IsStringSetting
        {
            get
            {
                Type t = Nullable.GetUnderlyingType(ValueType) ?? ValueType;
                return t == typeof(string) || t == typeof(char);
            }
        }

        public bool IsColorSetting
        {
            get
            {
                Type t = Nullable.GetUnderlyingType(ValueType) ?? ValueType;
                return t == typeof(Color) || t == typeof(Color32);
            }
        }

        /// <summary>
        /// Java has to infer "enum" by elimination. C# can just ask the type,
        /// but the elimination fallback is kept so anonymous/custom option types
        /// still render as a dropdown. Colors are carved out so they get a
        /// picker instead of a dropdown.
        /// </summary>
        public bool IsEnumSetting =>
            (Nullable.GetUnderlyingType(ValueType) ?? ValueType).IsEnum
            || (!IsNumberSetting && !IsBoolSetting && !IsStringSetting && !IsColorSetting);

        protected void RaiseValueChanged()
        {
            ValueChanged?.Invoke(this);
            AnyValueChanged?.Invoke(this);
        }

        // ---- Factories -------------------------------------------------
        // Preferred over the constructors: no overload ambiguity, and type
        // inference means you never have to write the <T> yourself.

        public static Setting<T> Of<T>(string name, T value)
            => new Setting<T>(name, value);

        public static Setting<T> Range<T>(string name, T value, T min, T max)
            => new Setting<T>(name, value, min, max);

        public static Setting<T> Combo<T>(string name, T value, params T[] values)
            => new Setting<T>(name, value, values);
    }

    public class Setting<T> : Setting
    {
        private T _value;
        private T _min;
        private T _max;
        private bool _hasMin;
        private bool _hasMax;

        public Setting(string name, T value)
        {
            Name = name;
            _value = value;
            DefaultValue = value;
        }

        /// <summary>
        /// Combo-box constructor. WARNING: with exactly two extra arguments
        /// (<c>new Setting&lt;int&gt;("X", 1, 2, 3)</c>) C# picks the min/max
        /// overload instead, same trap the Java varargs version has.
        /// Use <see cref="Setting.Combo{T}"/> to be unambiguous.
        /// </summary>
        public Setting(string name, T value, params T[] values) : this(name, value)
        {
            if (values != null && values.Length > 0)
                ComboBox = new List<T>(values);
        }

        public Setting(string name, T value, T min, T max) : this(name, value)
        {
            Min = min;
            Max = max;
        }

        public Setting(string name, T value, Predicate<T> visibility) : this(name, value)
        {
            Visibility = visibility;
        }

        public Setting(string name, T value, T min, T max, Predicate<T> visibility)
            : this(name, value, min, max)
        {
            Visibility = visibility;
        }

        public T DefaultValue { get; private set; }

        public T Value
        {
            get { return _value; }
            set
            {
                _value = value;
                RaiseValueChanged();
            }
        }

        /// <summary>Assign without firing the update event (config loading, etc.).</summary>
        public void SetValueSilent(T value) => _value = value;

        public T Min
        {
            get { return _min; }
            set { _min = value; _hasMin = true; }
        }

        public T Max
        {
            get { return _max; }
            set { _max = value; _hasMax = true; }
        }

        public Predicate<T> Visibility { get; set; }

        public List<T> ComboBox { get; set; }

        public override Type ValueType => typeof(T);

        public override object BoxedValue
        {
            get { return _value; }
            set { Value = Coerce(value); }
        }

        // Value types default to 0/false rather than null, so a flag is needed
        // to tell "no min supplied" apart from "min is 0".
        public override object BoxedMin => _hasMin ? (object)_min : null;

        public override object BoxedMax => _hasMax ? (object)_max : null;

        public override IReadOnlyList<object> BoxedComboBox =>
            ComboBox == null ? null : ComboBox.Cast<object>().ToList();

        public override bool IsVisible => Visibility == null || Visibility(_value);

        public override void ResetToDefault() => Value = DefaultValue;

        // ---- Fluent configuration --------------------------------------

        public Setting<T> WithDescription(string message)
        {
            Description = message;
            return this;
        }

        public Setting<T> WithVisibility(Predicate<T> visibility)
        {
            Visibility = visibility;
            return this;
        }

        public Setting<T> WithRange(T min, T max)
        {
            Min = min;
            Max = max;
            return this;
        }

        public Setting<T> WithComboBox(params T[] values)
        {
            ComboBox = new List<T>(values ?? Array.Empty<T>());
            return this;
        }

        /// <summary>
        /// Lets a setting be used directly where its value is expected:
        /// <c>if (onlyWhenSprinting) { ... }</c>. Delete if you'd rather be explicit.
        /// </summary>
        public static implicit operator T(Setting<T> setting) => setting._value;

        public override string ToString() => $"{Name}: {_value}";

        private static T Coerce(object raw)
        {
            if (raw is T typed)
                return typed;

            Type target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            if (raw == null)
                return default;

            if (target.IsEnum)
            {
                if (raw is string s)
                    return (T)Enum.Parse(target, s, true);
                return (T)Enum.ToObject(target, raw);
            }

            return (T)Convert.ChangeType(raw, target);
        }
    }
}