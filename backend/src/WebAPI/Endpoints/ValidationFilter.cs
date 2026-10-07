using System.ComponentModel.DataAnnotations;

namespace PainelEstetica.WebAPI.Endpoints;

public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["body"] = ["O corpo da requisição é obrigatório."]
            });
        }

        var validationResults = new List<ValidationResult>();
        if (Validator.TryValidateObject(model, new ValidationContext(model), validationResults, validateAllProperties: true))
        {
            return await next(context);
        }

        var errors = validationResults
            .SelectMany(result => result.MemberNames.DefaultIfEmpty("body")
                .Select(member => new { member, message = result.ErrorMessage ?? "Valor inválido." }))
            .GroupBy(error => error.member, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.message).Distinct().ToArray(),
                StringComparer.OrdinalIgnoreCase);
        return Results.ValidationProblem(errors);
    }
}
