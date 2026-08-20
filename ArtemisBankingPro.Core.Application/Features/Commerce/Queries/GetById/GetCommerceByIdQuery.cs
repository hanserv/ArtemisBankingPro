using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.Commerce;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using MediatR;

namespace ArtemisBankingPro.Core.Application.Features.Commerce.Queries.GetById
{
    public class GetCommerceByIdQuery : IRequest<CommerceDetailsDto>
    {
        public int Id { get; set; }
    }

    public class GetCommerceByIdQueryHandler : IRequestHandler<GetCommerceByIdQuery, CommerceDetailsDto>
    {
        private readonly ICommerceRepository _commerceRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IMapper _mapper;

        public GetCommerceByIdQueryHandler(ICommerceRepository commerceRepository, IBasicUserInfoService basicUserInfoService,
            IMapper mapper)
        {
            _commerceRepository = commerceRepository;
            _basicUserInfoService = basicUserInfoService;
            _mapper = mapper;
        }

        public async Task<CommerceDetailsDto> Handle(GetCommerceByIdQuery request, CancellationToken cancellationToken)
        {
            var commerce = await _commerceRepository.GetByIdAsync(request.Id);

            if (commerce is null)
            {
                throw new ApiException("The selected commerce does not exist.", (int)HttpStatusCode.NotFound);
            }

            var dto = _mapper.Map<CommerceDetailsDto>(commerce);

            if (!string.IsNullOrWhiteSpace(commerce.AssociatedUserId))
            {
                dto.AssociatedUser = await _basicUserInfoService.GetCommerceAssociatedUserInfoAsync(commerce.AssociatedUserId);
            }

            return dto;
        }
    }
}
