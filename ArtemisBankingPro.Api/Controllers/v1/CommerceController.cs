using System.Security.Claims;
using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.Commerce;
using ArtemisBankingPro.Core.Application.Features.Commerce.Commands.ChangeStatus;
using ArtemisBankingPro.Core.Application.Features.Commerce.Commands.Create;
using ArtemisBankingPro.Core.Application.Features.Commerce.Commands.Update;
using ArtemisBankingPro.Core.Application.Features.Commerce.Queries.GetAll;
using ArtemisBankingPro.Core.Application.Features.Commerce.Queries.GetById;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Api.Controllers.v1
{
    [ApiVersion("1.0")]
    [Authorize(Roles = "Admin")]
    [SwaggerTag("Provides endpoints for managing commerces, including querying commerces, viewing commerce details, " +
        "creating new commerces, updating their information, and activating or deactivating existing commerces")]
    public class CommerceController : BaseApiController
    {
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<CommerceDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [SwaggerOperation(
            Summary = "Get all commerces",
            Description = "Retrieves a paginated list of registered commerces. By default, only active commerces are returned, ordered from most recent to oldest."
        )]
        public async Task<IActionResult> GetAll([FromQuery] GetAllCommercesQuery query)
        {
            var commerces = await Mediator.Send(query);
            return Ok(commerces);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CommerceDetailsDto))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(
            Summary = "Get commerce by id",
            Description = "Retrieves the detailed information of a specific commerce by its identifier."
        )]
        public async Task<IActionResult> GetById(int id)
        {
            var commerce = await Mediator.Send(new GetCommerceByIdQuery { Id = id });
            return Ok(commerce);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CommerceCreatedResponseDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [SwaggerOperation(
            Summary = "Create a new commerce",
            Description = "Registers a new commerce. The associated user with the Commerce role must be created separately from the User Management module."
        )]
        public async Task<IActionResult> Create(CreateCommerceCommand command)
        {
            command.AdminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var commerce = await Mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id = commerce.Id }, commerce);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [SwaggerOperation(
            Summary = "Update an existing commerce",
            Description = "Updates a commerce's data. This endpoint does not modify the commerce's status; use the dedicated status endpoint for that."
        )]
        public async Task<IActionResult> Update(int id, UpdateCommerceCommand command)
        {
            command.Id = id;
            await Mediator.Send(command);
            return NoContent();
        }

        [HttpPatch("{id}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(
            Summary = "Change commerce status",
            Description = "Activates or deactivates a commerce. Deactivating a commerce also deactivates its associated user; reactivating does not automatically reactivate that user."
        )]
        public async Task<IActionResult> ChangeStatus(int id, ChangeCommerceStatusCommand command)
        {
            command.Id = id;
            await Mediator.Send(command);
            return NoContent();
        }
    }
}
