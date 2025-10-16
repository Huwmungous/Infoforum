using System.Text;
using System.Text.Json;

namespace FileSystemMcpServer
{
    public class FileSystemTools
    {
        public async Task<object> ReadFile(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;
            var encoding = args.TryGetProperty("encoding", out var enc) ? enc.GetString() : "utf-8";

            var encodingObj = GetEncoding(encoding);
            var content = await File.ReadAllTextAsync(path, encodingObj);

            return new
            {
                success = true,
                content,
                path,
                size = new FileInfo(path).Length
            };
        }

        public async Task<object> WriteFile(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;
            var content = args.GetProperty("content").GetString()!;
            var encoding = args.TryGetProperty("encoding", out var enc) ? enc.GetString() : "utf-8";

            var encodingObj = GetEncoding(encoding);
            
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, content, encodingObj);

            return new
            {
                success = true,
                path,
                bytesWritten = encodingObj.GetByteCount(content)
            };
        }

        public async Task<object> AppendFile(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;
            var content = args.GetProperty("content").GetString()!;

            await File.AppendAllTextAsync(path, content);

            return new
            {
                success = true,
                path
            };
        }

        public Task<object> DeleteFile(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;

            if (File.Exists(path))
            {
                File.Delete(path);
                return Task.FromResult<object>(new { success = true, path });
            }

            throw new FileNotFoundException($"File not found: {path}");
        }

        public Task<object> ListDirectory(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;
            var pattern = args.TryGetProperty("pattern", out var pat) ? pat.GetString() : "*";
            var recursive = args.TryGetProperty("recursive", out var rec) && rec.GetBoolean();

            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"Directory not found: {path}");

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            
            var files = Directory.GetFiles(path, pattern ?? "*", searchOption)
                .Select(f => new
                {
                    path = f,
                    name = Path.GetFileName(f),
                    type = "file",
                    size = new FileInfo(f).Length,
                    lastModified = new FileInfo(f).LastWriteTimeUtc.ToString("o")
                });

            var directories = Directory.GetDirectories(path, pattern ?? "*", searchOption)
                .Select(d => new
                {
                    path = d,
                    name = Path.GetFileName(d),
                    type = "directory",
                    size = (long?)null,
                    lastModified = new DirectoryInfo(d).LastWriteTimeUtc.ToString("o")
                });

            var items = files.Concat<object>(directories).ToList();

            return Task.FromResult<object>(new
            {
                success = true,
                path,
                itemCount = items.Count,
                items
            });
        }

        public async Task<object> SearchFiles(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;
            var searchTerm = args.GetProperty("searchTerm").GetString()!;
            var filePattern = args.TryGetProperty("filePattern", out var pat) ? pat.GetString() : "*";
            var recursive = !args.TryGetProperty("recursive", out var rec) || rec.GetBoolean();

            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"Directory not found: {path}");

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(path, filePattern ?? "*", searchOption);

            var results = new List<object>();

            foreach (var file in files)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var lines = content.Split('\n');

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(new
                            {
                                file,
                                line = i + 1,
                                content = lines[i].Trim(),
                                column = lines[i].IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase)
                            });
                        }
                    }
                }
                catch
                {
                }
            }

            return new
            {
                success = true,
                searchTerm,
                path,
                matchCount = results.Count,
                matches = results
            };
        }

        public Task<object> GetFileInfo(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;

            if (File.Exists(path))
            {
                var fileInfo = new FileInfo(path);
                return Task.FromResult<object>(new
                {
                    success = true,
                    path,
                    type = "file",
                    size = fileInfo.Length,
                    created = fileInfo.CreationTimeUtc.ToString("o"),
                    lastModified = fileInfo.LastWriteTimeUtc.ToString("o"),
                    lastAccessed = fileInfo.LastAccessTimeUtc.ToString("o"),
                    extension = fileInfo.Extension,
                    isReadOnly = fileInfo.IsReadOnly
                });
            }
            else if (Directory.Exists(path))
            {
                var dirInfo = new DirectoryInfo(path);
                return Task.FromResult<object>(new
                {
                    success = true,
                    path,
                    type = "directory",
                    created = dirInfo.CreationTimeUtc.ToString("o"),
                    lastModified = dirInfo.LastWriteTimeUtc.ToString("o"),
                    lastAccessed = dirInfo.LastAccessTimeUtc.ToString("o")
                });
            }

            throw new FileNotFoundException($"Path not found: {path}");
        }

        public Task<object> CreateDirectory(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;

            Directory.CreateDirectory(path);

            return Task.FromResult<object>(new
            {
                success = true,
                path,
                created = Directory.Exists(path)
            });
        }

        public Task<object> MoveFile(JsonElement args)
        {
            var sourcePath = args.GetProperty("sourcePath").GetString()!;
            var destPath = args.GetProperty("destPath").GetString()!;

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Source file not found: {sourcePath}");

            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Move(sourcePath, destPath, overwrite: true);

            return Task.FromResult<object>(new
            {
                success = true,
                sourcePath,
                destPath
            });
        }

        public Task<object> CopyFile(JsonElement args)
        {
            var sourcePath = args.GetProperty("sourcePath").GetString()!;
            var destPath = args.GetProperty("destPath").GetString()!;

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Source file not found: {sourcePath}");

            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(sourcePath, destPath, overwrite: true);

            return Task.FromResult<object>(new
            {
                success = true,
                sourcePath,
                destPath
            });
        }

        public Task<object> FileExists(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;

            return Task.FromResult<object>(new
            {
                exists = File.Exists(path),
                path
            });
        }

        public Task<object> DirectoryExists(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;

            return Task.FromResult<object>(new
            {
                exists = Directory.Exists(path),
                path
            });
        }

        private static Encoding GetEncoding(string? encoding)
        {
            return encoding?.ToLowerInvariant() switch
            {
                "utf-8" or "utf8" or null => Encoding.UTF8,
                "ascii" => Encoding.ASCII,
                "utf-16" or "utf16" or "unicode" => Encoding.Unicode,
                "utf-32" or "utf32" => Encoding.UTF32,
                _ => Encoding.UTF8
            };
        }
    }
}
