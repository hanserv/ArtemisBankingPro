using System.Security.Claims;
using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.Features.CreditCard.Commands.Assign;
using ArtemisBankingPro.Core.Application.Features.CreditCard.Commands.Cancel;
using ArtemisBankingPro.Core.Application.Features.CreditCard.Commands.ModifyLimit;
using ArtemisBankingPro.Core.Application.Features.CreditCard.Queries.GetAll;
using ArtemisBankingPro.Core.Application.Features.CreditCard.Queries.GetById;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Api.Controllers.v1
{
    [ApiVersion("1.0")]
    [Authorize(Roles = "Admin")]
    [SwaggerTag("Provides endpoints for managing customer credit cards, including querying cards, assigning new cards to " +
        "active customers, viewing card transactions, updating credit limits, and canceling cards with no outstanding balance")]
    public class CreditCardController : BaseApiController
    {
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<CreditCardDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [SwaggerOperation(
            Summary = "Get all credit cards",
            Description = "Retrieves a paginated list of credit cards, defaulting to active cards ordered from most recent to oldest."
        )]
        public async Task<IActionResult> GetAll([FromQuery] GetAllCreditCardsQuery query)
        {
            var cards = await Mediator.Send(query);
            return Ok(cards);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CreditCardDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [SwaggerOperation(
            Summary = "Assign a new credit card",
            Description = "Assigns a new credit card to an active client, automatically generating the card number, expiration date, and CVC. The card is created in Active status with an initial debt of RD$0.00."
        )]
        public async Task<IActionResult> Assign(AssignCreditCardCommand command)
        {
            command.AdminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var card = await Mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id = card.Id }, card);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreditCardDetailsResponseDto))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(
            Summary = "Get credit card details",
            Description = "Retrieves the general information of a credit card along with its list of associated consumptions, ordered from most recent to oldest."
        )]
        public async Task<IActionResult> GetById(int id)
        {
            var card = await Mediator.Send(new GetCreditCardByIdQuery { Id = id });
            return Ok(card);
        }

        [HttpPatch("{id}/limit")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(
            Summary = "Modify a credit card's limit",
            Description = "Modifies the credit limit of an active credit card. The new limit cannot be lower than the card's current outstanding debt."
        )]
        public async Task<IActionResult> ModifyLimit(int id, ModifyCreditCardLimitCommand command)
        {
            command.CreditCardId = id;
            command.AdminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await Mediator.Send(command);
            return NoContent();
        }

        [HttpPatch("{id:int}/cancel")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(
            Summary = "Cancel a credit card",
            Description = "Cancels an active credit card, as long as it has no outstanding debt. A cancelled card cannot generate new consumptions, payments, or cash advances."
        )]
        public async Task<IActionResult> Cancel(int id)
        {
            await Mediator.Send(new CancelCreditCardCommand
            {
                CreditCardId = id,
                AdminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!
            });

            return NoContent();
        }
    }
}
