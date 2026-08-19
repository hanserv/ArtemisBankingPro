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

            config.NewConfig<ExpressTransactionViewModel, ExpressTransactionDto>();
            config.NewConfig<ExpressTransactionConfirmationDto, ExpressTransactionConfirmationViewModel>();
            config.NewConfig<ExpressTransactionConfirmationViewModel, ExpressTransactionConfirmationDto>();

            config.NewConfig<ClientCreditCardPaymentViewModel, ClientCreditCardPaymentDto>();

            config.NewConfig<ClientLoanPaymentViewModel, ClientLoanPaymentDto>();

            config.NewConfig<BeneficiaryTransactionViewModel, BeneficiaryTransactionDto>();
            config.NewConfig<BeneficiaryTransactionConfirmationDto, BeneficiaryTransactionConfirmationViewModel>();
            config.NewConfig<BeneficiaryTransactionConfirmationViewModel, BeneficiaryTransactionConfirmationDto>();

            config.NewConfig<OwnAccountTransferViewModel, OwnAccountTransferDto>();
            config.NewConfig<OwnAccountTransferConfirmationDto, OwnAccountTransferConfirmationViewModel>();
            config.NewConfig<OwnAccountTransferConfirmationViewModel, OwnAccountTransferConfirmationDto>();

            config.NewConfig<CashAdvanceViewModel, CashAdvanceDto>();
        }
    }
}
