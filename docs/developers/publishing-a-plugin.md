---
title: Publishing a plugin
---

# Publishing a plugin

This page takes a finished plugin to a signed, versioned package that operators can install, and shows how to list it in a plugin registry. For writing the plugin itself, see [Write a plugin](./write-a-plugin).

A plugin registry is a service that lists signed plugin packages and lets an operator install one from the admin panel. Where this page needs an address, it uses `registry.example.com`; use the address of the registry you publish to.

## Checklist before you release

Check each item. The host runs the same rules at install and at load, so a miss here becomes a refused package later.

- `plugin.json` is at the root of the package and has `"manifestVersion": 2`.
- `id` uses lower-case letters, digits and single hyphens, at most 64 characters, and never changes between versions.
- `version` is a strict `major.minor.patch` version and matches the `<Version>` in your project file and the release tag.
- `type`, `entryPoint` and `sdkVersion` are set, and `capabilities` agrees with the interfaces the entry class really implements.
- Every key-shaped label, title and description (such as `menu.reports`) is defined in the `en` texts under `locales`.
- Every route, page and slot follows the path, permission and slot rules in [Write a plugin](./write-a-plugin#manifest-v2-members).
- The optional `icon` is a package-relative `.svg` or `.png` file of at most 256 KiB.
- The assembly name is your own prefix, such as `MyCompany.MyGateway`. Only official plugins use `Innovayse.Hostpanel.Plugins.<Name>`.
- The package carries no copy of `Hostpanel.Sdk.dll` and no assembly the host already provides.
- The plugin builds against the SDK major the target host supports. A package built against an older SDK major is refused with `PLUGIN_SDK_ASSEMBLY_OUTDATED`.

## Generate a signing key

Production servers accept only signed packages. Signing uses an Ed25519 key pair and the `PluginSigner` tool from the Hostpanel repository (`backend/tools/Innovayse.Hostpanel.PluginSigner`).

```bash
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- keygen my-key.key
```

The command writes the private key to `my-key.key` (the base64 of the 32-byte key) and prints the public key. The tool refuses a key file inside a git working tree, so keep it elsewhere. To print the public key again later:

```bash
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- public-key my-key.key
```

Choose a key id of lower-case letters, digits, dots, hyphens and underscores, starting with a letter or digit, at most 64 characters, for example `mycompany-2026`. The id is part of the signed text, so a signature cannot be shown under another id.

The private key is never committed, never put in configuration and never copied to a server. In a release pipeline it lives in a protected secret, as in the workflow template below. The key id is public and needs no protection.

## Package layout

Stage a folder that holds only what the host needs, then sign that folder:

```
plugin.json                       at the root
MyCompany.MyGateway.dll           your assembly
assets/icon.svg                   the file plugin.json names in icon (optional)
LICENSE
plugin.sig                        written by the signer
```

Never copy the whole build output into the folder. It also holds the SDK and the framework libraries, and the host refuses a package that ships an assembly it provides.

## Sign the folder

Sign after the last change to any file in the folder:

```bash
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- sign ./stage/my-plugin my-key.key mycompany-2026
```

This writes `plugin.sig` at the root. The signature format and its verification live in the `Hostpanel.Sdk.Packaging` package (3.1.0), so the signer, the host and your own pipeline all use one implementation. The signature covers every file in the folder, subfolders included, by path and SHA-256 digest. Changing, adding or removing a file afterwards makes the package fail with `PLUGIN_SIGNATURE_INVALID`.

The signer refuses a path with a control character in its name and any file or folder that is a symbolic link or a junction.

## Zip content rules

- Zip the contents of the folder, not the folder itself, so `plugin.json` and `plugin.sig` are at the root of the archive.
- Name the archive `<id>-<version>.zip`, for example `my-plugin-1.2.0.zip`.
- Keep the archive within the limits of the installing host and registry: a bounded entry count and a bounded uncompressed size. A package over the limit is refused with `PLUGIN_ARCHIVE_TOO_LARGE`.
- Every entry must stay inside the plugin folder. A name that climbs out with `..` or starts at a root is refused.
- Do not add a file after signing, including a stray `.DS_Store` or a `Thumbs.db`.
- Check the finished archive before you publish it: list it and confirm it holds `plugin.json`, `plugin.sig` and no `Hostpanel.Sdk` or framework assembly.

```bash
(cd stage/my-plugin && zip -r -X ../../my-plugin-1.2.0.zip .)
unzip -l my-plugin-1.2.0.zip
```

## Register your key and reserve an id

A registry needs to know who you are before it accepts a package. The steps, in order:

1. Sign in to the registry and create a publisher account for yourself or your organisation.
2. Register your public key under that publisher, with the key id you pass to `sign`. Only the public key is shared. A key id belongs to one publisher.
3. Reserve your plugin id. An id is owned by one publisher, and the registry refuses a package for an id that another publisher holds. Some ids are reserved for the registry operator.
4. For GitHub release publishing, prove that you control the repository, as the registry instructs.

Operators who install your package straight from a ZIP, without a registry, add the same public key to `PluginSigning:OperatorKeys` on their server. Without it the install is refused with `PLUGIN_SIGNATURE_KEY_UNKNOWN`.

## Upload to a registry

There are two ways to get a signed package into a registry. Use one or both.

### Upload with an API key

Create an API key for your publisher in the registry. The upload is a multipart request to `POST /v1/plugins/{id}/versions` with the signed ZIP in the `file` part:

```bash
curl --fail-with-body \
  --header "Authorization: Bearer $PLUGIN_REGISTRY_API_KEY" \
  --form "file=@my-plugin-1.2.0.zip;type=application/zip" \
  https://registry.example.com/v1/plugins/my-plugin/versions
```

The registry runs the checks the host runs, which come from the `Hostpanel.Sdk.Packaging` package (3.1.0, versioned with `Hostpanel.Sdk`; the same package you can reference to run them locally): manifest rules, the id and its reservation, the SDK contract and assembly major, no host assemblies, archive limits, icon safety and the signature against a key registered to your publisher. A refusal comes back as an error status with the rule that failed. Treat the API key as a secret: keep it in the CI secret store, never in the repository.

The workflow template below ends with an optional step that makes this request for you.

### Link GitHub releases

Instead of uploading, link the repository to the registry. When you publish a GitHub release that carries the signed ZIP as an asset, the registry pulls it and runs the same checks. The release workflow below already creates such a release. Name the asset `<id>-<version>.zip` and keep the release tag equal to `v<version>`.

## The release workflow

The template is a GitHub Actions workflow in two jobs. The `package` job builds, tests and stages the folder and has no secrets. The `sign` job runs in a protected `release` environment that holds the signing key and waits for a required reviewer. It signs the folder, zips it, checks the archive for host assemblies, uploads the ZIP to the registry when one is configured and only then publishes the GitHub release, so a package the registry refuses leaves no release behind. The `sign` job never builds the plugin's own code; it builds only the pinned signing tool and signs the folder the `package` job staged. Third-party actions are pinned to a commit SHA.

[Download plugin-release.yml](../templates/plugin-release.yml). Save it as `.github/workflows/release.yml` and change the values marked `CHANGE`: `PLUGIN_ID` (the id from `plugin.json`), `PROJECT` and `PROJECT_FILE` (the project folder and the project file that holds `<Version>`), `SOLUTION` (the solution to build and test) and the `dotnet-version` of the `package` job (change it when your plugin targets another framework). It expects `scripts/package.sh` in your repository to stage the folder shown above.

One-time setup in the plugin repository:

- An environment named `release` with required reviewers and a deployment rule that allows only tags matching `v*`.
- The environment secret `PLUGIN_SIGNING_KEY` (the contents of the private key file). It is the only secret the signing needs.
- The repository variable `PLUGIN_SIGNING_KEY_ID`, your key id. It is a variable, not a secret, because the id is public by design: it is written into `plugin.sig` and the release notes.
- A repository variable `HOSTPANEL_SIGNER_REF`: the full 40 character commit SHA of the Hostpanel repository whose signer builds the signature. The workflow refuses an empty or non-SHA value.
- A tag protection rule for `v*`.
- To publish to a registry: the repository variable `PLUGIN_REGISTRY_URL` (for example `https://registry.example.com`) and the environment secret `PLUGIN_REGISTRY_API_KEY`. With no `PLUGIN_REGISTRY_URL` the upload step is skipped.

To release, bump the version in `plugin.json` and the project file, merge to `main`, then push a tag `v<version>`. The workflow refuses a tag whose commit is not on `main` and a tag that disagrees with the manifest or the project.

<<< @/public/templates/plugin-release.yml{yaml}

The upload step assumes a registry that takes an API key as a bearer token. If your registry names its header differently, change the one line that writes the header file.

## Verification badge

A registry may show a verification badge beside a plugin. The badge is set by the registry after its own review, usually a manual one, and it is not something the package can claim. A signature proves who published the files and that they are unchanged. It does not prove the code is safe, and it does not stop an older signed version from being installed again.

To be reviewed, keep the source public or available to the reviewers, list the permissions and hosts the plugin uses, and keep the changelog current. Follow the registry's own review guidance for the rest.

## After you publish

- Operators install from the registry or by uploading the ZIP under **Plugins**. An upgrade of a loaded plugin takes effect at the next restart.
- To withdraw a bad version, follow the registry's yank process and ship a new, higher version. To stop operators trusting a key, remove its registration and tell operators to remove it from `PluginSigning:OperatorKeys`.
- Never paste live credentials or the private key into an issue, a log or a screenshot.
