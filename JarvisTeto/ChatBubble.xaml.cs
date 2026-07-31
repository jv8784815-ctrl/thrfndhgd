using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using JarvisTeto.Controls;
using JarvisTeto.Models;
using JarvisTeto.Services;
using JarvisTeto.Utils;

namespace JarvisTeto
{
    /// <summary>
    /// La "burbuja pequeña de chat" que aparece al hacer click en el widget flotante. Es un chat
    /// completo pero condensado: tiene su propio historial corto, usa la misma GeminiService y
    /// VoiceService que la ventana completa, y refleja el estado (pensando/hablando/escuchando)
    /// en la esfera de orbes del widget que la abrió, para que todo se vea como una sola cosa viva.
    /// </summary>
    public partial class ChatBubble : Window
    {
        private readonly ObservableCollection<ChatMessage> _messages = new();
        private readonly List<(string role, string text)> _history = new();
        private readonly VoiceService _voice = new();
        private readonly FloatingWidget _owner;
        private AppSettings _settings = SettingsService.Load();
        private bool _isBusy;

        public ChatBubble(FloatingWidget owner)
        {
            InitializeComponent();
            _owner = owner;
            MessagesList.ItemsSource = _messages;

            AddBotMessage("Hola, soy Jarvis. Escribime acá para algo rápido, " +
                          "o abrí la ventana completa desde el menú del widget para todo lo demás.");

            _voice.SpeechRecognized += text => Dispatcher.Invoke(() =>
            {
                InputBox.Text = text;
                _ = SendMessageAsync();
            });
            _voice.SpeakStarted += () => Dispatcher.Invoke(() => _owner.Orb.SetMode(OrbMode.Speaking));
            _voice.WordSpoken += () => Dispatcher.Invoke(() => _owner.Orb.PulseSpeech());
            _voice.SpeakCompleted += () => Dispatcher.Invoke(() =>
            {
                if (!_isBusy) _owner.Orb.SetMode(OrbMode.Idle);
            });

            Loaded += (s, e) => InputBox.Focus();
            Closed += (s, e) => Cleanup();
        }

        /// <summary>Ubica la burbuja pegada al widget: a un lado si hay lugar, ajustada para no salirse de la pantalla.</summary>
        public void PositionNextTo(FloatingWidget widget)
        {
            var workArea = SystemParameters.WorkArea;
            const double margin = 12;

            double left = widget.Left - Width - margin;
            if (left < workArea.Left)
                left = widget.Left + widget.Width + margin;
            if (left + Width > workArea.Right)
                left = workArea.Right - Width - margin;
            if (left < workArea.Left)
                left = workArea.Left + margin;

            double top = widget.Top + (widget.Height / 2.0) - (Height / 2.0);
            if (top < workArea.Top) top = workArea.Top + margin;
            if (top + Height > workArea.Bottom) top = workArea.Bottom - Height - margin;

            Left = left;
            Top = top;
        }

        public void FocusInput() => InputBox.Focus();

        private void Cleanup()
        {
            try
            {
                if (_voice.IsListening) _voice.StopListening();
                _voice.StopSpeaking();
                _voice.Dispose();
            }
            catch { /* la ventana se está cerrando igual; no hace falta romper nada más */ }

            _owner.Orb.SetMode(OrbMode.Idle);
        }

        // ---------- Envío de mensajes ----------

        private async void SendButton_Click(object sender, RoutedEventArgs e) => await SendMessageAsync();

        private async void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await SendMessageAsync();
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
            _owner.Orb.SetMode(OrbMode.Thinking);

            string response = await GeminiService.SendAsync(_settings.ApiKey, _settings.Model, text, null, _history);

            _history.Add(("user", text));
            _history.Add(("model", response));
            if (_history.Count > 20) _history.RemoveRange(0, 2);

            AddBotMessage(response);
            _isBusy = false;

            if (_settings.AutoSpeak)
            {
                // El modo "Speaking" y el pulso de la esfera se disparan solos desde los eventos
                // de VoiceService (suscriptos en el constructor), igual que en la ventana completa.
                _voice.Speak(response, _settings.VoiceName, _settings.VoicePitch, _settings.VoiceRate);
            }
            else
            {
                _owner.Orb.SetMode(OrbMode.Idle);
            }

            StatusText.Text = "  listo";
            SendButton.IsEnabled = true;
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
                    if (!_isBusy && !_voice.IsSpeaking) _owner.Orb.SetMode(OrbMode.Idle);
                }
                else
                {
                    _voice.StartListening();
                    StatusText.Text = "  escuchando...";
                    _owner.Orb.SetMode(OrbMode.Listening);
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
            _messages.Add(new ChatMessage { Text = TextUtils.ForDisplay(text), IsUser = false });
            ChatScroll.ScrollToEnd();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
