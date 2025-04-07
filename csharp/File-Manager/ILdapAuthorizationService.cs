// ILdapAuthorizationService.cs
using System.Threading.Tasks;

namespace FileManager.Api.Services
{
    public enum AccessLevel
    {
        Read,
        Write,
        Delete
    }

    public interface ILdapAuthorizationService
    {
        Task<bool> HasAccessAsync(string username, string path, AccessLevel level = AccessLevel.Read);
    }
}