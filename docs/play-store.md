# OrbisExtract — preparação para Google Play

Pacote: `com.tatsuya.orbisextract`. Versão: `1.0.5`. Primeiro AAB preparado com
`versionCode=1051000`, acima do CI observado (`1050368`, run 368). Antes de outro
envio, confirme os códigos efetivamente distribuídos e sempre aumente o código.
Android mínimo: 7.0 (API 24). Alvo explícito: API 36. AAB: ARM64 e x86-64.

## Assinatura e continuidade

A nova chave `orbisextract-upload.jks`, alias `orbisextract-upload`, autentica o
envio de AAB. A senha está somente na entrega privada, nunca no repositório.
Guarde o arquivo e a senha com backup. O certificado PEM pode ser cadastrado na
Play Console. Não substitua `ANDROID_SIGNING_KEYSTORE_BASE64`: é a chave dos APKs
GitHub existentes. O CI atual usa alias `androiddebugkey` e senha convencional;
é uma chave de desenvolvimento persistente, inadequada para importar como nova
chave de produção Play sem revisão. Criar outra chave não atualiza os APKs antigos.

Para a primeira publicação Play, use Play App Signing e deixe o Google criar a
chave de assinatura do app, separada da upload key. Isso mantém o GitHub legado
e a Play em linhas de assinatura diferentes. Não prometa atualização por cima
entre essas linhas. Para ter APKs compatíveis com a Play no GitHub, obtenha o
APK universal assinado na Play Console e distribua esse APK (ele não substituirá
os APKs GitHub legados com certificado diferente). Migrar instalações antigas
entre canais pode exigir reinstalação e preservação dos arquivos/histórico.
Uma migração com continuidade dos antigos exigiria recuperar e avaliar a chave
original e configurar o Play App Signing antes do primeiro lançamento; ela não
foi acessada e não pode ser recuperada a partir de um APK ou dos Secrets.

## Gerar novos AABs

Instale .NET 10, workload Android, JDK e Android SDK 36. Execute
`scripts/publish-play.sh` com as variáveis descritas nele. Senhas são fornecidas
por arquivo para evitar exposição no comando. O canal Play é fixado na compilação.
O workflow manual `play-aab.yml` usa os Secrets novos:

- `PLAY_UPLOAD_KEYSTORE_BASE64`: bytes da nova chave em Base64.
- `PLAY_UPLOAD_KEY_PASSWORD`: senha da chave e do keystore.

Os Secrets não foram configurados automaticamente. O workflow gera o artefato,
mas não publica na loja. A automação legada de APK continua com sua chave atual.
Execute `scripts/verify-play-bundle.py arquivo.aab` para conferir os segmentos
ELF em 16 KB, além de `bundletool validate` e `bundletool dump config`.

## Atualizações

- GitHub: abre a release; instalação manual pelo Android, com mesma chave e
  código superior. O app não baixa nem instala APK e não pede REQUEST_INSTALL_PACKAGES.
- Play: abre somente a página do pacote na Play; atualizações automáticas ficam
  com a loja. Não há Play Core/in-app updates nesta implementação.
- `update.json` anuncia versões independentes por canal. O canal Play começa
  com `published:false`. Ative somente quando disponível para os usuários-alvo;
  em rollout gradual, espere a disponibilidade completa. Publique os artefatos
  antes de anunciar a versão. Clientes antigos continuam lendo a versão raiz.

## Ficha da loja e privacidade

Textos PT-BR e EN-US em `packaging/play/`. Categoria sugerida: Ferramentas.
Sem anúncios e sem login no código revisado. Sem compras nesta versão.
Contato de suporte e privacidade autorizado: `fluugell@gmail.com`. Informe o mesmo e-mail na Console.
A classificação indicativa e público-alvo devem ser respondidos pelo titular.
O app não fornece jogos ou downloads PKG. A descrição informa o uso autorizado.

