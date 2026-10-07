using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Identity;
using PainelEstetica.Application.Security;
using PainelEstetica.Infrastructure.Identity;
using PainelEstetica.WebAPI.Endpoints;

namespace PainelEstetica.WebAPI.Modules.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");

        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { token = tokens.RequestToken });
        });

        auth.MapPost("/login", LoginAsync)
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .RequireRateLimiting("auth")
            .Mutating();

        auth.MapPost("/login/mfa", LoginWithMfaAsync)
            .AddEndpointFilter<ValidationFilter<MfaLoginRequest>>()
            .RequireRateLimiting("auth")
            .Mutating();

        auth.MapPost("/login/recovery", LoginWithRecoveryCodeAsync)
            .AddEndpointFilter<ValidationFilter<RecoveryCodeLoginRequest>>()
            .RequireRateLimiting("auth")
            .Mutating();

        auth.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        auth.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .Mutating();

        auth.MapPost("/change-password", ChangePasswordAsync)
            .AddEndpointFilter<ValidationFilter<ChangePasswordRequest>>()
            .RequireAuthorization()
            .Mutating();

        auth.MapPost("/mfa/setup", SetupMfaAsync)
            .RequireAuthorization()
            .Mutating();

        auth.MapPost("/mfa/enable", EnableMfaAsync)
            .AddEndpointFilter<ValidationFilter<EnableMfaRequest>>()
            .RequireAuthorization()
            .Mutating();

        auth.MapPost("/mfa/recovery-codes", GenerateRecoveryCodesAsync)
            .RequireAuthorization()
            .Mutating();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IUserAccessService userAccessService,
        IAuditService auditService,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var user = await userManager.FindByNameAsync(username);
        if (user is null || !user.IsActive)
        {
            await AuditLoginAsync(auditService, httpContext, username, "Denied", cancellationToken);
            return Results.Unauthorized();
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            await AuditLoginAsync(auditService, httpContext, username, "LockedOut", cancellationToken);
            return Results.Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "Conta temporariamente bloqueada",
                detail: "Aguarde o período de bloqueio antes de tentar novamente.");
        }

        if (result.RequiresTwoFactor)
        {
            await AuditLoginAsync(auditService, httpContext, username, "MfaRequired", cancellationToken);
            return Results.Ok(new LoginResultDto("RequiresTwoFactor"));
        }

        if (!result.Succeeded)
        {
            await AuditLoginAsync(auditService, httpContext, username, "Denied", cancellationToken);
            return Results.Unauthorized();
        }

        user.LastLoginAtUtc = clock.UtcNow;
        await userManager.UpdateAsync(user);
        await AuditLoginAsync(auditService, httpContext, username, "Success", cancellationToken, user.Id);
        return Results.Ok(new LoginResultDto("Authenticated", await BuildCurrentUserAsync(user, userManager, userAccessService, cancellationToken)));
    }

    private static async Task<IResult> LoginWithMfaAsync(
        MfaLoginRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IUserAccessService userAccessService,
        IAuditService auditService,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var pendingUser = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (pendingUser is null || !pendingUser.IsActive)
        {
            return Results.Unauthorized();
        }

        var code = request.Code.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(
            code,
            isPersistent: false,
            rememberClient: request.RememberMachine);

        if (!result.Succeeded)
        {
            await AuditLoginAsync(auditService, httpContext, pendingUser.UserName ?? string.Empty, "MfaDenied", cancellationToken, pendingUser.Id);
            return result.IsLockedOut
                ? Results.Problem(statusCode: StatusCodes.Status423Locked, title: "Conta temporariamente bloqueada")
                : Results.Unauthorized();
        }

        pendingUser.LastLoginAtUtc = clock.UtcNow;
        await userManager.UpdateAsync(pendingUser);
        await AuditLoginAsync(auditService, httpContext, pendingUser.UserName ?? string.Empty, "Success", cancellationToken, pendingUser.Id);
        return Results.Ok(new LoginResultDto(
            "Authenticated",
            await BuildCurrentUserAsync(pendingUser, userManager, userAccessService, cancellationToken)));
    }

    private static async Task<IResult> LoginWithRecoveryCodeAsync(
        RecoveryCodeLoginRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IUserAccessService userAccessService,
        IAuditService auditService,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var pendingUser = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (pendingUser is null || !pendingUser.IsActive)
        {
            return Results.Unauthorized();
        }

        var recoveryCode = request.Code.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        var result = await signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);
        if (!result.Succeeded)
        {
            await AuditLoginAsync(
                auditService,
                httpContext,
                pendingUser.UserName ?? string.Empty,
                "RecoveryCodeDenied",
                cancellationToken,
                pendingUser.Id);
            return Results.Unauthorized();
        }

        pendingUser.LastLoginAtUtc = clock.UtcNow;
        await userManager.UpdateAsync(pendingUser);
        await AuditLoginAsync(
            auditService,
            httpContext,
            pendingUser.UserName ?? string.Empty,
            "RecoveryCodeSuccess",
            cancellationToken,
            pendingUser.Id);
        return Results.Ok(new LoginResultDto(
            "Authenticated",
            await BuildCurrentUserAsync(pendingUser, userManager, userAccessService, cancellationToken)));
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IUserAccessService userAccessService,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        return user is null || !user.IsActive
            ? Results.Unauthorized()
            : Results.Ok(await BuildCurrentUserAsync(user, userManager, userAccessService, cancellationToken));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        SignInManager<ApplicationUser> signInManager,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        await auditService.WriteAsync(
            context.ToAuditContext(),
            "Auth.Logout",
            "User",
            context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken: cancellationToken);
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null || !user.IsActive) return Results.Unauthorized();

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["newPassword"] = result.Errors.Select(error => error.Description).ToArray()
            });
        }

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);
        await auditService.WriteAsync(
            context.ToAuditContext(),
            "Auth.PasswordChanged",
            "User",
            user.Id.ToString(),
            cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> SetupMfaAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null || !user.IsActive) return Results.Unauthorized();
        if (user.MustChangePassword)
        {
            return Results.Conflict(new { message = "Troque a senha inicial antes de configurar o MFA." });
        }

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var issuer = UrlEncoder.Default.Encode("Clínica Estética");
        var account = UrlEncoder.Default.Encode(user.Email ?? user.UserName ?? user.Id.ToString());
        var uri = string.Format(
            CultureInfo.InvariantCulture,
            "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
            issuer,
            account,
            key);
        return Results.Ok(new MfaSetupDto(key!, uri));
    }

    private static async Task<IResult> EnableMfaAsync(
        EnableMfaRequest request,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null || !user.IsActive) return Results.Unauthorized();
        if (user.MustChangePassword)
        {
            return Results.Conflict(new { message = "Troque a senha inicial antes de configurar o MFA." });
        }

        var code = request.Code.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        if (!await userManager.VerifyTwoFactorTokenAsync(
                user,
                userManager.Options.Tokens.AuthenticatorTokenProvider,
                code))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["code"] = ["Código do autenticador inválido."]
            });
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8))?.ToArray()
                            ?? throw new InvalidOperationException("Não foi possível gerar códigos de recuperação.");
        await userManager.UpdateSecurityStampAsync(user);
        await signInManager.RefreshSignInAsync(user);
        await auditService.WriteAsync(
            context.ToAuditContext(),
            "Auth.MfaEnabled",
            "User",
            user.Id.ToString(),
            cancellationToken: cancellationToken);
        return Results.Ok(new MfaRecoveryCodesDto(recoveryCodes));
    }

    private static async Task<IResult> GenerateRecoveryCodesAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null || !user.IsActive) return Results.Unauthorized();
        if (!user.TwoFactorEnabled)
        {
            return Results.Conflict(new { message = "Ative o MFA antes de gerar códigos de recuperação." });
        }

        var recoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8))?.ToArray()
                            ?? throw new InvalidOperationException("Não foi possível gerar códigos de recuperação.");
        await userManager.UpdateSecurityStampAsync(user);
        await signInManager.RefreshSignInAsync(user);
        await auditService.WriteAsync(
            context.ToAuditContext(),
            "Auth.RecoveryCodesRegenerated",
            "User",
            user.Id.ToString(),
            cancellationToken: cancellationToken);
        return Results.Ok(new MfaRecoveryCodesDto(recoveryCodes));
    }

    private static async Task<CurrentUserDto> BuildCurrentUserAsync(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        IUserAccessService userAccessService,
        CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var permissions = await userAccessService.GetEffectivePermissionsAsync(user.Id, cancellationToken);
        var requiresMfa = !user.TwoFactorEnabled && roles.Any(AppRoles.MfaRequired.Contains);
        return new CurrentUserDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            roles.Order(StringComparer.Ordinal).ToArray(),
            permissions,
            user.MustChangePassword,
            user.TwoFactorEnabled,
            requiresMfa);
    }

    private static Task AuditLoginAsync(
        IAuditService auditService,
        HttpContext context,
        string username,
        string outcome,
        CancellationToken cancellationToken,
        Guid? userId = null) =>
        auditService.WriteAsync(
            new AuditContext(
                userId,
                username,
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString()),
            "Auth.Login",
            "User",
            userId?.ToString(),
            outcome,
            cancellationToken: cancellationToken);
}
