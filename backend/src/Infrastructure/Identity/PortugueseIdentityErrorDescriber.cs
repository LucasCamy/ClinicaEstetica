using Microsoft.AspNetCore.Identity;

namespace PainelEstetica.Infrastructure.Identity;

/// <summary>
/// Mantém as mensagens que podem chegar do ASP.NET Identity alinhadas ao idioma do produto.
/// Os códigos originais são preservados para rastreabilidade e eventuais integrações.
/// </summary>
public sealed class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
{
    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };

    public override IdentityError DefaultError() => Error(nameof(DefaultError), "Não foi possível concluir a operação de acesso.");
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "Os dados foram alterados por outra operação. Atualize a tela e tente novamente.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "A senha atual está incorreta.");
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "O código ou token informado é inválido ou expirou.");
    public override IdentityError RecoveryCodeRedemptionFailed() => Error(nameof(RecoveryCodeRedemptionFailed), "O código de recuperação é inválido ou já foi utilizado.");
    public override IdentityError LoginAlreadyAssociated() => Error(nameof(LoginAlreadyAssociated), "Este método de acesso já está vinculado a uma conta.");
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), "O nome de usuário contém caracteres não permitidos. Use letras, números ou - . _ @ +, sem espaços.");
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), "O e-mail informado não é válido.");
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), "Este nome de usuário já está em uso.");
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), "Este e-mail já está em uso.");
    public override IdentityError InvalidRoleName(string? role) => Error(nameof(InvalidRoleName), "O papel de acesso informado é inválido.");
    public override IdentityError DuplicateRoleName(string role) => Error(nameof(DuplicateRoleName), "Este papel de acesso já existe.");
    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword), "Esta conta já possui uma senha definida.");
    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled), "O bloqueio de acesso não está habilitado para esta conta.");
    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole), "Esta conta já possui o papel de acesso informado.");
    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole), "Esta conta não possui o papel de acesso informado.");
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"A senha deve ter pelo menos {length} caracteres.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"A senha deve usar ao menos {uniqueChars} caracteres diferentes.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "A senha deve incluir ao menos um símbolo.");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "A senha deve incluir ao menos um número.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "A senha deve incluir ao menos uma letra minúscula.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "A senha deve incluir ao menos uma letra maiúscula.");
}
