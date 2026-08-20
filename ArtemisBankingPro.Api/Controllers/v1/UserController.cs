using System.Security.Claims;
using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Interfaces;
using Asp.Versioning;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Api.Controllers.v1
{
    [ApiVersion("1.0")]
    [Authorize(Roles = "Admin")]
    [SwaggerTag("Provides endpoints for user CRUD operations and change status")]
    public class UserController : BaseApiController
    {
        private readonly IAccountServiceForApi _accountService;
        private readonly IMapper _mapper;

        public UserController(IAccountServiceForApi accountService, IMapper mapper)
        {
            _accountService = accountService;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<UserDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [SwaggerOperation(
            Summary = "Get all users",
            Description = "Retrieves a list of all users, with optional filtering and pagination"
        )]
        public async Task<IActionResult> GetAllUsers([FromQuery] UserFilterDto filter)
        {
            var result = await _accountService.GetAllUsersAsync(filter);

            if (!result.IsSuccess)
            {
                throw new ApiException(result.Error!, result.StatusCode);
            }

            return Ok(result.Value);
        }

        [HttpGet("commerce")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<UserCommerceDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [SwaggerOperation(
            Summary = "Get all commerce users",
            Description = "Retrieves a list of all commerce users, with optional pagination"
        )]
        public async Task<IActionResult> GetCommerceUsers([FromQuery] CommerceUserFilterDto filter)
        {
            return Ok(await _accountService.GetCommerceUsersAsync(filter));
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(RegisterResponseDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [SwaggerOperation(
            Summary = "Create a new user",
            Description = "Creates a new user with the specified details and role"
        )]
        public async Task<IActionResult> Create([FromBody] CreateUserApiDto dto)
        {
            var registerDto = _mapper.Map<RegisterDto>(dto);

            var result = await _accountService.RegisterUserAsync(registerDto, dto.Role.ToString(), isApi: true);

            if (!result.IsSuccess)
            {
                throw new ApiException(result.Error!, result.StatusCode);
            }

            return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
        }

        [HttpPost("commerce/{commerceId}")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CommerceUserApiResponseDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [SwaggerOperation(
            Summary = "Create a new commerce user",
            Description = "Creates a new commerce user with the specified details"
        )]
        public async Task<IActionResult> CreateCommerce([FromRoute] int commerceId, [FromBody] RegisterDto dto)
        {
            var result = await _accountService.RegisterCommerceUserAsync(dto, commerceId);
            return CreatedAtAction(nameof(GetById), new { id = result!.Id }, result);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [SwaggerOperation(
            Summary = "Modifies the data of an existing user",
            Description = "Modifies the data of an existing user except for the user type"
        )]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateUserDto dto)
        {
            dto.Id = id;
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _accountService.EditUserAsync(dto, currentUserId);

            if (!result.IsSuccess)
            {
                throw new ApiException(result.Error!, result.StatusCode);
            }

            return NoContent();
        }

        [HttpPatch("{id}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(
            Summary = "Changes the status of a user",
            Description = "Changes the status of a user"
        )]
        public async Task<IActionResult> ChangeStatus(string id, [FromBody] bool status)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _accountService.ChangeUserStatusAsync(id, status, currentUserId);

            if (!result.IsSuccess)
            {
                throw new ApiException(result.Error!, result.StatusCode);
            }

            return NoContent();
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [SwaggerOperation(
            Summary = "Get user details by Id",
            Description = "Retrieves a user by their unique identifier"
        )]
        public async Task<IActionResult> GetById(string id)
        {
            return Ok(await _accountService.GetUserDetailByIdAsync(id));
        }
    }
}
