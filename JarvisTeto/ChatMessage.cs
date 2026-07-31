namespace JarvisTeto.Models
{
    public class ChatMessage
    {
        public string Text { get; set; } = string.Empty;
        public bool IsUser { get; set; }
        public bool HasImage { get; set; }
        public string Timestamp { get; set; } = DateTime.Now.ToString("HH:mm");
    }
}
