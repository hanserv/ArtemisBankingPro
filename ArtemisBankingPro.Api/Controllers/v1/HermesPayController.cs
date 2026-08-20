using System.Security.Claims;
using ArtemisBankingPro.Core.Application.DTOs.HermesPay;
using ArtemisBankingPro.Core.Application.Features.HermesPay.Commands.ProcessPayment;
using ArtemisBankingPro.Core.Application.Features.HermesPay.Queries.GetCommerceTransactions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Api.Controllers.v1
{
    [ApiVersion("1.0")]
    [Authorize(Roles = "Admin,Commerce")]
    [SwaggerTag("Provides endpoints for processing credit card payments in favor of registered commerces, " +
        "and for querying the transactions received by a commerce.")]
    public class HermesPayController : BaseApiController
    {
        [HttpGet("get-transactions/{commerceId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CommerceTransactionsResponseDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(
            Summary = "Get commerce transactions",
            Description = "Retrieves a paginated list of transactions received by a commerce through Hermes Pay."
        )]
        public async Task<IActionResult> GetTransactions(int commerceId, [FromQuery] GetCommerceTransactionsQuery query)
        {
            if (!TryResolveCommerceId(commerceId, out var resolvedCommerceId))
            {
                return Forbid();
            }

            query.CommerceId = resolvedCommerceId;
            var result = await Mediator.Send(query);
            return Ok(result);
        }

        [HttpPost("process-payment/{commerceId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(
            Summary = "Process a commerce payment",
            Description = "Processes a credit card payment in favor of a commerce, increasing the card's debt and crediting the amount to the commerce's principal savings account."
        )]
        public async Task<IActionResult> ProcessPayment(int commerceId, ProcessCommercePaymentCommand command)
        {
            if (!TryResolveCommerceId(commerceId, out var resolvedCommerceId))
            {
                return Forbid();
            }

            command.CommerceId = resolvedCommerceId;
            command.PerformedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            await Mediator.Send(command);
            return NoContent();
        }

        #region Private Methods
        private bool TryResolveCommerceId(int commerceIdFromRoute, out int resolvedCommerceId)
        {
            if (User.IsInRole("Commerce"))
            {
                var claim = User.FindFirstValue("commerceId");

                if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out resolvedCommerceId))
                {
                    resolvedCommerceId = 0;
                    return false;
                }

                return true;
            }

            resolvedCommerceId = commerceIdFromRoute;
            return true;
        }
        #endregion
    }
}
