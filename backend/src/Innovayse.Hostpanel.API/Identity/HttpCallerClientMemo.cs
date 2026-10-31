using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Innovayse.Hostpanel.API.Identity;

/// <summary>
/// Keeps the caller's resolved client account in <see cref="HttpContext.Items"/>, which every scope of the same
/// request shares and which ends with the request.
/// </summary>
public sealed class HttpCallerClientMemo : ICallerClientMemo
{
    /// <summary>The <see cref="HttpContext.Items"/> key; a private object so nothing else can read or overwrite it.</summary>
    private static readonly object ItemKey = new();

    /// <summary>Source of the current request.</summary>
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Initializes the memo.</summary>
    /// <param name="httpContextAccessor">Source of the current request.</param>
    public HttpCallerClientMemo(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public bool TryGet(out CallerClientAccess access)
    {
        if (_httpContextAccessor.HttpContext?.Items.TryGetValue(ItemKey, out var kept) == true && kept is CallerClientAccess found)
        {
            access = found;
            return true;
        }

        access = CallerClientAccess.None;
        return false;
    }

    /// <inheritdoc/>
    public void Set(CallerClientAccess access)
    {
        if (_httpContextAccessor.HttpContext is { } context)
        {
            context.Items[ItemKey] = access;
        }
    }

    /// <inheritdoc/>
    public void Forget()
    {
        _httpContextAccessor.HttpContext?.Items.Remove(ItemKey);
    }
}
