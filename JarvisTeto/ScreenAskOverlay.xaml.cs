using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using JarvisTeto.Services;
using JarvisTeto.Utils;

namespace JarvisTeto
{
    /// <summary>
    /// Overlay tipo HUD que se abre con Ctrl+Alt+K: un aro de luz neón alrededor del monitor
    /// donde vive Jarvis y una burbuja flotante para escribir la pregunta directo, sin abrir
    /// la ventana de chat. Solo "ve" (captura) el monitor donde está posicionada esta ventana.
    /// </summary>
    public partial class ScreenAskOverlay : Window
    {
        private readonly System.Drawing.Rectangle _screenBounds;
        private readonly VoiceService _voice;
        private bool _answered;

        public ScreenAskOverlay(System.Drawing.Rectangle screenBounds, VoiceService voice)
        {
            InitializeComponent();
            _screenBounds = screenBounds;
            _voice = voice;

            // Posiciona el overlay exactamente sobre el monitor de Jarvis (no otros monitores).
            Left = screenBounds.Left;
            Top = screenBounds.Top;
            Width = screenBounds.Width;
            Height = screenBounds.Height;

            Loaded += (s, e) =>
            {
                QuestionBox.Focus();
                StartGlowAnimation();
            };

            PreviewKeyDown += Overlay_PreviewKeyDown;
            Deactivated += (s, e) => { if (!_answered) Close(); };
        }

        private void StartGlowAnimation()
        {
            var pulse = new DoubleAnimation(0.45, 1.0, TimeSpan.FromSeconds(1.3))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            GlowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, pulse);

            var outerPulse = new DoubleAnimation(0.3, 0.75, TimeSpan.FromSeconds(1.3))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            OuterGlow.BeginAnimation(DropShadowEffect.OpacityProperty, outerPulse);
        }

        private void QuestionBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Placeholder.Visibility = string.IsNullOrEmpty(QuestionBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void QuestionBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                string question = QuestionBox.Text.Trim();
                if (string.IsNullOrEmpty(question)) return;
                await AskAsync(question);
            }
        }

        private void Overlay_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Click fuera de la burbuja (sobre el fondo transparente) cierra el overlay,
            // salvo que ya se haya mandado una pregunta y esté mostrando la respuesta.
            if (!_answered && !IsMouseOverBubble(e))
                Close();
        }

        private bool IsMouseOverBubble(MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(Bubble);
            return pos.X >= 0 && pos.Y >= 0 && pos.X <= Bubble.ActualWidth && pos.Y <= Bubble.ActualHeight;
        }

        private async Task AskAsync(string question)
        {
            _answered = true;
            QuestionBox.IsEnabled = false;
            QuestionBox.Text = string.Empty;
            Placeholder.Text = "Jarvis está pensando...";
            Placeholder.Visibility = Visibility.Visible;

            var settings = SettingsService.Load();

            string? screenshot = null;
            try { screenshot = ScreenCaptureService.CaptureRegionAsBase64Png(_screenBounds); }
            catch { /* seguimos sin imagen si falla */ }

            string rawResponse = await GeminiService.SendAsync(settings.ApiKey, settings.Model, question, screenshot, null);

            // Limpia el Markdown (**negrita**, #títulos, etc.) que devuelve Gemini antes de mostrarlo
            // y antes de leerlo en voz alta, para no dejar símbolos sueltos tipo "***********" a la vista.
            string response = TextUtils.ForDisplay(rawResponse);

            InputRow.Visibility = Visibility.Collapsed;
            ResponseText.Text = response;
            ResponseScroll.Visibility = Visibility.Visible;

            // El aro de luz se queda pulsando exactamente mientras Jarvis está hablando, en vez de
            // usar un tiempo fijo que antes se quedaba corto o largo respecto al audio real.
            if (settings.AutoSpeak)
            {
                await SpeakAndWaitAsync(response, settings);
            }
            else
            {
                // Sin voz: le damos tiempo de leer el texto a ojo (según su largo) antes de cerrar solo.
                int words = response.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                double readSeconds = Math.Clamp(words / 3.3, 4, 20);
                await Task.Delay(TimeSpan.FromSeconds(readSeconds));
            }

            // Un respiro corto después de terminar de hablar/leer, y se cierra --salvo que el usuario
            // ya lo haya cerrado antes con Escape o haciendo click afuera.
            await Task.Delay(900);
            if (IsVisible) Close();
        }

        /// <summary>
        /// Manda a hablar y espera al evento real SpeakCompleted de VoiceService (en vez de un
        /// Task.Delay fijo), con un tope de seguridad por si algo raro pasa con el sintetizador.
        /// </summary>
        private Task SpeakAndWaitAsync(string text, AppSettings settings)
        {
            var tcs = new TaskCompletionSource<bool>();
            void OnCompleted()
            {
                _voice.SpeakCompleted -= OnCompleted;
                tcs.TrySetResult(true);
            }
            _voice.SpeakCompleted += OnCompleted;

            _voice.Speak(text, settings.VoiceName, settings.VoicePitch, settings.VoiceRate);

            var safetyTimeout = Task.Delay(TimeSpan.FromSeconds(60));
            return Task.WhenAny(tcs.Task, safetyTimeout).ContinueWith(_ =>
            {
                _voice.SpeakCompleted -= OnCompleted;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
