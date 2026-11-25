# Apoio pelo Google Play

O OrbisExtract continua gratuito, sem anúncios e sem assinatura. A compra opcional dá apenas um selo de Apoiador na tela Sobre. A extração não depende da compra, da Play Store nem da conexão com a internet.

## Play Console

1. No app `com.flugel.orbisextract`, vincule o perfil de pagamentos e configure a conta bancária de recebimento. Conclua a verificação exigida no painel. O Google processa a cobrança e repassa o líquido segundo as condições da conta.
2. Crie o produto de compra única `orbisextract_apoio`, nome **Apoiador do OrbisExtract**. Crie uma opção do tipo **Compra** (não aluguel) com ID `apoio-unico`, sem promoção e sem compra de múltiplas unidades. Defina **US$ 1,00** no mercado dos Estados Unidos, revise os preços locais e ative a opção nas regiões desejadas. O app exibe o preço formatado retornado pelo Google Play, não um valor fixo nem uma conversão própria.
3. Copie a chave **pública** RSA de licenciamento do app na Play Console (Monetização → Configuração de monetização). Não use a chave de assinatura do app, uma chave privada nem uma credencial de conta de serviço.
4. Para o AAB de teste/produção, forneça `PlayBillingPublicKey` ao MSBuild. Exemplo em PowerShell, assumindo que a versão e a assinatura de upload já estejam configuradas no ambiente:

   ```powershell
   dotnet publish src/OrbisExtract.App/OrbisExtract.App.Android/OrbisExtract.App.Android.csproj -c Release -p:AndroidPackageFormat=aab "-p:PlayBillingPublicKey=$env:ORBIS_PLAY_BILLING_PUBLIC_KEY"
   ```

   Se a chave estiver ausente ou inválida, ou o APK não tiver sido instalado pelo Play, o checkout fica indisponível e o compartilhamento continua funcionando. O fluxo de extração não muda. Não publique um AAB de produção sem passar a chave.
5. Envie o AAB para teste interno e adicione sua conta aos testadores da faixa e aos **testadores de licença**. Instale pela Play Store. Valide preço, cancelamento, aprovação, pagamento pendente, reconhecimento, reinstalação na mesma conta, reembolso/revogação e uso offline. Use somente os meios de teste do Google nessa etapa.

A compra é não consumível. O app consulta a propriedade via Google Play e verifica a assinatura da transação no dispositivo antes de reconhecer a compra concluída. O selo é cosmético e não serve como prova para serviços externos. Não há backend de cobrança nem armazenamento de recibos pelo app. Se o Play estiver indisponível, o app não exibe um estado de compra como confirmado até consultar novamente.

## Agradecimentos no GitHub

O botão **Solicitar menção no GitHub** abre um rascunho de e-mail para o usuário revisar e enviar. Ele não envia automaticamente e não inclui token, recibo ou dados de pagamento. Registre no README apenas o apelido que a pessoa autorizou publicar. Não vincule o e-mail do remetente à lista e remova o apelido se solicitado. A lista é manual, não certifica compras e não deve ser usada como fonte de autorização.

Antes de publicar, revise a ficha de segurança de dados e a política de privacidade da versão Play conforme as práticas efetivas de pagamento e atendimento.
