using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.SavingsAccount.Queries.GetAll
{
    /// <summary>
    /// Filter parameters for retrieving the paginated list of savings accounts.
    /// </summary>
    public class GetAllSavingsAccountsQuery : IRequest<PagedResult<SavingsAccountResponseDto>>
    {
        /// <example>1</example>
        [SwaggerParameter(Description = "Page number to retrieve.")]
        public int Page { get; set; } = 1;

        /// <example>20</example>
        [SwaggerParameter(Description = "Number of records per page. Maximum allowed is 20.")]
        public int PageSize { get; set; } = 20;

        [SwaggerParameter(Description = "Optional client identification to search accounts for a specific client.")]
        public string? Identification { get; set; }

        /// <example>Active</example>
        [SwaggerParameter(Description = "Optional account status filter.")]
        public SavingsAccountStatus? Status { get; set; } = SavingsAccountStatus.Active;

        /// <example>Principal</example>
        [SwaggerParameter(Description = "Optional account type filter.")]
        public SavingsAccountType? Type { get; set; }
    }

    public class GetAllSavingsAccountsQueryHandler : IRequestHandler<GetAllSavingsAccountsQuery, PagedResult<SavingsAccountResponseDto>>
    {
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;

        public GetAllSavingsAccountsQueryHandler(ISavingsAccountRepository savingsAccountRepository, IBasicUserInfoService basicUserInfoService)
        {
            _savingsAccountRepository = savingsAccountRepository;
            _basicUserInfoService = basicUserInfoService;
        }

        public async Task<PagedResult<SavingsAccountResponseDto>> Handle(GetAllSavingsAccountsQuery request, CancellationToken cancellationToken)
        {
            if (request.PageSize > 20)
            {
                request.PageSize = 20;
            }

            string? clientId = null;

            if (!string.IsNullOrWhiteSpace(request.Identification))
            {
                clientId = await _basicUserInfoService.GetUserIdByIdentificationAsync(request.Identification);

                if (clientId is null)
                {
                    return new PagedResult<SavingsAccountResponseDto>
                    {
                        Page = request.Page,
                        PageSize = request.PageSize,
                        TotalRecords = 0,
                        Items = []
                    };
                }
            }

            var query = _savingsAccountRepository.GetAllQuery();

            if (request.Status is not null)
            {
                query = query.Where(a => a.Status == request.Status);
            }

            if (request.Type is not null)
            {
                query = query.Where(a => a.Type == request.Type);
            }

            if (clientId is not null)
            {
                query = query.Where(a => a.ClientId == clientId);
            }

            var totalRecords = await query.CountAsync(cancellationToken);

            var accounts = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var items = new List<SavingsAccountResponseDto>();

            foreach (var account in accounts)
            {
                var clientInfo = await _basicUserInfoService.GetBasicInfoAsync(account.ClientId);

                items.Add(new SavingsAccountResponseDto
                {
                    Id = account.Id,
                    AccountNumber = account.AccountNumber,
                    ClientId = account.ClientId,
                    ClientFullName = clientInfo!.FullName,
                    Identification = clientInfo.Identification,
                    Balance = account.Balance,
                    Type = account.Type,
                    Status = account.Status,
                    CreatedAt = account.CreatedAt
                });
            }

            return new PagedResult<SavingsAccountResponseDto>
            {
                Page = request.Page,
                PageSize = request.PageSize,
                TotalRecords = totalRecords,
                Items = items
            };
        }
    }
}
