using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Finance;
using PainelEstetica.Application.Security;
using PainelEstetica.Domain.Enums;
using PainelEstetica.WebAPI.Endpoints;

namespace PainelEstetica.WebAPI.Modules.Finance;

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this RouteGroupBuilder securedApi)
    {
        var finance = securedApi.MapGroup("/finance").WithTags("Finance");

        finance.MapGet("/overview", async (
            DateOnly? from,
            DateOnly? to,
            IFinanceService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetOverviewAsync(from, to, cancellationToken)))
            .RequireAuthorization(AppPermissions.FinanceRead);

        finance.MapGet("/entries", async (
            int? page,
            int? pageSize,
            string? search,
            DateOnly? from,
            DateOnly? to,
            IFinanceService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListEntriesAsync(
                new PageRequest(page ?? 1, pageSize ?? 25, search),
                from,
                to,
                cancellationToken)))
            .RequireAuthorization(AppPermissions.FinanceRead);

        finance.MapGet("/ledger", async (
            int? page,
            int? pageSize,
            string? search,
            DateOnly? from,
            DateOnly? to,
            FinancialLedgerSource? source,
            FinancialEntryType? type,
            FinancialEntryStatus? status,
            string? category,
            IFinanceService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListLedgerAsync(
                new PageRequest(page ?? 1, pageSize ?? 50, search),
                from,
                to,
                source,
                type,
                status,
                category,
                cancellationToken)))
            .RequireAuthorization(AppPermissions.FinanceRead);

        finance.MapPost("/entries", async (
            SaveFinancialEntryRequest request,
            HttpContext context,
            IFinanceService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var entry = await service.CreateEntryAsync(request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FinancialEntry.Created",
                "FinancialEntry",
                entry.Id.ToString(),
                details: new { entry.Type, entry.Status, entry.Category, entry.Amount, entry.EffectiveDate },
                cancellationToken: cancellationToken);
            return Results.Created($"/api/finance/entries/{entry.Id}", entry);
        })
            .AddEndpointFilter<ValidationFilter<SaveFinancialEntryRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.FinanceManage);

        finance.MapPut("/entries/{id:guid}", async (
            Guid id,
            SaveFinancialEntryRequest request,
            HttpContext context,
            IFinanceService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var entry = await service.UpdateEntryAsync(id, request, cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FinancialEntry.Updated",
                "FinancialEntry",
                id.ToString(),
                details: new { entry.Type, entry.Status, entry.Category, entry.Amount, entry.EffectiveDate },
                cancellationToken: cancellationToken);
            return Results.Ok(entry);
        })
            .AddEndpointFilter<ValidationFilter<SaveFinancialEntryRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.FinanceManage);

        finance.MapPost("/entries/{id:guid}/settle", async (
            Guid id,
            SettleFinancialEntryRequest request,
            HttpContext context,
            IFinanceService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var entry = await service.SettleEntryAsync(id, request, cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FinancialEntry.Settled",
                "FinancialEntry",
                id.ToString(),
                details: new { entry.Amount, entry.EffectiveDate },
                cancellationToken: cancellationToken);
            return Results.Ok(entry);
        })
            .AddEndpointFilter<ValidationFilter<SettleFinancialEntryRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.FinanceManage);

        finance.MapDelete("/entries/{id:guid}", async (
            Guid id,
            HttpContext context,
            IFinanceService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            await service.CancelEntryAsync(id, cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FinancialEntry.Cancelled",
                "FinancialEntry",
                id.ToString(),
                cancellationToken: cancellationToken);
            return Results.NoContent();
        })
            .Mutating()
            .RequireAuthorization(AppPermissions.FinanceManage);
    }

    private static Guid RequiredActorId(HttpContext context) =>
        context.ToAuditContext().ActorUserId
        ?? throw new UnauthorizedAccessException("A sessão não possui um usuário interno válido.");
}
