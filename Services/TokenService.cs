using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Zhuanzhu.WebApi.Options;

namespace Zhuanzhu.WebApi.Services
{
    public class TokenService
    {
        /// <summary>业务用户 Id 的 claim 名称，前端与服务端约定一致。</summary>
        public const string UserIdClaim = "uid";

        private readonly JwtOptions _options;

        public TokenService(JwtOptions options)
        {
            _options = options;
        }

        public (string Token, int ExpiresIn) Issue(long userId)
        {
            var now = DateTime.UtcNow;
            var days = _options.ExpireDays > 0 ? _options.ExpireDays : 7;
            var expires = now.AddDays(days);

            var claims = new List<Claim>
            {
                new Claim(UserIdClaim, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                NotBefore = now,
                IssuedAt = now,
                Expires = expires,
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };

            var handler = new JwtSecurityTokenHandler();
            var token = handler.WriteToken(handler.CreateToken(descriptor));
            return (token, (int)(expires - now).TotalSeconds);
        }
    }
}
