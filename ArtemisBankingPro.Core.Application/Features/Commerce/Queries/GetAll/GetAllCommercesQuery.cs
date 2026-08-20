using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.Commerce;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.Commerce.Queries.GetAll
{
    public class GetAllCommercesQuery : IRequest<PagedResult<CommerceDto>>
    {
        /// <example>1</example>
        [SwaggerParameter(Description = "Page number to retrieve.")]
        public int Page { get; set; } = 1;

        /// <example>20</example>
        [SwaggerParameter(Description = "Number of records per page. Maximum allowed is 20.")]
        public int PageSize { get; set; } = 20;

        /// <example>Active</example>
        [SwaggerParameter(Description = "Status filter. Allowed values: Active, Inactive, All.")]
        public CommerceStatusFilter Status { get; set; } = CommerceStatusFilter.Active;
    }

    public class GetAllCommercesQueryHandler : IRequestHandler<GetAllCommercesQuery, PagedResult<CommerceDto>>
    {
        private readonly ICommerceRepository _commerceRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IMapper _mapper;

        public GetAllCommercesQueryHandler(ICommerceRepository commerceRepository, IBasicUserInfoService basicUserInfoService, IMapper mapper)
        {
            _commerceRepository = commerceRepository;
            _basicUserInfoService = basicUserInfoService;
            _mapper = mapper;
        }

        public async Task<PagedResult<CommerceDto>> Handle(GetAllCommercesQuery request, CancellationToken cancellationToken)
        {
            if (request.PageSize > 20)
            {
                request.PageSize = 20;
            }

            var query = _commerceRepository.GetAllQuery();

            query = request.Status switch
            {
                CommerceStatusFilter.Active => query.Where(c => c.IsActive),
                CommerceStatusFilter.Inactive => query.Where(c => !c.IsActive),
                _ => query
            };

            var totalRecords = await query.CountAsync();

            var commerces = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            var commerceIdsWithUser = await _basicUserInfoService.GetCommerceIdsWithAssociatedUserAsync(commerces.Select(c => c.Id));

            var items = commerces.Select(c =>
            {
                var dto = _mapper.Map<CommerceDto>(c);
                dto.HasAssociatedUser = commerceIdsWithUser.Contains(c.Id);
                return dto;
            }).ToList();

            return new PagedResult<CommerceDto>
            {
                Items = items,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalRecords = totalRecords
            };
        }
    }
}
