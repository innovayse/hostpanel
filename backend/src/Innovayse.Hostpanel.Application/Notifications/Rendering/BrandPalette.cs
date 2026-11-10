using System.Globalization;

namespace Innovayse.Hostpanel.Application.Notifications.Rendering;

/// <summary>
/// Derives the shades and the readable text colour for the brand colour an operator picked,
/// for the e-mail renderer.
/// </summary>
/// <remarks>
/// <para>
/// A port of <c>client/utils/brandPalette.ts</c>, which is the authority: the storefront
/// emits its <c>:root</c> variables and the admin panel draws its preview from that file, and
/// an e-mail must use the same numbers or a button in the inbox will not match the button on
/// the site. <c>BrandPaletteTests</c> carries the same expected values as the TypeScript test
/// so that a change to one side without the other fails a build.
/// </para>
/// <para>
/// The ramp is built in OKLCH, lightness spread from near-white (50) to near-black (950),
/// hue fixed, chroma tapered toward the ends and pulled back into the sRGB gamut rather than
/// clamped, so a tint of a saturated blue stays blue instead of turning cyan. The picked
/// colour itself sits at 500.
/// </para>
/// </remarks>
public static class BrandPalette
{
    /// <summary>The shade steps, in Tailwind's order.</summary>
    public static readonly IReadOnlyList<int> Steps = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];

    /// <summary>White, the first candidate for text on a filled surface.</summary>
    public const string White = "#ffffff";

    /// <summary>Near-black, the text colour for a surface too light for white.</summary>
    public const string Ink = "#111111";

    /// <summary>Contrast ratio at which white is accepted on a filled surface (WCAG AA, large text).</summary>
    private const double WhiteThreshold = 3.0;

    /// <summary>Reference OKLCH lightness per step; 500 is replaced by the picked colour's own.</summary>
    private static readonly IReadOnlyDictionary<int, double> TargetL = new Dictionary<int, double>
    {
        [50] = 0.975,
        [100] = 0.945,
        [200] = 0.89,
        [300] = 0.81,
        [400] = 0.72,
        [500] = 0.62,
        [600] = 0.53,
        [700] = 0.45,
        [800] = 0.38,
        [900] = 0.31,
        [950] = 0.22,
    };

    /// <summary>
    /// Parses <c>#rrggbb</c> in any case.
    /// </summary>
    /// <param name="hex">The colour with its <c>#</c>.</param>
    /// <returns>The channels 0 to 255, or <see langword="null"/> when the string is not a colour.</returns>
    public static RgbColor? Parse(string hex)
    {
        var s = hex.Trim();
        if (s.Length != 7 || s[0] != '#'
            || !int.TryParse(s.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var n))
        {
            return null;
        }

        return new RgbColor((n >> 16) & 255, (n >> 8) & 255, n & 255);
    }

    /// <summary>
    /// Formats channels as lower-case <c>#rrggbb</c>.
    /// </summary>
    /// <param name="rgb">The channels.</param>
    /// <returns>The hex string.</returns>
    public static string Format(RgbColor rgb) =>
        string.Create(CultureInfo.InvariantCulture, $"#{rgb.R:x2}{rgb.G:x2}{rgb.B:x2}");

    /// <summary>
    /// One shade of the scale built around a colour.
    /// </summary>
    /// <param name="hex">The picked colour, <c>#rrggbb</c>.</param>
    /// <param name="step">A member of <see cref="Steps"/>.</param>
    /// <returns>The shade as <c>#rrggbb</c>; the input itself for step 500.</returns>
    /// <exception cref="ArgumentException">Not a colour, or not a step.</exception>
    public static string Shade(string hex, int step)
    {
        var rgb = Parse(hex) ?? throw new ArgumentException($"Not a #rrggbb colour: {hex}", nameof(hex));
        if (!TargetL.TryGetValue(step, out var refL))
        {
            throw new ArgumentException($"Not a shade step: {step}", nameof(step));
        }

        if (step == 500)
        {
            return Format(rgb);
        }

        var (l0, a0, b0) = RgbToOklab(rgb);
        var chroma0 = Math.Sqrt((a0 * a0) + (b0 * b0));
        var hue = Math.Atan2(b0, a0);

        // Same construction as the TypeScript: the ends move with the picked colour so the
        // ramp never flattens, and tints taper chroma harder than shades.
        var top = Math.Max(0.985, l0 + ((1 - l0) * 0.6));
        var bottom = Math.Min(0.15, l0 * 0.6);
        var ref500 = TargetL[500];
        var l = step < 500
            ? l0 + ((refL - ref500) / (TargetL[50] - ref500) * (top - l0))
            : l0 - ((ref500 - refL) / (ref500 - TargetL[950]) * (l0 - bottom));
        var distance = Math.Min(1, Math.Abs(l - l0) / Math.Max(l0 - bottom, top - l0));
        var taper = step < 500 ? 0.92 : 0.75;
        var chroma = chroma0 * (1 - (taper * Math.Pow(distance, 0.8)));

        return Format(OklchToRgb(l, chroma, hue));
    }

    /// <summary>
    /// WCAG 2 contrast ratio between two colours, 1 to 21.
    /// </summary>
    /// <param name="a">One colour.</param>
    /// <param name="b">The other.</param>
    /// <returns>The ratio, or <see cref="double.NaN"/> when either is not a colour.</returns>
    public static double ContrastRatio(string a, string b)
    {
        if (Parse(a) is not { } ra || Parse(b) is not { } rb)
        {
            return double.NaN;
        }

        var la = Luminance(ra);
        var lb = Luminance(rb);
        var (hi, lo) = la > lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>
    /// The text colour for a filled surface: white when it reaches 3:1, otherwise whichever
    /// of white and near-black reads better. Identical to the TypeScript rule.
    /// </summary>
    /// <param name="hex">The surface colour.</param>
    /// <returns><see cref="White"/> or <see cref="Ink"/>.</returns>
    public static string BestTextOn(string hex)
    {
        var white = ContrastRatio(White, hex);
        if (white >= WhiteThreshold)
        {
            return White;
        }

        return white >= ContrastRatio(Ink, hex) ? White : Ink;
    }

    /// <summary>WCAG 2 relative luminance.</summary>
    /// <param name="rgb">Colour as 8-bit red, green and blue channels.</param>
    /// <returns>Luminance from 0 (black) to 1 (white).</returns>
    private static double Luminance(RgbColor rgb) =>
        (0.2126 * ToLinear(rgb.R)) + (0.7152 * ToLinear(rgb.G)) + (0.0722 * ToLinear(rgb.B));

    /// <summary>sRGB 0 to 255 → linear 0 to 1.</summary>
    /// <param name="c">One sRGB channel, 0 to 255.</param>
    /// <returns>The linear-light value, 0 to 1.</returns>
    private static double ToLinear(int c)
    {
        var s = c / 255.0;
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    /// <summary>Linear 0 to 1 → sRGB 0 to 255, clamped.</summary>
    /// <param name="l">One linear-light channel, 0 to 1.</param>
    /// <returns>The sRGB channel, 0 to 255.</returns>
    private static int ToSrgb(double l)
    {
        var c = l <= 0.0031308 ? l * 12.92 : (1.055 * Math.Pow(l, 1 / 2.4)) - 0.055;
        return (int)Math.Round(Math.Clamp(c, 0, 1) * 255, MidpointRounding.AwayFromZero);
    }

    /// <summary>sRGB → OKLab.</summary>
    /// <param name="rgb">Colour as 8-bit red, green and blue channels.</param>
    /// <returns>The lightness and the two opponent-colour axes.</returns>
    private static OklabColor RgbToOklab(RgbColor rgb)
    {
        var lr = ToLinear(rgb.R);
        var lg = ToLinear(rgb.G);
        var lb = ToLinear(rgb.B);
        var l = Math.Cbrt((0.4122214708 * lr) + (0.5363325363 * lg) + (0.0514459929 * lb));
        var m = Math.Cbrt((0.2119034982 * lr) + (0.6806995451 * lg) + (0.1073969566 * lb));
        var s = Math.Cbrt((0.0883024619 * lr) + (0.2817188376 * lg) + (0.6299787005 * lb));
        return new OklabColor(
            (0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s),
            (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s),
            (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s));
    }

    /// <summary>OKLab → linear sRGB, unclamped.</summary>
    /// <param name="lightness">Perceptual lightness.</param>
    /// <param name="a">Green-red axis.</param>
    /// <param name="b">Blue-yellow axis.</param>
    /// <returns>Linear red, green and blue channels, possibly outside 0 to 1.</returns>
    private static LinearRgb OklabToLinear(double lightness, double a, double b)
    {
        var l = Math.Pow(lightness + (0.3963377774 * a) + (0.2158037573 * b), 3);
        var m = Math.Pow(lightness - (0.1055613458 * a) - (0.0638541728 * b), 3);
        var s = Math.Pow(lightness - (0.0894841775 * a) - (1.2914855480 * b), 3);
        return new LinearRgb(
            (+4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s),
            (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s),
            (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s));
    }

    /// <summary>Whether every linear channel is displayable.</summary>
    /// <param name="lin">Colour as linear red, green and blue channels.</param>
    /// <returns>True when all three channels lie within 0 to 1.</returns>
    private static bool InGamut(LinearRgb lin) =>
        lin.R is >= -0.0005 and <= 1.0005 && lin.G is >= -0.0005 and <= 1.0005 && lin.B is >= -0.0005 and <= 1.0005;

    /// <summary>OKLCH → sRGB, reducing chroma at fixed lightness and hue until the colour fits.</summary>
    /// <param name="l">Perceptual lightness.</param>
    /// <param name="chroma">Requested chroma before gamut reduction.</param>
    /// <param name="hue">Hue angle in radians.</param>
    /// <returns>The 8-bit channels of the in-gamut colour.</returns>
    private static RgbColor OklchToRgb(double l, double chroma, double hue)
    {
        LinearRgb At(double c) => OklabToLinear(l, c * Math.Cos(hue), c * Math.Sin(hue));

        var lin = At(chroma);
        if (!InGamut(lin))
        {
            double lo = 0, hi = chroma;
            for (var i = 0; i < 20; i++)
            {
                var mid = (lo + hi) / 2;
                if (InGamut(At(mid)))
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            lin = At(lo);
        }

        return new RgbColor(ToSrgb(lin.R), ToSrgb(lin.G), ToSrgb(lin.B));
    }
}
