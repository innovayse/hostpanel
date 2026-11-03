using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.PluginSigner;

/// <summary>
/// The commands of the plugin signing tool:
/// <c>keygen &lt;private-key-file&gt;</c>, <c>public-key &lt;private-key-file&gt;</c> and
/// <c>sign &lt;package-dir&gt; &lt;private-key-file&gt; &lt;key-id&gt;</c>.
/// A private key file holds the base64 of the 32-byte Ed25519 private key. The tool refuses a key file inside a git
/// working tree, so a private key cannot be committed by accident.
/// </summary>
public static class SignerCli
{
    /// <summary>The exit code of a command that did its work.</summary>
    private const int Ok = 0;

    /// <summary>The exit code of a command that was refused or failed.</summary>
    private const int Failed = 1;

    /// <summary>The exit code of a command line that is not understood.</summary>
    private const int Usage = 2;

    /// <summary>Runs one command.</summary>
    /// <param name="args">The command line arguments.</param>
    /// <param name="output">Receives what the command prints (a public key, a confirmation).</param>
    /// <param name="error">Receives the usage text and every refusal.</param>
    /// <returns>The process exit code.</returns>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            return (args.Length, args.FirstOrDefault()) switch
            {
                (2, "keygen") => KeyGen(args[1], output),
                (2, "public-key") => PublicKey(args[1], output),
                (4, "sign") => Sign(args[1], args[2], args[3], output),
                _ => PrintUsage(error),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or FormatException)
        {
            error.WriteLine(ex.Message);
            return Failed;
        }
    }

    /// <summary>Creates a private key file and prints the matching public key.</summary>
    /// <param name="keyFile">Where to write the private key; it must not exist yet.</param>
    /// <param name="output">Receives the public key to put in the host's trusted key list.</param>
    /// <returns>The exit code.</returns>
    private static int KeyGen(string keyFile, TextWriter output)
    {
        RefuseInsideGitWorkTree(keyFile);

        var (privateKey, publicKey) = Infrastructure.Plugins.PluginSigner.GenerateKeyPair();
        using (var stream = new FileStream(keyFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            // Owner-only where the platform has file modes; a private key file is not for anyone else to read.
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(keyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            using var writer = new StreamWriter(stream);
            writer.WriteLine(Convert.ToBase64String(privateKey));
        }

        output.WriteLine(Convert.ToBase64String(publicKey));
        return Ok;
    }

    /// <summary>Prints the public key of an existing private key file.</summary>
    /// <param name="keyFile">The private key file.</param>
    /// <param name="output">Receives the public key.</param>
    /// <returns>The exit code.</returns>
    private static int PublicKey(string keyFile, TextWriter output)
    {
        output.WriteLine(Convert.ToBase64String(Infrastructure.Plugins.PluginSigner.PublicKeyOf(ReadPrivateKey(keyFile))));
        return Ok;
    }

    /// <summary>Signs a package folder, writing <c>plugin.sig</c> at its root.</summary>
    /// <param name="packageDir">The package root: the folder holding <c>plugin.json</c>.</param>
    /// <param name="keyFile">The private key file.</param>
    /// <param name="keyId">The id the host's trusted key list knows the key by.</param>
    /// <param name="output">Receives a confirmation.</param>
    /// <returns>The exit code.</returns>
    private static int Sign(string packageDir, string keyFile, string keyId, TextWriter output)
    {
        if (!File.Exists(Path.Combine(packageDir, "plugin.json")))
        {
            throw new InvalidDataException($"{packageDir} holds no plugin.json: sign the folder that will be zipped.");
        }

        var privateKey = ReadPrivateKey(keyFile);
        Infrastructure.Plugins.PluginSigner.Sign(packageDir, keyId, privateKey);
        output.WriteLine($"Signed {packageDir} with key '{keyId}'. Zip the folder as it is now: changing any file invalidates {PluginSignatureFile.FileName}.");
        return Ok;
    }

    /// <summary>Prints the usage text.</summary>
    /// <param name="error">Receives the text.</param>
    /// <returns>The usage exit code.</returns>
    private static int PrintUsage(TextWriter error)
    {
        error.WriteLine("Usage:");
        error.WriteLine("  keygen <private-key-file>                      create a key; prints the public key for PluginSigning:OperatorKeys");
        error.WriteLine("  public-key <private-key-file>                  print the public key of a private key file");
        error.WriteLine("  sign <package-dir> <private-key-file> <key-id> write plugin.sig into the package folder");
        return Usage;
    }

    /// <summary>Reads a private key file.</summary>
    /// <param name="keyFile">The file.</param>
    /// <returns>The 32 key bytes.</returns>
    /// <exception cref="InvalidDataException">The file is inside a git working tree, or does not hold a base64 32-byte key.</exception>
    private static byte[] ReadPrivateKey(string keyFile)
    {
        RefuseInsideGitWorkTree(keyFile);

        var text = File.ReadAllText(keyFile).Trim();
        var buffer = new byte[Infrastructure.Plugins.PluginSigner.PrivateKeyLength];
        if (!Convert.TryFromBase64String(text, buffer, out var written) || written != buffer.Length)
        {
            throw new InvalidDataException($"{keyFile} does not hold the base64 of a 32-byte private key.");
        }

        return buffer;
    }

    /// <summary>Refuses a key file that sits inside a git working tree, however deep.</summary>
    /// <param name="keyFile">The key file path.</param>
    /// <exception cref="InvalidDataException">A parent folder holds a <c>.git</c> entry.</exception>
    private static void RefuseInsideGitWorkTree(string keyFile)
    {
        for (var dir = Path.GetDirectoryName(Path.GetFullPath(keyFile)); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git")))
            {
                throw new InvalidDataException($"{keyFile} is inside the git working tree {dir}. Keep a private key outside every repository.");
            }
        }
    }
}
