namespace Innovayse.Hostpanel.Application.Common.Settings;

/// <summary>How large an invoice this deployment will render as a PDF, and how many at once.</summary>
/// <remarks>
/// Rendering lays out every line in memory before the first page is written, so the line count is
/// what decides how long a request holds a thread and how much it allocates. The ceilings live in
/// configuration because they are a deployment's call: an install that bills usage line by line may
/// need more lines than the default, and a larger box can afford more renders at once.
/// </remarks>
public sealed class InvoicePdfOptions
{
    /// <summary>Configuration section this binds from.</summary>
    public const string SectionName = "InvoicePdf";

    /// <summary>
    /// Longest <see cref="MaxRenderWaitSeconds"/> accepted at startup: an hour, the same ceiling as
    /// <c>ServiceUnavailable:RetryAfterSeconds</c>. A wait anywhere near it already defeats the point
    /// of bounding it, and far past it the semaphore refuses the timeout at request time.
    /// </summary>
    public const int MaxRenderWaitSecondsCeiling = 3600;

    /// <summary>
    /// Most line items an invoice may have and still be rendered (<c>InvoicePdf__MaxLines</c>).
    /// An invoice over it answers <c>INVOICE_PDF_EXCEEDS_LIMIT</c> before anything is drawn.
    /// </summary>
    public int MaxLines { get; init; } = 500;

    /// <summary>
    /// Most invoice PDFs the whole process renders at once (<c>InvoicePdf__MaxConcurrentRenders</c>).
    /// A render over it waits for a free slot without holding a thread.
    /// </summary>
    /// <remarks>
    /// Each render keeps a thread-pool thread busy for its whole duration, so this bounds how many
    /// pool threads PDFs can take from every other request. The default of 4 is twice the two CPUs
    /// the API container is given.
    /// </remarks>
    public int MaxConcurrentRenders { get; init; } = 4;

    /// <summary>
    /// Longest a render waits for a free slot, in seconds (<c>InvoicePdf__MaxRenderWaitSeconds</c>),
    /// from 1 to <see cref="MaxRenderWaitSecondsCeiling"/>. A render that gets no slot within it answers
    /// <c>INVOICE_PDF_RENDER_BUSY</c> (503, with <c>Retry-After</c>) instead of rendering.
    /// </summary>
    /// <remarks>
    /// Without it a burst over <see cref="MaxConcurrentRenders"/> queues for as long as the burst
    /// lasts, and every request in the queue keeps its connection open the whole time. A render
    /// takes well under a second, so the default of 10 still covers a full queue draining.
    /// </remarks>
    public int MaxRenderWaitSeconds { get; init; } = 10;
}
