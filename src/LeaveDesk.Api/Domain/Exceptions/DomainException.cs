namespace LeaveDesk.Api.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}

public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message) : base(message)
    {
    }
}

public sealed class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}

public sealed class AuthenticationFailedException : DomainException
{
    public AuthenticationFailedException(string message) : base(message)
    {
    }
}
