using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.Loan.Queries.GetAll
{
    /// <summary>
    /// Filter parameters for retrieving the paginated list of loans.
    /// </summary>
    public class GetAllLoansQuery : IRequest<PagedResult<LoanDto>>
    {
        /// <example>1</example>
        [SwaggerParameter(Description = "Page number to retrieve.")]
        public int Page { get; set; } = 1;

        /// <example>20</example>
        [SwaggerParameter(Description = "Number of records per page. Maximum allowed is 20.")]
        public int PageSize { get; set; } = 20;

        /// <example>Active</example>
        [SwaggerParameter(Description = "Optional status filter.")]
        public LoanStatus? Status { get; set; } = LoanStatus.Active;

        [SwaggerParameter(Description = "Optional client identification to search loans for a specific client.")]
        public string? Identification { get; set; }
    }

    public class GetAllLoansQueryHandler : IRequestHandler<GetAllLoansQuery, PagedResult<LoanDto>>
    {
        private readonly ILoanRepository _loanRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IMapper _mapper;

        public GetAllLoansQueryHandler(ILoanRepository loanRepository, IBasicUserInfoService basicUserInfoService, 
            IMapper mapper)
        {
            _loanRepository = loanRepository;
            _basicUserInfoService = basicUserInfoService;
            _mapper = mapper;
        }

        public async Task<PagedResult<LoanDto>> Handle(GetAllLoansQuery request, CancellationToken cancellationToken)
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
                    return new PagedResult<LoanDto>
                    {
                        Page = request.Page,
                        PageSize = request.PageSize,
                        TotalRecords = 0,
                        Items = []
                    };
                }
            }

            var query = _loanRepository.GetAllQueryInclude(["Installments"]);

            if (request.Status is not null)
            {
                query = query.Where(l => l.Status == request.Status);
            }

            if (clientId is not null)
            {
                query = query.Where(l => l.ClientId == clientId);
            }

            var totalRecords = await query.CountAsync();

            if (clientId is not null && totalRecords == 0)
            {
                return new PagedResult<LoanDto>
                {
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalRecords = totalRecords,
                    Items = []
                };
            }

            var orderedQuery = clientId is not null && request.Status is null
            ? query.OrderBy(l => l.Status == LoanStatus.Completed).ThenByDescending(l => l.CreatedAt)
            : query.OrderByDescending(l => l.CreatedAt);

            var loans = await orderedQuery
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            var items = new List<LoanDto>();
            foreach (var loan in loans)
            {
                var dto = _mapper.Map<LoanDto>(loan);
                dto.ClientFullName = await _basicUserInfoService.GetFullNameAsync(loan.ClientId);
                items.Add(dto);
            }

            return new PagedResult<LoanDto>
            {
                Page = request.Page,
                PageSize = request.PageSize,
                TotalRecords = totalRecords,
                Items = items
            };
        }
    }
}
