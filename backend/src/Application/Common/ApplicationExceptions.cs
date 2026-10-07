namespace PainelEstetica.Application.Common;

public abstract class ApplicationExceptionBase(string message) : Exception(message);

public sealed class ResourceNotFoundException(string resource, object id)
    : ApplicationExceptionBase($"{resource} não encontrado: {id}.");

public sealed class BusinessRuleException(string message)
    : ApplicationExceptionBase(message);

public sealed class ConflictException(string message)
    : ApplicationExceptionBase(message);
