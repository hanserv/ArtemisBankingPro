using System.Net;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.Commerce.Commands.Update
{
    /// <summary>
    /// Parameters required to update an existing commerce.
    /// </summary>
    public class UpdateCommerceCommand : IRequest
    {
        public int Id { get; set; }

        /// <example>Tienda Demo Updated</example>
        [SwaggerParameter(Description = "Commercial name of the commerce.")]
        public required string Name { get; set; }

        /// <example>Comercio Updated</example>
        [SwaggerParameter(Description = "General description of the commerce.")]
        public string? Description { get; set; }

        /// <example>contactupdate@itlademo.com</example>
        [SwaggerParameter(Description = "Contact email of the commerce.")]
        public required string Email { get; set; }

        /// <example>8290091423</example>
        [SwaggerParameter(Description = "Phone number of the commerce.")]
        public required string PhoneNumber { get; set; }

        /// <example>101912390</example>
        [SwaggerParameter(Description = "Tax identifier (RNC) of the commerce.")]
        public required string Rnc { get; set; }
    }

    public class UpdateCommerceCommandHandler : IRequestHandler<UpdateCommerceCommand>
    {
        private readonly ICommerceRepository _commerceRepository;

        public UpdateCommerceCommandHandler(ICommerceRepository commerceRepository)
        {
            _commerceRepository = commerceRepository;
        }

        public async Task Handle(UpdateCommerceCommand request, CancellationToken cancellationToken)
        {
            var commerce = await _commerceRepository.GetByIdAsync(request.Id);

            if (commerce is null)
            {
                throw new ApiException("The selected commerce does not exist.", (int)HttpStatusCode.NotFound);
            }

            var rncBelongsToAnother = await _commerceRepository.GetAllQuery()
                .AnyAsync(c => c.Rnc == request.Rnc && c.Id != request.Id, cancellationToken);

            if (rncBelongsToAnother)
            {
                throw new ApiException("The RNC belongs to another commerce.", (int)HttpStatusCode.Conflict);
            }

            var emailBelongsToAnother = await _commerceRepository.GetAllQuery()
                .AnyAsync(c => c.Email == request.Email && c.Id != request.Id, cancellationToken);

            if (emailBelongsToAnother)
            {
                throw new ApiException("The email belongs to another commerce.", (int)HttpStatusCode.Conflict);
            }

            commerce.Name = request.Name;
            commerce.Description = request.Description;
            commerce.Email = request.Email;
            commerce.PhoneNumber = request.PhoneNumber;
            commerce.Rnc = request.Rnc;

            await _commerceRepository.UpdateAsync(commerce);
        }
    }
}
