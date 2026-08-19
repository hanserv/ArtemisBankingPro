using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using ArtemisBankingPro.Core.Application;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Settings;
using ArtemisBankingPro.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ArtemisBankingPro.Infrastructure.Identity.Services
{
    public class AccountServiceForApi : BaseAccountService, IAccountServiceForApi
    {
        private readonly JwtSettings _jwtSettings;
        private readonly SignInManager<AppUser> _signInManager;

        public AccountServiceForApi(UserManager<AppUser> userManager, IEmailService emailService,
            ISavingsAccountService savingsAccountService, ILogger<BaseAccountService> logger,
            IFinancialSummaryService financialSummaryService, IOptions<JwtSettings> jwtSettings, SignInManager<AppUser> signInManager)
            : base(userManager, emailService, savingsAccountService, logger, financialSummaryService)
        {
            _jwtSettings = jwtSettings.Value;
            _signInManager = signInManager;
        }

        public async Task<LoginResponseForApiDto> AuthenticateAsync(LoginDto loginDto)
        {
            var user = await _userManager.FindByNameAsync(loginDto.UserName);
            if (user is null)
            {
                throw new ApiException("The login credentials are invalid.", (int)HttpStatusCode.Unauthorized);
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                {
                    throw new ApiException("Your account has been locked due to multiple failed attempts. Please try again in 10 minutes.",
                        (int)HttpStatusCode.Unauthorized);
                }

                throw new ApiException("The login credentials are invalid.", (int)HttpStatusCode.Unauthorized);
            }

            if (!user.IsActive)
            {
                throw new ApiException("The account is inactive. You must activate it before logging in.", (int)HttpStatusCode.Unauthorized);
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Any(r => r == "Admin" || r == "Commerce"))
            {
                throw new ApiException("Access denied. You do not have permission to use this resource.", (int)HttpStatusCode.Forbidden);
            }

            var jwtSecurityToken = await GenerateJwtToken(user);

            return new LoginResponseForApiDto
            {
                Jwt = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken)
            };
        }

        public async Task ConfirmAccountApiAsync(ConfirmAccountDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Token))
            {
                throw new ApiException("The confirmation token is invalid.", (int)HttpStatusCode.BadRequest);
            }

            string decodedString;

            try
            {
                decodedString = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(dto.Token));
            }
            catch (FormatException)
            {
                throw new ApiException("The confirmation token is invalid.", (int)HttpStatusCode.BadRequest);
            }

            var parts = decodedString.Split('|', 2);

            if (parts.Length != 2)
            {
                throw new ApiException("The confirmation token is invalid.", (int)HttpStatusCode.BadRequest);
            }

            var userId = parts[0];
            var rawToken = parts[1];

            var user = await _userManager.FindByIdAsync(userId);

            if (user is null)
            {
                throw new ApiException("The confirmation token is invalid.", (int)HttpStatusCode.BadRequest);
            }

            if (user.EmailConfirmed)
            {
                throw new ApiException("The activation token has already been used.", (int)HttpStatusCode.BadRequest);
            }

            var result = await _userManager.ConfirmEmailAsync(user, rawToken);

            if (!result.Succeeded)
            {
                throw new ApiException("The confirmation token is invalid or has expired.", (int)HttpStatusCode.BadRequest);
            }

            user.IsActive = true;
            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                throw new ApiException("The account could not be activated.", (int)HttpStatusCode.InternalServerError);
            }
        }

        #region "private methods"
        private async Task<JwtSecurityToken> GenerateJwtToken(AppUser user)
        {
            var userClaims = await _userManager.GetClaimsAsync(user);
            var roles = await _userManager.GetRolesAsync(user);

            var rolesClaims = new List<Claim>();
            foreach (var role in roles)
            {
                rolesClaims.Add(new Claim(ClaimTypes.Role, role));
            }
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,user.UserName ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim("uid",user.Id ?? "")
            }.Union(userClaims).Union(rolesClaims);

            var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var signingCredentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);

            var jwtSecurityToken = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
                signingCredentials: signingCredentials
            );

            return jwtSecurityToken;
        }
        #endregion
    }
}
