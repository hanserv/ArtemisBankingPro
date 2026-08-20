using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Interfaces;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Api.Controllers.v1
{
    [ApiVersion("1.0")]
    [SwaggerTag("Endpoints for user authentication, account confirmation and recovery")]
    public class AccountController : BaseApiController
    {
        private readonly IAccountServiceForApi _accountService;

        public AccountController(IAccountServiceForApi accountService)
        {
            _accountService = accountService;
        }

        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LoginResponseForApiDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [SwaggerOperation(
            Summary = "Authenticate user",
            Description = "Validates user credentials and returns an authentication token with user information"
        )]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            return Ok(await _accountService.AuthenticateAsync(dto));
        }

        [HttpPost("confirm")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(
            Summary = "Confirm user account",
            Description = "Validates the confirmation token and activates the associated user account"
        )]
        public async Task<IActionResult> ConfirmAccount([FromBody] ConfirmAccountDto dto)
        {
            await _accountService.ConfirmAccountApiAsync(dto);
            return NoContent();
        }

        [HttpPost("get-reset-token")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(
            Summary = "Get password reset token",
            Description = "Generates a password reset token for a user with role admin or commerce"
        )]
        public async Task<IActionResult> GetResetToken([FromBody] RequestPasswordResetDto dto)
        {
            var result = await _accountService.RequestPasswordResetAsync(dto,"",isApi:true);

            if(!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            return NoContent();
        }

        [HttpPost("reset-password")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(
            Summary = "Reset user password",
            Description = "Allows a user to change their password using the reset token received via email"
        )]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var result = await _accountService.ResetPasswordAsync(dto);

            if(!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            return NoContent();
        }
    }
}
