using Innovayse.Hostpanel.Application.Admin.Queries.GetDashboardStats;
using Innovayse.Hostpanel.Application.Admin.Queries.GetRecentActivity;
using Innovayse.Hostpanel.Application.Admin.Queries.GetServiceUsageReport;
using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Clients.Commands.ExportClientData;
using Innovayse.Hostpanel.Application.Clients.Queries.GetClientSummary;
using Innovayse.Hostpanel.Architecture.Tests.Enums;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// The allow-list for <see cref="ModuleTableOwnershipTests"/>: the read models that genuinely span
/// module tables (rules/architecture.md, "A module owns its tables"), and the few documented writers
/// marked <see cref="CrossModuleAccess.Writer"/>. Keep it small; an ordinary feature write or query
/// never belongs here, it goes through the owning module's command, query or a port. A writer is
/// listed only on an owner decision, with its reason here and at the point of exception.
/// </summary>
internal static class CrossModuleReadModels
{
    /// <summary>Every documented exception, each with its one-line reason.</summary>
    public static readonly IReadOnlyList<CrossModuleReadModel> All =
    [
        new("Innovayse.Hostpanel.Application.Reports.Queries", "Reports are read-only projections across billing, clients, services, domains and support by definition."),
        new(typeof(GetDashboardStatsHandler).FullName!, "The admin dashboard counts rows of every module on one screen."),
        new(typeof(GetRecentActivityHandler).FullName!, "The admin activity feed merges recent rows of several modules into one timeline."),
        new(typeof(GetServiceUsageReportHandler).FullName!, "A usage report is a read-only projection of services per product for the admin dashboard."),
        new(typeof(ExportClientDataHandler).FullName!, "A personal data export must gather every row a client owns, whichever module stores it."),
        new(typeof(GetClientSummaryHandler).FullName!, "The client overview shows totals from billing, services, domains, support and email in one read."),
        new(typeof(CampaignAudience).FullName!, "A campaign audience is a read-only projection of the opted-in clients and their services, the one place the count an admin sees and the recipients sending fixes are decided."),
        new(
            "Innovayse.Hostpanel.Application.Migration.Importers",
            "Bulk import of legacy data across modules; runs as one job with its own log.",
            CrossModuleAccess.Writer),
    ];
}
