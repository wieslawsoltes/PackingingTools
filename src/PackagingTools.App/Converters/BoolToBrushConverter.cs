using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PackagingTools.App.Converters;

public sealed class BoolToBrushConverter : IValueConverter
{
    public IBrush TrueBrush { get; set; } = SolidColorBrush.Parse("#2E7D32");
    public IBrush FalseBrush { get; set; } = SolidColorBrush.Parse("#8B0000");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueBrush : FalseBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
