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
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IFinancialSummaryService _financialSummaryService;

        public AccountServiceForWebApp(UserManager<AppUser> userManager, IEmailService emailService,
            ISavingsAccountService savingsAccountService, SignInManager<AppUser> signInManager,
            ILogger<BaseAccountService> logger, IBasicUserInfoService basicUserInfoService, 
            IFinancialSummaryService financialSummaryService)
            : base(userManager, emailService, savingsAccountService, logger)
        {
            _signInManager = signInManager;
            _basicUserInfoService = basicUserInfoService;
            _financialSummaryService = financialSummaryService;
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

        public async Task<Result<List<ClientForAssignmentDto>>> GetClientsForAssignmentAsync(string? identification)
        {
            var clients = await _basicUserInfoService.GetActiveClientsAsync(identification);

            var items = new List<ClientForAssignmentDto>();
            foreach (var client in clients)
            {
                var totalDebt = await _financialSummaryService.GetTotalDebtByClientAsync(client.Id);

                items.Add(new ClientForAssignmentDto
                {
                    Id = client.Id,
                    Identification = client.Identification,
                    FullName = client.FullName,
                    Email = client.Email,
                    TotalDebt = totalDebt
                });
            }

            return Result<List<ClientForAssignmentDto>>.Success(items);
        }
    }
}
