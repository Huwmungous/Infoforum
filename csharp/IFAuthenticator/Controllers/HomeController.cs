﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IFAuthenticator.Controllers
{
    [ApiController]
    public class HomeController : ControllerBase
    {
        private ILogger<AuthClass> _logger;
        private readonly AuthClass _authClass;
        private LdapSettings _ldapSettings;

        public HomeController(ILogger<AuthClass> logger, IOptions<LdapSettings> ldapSettings)
        {
            _logger = logger;

            _authClass = new AuthClass(logger, ldapSettings);

            _ldapSettings = ldapSettings.Value;
        }

        [HttpPost]
        [Route("Authenticate")]
        public async Task<string> Authenticate([FromBody] UserPass userPass)
        {
            var (isAuthenticated, token) = await _authClass.AuthenticateUserAsync(userPass.User, userPass.Pass);

            return isAuthenticated ? token : "";
        }


        [HttpPost]
        [Route("Authorise")]
        public async Task<bool> Authorise([FromBody] TokenClaim tokenClaim)
        {
            return await _authClass.AuthoriseAsync(tokenClaim.Token, tokenClaim.Claim);
        }
    }
}