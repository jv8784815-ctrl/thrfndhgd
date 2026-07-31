using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace JarvisTeto
{
    public partial class App : Application
    {
        private NotifyIcon? _trayIcon;
        private MainWindow? _mainWindow;
        private FloatingWidget? _widget;
        private System.Threading.Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool isNew;
            _mutex = new System.Threading.Mutex(true, "JarvisTeto_SingleInstance", out isNew);
            if (!isNew)
            {
                MessageBox.Show("Jarvis ya está corriendo en segundo plano. Buscalo en la bandeja del sistema o en el widget flotante.",
                    "Jarvis", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            _mainWindow = new MainWindow();

            // La ventana completa arranca oculta (el widget es la cara "de fondo" de Jarvis), pero
            // le creamos el handle de Win32 igual, sin mostrarla, para que el hotkey global
            // (Ctrl+Alt+J) quede activo desde el primer segundo y no recién cuando se la abre.
            new System.Windows.Interop.WindowInteropHelper(_mainWindow).EnsureHandle();

            _widget = new FloatingWidget();

            _trayIcon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Visible = true,
                Text = "Jarvis (Teto)"
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Mostrar Jarvis (ventana completa)", null, (s, a) => ShowMainWindow());
            menu.Items.Add("Mostrar/ocultar widget", null, (s, a) => ToggleWidget());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Salir", null, (s, a) => ExitApp());
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, a) => ShowMainWindow();

            // Arranca solo con el widget flotando en el escritorio; la ventana completa (chat grande,
            // configuración, captura de pantalla) se abre a demanda: doble click en la bandeja,
            // menú del widget, o el hotkey Ctrl+Alt+J de siempre.
            _widget.Show();
        }

        internal void ShowMainWindow()
        {
            if (_mainWindow == null) return;
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }

        private void ToggleWidget()
        {
            if (_widget == null) return;
            if (_widget.IsVisible) _widget.Hide();
            else _widget.Show();
        }

        internal void ExitApp()
        {
            _trayIcon!.Visible = false;
            _trayIcon.Dispose();
            _mutex?.ReleaseMutex();
            Shutdown();
        }
    }
}
