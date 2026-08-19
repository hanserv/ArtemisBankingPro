using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.Helpers;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class CreditCardService : ICreditCardService
    {
        private readonly ICreditCardRepository _creditCardRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IMapper _mapper;
        private readonly IEmailService _emailService;
        private readonly ILogger<CreditCardService> _logger;

        public CreditCardService(ICreditCardRepository creditCardRepository, IBasicUserInfoService basicUserInfoService,
            IMapper mapper, IEmailService emailService,
            ILogger<CreditCardService> logger)
        {
            _creditCardRepository = creditCardRepository;
            _basicUserInfoService = basicUserInfoService;
            _mapper = mapper;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<Result<CreditCardDto>> GetByIdAsync(int id)
        {
            var card = await _creditCardRepository.GetByIdAsync(id);
            if (card is null)
            {
                return Result<CreditCardDto>.Failure(error: "The selected credit card does not exist.");
            }

            var dto = _mapper.Map<CreditCardDto>(card);
            dto.ClientFullName = await _basicUserInfoService.GetFullNameAsync(card.ClientId);
            dto.CreatedByAdminName = await _basicUserInfoService.GetFullNameAsync(card.CreatedByAdminId);

            return Result<CreditCardDto>.Success(dto);
        }

        public async Task<Result<PagedResult<CreditCardDto>>> GetPagedAsync(CreditCardFilterDto filter)
        {
            if (filter.Page <= 0)
            {
                return Result<PagedResult<CreditCardDto>>.Failure(error: "The page parameter must be greater than zero.");
            }

            if (filter.PageSize <= 0)
            {
                return Result<PagedResult<CreditCardDto>>.Failure(error: "The pageSize parameter must be greater than zero.");
            }

            if (filter.PageSize > 20)
            {
                filter.PageSize = 20;
            }

            string? clientId = null;

            if (!string.IsNullOrWhiteSpace(filter.Identification))
            {
                clientId = await _basicUserInfoService.GetUserIdByIdentificationAsync(filter.Identification);
                if (clientId is null)
                {
                    return Result<PagedResult<CreditCardDto>>.Failure(error: "There is no client registered with this identification.");
                }
            }

            var query = _creditCardRepository.GetAllQuery();

            if (filter.Status is not null)
            {
                query = query.Where(c => c.Status == filter.Status);
            }

            if (clientId is not null)
            {
                query = query.Where(c => c.ClientId == clientId);
            }

            var totalRecords = await query.CountAsync();

            if (clientId is not null && totalRecords == 0)
            {
                return Result<PagedResult<CreditCardDto>>.Failure(error: "This client has no credit cards registered.");
            }

            var orderedQuery = clientId is not null && filter.Status is null
                ? query.OrderBy(c => c.Status == CreditCardStatus.Cancelled).ThenByDescending(c => c.CreatedAt)
                : query.OrderByDescending(c => c.CreatedAt);

            var cards = await orderedQuery
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var items = new List<CreditCardDto>();
            foreach (var card in cards)
            {
                var dto = _mapper.Map<CreditCardDto>(card);
                dto.LastFourDigits = card.CardNumber[^4..];
                dto.ClientFullName = await _basicUserInfoService.GetFullNameAsync(card.ClientId);
                dto.CreatedByAdminName = await _basicUserInfoService.GetFullNameAsync(card.CreatedByAdminId);
                items.Add(dto);
            }

            return Result<PagedResult<CreditCardDto>>.Success(new PagedResult<CreditCardDto>
            {
                Items = items,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalRecords = totalRecords
            });
        }

        public async Task<Result<List<CardConsumptionDto>>> GetConsumptionsAsync(int creditCardId)
        {
            var cardExists = await _creditCardRepository.GetByIdAsync(creditCardId);
            if (cardExists is null)
            {
                return Result<List<CardConsumptionDto>>.Failure(error: "The selected credit card does not exist.");
            }

            var consumptions = await _creditCardRepository.GetAllQueryInclude(["Consumptions", "Consumptions.Commerce"])
                    .Where(c => c.Id == creditCardId)
                    .SelectMany(c => c.Consumptions!)
                    .OrderByDescending(c => c.ConsumptionDate)
                    .ToListAsync();

            return Result<List<CardConsumptionDto>>.Success(_mapper.Map<List<CardConsumptionDto>>(consumptions));
        }

        public async Task<Result<List<CreditCardDto>>> GetActiveCardsByClientIdAsync(string clientId)
        {
            var cards = await _creditCardRepository.GetActiveByClientIdAsync(clientId);
            return Result<List<CreditCardDto>>.Success(_mapper.Map<List<CreditCardDto>>(cards));
        }

        public async Task<Result> ValidateClientForAssignmentAsync(string? clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return Result.Failure(error: "You must select a client to continue.");
            }

            var isActive = await _basicUserInfoService.IsClientActiveAsync(clientId);

            if (isActive is null or false)
            {
                return Result.Failure(error: "Credit Cards can only be assigned to active clients.");
            }

            return Result.Success();
        }

        public async Task<Result> AssignCreditCardAsync(AssignCreditCardDto dto, string createdByAdminId)
        {
            _logger.LogInformation("Administrator {AdminId} initiated credit card assignment for client {ClientId} with a requested credit limit of {CreditLimit:C}.", createdByAdminId, dto.ClientId, dto.CreditLimit);


            if (dto.CreditLimit <= 0)
            {
                _logger.LogWarning("Credit card assignment rejected for client {ClientId}: requested credit limit {CreditLimit:C} is not greater than zero.", dto.ClientId, dto.CreditLimit);

                return Result.Failure(error: "The credit limit must be greater than zero.");
            }

            var validation = await ValidateClientForAssignmentAsync(dto.ClientId);
            if (!validation.IsSuccess)
            {
                _logger.LogWarning("Credit card assignment rejected for client {ClientId}: {Error}.", dto.ClientId, validation.Error);

                return validation;
            }

            var cardNumber = await GenerateCardNumberAsync();
            var cvc = NumericStringGenerator.Generate(3);
            var expirationDate = DateTime.UtcNow.AddYears(3).ToString("MM/yy");

            var card = new CreditCard
            {
                Id = 0,
                CardNumber = cardNumber,
                ClientId = dto.ClientId,
                CreditLimit = dto.CreditLimit,
                CurrentDebt = 0m,
                ExpirationDate = expirationDate,
                CvcHash = Sha256Helper.Hash(cvc),
                CreatedByAdminId = createdByAdminId,
                Status = CreditCardStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            await _creditCardRepository.AddAsync(card);

            _logger.LogInformation("Credit card ending in {LastFourDigits} assigned to client {ClientId} by administrator {AdminId} with a credit limit of {CreditLimit:C}.", card.CardNumber[^4..], dto.ClientId, createdByAdminId, dto.CreditLimit);

            var emailSent = await TrySendAssignmentEmailAsync(card);

            if (!emailSent)
            {
                _logger.LogWarning("Credit card ending in {LastFourDigits} was created for client {ClientId}, but the assignment notification email could not be sent.", card.CardNumber[^4..], dto.ClientId);
            }


            return Result.Success(message: emailSent
                ? "The credit card has been assigned successfully."
                : "The credit card was created successfully, but the notification email could not be sent.");
        }

        public async Task<Result> ModifyCreditCardLimitAsync(ModifyCreditCardLimitDto dto, string performedByAdminId)
        {
            var card = await _creditCardRepository.GetByIdAsync(dto.CreditCardId);
            if (card is null)
            {
                return Result.Failure(error: "The selected credit card does not exist.");
            }

            if (card.Status != CreditCardStatus.Active)
            {
                return Result.Failure(error: "Cancelled credit cards cannot be modified.");
            }

            if (dto.CreditLimit <= 0)
            {
                return Result.Failure(error: "The credit limit must be greater than zero.");
            }

            if (dto.CreditLimit < card.CurrentDebt)
            {
                return Result.Failure(error: "The credit limit cannot be lower than the current outstanding debt.");
            }

            card.CreditLimit = dto.CreditLimit;
            await _creditCardRepository.UpdateAsync(card);

            _logger.LogInformation( "Credit limit for card ending in {LastFourDigits} updated to {NewLimit:C} by administrator {AdminId}.", card.CardNumber[^4..], dto.CreditLimit, performedByAdminId);

            var emailSent = await TrySendLimitChangeEmailAsync(card);

            return Result.Success(message: emailSent
                ? "The credit limit has been updated successfully."
                : "The credit limit was updated successfully, but the notification email could not be sent.");
        }

        public async Task<Result> CancelCreditCardAsync(int creditCardId, string performedByAdminId)
        {
            var card = await _creditCardRepository.GetByIdAsync(creditCardId);
            if (card is null)
            {
                return Result.Failure(error: "The selected credit card does not exist.");
            }

            if (card.Status != CreditCardStatus.Active)
            {
                return Result.Failure(error: "The selected credit card is already cancelled.");
            }

            if (card.CurrentDebt > 0)
            {
                return Result.Failure(error: "To cancel this card, the client must first settle the outstanding debt.");
            }

            card.Status = CreditCardStatus.Cancelled;
            await _creditCardRepository.UpdateAsync(card);

            _logger.LogInformation("Credit card ending in {LastFourDigits} cancelled by administrator {AdminId}.", card.CardNumber[^4..], performedByAdminId);

            return Result.Success(message: "The credit card has been cancelled successfully.");
        }

        public async Task<Result<CreditCardDto>> GetClientCardByIdAsync(int id, string clientId)
        {
            var card = await _creditCardRepository.GetAllQuery()
                .FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId);

            if (card is null)
            {
                return Result<CreditCardDto>.Failure(error: "The selected credit card does not exist.");
            }

            var dto = _mapper.Map<CreditCardDto>(card);

            return Result<CreditCardDto>.Success(dto);
        }

        public async Task<Result<List<CardConsumptionDto>>> GetClientCardConsumptionsAsync(int id, string clientId)
        {
            var cardExists = await _creditCardRepository.GetAllQuery()
                    .AnyAsync(c => c.Id == id && c.ClientId == clientId);

            if (!cardExists)
            {
                return Result<List<CardConsumptionDto>>.Failure(error: "The selected credit card does not exist.");
            }

            var consumptions = await _creditCardRepository.GetAllQueryInclude(["Consumptions", "Consumptions.Commerce"])
                    .Where(c => c.Id == id)
                    .SelectMany(c => c.Consumptions!)
                    .OrderByDescending(c => c.ConsumptionDate)
                    .ToListAsync();

            return Result<List<CardConsumptionDto>>.Success(_mapper.Map<List<CardConsumptionDto>>(consumptions));
        }

        #region Private Methods
        private async Task<string> GenerateCardNumberAsync()
        {
            const int maxAttempts = 25;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var candidate = NumericStringGenerator.Generate(16);

                if (!await _creditCardRepository.CardNumberExistsAsync(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Could not generate a unique 16-digit card number after several attempts.");
        }

        private async Task<bool> TrySendAssignmentEmailAsync(CreditCard card)
        {
            var clientInfo = await _basicUserInfoService.GetBasicInfoAsync(card.ClientId);
            var lastFour = card.CardNumber[^4..];

            try
            {
                await _emailService.SendAsync(new EmailRequestDto
                {
                    To = clientInfo!.Email,
                    Subject = "New credit card assigned",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{clientInfo.FullName}</span></h3>
                        <p>A new credit card has been assigned to your account.</p>
                        <p>Card ending in: <strong>{lastFour}</strong></p>
                        <p>Approved limit: <strong>RD$ {card.CreditLimit:N2}</strong></p>
                        <p>Expiration date: <strong>{card.ExpirationDate}</strong></p>
                        <p>Assignment date: <strong>{card.CreatedAt:dd/MM/yyyy}</strong></p>
                        <p style="font-size: 14px; color: #6c757d;">For your security, do not share your card information with anyone.</p>
                    """
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send assignment notification email for credit card ending in {LastFourDigits} to client {ClientId}.", lastFour, card.ClientId);

                return false;
            }
        }

        private async Task<bool> TrySendLimitChangeEmailAsync(CreditCard card)
        {
            var clientInfo = await _basicUserInfoService.GetBasicInfoAsync(card.ClientId);
            var lastFour = card.CardNumber[^4..];

            try
            {
                await _emailService.SendAsync(new EmailRequestDto
                {
                    To = clientInfo!.Email,
                    Subject = "Credit card limit modification",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{clientInfo.FullName}</span></h3>
                        <p>The credit limit of your credit card ending in <strong>{lastFour}</strong> has been updated.</p>
                        <p>New approved limit: <strong>RD$ {card.CreditLimit:N2}</strong></p>
                        <p style="font-size: 14px; color: #6c757d;">If you do not recognize this change, please contact the bank.</p>
                    """
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send limit change notification email for credit card ending in {LastFourDigits} to client {ClientId}.", lastFour, card.ClientId);

                return false;
            }
        }
        #endregion
    }
}