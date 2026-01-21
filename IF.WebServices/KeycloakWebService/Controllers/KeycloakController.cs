using KeycloakWebService.Models;
using KeycloakWebService.Services;
using Microsoft.AspNetCore.Mvc;

namespace KeycloakWebService.Controllers
{
    /// <summary>
    /// API controller for Keycloak operations
    /// </summary>

    [ApiController]
    [Route("api/[controller]")]
    public partial class KeycloakController(KeycloakService keycloakService, ILogger<KeycloakController> logger) : ControllerBase
    {

        [HttpGet("realms")]
        public async Task<ActionResult<List<KeycloakRealm>>> GetAllRealms()
        {
            try
            {
                var realms = await keycloakService.GetAllRealmsAsync();
                return Ok(realms);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to retrieve Keycloak realms");
                return StatusCode(500, new { message = "Failed to retrieve realms" });
            }
        }

        [HttpPost("realms")]
        public async Task<IActionResult> CreateRealm([FromBody] KeycloakRealm newRealm, [FromQuery] bool provision)
        {
            if (string.IsNullOrWhiteSpace(newRealm.Realm))
                return BadRequest(new { message = "Realm name cannot be empty" });

            try
            {
                await keycloakService.CreateRealmAsync(newRealm);

                if (provision)
                {
                    await keycloakService.ProvisionRealmAsync(newRealm.Realm);
                }

                return Ok(new { message = $"Realm '{newRealm.Realm}' created successfully" });
            }  
            catch (Exception e)
            {
                logger.LogError(e, "Failed to create realm {RealmName}", newRealm.Realm);
                return StatusCode(500, new { message = $"Failed to create realm '{newRealm.Realm}'" });
            }
        }


        [HttpGet("users")]
        public async Task<ActionResult<List<KeycloakUser>>> GetUsers([FromQuery] string? username = null)
        {
            List<KeycloakUser> users = await keycloakService.GetUsersAsync(username);

            return Ok(users);
        }

        [HttpGet("users/{userId}")]
        public async Task<ActionResult<KeycloakUser>> GetUser(string userId)
        {
            var user = await keycloakService.GetUserByIdAsync(userId);
            return Ok(user);
        }

        [HttpGet("groups")]
        public async Task<ActionResult<List<KeycloakGroup>>> GetGroups()
        {
            var groups = await keycloakService.GetGroupsAsync();
            return Ok(groups);
        }

        [HttpGet("realms/{realmId}/clients")]
        public async Task<ActionResult<List<KeycloakGroup>>> GetClients(String realmId)
        {
            var clients = await keycloakService.GetRealmClientsAsync(realmId);
            return Ok(clients);
        }

        [HttpGet("groups/{groupId}/members")]
        public async Task<ActionResult<List<KeycloakUser>>> GetGroupMembers(string groupId, [FromQuery] string? username = null)
        {
            var members = await keycloakService.GetGroupMembersAsync(groupId, username);
            return Ok(members);
        }

        [HttpGet("users/{userId}/groups")]
        public async Task<ActionResult<List<KeycloakGroup>>> GetUserGroups(string userId)
        {
            var groups = await keycloakService.GetUserGroupsAsync(userId);
            return Ok(groups);
        }

        [HttpPost("users/{userId}/groups/{groupId}")]
        public async Task<IActionResult> AddUserToGroup(string userId, string groupId)
        {
            try
            {
                await keycloakService.AddUserToGroupAsync(userId, groupId);

                return Ok(new { message = "User added to group successfully" });
            }
            catch (Exception e)
            {
                LogFailedToAddUserToGroup(e, userId, groupId);

                throw;
            }
        }

        [HttpDelete("users/{userId}/groups/{groupId}")]
        public async Task<IActionResult> RemoveUserFromGroup(string userId, string groupId)
        {
            await keycloakService.RemoveUserFromGroupAsync(userId, groupId);
            return Ok(new { message = "User removed from group successfully" });
        }

        [HttpGet("groups/{groupId}/children")]
        public async Task<ActionResult<List<KeycloakGroup>>> GetChildGroups(string groupId)
        {
            var groups = await keycloakService.GetChildGroupsAsync(groupId);
            return Ok(groups);
        }

        [HttpPost("groups/all-realms")]
        public async Task<IActionResult> CreateGroupInAllRealms([FromBody] CreateGroupRequest request)
        {
            try
            {
                await keycloakService.CreateGroupInAllRealmsAsync(request.Name);
                return Ok(new { message = "Group created in all realms" });
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to create group {GroupName} in all realms", request.Name);
                return StatusCode(500, new { message = "Failed to create group in all realms" });
            }
        }

        [HttpPost("realms/provision")]
        public async Task<IActionResult> ProvisionRealm([FromBody] CreateRealmRequest request)
        {
            await keycloakService.ProvisionRealmAsync(request.Realm);

            return Ok(new { realm = request.Realm });
        }

        public class CreateRealmRequest
        {
            public string Realm { get; set; } = "";
        }


        [HttpPost("realms/{realm}/clients")]
        public async Task<IActionResult> CreateClient(
        string realm,
        [FromBody] CreateKeycloakClient client)
        {
            if (string.IsNullOrWhiteSpace(client.ClientId))
                return BadRequest("clientId is required");

            await keycloakService.CreateClientAsync(realm, client);

            return Ok();
        }


        #region Logging

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to add user {UserId} to group {GroupId}")]
        private partial void LogFailedToAddUserToGroup(Exception ex, string userId, string groupId);

        #endregion
    }
}