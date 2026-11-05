using LeaveDesk.Api.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace LeaveDesk.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (DomainException ex)
        {
            var (status, title) = Map(ex);
            _logger.LogWarning(ex, "Error de dominio manejado: {Title}", title);
            await WriteProblemAsync(context, status, title, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado");
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Error interno", "Ocurrio un error inesperado.");
        }
    }

    private static (int Status, string Title) Map(DomainException ex) => ex switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
        ConflictException => (StatusCodes.Status409Conflict, "Conflicto"),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Accion no permitida"),
        AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Autenticacion fallida"),
        BusinessRuleException => (StatusCodes.Status422UnprocessableEntity, "Regla de negocio violada"),
        _ => (StatusCodes.Status400BadRequest, "Solicitud invalida")
    };

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
