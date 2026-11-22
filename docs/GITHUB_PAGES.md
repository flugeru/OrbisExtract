# OrbisExtract website

The public site lives in `docs/`. It is plain HTML/CSS, with no scripts, analytics,
external fonts, accounts or upload form. PT-BR and English are available.
Contact published with the owner's authorization: `fluugell@gmail.com`.

Stable URLs after activation:

- Site: https://flugeru.github.io/OrbisExtract/
- Privacy (used by the prepared Android app): https://flugeru.github.io/OrbisExtract/privacy-policy.html
- Terms: https://flugeru.github.io/OrbisExtract/terms.html
- Support: https://flugeru.github.io/OrbisExtract/support.html

## One-time repository setup

In repository **Settings → Pages → Build and deployment → Source**, choose
**GitHub Actions**. This requires repository administration and is not granted
by the connector's code-write permission. Do not add a custom domain unless you
own it and intend to configure DNS. Use the default HTTPS URL above.

The `OrbisExtract website` workflow checks the site, stages an explicit allowlist
of public web files and publishes them on pushes to `main`. It can also be run
manually. Source code, release instructions, signing keys, passwords and other
documents are not included in the website artifact.

If the first run fails at Configure Pages because the site has not been enabled,
select GitHub Actions in Settings and rerun that failed job. Enable Enforce HTTPS
when GitHub exposes that setting. Verify the deployed page and privacy URL return
HTTP 200 before entering the privacy URL in Google Play Console.

## Editing and checks

Edit the static files directly. No generated source or frontend build is needed.
Run `python3 scripts/check-site.py` to check titles, language, viewport, local
links, anchors, required contact information, assets and sitemap. The same checks
run for pull requests. For an allowlisted publish folder, use
`python3 scripts/check-site.py --output /tmp/orbis-pages`.

The privacy policy reflects local PKG processing, up to 100 local history entries,
in-memory artwork caching, game-title queries to Wikimedia Commons, GitHub update
checks, host logging and support email. Retention by external providers is not
invented. Prepared Play 1.0.5 backup/cleartext settings are distinguished from
previous releases. Keep policies aligned with code and Console Data safety.

The terms preserve GPL rights and earlier release licenses. They do not add a
click-through acceptance gate. Google Play is shown as being prepared until
actual store availability; never add a working store badge before that.

Primary references:
- https://support.google.com/googleplay/android-developer/answer/10144311
- https://support.google.com/googleplay/android-developer/answer/9859455
- https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages
- https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site
