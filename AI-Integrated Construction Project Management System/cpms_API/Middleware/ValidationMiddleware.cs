using FluentValidation;

namespace cpms_API.Middleware
{
    public class ValidationMiddleware
    {
        private readonly RequestDelegate _next;
        public ValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ValidationException ex)
            {
                await HandleValidationExceptionAsync(context, ex);
            }
        }

        private Task HandleValidationExceptionAsync(HttpContext context, ValidationException exception)
        {
            // Set the response status code to 400 (Bad Request)
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            // Create a custom error response object
            var response = new
            {
                statusCode = 400,
                isSuccess = false,
                errorMessage = "Validation failed",
                result = exception.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray())
            };

            // Serialize and return the custom error response
            return context.Response.WriteAsJsonAsync(response);
        }
    }
}
