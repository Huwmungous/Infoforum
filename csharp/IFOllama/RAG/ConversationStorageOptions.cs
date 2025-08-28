namespace IFOllama.RAG
{
    public class ConversationStorageOptions
    {
        /// <summary>
        /// Base folder for all conversation data. Can be absolute or relative.
        /// If relative, it is resolved against ASP.NET ContentRootPath.
        /// Default: "Conversations".
        /// </summary>
        public string BasePath { get; set; } = "Conversations";

        /// <summary>
        /// Maximum number of code files to include from /context when composing GetContext().
        /// </summary>
        public int MaxContextFiles { get; set; } = 10;
    }
}
