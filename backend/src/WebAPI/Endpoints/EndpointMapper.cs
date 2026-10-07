using PainelEstetica.WebAPI.Modules.Appointments;
using PainelEstetica.WebAPI.Modules.Audit;
using PainelEstetica.WebAPI.Modules.Auth;
using PainelEstetica.WebAPI.Modules.Clients;
using PainelEstetica.WebAPI.Modules.Content;
using PainelEstetica.WebAPI.Modules.Finance;
using PainelEstetica.WebAPI.Modules.Forms;
using PainelEstetica.WebAPI.Modules.Leads;
using PainelEstetica.WebAPI.Modules.Integrations;
using PainelEstetica.WebAPI.Modules.Procedures;
using PainelEstetica.WebAPI.Modules.Reports;
using PainelEstetica.WebAPI.Modules.Settings;
using PainelEstetica.WebAPI.Modules.Terms;
using PainelEstetica.WebAPI.Modules.Users;

namespace PainelEstetica.WebAPI.Endpoints;

public static class EndpointMapper
{
    public static void MapEsteticaEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        var publicApi = api.MapGroup("/public").WithTags("Public");
        var securedApi = api.MapGroup(string.Empty).RequireAuthorization();

        api.MapAuthEndpoints();
        api.MapGoogleCalendarCallbackEndpoints();
        publicApi.MapPublicContentEndpoints();
        publicApi.MapPublicProcedureEndpoints();
        publicApi.MapPublicLeadEndpoints();
        publicApi.MapPublicPhotoEndpoints();

        securedApi.MapClientEndpoints();
        securedApi.MapFormEndpoints();
        securedApi.MapFormSubmissionEndpoints();
        securedApi.MapTermEndpoints();
        securedApi.MapAppointmentEndpoints();
        securedApi.MapGoogleCalendarEndpoints();
        securedApi.MapClinicSettingsEndpoints();
        securedApi.MapProcedureEndpoints();
        securedApi.MapLeadEndpoints();
        securedApi.MapContentEndpoints();
        securedApi.MapFinanceEndpoints();
        securedApi.MapReportEndpoints();
        securedApi.MapUserEndpoints();
        securedApi.MapAuditEndpoints();
    }
}
