using System.Speech.Recognition;
using System.Speech.Synthesis;
using JarvisTeto.Utils;

namespace JarvisTeto.Services
{
    public class VoiceService : IDisposable
    {
        private SpeechRecognitionEngine? _recognizer;
        private readonly SpeechSynthesizer _synthesizer = new();
        private bool _listening;
        private string _currentVoiceName = string.Empty;

        public event Action<string>? SpeechRecognized;
        public event Action? ListeningStopped;

        /// <summary>Se dispara justo cuando arranca a sonar el audio (no cuando se encola).</summary>
        public event Action? SpeakStarted;

        /// <summary>Se dispara cuando termina de sonar el audio, sea porque terminó solo o se canceló.</summary>
        public event Action? SpeakCompleted;

        /// <summary>
        /// Se dispara por cada palabra que efectivamente se está pronunciando (word boundary de SAPI).
        /// Sirve para sincronizar animaciones (la esfera de orbes, el brillo del overlay) con el
        /// ritmo real del habla en vez de con un tiempo fijo inventado.
        /// </summary>
        public event Action? WordSpoken;

        public bool IsListening => _listening;
        public bool IsSpeaking { get; private set; }

        public VoiceService()
        {
            _synthesizer.SpeakStarted += (s, e) =>
            {
                IsSpeaking = true;
                SpeakStarted?.Invoke();
            };
            _synthesizer.SpeakCompleted += (s, e) =>
            {
                IsSpeaking = false;
                SpeakCompleted?.Invoke();
            };
            _synthesizer.SpeakProgress += (s, e) => WordSpoken?.Invoke();
        }

