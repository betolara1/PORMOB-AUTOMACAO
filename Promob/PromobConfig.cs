using System;
using System.IO;

namespace AutomacaoPromobTeste.Promob{
    public static class PromobConfig{
        // --- Pastas e Caminhos ---
        public static readonly string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        public static readonly string PastaPromob = Path.Combine(DesktopPath, "promob");
        public static readonly string PastaXml = Path.Combine(DesktopPath, "xml");

        // --- Timeouts e Intervalos ---
        public const int TimeoutCurto = 2000;
        public const int TimeoutPadrao = 5000;
        public const int TimeoutLongo = 10000;
        public const int TimeoutExportacaoErp = 35 * 60 * 1000; // 35 minutos (projetos grandes podem levar até 30min)
        public const int PollMs = 200;
        public const int DelayMinimo = 150;

        // --- Automation IDs ---
        public const string AutomationIdHost = "elementHost1";
        public const string IdFileTab = "FileTab";
        public const string IdProjectClose = "ProjectClose";
        public const string IdToolsTab = "ToolsTab";
        public const string IdOrcamentoToggle = "PART_ToggleButton";
        public const string IdOrcamentoMenu = "bOrçamento";
        public const string IdOrcamentoMenuAlt = "bOrcamento";
        public const string IdImportarBotao = "ProjectImport";
        public const string IdBrowseButton = "BrowseButton";
        public const string IdCampoArquivoWin = "1148";
        public const string IdHostCampoArquivo = "FileNameControlHost";
        public const string IdBtnAbrirWin = "1";

        // --- Nomes de Elementos (UI Names) ---
        public const string NomeJanelaWizardImportacao = "Importar projeto";
        public const string AbaArquivo = "Arquivo";
        public const string AbaFerramentas = "Ferramentas";
        public const string SecaoOrcamento = "Orçamento";
        public const string SecaoOrcamentoAlt = "Orcamento";
        public const string BotaoIntegradores = "Integradores";
        public const string MenuPromobErp = "Promob ERP";
        public const string MsgExportacaoSucesso = "completado com sucesso";
        public const string NomePastaXmlExport = "01_XML";
        
        public const string BtnFechar = "Fechar";
        public const string BtnAvancar = "Avançar";
        public const string BtnAvancarAlt = "Avancar";
        public const string BtnNext = "Next";
        public const string BtnProcurar = "...";
        public const string BtnProcurarTexto = "Procurar";
        public const string BtnAbrir = "Abrir";
        public const string BtnOpen = "Open";
        public const string BtnSalvarComo = "Salvar Como";
        public const string BtnSaveAs = "Save As";
        public const string BtnNao = "Não";
        public const string BtnNaoAlt = "Nao";
        public const string BtnNo = "No";
        public const string BtnOk = "OK";
        public const string BtnOkAlt = "Ok";
        public const string BtnSim = "Sim";
        public const string BtnConcluir = "Concluir";
        public const string BtnCancelar = "Cancelar";
        
        // --- Campos do Wizard ---
        public const string NameCampoCaminho = "Caminho";
        public const string NameCampoChave = "Chave de importação";
        public const string BtnAbrirProjeto = "Abrir projeto";

        // --- Textos de Validação e Popups ---
        public const string MsgCarregandoItens = "Alguns itens ainda estão sendo carregados";
        public const string MsgConfirmarCancelamento = "cancelar";
        
        // Filtros de Títulos de Janelas e Diálogos
        public static readonly string[] TitulosAviso = { "Aviso", "Erro", "Atenção", "Atencao", "Atençao", "Confirmação", "Confirmacao", "Salvar", "Save" };
        public static readonly string[] TermosDialogoArquivo = { "Abrir", "Open", "Salvar Como", "Save As" };
    }
}
