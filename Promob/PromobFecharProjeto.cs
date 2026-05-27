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
            
            bool achouImportar = false;
            bool achouFechar = false;
            
            const int maxTentativas = 150; // 150 tentativas * 2 segundos = 5 minutos de espera máxima
            const int tempoEsperaMs = 2000;
            
            for (int tentativa = 1; tentativa <= maxTentativas; tentativa++){
                try{
                    AppLogs.LogFecharProjetoVerificandoEstado(tentativa, maxTentativas);
                    
                    var raiz = WindowFinder.ObterHostOuJanela(janela, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);
                    
                    // Se a raiz obtida for a própria janela principal (elementHost1 ainda não carregado),
                    // significa que o Promob ainda está na fase inicial de carregamento de plugins/splash.
                    // Procurar botões internos agora causará timeouts e lentidão extrema no UIA.
                    if (raiz == janela && WindowFinder.CachedHost == null){
                        AppLogs.LogFecharProjetoInterfaceAindaNaoRenderizada();
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
                    AppLogs.LogFecharProjetoNaoRespondeuOuCarregando(ex.Message);
                    // Invalidamos o cache do host para forçar uma nova varredura física na próxima tentativa
                    WindowFinder.CachedHost = null;
                }

                Thread.Sleep(tempoEsperaMs);
            }

            if (achouImportar){
                return;
            }

            if (achouFechar){
                Fechar(automation, janela);
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

            InteractionHelper.AtivarJanela(janela);
            var raizBusca = WindowFinder.ObterHostOuJanela(janela, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);

            var swAba = Stopwatch.StartNew();

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
                InteractionHelper.SelecionarOuClicar(abaArquivo);
                InteractionHelper.EsperarUiRespirar(400);
            }
            else{}

            var swBtn = Stopwatch.StartNew();

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
                InteractionHelper.AtivarJanela(janela); // Garante foco antes de clicar
                InteractionHelper.ClicarComFallback(btnFechar);
            }
            else{
                Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_F);
                InteractionHelper.EsperarUiRespirar(800);
            }

            // Aguardar ativamente o fechamento do projeto
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
                    AppLogs.LogFecharProjetoPopupSalvarDetectado(popup.Name);
                    // Busca profunda: FindAllDescendants para achar botões dentro de Panels intermediários
                    var btnNao = popup.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
                        .FirstOrDefault(b => b.Name == PromobConfig.BtnNao || b.Name == PromobConfig.BtnNaoAlt || b.Name == "No" || b.Name == "Nao");

                    if (btnNao != null){
                        AppLogs.LogFecharProjetoBotaoNaoLocalizadoClicando();
                        InteractionHelper.AtivarJanela(popup.AsWindow());
                        InteractionHelper.ClicarComFallback(btnNao);
                        InteractionHelper.EsperarUiRespirar(1000);
                    }
                    else{
                        // Fallback: ativa o popup e pressiona Alt+N (atalho do botão 'Não')
                        AppLogs.LogFecharProjetoBotaoNaoNaoEncontradoTeclado(popup.Name);
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
                        AppLogs.LogFecharProjetoDisparandoAltNPreventivo();
                        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_N);
                        InteractionHelper.EsperarUiRespirar(500);
                    }
                }

                Thread.Sleep(500);
            }

            if (!projetoFechado){
                AppLogs.LogFecharProjetoTimeoutImportarNaoDetectado();
            }

            swTotal.Stop();
            AppLogs.LogFecharProjetoConcluido(swTotal.ElapsedMilliseconds);
        }
    }
}
