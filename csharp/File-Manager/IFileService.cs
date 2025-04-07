// IFileService.cs
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FileManager.Api.Services
{
    public interface IFileService
    {
        Task<IEnumerable<FileSystemNode>> GetRootNodesAsync();
        Task<IEnumerable<FileSystemNode>> GetChildrenAsync(string path);
        Task<FileInfo> GetFileInfoAsync(string path);
        Task<Stream> GetFileStreamAsync(string path);
        Task<FileSystemNode> SaveFileAsync(IFormFile file, string path);
        Task DeleteNodeAsync(string path);
        Task<FolderNode> CreateFolderAsync(string path, string folderName);
    }

    public enum NodeType
    {
        File,
        Folder
    }

    public abstract class FileSystemNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public NodeType Type { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class FileNode : FileSystemNode
    {
        public FileNode()
        {
            Type = NodeType.File;
        }

        public long Size { get; set; }
        public string MimeType { get; set; }
    }

    public class FolderNode : FileSystemNode
    {
        public FolderNode()
        {
            Type = NodeType.Folder;
        }
    }
}