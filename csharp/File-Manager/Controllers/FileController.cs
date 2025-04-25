// FileController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace File_Manager.Controllers
{
    [ApiController]
    [Route("api/files")]
    [Authorize]
    public class FileController(IFileService fileService, ILdapAuthorizationService ldapAuth) : ControllerBase
    {
        private readonly IFileService _fileService = fileService;
        private readonly ILdapAuthorizationService _ldapAuth = ldapAuth;

        [HttpGet("root")]
        public async Task<IActionResult> GetRootNodes()
        {
            string? username = User.FindFirst(ClaimTypes.Name)?.Value;

            // Check if user has access to root
            if (!await _ldapAuth.HasAccessAsync(username, "/"))
            {
                return Forbid();
            }

            var rootNodes = await _fileService.GetRootNodesAsync();
            return Ok(rootNodes);
        }

        [HttpGet("children")]
        public async Task<IActionResult> GetChildren([FromQuery] string path)
        {
            string? username = User.FindFirst(ClaimTypes.Name)?.Value;

            // Check if user has access to the specified path
            if (!await _ldapAuth.HasAccessAsync(username, path))
            {
                return Forbid();
            }

            var children = await _fileService.GetChildrenAsync(path);
            return Ok(children);
        }

        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile([FromQuery] string path)
        {
            string? username = User.FindFirst(ClaimTypes.Name)?.Value;

            if (!await _ldapAuth.HasAccessAsync(username, path))
            {
                return Forbid();
            }

            var fileInfo = await _fileService.GetFileInfoAsync(path);
            if (fileInfo == null)
            {
                return NotFound();
            }

            var fileStream = await _fileService.GetFileStreamAsync(path);
            return File(fileStream, "application/octet-stream", fileInfo.Name);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string path)
        {
            string? username = User.FindFirst(ClaimTypes.Name)?.Value;

            if (!await _ldapAuth.HasAccessAsync(username, path, AccessLevel.Write))
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            var result = await _fileService.SaveFileAsync(file, path);
            return Ok(result);
        }

        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteNode([FromQuery] string path)
        {
            string? username = User.FindFirst(ClaimTypes.Name)?.Value;

            if (!await _ldapAuth.HasAccessAsync(username, path, AccessLevel.Delete))
            {
                return Forbid();
            }

            await _fileService.DeleteNodeAsync(path);
            return NoContent();
        }

        [HttpPost("folder")]
        public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request)
        {
            string? username = User.FindFirst(ClaimTypes.Name)?.Value;

            if (!await _ldapAuth.HasAccessAsync(username, request.Path, AccessLevel.Write))
            {
                return Forbid();
            }

            var result = await _fileService.CreateFolderAsync(request.Path, request.FolderName);
            return Ok(result);
        }
    }

    public class CreateFolderRequest
    {
        public string Path { get; set; }
        public string FolderName { get; set; }
    }
}