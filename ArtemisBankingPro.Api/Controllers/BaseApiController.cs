using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public abstract class BaseApiController : ControllerBase
    {
    }
}