A política bilíngue está em `docs/privacy-policy.html`; o app abre
`https://flugeru.github.io/OrbisExtract/privacy-policy.html` em Sobre.
O site oficial está publicado por GitHub Actions em
`https://flugeru.github.io/OrbisExtract/`. A política foi verificada publicamente
em `https://flugeru.github.io/OrbisExtract/privacy-policy.html`, sem login,
com contato `fluugell@gmail.com`. Termos: `/OrbisExtract/terms.html`;
suporte: `/OrbisExtract/support.html`. A URL de privacidade já corresponde ao
link incluído no AAB preparado; não é necessário reconstruí-lo só por esta
publicação. Os termos preservam os direitos GPL e as licenças das versões antigas.
Para atualizar o site, edite os arquivos em `docs/`; o workflow Pages valida
links e publica apenas os arquivos web permitidos.

Não marque “nenhum dado coletado” sem revisar o formulário: a busca automática
de capas envia o título do jogo ao Wikimedia Commons; conexões de atualização
e de capas revelam IP e dados técnicos a terceiros. Os arquivos PKG não são
enviados a um servidor do projeto. Histórico e relatórios ficam localmente.
Para Segurança dos dados, avalie título pesquisado como histórico de pesquisa
no app, IP conforme a finalidade e retenção dos provedores, processamento
transitório, finalidade funcional e as exceções aplicáveis a compartilhamento.
Não presumimos a retenção dos logs GitHub/Wikimedia. A política os descreve.
Todas as conexões implementadas usam HTTPS; cleartext e backup automático dos
dados privados foram desativados no manifesto Android.

## Serviço em primeiro plano

A extração usa `dataSync` por ser processamento local de arquivos iniciado pelo
usuário. O serviço não é exportado, tem notificação e controles, e cancela e
encerra em `OnTimeout`. A API 35+ limita esse tipo em segundo plano; não prometa
execução ilimitada. Na Console, preencha a declaração de foreground service e
gravar vídeo real: selecionar PKG e destino, iniciar, sair do app, mostrar a
notificação e cancelar/concluir. Não há inicialização por boot no código.

## Antes do envio

1. Integrar as mudanças de preparação do app do PR; o site e a política já estão publicados.
2. Preencher o contato, classificação, público-alvo, Segurança dos dados e FGS.
3. Usar o ícone 512×512 e feature graphic 1024×500 da entrega; conferir titularidade.
4. Capturar ao menos duas screenshots reais do app Android; não substituir por mockups.
5. Enviar o AAB para teste interno, ativar Play App Signing e conferir o certificado.
6. Verificar relatório pré-lançamento e testar Android 7/13/15/16, POCO/HyperOS,
   rotação, seleção de pastas, falta de espaço, vários PKGs, pausa e cancelamento.
7. Testar instalação e atualização reais pelo Play e pelo GitHub, sem misturar chaves;
   testar também em dispositivo/emulador de páginas de 16 KB. Análise estática não
   substitui teste de execução nesse ambiente.
8. Para contas pessoais novas sujeitas à regra, cumprir teste fechado com ao menos
   12 testadores por 14 dias contínuos antes de pedir acesso à produção.
9. Publicar código-fonte correspondente ao binário GPL-3.0 e preservar os avisos.
10. Só depois da aprovação/disponibilidade atualizar o manifesto público do canal.

## Fontes oficiais consultadas em 22/11/2026

- https://developer.android.com/google/play/requirements/target-sdk
- https://developer.android.com/guide/practices/page-sizes
- https://support.google.com/googleplay/android-developer/answer/9842756
- https://support.google.com/googleplay/android-developer/answer/10144311
- https://support.google.com/googleplay/android-developer/answer/10787469
- https://support.google.com/googleplay/android-developer/answer/9866151
- https://support.google.com/googleplay/android-developer/answer/14151465
- https://learn.microsoft.com/dotnet/android/building-apps/build-properties
