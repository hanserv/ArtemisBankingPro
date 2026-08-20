using System.Security.Claims;
using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;
using ArtemisBankingPro.Core.Application.Features.SavingsAccount.Commands.AssignSecondary;
using ArtemisBankingPro.Core.Application.Features.SavingsAccount.Commands.CancelSecondary;
using ArtemisBankingPro.Core.Application.Features.SavingsAccount.Queries.GetAll;
using ArtemisBankingPro.Core.Application.Features.SavingsAccount.Queries.GetTransactions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Api.Controllers.v1
{
    [ApiVersion("1.0")]
    [Authorize(Roles = "Admin")]
    [SwaggerTag("Provides endpoints for managing customer savings accounts, including querying accounts, assigning new secondary " +
        "accounts to customers, viewing account transaction history, and canceling secondary accounts when applicable")]
    public class SavingsAccountController : BaseApiController
    {
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<SavingsAccountResponseDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [SwaggerOperation(
            Summary = "Get all savings accounts",
            Description = "Retrieves a paginated list of all savings accounts in the system."
        )]
        public async Task<IActionResult> GetAll([FromQuery] GetAllSavingsAccountsQuery query)
        {
            var accounts = await Mediator.Send(query);
            return Ok(accounts);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(SavingsAccountDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [SwaggerOperation(
            Summary = "Assign a secondary savings account",
            Description = "Creates a new secondary savings account for an active client. This endpoint does not create principal accounts."
        )]
        public async Task<IActionResult> Assign(AssignSecondaryAccountCommand command)
        {
            command.CreatedByAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var account = await Mediator.Send(command);
            return StatusCode(StatusCodes.Status201Created, account);
        }

        [HttpGet("{accountNumber}/transactions")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SavingsAccountTransactionHistoryDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(
            Summary = "Get account transaction history",
            Description = "Retrieves the transaction history registered for a specific savings account, ordered from most recent to oldest."
        )]
        public async Task<IActionResult> GetTransactions(string accountNumber, [FromQuery] GetSavingsAccountTransactionsQuery query)
        {
            query.AccountNumber = accountNumber;
            var result = await Mediator.Send(query);
            return Ok(result);
        }

        [HttpPatch("{accountNumber}/cancel")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(
            Summary = "Cancel a secondary savings account",
            Description = "Cancels an active secondary savings account. If it has an available balance, it is automatically transferred to the client's active principal account before cancelling."
        )]
        public async Task<IActionResult> Cancel(string accountNumber)
        {
            await Mediator.Send(new CancelSecondaryAccountCommand
            {
                AccountNumber = accountNumber,
                PerformedByAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!
            });

            return NoContent();
        }
    }
}
