using System.Text;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginSignaturePayload"/> edge cases: links are never followed, a name with no UTF-8 form is refused,
/// a nested <c>plugin.sig</c> is covered by the signature, and unusual package shapes (spaces in a path, an empty
/// package, a package holding only the signature) sign and verify as documented. No container or database.
/// </summary>
public sealed class PluginSignaturePayloadTests
{
    /// <summary>A file that is a symbolic link makes the package unsignable and malformed.</summary>
    [Fact]
    public void Build_SymlinkedFile_IsMalformed()
    {
        var folder = SignedFolder("payload-link-file");
        var outside = PluginSigningTestSeeds.NewFolder("payload-link-file-target");
        try
        {
            var target = Path.Combine(outside, "target.txt");
            File.WriteAllText(target, "outside the package");
            if (!TryLink(() => File.CreateSymbolicLink(Path.Combine(folder, "link.txt"), target)))
            {
                // This OS account cannot create symbolic links (Windows without developer mode); the Linux test container can.
                return;
            }

            AssertRefused(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>A folder that is a symbolic link makes the package unsignable and malformed, and is not walked into.</summary>
    [Fact]
    public void Build_SymlinkedDirectory_IsMalformed()
    {
        var folder = SignedFolder("payload-link-dir");
        var outside = PluginSigningTestSeeds.NewFolder("payload-link-dir-target");
        try
        {
            File.WriteAllText(Path.Combine(outside, "inner.txt"), "outside the package");
            if (!TryLink(() => Directory.CreateSymbolicLink(Path.Combine(folder, "linked"), outside)))
            {
                // This OS account cannot create symbolic links (Windows without developer mode); the Linux test container can.
                return;
            }

            AssertRefused(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>Text with a lone surrogate has no UTF-8 form: it is refused, never written with a replacement character.</summary>
    [Fact]
    public void Encode_LoneSurrogate_IsRefused()
    {
        var encode = () => PluginSignaturePayload.Encode("innovayse\ud800");

        encode.Should().Throw<InvalidDataException>();
        Encoding.UTF8.GetString(PluginSignaturePayload.Encode("naïve 😀")).Should().Be("naïve 😀");
    }

    /// <summary>A lone surrogate in a real file name (possible on Windows) makes the package malformed.</summary>
    [Fact]
    public void Build_FileNameWithLoneSurrogate_IsMalformed()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Other file systems store bytes and the runtime never reports a lone surrogate; Encode_LoneSurrogate_IsRefused covers the rule.
            return;
        }

        var folder = SignedFolder("payload-surrogate");
        try
        {
            File.WriteAllText(Path.Combine(folder, "bad\ud800.txt"), "x");

            AssertRefused(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A <c>plugin.sig</c> below the root is a normal file: it is signed, so changing it fails the check.</summary>
    [Fact]
    public void Inspect_ChangedNestedSignatureFile_IsInvalid()
    {
        var folder = PluginSigningTestSeeds.NewFolder("payload-nested-sig");
        try
        {
            PluginSigningTestSeeds.WritePackage(folder, "nested-sig-plugin", new Dictionary<string, string> { ["sub/plugin.sig"] = "first" });
            PluginSigningTestSeeds.Sign(folder);
            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder).Should().BeNull();

            File.WriteAllText(Path.Combine(folder, "sub", "plugin.sig"), "second");

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Invalid);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A path with spaces signs, verifies, and fails once its file changes.</summary>
    [Fact]
    public void Inspect_PathWithSpaces_SignsAndVerifies()
    {
        var folder = PluginSigningTestSeeds.NewFolder("payload-spaces");
        try
        {
            PluginSigningTestSeeds.WritePackage(
                folder, "spaces-plugin", new Dictionary<string, string> { ["my assets/a file name.txt"] = "spaced" });
            PluginSigningTestSeeds.Sign(folder);

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder).Should().BeNull();
            Encoding.UTF8.GetString(PluginSignaturePayload.Build(folder, PluginSigningTestSeeds.TrustedKeyId))
                .Should().Contain(" my assets/a file name.txt\n");

            File.WriteAllText(Path.Combine(folder, "my assets", "a file name.txt"), "changed");

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Invalid);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>An empty package is unsigned; once signed it verifies, and a file added afterwards fails it.</summary>
    [Fact]
    public void Inspect_EmptyPackage_IsUnsignedThenSignedThenInvalidWhenAFileAppears()
    {
        var folder = PluginSigningTestSeeds.NewFolder("payload-empty");
        try
        {
            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Unsigned);

            PluginSigningTestSeeds.Sign(folder);
            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder).Should().BeNull();
            Encoding.UTF8.GetString(PluginSignaturePayload.Build(folder, PluginSigningTestSeeds.TrustedKeyId))
                .Should().Be("innovayse-plugin-signature-v1\nkey test-key\n");

            File.WriteAllText(Path.Combine(folder, "late.txt"), "added");

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Invalid);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A package holding only <c>plugin.sig</c> with something that is not a signature is malformed.</summary>
    [Fact]
    public void Inspect_PackageWithOnlyAGarbageSignatureFile_IsMalformed()
    {
        var folder = PluginSigningTestSeeds.NewFolder("payload-only-sig");
        try
        {
            File.WriteAllText(Path.Combine(folder, PluginSignatureFile.FileName), "this is not a signature");

            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Malformed);
            PluginSignaturePayload.Build(folder, PluginSigningTestSeeds.TrustedKeyId)
                .Should().Equal(Encoding.UTF8.GetBytes("innovayse-plugin-signature-v1\nkey test-key\n"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Asserts a package with a link is malformed for the verifier and unsignable for the signer.</summary>
    /// <param name="folder">The package folder holding the link, signed beforehand.</param>
    private static void AssertRefused(string folder)
    {
        PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule.Should().Be(PluginSignatureRule.Malformed);
        var sign = () => PluginSigningTestSeeds.Sign(folder);
        sign.Should().Throw<InvalidDataException>();
    }

    /// <summary>Builds a signed package folder that is still unchanged.</summary>
    /// <param name="label">A short label for the folder name.</param>
    /// <returns>The folder; the caller deletes it.</returns>
    private static string SignedFolder(string label)
    {
        var folder = PluginSigningTestSeeds.NewFolder(label);
        PluginSigningTestSeeds.WritePackage(folder, "linked-plugin");
        PluginSigningTestSeeds.Sign(folder);
        return folder;
    }

    /// <summary>Tries to create a link, reporting whether this OS account is allowed to.</summary>
    /// <param name="create">Creates the link.</param>
    /// <returns><see langword="true"/> when the link exists now.</returns>
    private static bool TryLink(Action create)
    {
        try
        {
            create();
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}
