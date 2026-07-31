using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace JarvisTeto.Services
{
    /// <summary>
    /// Registra un atajo de teclado global (funciona aunque la ventana esté oculta
    /// o minimizada a la bandeja) usando RegisterHotKey de Win32.
    /// </summary>
    public class HotkeyManager
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_ALT = 0x0001;

        private const int HOTKEY_ID_TOGGLE = 9001;
        private const int HOTKEY_ID_CAPTURE = 9002;

        private HwndSource? _source;
        private IntPtr _handle;

        public event Action? ToggleWindowRequested;
        public event Action? CaptureScreenRequested;

        public void Register(Window window)
        {
            _handle = new WindowInteropHelper(window).Handle;
            _source = HwndSource.FromHwnd(_handle);
            _source?.AddHook(WndProc);

            // Ctrl+Alt+J -> mostrar/ocultar Jarvis
            RegisterHotKey(_handle, HOTKEY_ID_TOGGLE, MOD_CONTROL | MOD_ALT, (uint)System.Windows.Forms.Keys.J);
            // Ctrl+Alt+K -> capturar pantalla y preguntar directo
            RegisterHotKey(_handle, HOTKEY_ID_CAPTURE, MOD_CONTROL | MOD_ALT, (uint)System.Windows.Forms.Keys.K);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_TOGGLE) ToggleWindowRequested?.Invoke();
                else if (id == HOTKEY_ID_CAPTURE) CaptureScreenRequested?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Unregister()
        {
            if (_handle != IntPtr.Zero)
            {
                UnregisterHotKey(_handle, HOTKEY_ID_TOGGLE);
                UnregisterHotKey(_handle, HOTKEY_ID_CAPTURE);
            }
            _source?.RemoveHook(WndProc);
        }
    }
}
