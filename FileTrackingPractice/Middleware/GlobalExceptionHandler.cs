using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FileTrackingPractice.Exceptions;

namespace FileTrackingPractice.Middleware
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;


        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception ex,
            CancellationToken cancelToken)
        {
            if (ex is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "Request was cancelled by the client: {Method} {Path}",
                    httpContext.Request.Method,
                    httpContext.Request.Path);

                return true;
            }

            _logger.LogError(
                ex,
                "An unhandled exception occured while processing {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);

            var problemDetails = CreateProblemDetails(httpContext, ex);

            httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancelToken);

            return true;
        }

        private ProblemDetails CreateProblemDetails(HttpContext httpContext, Exception ex)
        {
            var problemDetails = ex switch
            {
                FileScanConfigurationException => new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Scan folder configuration error",
                    Detail = ex.Message
                },

                InvalidOperationException => new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Invalid operation",
                    Detail = "System is not suitable for this operation"
                },

                DirectoryNotFoundException => new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Scan folder not found",
                    Detail = ex.Message
                },

                UnauthorizedAccessException => new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Scan folder access error",
                    Detail = "Application does not have permission to access the scan folder"
                },

                IOException => new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "File system error",
                    Detail = "An error occured while accessing the file system"
                },

                DbUpdateException => new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Database error",
                    Detail = "An error occured while saving file records"
                },

                OutOfMemoryException => new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Insufficient system resources",
                    Detail = "System does not have enough memory"
                },

                _ => new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal server error",
                    Detail = "An unexpected error occured",
                }
            };

            problemDetails.Instance = httpContext.Request.Path;
            problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

            return problemDetails;
        }
    }
}
