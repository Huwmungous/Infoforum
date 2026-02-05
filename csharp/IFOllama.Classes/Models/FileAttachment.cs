namespace IFOllama.Classes.Models;

public class FileAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public FileContentType FileType { get; set; }
}

public enum FileContentType
{
    Unknown,
    Image,
    Document,
    Text,
    Pdf,
    Zip
}
