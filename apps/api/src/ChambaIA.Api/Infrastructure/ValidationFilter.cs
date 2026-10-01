using System.ComponentModel.DataAnnotations;

namespace ChambaIA.Api.Infrastructure;

/// <summary>Validates the bound request body with DataAnnotations / IValidatableObject and answers 400 ProblemDetails.</summary>
public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["El cuerpo de la solicitud es obligatorio."] });

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
        {
            var errors = results
                .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : [""]).Select(m => (Member: m, Message: r.ErrorMessage ?? "Valor inválido.")))
                .GroupBy(x => ToCamel(x.Member))
                .ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());
            return TypedResults.ValidationProblem(errors);
        }

        return await next(context);
    }

    private static string ToCamel(string member) =>
        member.Length == 0 ? "body" : char.ToLowerInvariant(member[0]) + member[1..];
}

public static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>();
}
