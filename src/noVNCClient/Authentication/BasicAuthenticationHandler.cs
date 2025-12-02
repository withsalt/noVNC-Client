using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace noVNCClient.Authentication
{
    public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IConfiguration _configuration;

        public BasicAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfiguration configuration)
            : base(options, logger, encoder)
        {
            _configuration = configuration;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var enabled = _configuration.GetValue<bool>("BasicAuth:Enabled", true);
            if (!enabled)
            {
                // 如果 BasicAuth 被禁用，直接返回成功，模拟一个已认证的用户
                var claims = new[] {
                    new Claim(ClaimTypes.NameIdentifier, "admin"),
                    new Claim(ClaimTypes.Name, "admin"),
                };
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return AuthenticateResult.Success(ticket);
            }

            if (!Request.Headers.ContainsKey("Authorization"))
                return AuthenticateResult.Fail("Missing Authorization Header");

            string username;
            string password;

            try
            {
                string? authorization = Request.Headers["Authorization"];
                if (string.IsNullOrWhiteSpace(authorization))
                {
                    return AuthenticateResult.Fail("Invalid Authorization Header");
                }
                var authHeader = AuthenticationHeaderValue.Parse(authorization);
                string? parameter = authHeader.Parameter;
                if (string.IsNullOrWhiteSpace(parameter))
                {
                    return AuthenticateResult.Fail("Invalid Authorization parameter");
                }
                var credentialBytes = Convert.FromBase64String(parameter);
                var credentials = Encoding.UTF8.GetString(credentialBytes).Split(new[] { ':' }, 2);
                username = credentials[0];
                password = credentials[1];
            }
            catch
            {
                return AuthenticateResult.Fail("Invalid Authorization Header");
            }

            var configUsername = _configuration["BasicAuth:Username"];
            var configPassword = _configuration["BasicAuth:Password"];

            if (username != configUsername || password != configPassword)
            {
                return AuthenticateResult.Fail("Invalid Username or Password");
            }

            var authenticatedClaims = new[] {
                new Claim(ClaimTypes.NameIdentifier, username),
                new Claim(ClaimTypes.Name, username),
            };
            var authenticatedIdentity = new ClaimsIdentity(authenticatedClaims, Scheme.Name);
            var authenticatedPrincipal = new ClaimsPrincipal(authenticatedIdentity);
            var authenticatedTicket = new AuthenticationTicket(authenticatedPrincipal, Scheme.Name);

            return AuthenticateResult.Success(authenticatedTicket);
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            var enabled = _configuration.GetValue<bool>("BasicAuth:Enabled", true);
            if (!enabled)
            {
                // 如果禁用，不应该触发 Challenge，但以防万一
                return;
            }

            Response.Headers["WWW-Authenticate"] = "Basic realm=\"noVNCClient\", charset=\"UTF-8\"";
            await base.HandleChallengeAsync(properties);
        }
    }
}
