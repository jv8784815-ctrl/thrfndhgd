using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace JarvisTeto
{
    public class AlignConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isUser = value is bool b && b;
            return isUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BubbleColorConverter : IValueConverter
    {
        // Usuario: azul lleno "Jarvis". Bot: panel oscuro con un dejo azulado, para que el usuario
        // se distinga a simple vista sin salirse de la paleta.
        private static readonly SolidColorBrush UserBrush = new(System.Windows.Media.Color.FromRgb(0x1F, 0xA8, 0xD8));
        private static readonly SolidColorBrush BotBrush = new(System.Windows.Media.Color.FromRgb(0x12, 0x1B, 0x29));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isUser = value is bool b && b;
            return isUser ? UserBrush : BotBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Texto del mensaje: blanco azulado para el bot, casi negro para el burbuja azul del usuario (más contraste/legible).</summary>
    public class BubbleTextColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush UserText = new(System.Windows.Media.Color.FromRgb(0x03, 0x0A, 0x10));
        private static readonly SolidColorBrush BotText = new(System.Windows.Media.Color.FromRgb(0xE7, 0xF6, 0xFF));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isUser = value is bool b && b;
            return isUser ? UserText : BotText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
