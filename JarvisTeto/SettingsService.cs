using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace JarvisTeto.Services
{
    public class AppSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-3.6-flash";
        public bool AutoSpeak { get; set; } = true;
        public string HotkeyInfo { get; set; } = "Ctrl+Alt+J";
        public string VoiceName { get; set; } = string.Empty; // vacío = Jarvis elige la mejor disponible
        public int VoicePitch { get; set; } = -4;  // -10 a 10 (negativo = más grave)
        public int VoiceRate { get; set; } = -1;   // -10 a 10 (velocidad de habla)

        // Posición guardada del widget flotante (null = todavía no se movió, usar posición por defecto).
        public double? WidgetLeft { get; set; }
        public double? WidgetTop { get; set; }
    }

    /// <summary>
    /// Guarda la configuración (incluida la API key) cifrada con DPAPI
    /// (ligada al usuario de Windows actual) en %AppData%\JarvisTeto\settings.dat
    /// </summary>
    public static class SettingsService
    {
        private static readonly string Folder =
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JarvisTeto");

        private static readonly string FilePath = System.IO.Path.Combine(Folder, "settings.dat");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new AppSettings();

                byte[] encrypted = File.ReadAllBytes(FilePath);
                byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(plain);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            Directory.CreateDirectory(Folder);
            string json = JsonSerializer.Serialize(settings);
            byte[] plain = Encoding.UTF8.GetBytes(json);
            byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, encrypted);
        }
    }
}
