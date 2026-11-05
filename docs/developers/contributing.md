---
title: Contributing
---

# Contributing

::: warning Work in progress
This page is being written. Check back soon.
:::

## Documentation site settings

The documentation site (VitePress, in `docs/`) reads two optional environment variables when it is built. Both have defaults for the public GitHub Pages site, so most contributors set nothing.

| Variable | Default | Meaning |
|---|---|---|
| `DOCS_BASE` | `/hostpanel/` | URL path the site is served under, with a leading and a trailing slash. Also used for the favicon link. Use `/` on a custom domain. |
| `DOCS_SITE_URL` | `https://innovayse.github.io/hostpanel` | Public origin plus base path, without a trailing slash. Used for the canonical links, the social preview image URLs and the sitemap (`sitemap.xml` under the base path). Keep it in step with `DOCS_BASE`. |

For example, to build a copy served from the root of your own domain:

```bash
DOCS_BASE=/ DOCS_SITE_URL=https://docs.example.org npm run build
```
