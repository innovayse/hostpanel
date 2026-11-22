namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// The one moment every demo date is measured from, and the one random seed every demo figure is drawn from.
/// Seeding captures the anchor once and hands it to each seeder, so a row's date is "anchor minus a fixed
/// number of days" and a figure is the n-th draw of a fixed-seed generator: the picture is identical after
/// every reset, only shifted forward to today, so the charts always show the last twelve months and the
/// dashboard always shows customers joining, invoices being paid and orders arriving in the last few weeks.
/// </summary>
internal static class DemoAnchor
{
    /// <summary>The seed of every generator the demo seeders create; never changes, so figures repeat.</summary>
    internal const int RandomSeed = 20260101;

    /// <summary>Months of history the billing charts are filled for, the current month included.</summary>
    internal const int HistoryMonths = 12;

    /// <summary>Every n-th customer (by position) joined in the last few weeks instead of long ago.</summary>
    private const int RecentSignupEvery = 4;

    /// <summary>The remainder of the position that marks a recent signup, so the demo client (position zero) is never one.</summary>
    private const int RecentSignupRemainder = 3;

    /// <summary>Days between a recent customer's signup and the first invoice.</summary>
    private const int RecentFirstInvoiceLagDays = 3;

    /// <summary>Days between an older customer's signup and the first invoice, before the per-customer spread.</summary>
    private const int FirstInvoiceLagDays = 21;

    /// <summary>
    /// Width in days of the spread of billing days over the cycle: each older customer is billed on its own day of the
    /// month, so payments land on many different days instead of one cluster.
    /// </summary>
    private const int BillingDaySpread = 21;

    /// <summary>
    /// How many days ago each recent customer signed up, in position order. Denser towards today, so signups and the
    /// invoices that follow them show a gentle growth. No value puts a monthly invoice within a day or two of the anchor
    /// or of the point where one more invoice would fall due (the first invoice is three days after signup, the next
    /// ones a month apart), so the count of invoices, and the stage each one is in, is the same whatever day the demo
    /// is reset on.
    /// </summary>
    private static readonly int[] RecentSignupAges = [3, 4, 5, 7, 9, 12, 15, 18, 22, 27, 36, 39, 43, 56, 67, 72];

    /// <summary>Reads the anchor: midnight UTC at the start of the current day.</summary>
    /// <param name="now">The current moment, from the clock.</param>
    /// <returns>Midnight UTC today.</returns>
    internal static DateTimeOffset Today(DateTimeOffset now)
    {
        return new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
    }

    /// <summary>Whether a customer joined in the last few weeks, which gives the dashboard new customers every day.</summary>
    /// <param name="position">The customer's position in the id order, the demo client first.</param>
    /// <returns>True for a recent signup.</returns>
    internal static bool IsRecentSignup(int position) => position % RecentSignupEvery == RecentSignupRemainder;

    /// <summary>The first month, counted back from the current one, an older customer is billed in; later joiners have shorter histories.</summary>
    /// <param name="position">The customer's position in the id order, the demo client first.</param>
    /// <returns>Months back, at most <see cref="HistoryMonths"/> minus one.</returns>
    internal static int FirstBillingMonthBack(int position) =>
        position == 0 ? HistoryMonths - 1 : Math.Min(HistoryMonths - 1, 3 + ((position * 5) % 9));

    /// <summary>The day of the billing cycle an older customer is invoiced on, as days before the same day of the month.</summary>
    /// <param name="position">The customer's position in the id order, the demo client first.</param>
    /// <returns>A number of days from zero to <see cref="BillingDaySpread"/> minus one; zero for the demo client.</returns>
    private static int BillingDayOffset(int position) => (position * 11) % BillingDaySpread;

    /// <summary>How many days ago a recent customer signed up.</summary>
    /// <param name="position">The position of a recent signup (see <see cref="IsRecentSignup"/>).</param>
    /// <returns>Days before the anchor.</returns>
    private static int RecentSignupAge(int position) => RecentSignupAges[(position / RecentSignupEvery) % RecentSignupAges.Length];

