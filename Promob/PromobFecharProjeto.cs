using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using AutomacaoPromobTeste.Automation;
using AutomacaoPromobTeste.Utils;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace AutomacaoPromobTeste.Promob{
    public static class PromobFecharProjeto{

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Verifica o estado inicial do Promob ao iniciar o programa.
            /// Se detectar que há um projeto aberto (o botão "Importar" da tela inicial NÃO está visível),
            /// executa o fechamento do projeto para retornar à tela principal antes de continuar.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
            /// <param name="janela">A janela principal ativa do Promob.</param>
        //--------------------------------------------------------------------------------------
        public static void FecharProjetoPendenteSeNecessario(UIA3Automation automation, Window janela){
            var raiz = WindowFinder.ObterHostOuJanela(janela, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);

            var btnImportar = WindowFinder.BuscarElementoComFallback(
                raiz,
                cf => cf.ByAutomationId(PromobConfig.IdImportarBotao),
                e => (e.Properties.AutomationId.ValueOrDefault ?? "").Equals(PromobConfig.IdImportarBotao, StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.NomeJanelaWizardImportacao, StringComparison.OrdinalIgnoreCase),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            bool naTelaPrincipal = btnImportar != null && !btnImportar.Properties.IsOffscreen.ValueOrDefault;

            if (naTelaPrincipal){
                Logger.Log("  [INFO] Promob está na tela inicial. Nenhum projeto aberto detectado. Prosseguindo...");
                return;
            }

            Logger.Log("  [AVISO] Promob NÃO está na tela inicial — projeto aberto detectado. Fechando antes de importar...", LogLevel.Warn);
            Fechar(automation, janela);
            Logger.Log("  [OK] Projeto anterior fechado. Promob retornou à tela inicial.");
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Realiza o fechamento seguro do projeto atualmente aberto no Promob,
            /// tratando popups de confirmação de salvamento de forma a rejeitar alterações.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
            /// <param name="janela">A janela principal ativa do Promob.</param>
        //--------------------------------------------------------------------------------------
        public static void Fechar(UIA3Automation automation, Window janela){
            var swTotal = Stopwatch.StartNew();
            Logger.Log("  [INFO] Iniciando sequência de fechamento do projeto...");
            InteractionHelper.AtivarJanela(janela);
            var raizBusca = WindowFinder.ObterHostOuJanela(janela, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);

            var swAba = Stopwatch.StartNew();
            Logger.Log("    -> Procurando aba 'Arquivo' (FileTab)...");
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
            swAba.Stop();

            if (abaArquivo != null){
                Logger.Log($"    [OK] Aba 'Arquivo' localizada ({swAba.ElapsedMilliseconds}ms). Clicando...");
                InteractionHelper.SelecionarOuClicar(abaArquivo);
                InteractionHelper.EsperarUiRespirar(400);
            }
            else{
                Logger.Log($"    [AVISO] Aba 'Arquivo' não encontrada após {swAba.ElapsedMilliseconds}ms.", LogLevel.Warn);
            }

            var swBtn = Stopwatch.StartNew();
            Logger.Log("    -> Procurando botão 'Fechar' (ProjectClose)...");
            var btnFechar = WindowFinder.BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                        .And(cf.ByAutomationId(PromobConfig.IdProjectClose).Or(cf.ByName(PromobConfig.BtnFechar))),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                     ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals(PromobConfig.IdProjectClose, StringComparison.OrdinalIgnoreCase) ||
                      (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.BtnFechar, StringComparison.OrdinalIgnoreCase)),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );
            swBtn.Stop();

            if (btnFechar != null){
                Logger.Log($"    [OK] Botão 'Fechar' localizado ({swBtn.ElapsedMilliseconds}ms). Clicando...");
                InteractionHelper.AtivarJanela(janela); // Garante foco antes de clicar
                InteractionHelper.ClicarComFallback(btnFechar);
            }
            else{
                Logger.Log($"    [AVISO] Botão 'Fechar' não encontrado após {swBtn.ElapsedMilliseconds}ms. Tentando atalho Alt+F...", LogLevel.Warn);
                Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_F);
                InteractionHelper.EsperarUiRespirar(800);
            }

            // Aguardar ativamente o fechamento do projeto
            Logger.Log("    [INFO] Aguardando fechamento do projeto (e possível popup 'Deseja salvar?')...");
            var swFechamento = Stopwatch.StartNew();
            bool projetoFechado = false;
            
            while (swFechamento.ElapsedMilliseconds < 60000)
            {
                // 1. Verifica se retornou à tela inicial (botão Importar visível)
                var raizNova = WindowFinder.ObterHostOuJanela(janela, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);
                var btnImportar = WindowFinder.BuscarElementoComFallback(
                    raizNova,
                    cf => cf.ByAutomationId(PromobConfig.IdImportarBotao),
                    e => (e.Properties.AutomationId.ValueOrDefault ?? "").Equals(PromobConfig.IdImportarBotao, StringComparison.OrdinalIgnoreCase) ||
                         (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.NomeJanelaWizardImportacao, StringComparison.OrdinalIgnoreCase),
                    limitarAoMesmoProcesso: true,
                    processId: PromobWindowHelper.CachedProcessIdPromob
                );

                if (btnImportar != null && !btnImportar.Properties.IsOffscreen.ValueOrDefault)
                {
                    Logger.Log($"    [SUCESSO] Botão 'Importar' detectado! Projeto fechado ({swFechamento.ElapsedMilliseconds}ms).");
                    projetoFechado = true;
                    break;
                }

                // 2. Verifica se existe o popup de Salvar aberto
                var desktop = automation.GetDesktop();
                var popup = PromobWindowHelper.EncontrarPopupAtencao(desktop, PromobWindowHelper.CachedProcessIdPromob);

                if (popup != null)
                {
                    // Previne prender no fallback da janela principal avaliando se o botão "Não" existe
                    var btnNao = popup.FindFirstDescendant(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                          .And(cf.ByName(PromobConfig.BtnNao).Or(cf.ByName(PromobConfig.BtnNaoAlt)).Or(cf.ByName(PromobConfig.BtnNo))));

                    if (btnNao == null)
                    {
                        btnNao = popup.FindFirstChild(cf =>
                            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                              .And(cf.ByName(PromobConfig.BtnNao).Or(cf.ByName(PromobConfig.BtnNaoAlt)).Or(cf.ByName(PromobConfig.BtnNo))));
                    }

                    if (btnNao != null)
                    {
                        Logger.Log($"    [OK] Popup de salvamento detectado. Clicando em 'Não'...");
                        InteractionHelper.AtivarJanela(popup.AsWindow());
                        InteractionHelper.ClicarComFallback(btnNao);
                        InteractionHelper.EsperarUiRespirar(1000); // Dá tempo para o popup fechar e o projeto começar a fechar
                    }
                }

                Thread.Sleep(500);
            }

            if (!projetoFechado)
            {
                Logger.Log($"    [AVISO] Timeout de 60s atingido e botão 'Importar' não foi detectado. O Promob pode estar travado.", LogLevel.Warn);
            }

            swTotal.Stop();
            Logger.Log($"  [SUCESSO] Sequência de fechamento concluída em {swTotal.ElapsedMilliseconds}ms.");
        }
    }
}
