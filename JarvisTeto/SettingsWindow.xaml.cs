using System.Windows;
using System.Windows.Controls;
using JarvisTeto.Services;

namespace JarvisTeto
{
    public partial class SettingsWindow : Window
    {
        private readonly VoiceService _voice = new();

        public SettingsWindow()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                var settings = SettingsService.Load();
                ApiKeyBox.Password = settings.ApiKey;
                AutoSpeakCheck.IsChecked = settings.AutoSpeak;
                ModelCombo.SelectedIndex = settings.Model == "gemini-2.5-pro" ? 1 : 0;

                var voiceNames = _voice.GetInstalledVoiceNames();
                VoiceCombo.ItemsSource = voiceNames;

                string preselect = !string.IsNullOrWhiteSpace(settings.VoiceName) && voiceNames.Contains(settings.VoiceName)
                    ? settings.VoiceName
                    : _voice.PickDefaultJarvisVoice();

                VoiceCombo.SelectedItem = preselect;

                PitchSlider.Value = settings.VoicePitch;
                RateSlider.Value = settings.VoiceRate;
                PitchValueText.Text = settings.VoicePitch.ToString();
                RateValueText.Text = settings.VoiceRate.ToString();
            };

            Closed += (s, e) => _voice.Dispose();
        }

        private void PitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PitchValueText != null) PitchValueText.Text = ((int)e.NewValue).ToString();
        }

        private void RateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RateValueText != null) RateValueText.Text = ((int)e.NewValue).ToString();
        }

        private void PreviewVoice_Click(object sender, RoutedEventArgs e)
        {
            string? voiceName = VoiceCombo.SelectedItem as string;
            _voice.PreviewVoice(voiceName ?? string.Empty, (int)PitchSlider.Value, (int)RateSlider.Value);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var settings = new AppSettings
            {
                ApiKey = ApiKeyBox.Password.Trim(),
                Model = ModelCombo.SelectedIndex == 1 ? "gemini-2.5-pro" : "gemini-3.6-flash",
                AutoSpeak = AutoSpeakCheck.IsChecked ?? true,
                VoiceName = VoiceCombo.SelectedItem as string ?? string.Empty,
                VoicePitch = (int)PitchSlider.Value,
                VoiceRate = (int)RateSlider.Value
            };
            SettingsService.Save(settings);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
