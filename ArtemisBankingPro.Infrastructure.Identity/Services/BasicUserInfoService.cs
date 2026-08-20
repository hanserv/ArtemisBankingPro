using ArtemisBankingPro.Core.Application.DTOs.Commerce;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Infrastructure.Identity.Services
{
    public class BasicUserInfoService : IBasicUserInfoService
    {
        private readonly UserManager<AppUser> _userManager;

        public BasicUserInfoService(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<string?> GetUserIdByIdentificationAsync(string identification)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Identification == identification);
            return user?.Id;
        }

        public async Task<string> GetFullNameAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user is null ? "Unknown user" : $"{user.FirstName} {user.LastName}";
        }

        public async Task<List<UserBasicInfoDto>> GetActiveClientsAsync(string? identification)
        {
            var clients = await _userManager.GetUsersInRoleAsync("Client");
            var activeClients = clients.Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(identification))
            {
                activeClients = activeClients.Where(c => c.Identification.Contains(identification));
            }

            return activeClients
                .Select(c => new UserBasicInfoDto
                {
                    Id = c.Id,
                    Identification = c.Identification,
                    FullName = $"{c.FirstName} {c.LastName}",
                    Email = c.Email!
                })
                .ToList();
        }

        public async Task<bool?> IsClientActiveAsync(string clientId)
        {
            var user = await _userManager.FindByIdAsync(clientId);
            if (user is null)
            {
                return null;
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("Client"))
            {
                return null;
            }

            return user.IsActive;
        }

        public async Task<UserBasicInfoDto?> GetBasicInfoAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user is null)
            {
                return null;
            }

            return new UserBasicInfoDto
            {
                Id = user.Id,
                Identification = user.Identification,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email!
            };
        }

        public async Task<(int Active, int Inactive)> GetClientStatusCountsAsync()
        {
            var clients = await _userManager.GetUsersInRoleAsync("Client");
            return (clients.Count(c => c.IsActive), clients.Count(c => !c.IsActive));
        }

        public async Task<HashSet<int>> GetCommerceIdsWithAssociatedUserAsync(IEnumerable<int> commerceIds)
        {
            var ids = commerceIds.ToList();

            return (await _userManager.Users
                .Where(u => u.CommerceId != null && ids.Contains(u.CommerceId.Value))
                .Select(u => u.CommerceId!.Value)
                .ToListAsync())
                .ToHashSet();
        }

        public async Task<CommerceAssociatedUserDto?> GetCommerceAssociatedUserInfoAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            
            if (user is null)
            {
                return null;
            }

            return new CommerceAssociatedUserDto
            {
                Id = user.Id,
                UserName = user.UserName!,
                Email = user.Email!,
                IsActive = user.IsActive
            };
        }
    }
}
