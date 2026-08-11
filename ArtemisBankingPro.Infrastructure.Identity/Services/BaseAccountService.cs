using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text;
using ArtemisBankingPro.Core.Application;
using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtemisBankingPro.Infrastructure.Identity.Services
{
    public abstract class BaseAccountService : IBaseAccountService
    {
        protected readonly UserManager<AppUser> _userManager;
        protected readonly IEmailService _emailService;
        protected readonly ISavingsAccountService _savingsAccountService;
        protected readonly ILogger<BaseAccountService> _logger;

        public BaseAccountService(UserManager<AppUser> userManager, IEmailService emailService,
            ISavingsAccountService savingsAccountService, ILogger<BaseAccountService> logger)
        {
            _userManager = userManager;
            _emailService = emailService;
            _savingsAccountService = savingsAccountService;
            _logger = logger;
        }

        public virtual async Task<Result<PagedResult<UserDto>>> GetAllUsersAsync(int page, int pageSize, string? role)
        {
            if (page <= 0)
            {
                return Result<PagedResult<UserDto>>.Failure(error: "The page parameter must be greater than zero.");
            }

            if (pageSize <= 0)
            {
                return Result<PagedResult<UserDto>>.Failure(error: "The pageSize parameter must be greater than zero.");
            }

            if (pageSize > 20)
            {
                pageSize = 20;
            }

            var validRoles = new[] { "Admin", "Cashier", "Client" };
            if (role is not null && !validRoles.Contains(role))
            {
                return Result<PagedResult<UserDto>>.Failure(error: "The role parameter must be Admin, Cashier or Client.");
            }

            var users = new List<AppUser>();

            if (role is not null)
            {
                users = (await _userManager.GetUsersInRoleAsync(role)).ToList();
            }
            else
            {
                foreach (var r in validRoles)
                {
                    var usersInRole = await _userManager.GetUsersInRoleAsync(r);
                    users.AddRange(usersInRole);
                }
            }

            var totalRecords = users.Count;

            var pagedUsers = users
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

            var items = new List<UserDto>();

            foreach (var user in pagedUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);

                items.Add(new UserDto
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Identification = user.Identification,
                    UserName = user.UserName!,
                    Email = user.Email!,
                    IsActive = user.IsActive,
                    Role = roles.First()
                });
            }

            return Result<PagedResult<UserDto>>.Success(new PagedResult<UserDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            });
        }

        public virtual async Task<Result<UserDto>> GetUserByIdAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return Result<UserDto>.Failure(error: "The selected user does not exist.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();

            if (role is null)
            {
                return Result<UserDto>.Failure(error: "The selected user does not exist.");
            }

            return Result<UserDto>.Success(new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Identification = user.Identification,
                UserName = user.UserName!,
                Email = user.Email!,
                IsActive = user.IsActive,
                Role = role,
                CreatedAt = user.CreatedAt
            });
        }

        public virtual async Task<Result<RegisterResponseDto>> RegisterUserAsync(RegisterDto registerDto, string role, string? origin = null, bool isApi = false)
        {
            var basicValidationsResult = ValidateUserFields(registerDto.UserName, registerDto.FirstName, registerDto.LastName);
            if (!basicValidationsResult.IsSuccess)
            {
                return Result<RegisterResponseDto>.Failure(error: basicValidationsResult.Error!);
            }

            if (string.IsNullOrWhiteSpace(registerDto.Email) || !new EmailAddressAttribute().IsValid(registerDto.Email))
            {
                return Result<RegisterResponseDto>.Failure(error: "You must enter a valid email address.");
            }

            var passwordVerificationResult = ValidatePassword(registerDto.Password);
            if (!passwordVerificationResult.IsSuccess)
            {
                return Result<RegisterResponseDto>.Failure(error: passwordVerificationResult.Error!);
            }

            // Business logic validations

            if (await _userManager.FindByEmailAsync(registerDto.Email) is not null)
            {
                _logger.LogWarning("User registration rejected: email {Email} is already registered.", registerDto.Email);
                return Result<RegisterResponseDto>.Failure(error: "A user with this email address is already registered.");
            }

            if (await _userManager.FindByNameAsync(registerDto.UserName) is not null)
            {
                _logger.LogWarning("User registration rejected: username {UserName} is already registered.", registerDto.UserName);
                return Result<RegisterResponseDto>.Failure(error: "A user with this username is already registered.");
            }

            if (await _userManager.Users.AnyAsync(u => u.Identification == registerDto.Identification))
            {
                _logger.LogWarning("User registration rejected: identification {Identification} is already registered.", registerDto.Identification);
                return Result<RegisterResponseDto>.Failure(error: "A user with this identification is already registered.");
            }

            var user = new AppUser
            {
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                Identification = registerDto.Identification,
                UserName = registerDto.UserName,
                Email = registerDto.Email,
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
            };

            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (!result.Succeeded)
            {
                return Result<RegisterResponseDto>.Failure(error: string.Join("\n", result.Errors.Select(e => e.Description)));
            }

            await _userManager.AddToRoleAsync(user, role);

            if (role == "Client" || role == "Commerce")
            {
                var initialAmount = registerDto.InitialAmount ?? 0m;
                var accountResult = await _savingsAccountService.CreatePrincipalAccountAsync(user.Id, registerDto.InitialAmount ?? 0m);

                if (!accountResult.IsSuccess)
                {
                    await _userManager.DeleteAsync(user);
                    _logger.LogWarning("User {UserId} rolled back after principal account creation failed: {Error}.", user.Id, accountResult.Error);
                    return Result<RegisterResponseDto>.Failure(error: accountResult.Error!);
                }

                if (initialAmount > 0)
                {
                    _logger.LogInformation("Principal savings account opened for {Role} {UserId} with an initial credit of {InitialAmount:C}.", role, user.Id, initialAmount);
                }
            }

            _logger.LogInformation("User {UserId} created successfully with role {Role}.", user.Id, role);

            await SendActivationEmailAsync(user, origin, isApi);

            var responseDto = new RegisterResponseDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserName = user.UserName,
                Email = user.Email,
                Identification = user.Identification,
                IsActive = user.IsActive,
                Role = role
            };

            return Result<RegisterResponseDto>.Success(responseDto, message: $"The {role} has been created successfully.");
        }

        public virtual async Task<Result> EditUserAsync(UpdateUserDto updateDto, string currentUserId)
        {
            if (updateDto.Id == currentUserId)
            {
                return Result.Failure(error: "You cannot edit your own account from this module.");
            }

            var user = await _userManager.FindByIdAsync(updateDto.Id);
            if (user is null)
            {
                return Result.Failure(error: "The selected user does not exist.");
            }

            if (string.IsNullOrWhiteSpace(updateDto.FirstName))
            {
                return Result.Failure(error: "The first name is required.");
            }

            if (string.IsNullOrWhiteSpace(updateDto.LastName))
            {
                return Result.Failure(error: "The last name is required.");
            }

            if (string.IsNullOrWhiteSpace(updateDto.Identification))
            {
                return Result.Failure(error: "The identification is required.");
            }

            if (await _userManager.Users.AnyAsync(u => u.Identification == updateDto.Identification && u.Id != updateDto.Id))
            {
                _logger.LogWarning("User update rejected for {UserId}: identification {Identification} already belongs to another user.", updateDto.Id, updateDto.Identification);
                return Result.Failure(error: "There is already another registered user with this identification.");
            }

            if (string.IsNullOrWhiteSpace(updateDto.Email))
            {
                return Result.Failure(error: "The email is required.");
            }

            if (!new EmailAddressAttribute().IsValid(updateDto.Email))
            {
                return Result.Failure(error: "The email must have a valid format.");
            }

            var existingByEmail = await _userManager.FindByEmailAsync(updateDto.Email);
            if (existingByEmail is not null && existingByEmail.Id != updateDto.Id)
            {
                _logger.LogWarning("User update rejected for {UserId}: email {Email} already belongs to another user.", updateDto.Id, updateDto.Email);
                return Result.Failure(error: "There is already another registered user with this email.");
            }

            if (string.IsNullOrWhiteSpace(updateDto.UserName))
            {
                return Result.Failure(error: "The username is required.");
            }

            var existingByUserName = await _userManager.FindByNameAsync(updateDto.UserName);
            if (existingByUserName is not null && existingByUserName.Id != updateDto.Id)
            {
                _logger.LogWarning("User update rejected for {UserId}: username {UserName} already belongs to another user.", updateDto.Id, updateDto.UserName);
                return Result.Failure(error: "There is already another registered user with this username.");
            }

            if (!string.IsNullOrWhiteSpace(updateDto.Password) || !string.IsNullOrWhiteSpace(updateDto.ConfirmPassword))
            {
                if (string.IsNullOrWhiteSpace(updateDto.ConfirmPassword))
                {
                    return Result.Failure(error: "You must confirm the new password.");
                }

                if (updateDto.Password != updateDto.ConfirmPassword)
                {
                    return Result.Failure(error: "The password and the password confirmation must match.");
                }

                var passwordVerificationResult = ValidatePassword(updateDto.Password!);
                if (!passwordVerificationResult.IsSuccess)
                {
                    return Result.Failure(error: passwordVerificationResult.Error!);
                }
            }

            var roles = await _userManager.GetRolesAsync(user);
            var isClient = roles.Contains("Client");
            var isCommerce = roles.Contains("Commerce");

            if ((isClient || isCommerce) && updateDto.AdditionalAmount is < 0)
            {
                return Result.Failure(error: "The additional amount cannot be negative.");
            }

            user.FirstName = updateDto.FirstName;
            user.LastName = updateDto.LastName;
            user.Identification = updateDto.Identification;
            user.Email = updateDto.Email;
            user.UserName = updateDto.UserName;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return Result.Failure(error: string.Join("\n", updateResult.Errors.Select(e => e.Description)));
            }

            if (!string.IsNullOrWhiteSpace(updateDto.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, token, updateDto.Password);
                if (!resetResult.Succeeded)
                {
                    return Result.Failure(error: string.Join("\n", resetResult.Errors.Select(e => e.Description)));
                }
            }

            if ((isClient || isCommerce) && updateDto.AdditionalAmount is > 0)
            {
                var creditResult = await _savingsAccountService.CreditAdditionalAmountAsync(user.Id, updateDto.AdditionalAmount!.Value, currentUserId);

                if (!creditResult.IsSuccess)
                {
                    _logger.LogWarning("Additional amount credit failed for user {UserId}: {Error}.", user.Id, creditResult.Error);
                    return Result.Failure(error: creditResult.Error!);
                }

                _logger.LogInformation("Additional amount of {AdditionalAmount:C} credited to user {UserId} by administrator {CurrentUserId}.",
                        updateDto.AdditionalAmount.Value, user.Id, currentUserId);
            }

            _logger.LogInformation("User {UserId} updated successfully by administrator {CurrentUserId}.", user.Id, currentUserId);

            return Result.Success(message: "The user has been updated successfully.");
        }

        public virtual async Task<Result> ChangeUserStatusAsync(string userId, bool isActive, string currentUserId)
        {
            if (userId == currentUserId)
            {
                _logger.LogWarning("Administrator {CurrentUserId} attempted to change their own account status.", currentUserId);
                return Result.Failure(error: "You cannot change the status of your own account.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return Result.Failure(error: "The selected user does not exist.");
            }

            user.IsActive = isActive;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return Result.Failure(error: string.Join("\n", updateResult.Errors.Select(e => e.Description)));
            }

            _logger.LogInformation("User {UserId} was {Status} by administrator {CurrentUserId}.", userId, isActive ? "activated" : "deactivated", currentUserId);

            return Result.Success(message: isActive
                ? "The user has been activated successfully."
                : "The user has been deactivated successfully.");
        }

        public virtual async Task<Result> RequestPasswordResetAsync(RequestPasswordResetDto request, string? origin, bool isApi = false)
        {
            if (string.IsNullOrWhiteSpace(request.UserName))
            {
                return Result.Failure(error: "You must enter a username.");
            }

            var user = await _userManager.FindByNameAsync(request.UserName);

            if (user is null)
            {
                return Result.Failure(error: "There is no registered user with this username.");
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return Result.Failure(error: "This user does not have a registered email address. The reset request cannot be sent.");
            }

            var allowedRoles = GetAllowedRolesForResetFlow(isApi);
            var userRoles = await _userManager.GetRolesAsync(user);

            if (!userRoles.Any(r => allowedRoles.Contains(r)))
            {
                return Result.Failure(error: "This user does not have a role permitted for this reset flow.");
            }

            user.IsActive = false;
            user.PasswordResetTokenGeneratedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            await SendPasswordResetEmailAsync(user, token, origin, isApi);

            return Result.Success(message: isApi
                ? "A password reset token has been generated and sent to the registered email."
                : "A password reset link has been sent to the registered email.");
        }

        public virtual async Task<Result> ResetPasswordAsync(ResetPasswordDto request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return Result.Failure(error: "You must enter the user id.");
            }

            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return Result.Failure(error: "You must enter the token.");
            }

            if (request.Password != request.ConfirmPassword)
            {
                return Result.Failure(error: "The password and confirmation password do not match.");
            }

            var passwordVerificationResult = ValidatePassword(request.Password);
            if (!passwordVerificationResult.IsSuccess)
            {
                return Result.Failure(error: passwordVerificationResult.Error!);
            }

            var user = await _userManager.FindByIdAsync(request.UserId);

            if (user is null)
            {
                return Result.Failure(error: "No account was found with the information provided.");
            }

            var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
            var result = await _userManager.ResetPasswordAsync(user, token, request.Password);

            if (!result.Succeeded)
            {
                return Result.Failure(error: "The reset link is not valid or has already been used.");
            }

            user.IsActive = true;
            await _userManager.UpdateAsync(user);

            await _userManager.ResetAccessFailedCountAsync(user);
            await _userManager.SetLockoutEndDateAsync(user, null);

            return Result.Success(message: "Your password has been successfully reset. You can now log in.");
        }

        #region Private Methods
        private Result ValidateUserFields(string userName, string firstName, string lastName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return Result<RegisterResponseDto>.Failure(error: "You must enter a username.");
            }

            if (string.IsNullOrWhiteSpace(firstName))
            {
                return Result.Failure(error: "You must enter your first name.");
            }

            if (string.IsNullOrWhiteSpace(lastName))
            {
                return Result.Failure(error: "You must enter your last name.");
            }

            return Result.Success();
        }

        private Result ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return Result.Failure(error: "You must enter a password.");
            }

            if (password.Length < 8)
            {
                return Result.Failure(error: "The password must be at least 8 characters long.");
            }

            if (!password.Any(char.IsUpper))
            {
                return Result.Failure(error: "The password must contain at least one uppercase letter.");
            }

            if (!password.Any(char.IsLower))
            {
                return Result.Failure(error: "The password must contain at least one lowercase letter.");
            }

            if (!password.Any(char.IsDigit))
            {
                return Result.Failure(error: "The password must contain at least one digit.");
            }

            if (!password.Any(c => !char.IsLetterOrDigit(c)))
            {
                return Result.Failure(error: "The password must contain at least one special character.");
            }

            return Result.Success();
        }

        private async Task SendActivationEmailAsync(AppUser user, string? origin, bool isApi)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            if (isApi)
            {
                await _emailService.SendAsync(new EmailRequestDto
                {
                    To = user.Email!,
                    Subject = "Account Activation Token",
                    BodyHtml = $"Your Artemis Banking account has been created successfully. " +
                               $"Use the following token to activate your account through the corresponding endpoint: {token}"
                });
                return;
            }

            var activationUri = BuildActivationUri(user, token, origin ?? string.Empty);
            await _emailService.SendAsync(new EmailRequestDto
            {
                To = user.Email!,
                Subject = "Account Activation",
                BodyHtml = $"""
                    <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{user.FirstName} {user.LastName}</span></h3>
                    <p>Your Artemis Banking account has been created successfully.</p>
                    <p>To activate your account, click the following button:</p>
                    <p style="margin: 30px 0;">
                        <a href="{activationUri}" style="display: inline-block;padding: 12px 24px;background-color: #0D6EFD;color: #ffffff;text-decoration: none;border-radius: 6px;">
                            Activate Account
                        </a>
                    </p>
                """
            });
        }

        private string BuildActivationUri(AppUser user, string token, string origin)
        {
            var completeUrl = new Uri(string.Concat(origin, "/Auth/ConfirmEmail"));
            var uri = QueryHelpers.AddQueryString(completeUrl.ToString(), "userId", user.Id);
            uri = QueryHelpers.AddQueryString(uri, "token", token);
            return uri;
        }

        public virtual async Task<Result> ConfirmAccountAsync(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user is null)
            {
                return Result.Failure(error: "There is no account registered with this user.");
            }

            if (user.EmailConfirmed)
            {
                return Result.Failure(error: "The activation link has already been used.");
            }

            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            }
            catch (Exception)
            {
                return Result.Failure(error: "An unexpected error occurred.");
            }

            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
            if (!result.Succeeded)
            {
                return Result.Failure(error: "The activation link is not valid or has already been used.");
            }

            user.IsActive = true;
            await _userManager.UpdateAsync(user);

            return Result.Success(message: "Your account has been successfully activated. You can now log in.");
        }

        private IEnumerable<string> GetAllowedRolesForResetFlow(bool isApi) =>
                isApi ? ["Admin", "Commerce"] : [ "Admin", "Cashier", "Client" ];

        private async Task SendPasswordResetEmailAsync(AppUser user, string token, string? origin, bool isApi)
        {
            if (isApi)
            {
                await _emailService.SendAsync(new EmailRequestDto
                {
                    To = user.Email!,
                    Subject = "Password reset token",
                    BodyHtml = $"A token has been generated to reset your account's password.<br/>" +
                               $"Reset token: {token}<br/>" +
                               $"Use this token in the corresponding endpoint to complete the password change."
                });
                return;
            }

            var resetUri = BuildResetPasswordUri(user, token, origin ?? string.Empty);
            await _emailService.SendAsync(new EmailRequestDto
            {
                To = user.Email!,
                Subject = "Password reset request",
                BodyHtml = $"""
                    <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{user.FirstName} {user.LastName}</span></h3>
                    <p>We have received a request to reset your account password.</p>
                    <p>To continue, click the following button:</p>
                    <p style="margin: 30px 0;">
                        <a href="{resetUri}" style="display: inline-block;padding: 12px 24px;background-color: #0D6EFD;color: #ffffff;text-decoration: none;border-radius: 6px;">
                            Reset Password
                        </a>
                    </p>
                    <p>This link will be valid for 30 minutes.</p>
                    <p style="font-size: 14px; color: #6c757d;">If you did not request this change, please ignore this message.</p>
                """
            });
        }

        private string BuildResetPasswordUri(AppUser user, string token, string origin)
        {
            var completeUrl = new Uri(string.Concat(origin, "/Auth/ResetPassword"));
            var uri = QueryHelpers.AddQueryString(completeUrl.ToString(), "userId", user.Id);
            uri = QueryHelpers.AddQueryString(uri, "token", token);
            return uri;
        }
        #endregion
    }
}
