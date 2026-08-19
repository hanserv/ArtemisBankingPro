using System.Net;
using ArtemisBankingPro.Core.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.Api.Handlers
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var exceptionTitle = "An unexpected error occurred";
            var details = exception.Message;

            switch (exception)
            {
                case ApiException apiException:
                    switch (apiException.StatusCode)
                    {
                        case (int)HttpStatusCode.BadRequest:
                            exceptionTitle = "Bad Request";
                            httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            break;
                        case (int)HttpStatusCode.NotFound:
                            exceptionTitle = "Not Found";
                            httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                            break;
                        case (int)HttpStatusCode.Unauthorized:
                            exceptionTitle = "Unauthorized";
                            httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            break;
                        case (int)HttpStatusCode.Forbidden:
                            exceptionTitle = "Forbidden";
                            httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                            break;
                        default:
                            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            break;
                    }
                    break;

                case ArtemisBankingPro.Core.Application.Exceptions.ValidationException validationException:
                    exceptionTitle = "Bad Request";
                    details = string.Join(", ", validationException.Errors);
                    httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    break;

                case KeyNotFoundException:
                    exceptionTitle = "Not Found";
                    httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;

                case UnauthorizedAccessException:
                    exceptionTitle = "Forbidden";
                    httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    break;

                case ArgumentException:
                    exceptionTitle = "Bad Request";
                    httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    break;

                default:
                    httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    break;
            }

            if (httpContext.Response.StatusCode == (int)HttpStatusCode.InternalServerError)
            {
                _logger.LogError(exception, "Unhandled exception on {Path}.", httpContext.Request.Path);
            }
            else
            {
                _logger.LogWarning(exception, "Handled exception on {Path}: {Title}.", httpContext.Request.Path, exceptionTitle);
            }

            var problemDetails = new ProblemDetails
            {
                Title = exceptionTitle,
                Status = httpContext.Response.StatusCode,
                Detail = details,
                Instance = httpContext.Request.Path,
            };

            httpContext.Response.ContentType = "application/problem+json";

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);

            return true;
        }
    }
}
