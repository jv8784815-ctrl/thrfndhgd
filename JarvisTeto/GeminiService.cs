using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace JarvisTeto.Services
{
    public class GeminiService
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        /// <summary>
        /// Envía un prompt de texto, opcionalmente con una imagen (screenshot) adjunta en base64 PNG,
        /// al modelo de Gemini configurado. Usa gemini-2.0-flash por defecto para respuestas rápidas.
        /// </summary>
        public static async Task<string> SendAsync(string apiKey, string model, string prompt, string? imageBase64Png, List<(string role, string text)>? history = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return "⚠ No configuraste tu API key de Google todavía. Abrí Configuración y pegala ahí.";

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var contents = new List<object>();

            // Historial breve para dar contexto de la conversación
            if (history != null)
            {
                foreach (var (role, text) in history)
                {
                    contents.Add(new
                    {
                        role = role == "user" ? "user" : "model",
                        parts = new object[] { new { text } }
                    });
                }
            }

            var parts = new List<object> { new { text = prompt } };
            if (!string.IsNullOrEmpty(imageBase64Png))
            {
                parts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = "image/png",
                        data = imageBase64Png
                    }
                });
            }

            contents.Add(new { role = "user", parts });

            var body = new
            {
                contents,
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 1024
                },
                systemInstruction = new
                {
                    parts = new object[]
                    {
                        new { text = "Sos Jarvis, un asistente de escritorio estilo Kasane Teto: directo, ágil, con actitud, " +
                                     "que responde en español y va al grano. Si te pasan una captura de pantalla, describí " +
                                     "solo lo relevante para ayudar, sin relleno." }
                    }
                }
            };

            string json = JsonSerializer.Serialize(body);

            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await Http.PostAsync(url, content);
                string responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return $"⚠ Error de la API ({(int)response.StatusCode}): {ExtractErrorMessage(responseText)}";
                }

                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var partsEl = candidates[0].GetProperty("content").GetProperty("parts");
                    var sb = new StringBuilder();
                    foreach (var p in partsEl.EnumerateArray())
                    {
                        if (p.TryGetProperty("text", out var t))
                            sb.Append(t.GetString());
                    }
                    return sb.Length > 0 ? sb.ToString() : "⚠ La API no devolvió texto.";
                }

                return "⚠ No se recibió respuesta válida de Gemini.";
            }
            catch (TaskCanceledException)
            {
                return "⚠ La API tardó demasiado en responder (timeout).";
            }
            catch (Exception ex)
            {
                return $"⚠ Error de conexión: {ex.Message}";
            }
        }

        private static string ExtractErrorMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var err) &&
                    err.TryGetProperty("message", out var msg))
                    return msg.GetString() ?? json;
                return json;
            }
            catch
            {
                return json;
            }
        }
    }
}
