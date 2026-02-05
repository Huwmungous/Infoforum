using IFOllama.Classes.Models;

namespace IFOllama.WebService.Data;

/// <summary>
/// Minimal persistence contract used by the chat UI.
/// </summary>
public interface IConversationStore
{
    /// <summary>Returns the list of conversations (id + title), newest first.</summary>
    Task<List<ConversationListItem>> ListAsync(string userId);

    /// <summary>Creates a new conversation with the given title and returns its id + title.</summary>
    Task<ConversationListItem> CreateAsync(string title, string userId);

    /// <summary>Loads all messages for a conversation (in order).</summary>
    Task<List<Message>> ReadMessagesAsync(string conversationId, string userId);

    /// <summary>Appends a single message to a conversation.</summary>
    Task AppendMessageAsync(string conversationId, Message message, string userId);

    /// <summary>Removes a conversation and all its data.</summary>
    Task RemoveAsync(string conversationId, string userId);

    /// <summary>Checks if a user owns a conversation.</summary>
    Task<bool> OwnsConversationAsync(string conversationId, string userId);
}
