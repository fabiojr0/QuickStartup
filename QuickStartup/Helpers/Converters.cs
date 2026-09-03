using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace QuickStartup.Helpers;

/// <summary>Converte bool IsDefault → " · Padrão" ou ""</summary>
public class BoolToDefaultTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? " · Padrão" : "";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>null → Visible, não-null → Collapsed (ou invertido via IsInverse)</summary>
public class NullToVisibilityConverter : IValueConverter
{
    /// <summary>Se true, retorna Visible quando valor é null (modo padrão: placeholder).</summary>
    public bool VisibleWhenNull { get; set; } = true;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value is null) == VisibleWhenNull ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Extrai a primeira letra (maiúscula) de um nome, para o avatar circular
/// dos cards de perfil. Usa "?" quando o texto está vazio.</summary>
public class InitialLetterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string;
        return string.IsNullOrWhiteSpace(text)
            ? "?"
            : char.ToUpper(text.Trim()[0], culture).ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Colore o retângulo de um monitor na prévia clicável: destaca o monitor
/// atualmente escolhido para o app (mesma cor de seleção usada nas listas do app) e mantém
/// a borda de destaque do monitor principal quando nenhum dos dois se aplica.
/// Espera 3 valores: Monitor.Index, Monitor.IsPrimary, SelectedApp.MonitorIndex.
/// ConverterParameter "Border" ou "Background" escolhe qual brush retornar.</summary>
public class MonitorHighlightConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush AccentBrush     = new(System.Windows.Media.Color.FromRgb(0x60, 0xCD, 0xFF));
    private static readonly SolidColorBrush NeutralBrush    = new(System.Windows.Media.Color.FromRgb(0x45, 0x45, 0x47));
    private static readonly SolidColorBrush SurfaceBrush    = new(System.Windows.Media.Color.FromRgb(0x38, 0x38, 0x38));
    private static readonly SolidColorBrush SelectedBgBrush = new(System.Windows.Media.Color.FromArgb(0x50, 0x60, 0xCD, 0xFF));

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        bool isSelected = values.Length >= 3
            && values[0] is int monitorIndex
            && values[2] is int selectedIndex
            && monitorIndex == selectedIndex;
        bool isPrimary = values.Length >= 2 && values[1] is true;

        if (parameter as string == "Background")
            return isSelected ? SelectedBgBrush : SurfaceBrush;

        return isSelected || isPrimary ? AccentBrush : NeutralBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
