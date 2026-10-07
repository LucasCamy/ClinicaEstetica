using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Leads;
using PainelEstetica.Application.Security;
using PainelEstetica.WebAPI.Endpoints;

namespace PainelEstetica.WebAPI.Modules.Leads;

public static class LeadEndpoints
{
    public static void MapPublicLeadEndpoints(this RouteGroupBuilder publicApi)
    {
        publicApi.MapPost("/leads", async (
            CreateLeadRequest request,
            ILeadService service,
            CancellationToken cancellationToken) =>
        {
            await service.CreatePublicAsync(request, cancellationToken);
            return Results.Accepted(value: new { message = "Recebemos seu contato e retornaremos em breve." });
        })
            .AddEndpointFilter<ValidationFilter<CreateLeadRequest>>()
            .RequireRateLimiting("public-leads");
    }

    public static void MapLeadEndpoints(this RouteGroupBuilder securedApi)
    {
        securedApi.MapGet("/leads", async (
            int? page,
            int? pageSize,
            string? search,
            string? status,
            ILeadService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(new PageRequest(page ?? 1, pageSize ?? 25, search), status, cancellationToken)))
            .WithTags("Leads")
            .RequireAuthorization(AppPermissions.LeadRead);

        securedApi.MapPut("/leads/{id:guid}/status", async (
            Guid id,
            UpdateLeadStatusRequest request,
            HttpContext context,
            ILeadService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var lead = await service.UpdateStatusAsync(id, request, cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "Lead.StatusUpdated",
                "Lead",
                id.ToString(),
                details: new { lead.Status },
                cancellationToken: cancellationToken);
            return Results.Ok(lead);
        })
            .WithTags("Leads")
            .AddEndpointFilter<ValidationFilter<UpdateLeadStatusRequest>>()
            .RequireAuthorization(AppPermissions.LeadManage)
            .Mutating();
    }
}
