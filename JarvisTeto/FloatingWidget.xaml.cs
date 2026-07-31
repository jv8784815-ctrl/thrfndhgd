using System.Windows;
using System.Windows.Input;
using JarvisTeto.Services;

namespace JarvisTeto
{
    /// <summary>
    /// Jarvis "de fondo": una ventanita redonda, siempre visible y siempre encima, que se puede
    /// arrastrar por todo el escritorio como cualquier widget. Un click corto (sin arrastrar) abre
    /// o cierra una burbuja de chat chica pegada al lado; el click derecho da un menú rápido.
    ///
    /// El truco para distinguir "click" de "arrastre" con una sola ventana sin borde es simple:
    /// se guarda la posición antes de llamar a DragMove() (que bloquea hasta soltar el botón) y,
    /// al volver, si la posición no cambió, fue un click y no un arrastre.
    /// </summary>
    public partial class FloatingWidget : Window
    {
        private ChatBubble? _bubble;
        private double _dragStartLeft;
        private double _dragStartTop;

        public FloatingWidget()
        {
            InitializeComponent();
            Loaded += (s, e) => PlaceAtStartupPosition();
        }

        private void PlaceAtStartupPosition()
        {
            var settings = SettingsService.Load();

            if (settings.WidgetLeft.HasValue && settings.WidgetTop.HasValue)
            {
                Left = settings.WidgetLeft.Value;
                Top = settings.WidgetTop.Value;
            }
            else
            {
                // Por defecto, abajo a la derecha del área de trabajo (como cualquier widget de escritorio).
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Right - Width - 28;
                Top = workArea.Bottom - Height - 28;
            }

            ClampToVirtualScreen();
        }

        private void ClampToVirtualScreen()
        {
            double minLeft = SystemParameters.VirtualScreenLeft;
            double minTop = SystemParameters.VirtualScreenTop;
            double maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width;
            double maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height;

            if (maxLeft > minLeft) Left = Math.Clamp(Left, minLeft, maxLeft);
            if (maxTop > minTop) Top = Math.Clamp(Top, minTop, maxTop);
        }

        private void SaveWidgetPosition()
        {
            try
            {
                var settings = SettingsService.Load();
                settings.WidgetLeft = Left;
                settings.WidgetTop = Top;
                SettingsService.Save(settings);
            }
            catch { /* si falla el guardado, la próxima vez arranca en la posición por defecto */ }
        }

        // ---------- Arrastrar y click ----------

        private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartLeft = Left;
            _dragStartTop = Top;

            try { DragMove(); }
            catch { /* el botón se soltó antes de que el drag arrancara; no pasa nada */ }

            bool moved = Math.Abs(Left - _dragStartLeft) > 2 || Math.Abs(Top - _dragStartTop) > 2;

            if (moved)
            {
                ClampToVirtualScreen();
                SaveWidgetPosition();
                RepositionBubbleIfOpen();
            }
            else
            {
                ToggleBubble();
            }
        }

        // ---------- Burbuja de chat ----------

        private void ToggleBubble()
        {
            if (_bubble != null)
            {
                _bubble.Close();
                return;
            }

            _bubble = new ChatBubble(this);
            _bubble.Closed += (s, e) => _bubble = null;
            _bubble.PositionNextTo(this);
            _bubble.Show();
            _bubble.Activate();
            _bubble.FocusInput();
        }

        private void RepositionBubbleIfOpen() => _bubble?.PositionNextTo(this);

        private void OpenBubble_Click(object sender, RoutedEventArgs e)
        {
            if (_bubble == null) ToggleBubble();
            else _bubble.Activate();
        }

        // ---------- Menú contextual ----------

        private void OpenFullWindow_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Application.Current is App app) app.ShowMainWindow();
        }

        private void HideWidget_Click(object sender, RoutedEventArgs e) => Hide();

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Application.Current is App app) app.ExitApp();
        }
    }
}
