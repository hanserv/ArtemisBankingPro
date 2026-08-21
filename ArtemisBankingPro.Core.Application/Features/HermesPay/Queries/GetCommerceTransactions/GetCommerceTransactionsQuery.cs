using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.HermesPay;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.HermesPay.Queries.GetCommerceTransactions
{
    /// <summary>
    /// Parameters required to retrieve the paginated list of transactions received by a commerce.
    /// </summary>
    public class GetCommerceTransactionsQuery : IRequest<CommerceTransactionsResponseDto>
    {
        public int CommerceId { get; set; }

        /// <example>1</example>
        [SwaggerParameter(Description = "Page number to retrieve.")]
        public int Page { get; set; } = 1;

        /// <example>20</example>
        [SwaggerParameter(Description = "Number of records per page. Maximum allowed is 20.")]
        public int PageSize { get; set; } = 20;
    }

    public class GetCommerceTransactionsQueryHandler : IRequestHandler<GetCommerceTransactionsQuery, CommerceTransactionsResponseDto>
    {
        private readonly ICommerceRepository _commerceRepository;
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;

        public GetCommerceTransactionsQueryHandler(ICommerceRepository commerceRepository, ISavingsAccountRepository savingsAccountRepository,
            ITransactionRepository transactionRepository, IBasicUserInfoService basicUserInfoService)
        {
            _commerceRepository = commerceRepository;
            _savingsAccountRepository = savingsAccountRepository;
            _transactionRepository = transactionRepository;
            _basicUserInfoService = basicUserInfoService;
        }

        public async Task<CommerceTransactionsResponseDto> Handle(GetCommerceTransactionsQuery request, CancellationToken cancellationToken)
        {
            if (request.PageSize > 20)
            {
                request.PageSize = 20;
            }

            var commerce = await _commerceRepository.GetByIdAsync(request.CommerceId);
            if (commerce is null)
            {
                throw new ApiException("The specified commerce does not exist.", (int)HttpStatusCode.NotFound);
            }

            if (!commerce.IsActive)
            {
                throw new ApiException("The specified commerce is not active.", (int)HttpStatusCode.BadRequest);
            }

            var commerceUserId = await _basicUserInfoService.GetUserIdByCommerceIdAsync(commerce.Id);
            var principalAccount = commerceUserId is null
                ? null
                : await _savingsAccountRepository.GetPrincipalAccountByClientIdAsync(commerceUserId);

            if (principalAccount is null)
            {
                return new CommerceTransactionsResponseDto
                {
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalRecords = 0,
                    CommerceId = commerce.Id,
                    CommerceName = commerce.Name,
                    Items = []
                };
            }

            var query = _transactionRepository.GetAllQuery()
                .Where(t => t.SavingsAccountId == principalAccount.Id && t.Category == TransactionCategory.HermesPayment);

            var totalRecords = await query.CountAsync(cancellationToken);

            var transactions = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            return new CommerceTransactionsResponseDto
            {
                Page = request.Page,
                PageSize = request.PageSize,
                TotalRecords = totalRecords,
                CommerceId = commerce.Id,
                CommerceName = commerce.Name,
                Items = transactions.Select(t => new CommerceTransactionDto
                {
                    Id = t.Id,
                    TransactionDate = t.CreatedAt,
                    Amount = t.Amount,
                    CardLastFourDigits = t.Origin,
                    Status = t.Status
                }).ToList()
            };
        }
    }
}
