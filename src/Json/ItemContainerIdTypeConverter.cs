using System;
using System.ComponentModel;
using System.Globalization;

namespace Oxide.Game.Rust.Json
{
    internal class ItemContainerIdTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) => value is string s ? new ItemContainerId(ulong.Parse(s)) : base.ConvertFrom(context, culture, value);

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            return destinationType == typeof(string) && value is ItemContainerId t
                ? t.ToString()
                : base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
