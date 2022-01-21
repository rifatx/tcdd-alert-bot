using System.ComponentModel;
using System.Globalization;

namespace TCDDAlertBot;

public class AlertRequestConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) =>
        value is string s ? AlertRequest.Parse(s) : base.ConvertFrom(context, culture, value);

    public override object? ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object? value,
        Type destinationType)
    {
        return destinationType == typeof(string) && value is AlertRequest ar
            ? ar.ToString()
            : base.ConvertTo(context, culture, value, destinationType);
    }
}