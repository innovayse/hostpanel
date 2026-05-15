namespace Innovayse.Application.Tests.Common;

using Innovayse.Application.Common;
using Xunit;

/// <summary>Verifies that <see cref="IUnitOfWork"/> contract is accessible and well-formed.</summary>
public class IUnitOfWorkContractTests
{
    /// <summary><see cref="IUnitOfWork.SaveChangesAsync"/> must exist and return <see cref="Task{T}"/> of int.</summary>
    [Fact]
    public void IUnitOfWork_SaveChangesAsync_ShouldExistWithCorrectSignature()
    {
        var method = typeof(IUnitOfWork).GetMethod(nameof(IUnitOfWork.SaveChangesAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<int>), method!.ReturnType);
    }
}
