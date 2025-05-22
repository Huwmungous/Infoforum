// ILdapAuthorizationService.cs
namespace File_Manager
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