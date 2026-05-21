using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using AutomacaoPromobTeste.Automation;
using AutomacaoPromobTeste.Utils;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace AutomacaoPromobTeste.Promob{
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
            Logger.Log("  [INFO] Localizando o primeiro projeto da lista de recentes...");

            var itemProjeto = WindowFinder.BuscarElementoComFallback(
                janelaPromob,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem).Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.DataItem)),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.ListItem || e.ControlType == FlaUI.Core.Definitions.ControlType.DataItem,
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            if (itemProjeto != null){
                Logger.Log($"  [OK] Primeiro projeto localizado na lista: '{itemProjeto.Name}'. Procurando botão 'Abrir projeto'...");
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
                        Logger.Log($"  [OK] Botão 'Abrir projeto' encontrado na tentativa {i}. Clicando...");
                        InteractionHelper.ClicarComFallback(btnAbrirProjeto);
                        botaoClicado = true;
                        break;
                    }

                    if (i < 3) {
                        Logger.Log($"  [AVISO] Botão 'Abrir projeto' não encontrado na tentativa {i}. Aguardando 5s...", LogLevel.Warn);
                        Thread.Sleep(5000);
                    }
                }

                if (!botaoClicado) {
                    Logger.Log("  [AVISO] Botão 'Abrir projeto' não encontrado após 3 tentativas. Executando duplo clique no item do projeto...", LogLevel.Warn);
                    itemProjeto.DoubleClick();
                }
            }
            else{
                Logger.Log("  [AVISO] Nenhum item de projeto (ListItem/DataItem) encontrado na tela. Tentando botão de abrir genérico...", LogLevel.Warn);

                var btnAbrir = janelaPromob.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                      .And(cf.ByName(PromobConfig.BtnAbrirProjeto).Or(cf.ByName(PromobConfig.BtnAbrir)).Or(cf.ByName("Acessar")).Or(cf.ByName("Editar"))));

                if (btnAbrir != null){
                    Logger.Log("  [OK] Botão de abrir genérico encontrado. Clicando...");
                    InteractionHelper.ClicarComFallback(btnAbrir);
                }
                else{
                    Logger.Log("  [AVISO] Nenhuma forma de abrir encontrada. Tentando ENTER...", LogLevel.Warn);
                    InteractionHelper.AtivarJanela(janelaPromob);
                    Keyboard.Type(VirtualKeyShort.RETURN);
                }
            }

            int timeoutAtual = 10000;
            int tentativaLoop = 1;

            while (true){
                Logger.Log($"  [INFO] Aguardando o carregamento do projeto (Tentativa {tentativaLoop}, timeout: {timeoutAtual / 1000}s)...");

                bool carregou = InteractionHelper.EsperarAte(() =>{
                    var swTotal = Stopwatch.StartNew();
                    Logger.Log("    [DEBUG] Iniciando ciclo de verificação UI...");

                    var raizBusca = WindowFinder.ObterHostOuJanela(janelaPromob, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);

                    var swAba = Stopwatch.StartNew();
                    Logger.Log("      -> Procurando aba 'Ferramentas' (TabItem)...");
                    var aba = raizBusca.FindFirstDescendant(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)
                          .And(cf.ByAutomationId(PromobConfig.IdToolsTab).Or(cf.ByName(PromobConfig.AbaFerramentas))));
                    swAba.Stop();

                    if (aba != null) Logger.Log($"      [OK] Aba encontrada em {swAba.ElapsedMilliseconds}ms.");
                    else Logger.Log($"      [AGUARDE] Aba não visível após {swAba.ElapsedMilliseconds}ms.");

                    var swMsg = Stopwatch.StartNew();
                    Logger.Log("      -> Verificando mensagem de carregamento (Text/Label)...");
                    var msgCarregando = raizBusca.FindFirstDescendant(cf =>
                        cf.ByName(PromobConfig.MsgCarregandoItens));
                    swMsg.Stop();

                    if (msgCarregando != null) Logger.Log($"      [LOADING] Módulos carregando ({swMsg.ElapsedMilliseconds}ms).");
                    else Logger.Log($"      [READY] Sem mensagem de carregamento ({swMsg.ElapsedMilliseconds}ms).");

                    bool pronto = (aba != null) && (msgCarregando == null);

                    if (!pronto){
                        var swPopup = Stopwatch.StartNew();
                        Logger.Log("    [INFO] Procurando popups de bloqueio...");
                        var desktop = janelaPromob.Automation.GetDesktop();
                        var popup = PromobWindowHelper.EncontrarPopupAtencao(desktop, PromobWindowHelper.CachedProcessIdPromob);
                        swPopup.Stop();

                        if (popup != null){
                            Logger.Log($"    [AVISO] Popup '{popup.Name}' tratado ({swPopup.ElapsedMilliseconds}ms).");
                            TratarPopupGenerico(popup);
                        }
                        else{
                            Logger.Log($"    [INFO] Sem popups detectados em {swPopup.ElapsedMilliseconds}ms.");
                        }
                    }
                    else{
                        Logger.Log("    [SUCESSO] Condições de carregamento concluídas.");
                        InteractionHelper.SelecionarOuClicar(aba!);
                    }

                    swTotal.Stop();
                    Logger.Log($"    [DEBUG] Ciclo finalizado em {swTotal.ElapsedMilliseconds}ms total.");
                    return pronto;
                }, timeoutMs: timeoutAtual, intervaloMs: 2500);

                if (carregou){
                    Logger.Log("  [OK] Projeto carregado e validado com sucesso.");
                    InteractionHelper.EsperarUiRespirar(1000);
                    break;
                }

                Logger.Log($"  [AVISO] Timeout de {timeoutAtual / 1000}s atingido sem concluir o carregamento por UIA.", LogLevel.Warn);

                // Fallback de Visão Computacional (AI) caso o mapeamento por árvore UIA falhe
                if (VisionHelper.Habilitado){
                    Logger.Log("  [VISION] Iniciando verificação visual (IA) como fallback final para este ciclo...");
                    var visao = VisionHelper.AguardarEstadoTela(
                        "A aba 'Ferramentas' está visível e não há mensagens de 'Carregando' ou 'Módulos Invisíveis' na parte inferior da tela.",
                        maxTentativas: 1, fallbackMs: 500);

                    if (visao){
                        Logger.Log("  [VISION] IA detectou que a tela parece estar pronta (carregada). Prosseguindo.");
                        break;
                    }
                    else{
                        Logger.Log("  [VISION] IA confirmou que o projeto ainda parece estar carregando ou em estado inconsistente.");
                    }
                }

                tentativaLoop++;
                timeoutAtual = 10000;
                Logger.Log("  [INFO] Reiniciando verificação para novo ciclo de 10s...");
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
            Logger.Log($"  [INFO] Tratando popup: '{popup.Name}'");
            InteractionHelper.AtivarJanela(popup);

            if (InteractionHelper.ContemQualquer(popup.Name, PromobConfig.TitulosAviso)){
                var btnOk = popup.FindFirstDescendant(cf => 
                    cf.ByName(PromobConfig.BtnOk)
                      .Or(cf.ByName(PromobConfig.BtnOkAlt))
                      .Or(cf.ByName(PromobConfig.BtnConcluir))
                      .Or(cf.ByName(PromobConfig.BtnSim))); // Adicionado Sim

                if (btnOk != null){
                    Logger.Log($"  [OK] Clicando em '{btnOk.Name}' no popup.");
                    InteractionHelper.AtivarJanela(popup);
                    InteractionHelper.ClicarComFallback(btnOk);
                }
                else{
                    Logger.Log("  [OK] Enviando ALT+F4 para fechar o popup.");
                    InteractionHelper.AtivarJanela(popup);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
                }
                InteractionHelper.EsperarUiRespirar(500);
            }
        }
    }
}
