using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PainelEstetica.Application.Common;

namespace PainelEstetica.WebAPI.Middlewares;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            if (exception is ApplicationExceptionBase or ArgumentException)
            {
                logger.LogWarning("Requisição rejeitada em {Path}: {Message}", context.Request.Path, exception.Message);
            }
            else
            {
                logger.LogError(exception, "Falha inesperada em {Path}", context.Request.Path);
            }

            await HandleExceptionAsync(context, exception, jsonOptions.Value.SerializerOptions);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception, JsonSerializerOptions serializerOptions)
    {
        var (status, title, detail) = exception switch
        {
            ResourceNotFoundException => (HttpStatusCode.NotFound, "Recurso não encontrado", exception.Message),
            ConflictException => (HttpStatusCode.Conflict, "Conflito", exception.Message),
            BusinessRuleException => (HttpStatusCode.UnprocessableEntity, "Regra de negócio não atendida", exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, "Solicitação inválida", exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Acesso não autorizado", "A sessão não possui uma identidade válida."),
            DbUpdateException => (HttpStatusCode.Conflict, "Conflito de persistência", "A operação conflita com dados já cadastrados."),
            _ => (HttpStatusCode.InternalServerError, "Erro interno no servidor", "Ocorreu um erro inesperado ao processar a requisição.")
        };

        context.Response.StatusCode = (int)status;
        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        return context.Response.WriteAsJsonAsync(problem, serializerOptions, "application/problem+json");
    }
}
