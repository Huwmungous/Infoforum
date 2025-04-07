// FileService.cs
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileManager.Api.Services
{
    public class FileService : IFileService
    {
        private readonly string _basePath;
        private readonly IConfiguration _configuration;

        public FileService(IConfiguration configuration)
        {
            _configuration = configuration;
            _basePath = _configuration["FileStorage:BasePath"];

            // Ensure base directory exists
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
        }

        public async Task<IEnumerable<FileSystemNode>> GetRootNodesAsync()
        {
            var result = new List<FileSystemNode>();

            // This could be configurable based on your requirements
            var rootFolders = new[] { "Documents", "Images", "Videos" };

            foreach (var folder in rootFolders)
            {
                var folderPath = Path.Combine(_basePath, folder);

                // Create folder if it doesn't exist
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var dirInfo = new DirectoryInfo(folderPath);

                result.Add(new FolderNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = folder,
                    Path = $"/{folder}",
                    LastModified = dirInfo.LastWriteTime
                });
            }

            return await Task.FromResult(result);
        }

        public async Task<IEnumerable<FileSystemNode>> GetChildrenAsync(string path)
        {
            var result = new List<FileSystemNode>();
            var fullPath = GetFullPath(path);

            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            }

            // Get directories
            foreach (var directory in Directory.GetDirectories(fullPath))
            {
                var dirInfo = new DirectoryInfo(directory);
                result.Add(new FolderNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = dirInfo.Name,
                    Path = CombineVirtualPath(path, dirInfo.Name),
                    LastModified = dirInfo.LastWriteTime
                });
            }

            // Get files
            foreach (var file in Directory.GetFiles(fullPath))
            {
                var fileInfo = new FileInfo(file);
                result.Add(new FileNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = fileInfo.Name,
                    Path = CombineVirtualPath(path, fileInfo.Name),
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    MimeType = GetMimeType(fileInfo.Extension)
                });
            }

            return await Task.FromResult(result);
        }

        public async Task<FileInfo?> GetFileInfoAsync(string path)
        {
            FileInfo result = null;

            await Task.Run(() =>
            {
                var fullPath = GetFullPath(path);

                if (!System.IO.File.Exists(fullPath))
                {
                    result = null;
                }

                else
                {
                    result = new FileInfo(fullPath);
                }
            });
            return result;
        }

        public async Task<Stream> GetFileStreamAsync(string path)
        {
            FileStream result = null;

            await Task.Run(() => 
            {

                var fullPath = GetFullPath(path);

                if (!System.IO.File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"File not found: {path}");
                }

                result = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            });

            return result;
        }

        public async Task<FileSystemNode> SaveFileAsync(IFormFile file, string path)
        {
            var fullPath = GetFullPath(path);

            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            }

            var filePath = Path.Combine(fullPath, file.FileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileInfo = new FileInfo(filePath);

            return new FileNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = fileInfo.Name,
                Path = CombineVirtualPath(path, fileInfo.Name),
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                MimeType = GetMimeType(fileInfo.Extension)
            };
        }

        public async Task DeleteNodeAsync(string path)
        {
            var fullPath = GetFullPath(path);

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
            else if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
            }
            else
            {
                throw new FileNotFoundException($"Node not found: {path}");
            }
        }

        public async Task<FolderNode> CreateFolderAsync(string path, string folderName)
        {
            var parentPath = GetFullPath(path);

            if (!Directory.Exists(parentPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            }

            var newFolderPath = Path.Combine(parentPath, folderName);

            if (Directory.Exists(newFolderPath))
            {
                throw new InvalidOperationException($"Folder already exists: {folderName}");
            }

            Directory.CreateDirectory(newFolderPath);

            var dirInfo = new DirectoryInfo(newFolderPath);

            return new FolderNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = folderName,
                Path = CombineVirtualPath(path, folderName),
                LastModified = dirInfo.LastWriteTime
            };
        }

        private string GetFullPath(string virtualPath)
        {
            // Remove leading slash if present
            if (virtualPath.StartsWith("/"))
            {
                virtualPath = virtualPath.Substring(1);
            }

            return Path.Combine(_basePath, virtualPath);
        }

        private string CombineVirtualPath(string path1, string path2)
        {
            if (!path1.EndsWith("/"))
            {
                path1 = path1 + "/";
            }

            return path1 + path2;
        }

        private string GetMimeType(string extension)
        {
            // Simple mapping, could be expanded or use a proper MIME type library
            var mimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ".txt", "text/plain" },
                { ".pdf", "application/pdf" },
                { ".doc", "application/msword" },
                { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
                { ".xls", "application/vnd.ms-excel" },
                { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                { ".png", "image/png" },
                { ".jpg", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".gif", "image/gif" }
            };

            if (mimeTypes.TryGetValue(extension, out string mime))
            {
                return mime;
            }

            return "application/octet-stream";
        }
    }
}