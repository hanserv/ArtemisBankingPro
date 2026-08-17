using ArtemisBankingPro.Core.Application.DTOs.Beneficiary;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class BeneficiaryService : IBeneficiaryService
    {
        private readonly IBeneficiaryRepository _beneficiaryRepository;
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IMapper _mapper;
        private readonly ILogger<BeneficiaryService> _logger;

        public BeneficiaryService(IBeneficiaryRepository beneficiaryRepository, ISavingsAccountRepository savingsAccountRepository,
            IBasicUserInfoService basicUserInfoService, IMapper mapper,
            ILogger<BeneficiaryService> logger)
        {
            _beneficiaryRepository = beneficiaryRepository;
            _savingsAccountRepository = savingsAccountRepository;
            _basicUserInfoService = basicUserInfoService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<Result<List<BeneficiaryDto>>> GetByClientIdAsync(string clientId)
        {
            var beneficiaries = await _beneficiaryRepository.GetByClientIdAsync(clientId);

            var items = _mapper.Map<List<BeneficiaryDto>>(beneficiaries);

            for (var i = 0; i < beneficiaries.Count; i++)
            {
                var owner = await _basicUserInfoService.GetBasicInfoAsync(beneficiaries[i].SavingsAccount!.ClientId);
                items[i].FullName = owner!.FullName;
            }

            return Result<List<BeneficiaryDto>>.Success(items);
        }

        public async Task<Result<BeneficiaryDto>> GetByIdAsync(int id, string clientId)
        {
            var beneficiary = await _beneficiaryRepository.GetAllQueryInclude(["SavingsAccount"])
                    .FirstOrDefaultAsync(b => b.Id == id && b.ClientId == clientId);

            if (beneficiary is null)
            {
                return Result<BeneficiaryDto>.Failure("The selected beneficiary does not exist.");
            }

            var dto = _mapper.Map<BeneficiaryDto>(beneficiary);
            var owner = await _basicUserInfoService.GetBasicInfoAsync(beneficiary.SavingsAccount!.ClientId);
            dto.FullName = owner!.FullName;

            return Result<BeneficiaryDto>.Success(dto);
        }

        public async Task<Result> AddAsync(AddBeneficiaryDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.AccountNumber))
            {
                return Result.Failure("The account number is required.");
            }

            if (dto.AccountNumber.Length != 9 || !dto.AccountNumber.All(char.IsDigit))
            {
                return Result.Failure("The account number must contain exactly 9 digits.");
            }

            var account = await _savingsAccountRepository.GetByAccountNumberAsync(dto.AccountNumber);
            if (account is null)
            {
                return Result.Failure("The account number entered does not correspond to a valid account.");
            }

            if (account.Status != SavingsAccountStatus.Active)
            {
                return Result.Failure("You cannot add a cancelled account as a beneficiary.");
            }

            if (account.ClientId == dto.ClientId)
            {
                return Result.Failure("You cannot add your own account as a beneficiary. Use the Transfer option to move funds between your accounts.");
            }

            var alreadyExists = await _beneficiaryRepository.ExistsAsync(dto.ClientId, account.Id);
            if (alreadyExists)
            {
                return Result.Failure("This account is already registered as a beneficiary.");
            }

            var beneficiary = _mapper.Map<Beneficiary>(dto);
            beneficiary.SavingsAccountId = account.Id;
            beneficiary.CreatedAt = DateTime.UtcNow;

            await _beneficiaryRepository.AddAsync(beneficiary);

            _logger.LogInformation("Beneficiary account ending in {LastFourDigits} added by client {ClientId}.",
                account.AccountNumber[^4..], dto.ClientId);

            return Result.Success("Beneficiary added successfully.");
        }

        public async Task<Result> DeleteAsync(int beneficiaryId, string clientId)
        {
            var beneficiary = await _beneficiaryRepository.GetAllQuery()
                    .FirstOrDefaultAsync(b => b.Id == beneficiaryId && b.ClientId == clientId);

            if (beneficiary is null)
            {
                return Result.Failure("The selected beneficiary does not exist.");
            }

            await _beneficiaryRepository.DeleteAsync(beneficiary);

            _logger.LogInformation("Beneficiary {BeneficiaryId} removed by client {ClientId}.", beneficiaryId, clientId);

            return Result.Success("Beneficiary deleted successfully.");
        }
    }
}
