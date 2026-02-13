using IFOllama.Classes.Models;
using System.Text.Json;

namespace IFOllama.WebService.Data;

public class ConversationStore : IConversationStore
{
    private readonly string _conversationsPath;
    private readonly ILogger<ConversationStore> _logger;

    public ConversationStore(IConfiguration configuration, ILogger<ConversationStore> logger)
    {
        _logger = logger;
        _conversationsPath = configuration["Storage:ConversationsPath"] ?? "Data/Conversations";
        Directory.CreateDirectory(_conversationsPath);
    }

    public async Task<bool> OwnsConversationAsync(string conversationId, string userId)
    {
        var metaPath = Path.Combine(_conversationsPath, conversationId, "meta.json");
        if (!File.Exists(metaPath))
        {
            _logger.LogWarning("Meta file not found for conversation {ConversationId}", conversationId);
            return false;
        }

        var json = await File.ReadAllTextAsync(metaPath);
        var meta = JsonSerializer.Deserialize<ConversationListItem>(json);

        return meta?.UserId == userId;
    }

    public async Task<List<ConversationListItem>> ListAsync(string userId)
    {
        var result = new List<ConversationListItem>();
        if (!Directory.Exists(_conversationsPath)) return result;

        foreach (var dir in Directory.GetDirectories(_conversationsPath))
        {
            var metaPath = Path.Combine(dir, "meta.json");
            if (File.Exists(metaPath))
            {
                await using var strm = File.OpenRead(metaPath);
                var meta = await JsonSerializer.DeserializeAsync<ConversationListItem>(strm);
                if (meta != null && meta.UserId == userId)
                    result.Add(meta);
            }
        }

        return result.OrderByDescending(c => c.Id).ToList();
    }

    public async Task<ConversationListItem> CreateAsync(string title, string userId)
    {
        var id = Guid.NewGuid().ToString("N");
        var item = new ConversationListItem(id, title, userId);

        var dir = Path.Combine(_conversationsPath, id);
        Directory.CreateDirectory(dir);

        var metaPath = Path.Combine(dir, "meta.json");
        var messagesPath = Path.Combine(dir, "messages.json");

        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(item));
        await File.WriteAllTextAsync(messagesPath, "[]");

        _logger.LogInformation("Created conversation {Id} for user {UserId}", id, userId);
        return item;
    }

    public async Task<List<Message>> ReadMessagesAsync(string conversationId, string userId)
    {
        var metaPath = Path.Combine(_conversationsPath, conversationId, "meta.json");
        if (!File.Exists(metaPath)) return [];

        var meta = JsonSerializer.Deserialize<ConversationListItem>(await File.ReadAllTextAsync(metaPath));
        if (meta?.UserId != userId) throw new UnauthorizedAccessException();

        var messagesPath = Path.Combine(_conversationsPath, conversationId, "messages.json");
        if (!File.Exists(messagesPath)) return [];

        await using var stream = File.OpenRead(messagesPath);
        var messages = await JsonSerializer.DeserializeAsync<List<Message>>(stream);
        return messages ?? [];
    }

    public async Task AppendMessageAsync(string conversationId, Message message, string userId)
    {
        var metaPath = Path.Combine(_conversationsPath, conversationId, "meta.json");
        if (!File.Exists(metaPath)) throw new FileNotFoundException();

        var meta = JsonSerializer.Deserialize<ConversationListItem>(
            await File.ReadAllTextAsync(metaPath)
        );
        if (meta?.UserId != userId) throw new UnauthorizedAccessException();

        var messagesPath = Path.Combine(_conversationsPath, conversationId, "messages.json");

        List<Message> messages;

        if (File.Exists(messagesPath))
        {
            await using var stream = File.OpenRead(messagesPath);
            messages = await JsonSerializer.DeserializeAsync<List<Message>>(stream) ?? [];
        }
        else
        {
            messages = [];
        }

        messages.Add(message);

        await File.WriteAllTextAsync(
            messagesPath,
            JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true })
        );
    }

    public async Task UpdateTitleAsync(string conversationId, string newTitle, string userId)
    {
        var metaPath = Path.Combine(_conversationsPath, conversationId, "meta.json");
        if (!File.Exists(metaPath))
            throw new FileNotFoundException($"Conversation {conversationId} not found");

        var meta = JsonSerializer.Deserialize<ConversationListItem>(
            await File.ReadAllTextAsync(metaPath));

        if (meta?.UserId != userId)
            throw new UnauthorizedAccessException();

        var updated = new ConversationListItem(meta.Id, newTitle, meta.UserId);
        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(updated));

        _logger.LogInformation("Updated title for conversation {Id}: {Title}", conversationId, newTitle);
    }

    public async Task RemoveAsync(string conversationId, string userId)
    {
        var metaPath = Path.Combine(_conversationsPath, conversationId, "meta.json");
        if (!File.Exists(metaPath)) return;

        var meta = JsonSerializer.Deserialize<ConversationListItem>(await File.ReadAllTextAsync(metaPath));
        if (meta?.UserId != userId) throw new UnauthorizedAccessException();

        var dir = Path.Combine(_conversationsPath, conversationId);
        Directory.Delete(dir, recursive: true);

        _logger.LogInformation("Removed conversation {Id}", conversationId);
    }
}
