using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using Innovayse.Hostpanel.API.Controllers;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Every client-facing route that takes a service id off the URL refuses a service that belongs
/// to somebody else, answering 404 <c>SERVICE_NOT_FOUND</c>.
/// </summary>
/// <remarks>
/// Service ids are sequential, so an action that trusted the id would let any signed-in customer
/// walk them and reach another customer's hosting account. The rule is asserted by behaviour, over
/// HTTP against the real database and the real ownership rule, for each route listed in
/// <see cref="Routes"/>. The list is not trusted on its own: a reflection guard discovers every
/// <c>MyServicesController</c> action whose route takes the service id and fails unless the action
/// dispatches an <see cref="ICallerScopedServiceMessage"/> and the list names exactly the
/// discovered routes. <see cref="CallerScopedServiceMessageTests"/> then proves every such
/// message has a handler that takes the ownership rule.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class MyServicesRoutesAreOwnershipScopedTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route prefix every service-id action template starts with.</summary>
    private const string ServiceIdPrefix = "{id:int}/";

    /// <summary>The IL opcode of <c>newobj</c>, which is how an action builds the message it dispatches.</summary>
    private const byte NewObjOpCode = 0x73;

    /// <summary>The fixed key id the key-revocation route is called with.</summary>
    private const string FixedKeyId = "3f2a1c4e-8b7d-4e6a-9c5b-1d2e3f4a5b6c";

    /// <summary>Every route on <c>MyServicesController</c> that takes a service id, with the body it needs.</summary>
    /// <returns>One row per route: method, route template after the service id, concrete path after the service id, and a JSON body or <see langword="null"/>.</returns>
    public static TheoryData<string, string, string, object?> Routes() => new()
    {
        { "GET", "cpanel-sso", "cpanel-sso", null },
        { "POST", "cancel", "cancel", new { type = "Immediate", reason = "no longer needed" } },
        { "POST", "setup", "setup", new { domain = "example.com", username = "abcd", password = "Passw0rd!x" } },
        { "GET", "invoices", "invoices", null },
        { "GET", "cancellation-status", "cancellation-status", null },
        { "GET", "hosting-info", "hosting-info", null },
        { "POST", "change-password", "change-password", new { newPassword = "Passw0rd!x" } },
        { "GET", "ssh", "ssh", null },
        { "GET", "ssh/keys", "ssh/keys", null },
        { "POST", "ssh/keys", "ssh/keys", new { label = "laptop", publicKey = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIexample laptop" } },
        { "DELETE", "ssh/keys/{keyId:guid}", $"ssh/keys/{FixedKeyId}", null },
        { "GET", "plan-change-options", "plan-change-options", null },
        { "POST", "plan-change", "plan-change", new { toProductId = 1 } },
    };

    /// <summary>A request for another client's service id answers 404 <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <param name="method">The HTTP method of the route.</param>
    /// <param name="template">The route template after the service id; matched by the coverage guard.</param>
    /// <param name="suffix">The concrete path after the service id.</param>
    /// <param name="body">The JSON body the route binds, or <see langword="null"/> when it takes none.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task ARequestForAnotherClientsService_Returns404WithServiceNotFound(
        string method, string template, string suffix, object? body)
    {
        _ = template;
        var setup = factory.CreateClient();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setup, await factory.GetAdminTokenAsync());
        var stranger = await DomainsTestSeeds.RegisterStrangerAsync(factory, "my-services-ownership");
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/me/services/{serviceId}/{suffix}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await stranger.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>
    /// Every action whose route takes a service id dispatches a caller-scoped message, and the
    /// HTTP list above names exactly those routes, so a new route fails here until it is both
    /// checked and covered over HTTP.
    /// </summary>
    [Fact]
    public void EveryServiceIdRoute_DispatchesACallerScopedMessage_AndIsCoveredOverHttp()
    {
        var actions = typeof(MyServicesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().Select(a => (Action: m, Attribute: a)))
            .Where(x => x.Attribute.Template is not null && x.Attribute.Template.StartsWith(ServiceIdPrefix, StringComparison.Ordinal))
            .ToList();

        actions.Should().NotBeEmpty("the guard must discover the service-id routes or it tests nothing");

        var unscoped = actions
            .Where(x => !DispatchesCallerScopedMessage(x.Action))
            .Select(x => x.Action.Name)
            .ToList();
        unscoped.Should().BeEmpty("every service-id route must dispatch an ICallerScopedServiceMessage");

        var discovered = actions
            .Select(x => $"{x.Attribute.HttpMethods.Single()} {x.Attribute.Template![ServiceIdPrefix.Length..]}")
            .ToList();
        var listed = Routes().Select(row => $"{row[0]} {row[1]}").ToList();
        listed.Should().BeEquivalentTo(discovered, "the HTTP ownership test must cover exactly the discovered routes");
    }

    /// <summary>
    /// Whether an action dispatches a caller-scoped message: it binds one from the body, or its
    /// body constructs one.
    /// </summary>
    /// <param name="action">The controller action.</param>
    /// <returns><see langword="true"/> when the action carries a caller-scoped message.</returns>
    private static bool DispatchesCallerScopedMessage(MethodInfo action)
    {
        if (action.GetParameters().Any(p => typeof(ICallerScopedServiceMessage).IsAssignableFrom(p.ParameterType)))
        {
            return true;
        }

        return ConstructedTypes(BodyOf(action)).Any(t => typeof(ICallerScopedServiceMessage).IsAssignableFrom(t));
    }

    /// <summary>The method holding an action's code: the state machine's <c>MoveNext</c> for an async action.</summary>
    /// <param name="action">The controller action.</param>
    /// <returns>The method whose IL builds the dispatched message.</returns>
    private static MethodInfo BodyOf(MethodInfo action)
    {
        var stateMachine = action.GetCustomAttribute<AsyncStateMachineAttribute>();
        if (stateMachine is null)
        {
            return action;
        }

        return stateMachine.StateMachineType.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    /// <summary>
    /// The types a method constructs, read from the method-definition and member-reference tokens
    /// that follow each <c>newobj</c> opcode in its IL.
    /// </summary>
    /// <param name="method">The method to read.</param>
    /// <returns>The declaring types of the constructors the method calls.</returns>
    private static IEnumerable<Type> ConstructedTypes(MethodInfo method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        for (var i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != NewObjOpCode)
            {
                continue;
            }

            var token = BitConverter.ToInt32(il, i + 1);
            var table = token >>> 24;
            if (table is not (0x06 or 0x0A))
            {
                continue;
            }

            var type = TryResolveConstructorType(method, token);
            if (type is not null)
            {
                yield return type;
            }
        }
    }

    /// <summary>Resolves a constructor token to its declaring type, or <see langword="null"/> when the token is not a constructor.</summary>
    /// <param name="method">The method the token was read from.</param>
    /// <param name="token">The metadata token.</param>
    /// <returns>The constructed type, or <see langword="null"/>.</returns>
    private static Type? TryResolveConstructorType(MethodInfo method, int token)
    {
        try
        {
            return method.Module.ResolveMethod(
                token,
                method.DeclaringType?.GetGenericArguments(),
                null) is ConstructorInfo ctor ? ctor.DeclaringType : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
