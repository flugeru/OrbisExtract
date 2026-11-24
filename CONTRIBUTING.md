# Contributing to OrbisExtract

**[English](#english)** · **[Português (Brasil)](#português-brasil)**

## English

### Report a problem

Search [existing issues](https://github.com/flugeru/OrbisExtract/issues) first. Include the OrbisExtract version, platform and architecture, what you expected, what happened, and steps to reproduce. For extraction issues, indicate whether you selected a base game alone or a base with updates, and include the title ID, package version, and relevant log lines if available.

Redact personal paths and credentials. Do not attach PKGs, extracted game files or signing material. Follow the [security policy](SECURITY.md) for vulnerabilities.

### Submit a change

1. Branch from `main` and keep the pull request focused on one change.
2. Add tests for extraction, update ordering or file output changes. Use synthetic fixtures rather than game data.
3. Update English and Brazilian Portuguese strings or public documentation when changing user-facing behavior. Include screenshots for visual changes where useful.
4. In the pull request, list the platforms and checks you ran and describe any validation still needed.

Run `dotnet test OrbisExtract.sln -c Release` for desktop and core changes. Run `python3 scripts/check-site.py` for site changes. See the [build instructions](README.md#building) for tooling and platform requirements.

## Português (Brasil)

### Relatar um problema

Pesquise nas [issues existentes](https://github.com/flugeru/OrbisExtract/issues). Informe a versão do OrbisExtract, a plataforma e a arquitetura, o resultado esperado, o que aconteceu e os passos para reproduzir. Para problemas de extração, diga se selecionou só o jogo base ou também updates. Inclua o ID do título, a versão do pacote e as linhas relevantes do log, se disponíveis.

Oculte caminhos pessoais e credenciais. Não anexe PKGs, arquivos de jogos extraídos nem material de assinatura. Para vulnerabilidades, siga a [política de segurança](SECURITY.md).

### Enviar uma alteração

1. Crie uma branch a partir de `main` e mantenha o pull request focado em uma alteração.
2. Adicione testes para mudanças na extração, na ordem dos updates ou nos arquivos de saída. Use dados de teste sintéticos, não arquivos de jogos.
3. Atualize os textos ou a documentação pública em inglês e PT-BR quando mudar algo visível ao usuário. Inclua capturas para mudanças visuais quando forem úteis.
4. No pull request, informe as plataformas e os testes executados, além do que ainda precisa ser validado.

Execute `dotnet test OrbisExtract.sln -c Release` para alterações no desktop ou no núcleo. Para o site, execute `python3 scripts/check-site.py`. Consulte as [instruções de compilação](README.md#compilar) para os requisitos de cada plataforma.
