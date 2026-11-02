using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary><c>BaseApiController.HandleStream</c>: a stream for a good outcome, the mapped refusal for a failed one or one with no stream.</summary>
public sealed class HandleStreamTests
{
    /// <summary>A controller that exposes <c>HandleStream</c>, over the real error message resolver.</summary>
    private sealed class StreamingController : BaseApiController
    {
        /// <summary>Initializes the controller with a request whose services hold the resolver.</summary>
        public StreamingController()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddLocalization();
            services.AddSingleton<IErrorMessageResolver, ErrorMessageResolver>();
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
            };
        }

        /// <summary>Runs <c>HandleStream</c>.</summary>
        /// <param name="result">The handler's outcome.</param>
        /// <returns>The response.</returns>
        public IActionResult Run(Result<StreamedReply> result)
        {
            return HandleStream(result);
        }
    }

    /// <summary>A reply with no events.</summary>
    /// <returns>The reply.</returns>
    private static StreamedReply Reply()
    {
        return new StreamedReply(NoEvents(), TimeSpan.FromSeconds(1));
    }

    /// <summary>An event sequence that ends at once; the test only checks which result wraps it.</summary>
    /// <returns>An empty sequence of events.</returns>
    private static async IAsyncEnumerable<StreamedEvent> NoEvents()
    {
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>A successful outcome with a stream is written as an event stream.</summary>
    [Fact]
    public void ASuccessfulStream_IsAnEventStreamResult()
    {
        new StreamingController().Run(Result<StreamedReply>.Success(Reply())).Should().BeOfType<EventStreamResult>();
    }

    /// <summary>A failed outcome is the mapped refusal with its own status, never a stream.</summary>
    [Fact]
    public void AFailedResult_IsTheMappedRefusal()
    {
        var response = new StreamingController().Run(Result.Failure<StreamedReply>("failed", HostpanelErrorCodes.PLUGIN_FAILED));

        response.Should().NotBeOfType<EventStreamResult>();
        response.Should().BeAssignableTo<IStatusCodeActionResult>().Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
    }

    /// <summary>A success that carries no stream is refused too, rather than failing on the missing value.</summary>
    [Fact]
    public void ASuccessWithNoStream_IsARefusal()
    {
        var response = new StreamingController().Run(Result<StreamedReply>.Success(null!));

        response.Should().NotBeOfType<EventStreamResult>();
        response.Should().BeAssignableTo<IStatusCodeActionResult>().Which.StatusCode.Should().BeGreaterThanOrEqualTo(StatusCodes.Status400BadRequest);
    }
}
