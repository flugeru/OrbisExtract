using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace OrbisExtract.App.Localization;

public static class AppLanguage
{
    private static readonly string PreferencePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrbisExtract", "language.txt");
    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["Nenhum pacote selecionado"] = "No package selected",
        ["Jogo"] = "Game", ["Versão"] = "Version", ["Selecionar arquivo PKG"] = "Select PKG file",
        ["Arquivo do pacote"] = "Package file", ["Selecione o PKG para consultar os metadados antes de extrair."] = "Select a PKG to inspect its metadata before extraction.",
        ["Nenhuma pasta escolhida"] = "No folder selected", ["Escolher pasta"] = "Choose folder", ["Extrair arquivos"] = "Extract files",
        ["Configuração"] = "Setup", ["Defina onde salvar os arquivos extraídos."] = "Choose where to save extracted files.",
        ["Pasta de destino"] = "Destination folder", ["Pronto para extrair"] = "Ready to extract",
        ["Aguardando seleção de arquivo e destino."] = "Waiting for a file and destination.", ["Cancelar"] = "Cancel",
        ["Extrair PKG"] = "Extract PKG", ["Ferramenta de extração para pacotes PlayStation 4."] = "Extraction tool for PlayStation 4 packages.",
        ["Fechar"] = "Close", ["Sobre o OrbisExtract"] = "About OrbisExtract", ["Repositório"] = "Repository",
        ["Changelog"] = "Changelog", ["Licença MIT"] = "MIT License", ["Verificar atualizações"] = "Check for updates",
        ["Abrir atualização"] = "Open update", ["Agora não"] = "Not now", ["Atualização do OrbisExtract"] = "OrbisExtract update",
        ["Configurações"] = "Settings", ["Idioma"] = "Language", ["Automático (sistema)"] = "Automatic (system)",
        ["Português"] = "Portuguese", ["Inglês"] = "English", ["Arquivo selecionado."] = "File selected.",
        ["Metadados serão validados durante a extração."] = "Metadata will be validated during extraction.",
        ["Lendo metadados do PKG…"] = "Reading PKG metadata…", ["Metadados do PKG lidos."] = "PKG metadata read.",
        ["Metadados indisponíveis; o pacote ainda pode ser tentado."] = "Metadata unavailable; you can still try extracting the package.",
        ["Selecionar pacote PlayStation PKG"] = "Select PlayStation PKG package", ["Pacotes PKG"] = "PKG packages",
        ["Escolher pasta para os arquivos extraídos"] = "Choose folder for extracted files", ["Preparando"] = "Preparing",
        ["Preparando a extração…"] = "Preparing extraction…", ["Preparando pacote"] = "Preparing package",
        ["Abrindo e validando a estrutura interna do PKG…"] = "Opening and validating the PKG structure…",
        ["Concluindo"] = "Finishing", ["Concluído"] = "Completed", ["Cancelado"] = "Cancelled",
        ["A extração foi interrompida."] = "Extraction was interrupted.", ["Extração cancelada."] = "Extraction cancelled.",
        ["Falha na extração"] = "Extraction failed", ["Veja o log detalhado para mais informações."] = "See the detailed log for more information.",
        ["Você já está na versão mais recente."] = "You already have the latest version.",
        ["Novidades:"] = "What's new:", ["OrbisExtract iniciado."] = "OrbisExtract started.",
        ["Sobre"] = "About", ["Nova versão"] = "New version", ["disponível."] = "available.",
        ["Deseja abrir a página para baixar a atualização?"] = "Would you like to open the download page?",
        ["Não foi possível verificar atualizações:"] = "Could not check for updates:",
        ["Não foi possível abrir o link:"] = "Could not open the link:", ["Navegador indisponível."] = "Browser unavailable.",
        ["de"] = "of", ["arquivos"] = "files", ["Extraindo"] = "Extracting",
        ["Histórico"] = "History", ["Histórico de extrações"] = "Extraction history",
        ["Relatórios locais por pacote PKG"] = "Local reports for each PKG",
        ["Nenhuma extração registrada."] = "No extractions recorded.",
        ["Ver relatório"] = "View report",
        ["Pausar"] = "Pause", ["Retomar"] = "Resume", ["Pausado"] = "Paused",
        ["Extração concluída com sucesso em"] = "Extraction completed successfully in",
        ["Preparando extração"] = "Preparing extraction", ["Extraindo arquivos"] = "Extracting files",
        ["Extração concluída"] = "Extraction completed", ["Destino:"] = "Destination:",
        ["arquivos processados"] = "files processed",
        ["OrbisExtract · Extração"] = "OrbisExtract · Extraction",
        ["Progresso da extração de pacotes PKG em segundo plano"] = "Background PKG extraction progress",
        ["Versão 1.0.3 anunciada apenas para testar a verificação de atualizações; ainda não há download desta versão."] = "Version 1.0.3 is announced only to test update checking; there is no download for this version yet.",
        ["Não foi possível abrir o seletor de arquivos:"] = "Could not open the file picker:",
        ["Não foi possível selecionar a pasta:"] = "Could not select the folder:",
        ["Falha na extração:"] = "Extraction failed:",
        ["Manifesto de atualização inválido."] = "Invalid update manifest.",
        ["Link de atualização inválido ou indisponível para esta plataforma."] = "Invalid update link or unavailable for this platform.",
        ["OrbisExtract não conseguiu iniciar a interface"] = "OrbisExtract could not start the interface",
        ["OrbisExtract — erro de inicialização"] = "OrbisExtract — startup error",
        ["Pacote selecionado:"] = "Package selected:", ["Destino selecionado:"] = "Destination selected:",
        ["Aviso ao abrir o seletor de pacotes:"] = "Warning opening the package picker:",
        ["Aviso ao abrir o seletor de pastas:"] = "Warning opening the folder picker:",
        ["Aviso ao ler metadados:"] = "Warning reading metadata:",
        ["Iniciando extração. Pacote:"] = "Starting extraction. Package:",
        ["Passcode: não utilizado nesta versão."] = "Passcode: not used in this version.",
        ["Lendo o PKG selecionado diretamente, sem copiá-lo."] = "Reading the selected PKG directly without copying it.",
        ["O provedor não permite acesso aleatório; preparando uma cópia temporária necessária para leitura."] = "The provider does not support random access; preparing a temporary copy for reading.",
        ["Falha ao acessar destino:"] = "Could not access destination:",
        ["Extração concluída. Arquivos:"] = "Extraction completed. Files:",
        ["Processo de extração finalizado."] = "Extraction process finished.",
        ["Pasta '"] = "Folder '", ["Criando diretório de saída:"] = "Creating output directory:",
        ["Criando arquivo temporário:"] = "Creating temporary file:",
        ["Caminho interno inseguro:"] = "Unsafe internal path:",
        ["O provedor de armazenamento não forneceu a pasta de destino."] = "The storage provider did not provide a destination folder.",
        ["Pasta pai inválida ao criar '"] = "Invalid parent folder while creating '",
        ["O provedor de armazenamento não conseguiu abrir nem criar o diretório '"] = "The storage provider could not open or create the directory '",
        ["Pasta de destino inválida para '"] = "Invalid destination folder for '",
        ["O provedor de armazenamento não criou o arquivo de destino para '"] = "The storage provider did not create the destination file for '",
        ["Não foi possível abrir o arquivo temporário para escrita: '"] = "Could not open the temporary file for writing: '",
        ["Arquivo temporário de destino inválido."] = "Invalid temporary destination file.",
        ["O arquivo já existe:"] = "File already exists:",
        ["Destino local recebeu um temporário inválido."] = "Local destination received an invalid temporary file.",
        ["• Configurações de idioma: automático, português ou inglês.\n• Idioma inicial de acordo com o sistema; escolha manual salva no aparelho."] = "• Language settings: automatic, Portuguese or English.\n• Initial language follows the system; manual choice is saved on the device."
    };

    public static event Action? Changed;
    public static string Mode { get; private set; } = LoadMode();
    public static bool IsEnglish => (Mode == "system" ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName : Mode) != "pt";

    public static string Text(string portuguese) => IsEnglish && English.TryGetValue(portuguese, out var translated) ? translated : portuguese;

    public static string Message(string message)
    {
        if (!IsEnglish) return message;
        if (English.TryGetValue(message, out var exact)) return exact;
        foreach (var (original, translation) in English)
            if (message.StartsWith(original + " ", StringComparison.Ordinal) || original.EndsWith(' ') && message.StartsWith(original, StringComparison.Ordinal))
                return translation + message[original.Length..];
        return message;
    }

    public static void SetMode(string mode)
    {
        if (mode is not ("system" or "pt" or "en")) throw new ArgumentOutOfRangeException(nameof(mode));
        if (Mode == mode) return;
        Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
        File.WriteAllText(PreferencePath, mode);
        Mode = mode;
        Changed?.Invoke();
    }

    private static string LoadMode()
    {
        try
        {
            var value = File.ReadAllText(PreferencePath).Trim();
            return value is "pt" or "en" ? value : "system";
        }
        catch (IOException) { return "system"; }
        catch (UnauthorizedAccessException) { return "system"; }
    }
}
