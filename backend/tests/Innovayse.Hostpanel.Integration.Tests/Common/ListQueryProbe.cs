using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary>A bare list query the shared list validator is exercised on.</summary>
/// <param name="Page">The 1-based page.</param>
/// <param name="PageSize">The rows per page.</param>
/// <param name="Search">The search text.</param>
internal sealed record ListQueryProbe(int Page, int PageSize, string? Search = null) : IListQuery;
