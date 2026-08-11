using ArtemisBankingPro.Core.Application;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace ArtemisBankingPro.Infrastructure.Identity.Services
{
    public class AccountServiceForWebApp : BaseAccountService, IAccountServiceForWebApp
    {
        private readonly SignInManager<AppUser> _signInManager;

        public AccountServiceForWebApp(UserManager<AppUser> userManager, IEmailService emailService,
            ISavingsAccountService savingsAccountService, SignInManager<AppUser> signInManager,
            ILogger<BaseAccountService> logger) 
            : base(userManager, emailService, savingsAccountService, logger)
        {
            _signInManager = signInManager;
        }

        public async Task<Result<LoginResponseDto>> AuthenticateAsync(LoginDto loginDto)
        {
            var user = await _userManager.FindByNameAsync(loginDto.UserName);

            if (user is null)
            {
                return Result<LoginResponseDto>.Failure(error: "The login credentials are invalid.");
            }

            var result = await _signInManager.PasswordSignInAsync(user, loginDto.Password, isPersistent: false, lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                {
                    return Result<LoginResponseDto>.Failure(error: "Your account has been locked due to multiple failed attempts. " +
                        "Please try again in 10 minutes.");
                }

                return Result<LoginResponseDto>.Failure(error: "The login credentials are invalid.");
            }

            if (!user.IsActive)
            {
                await _signInManager.SignOutAsync();
                return Result<LoginResponseDto>.Failure(error: "Your account is inactive. You must activate your account using the link " +
                    "sent to your registered email address in order to access the system.");
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            if (!userRoles.Any(r => r == "Admin" || r == "Cashier" || r == "Client"))
            {
                await _signInManager.SignOutAsync();
                return Result<LoginResponseDto>.Failure(error: "This user does not have permission to access the web application.");
            }

            var responseDto = new LoginResponseDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserName = user.UserName!,
                Identification = user.Identification!,
                Email = user.Email!,
                IsActive = user.IsActive,
                Role = userRoles.FirstOrDefault()!
            };

            return Result<LoginResponseDto>.Success(responseDto, message: "Login successful.");
        }

        public async Task SignOutAsync()
        {
            await _signInManager.SignOutAsync();
        }
    }
}
