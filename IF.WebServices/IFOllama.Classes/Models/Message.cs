namespace IFOllama.Classes.Models;

public class Message
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<FileAttachment>? Attachments { get; set; }
    
    public Message() { }
     
    public Message(string role, string content)
    {
        Role = role;
        Content = content;
        Timestamp = DateTime.UtcNow;
    }
}
