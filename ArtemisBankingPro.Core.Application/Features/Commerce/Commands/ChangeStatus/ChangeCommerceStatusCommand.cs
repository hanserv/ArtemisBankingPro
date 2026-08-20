
using System.Net;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MediatR;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.Commerce.Commands.ChangeStatus
{
    /// <summary>
    /// Parameters required to change a commerce's status.
    /// </summary>
    public class ChangeCommerceStatusCommand : IRequest
    {
        public int Id { get; set; }

        /// <example>true</example>
        [SwaggerParameter(Description = "New status of the commerce. true for active, false for inactive.")]
        public required bool Status { get; set; }
    }

    public class ChangeCommerceStatusCommandHandler : IRequestHandler<ChangeCommerceStatusCommand>
    {
        private readonly ICommerceRepository _commerceRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;

        public ChangeCommerceStatusCommandHandler(ICommerceRepository commerceRepository, IBasicUserInfoService basicUserInfoService)
        {
            _commerceRepository = commerceRepository;
            _basicUserInfoService = basicUserInfoService;
        }

        public async Task Handle(ChangeCommerceStatusCommand request, CancellationToken cancellationToken)
        {
            var commerce = await _commerceRepository.GetByIdAsync(request.Id);

            if (commerce is null)
            {
                throw new ApiException("The selected commerce does not exist.", (int)HttpStatusCode.NotFound);
            }

            commerce.IsActive = request.Status;
            await _commerceRepository.UpdateAsync(commerce);

            if (!request.Status && !string.IsNullOrWhiteSpace(commerce.AssociatedUserId))
            {
                await _basicUserInfoService.DeactivateUserAsync(commerce.AssociatedUserId);
            }
        }
    }
}
