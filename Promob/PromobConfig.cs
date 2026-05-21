using System;
using System.IO;

namespace AutomacaoPromobTeste.Promob{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Classe centralizadora de configurações, constantes e caminhos do Promob.
        /// Evita o uso de "Magic Strings" e "Magic Numbers" espalhados pelo código, facilitando futuras manutenções.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class PromobConfig{


        // ==========================================
        // --- Pastas e Caminhos ---
        // ==========================================

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Caminho absoluto para a pasta Desktop (Área de Trabalho) do usuário ativo do Windows.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public static readonly string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Caminho completo para o diretório de destino do Promob na Área de Trabalho.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public static readonly string PastaPromob = Path.Combine(DesktopPath, "promob");

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Caminho completo para o diretório temporário/armazenamento de arquivos XML na Área de Trabalho.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public static readonly string PastaXml = Path.Combine(DesktopPath, "xml");

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Caminho completo para o diretório de arquivos que falharam na exportação.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public static readonly string PastaPromobErro = Path.Combine(DesktopPath, "promob erro");



        // ==========================================
        // --- Timeouts e Intervalos ---
        // ==========================================
        
        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Tempo de espera padrão curto (2 segundos) para interações de transição imediata de telas.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const int TimeoutCurto = 2000;

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Tempo de espera padrão (5 segundos) para carregamentos comuns de janelas e componentes da interface gráfica.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const int TimeoutPadrao = 5000;

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Tempo de espera longo (10 segundos) para modais lentos ou processamentos locais leves.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const int TimeoutLongo = 10000;

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Tempo máximo limite de espera para o término da exportação do ERP do Promob (35 minutos). 
            /// Projetos muito robustos e complexos podem consumir até 30 minutos de renderização de dados.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const int TimeoutExportacaoErp = 35 * 60 * 1000; 

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Intervalo de tempo em milissegundos para polling (verificações periódicas em loops de espera ativa).
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const int PollMs = 200;

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Delay padrão de segurança em milissegundos para dar "fôlego" à interface do sistema operacional entre comandos (150 milissegundos).
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const int DelayMinimo = 150;



        // ==========================================
        // --- Automation IDs (UIA Nativo) ---
        // ==========================================
        // IDs imutáveis definidos pelo desenvolvedor do Promob. São as âncoras mais seguras para buscar botões.
        
        //--------------------------------------------------------------------------------------
            /// <summary>
            /// ID de automação da área de conteúdo/container principal do Promob.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string AutomationIdHost = "elementHost1";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// ID de automação da aba 'Arquivo' (Menu Superior).
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string IdFileTab = "FileTab";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// ID de automação do botão para fechar o projeto ativo.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string IdProjectClose = "ProjectClose";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// ID de automação da aba 'Ferramentas' (Menu Superior).
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string IdToolsTab = "ToolsTab";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// ID de automação do botão alternador (Toggle) da seção de orçamentos.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string IdOrcamentoToggle = "PART_ToggleButton";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// ID primário de automação do botão de menu de Orçamento.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string IdOrcamentoMenu = "bOrçamento";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// ID alternativo de automação do botão de menu de Orçamento (sem caractere especial/acentuação).
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string IdOrcamentoMenuAlt = "bOrcamento";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// ID de automação do botão para iniciar a importação de projetos.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string IdImportarBotao = "ProjectImport";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// ID de automação do botão 'Procurar' (Browse) no assistente de importação.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string IdBrowseButton = "BrowseButton";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// ID nativo da caixa de texto do Windows FileDialog para inserção direta do caminho de arquivo.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string IdCampoArquivoWin = "1148";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// ID de automação do container que engloba a caixa de texto de seleção de arquivo.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string IdHostCampoArquivo = "FileNameControlHost";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// ID nativo do botão clássico 'Abrir' nas janelas padrões de arquivo do Windows (FileDialog).
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string IdBtnAbrirWin = "1";