        public void StartListening()
        {
            if (_listening) return;
            try
            {
                _recognizer = new SpeechRecognitionEngine();
                _recognizer.SetInputToDefaultAudioDevice();
                _recognizer.LoadGrammar(new DictationGrammar());
                _recognizer.SpeechRecognized += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Result.Text))
                        SpeechRecognized?.Invoke(e.Result.Text);
                };
                _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                _listening = true;
            }
            catch (Exception)
            {
                _listening = false;
                throw;
            }
        }

        public void StopListening()
        {
            if (!_listening) return;
            _recognizer?.RecognizeAsyncStop();
            _recognizer?.Dispose();
            _recognizer = null;
            _listening = false;
            ListeningStopped?.Invoke();
        }

        /// <summary>
        /// Lista los nombres de todas las voces instaladas en Windows (Configuración > Hora e idioma > Voz),
        /// para que el usuario elija cuál usar como "voz de Jarvis".
        /// </summary>
        public List<string> GetInstalledVoiceNames()
        {
            var names = new List<string>();
            try
            {
                foreach (var v in _synthesizer.GetInstalledVoices())
                {
                    if (v.Enabled)
                        names.Add(v.VoiceInfo.Name);
                }
            }
            catch { /* si falla, devolvemos lista vacía y se usa la voz por defecto */ }
            return names;
        }

        /// <summary>
        /// Elige automáticamente la voz más parecida a un "asistente tipo Jarvis": prioriza las voces
        /// "Natural"/neuronales de Windows 11 (suenan muchísimo mejor que las SAPI clásicas), después
        /// cualquier voz masculina británica, después masculina en inglés, y de último cualquiera.
        ///
        /// Nota honesta: esto elige la MEJOR voz disponible en Windows, pero no es (ni puede ser) un
        /// clon idéntico de la voz de la película, que es una interpretación de un actor real y está
        /// protegida por derechos de autor. Para algo mucho más cercano al timbre real de Jarvis, lo que
        /// existe hoy son voces premium tipo Azure Neural (p. ej. "en-GB-RyanNeural") o ElevenLabs; te
        /// dejo el enganche listo en Settings por si más adelante querés cablear una de esas APIs.
        /// </summary>
        public string PickDefaultJarvisVoice()
        {
            try
            {
                var voices = _synthesizer.GetInstalledVoices()
                    .Where(v => v.Enabled)
                    .Select(v => v.VoiceInfo)
                    .ToList();

                if (voices.Count == 0) return string.Empty;

                // Las voces "Natural" de Windows 11 (neuronales) tienen "Natural" en el nombre.
                var natural = voices.FirstOrDefault(v =>
                    v.Gender == VoiceGender.Male &&
                    v.Culture.TwoLetterISOLanguageName == "en" &&
                    v.Name.Contains("Natural", StringComparison.OrdinalIgnoreCase));
                if (natural != null) return natural.Name;

                string[] preferredKeywords = { "ryan", "george", "david", "guy", "mark" };

                var enGbPreferred = voices.FirstOrDefault(v =>
                    v.Culture.Name.Equals("en-GB", StringComparison.OrdinalIgnoreCase) &&
                    preferredKeywords.Any(k => v.Name.Contains(k, StringComparison.OrdinalIgnoreCase)));
                if (enGbPreferred != null) return enGbPreferred.Name;

                var enGb = voices.FirstOrDefault(v => v.Culture.Name.Equals("en-GB", StringComparison.OrdinalIgnoreCase)
                                                       && v.Gender == VoiceGender.Male);
                if (enGb != null) return enGb.Name;

                var enPreferred = voices.FirstOrDefault(v =>
                    v.Culture.TwoLetterISOLanguageName == "en" &&
                    preferredKeywords.Any(k => v.Name.Contains(k, StringComparison.OrdinalIgnoreCase)));
                if (enPreferred != null) return enPreferred.Name;

                var anyEnglishMale = voices.FirstOrDefault(v => v.Culture.TwoLetterISOLanguageName == "en" && v.Gender == VoiceGender.Male);
                if (anyEnglishMale != null) return anyEnglishMale.Name;

                var anyEnglish = voices.FirstOrDefault(v => v.Culture.TwoLetterISOLanguageName == "en");
                if (anyEnglish != null) return anyEnglish.Name;

                var anySpanish = voices.FirstOrDefault(v => v.Culture.TwoLetterISOLanguageName == "es");
                if (anySpanish != null) return anySpanish.Name;

                return voices[0].Name;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Reproduce audio previsualizando la voz elegida con el tono/velocidad configurados,
        /// sin depender de los ajustes guardados (para el botón "Probar voz" en Configuración).
        /// </summary>
        public void PreviewVoice(string voiceName, int pitch, int rate)
            => Speak("Hola, soy Jarvis. Así sonaré a partir de ahora.", voiceName, pitch, rate);

        /// <summary>
        /// Habla el texto dado usando la voz, tono y velocidad indicados. Primero limpia cualquier
        /// resto de Markdown (**, #, backticks, etc.) para no leer símbolos en voz alta. El tono
        /// (pitch) y un leve "contour" se logran vía SSML porque System.Speech no expone una
        /// propiedad nativa de pitch; el contour agrega esa cadencia un poco más marcada/sintética
        /// que ayuda a que suene menos "lectura plana" y más "asistente".
        /// </summary>
        public void Speak(string text, string? voiceName, int pitch, int rate)
        {
            try
            {
                string clean = TextUtils.ForSpeech(text);
                if (string.IsNullOrWhiteSpace(clean)) return;

                _synthesizer.SpeakAsyncCancelAll();

                SelectVoiceIfNeeded(voiceName);

                _synthesizer.Rate = Math.Clamp(rate, -10, 10);

                string lang = "en-US";
                try { lang = _synthesizer.Voice.Culture.Name; } catch { /* usa en-US por defecto */ }

                int pitchPercent = Math.Clamp(pitch, -10, 10) * 2; // -20% a +20%
                string escaped = EscapeXml(clean);

                string ssml =
                    $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"{lang}\">" +
                    $"<prosody pitch=\"{pitchPercent}%\" contour=\"(0%,{pitchPercent}%) (50%,{pitchPercent - 2}%) (100%,{pitchPercent}%)\">" +
                    $"{escaped}</prosody></speak>";

                _synthesizer.SpeakSsmlAsync(ssml);
            }
            catch
            {
                // Si algo del SSML falla (voz sin soporte, etc.), como último recurso habla sin efectos.
                try { _synthesizer.SpeakAsync(TextUtils.ForSpeech(text)); } catch { /* sin audio disponible */ }
            }
        }

        private void SelectVoiceIfNeeded(string? voiceName)
        {
            string target = string.IsNullOrWhiteSpace(voiceName) ? PickDefaultJarvisVoice() : voiceName;
            if (string.IsNullOrWhiteSpace(target) || target == _currentVoiceName) return;

            try
            {
                _synthesizer.SelectVoice(target);
                _currentVoiceName = target;
            }
            catch
            {
                // si el nombre guardado ya no existe (voz desinstalada), se queda con la voz por defecto
            }
        }

        private static string EscapeXml(string text) =>
            text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&apos;");

        public void StopSpeaking() => _synthesizer.SpeakAsyncCancelAll();

        public void Dispose()
        {
            _recognizer?.Dispose();
            _synthesizer.Dispose();
        }
    }
}
