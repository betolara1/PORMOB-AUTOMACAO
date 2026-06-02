using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using PromobAutomacao.Automation;
using PromobAutomacao.Utils;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace PromobAutomacao.Promob{
    //--------------------------------------------------------------------------------------
    /// <summary>
    /// Componente responsável por gerenciar o carregamento de projetos recém-importados no Promob,
    /// aguardando e validando a renderização correta de abas e tratando possíveis popups informativos.
    /// </summary>
    //--------------------------------------------------------------------------------------
    public static class PromobCarregadorProjeto{

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Localiza o projeto importado pelo nome na lista de projetos recentes e efetua o duplo clique
        /// para abri-lo, gerenciando ativamente a espera pelo carregamento total e avisos de cena na tela.
        /// </summary>
        /// <param name="janelaPromob">A janela principal ativa do Promob.</param>
        //--------------------------------------------------------------------------------------
        public static void AbrirProjetoSelecionado(Window janelaPromob){
            InteractionHelper.AtivarJanela(janelaPromob);
            AppLogs.LogCarregadorLocalizandoPrimeiroProjetoRecentes();

            var itemProjeto = WindowFinder.BuscarElementoComFallback(
                janelaPromob,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem).Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.DataItem)),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.ListItem || e.ControlType == FlaUI.Core.Definitions.ControlType.DataItem,
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            if (itemProjeto != null){
                AppLogs.LogCarregadorPrimeiroProjetoLocalizado(itemProjeto.Name);
                bool botaoClicado = false;
                
                // Tenta achar o ListItem (pai) caso o elemento encontrado seja apenas o Text com o nome do projeto
                var containerBusca = itemProjeto;
                try {
                    if (containerBusca.ControlType != FlaUI.Core.Definitions.ControlType.ListItem && containerBusca.Parent != null) {
                        containerBusca = containerBusca.Parent;
                        if (containerBusca.ControlType != FlaUI.Core.Definitions.ControlType.ListItem && containerBusca.Parent != null) {
                            containerBusca = containerBusca.Parent;
                        }
                    }
                } catch { }

                for (int i = 1; i <= 3; i++) {
                    // O botão tem AutomationId='openProjectAction' e Nome vazio — buscar por Id
                    var btnAbrirProjeto = containerBusca.FindFirstDescendant(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                          .And(cf.ByAutomationId("openProjectAction")));

                    if (btnAbrirProjeto == null) {
                        // Fallback: tenta pelos nomes configurados
                        btnAbrirProjeto = containerBusca.FindFirstDescendant(cf =>
                            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                              .And(cf.ByName(PromobConfig.BtnAbrirProjeto).Or(cf.ByName(PromobConfig.BtnAbrir)).Or(cf.ByName("Acessar")).Or(cf.ByName("Editar"))));
                    }

                    if (btnAbrirProjeto != null) {
                        AppLogs.LogCarregadorBotaoAbrirEncontrado(i);
                        InteractionHelper.ClicarComFallback(btnAbrirProjeto);
                        botaoClicado = true;
                        break;
                    }

                    if (i < 3) {
                        AppLogs.LogCarregadorBotaoAbrirNaoEncontradoTentativa(i);
                        Thread.Sleep(5000);
                    }
                }

                if (!botaoClicado) {
                    AppLogs.LogCarregadorBotaoAbrirNaoEncontradoDuploClique();
                    itemProjeto.DoubleClick();
                }
            }
            else{
                AppLogs.LogCarregadorListItemNaoEncontradoGenerico();

                var btnAbrir = janelaPromob.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                      .And(cf.ByName(PromobConfig.BtnAbrirProjeto).Or(cf.ByName(PromobConfig.BtnAbrir)).Or(cf.ByName("Acessar")).Or(cf.ByName("Editar"))));

                if (btnAbrir != null){
                    AppLogs.LogCarregadorBotaoAbrirGenericoClicado();
                    InteractionHelper.ClicarComFallback(btnAbrir);
                }
                else{
                    AppLogs.LogCarregadorFormaAbrirNaoEncontradaEnter();
                    InteractionHelper.AtivarJanela(janelaPromob);
                    Keyboard.Type(VirtualKeyShort.RETURN);
                }
            }

            int timeoutAtual = 10000;
            int tentativaLoop = 10;

            while (true){
                AppLogs.LogCarregadorAguardandoCarregamentoProjeto(tentativaLoop, timeoutAtual / 1000);

                bool carregou = InteractionHelper.EsperarAte(() =>{
                    var swTotal = Stopwatch.StartNew();
                    AppLogs.LogCarregadorIniciandoCicloVerificacao();

                    // --- DETECÇÃO PREVENTIVA DE POPUPS DE BLOQUEIO (COMO O DE APENAS LEITURA) ---
                    try {
                        var desktop = janelaPromob.Automation.GetDesktop();
                        var janelasDesktop = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
                        var janelasFilhas = janelaPromob.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
                        
                        var todasJanelas = janelasDesktop.Concat(janelasFilhas).ToList();
                        var janelasAviso = todasJanelas.Where(j => {
                            var nome = j.Name ?? "";
                            return InteractionHelper.ContemQualquer(nome, PromobConfig.TitulosAviso);
                        });
                        
                        foreach (var j in janelasAviso) {
                            var textos = j.FindAllDescendants()
                                .Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Text)
                                .Select(e => e.Name ?? "")
                                .ToList();
                                
                            bool ehApenasLeitura = textos.Any(t => t.Contains("apenas leitura", StringComparison.OrdinalIgnoreCase) || 
                                                                   t.Contains("desatualizado", StringComparison.OrdinalIgnoreCase));
                            
                            if (ehApenasLeitura) {
                                AppLogs.LogCarregadorPopupApenasLeituraDetectado();
                                var popupWindow = j.AsWindow();
                                InteractionHelper.AtivarJanela(popupWindow);
                                
                                // Tenta fechar clicando no OK ou 'X' ou Alt+F4
                                var btnOk = popupWindow.FindFirstDescendant(cf => 
                                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                                      .And(cf.ByName("OK").Or(cf.ByName("Ok")).Or(cf.ByName("Fechar")).Or(cf.ByName("Concluir")).Or(cf.ByName("Close")).Or(cf.ByName("Sim")).Or(cf.ByName("Yes"))));
                                      
                                if (btnOk != null) {
                                    AppLogs.LogCarregadorPopupClicandoOk();
                                    InteractionHelper.ClicarComFallback(btnOk);
                                } else {
                                    var primeiroBotao = popupWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
                                    if (primeiroBotao != null) {
                                        AppLogs.LogCarregadorPopupClicandoPrimeiroBotao();
                                        InteractionHelper.ClicarComFallback(primeiroBotao);
                                    } else {
                                        AppLogs.LogCarregadorPopupEnviandoAltF4();
                                        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
                                    }
                                }
                                InteractionHelper.EsperarUiRespirar(1000);
                            }
                        }
                    } catch (Exception ex) {
                        AppLogs.LogCarregadorErroPopupAviso(ex.Message);
                    }
                    // ----------------------------------------------------------------------------

                    var raizBusca = WindowFinder.ObterHostOuJanela(janelaPromob, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);

                    var swAba = Stopwatch.StartNew();
                    AppLogs.LogCarregadorProcurandoAbaFerramentas();
                    var aba = raizBusca.FindFirstDescendant(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)
                          .And(cf.ByAutomationId(PromobConfig.IdToolsTab).Or(cf.ByName(PromobConfig.AbaFerramentas))));
                    swAba.Stop();

                    if (aba != null) AppLogs.LogCarregadorAbaFerramentasEncontrada(swAba.ElapsedMilliseconds);
                    else AppLogs.LogCarregadorAbaFerramentasNaoVisivel(swAba.ElapsedMilliseconds);

                    var swMsg = Stopwatch.StartNew();
                    AppLogs.LogCarregadorVerificandoMensagemCarregando();
                    var msgCarregando = raizBusca.FindFirstDescendant(cf =>
                        cf.ByName(PromobConfig.MsgCarregandoItens));
                    swMsg.Stop();

                    if (msgCarregando != null) AppLogs.LogCarregadorModulosCarregando(swMsg.ElapsedMilliseconds);
                    else AppLogs.LogCarregadorSemMensagemCarregando(swMsg.ElapsedMilliseconds);

                    bool pronto = (aba != null) && (msgCarregando == null);

                    if (!pronto){
                        var swPopup = Stopwatch.StartNew();
                        AppLogs.LogCarregadorProcurandoPopupsBloqueio();
                        var desktop = janelaPromob.Automation.GetDesktop();
                        var popup = PromobWindowHelper.EncontrarPopupAtencao(desktop, PromobWindowHelper.CachedProcessIdPromob);
                        swPopup.Stop();

                        if (popup != null){
                            AppLogs.LogCarregadorPopupTratado(popup.Name, swPopup.ElapsedMilliseconds);
                            TratarPopupGenerico(popup);
                        }
                        else{
                            AppLogs.LogCarregadorSemPopupsDetectados(swPopup.ElapsedMilliseconds);
                        }
                    }
                    else{
                        AppLogs.LogCarregadorCondicoesConcluidas();
                        InteractionHelper.SelecionarOuClicar(aba!);
                    }

                    swTotal.Stop();
                    AppLogs.LogCarregadorCicloFinalizado(swTotal.ElapsedMilliseconds);
                    return pronto;
                }, timeoutMs: timeoutAtual, intervaloMs: 2500);

                if (carregou){
                    AppLogs.LogCarregadorProjetoCarregadoSucesso();
                    InteractionHelper.EsperarUiRespirar(1000);
                    break;
                }

                AppLogs.LogCarregadorTimeoutCarregamentoUia(timeoutAtual / 1000);

                // Fallback de Visão Computacional (AI) caso o mapeamento por árvore UIA falhe
                if (VisionHelper.Habilitado){
                    AppLogs.LogCarregadorVisionIniciando();
                    var visao = VisionHelper.AguardarEstadoTela(
                        "A aba 'Ferramentas' está visível e não há mensagens de 'Carregando' ou 'Módulos Invisíveis' na parte inferior da tela.",
                        maxTentativas: 1, fallbackMs: 500);

                    if (visao){
                        AppLogs.LogCarregadorVisionPronta();
                        break;
                    }
                    else{
                        AppLogs.LogCarregadorVisionInconsistente();
                    }
                }

                tentativaLoop++;
                timeoutAtual = 10000;
                AppLogs.LogCarregadorReiniciandoVerificacao();
            }
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Trata e responde a um popup ou caixa de aviso/atenção genérica aberta pelo Promob,
        /// clicando em botões lógicos de confirmação (OK, Confirmar, Sim) ou enviando ALT+F4/ESC.
        /// </summary>
        /// <param name="popup">A janela do popup interceptado.</param>
        //--------------------------------------------------------------------------------------
        public static void TratarPopupGenerico(Window popup){
            AppLogs.LogCarregadorTratandoPopup(popup.Name);
            InteractionHelper.AtivarJanela(popup);

            if (InteractionHelper.ContemQualquer(popup.Name, PromobConfig.TitulosAviso)){
                var btnOk = popup.FindFirstDescendant(cf => 
                    cf.ByName(PromobConfig.BtnOk)
                      .Or(cf.ByName(PromobConfig.BtnOkAlt))
                      .Or(cf.ByName(PromobConfig.BtnConcluir))
                      .Or(cf.ByName(PromobConfig.BtnSim))); // Adicionado Sim

                if (btnOk != null){
                    AppLogs.LogCarregadorClicandoOkPopup(btnOk.Name);
                    InteractionHelper.AtivarJanela(popup);
                    InteractionHelper.ClicarComFallback(btnOk);
                }
                else{
                    AppLogs.LogCarregadorEnviandoAltF4Popup();
                    InteractionHelper.AtivarJanela(popup);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
                }
                InteractionHelper.EsperarUiRespirar(500);
            }
        }
    }
}
