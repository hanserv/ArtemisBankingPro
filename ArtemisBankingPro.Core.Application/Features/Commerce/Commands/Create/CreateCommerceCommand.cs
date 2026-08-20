using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.Commerce;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.Commerce.Commands.Create
{
    /// <summary>
    /// Parameters required to create a new commerce.
    /// </summary>
    public class CreateCommerceCommand : IRequest<CommerceCreatedResponseDto>
    {
        /// <example>Tienda Demo</example>
        [SwaggerParameter(Description = "Commercial name of the commerce.")]
        public required string Name { get; set; }

        /// <example>This is an example</example>
        [SwaggerParameter(Description = "General description of the commerce.")]
        public string? Description { get; set; }

        /// <example>contact@itlademo.com</example>
        [SwaggerParameter(Description = "Contact email of the commerce.")]
        public required string Email { get; set; }

        /// <example>8290091424</example>
        [SwaggerParameter(Description = "Phone number of the commerce.")]
        public required string PhoneNumber { get; set; }

        /// <example>101912396</example>
        [SwaggerParameter(Description = "Tax identifier (RNC) of the commerce.")]
        public required string Rnc { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string AdminId { get; set; } = string.Empty;
    }

    public class CreateCommerceCommandHandler : IRequestHandler<CreateCommerceCommand, CommerceCreatedResponseDto>
    {
        private readonly ICommerceRepository _commerceRepository;
        private readonly IMapper _mapper;

        public CreateCommerceCommandHandler(ICommerceRepository commerceRepository, IMapper mapper)
        {
            _commerceRepository = commerceRepository;
            _mapper = mapper;
        }

        public async Task<CommerceCreatedResponseDto> Handle(CreateCommerceCommand request, CancellationToken cancellationToken)
        {
            var rncExists = await _commerceRepository.GetAllQuery()
                .AnyAsync(c => c.Rnc == request.Rnc, cancellationToken);

            if (rncExists)
            {
                throw new ApiException("A commerce with the same RNC already exists.", (int)HttpStatusCode.Conflict);
            }

            var emailExists = await _commerceRepository.GetAllQuery()
                .AnyAsync(c => c.Email == request.Email, cancellationToken);

            if (emailExists)
            {
                throw new ApiException("A commerce with the same email already exists.", (int)HttpStatusCode.Conflict);
            }

            var commerce = new Domain.Entities.Commerce
            {
                Id = 0,
                Name = request.Name,
                Description = request.Description,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Rnc = request.Rnc,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedByAdminId = request.AdminId
            };

            await _commerceRepository.AddAsync(commerce);

            return _mapper.Map<CommerceCreatedResponseDto>(commerce);
        }
    }
}
