using cpms_Application.CustomExceptions;
using cpms_Application.Response;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace cpms_API.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await WriteAsync(context, HttpStatusCode.Conflict, "The record changed while it was being updated. Reload and retry.", ex);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                await WriteAsync(
                    context,
                    HttpStatusCode.Conflict,
                    "A record with the same unique key already exists.",
                    ex,
                    DatabaseError("DATABASE_DUPLICATE_KEY", retryable: false));
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
            {
                await WriteAsync(
                    context,
                    HttpStatusCode.BadRequest,
                    "The operation violates a database relationship or quantity constraint.",
                    ex,
                    DatabaseError("DATABASE_CONSTRAINT_VIOLATION", retryable: false));
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 208 })
            {
                await WriteAsync(
                    context,
                    HttpStatusCode.ServiceUnavailable,
                    "The database schema is not ready. Run the latest backend migrations.",
                    ex,
                    DatabaseError("DATABASE_SCHEMA_NOT_READY", retryable: false));
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlException && IsDatabaseUnavailable(sqlException))
            {
                await WriteAsync(
                    context,
                    HttpStatusCode.ServiceUnavailable,
                    "The database is temporarily unavailable.",
                    ex,
                    DatabaseError("DATABASE_UNAVAILABLE", retryable: true));
            }
            catch (DbUpdateException ex)
            {
                await WriteAsync(
                    context,
                    HttpStatusCode.InternalServerError,
                    "A database error occurred.",
                    ex,
                    DatabaseError("DATABASE_ERROR", retryable: true));
            }
            catch (NotFoundException ex)
            {
                await WriteAsync(context, HttpStatusCode.NotFound, ex.Message, ex);
            }
            catch (NotMatchException ex)
            {
                await WriteAsync(context, HttpStatusCode.Conflict, ex.Message, ex);
            }
            catch (ConflictExceptions ex)
            {
                await WriteAsync(context, HttpStatusCode.Conflict, ex.Message, ex);
            }
            catch (Exception ex)
            {
                await WriteAsync(context, HttpStatusCode.InternalServerError, "An unexpected server error occurred.", ex);
            }
        }

        private static bool IsDatabaseUnavailable(SqlException exception) =>
            exception.Number is -1 or 2 or 53 or 4060 or 18456;

        private static object DatabaseError(string code, bool retryable) => new
        {
            errorCode = code,
            retryable
        };

        private async Task WriteAsync(HttpContext context, HttpStatusCode status, string message, Exception exception, object? result = null)
        {
            _logger.LogError(exception, "Request failed with status {StatusCode}", (int)status);
            if (context.Response.HasStarted) throw exception;
            context.Response.Clear();
            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ApiResponse().SetApiResponse(status, false, message, result));
        }
    }
}
