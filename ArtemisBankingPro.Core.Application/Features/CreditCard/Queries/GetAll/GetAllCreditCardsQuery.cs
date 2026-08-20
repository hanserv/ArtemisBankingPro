using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.CreditCard.Queries.GetAll
{
    /// <summary>
    /// Filter parameters for retrieving the paginated list of credit cards.
    /// </summary>
    public class GetAllCreditCardsQuery : IRequest<PagedResult<CreditCardDto>>
    {
        /// <example>1</example>
        [SwaggerParameter(Description = "Page number to retrieve.")]
        public int Page { get; set; } = 1;

        /// <example>20</example>
        [SwaggerParameter(Description = "Number of records per page. Maximum allowed is 20.")]
        public int PageSize { get; set; } = 20;

        /// <example>Active</example>
        [SwaggerParameter(Description = "Optional status filter. Allowed values: Active, Cancelled or All. If not sent and no identification is provided, defaults to Active.")]
        public CreditCardStatusFilter? Status { get; set; }

        [SwaggerParameter(Description = "Optional client identification to search credit cards for a specific client.")]
        public string? Identification { get; set; }
    }

    public class GetAllCreditCardsQueryHandler : IRequestHandler<GetAllCreditCardsQuery, PagedResult<CreditCardDto>>
    {
        private readonly ICreditCardRepository _creditCardRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IMapper _mapper;

        public GetAllCreditCardsQueryHandler(
            ICreditCardRepository creditCardRepository, IBasicUserInfoService basicUserInfoService, IMapper mapper)
        {
            _creditCardRepository = creditCardRepository;
            _basicUserInfoService = basicUserInfoService;
            _mapper = mapper;
        }

        public async Task<PagedResult<CreditCardDto>> Handle(GetAllCreditCardsQuery request, CancellationToken cancellationToken)
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
                    return new PagedResult<CreditCardDto>
                    {
                        Page = request.Page,
                        PageSize = request.PageSize,
                        TotalRecords = 0,
                        Items = []
                    };
                }
            }

            var query = _creditCardRepository.GetAllQuery();

            if (request.Status is CreditCardStatusFilter.Active)
            {
                query = query.Where(c => c.Status == CreditCardStatus.Active);
            }
            else if (request.Status is CreditCardStatusFilter.Cancelled)
            {
                query = query.Where(c => c.Status == CreditCardStatus.Cancelled);
            }
            else if (request.Status is null && clientId is null)
            {
                query = query.Where(c => c.Status == CreditCardStatus.Active);
            }

            if (clientId is not null)
            {
                query = query.Where(c => c.ClientId == clientId);
            }

            var totalRecords = await query.CountAsync(cancellationToken);

            var orderedQuery = clientId is not null && request.Status is null or CreditCardStatusFilter.All
                ? query.OrderBy(c => c.Status == CreditCardStatus.Cancelled).ThenByDescending(c => c.CreatedAt)
                : query.OrderByDescending(c => c.CreatedAt);

            var cards = await orderedQuery
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var items = new List<CreditCardDto>();
            foreach (var card in cards)
            {
                var dto = _mapper.Map<CreditCardDto>(card);
                dto.ClientFullName = await _basicUserInfoService.GetFullNameAsync(card.ClientId);
                dto.CreatedByAdminName = await _basicUserInfoService.GetFullNameAsync(card.CreatedByAdminId);
                items.Add(dto);
            }

            return new PagedResult<CreditCardDto>
            {
                Items = items,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalRecords = totalRecords
            };
        }
    }
}
