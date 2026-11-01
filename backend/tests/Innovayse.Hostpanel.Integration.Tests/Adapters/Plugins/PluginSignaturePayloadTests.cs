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
    /// <summary>
    /// A file that is a symbolic link makes the package unsignable and malformed. Where the account may not create a
    /// file symbolic link (a local Windows account without developer mode) the test cannot run and returns; CI fails
    /// instead, and the directory tests below cover the link rule on every machine.
    /// </summary>
    [Fact]
    public void Build_SymlinkedFile_IsMalformed()
    {
        var folder = SignedFolder("payload-link-file");
        var outside = PluginSigningTestSeeds.NewFolder("payload-link-file-target");
        try
        {
            var target = Path.Combine(outside, "target.txt");
            File.WriteAllText(target, "outside the package");
            if (!PluginLinkTestSeeds.TryCreateFileLink(Path.Combine(folder, "link.txt"), target))
            {
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

    /// <summary>A folder that is a symbolic link (a junction on Windows) makes the package unsignable and malformed.</summary>
    [Fact]
    public void Build_SymlinkedDirectory_IsMalformed()
    {
        var folder = SignedFolder("payload-link-dir");
        var outside = PluginSigningTestSeeds.NewFolder("payload-link-dir-target");
        try
        {
            File.WriteAllText(Path.Combine(outside, "inner.txt"), "outside the package");
            PluginLinkTestSeeds.CreateDirectoryLink(Path.Combine(folder, "linked"), outside);

            AssertRefused(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>A directory junction is refused the same way; on Windows it needs no privilege, so this runs everywhere.</summary>
    [Fact]
    public void Build_JunctionedDirectory_IsMalformed()
    {
        var folder = SignedFolder("payload-junction");
        var outside = PluginSigningTestSeeds.NewFolder("payload-junction-target");
        try
        {
            File.WriteAllText(Path.Combine(outside, "inner.txt"), "outside the package");
            var junction = Path.Combine(folder, "junction");
            PluginLinkTestSeeds.CreateDirectoryLink(junction, outside);
            new DirectoryInfo(junction).Attributes.HasFlag(FileAttributes.ReparsePoint).Should().BeTrue("the junction must exist before it is judged");

            AssertRefused(folder);
            File.Exists(Path.Combine(outside, "inner.txt")).Should().BeTrue("the target of a link is never touched");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>
    /// Hidden files, dotfiles and a file two folders deep are all covered by the signature: each one is named in the
    /// payload, and changing any one of them makes the package invalid.
    /// </summary>
    [Fact]
    public void Inspect_HiddenDotAndNestedFiles_AreCovered()
    {
        string[] paths = [".hidden", ".config/.secret", "a/b/deep.txt"];
        var folder = PluginSigningTestSeeds.NewFolder("payload-hidden");
        try
        {
            PluginSigningTestSeeds.WritePackage(folder, "hidden-plugin", paths.ToDictionary(p => p, _ => "original"));
            if (OperatingSystem.IsWindows())
            {
                // On Unix a leading dot is what hides a file; on Windows the attribute does.
                File.SetAttributes(Path.Combine(folder, ".hidden"), FileAttributes.Hidden);
            }

            PluginSigningTestSeeds.Sign(folder);
            PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder).Should().BeNull();
            var payload = Encoding.UTF8.GetString(PluginSignaturePayload.Build(folder, PluginSigningTestSeeds.TrustedKeyId));
            foreach (var path in paths)
            {
                payload.Should().Contain(" " + path + "\n");
            }

            foreach (var path in paths)
            {
                var file = Path.Combine(folder, path);
                var attributes = File.GetAttributes(file);
                File.SetAttributes(file, FileAttributes.Normal);
                File.WriteAllText(file, "changed");
                File.SetAttributes(file, attributes);

                PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder)!.Rule
                    .Should().Be(PluginSignatureRule.Invalid, "changing '{0}' must break the signature", path);

                File.SetAttributes(file, FileAttributes.Normal);
                File.WriteAllText(file, "original");
                File.SetAttributes(file, attributes);
                PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder).Should().BeNull();
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
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

    /// <summary>Builds a signed package folder that is still unchanged, and proves it verifies, so a later Malformed is caused by what the test adds.</summary>
    /// <param name="label">A short label for the folder name.</param>
    /// <returns>The folder; the caller deletes it.</returns>
    private static string SignedFolder(string label)
    {
        var folder = PluginSigningTestSeeds.NewFolder(label);
        PluginSigningTestSeeds.WritePackage(folder, "linked-plugin");
        PluginSigningTestSeeds.Sign(folder);
        PluginSigningTestSeeds.Verifier(enforced: true).Inspect(folder).Should().BeNull("the package is signed and clean before a link is added");
        return folder;
    }
}