        // ==========================================
        // --- Nomes de Elementos (UI Names / Fallback) ---
        // ==========================================
        // Títulos de telas e textos de botões exibidos na interface gráfica visível ao usuário.
            
        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Título da janela principal do assistente de importação do Promob.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string NomeJanelaWizardImportacao = "Importar projeto";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Título visível da aba 'Arquivo'.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string AbaArquivo = "Arquivo";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Título visível da aba 'Ferramentas'.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string AbaFerramentas = "Ferramentas";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Título visível da seção de 'Orçamento'.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string SecaoOrcamento = "Orçamento";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Título visível da seção de 'Orçamento' (sem caractere especial/acentuação).
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string SecaoOrcamentoAlt = "Orcamento";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Nome visível do botão 'Integradores'.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string BotaoIntegradores = "Integradores";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Nome visível da opção de menu dropdown 'Promob ERP'.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string MenuPromobErp = "Promob ERP";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Mensagem de validação de exportação bem sucedida emitida no log ou janela de status do Promob.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string MsgExportacaoSucesso = "completado com sucesso";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Mensagem de erro da exportação ERP do Promob ("Abortado com erro!").
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string MsgExportacaoErro = "Abortado com erro";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Nome do diretório gerado para exportação das informações estruturais dos XMLs.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string NomePastaXmlExport = "01_XML";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome do botão 'Fechar'. </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnFechar = "Fechar";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome do botão 'Avançar'. </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnAvancar = "Avançar";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome alternativo para o botão 'Avançar' (sem acento). </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnAvancarAlt = "Avancar";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome em inglês para o botão 'Next' (Avançar). </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnNext = "Next";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Símbolo clássico de busca de arquivo '...'. </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnProcurar = "...";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome legível do botão de procurar arquivos. </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnProcurarTexto = "Procurar";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome do botão 'Abrir'. </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnAbrir = "Abrir";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome em inglês do botão 'Open' (Abrir). </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnOpen = "Open";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome do botão 'Salvar Como'. </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnSalvarComo = "Salvar Como";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome em inglês do botão 'Save As' (Salvar Como). </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnSaveAs = "Save As";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome do botão 'Não'. </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnNao = "Não";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome alternativo para o botão 'Não' (sem acento). </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnNaoAlt = "Nao";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome em inglês do botão 'No' (Não). </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnNo = "No";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome do botão 'OK'. </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnOk = "OK";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome alternativo do botão 'Ok'. </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnOkAlt = "Ok";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome do botão 'Sim'. </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnSim = "Sim";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome do botão 'Concluir'. </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnConcluir = "Concluir";
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome do botão 'Cancelar'. </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnCancelar = "Cancelar";
        

        
        // ==========================================
        // --- Campos de Formulário do Wizard ---
        // ==========================================
        
        //--------------------------------------------------------------------------------------
            /// <summary> Nome do campo destinado ao preenchimento de caminhos de arquivos. </summary>
        //--------------------------------------------------------------------------------------
        public const string NameCampoCaminho = "Caminho";

        //--------------------------------------------------------------------------------------
            /// <summary> Nome do campo destinado ao preenchimento da chave de identificação do projeto. </summary>
        //--------------------------------------------------------------------------------------
        public const string NameCampoChave = "Chave de importação";

        //--------------------------------------------------------------------------------------
            /// <summary> Nome do botão para abrir um projeto selecionado. </summary>
        //--------------------------------------------------------------------------------------
        public const string BtnAbrirProjeto = "openProjectAction";

        // ==========================================
        // --- Textos de Validação e Popups ---
        // ==========================================
                
        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Texto de aviso que indica que o Promob está terminando o carregamento dos elementos de cena antes de liberar a UI.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string MsgCarregandoItens = "Alguns itens ainda estão sendo carregados";

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Texto interno usado para validação e interceptação de cancelamentos de diálogos.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public const string MsgConfirmarCancelamento = "cancelar";
        
        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Coleção de títulos de janelas comuns que indicam modais nativos de aviso, erro ou atenção do Promob.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public static readonly string[] TitulosAviso = { "Aviso", "Erro", "Atenção", "Atencao", "Atençao", "Confirmação", "Confirmacao", "Salvar", "Save" };

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Coleção de títulos de janelas do sistema operacional focadas em seleção ou gravação de arquivos em disco.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public static readonly string[] TermosDialogoArquivo = { "Abrir", "Open", "Salvar Como", "Save As" };
    }
}
