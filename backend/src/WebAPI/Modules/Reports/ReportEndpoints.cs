using System.Text;
using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Reports;
using PainelEstetica.Application.Security;
using PainelEstetica.WebAPI.Endpoints;

namespace PainelEstetica.WebAPI.Modules.Reports;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this RouteGroupBuilder securedApi)
    {
        var reports = securedApi.MapGroup("/reports").WithTags("Reports");

        reports.MapGet("/dashboard", async (IReportService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetDashboardAsync(cancellationToken)))
            .WithTags("Reports")
            .RequireAuthorization(AppPermissions.ReportRead);

        reports.MapPost("/custom", async (
            CustomReportRequest request,
            IReportService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.RunCustomReportAsync(request, exportLimit: null, cancellationToken)))
            .AddEndpointFilter<ValidationFilter<CustomReportRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.ReportRead);

        reports.MapPost("/custom/export", async (
            CustomReportRequest request,
            HttpContext context,
            IReportService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var report = await service.RunCustomReportAsync(request, exportLimit: 10_000, cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "Report.ExportedCsv",
                "CustomReport",
                details: new
                {
                    Fields = report.Columns.Select(column => column.Key).ToArray(),
                    report.TotalCount,
                    ExportedRows = report.Rows.Count,
                    request.FromDate,
                    request.ToDate,
                    request.ProcedureTypeId,
                    request.AppointmentStatuses,
                    request.PaymentStatuses,
                    request.BirthdayMonth
                },
                cancellationToken: cancellationToken);

            return Results.File(
                BuildCsv(report),
                "text/csv; charset=utf-8",
                $"relatorio-clinica-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        })
            .AddEndpointFilter<ValidationFilter<CustomReportRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.ReportRead);
    }

    private static byte[] BuildCsv(CustomReportResultDto report)
    {
        var csv = new StringBuilder("\uFEFF");
        csv.AppendLine(string.Join(';', report.Columns.Select(column => EscapeCsv(column.Label))));
        foreach (var row in report.Rows)
        {
            csv.AppendLine(string.Join(';', report.Columns.Select(column =>
                EscapeCsv(row.Values.GetValueOrDefault(column.Key) ?? string.Empty))));
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private static string EscapeCsv(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";
}
