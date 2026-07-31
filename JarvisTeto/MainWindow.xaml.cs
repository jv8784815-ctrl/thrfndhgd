using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using JarvisTeto.Controls;
using JarvisTeto.Models;
using JarvisTeto.Services;
using JarvisTeto.Utils;

namespace JarvisTeto
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<ChatMessage> _messages = new();
        private readonly VoiceService _voice = new();
        private readonly HotkeyManager _hotkeys = new();
        private readonly List<(string role, string text)> _history = new();
        private AppSettings _settings = SettingsService.Load();
        private bool _isBusy;

        public MainWindow()
        {
            InitializeComponent();
            MessagesList.ItemsSource = _messages;

            AddBotMessage("Hola, soy Jarvis. Escribime o mantené presionado el micrófono. " +
                          "Ctrl+Alt+J para mostrarme/ocultarme, Ctrl+Alt+K para preguntarme sobre tu pantalla al toque.");

            Orb.SetMode(OrbMode.Idle);

            _voice.SpeechRecognized += text => Dispatcher.Invoke(() => OnSpeechRecognized(text));
            _voice.SpeakStarted += () => Dispatcher.Invoke(() => Orb.SetMode(OrbMode.Speaking));
            _voice.WordSpoken += () => Dispatcher.Invoke(() => Orb.PulseSpeech());
            _voice.SpeakCompleted += () => Dispatcher.Invoke(() =>
            {
                if (!_isBusy) Orb.SetMode(OrbMode.Idle);
            });

            SourceInitialized += (s, e) =>
            {
                _hotkeys.Register(this);
                _hotkeys.ToggleWindowRequested += () => Dispatcher.Invoke(ToggleVisibility);
                _hotkeys.CaptureScreenRequested += () => Dispatcher.Invoke(OpenScreenAskOverlay);
            };
        }

        private void ToggleVisibility()
        {
            if (IsVisible && WindowState != WindowState.Minimized)
            {
                Hide();
            }
            else
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                InputBox.Focus();
            }
        }

        private void OpenScreenAskOverlay()
        {
            // Se abre sobre el monitor donde vive Jarvis (no otros monitores), sin mostrar el chat.
            var bounds = ScreenCaptureService.GetOwnerScreenBounds(this);
            var overlay = new ScreenAskOverlay(bounds, _voice);
            overlay.Show();
            overlay.Activate();
        }

        // ---------- Envío de mensajes ----------

        private async void SendButton_Click(object sender, RoutedEventArgs e) => await SendMessageAsync();

        private async void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await SendMessageAsync();
        }

        private async Task SendMessageAsync()
        {
            if (_isBusy) return;
            string text = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _settings = SettingsService.Load(); // por si se actualizó en Configuración
            InputBox.Clear();
            AddUserMessage(text);

            _isBusy = true;
            StatusText.Text = "  pensando...";
            SendButton.IsEnabled = false;
            Orb.SetMode(OrbMode.Thinking);

            string? screenshot = null;
            if (ScreenButton.IsChecked == true)
            {
                try
                {
                    var bounds = ScreenCaptureService.GetOwnerScreenBounds(this);
                    screenshot = ScreenCaptureService.CaptureRegionAsBase64Png(bounds);
                }
                catch { /* si falla la captura, seguimos sin imagen */ }
            }

            string response = await GeminiService.SendAsync(_settings.ApiKey, _settings.Model, text, screenshot, _history);

            _history.Add(("user", text));
            _history.Add(("model", response));
            if (_history.Count > 20) _history.RemoveRange(0, 2); // limita el contexto para mantenerlo rápido

            AddBotMessage(response);

            _isBusy = false;

            if (_settings.AutoSpeak)
            {
                // El modo "Speaking" y el pulso del ecualizador de orbes se disparan solos desde los
                // eventos SpeakStarted/WordSpoken/SpeakCompleted de VoiceService (suscriptos arriba),
                // así que la esfera queda sincronizada con la duración real del audio, no con un tiempo inventado.
                _voice.Speak(response, _settings.VoiceName, _settings.VoicePitch, _settings.VoiceRate);
            }
            else
            {
                Orb.SetMode(OrbMode.Idle);
            }

            StatusText.Text = "  listo";
            SendButton.IsEnabled = true;
        }

        private void OnSpeechRecognized(string text)
        {
            InputBox.Text = text;
            _ = SendMessageAsync();
        }

        // ---------- Voz ----------

        private void MicButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_voice.IsListening)
                {
                    _voice.StopListening();
                    StatusText.Text = "  listo";
                    if (!_isBusy && !_voice.IsSpeaking) Orb.SetMode(OrbMode.Idle);
                }
                else
                {
                    _voice.StartListening();
                    StatusText.Text = "  escuchando...";
                    Orb.SetMode(OrbMode.Listening);
                }
            }
            catch
            {
                MicButton.IsChecked = false;
                System.Windows.MessageBox.Show(
                    "No pude acceder al micrófono. Revisá los permisos de micrófono de Windows para esta app.",
                    "Jarvis", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ---------- Mensajes UI ----------

        private void AddUserMessage(string text) => _messages.Add(new ChatMessage { Text = text, IsUser = true });

        private void AddBotMessage(string text)
        {
            // Limpia el Markdown que devuelve Gemini (**negrita**, #títulos, etc.) para que no
            // aparezcan símbolos sueltos como "***********" en las burbujas del chat.
            _messages.Add(new ChatMessage { Text = TextUtils.ForDisplay(text), IsUser = false });
            ChatScroll.ScrollToEnd();
        }

        // ---------- Configuración ----------

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow { Owner = this };
            if (win.ShowDialog() == true)
                _settings = SettingsService.Load();
        }

        // ---------- Ciclo de vida / bandeja ----------

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                Hide(); // minimizar = ocultar a la bandeja, sigue corriendo en segundo plano
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cerrar la ventana con la X solo la oculta; para salir de verdad se usa
            // "Salir" en el ícono de la bandeja del sistema.
            e.Cancel = true;
            Hide();
        }
    }
}
