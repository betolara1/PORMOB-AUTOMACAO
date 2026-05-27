using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using AutomacaoPromobTeste.Automation;
using AutomacaoPromobTeste.Utils;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace AutomacaoPromobTeste.Promob{
    //--------------------------------------------------------------------------------------
    /// <summary>
    /// Componente responsável por gerenciar a navegação e a exportação de dados para o ERP,
    /// cobrindo o acionamento do integrador no menu e o monitoramento da geração do XML.
    /// </summary>
    //--------------------------------------------------------------------------------------
    public static class PromobExportadorErp{

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Navega pela interface gráfica do Promob ativando o menu 'Ferramentas', abrindo a opção
        /// 'Integradores' e disparando o integrador 'Promob ERP'.
        /// </summary>
        /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        /// <param name="janela">A janela principal ativa do Promob.</param>
        //--------------------------------------------------------------------------------------
        public static void AbrirIntegradorErp(UIA3Automation automation, Window janela){
            InteractionHelper.AtivarJanela(janela);

            var raizBusca = WindowFinder.ObterHostOuJanela(janela, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);

            AppLogs.LogExportadorProcurandoAbaFerramentas();
            var abaFerramentas = WindowFinder.BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)
                        .And(cf.ByAutomationId(PromobConfig.IdToolsTab).Or(cf.ByName(PromobConfig.AbaFerramentas))),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.TabItem &&
                     ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals(PromobConfig.IdToolsTab, StringComparison.OrdinalIgnoreCase) ||
                      (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.AbaFerramentas, StringComparison.OrdinalIgnoreCase)),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            if (abaFerramentas != null){
                AppLogs.LogExportadorAbaFerramentasEncontrada();
                InteractionHelper.SelecionarOuClicar(abaFerramentas);
                InteractionHelper.EsperarUiRespirar(800);
            }
            else{
                AppLogs.LogExportadorAbaFerramentasNaoEncontrada();
            }

            AppLogs.LogExportadorProcurandoBotaoIntegradores();
            var btnIntegradores = WindowFinder.BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByName(PromobConfig.BotaoIntegradores).Or(cf.ByName(PromobConfig.BotaoIntegradores.ToUpperInvariant())),
                e => (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.BotaoIntegradores, StringComparison.OrdinalIgnoreCase),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            if (btnIntegradores != null){
                AppLogs.LogExportadorBotaoIntegradoresEncontrado();
                InteractionHelper.AcionarElementoSemMouse(btnIntegradores);

                AppLogs.LogExportadorAguardandoDropdown();
                AutomationElement? optErp = null;

                bool encontrou = InteractionHelper.EsperarAte(() => {
                    try {
                        var desktop = automation.GetDesktop();

                        // Busca primeiro na janela principal (pode ser descendente direto)
                        optErp = janela.FindFirstDescendant(cf =>
                            cf.ByName(PromobConfig.MenuPromobErp));

                        // Se não achou, busca no Desktop (menus dropdown de WPF flutuam fora da hierarquia da janela)
                        if (optErp == null){
                            optErp = desktop.FindFirstDescendant(cf =>
                                cf.ByName(PromobConfig.MenuPromobErp));
                        }

                        return optErp != null;
                    } catch { return false; }
                }, timeoutMs: 5000, intervaloMs: 500);

                if (encontrou && optErp != null){
                    AppLogs.LogExportadorOpcaoErpEncontrada(optErp.ControlType);
                    InteractionHelper.AcionarElementoSemMouse(optErp);
                    InteractionHelper.EsperarUiRespirar(500);
                }
                else{
                    AppLogs.LogExportadorOpcaoErpNaoEncontrada();
                }
            }
            else{
                AppLogs.LogExportadorBotaoIntegradoresNaoEncontrado();
            }
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Monitora ativamente o processamento pesado de exportação do Promob ERP, aguardando que o
        /// texto de sucesso apareça, fechando a janela de status e fechando o Explorer que abre ao fim.
        /// </summary>
        /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        /// <param name="janela">A janela principal ativa do Promob.</param>
        //--------------------------------------------------------------------------------------
        public static void AguardarExportacaoErp(UIA3Automation automation, Window janela){
            var swTotal = Stopwatch.StartNew();
            AppLogs.LogExportadorAguardandoExportacaoFinalizar();

            // ====================================================================
            // FASE 1: Aguardar a mensagem de sucesso OU erro
            // O popup de carregamento e a janela de exportação vão aparecer e sumir
            // sozinhos. Monitoramos até o texto de sucesso ou erro surgir.
            // ====================================================================
            AutomationElement? textoSucesso = null;
            Window? janelaExportacao = null;
            bool detectouErro = false;
            int logIntervalMs = 30000; // Log de progresso a cada 30s
            var swUltimoLog = Stopwatch.StartNew();

            bool exportouComResultado = InteractionHelper.EsperarAte(() => {
                // Log de progresso periódico para não parecer travado
                if (swUltimoLog.ElapsedMilliseconds >= logIntervalMs){
                    AppLogs.LogExportadorEmAndamento(swTotal.ElapsedMilliseconds / 1000);
                    swUltimoLog.Restart();
                }

                try {
                    var desktop = automation.GetDesktop();
                    var todasJanelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

                    foreach (var win in todasJanelas){
                        try {
                            // Filtra apenas janelas do processo Promob
                            if (PromobWindowHelper.CachedProcessIdPromob.HasValue &&
                                win.Properties.ProcessId.ValueOrDefault != PromobWindowHelper.CachedProcessIdPromob.Value)
                                continue;

                            // Procura textos dentro da janela
                            var txtSucesso = win.FindFirstDescendant(cf =>
                                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));

                            if (txtSucesso != null){
                                // Verifica todos os textos dentro da janela
                                var todosTextos = win.FindAllDescendants(cf =>
                                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));

                                foreach (var txt in todosTextos){
                                    try {
                                        var conteudo = txt.Properties.Name.ValueOrDefault ?? "";

                                        // Verifica ERRO primeiro (prioridade sobre sucesso)
                                        if (conteudo.Contains(PromobConfig.MsgExportacaoErro, StringComparison.OrdinalIgnoreCase)){
                                            AppLogs.LogExportadorErroDetectado(conteudo);
                                            janelaExportacao = win.AsWindow();
                                            detectouErro = true;
                                            return true;
                                        }

                                        // Verifica SUCESSO
                                        if (conteudo.Contains(PromobConfig.MsgExportacaoSucesso, StringComparison.OrdinalIgnoreCase)){
                                            textoSucesso = txt;
                                            janelaExportacao = win.AsWindow();
                                            return true;
                                        }
                                    } catch { }
                                }
                            }
                        } catch { }
                    }
                } catch { }

                return false;
            }, timeoutMs: PromobConfig.TimeoutExportacaoErp, intervaloMs: 5000);

            if (!exportouComResultado){
                AppLogs.LogExportadorTimeoutSemResultado();
                return;
            }

            // ====================================================================
            // FASE 2: Clicar no botão "Fechar" da janela de exportação
            // (tanto para sucesso quanto para erro)
            // ====================================================================
            if (janelaExportacao != null){
                AppLogs.LogExportadorProcurandoBotaoFechar();
                InteractionHelper.EsperarUiRespirar(500);

                AutomationElement? btnFechar = null;
                bool achouFechar = InteractionHelper.EsperarAte(() => {
                    try {
                        btnFechar = janelaExportacao.FindFirstDescendant(cf =>
                            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                              .And(cf.ByName(PromobConfig.BtnFechar)));

                        btnFechar ??= janelaExportacao.FindFirstDescendant(cf =>
                            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                              .And(cf.ByName("Close")));

                        return btnFechar != null && btnFechar.Properties.IsEnabled.ValueOrDefault;
                    } catch { return false; }
                }, timeoutMs: 10000, intervaloMs: 500);

                if (achouFechar && btnFechar != null){
                    AppLogs.LogExportadorBotaoFecharClicando();
                    InteractionHelper.ClicarComFallback(btnFechar);
                    InteractionHelper.EsperarUiRespirar(1500);
                }
                else{
                    AppLogs.LogExportadorBotaoFecharNaoEncontradoAltF4();
                    InteractionHelper.AtivarJanela(janelaExportacao);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
                    InteractionHelper.EsperarUiRespirar(1500);
                }
            }

            // Se detectou erro, lança exceção APÓS fechar a janela de exportação
            // para que o fluxo principal saiba que precisa mover o arquivo e fechar o projeto
            if (detectouErro){
                AppLogs.LogExportadorAbortadaErro();

                // Retorna foco para o Promob antes de lançar a exceção
                InteractionHelper.AtivarJanela(janela);
                InteractionHelper.EsperarUiRespirar(500);

                swTotal.Stop();
                throw new PromobExportException($"Exportação ERP abortada com erro após {swTotal.ElapsedMilliseconds / 1000}s.");
            }

            AppLogs.LogExportadorCompletadoSucesso(swTotal.ElapsedMilliseconds / 1000);

            // ====================================================================
            // FASE 3: Fechar a pasta 01_XML que abre automaticamente no Explorer
            // ====================================================================
            AppLogs.LogExportadorProcurandoExplorer();
            bool fechouExplorer = InteractionHelper.EsperarAte(() => {
                try {
                    var desktop = automation.GetDesktop();
                    var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

                    foreach (var win in janelas){
                        try {
                            var nome = win.Properties.Name.ValueOrDefault ?? "";
                            if (nome.Contains(PromobConfig.NomePastaXmlExport, StringComparison.OrdinalIgnoreCase)){
                                AppLogs.LogExportadorExplorerEncontradoFechando(nome);
                                var winExplorer = win.AsWindow();
                                InteractionHelper.AtivarJanela(winExplorer);
                                Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
                                InteractionHelper.EsperarUiRespirar(800);
                                return true;
                            }
                        } catch { }
                    }
                } catch { }
                return false;
            }, timeoutMs: 10000, intervaloMs: 1000);

            if (!fechouExplorer){
                AppLogs.LogExportadorExplorerNaoDetectado();
            }

            // ====================================================================
            // FASE 4: Retornar o foco para o Promob
            // ====================================================================
            AppLogs.LogExportadorRetornandoFoco();
            InteractionHelper.AtivarJanela(janela);
            InteractionHelper.EsperarUiRespirar(500);

            swTotal.Stop();
            AppLogs.LogExportadorConcluidaSucesso(swTotal.ElapsedMilliseconds / 1000);
        }
    }
}
