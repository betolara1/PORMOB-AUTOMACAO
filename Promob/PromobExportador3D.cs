using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using PromobAutomacao.Automation;
using PromobAutomacao.Utils;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace PromobAutomacao.Promob{
    //--------------------------------------------------------------------------------------
    /// <summary>
    /// Componente responsável por gerenciar a exportação 3D do projeto no Promob,
    /// clicando no botão "Exportar" e preenchendo as opções do assistente de exportação 3D.
    /// </summary>
    //--------------------------------------------------------------------------------------
    public static class PromobExportador3D{

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Verifica se o título de uma janela corresponde ao assistente de exportação 3D.
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static bool EhJanelaExportacao3D(string nome){
            if (string.IsNullOrWhiteSpace(nome)) return false;
            return nome.Contains("Exportação", StringComparison.OrdinalIgnoreCase) ||
                   nome.Contains("Exportacao", StringComparison.OrdinalIgnoreCase) ||
                   nome.Contains("Assistente para", StringComparison.OrdinalIgnoreCase);
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Executa a rotina de exportação 3D, selecionando a aba de Arquivo, clicando em Exportar,
        /// marcando a opção "Agrupar layer por módulo" e avançando no assistente.
        /// </summary>
        /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        /// <param name="janela">A janela principal ativa do Promob.</param>
        //--------------------------------------------------------------------------------------
        public static void Exportar(UIA3Automation automation, Window janela){
            InteractionHelper.AtivarJanela(janela);

            var raizBusca = WindowFinder.ObterHostOuJanela(janela, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);

            AppLogs.LogExportador3DSelecionandoAbaArquivo();
            var abaArquivo = WindowFinder.BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)
                        .And(cf.ByAutomationId(PromobConfig.IdFileTab).Or(cf.ByName(PromobConfig.AbaArquivo))),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.TabItem &&
                     ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals(PromobConfig.IdFileTab, StringComparison.OrdinalIgnoreCase) ||
                      (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.AbaArquivo, StringComparison.OrdinalIgnoreCase)),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            if (abaArquivo != null){
                InteractionHelper.SelecionarOuClicar(abaArquivo);
                InteractionHelper.EsperarUiRespirar(800);
            }

            AppLogs.LogExportador3DProcurandoBotaoExportar();
            var btnExportar = WindowFinder.BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                        .And(cf.ByAutomationId("ProjectExport").Or(cf.ByName("Exportar")).Or(cf.ByName("Export"))),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                     ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals("ProjectExport", StringComparison.OrdinalIgnoreCase) ||
                      (e.Properties.Name.ValueOrDefault ?? "").Equals("Exportar", StringComparison.OrdinalIgnoreCase) ||
                      (e.Properties.Name.ValueOrDefault ?? "").Equals("Export", StringComparison.OrdinalIgnoreCase)),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            if (btnExportar != null){
                AppLogs.LogExportador3DBotaoEncontrado();
                InteractionHelper.ClicarComFallback(btnExportar);
                InteractionHelper.EsperarUiRespirar(500);

                // Espera e clica na opção "Arquivo 3D" no menu suspenso que abriu
                AppLogs.LogExportador3DProcurandoOpcaoArquivo3D();
                AutomationElement? optArquivo3D = null;
                bool encontrouOpcao = InteractionHelper.EsperarAte(() => {
                    try {
                        var desktop = automation.GetDesktop();
                        optArquivo3D = janela.FindFirstDescendant(cf => cf.ByName("Arquivo 3D"))
                                    ?? desktop.FindFirstDescendant(cf => cf.ByName("Arquivo 3D"));
                        return optArquivo3D != null;
                    } catch { return false; }
                }, timeoutMs: 5000, intervaloMs: 300);

                if (encontrouOpcao && optArquivo3D != null){
                    AppLogs.LogExportador3DSelecionandoOpcaoArquivo3D();
                    InteractionHelper.ClicarComFallback(optArquivo3D);
                    InteractionHelper.EsperarUiRespirar(500);
                }
                else {
                    throw new Exception("Opção 'Arquivo 3D' não encontrada no menu suspenso após clicar em Exportar.");
                }
            }
            else {
                throw new Exception("Botão 'Exportar' não encontrado no menu/ribbon do Promob.");
            }

            AppLogs.LogExportador3DAguardandoJanelaWizard();
            Window? janelaWizard = null;
            bool encontrouWizard = InteractionHelper.EsperarAte(() => {
                try {
                    var desktop = automation.GetDesktop();

                    // Tentativa 1: Janela de topo no Desktop (janelas modais WPF flutuam no Desktop)
                    var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
                    foreach (var j in janelas) {
                        if (PromobWindowHelper.CachedProcessIdPromob.HasValue && j.Properties.ProcessId.ValueOrDefault != PromobWindowHelper.CachedProcessIdPromob.Value)
                            continue;

                        var name = j.Name ?? "";
                        if (EhJanelaExportacao3D(name)) {
                            janelaWizard = j.AsWindow();
                            return true;
                        }
                    }

                    // Tentativa 2: Janela-filha direta da janela principal do Promob (WinForms dialogs)
                    var filhasJanela = janela.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
                    foreach (var f in filhasJanela) {
                        var name = f.Name ?? "";
                        if (EhJanelaExportacao3D(name)) {
                            janelaWizard = f.AsWindow();
                            return true;
                        }
                    }

                    // Tentativa 3: Descendente raso (até nível 3) da janela principal
                    var descendentes = WindowFinder.BuscarAteNivel(janela, maxNivel: 3);
                    var wizardDesc = descendentes.FirstOrDefault(e =>
                        e.ControlType == FlaUI.Core.Definitions.ControlType.Window &&
                        EhJanelaExportacao3D(e.Properties.Name.ValueOrDefault ?? ""));
                    if (wizardDesc != null) {
                        janelaWizard = wizardDesc.AsWindow();
                        return true;
                    }
                } catch { }
                return false;
            }, timeoutMs: 15000, intervaloMs: 500);

            if (!encontrouWizard || janelaWizard == null){
                throw new Exception("Janela 'Assistente para Exportação de Arquivo 3D' não apareceu.");
            }

            AppLogs.LogExportador3DJanelaWizardEncontrada();
            InteractionHelper.AtivarJanela(janelaWizard);
            InteractionHelper.EsperarUiRespirar(500);

            // --- Selecionar RadioButton "Agrupar layer por módulo" ---
            AppLogs.LogExportador3DProcurandoOpcaoModulo();

            // Busca direta por nome exato
            var optAgruparModulo = janelaWizard.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.RadioButton)
                  .And(cf.ByName("Agrupar layer por módulo").Or(cf.ByName("Agrupar layer por modulo"))));

            if (optAgruparModulo == null) {
                // Fallback: busca todos os RadioButtons e filtra por texto parcial
                var allRadioButtons = janelaWizard.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.RadioButton));
                optAgruparModulo = allRadioButtons.FirstOrDefault(r => 
                    (r.Name ?? "").Contains("modulo", StringComparison.OrdinalIgnoreCase) || 
                    (r.Name ?? "").Contains("módulo", StringComparison.OrdinalIgnoreCase));
            }

            if (optAgruparModulo == null) {
                // Fallback extremo: busca qualquer elemento clicável com texto "módulo"
                var todosElementos = janelaWizard.FindAllDescendants();
                optAgruparModulo = todosElementos.FirstOrDefault(e =>
                    (e.Name ?? "").Contains("módulo", StringComparison.OrdinalIgnoreCase) ||
                    (e.Name ?? "").Contains("modulo", StringComparison.OrdinalIgnoreCase));
            }

            if (optAgruparModulo != null){
                AppLogs.LogExportador3DSelecionandoOpcaoModulo();
                InteractionHelper.SelecionarOuClicar(optAgruparModulo);
                InteractionHelper.EsperarUiRespirar(500);
            }
            else {
                throw new Exception("Opção 'Agrupar layer por módulo' não encontrada na janela do assistente.");
            }

            // --- Página 1 → 2: Clicar "Avançar" para ir à "Detalhamento de Exportação" ---
            AppLogs.LogExportador3DAvancandoDetalhamento();
            ClicarAvancar(janelaWizard);

            // --- Página 2 → 3: Clicar "Avançar" para ir à "Filtros" ---
            AppLogs.LogExportador3DAvancandoFiltros();
            ClicarAvancar(janelaWizard);

            // --- Processar "Filtro de Camadas": desmarcar tudo, marcar só ESPECIAIS ---
            AppLogs.LogExportador3DProcessandoFiltroCamadas();
            ProcessarFiltroCamadas(janelaWizard);

            // --- Clicar no botão "Concluir" ---
            AppLogs.LogExportador3DProcurandoBotaoConcluir();
            var btnConcluir = janelaWizard.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                  .And(cf.ByName("Concluir").Or(cf.ByName("Finish"))));

            if (btnConcluir == null) {
                var todosBotoes = janelaWizard.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
                btnConcluir = todosBotoes.FirstOrDefault(b =>
                    (b.Name ?? "").Contains("Concluir", StringComparison.OrdinalIgnoreCase) ||
                    (b.Name ?? "").Contains("Finish", StringComparison.OrdinalIgnoreCase));
            }

            if (btnConcluir != null){
                AppLogs.LogExportador3DClicandoBotaoConcluir();
                InteractionHelper.ClicarComFallback(btnConcluir);
                InteractionHelper.EsperarUiRespirar(1000);
            }
            else {
                throw new Exception("Botão 'Concluir' não encontrado na janela do assistente.");
            }

            // --- Verificar se a popup de erro "Não há módulos para serem exportados" apareceu ---
            AppLogs.LogExportador3DVerificandoPopupErro();
            Window? janelaPopup = null;
            bool encontrouPopup = InteractionHelper.EsperarAte(() => {
                try {
                    var desktop = automation.GetDesktop();
                    var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
                    foreach (var j in janelas) {
                        if (PromobWindowHelper.CachedProcessIdPromob.HasValue && j.Properties.ProcessId.ValueOrDefault != PromobWindowHelper.CachedProcessIdPromob.Value)
                            continue;

                        var name = j.Name ?? "";
                        if (name.Equals("Exportar", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Não há módulos", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("módulos", StringComparison.OrdinalIgnoreCase)) {
                            janelaPopup = j.AsWindow();
                            return true;
                        }
                    }
                } catch { }
                return false;
            }, timeoutMs: 3000, intervaloMs: 200);

            if (encontrouPopup && janelaPopup != null){
                AppLogs.LogExportador3DPopupErroDetectado();
                InteractionHelper.AtivarJanela(janelaPopup);
                InteractionHelper.EsperarUiRespirar(200);

                var btnOk = janelaPopup.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                      .And(cf.ByName("OK").Or(cf.ByName("Ok")).Or(cf.ByName("Sim"))))
                      ?? janelaPopup.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)).FirstOrDefault();

                if (btnOk != null){
                    AppLogs.LogExportador3DClicandoBotaoOkPopup();
                    InteractionHelper.ClicarComFallback(btnOk);
                }
                else {
                    Keyboard.Type(VirtualKeyShort.ENTER);
                }
                InteractionHelper.EsperarUiRespirar(800);
            }
            else {
                AppLogs.LogExportador3DPopupErroNaoDetectado();
            }

            // --- Se o assistente (wizard) ainda estiver aberto, fechar via Cancelar para limpar a tela ---
            InteractionHelper.EsperarUiRespirar(500);
            try {
                if (janelaWizard != null && !janelaWizard.IsOffscreen) {
                    AppLogs.LogExportador3DFechandoWizardRestante();
                    var btnCancelar = janelaWizard.FindFirstDescendant(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                          .And(cf.ByName("Cancelar").Or(cf.ByName("Cancel"))));

                    if (btnCancelar != null) {
                        InteractionHelper.ClicarComFallback(btnCancelar);
                    }
                    else {
                        janelaWizard.Close();
                    }
                    InteractionHelper.EsperarUiRespirar(800);
                }
            } catch { }
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Procura e clica no botão "Avançar >" dentro da janela do assistente.
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static void ClicarAvancar(Window janelaWizard){
            InteractionHelper.EsperarUiRespirar(800);

            var btnAvancar = janelaWizard.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                  .And(cf.ByName(PromobConfig.BtnAvancar).Or(cf.ByName(PromobConfig.BtnAvancarAlt)).Or(cf.ByName(PromobConfig.BtnNext))));

            if (btnAvancar == null) {
                var todosBotoes = janelaWizard.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
                btnAvancar = todosBotoes.FirstOrDefault(b =>
                    (b.Name ?? "").Contains("Avançar", StringComparison.OrdinalIgnoreCase) ||
                    (b.Name ?? "").Contains("Avancar", StringComparison.OrdinalIgnoreCase) ||
                    (b.Name ?? "").Contains("Next", StringComparison.OrdinalIgnoreCase));
            }

            if (btnAvancar != null){
                AppLogs.LogExportador3DClicandoBotaoAvancar();
                InteractionHelper.ClicarComFallback(btnAvancar);
                InteractionHelper.EsperarUiRespirar(1000);
            }
            else {
                throw new Exception("Botão 'Avançar' não encontrado na janela do assistente.");
            }
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Na página "Filtros" do assistente, desmarca todos os checkboxes de "Filtro de Camadas"
        /// e marca apenas o item "ESPECIAIS", se existir.
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static void ProcessarFiltroCamadas(Window janelaWizard){
            InteractionHelper.EsperarUiRespirar(500);

            // Busca todos os checkboxes dentro da janela do assistente
            var todosCheckboxes = janelaWizard.FindAllDescendants(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.CheckBox));

            if (todosCheckboxes == null || todosCheckboxes.Length == 0){
                // Fallback: em controles TreeView/ListView, os itens podem ser TreeItem ou ListItem
                // com padrão Toggle. Buscar todos os itens que suportam toggle.
                var todosItens = janelaWizard.FindAllDescendants();
                var itensToggle = todosItens.Where(e => {
                    try { return e.Patterns.Toggle.IsSupported; }
                    catch { return false; }
                }).ToArray();

                if (itensToggle.Length > 0){
                    ProcessarItensComToggle(itensToggle);
                    return;
                }

                throw new Exception("Nenhum checkbox ou item com toggle encontrado na página de Filtros.");
            }

            ProcessarCheckboxes(todosCheckboxes);
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Processa checkboxes nativos: desmarca todos e marca apenas ESPECIAIS.
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static void ProcessarCheckboxes(AutomationElement[] checkboxes){
            AutomationElement? especiais = null;
            int total = 0;

            foreach (var chk in checkboxes){
                var nome = chk.Name ?? "";
                // Ignora checkboxes do lado direito ("Filtro de Grupos") que contenham "Todos" ou "|"
                // Os itens do "Filtro de Camadas" são: Default, Sem área, Piso, Parede, etc.

                if (nome.Equals("ESPECIAIS", StringComparison.OrdinalIgnoreCase)){
                    especiais = chk;
                }

                // Desmarcar o checkbox se estiver marcado
                try {
                    var asChk = chk.AsCheckBox();
                    if (asChk.IsChecked == true){
                        AppLogs.LogExportador3DDesmarcandoCamada(nome);
                        asChk.IsChecked = false;
                        InteractionHelper.EsperarUiRespirar(100);
                    }
                }
                catch {
                    // Fallback: tentar via Toggle pattern
                    try {
                        if (chk.Patterns.Toggle.IsSupported){
                            var estado = chk.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault;
                            if (estado == FlaUI.Core.Definitions.ToggleState.On){
                                AppLogs.LogExportador3DDesmarcandoCamada(nome);
                                chk.Patterns.Toggle.Pattern.Toggle();
                                InteractionHelper.EsperarUiRespirar(100);
                            }
                        }
                    }
                    catch { }
                }
                total++;
            }

            // Marcar apenas ESPECIAIS
            bool temEspeciais = false;
            if (especiais != null){
                AppLogs.LogExportador3DMarcandoEspeciais();
                try {
                    var asChk = especiais.AsCheckBox();
                    asChk.IsChecked = true;
                    temEspeciais = true;
                }
                catch {
                    try {
                        if (especiais.Patterns.Toggle.IsSupported){
                            var estado = especiais.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault;
                            if (estado != FlaUI.Core.Definitions.ToggleState.On){
                                especiais.Patterns.Toggle.Pattern.Toggle();
                            }
                            temEspeciais = true;
                        }
                    }
                    catch { }
                }
                InteractionHelper.EsperarUiRespirar(200);
            }
            else {
                AppLogs.LogExportador3DEspeciaisNaoEncontrado();
            }

            AppLogs.LogExportador3DFiltrosProcessados(total, temEspeciais);
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Processa itens com TogglePattern (TreeItem/ListItem): desmarca todos e marca apenas ESPECIAIS.
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static void ProcessarItensComToggle(AutomationElement[] itens){
            AutomationElement? especiais = null;
            int total = 0;

            foreach (var item in itens){
                var nome = item.Name ?? "";

                if (nome.Equals("ESPECIAIS", StringComparison.OrdinalIgnoreCase)){
                    especiais = item;
                }

                try {
                    var estado = item.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault;
                    if (estado == FlaUI.Core.Definitions.ToggleState.On){
                        AppLogs.LogExportador3DDesmarcandoCamada(nome);
                        item.Patterns.Toggle.Pattern.Toggle();
                        InteractionHelper.EsperarUiRespirar(100);
                    }
                }
                catch { }
                total++;
            }

            bool temEspeciais = false;
            if (especiais != null){
                AppLogs.LogExportador3DMarcandoEspeciais();
                try {
                    var estado = especiais.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault;
                    if (estado != FlaUI.Core.Definitions.ToggleState.On){
                        especiais.Patterns.Toggle.Pattern.Toggle();
                    }
                    temEspeciais = true;
                }
                catch { }
                InteractionHelper.EsperarUiRespirar(200);
            }
            else {
                AppLogs.LogExportador3DEspeciaisNaoEncontrado();
            }

            AppLogs.LogExportador3DFiltrosProcessados(total, temEspeciais);
        }
    }
}
