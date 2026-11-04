# OrbisExtract 1.0.4

## Português (Brasil)

- Extração mais rápida com AES-XTS em lotes, menos cópias e leitura Android com buffer.
- Pausar, retomar e cancelar, inclusive pela notificação Android; melhorias de layout para HyperOS.
- Histórico local com resultado, duração e relatório por extração.
- Barra personalizada no desktop, sem faixa cinza superior no Windows, mantendo suporte a Snap e redimensionamento.
- Instalador Windows escuro, identidade visual e licença bilíngue.
- Correções de layout e traduções; diagnóstico de desempenho no log.
- Assinatura Android persistente em GitHub Actions Secrets e código de versão crescente nos builds do CI.

Teste informado pelo usuário: o mesmo PKG passou de 03:29 para cerca de 44 s no POCO X6 Pro com memória interna. Resultados variam por dispositivo, pacote e armazenamento.

**Android:** APKs antigos com assinatura diferente podem exigir reinstalação. Preserve dados importantes antes de desinstalar. Builds novos com a mesma assinatura permitem atualização por cima; builds mais antigos podem ser recusados por código de versão inferior. APK GitHub e eventual distribuição Google Play não devem ser considerados intercambiáveis sem conferir a assinatura.

## English

- Faster extraction with batched AES-XTS, fewer copies and buffered Android input.
- Pause, resume and cancel, including Android notification controls and HyperOS layout improvements.
- Local extraction history with outcomes, durations and per-extraction reports.
- Custom desktop title bar without the gray Windows top strip, retaining Snap and resizing support.
- Dark branded Windows installer and bilingual license.
- Layout/translation fixes and performance diagnostics in the extraction log.
- Persistent Android signing through GitHub Actions Secrets and increasing CI version codes.

User-reported test: the same PKG improved from 03:29 to about 44 seconds on a POCO X6 Pro using internal storage. Results vary by device, package and storage.

**Android:** older APKs with different signatures may require reinstallation. Preserve important data before uninstalling. New builds with the same signing identity support in-place updates; older builds may be rejected due to lower version codes. GitHub and any future Google Play distribution are not necessarily signature-compatible.

## Downloads

- `OrbisExtract-1.0.4-windows-x64-setup.exe`
- `OrbisExtract-1.0.4-windows-x64-portable.zip`
- `OrbisExtract-1.0.4-x86_64.AppImage`
- `OrbisExtract-1.0.4-signed.apk`
- Public Android signing certificate and package metadata.
