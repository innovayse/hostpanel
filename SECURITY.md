# Security policy

HostPanel handles billing data, server credentials and customer accounts, so we take
security reports seriously and we are grateful for them.

## Reporting a vulnerability

**Please do not open a public issue.** Report it privately instead:

1. Preferred: [open a private security advisory](https://github.com/innovayse/hostpanel/security/advisories/new) on GitHub
2. Or email **contact@innovayse.com** with "SECURITY" in the subject

Include what you found, how to reproduce it, and the version or commit you tested.

## What to expect

- We acknowledge your report within **3 working days**
- We keep you updated while we work on a fix
- We credit you in the release notes and the advisory, unless you prefer to stay anonymous

## Supported versions

Security fixes go into the latest release and `main`. Please test against `main` before
reporting, if you can.

## Scope

In scope: this repository (API, client portal, admin panel, Docker setup).
Out of scope: the public demo at demo-hostpanel.innovayse.com and
demo-hostpanel-admin.innovayse.com (it is reset regularly and shows its sign-in details on each
login page), denial-of-service by traffic volume, and findings that need an
already compromised server.
