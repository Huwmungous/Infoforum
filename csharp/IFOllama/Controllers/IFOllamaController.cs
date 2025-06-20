
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading;
using Microsoft.AspNetCore.Authorization;

namespace IFOllama.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IFOllamaController(ILogger<IFOllamaController> logger) : ControllerBase
    {
        private readonly ILogger<IFOllamaController> _logger = logger;

        [HttpGet("test")]
        public IActionResult Test()
        {
            _logger.LogInformation("Test endpoint hit.");
            _ = this.HttpContext?.RequestAborted ?? CancellationToken.None;
            return Ok("Working");
        }
    }
}
