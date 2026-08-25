namespace Innovayse.Application.Common;

/// <summary>
/// Thrown by <see cref="IUnitOfWork.SaveChangesAsync"/> when an aggregate was modified by
/// another writer between the time this handler read it and the time it tried to save.
/// Persistence-agnostic on purpose — the Application layer must not reference EF Core
/// (see <c>rules/architectures/clean-architecture.md</c>, "Application Layer Rules"), so
/// Infrastructure's <see cref="IUnitOfWork"/> implementation is responsible for catching the
/// real ORM-specific concurrency exception and translating it to this type.
/// </summary>
/// <param name="message">A human-readable description of the conflict.</param>
/// <param name="innerException">The original ORM-specific exception, if any.</param>
public sealed class ConcurrencyConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);
