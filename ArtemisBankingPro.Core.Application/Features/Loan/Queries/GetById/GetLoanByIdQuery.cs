using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Core.Application.Features.Loan.Queries.GetById
{
    public class GetLoanByIdQuery : IRequest<LoanDetailsDto>
    {
        public required int Id { get; set; }
    }

    public class GetLoanByIdQueryHandler : IRequestHandler<GetLoanByIdQuery, LoanDetailsDto>
    {
        private readonly ILoanRepository _loanRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IMapper _mapper;

        public GetLoanByIdQueryHandler(
            ILoanRepository loanRepository, IBasicUserInfoService basicUserInfoService, IMapper mapper)
        {
            _loanRepository = loanRepository;
            _basicUserInfoService = basicUserInfoService;
            _mapper = mapper;
        }

        public async Task<LoanDetailsDto> Handle(GetLoanByIdQuery request, CancellationToken cancellationToken)
        {
            var loan = await _loanRepository.GetAllQueryInclude(["Installments"])
                    .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);

            if (loan is null)
            {
                throw new ApiException("The requested loan does not exist.", (int)HttpStatusCode.NotFound);
            }

            var loanDto = _mapper.Map<LoanDto>(loan);
            loanDto.ClientFullName = await _basicUserInfoService.GetFullNameAsync(loan.ClientId);

            var installments = loan.Installments
                    .OrderBy(i => i.InstallmentNumber)
                    .ToList();

            return new LoanDetailsDto
            {
                Loan = loanDto,
                Installments = _mapper.Map<List<LoanInstallmentDto>>(installments)
            };
        }
    }

}
