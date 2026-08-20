using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Core.Domain.Settings;
using ArtemisBankingPro.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ArtemisBankingPro.Infrastructure.Identity.Services
{
    public class AccountServiceForApi : BaseAccountService, IAccountServiceForApi
    {
        private readonly JwtSettings _jwtSettings;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ICommerceRepository _commerceRepository;
        private readonly ISavingsAccountRepository _savingsAccountRepository;

        public AccountServiceForApi(UserManager<AppUser> userManager, IEmailService emailService,
            ISavingsAccountService savingsAccountService, ILogger<BaseAccountService> logger,
            IFinancialSummaryService financialSummaryService, IOptions<JwtSettings> jwtSettings, SignInManager<AppUser> signInManager, ICommerceRepository commerceRepository, ISavingsAccountRepository savingsAccountRepository)
            : base(userManager, emailService, savingsAccountService, logger, financialSummaryService)
        {
            _jwtSettings = jwtSettings.Value;
            _signInManager = signInManager;
            _commerceRepository = commerceRepository;
            _savingsAccountRepository = savingsAccountRepository;
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

        public async Task<CommerceUserApiResponseDto> RegisterCommerceUserAsync(RegisterDto dto, int commerceId)
        {
            var commerce = await _commerceRepository.GetByIdAsync(commerceId);
            if (commerce is null)
            {
                throw new ApiException("The specified commerce does not exist.", (int)HttpStatusCode.NotFound);
            }

            var hasAssociatedUser = await _userManager.Users.AnyAsync(u => u.CommerceId == commerceId);
            if (hasAssociatedUser)
            {
                throw new ApiException("This commerce already has an associated user.", (int)HttpStatusCode.Conflict); 
            }

            var result = await RegisterUserAsync(dto, role: "Commerce", isApi: true);

            if (!result.IsSuccess)
            {
                throw new ApiException(result.Error!, (int)HttpStatusCode.Conflict);
            }

            var user = await _userManager.FindByIdAsync(result.Value!.Id)
                ?? throw new ApiException("The commerce user could not be finalized.", (int)HttpStatusCode.InternalServerError);

            user.CommerceId = commerceId;
            await _userManager.UpdateAsync(user);

            return new CommerceUserApiResponseDto
            {
                Id = user.Id,
                UserName = user.UserName!,
                Email = user.Email!,
                Role = "Commerce",
                IsActive = user.IsActive,
                CommerceId = commerceId
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

        public virtual async Task<PagedResult<UserCommerceDto>> GetCommerceUsersAsync(CommerceUserFilterDto filter)
        {
            if (filter.Page <= 0)
            {
                throw new ApiException("The page parameter must be greater than zero.", 400);
            }

            if (filter.PageSize <= 0)
            {
                throw new ApiException("The pageSize parameter must be greater than zero.", 400);
            }

            if (filter.PageSize > 20)
            {
                filter.PageSize = 20;
            }

            var users = (await _userManager.GetUsersInRoleAsync("Commerce")).ToList();

            var totalRecords = users.Count;

            var pagedUsers = users
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToList();

            var items = new List<UserCommerceDto>();

            foreach (var user in pagedUsers)
            {
                var commerce = user.CommerceId is not null
                    ? await _commerceRepository.GetByIdAsync(user.CommerceId.Value)
                    : null;

                items.Add(new UserCommerceDto
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Identification = user.Identification,
                    UserName = user.UserName!,
                    Email = user.Email!,
                    IsActive = user.IsActive,
                    Role = "Commerce",
                    CommerceId = user.CommerceId,
                    CommerceName = commerce?.Name
                });
            }

            return new PagedResult<UserCommerceDto>
            {
                Items = items,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalRecords = totalRecords
            };
        }

        public virtual async Task<UserDetailDto> GetUserDetailByIdAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                throw new ApiException("The selected user does not exist.", (int)HttpStatusCode.NotFound);
            }

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();

            if (role is null)
            {
                throw new ApiException("The selected user does not have a valid role.", (int)HttpStatusCode.Forbidden);
            }

            var userMainAccount = await _savingsAccountRepository.GetPrincipalAccountByClientIdAsync(user.Id);

            if(userMainAccount is null)
            {
                throw new ApiException("The selected user does not have a main account.", (int)HttpStatusCode.Forbidden);
            }

            var mainAccountDto = new SavingsAccountDto
            {
                Id = userMainAccount.Id,
                AccountNumber = userMainAccount.AccountNumber,
                Balance = userMainAccount.Balance,
                ClientFullName = $"{user.FirstName} {user.LastName}",
                Status = userMainAccount.Status,
                Type = userMainAccount.Type
            };

            return new UserDetailDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Identification = user.Identification,
                UserName = user.UserName!,
                Email = user.Email!,
                IsActive = user.IsActive,
                Role = role,
                CreatedAt = user.CreatedAt,
                MainAccount = mainAccountDto
            };
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