    /// <summary>The day an older customer is invoiced in the month a number of months before the anchor's.</summary>
    /// <param name="position">The customer's position in the id order, the demo client first.</param>
    /// <param name="today">The anchor.</param>
    /// <param name="monthsBack">How many months before the anchor's month.</param>
    /// <returns>The invoice date: the anchor's day of the month in that month, less the customer's billing offset.</returns>
    private static DateTimeOffset BillingDay(int position, DateTimeOffset today, int monthsBack) =>
        today.AddMonths(-monthsBack).AddDays(-BillingDayOffset(position));

    /// <summary>The day a recent customer is first invoiced: a few days after signing up, but never after the anchor.</summary>
    /// <param name="position">The position of a recent signup (see <see cref="IsRecentSignup"/>).</param>
    /// <param name="today">The anchor.</param>
    /// <returns>The first invoice date.</returns>
    private static DateTimeOffset FirstRecentInvoiceDate(int position, DateTimeOffset today) =>
        Clamp(today.AddDays(RecentFirstInvoiceLagDays - RecentSignupAge(position)), today.AddDays(-RecentSignupAge(position)), today);

    /// <summary>
    /// The day a customer signed up: a recent customer some days before the anchor, an older one three weeks before the
    /// first invoice, so nothing in the customer's history (services, domains, orders, invoices, quotes, mail) is dated
    /// before the account existed.
    /// </summary>
    /// <param name="position">The customer's position in the id order, the demo client first.</param>
    /// <param name="today">The anchor.</param>
    /// <returns>The signup moment.</returns>
    internal static DateTimeOffset SignupDate(int position, DateTimeOffset today) =>
        IsRecentSignup(position)
            ? today.AddDays(-RecentSignupAge(position)).AddHours(9 + (position % 8))
            : BillingDay(position, today, FirstBillingMonthBack(position)).AddDays(-(FirstInvoiceLagDays + (position % 7))).AddHours(9 + (position % 8));

    /// <summary>
    /// The days a customer is invoiced on, oldest first; the last entry is the customer's current invoice. An older
    /// customer has one invoice for each month of its history, counted back from the anchor's own day of the month, so
    /// the newest is always the same number of days old whatever day the demo is reset on. A recent customer is invoiced
    /// a few days after signing up and then on the same day of every month, up to the anchor.
    /// </summary>
    /// <param name="position">The customer's position in the id order, the demo client first.</param>
    /// <param name="today">The anchor.</param>
    /// <returns>At least one date, none after the anchor.</returns>
    internal static IReadOnlyList<DateTimeOffset> InvoiceDates(int position, DateTimeOffset today)
    {
        if (!IsRecentSignup(position))
        {
            return [.. Enumerable.Range(0, FirstBillingMonthBack(position) + 1).Reverse().Select(monthsBack => BillingDay(position, today, monthsBack))];
        }

        var first = FirstRecentInvoiceDate(position, today);
        List<DateTimeOffset> dates = [];
        for (var month = 0; first.AddMonths(month) <= today; month++)
        {
            dates.Add(first.AddMonths(month));
        }

        return dates;
    }

    /// <summary>
    /// Holds a moment between two bounds, so a follow-up date is never before what it follows or after the anchor. The
    /// latest bound wins when the two cross (a follow-up planned for after the anchor of a customer who joined days ago).
    /// </summary>
    /// <param name="moment">The moment to hold.</param>
    /// <param name="earliest">The earliest moment allowed.</param>
    /// <param name="latest">The latest moment allowed.</param>
    /// <returns>The moment, or the bound it crossed.</returns>
    internal static DateTimeOffset Clamp(DateTimeOffset moment, DateTimeOffset earliest, DateTimeOffset latest)
    {
        var floored = moment < earliest ? earliest : moment;
        return floored > latest ? latest : floored;
    }

    /// <summary>A fresh generator on the fixed seed, so each seeder draws the same sequence every time.</summary>
    /// <returns>A generator seeded with <see cref="RandomSeed"/>.</returns>
    internal static Random NewRandom() => new(RandomSeed);
}
