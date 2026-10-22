# ORACLE Website

This is a static site built with plain HTML, CSS, and JavaScript. It has no install/build step and no analytics or third-party runtime dependencies. It includes a clearly labelled illustrative interface preview rather than claiming a screenshot of the running app.

## Live site

The public website is hosted at [https://nexora-os-1.github.io/oracle-desktop/](https://nexora-os-1.github.io/oracle-desktop/). The GitHub Actions workflow in `.github/workflows/pages.yml` publishes the contents of `website/deploy` on each push to `main`.

## Preview locally

The deployable site is the clean `website\deploy` directory. From that directory, run:

```powershell
node .\preview.mjs
```

Then open `http://localhost:8080`. `preview.mjs` uses only Node's built-in HTTP and file-system modules. The release card checks `release.json` and the ZIP with a same-origin `HEAD` request, so preview over HTTP rather than opening `index.html` directly.

## Static hosting

Deploy the contents of `website\deploy` as the site root. It contains only the current release and its required web files. Keep `release.json`, `assets/`, `styles.css`, `site.js`, and `downloads/ORACLE-2.0.0-win-x64-build4.zip` together at their relative paths. The download link is relative and the site checks the actual ZIP before enabling it. Configure the host to serve ZIP files as downloadable binary content (typically `application/zip`). No server-side runtime is required.

The illustrative preview is explicitly labelled and contains no live or sample telemetry. Release size and checksum are stored in `release.json` and are generated from `website\downloads\ORACLE-2.0.0-win-x64-build4.zip`; the deploy folder includes that same package.
