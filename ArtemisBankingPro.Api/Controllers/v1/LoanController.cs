using System.Security.Claims;
using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Application.Features.Loan.Commands.Asign;
using ArtemisBankingPro.Core.Application.Features.Loan.Commands.ModifyRate;
using ArtemisBankingPro.Core.Application.Features.Loan.Queries.GetAll;
using ArtemisBankingPro.Core.Application.Features.Loan.Queries.GetById;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Api.Controllers.v1
{
    [ApiVersion("1.0")]
    [Authorize(Roles = "Admin")]
    [SwaggerTag("Provides endpoints for managing customer loans, including querying loans, assigning new loans, " +
        "viewing loan details and amortization schedules, and updating the annual interest rate of active loans.")]
    public class LoanController : BaseApiController
    {
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<LoanDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [SwaggerOperation(
            Summary = "Get all loans",
            Description = "Retrieves a paginated list of all loans in the system."
        )]
        public async Task<IActionResult> GetAll(LoanFilterDto filter)
        {
            var loans = await Mediator.Send(new GetAllLoansQuery()
            {
                Page = filter.Page,
                PageSize = filter.PageSize,
                Identification = filter.Identification,
                Status = filter.Status
            });

            return Ok(loans);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(LoanCreatedResponseDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [SwaggerOperation(
            Summary = "Assign a new loan",
            Description = "Assigns a new loan to an active client, generates the amortization schedule and disburses the capital to the client's principal savings account."
        )]
        public async Task<IActionResult> Assign(AssignLoanCommand command)
        {
            command.AdminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var loan = await Mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id = loan.Id }, loan);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LoanDetailsDto))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(
            Summary = "Get loan details",
            Description = "Retrieves the details of a specific loan along with its amortization schedule."
        )]
        public async Task<IActionResult> GetById(int id)
        {
            var loan = await Mediator.Send(new GetLoanByIdQuery { Id = id });
            return Ok(loan);
        }

        [HttpPatch("{id:int}/rate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(
            Summary = "Modify loan interest rate",
            Description = "Updates the annual interest rate of an active loan and recalculates only the future pending installments."
        )]
        public async Task<IActionResult> ModifyRate(int id, ModifyLoanRateCommand command)
        {
            command.LoanId = id;
            command.AdminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            await Mediator.Send(command);
            return NoContent();
        }
    }
}
