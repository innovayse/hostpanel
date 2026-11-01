---
title: Payment plugins
---

# Payment plugins

::: warning Work in progress
This page is being written. Check back soon.
:::

## Signed plugin packages

A plugin runs inside the server, so the server needs to know who made a package before it runs one. A plugin package
(the ZIP you upload on the Plugins page) is signed with a detached Ed25519 signature, the file `plugin.sig` at the
root of the package.

### What the server does

| Plugin | Production | Development |
|---|---|---|
| Shipped inside the server image (the Inecobank payment plugin is one) | loads, never checked | loads |
| Uploaded, signed by a trusted key | installs and loads | installs and loads |
| Uploaded, unsigned, changed after signing, or signed by an unknown key | refused | installs and loads |

The check runs when you upload a package and again every time the server starts, so a package that is changed on
disk afterwards is skipped at the next start. Only that plugin is skipped; the log names the plugin and the reason.
Plugins that ship inside the image are part of the server itself, so they carry no signature of their own.

Everything except the Development and Testing environments counts as production.

### Allowing unsigned plugins

Open **Plugins** in the admin panel and switch on **Allow unsigned plugins**. It is off by default, and each change is
written to the activity log. With it on, a production server installs and loads packages that fail the signature
check, so only switch it on when you trust every package you upload. A change of the switch applies to the next
upload at once, and to plugins that are already installed after the next restart.

### Trusted keys

The server trusts the public keys listed in two places. The maintainers' keys ship with the server in the
`PluginSigning:MaintainerKeys` section (empty until the first key is published). You add your own under
`PluginSigning:OperatorKeys`, for example with environment variables:

```text
PluginSigning__OperatorKeys__0__KeyId=my-company-2026
PluginSigning__OperatorKeys__0__PublicKey=<base64 of the 32-byte public key>
```

A key id is 1 to 64 characters of `a-z`, `0-9`, `.`, `-` and `_`. Only public keys go here. The server refuses to
start when a key is not usable.

### Signing a package

The format is described in the repository's `rules/project.md`. To sign, use the signing tool from a checkout of the
repository. Keep the private key outside every repository; the tool refuses a key file inside a git working tree.

```bash
# once: create a key pair; this prints the public key for PluginSigning__OperatorKeys
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- keygen /secure/place/plugin-signing.key

# every release: sign the folder that will be zipped, then zip it as it is
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- sign ./my-plugin /secure/place/plugin-signing.key my-company-2026
```

Sign after the last change: adding, removing or changing any file afterwards makes the signature invalid.
