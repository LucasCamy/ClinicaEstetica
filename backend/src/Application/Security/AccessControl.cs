namespace PainelEstetica.Application.Security;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Professional = "Professional";
    public const string Receptionist = "Receptionist";

    public static readonly string[] All = [Admin, Professional, Receptionist];
    public static readonly string[] MfaRequired = [Admin, Professional];
}

public static class AppPermissions
{
    public const string DashboardRead = "dashboard.read";
    public const string ClientRead = "clients.read";
    public const string ClientManage = "clients.manage";
    public const string ClinicalRead = "clinical.read";
    public const string ClinicalManage = "clinical.manage";
    public const string FormRead = "forms.read";
    public const string FormManage = "forms.manage";
    public const string AppointmentRead = "appointments.read";
    public const string AppointmentManage = "appointments.manage";
    public const string ProcedureRead = "procedures.read";
    public const string ProcedureManage = "procedures.manage";
    public const string LeadRead = "leads.read";
    public const string LeadManage = "leads.manage";
    public const string CmsManage = "cms.manage";
    public const string ReportRead = "reports.read";
    public const string FinanceRead = "finance.read";
    public const string FinanceManage = "finance.manage";
    public const string UserManage = "users.manage";
    public const string AuditRead = "audit.read";
    public const string SettingsRead = "settings.read";
    public const string SettingsManage = "settings.manage";

    public static readonly string[] All =
    [
        DashboardRead,
        ClientRead,
        ClientManage,
        ClinicalRead,
        ClinicalManage,
        FormRead,
        FormManage,
        AppointmentRead,
        AppointmentManage,
        ProcedureRead,
        ProcedureManage,
        LeadRead,
        LeadManage,
        CmsManage,
        ReportRead,
        FinanceRead,
        FinanceManage,
        UserManage,
        AuditRead,
        SettingsRead,
        SettingsManage
    ];
}

public static class RolePermissionCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Defaults =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [AppRoles.Admin] = AppPermissions.All.ToHashSet(StringComparer.Ordinal),
            [AppRoles.Professional] = new HashSet<string>(StringComparer.Ordinal)
            {
                AppPermissions.DashboardRead,
                AppPermissions.ClientRead,
                AppPermissions.ClientManage,
                AppPermissions.ClinicalRead,
                AppPermissions.ClinicalManage,
                AppPermissions.FormRead,
                AppPermissions.FormManage,
                AppPermissions.AppointmentRead,
                AppPermissions.AppointmentManage,
                AppPermissions.ProcedureRead,
                AppPermissions.LeadRead,
                AppPermissions.ReportRead,
                AppPermissions.FinanceRead,
                AppPermissions.FinanceManage,
                AppPermissions.SettingsRead,
                AppPermissions.SettingsManage
            },
            [AppRoles.Receptionist] = new HashSet<string>(StringComparer.Ordinal)
            {
                AppPermissions.DashboardRead,
                AppPermissions.ClientRead,
                AppPermissions.ClientManage,
                AppPermissions.AppointmentRead,
                AppPermissions.AppointmentManage,
                AppPermissions.ProcedureRead,
                AppPermissions.LeadRead,
                AppPermissions.LeadManage
            }
        };

    public static bool IsGrantedByDefault(IEnumerable<string> roles, string permission) =>
        roles.Any(role => Defaults.TryGetValue(role, out var permissions) && permissions.Contains(permission));

    public static IReadOnlyCollection<string> ForRoles(IEnumerable<string> roles) =>
        roles
            .Where(Defaults.ContainsKey)
            .SelectMany(role => Defaults[role])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
