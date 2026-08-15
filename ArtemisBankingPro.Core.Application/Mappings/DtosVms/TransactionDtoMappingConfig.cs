using ArtemisBankingPro.Core.Application.DTOs.Transaction;
using ArtemisBankingPro.Core.Application.ViewModels.Transaction;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.DtosVms
{
    public class TransactionDtoMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<TransactionDto, TransactionViewModel>();

            config.NewConfig<DepositViewModel, DepositDto>();
            config.NewConfig<DepositConfirmationDto, DepositConfirmationViewModel>();
            config.NewConfig<DepositConfirmationViewModel, DepositConfirmationDto>();

            config.NewConfig<WithdrawalViewModel, WithdrawalDto>();
            config.NewConfig<WithdrawalConfirmationDto, WithdrawalConfirmationViewModel>();
            config.NewConfig<WithdrawalConfirmationViewModel, WithdrawalConfirmationDto>();

            config.NewConfig<CreditCardPaymentViewModel, CreditCardPaymentDto>();
            config.NewConfig<CreditCardPaymentConfirmationDto, CreditCardPaymentConfirmationViewModel>();
            config.NewConfig<CreditCardPaymentConfirmationViewModel, CreditCardPaymentConfirmationDto>();

            config.NewConfig<ThirdPartyTransactionViewModel, ThirdPartyTransactionDto>();
            config.NewConfig<ThirdPartyTransactionConfirmationDto, ThirdPartyTransactionConfirmationViewModel>();
            config.NewConfig<ThirdPartyTransactionConfirmationViewModel, ThirdPartyTransactionConfirmationDto>();

            config.NewConfig<LoanPaymentViewModel, LoanPaymentDto>();
            config.NewConfig<LoanPaymentConfirmationDto, LoanPaymentConfirmationViewModel>();
            config.NewConfig<LoanPaymentConfirmationViewModel, LoanPaymentConfirmationDto>();
        }
    }
}
