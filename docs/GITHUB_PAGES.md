# Website maintenance

The [OrbisExtract website](https://flugeru.github.io/OrbisExtract/) is a static HTML and CSS site. The Portuguese homepage is `index.html`; the English homepage is `en.html`. Each language has its own support, privacy and terms pages. Keep language switches pointed at the equivalent page.

## Check changes locally

From the repository root, run:

```sh
python3 scripts/check-site.py
```

The check validates page metadata, local links and anchors, assets, and the sitemap. To stage the files that will be published, run `python3 scripts/check-site.py --output <directory>` with a destination outside the source tree. Add new public assets to the checklist in `scripts/check-site.py` so they are included in the deployment.

## Deploy

Set the Pages source to **GitHub Actions** under **Settings → Pages**. The **OrbisExtract website** workflow validates and publishes changes on `main`. Only the files listed in `scripts/check-site.py` are deployed. The `docs/` directory itself is still visible in the GitHub repository.

Update both language versions when changing downloads, support instructions, or policy text. Verify the published pages after deployment.
