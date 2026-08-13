using ArtemisBankingPro.Core.Application.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MarkOverdueLoanInstallments;

public class MarkOverdueLoanInstallmentsFunction
{
    private readonly ILogger _logger;
    private readonly ILoanService _loanService;

    public MarkOverdueLoanInstallmentsFunction(ILoggerFactory loggerFactory, ILoanService loanService)
    {
        _logger = loggerFactory.CreateLogger<MarkOverdueLoanInstallmentsFunction>();
        _loanService = loanService;
    }

    [Function("MarkOverdueLoanInstallmentsFunction")]
    public async Task Run([TimerTrigger("%TimeTrigger%")] TimerInfo myTimer)
    {
        _logger.LogInformation("MarkOverdueLoanInstallments started.");

        var updatedCount = await _loanService.MarkOverdueInstallmentsAsync();

        _logger.LogInformation("MarkOverdueLoanInstallments finished. {Count} installment(s) updated.", updatedCount);
    }
}