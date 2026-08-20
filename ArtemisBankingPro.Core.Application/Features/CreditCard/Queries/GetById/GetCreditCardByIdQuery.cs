using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Core.Application.Features.CreditCard.Queries.GetById
{
    /// <summary>
    /// Parameters required to retrieve a credit card's details and consumption history.
    /// </summary>
    public class GetCreditCardByIdQuery : IRequest<CreditCardDetailsResponseDto>
    {
        public required int Id { get; set; }
    }

    public class GetCreditCardByIdQueryHandler : IRequestHandler<GetCreditCardByIdQuery, CreditCardDetailsResponseDto>
    {
        private readonly ICreditCardRepository _creditCardRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IMapper _mapper;

        public GetCreditCardByIdQueryHandler(
            ICreditCardRepository creditCardRepository,
            IBasicUserInfoService basicUserInfoService,
            IMapper mapper)
        {
            _creditCardRepository = creditCardRepository;
            _basicUserInfoService = basicUserInfoService;
            _mapper = mapper;
        }

        public async Task<CreditCardDetailsResponseDto> Handle(GetCreditCardByIdQuery request, CancellationToken cancellationToken)
        {
            var card = await _creditCardRepository.GetByIdAsync(request.Id);
            if (card is null)
            {
                throw new ApiException("The selected credit card does not exist.", (int)HttpStatusCode.NotFound);
            }

            var cardDto = _mapper.Map<CreditCardDto>(card);
            cardDto.ClientFullName = await _basicUserInfoService.GetFullNameAsync(card.ClientId);
            cardDto.CreatedByAdminName = await _basicUserInfoService.GetFullNameAsync(card.CreatedByAdminId);

            var consumptions = await _creditCardRepository.GetAllQueryInclude(["Consumptions", "Consumptions.Commerce"])
                .Where(c => c.Id == request.Id)
                .SelectMany(c => c.Consumptions!)
                .OrderByDescending(c => c.ConsumptionDate)
                .ToListAsync(cancellationToken);

            var responseDto = _mapper.Map<CreditCardDetailsResponseDto>(cardDto);
            responseDto.Consumptions = _mapper.Map<List<CardConsumptionDto>>(consumptions);

            return responseDto;
        }
    }
}
