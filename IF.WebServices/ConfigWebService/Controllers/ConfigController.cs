using ConfigWebService.Models;
using Microsoft.AspNetCore.Mvc;
using IFGlobal.Models;

namespace ConfigWebService.Controllers
{
    /// <summary>
    /// Configuration endpoint that provides client configuration based on cfg and type parameters
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class ConfigController(ILogger<ConfigController> logger, IConfiguration configuration) : ControllerBase
    {
        private readonly ILogger<ConfigController> _logger = logger;
        private readonly IConfiguration _configuration = configuration;

        /// <summary>
        /// Get configuration based on cfg type and user/service type
        /// </summary>
        /// <param name="cfg">Configuration type (e.g., 'oidc', 'bootstrap')</param>
        /// <param name="type">Client type: 'user', 'service', or 'patient'</param>
        /// <param name="realm">Realm name</param>
        /// <param name="client">Client base name</param>
        /// <returns>Configuration object appropriate for the request</returns>
        /// <response code="200">Returns the requested configuration</response>
        /// <response code="400">If cfg or type parameter is missing or invalid</response>
        /// <response code="401">If authentication token is missing or invalid</response>
        /// <response code="500">If configuration is not found or an internal error occurs</response>
        [HttpGet]
        [ProducesResponseType(typeof(Bootstrap), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(PGConnectionConfig), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public IActionResult Get([FromQuery] string cfg, [FromQuery] string type, [FromQuery] string realm, [FromQuery] string client)
        {
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(cfg))
                return BadRequest(new ErrorResponse { Error = "Parameter 'cfg' is required" });

            if (string.IsNullOrWhiteSpace(type))
                return BadRequest(new ErrorResponse { Error = "Parameter 'type' is required" });

            // Normalize to lowercase for case-insensitive comparison
            cfg = cfg.ToLowerInvariant();
            type = type.ToLowerInvariant();

            // If cfg is 'bootstrap', allow unauthenticated access
            if (cfg == "bootstrap")
            {
                if (type != "user" && type != "service" && type != "patient")
                    return BadRequest(new ErrorResponse { Error = "Parameter 'type' must be 'user', 'service', or 'patient'" });

                return GetBootstrapConfig(type, realm, client);
            }

            // For all other cfg values, require authentication
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return Unauthorized();
            }

            switch (cfg)
            {
                case "firebirddb":
                    return Ok(new FBConnectionConfig()
                    {
                        Host = "syden-ses-vm",
                        Port = 3050,
                        Database = "C:\\ReferenceDBs\\DEV\\ENGLAND.FDB",
                        UserName = "8A131580",
                        Password = "22101883",
                        Charset = "UTF8",
                        Role = "RDB$USER",
                        RequiresRelay = false  // true for sites with off-prem services and on-prem database
                    });

                case "grafanadb":
                    return Ok(new PGConnectionConfig()
                    {
                        Host = "intelligence",
                        Port = 6543,
                        Database = "Sfd_Log",
                        UserName = "grafana_service",
                        Password = "T4!bN8#wQ2@hM6$zF3",
                        RequiresRelay = false
                    });

                case "loggerdb":
                    return Ok(new PGConnectionConfig()
                    {
                        Host = "intelligence",
                        Port = 6543,
                        Database = "Sfd_Log",
                        UserName = "logger_service",
                        Password = "K9#mP7$vL2@nX4!qR8",
                        RequiresRelay = false
                    });

                case "delphianalysisdb":
                    return Ok(new PGConnectionConfig()
                    {
                        Host = "intelligence",
                        Port = 6543,
                        Database = "Sfd_DelphiAnalysis",
                        UserName = "analysis_service",
                        Password = "XB5h+1z8jNRo7JVnhv5xXw==",
                        RequiresRelay = false
                    });

                default:
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning("Unhandled configuration request: cfg={Cfg}, type={Type}", cfg, type);
                    }
                    break;
            }

            return BadRequest(new ErrorResponse { Error = "Unhandled Configuration Request" });
        }

        /// <summary>
        /// Get Bootstrap configuration (legacy support)
        /// </summary>
        private IActionResult GetBootstrapConfig(string type, string realm, string client)
        {
            try
            {
                var auth = _configuration["OidcConfig:Authority"];
                var loggerServiceUrl = _configuration["LoggerService"];
                var clientId = type == "user"
                   ? $"{client}-usr"
                   : type == "patient"
                       ? $"{client}-pps"
                       : $"{client}-svc";

                if (string.IsNullOrWhiteSpace(auth) || string.IsNullOrWhiteSpace(clientId))
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError("Bootstrap configuration not found for type: {Type}", type);
                    }
                    return StatusCode(500, new ErrorResponse { Error = $"Bootstrap configuration not found for type: {type}" });
                }

                var config = new Bootstrap
                {
                    OpenIdConfig = $"{auth}/auth/realms",
                    Realm = realm,
                    ClientId = clientId,
                    LoggerService = loggerServiceUrl ?? String.Empty,
                    LogLevel = LogLevel.Debug,
                    ClientSecret = type == "service" ? "SpplDs3eJ3M61Z6KEmKFn3gxXqJXEa58" : string.Empty
                };

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Returning Bootstrap config for type: {Type}, ClientId: {ClientId}", type, clientId);
                }
                return Ok(config);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "Error retrieving Bootstrap configuration for type: {Type}", type);
                }
                return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
            }
        }
    }
}