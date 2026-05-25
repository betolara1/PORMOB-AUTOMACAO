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
            Logger.Log("  [INFO] Verificando se o Promob está pronto na tela inicial...");
            
            bool achouImportar = false;
            bool achouFechar = false;
            
            const int maxTentativas = 150; // 150 tentativas * 2 segundos = 5 minutos de espera máxima
            const int tempoEsperaMs = 2000;
            
            for (int tentativa = 1; tentativa <= maxTentativas; tentativa++){
                try{
                    Logger.Log($"    -> Verificando estado do Promob (Tentativa {tentativa}/{maxTentativas})...");
                    
                    var raiz = WindowFinder.ObterHostOuJanela(janela, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);
                    
                    // Se a raiz obtida for a própria janela principal (elementHost1 ainda não carregado),
                    // significa que o Promob ainda está na fase inicial de carregamento de plugins/splash.
                    // Procurar botões internos agora causará timeouts e lentidão extrema no UIA.
                    if (raiz == janela && WindowFinder.CachedHost == null){
                        Logger.Log($"    [INFO] Interface gráfica (elementHost1) ainda não foi renderizada pelo Promob. Aguardando inicialização...");
                        Thread.Sleep(tempoEsperaMs);
                        continue;
                    }
                    
                    // 1. Tenta localizar o botão 'Importar Projeto' (Tela Inicial)
                    var btnImportar = WindowFinder.BuscarElementoComFallback(
                        raiz,
                        cf => cf.ByAutomationId(PromobConfig.IdImportarBotao),
                        e => (e.Properties.AutomationId.ValueOrDefault ?? "").Equals(PromobConfig.IdImportarBotao, StringComparison.OrdinalIgnoreCase) ||
                             (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.NomeJanelaWizardImportacao, StringComparison.OrdinalIgnoreCase),
                        limitarAoMesmoProcesso: true,
                        processId: PromobWindowHelper.CachedProcessIdPromob
                    );

                    if (btnImportar != null && !btnImportar.Properties.IsOffscreen.ValueOrDefault){
                        achouImportar = true;
                        break;
                    }

                    // 2. Se não achou Importar, tenta localizar elementos de fechar projeto (Projeto Aberto)
                    var abaArquivo = WindowFinder.BuscarElementoComFallback(
                        raiz,
                        cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)
                                .And(cf.ByAutomationId(PromobConfig.IdFileTab).Or(cf.ByName(PromobConfig.AbaArquivo))),
                        e => e.ControlType == FlaUI.Core.Definitions.ControlType.TabItem &&
                             ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals(PromobConfig.IdFileTab, StringComparison.OrdinalIgnoreCase) ||
                              (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.AbaArquivo, StringComparison.OrdinalIgnoreCase)),
                        limitarAoMesmoProcesso: true,
                        processId: PromobWindowHelper.CachedProcessIdPromob
                    );

                    var btnFechar = WindowFinder.BuscarElementoComFallback(
                        raiz,
                        cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                                .And(cf.ByAutomationId(PromobConfig.IdProjectClose).Or(cf.ByName(PromobConfig.BtnFechar))),
                        e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                             ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals(PromobConfig.IdProjectClose, StringComparison.OrdinalIgnoreCase) ||
                              (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.BtnFechar, StringComparison.OrdinalIgnoreCase)),
                        limitarAoMesmoProcesso: true,
                        processId: PromobWindowHelper.CachedProcessIdPromob
                    );

                    if (abaArquivo != null || btnFechar != null){
                        achouFechar = true;
                        break;
                    }
                }
                catch (Exception ex){
                    // Captura erros de timeout, janelas não responsivas ou COMExceptions comuns de inicialização do Promob
                    Logger.Log($"    [INFO] Promob ainda não respondeu ou está carregando ({ex.Message}). Continuando busca...", LogLevel.Debug);
                    // Invalidamos o cache do host para forçar uma nova varredura física na próxima tentativa
                    WindowFinder.CachedHost = null;
                }

                Thread.Sleep(tempoEsperaMs);
            }

            if (achouImportar){
                Logger.Log("  [INFO] Promob está na tela inicial (pronto para importar). Nenhum projeto aberto detectado.");
                return;
            }

            if (achouFechar){
                Logger.Log("  [AVISO] Projeto aberto detectado. Fechando projeto antes de importar...", LogLevel.Warn);
                Fechar(automation, janela);
                Logger.Log("  [OK] Projeto anterior fechado. Promob retornou à tela inicial.");
                return;
            }

            // Caso não encontre nenhum dos dois estados após as retentativas acumuladas
            throw new Exception("Não foi possível detectar o estado do Promob (nem tela inicial nem projeto aberto foram encontrados após retentativas). O programa ainda pode estar inicializando.");
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
            int ciclosSemPopup = 0;

            while (swFechamento.ElapsedMilliseconds < 60000){
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

                if (btnImportar != null && !btnImportar.Properties.IsOffscreen.ValueOrDefault){
                    Logger.Log($"    [SUCESSO] Botão 'Importar' detectado! Projeto fechado ({swFechamento.ElapsedMilliseconds}ms).");
                    projetoFechado = true;
                    break;
                }

                // 2. Verifica se existe o popup de Salvar aberto
                // Busca sem filtro de ControlType pois diálogos WPF owned podem não aparecer como Window no desktop
                var desktop = automation.GetDesktop();
                var todasJanelas = desktop.FindAllChildren(); // sem filtro — pega tudo no nivel do desktop
                var popup = todasJanelas
                    .Where(j => {
                        try { return !PromobWindowHelper.CachedProcessIdPromob.HasValue || j.Properties.ProcessId.ValueOrDefault == PromobWindowHelper.CachedProcessIdPromob.Value; }
                        catch { return true; }
                    })
                    .FirstOrDefault(j => {
                        var nome = j.Name ?? "";
                        return nome.Equals("Salvar", StringComparison.OrdinalIgnoreCase) ||
                               nome.Equals("Save", StringComparison.OrdinalIgnoreCase) ||
                               nome.Equals("Confirmação", StringComparison.OrdinalIgnoreCase) ||
                               nome.Equals("Confirmacao", StringComparison.OrdinalIgnoreCase);
                    });

                if (popup != null){
                    ciclosSemPopup = 0;
                    Logger.Log($"    [INFO] Popup '{popup.Name}' detectado. Buscando botão 'Não'...");
                    // Busca profunda: FindAllDescendants para achar botões dentro de Panels intermediários
                    var btnNao = popup.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
                        .FirstOrDefault(b => b.Name == PromobConfig.BtnNao || b.Name == PromobConfig.BtnNaoAlt || b.Name == "No" || b.Name == "Nao");

                    if (btnNao != null){
                        Logger.Log($"    [OK] Botão 'Não' localizado. Clicando...");
                        InteractionHelper.AtivarJanela(popup.AsWindow());
                        InteractionHelper.ClicarComFallback(btnNao);
                        InteractionHelper.EsperarUiRespirar(1000);
                    }
                    else{
                        // Fallback: ativa o popup e pressiona Alt+N (atalho do botão 'Não')
                        Logger.Log($"    [AVISO] Botão 'Não' não encontrado na árvore de '{popup.Name}'. Enviando Alt+N via teclado...", LogLevel.Warn);
                        InteractionHelper.AtivarJanela(popup.AsWindow());
                        InteractionHelper.EsperarUiRespirar(300);
                        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_N);
                        InteractionHelper.EsperarUiRespirar(1000);
                    }
                }
                else{
                    ciclosSemPopup++;
                    // Fallback redundante a cada 4 ciclos (~2s): se há modal com foco não detectado via UIA,
                    // Alt+N fecha direto sem afetar outros elementos
                    if (swFechamento.ElapsedMilliseconds > 1500 && ciclosSemPopup % 4 == 0){
                        Logger.Log($"    [DEBUG] Nenhum popup UIA detectado. Disparando Alt+N preventivo...", LogLevel.Debug);
                        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_N);
                        InteractionHelper.EsperarUiRespirar(500);
                    }
                }

                Thread.Sleep(500);
            }

            if (!projetoFechado){
                Logger.Log($"    [AVISO] Timeout de 60s atingido e botão 'Importar' não foi detectado. O Promob pode estar travado.", LogLevel.Warn);
            }

            swTotal.Stop();
            Logger.Log($"  [SUCESSO] Sequência de fechamento concluída em {swTotal.ElapsedMilliseconds}ms.");
        }
    }
}
