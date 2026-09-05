namespace Innovayse.Application.Admin.Common;

/// <summary>
/// The bytes an operator uploaded, before anything has been believed about them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately carries no content type.</b> The multipart part's <c>Content-Type</c> header
/// is written by the client and is therefore a claim, not a fact -- the previous version of this
/// upload trusted it as its only check, so a file named <c>.png</c> and declared
/// <c>image/png</c> was written to the web root whatever its actual bytes were. The format is
/// decided by <see cref="Interfaces.IBrandingImageProcessor"/> from the content itself.
/// </para>
/// <para>
/// <see cref="FileName"/> is kept for the log line and for nothing else. It never reaches the
/// filesystem: stored names are generated, so a crafted name cannot pick its own extension or
/// walk out of the upload directory.
/// </para>
/// </remarks>
/// <param name="Content">The uploaded bytes, already length-capped by the caller.</param>
/// <param name="FileName">The client-supplied name, for diagnostics only.</param>
public sealed record BrandingSource(byte[] Content, string FileName);
