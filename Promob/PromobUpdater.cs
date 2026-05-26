using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using AutomacaoPromobTeste.Automation;
using AutomacaoPromobTeste.Utils;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace AutomacaoPromobTeste.Promob{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Componente responsável por automatizar a rotina de atualização do Promob,
        /// cobrindo o acionamento do menu Arquivo > Atualizar o Promob, verificação do status
        /// dos módulos (Desatualizado vs Atualizado) e tomada de decisão de atualizar ou fechar.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class PromobUpdater{
        
        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Executa a rotina completa de busca, validação e atualização do Promob.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        //--------------------------------------------------------------------------------------
        public static void ExecutarAtualizacao(UIA3Automation automation){
            Logger.Log("══════════════════════════════════════════");
            Logger.Log("║   Iniciando Rotina de Atualização      ║");
            Logger.Log("══════════════════════════════════════════");

            Logger.Log("  [1/6] Localizando janela principal do Promob...");
            var janela = PromobWindowHelper.AguardarJanelaPromob(automation, 15000)
                ?? throw new Exception("Janela do Promob não encontrada. Abra o Promob antes de atualizar.");

            InteractionHelper.AtivarJanela(janela);
            
            Logger.Log("  [2/6] Localizando menu 'Arquivo'...");
            var buscaEm = WindowFinder.ObterHostOuJanela(janela, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);

            AutomationElement? menuArquivo = WindowFinder.BuscarElementoComFallback(
                buscaEm,
                cf => cf.ByName("Arquivo"),
                e => (e.Properties.Name.ValueOrDefault ?? "").Equals("Arquivo", StringComparison.OrdinalIgnoreCase),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            // Logger.Log("[INFO] Procurando janela do Promob para listar botões...");
            // var janelaInicial = PromobWindowHelper.AguardarJanelaPromob(automation, 5000);
            // if (janelaInicial != null){
            //     Diagnostics.ListarBotoesProject(janelaInicial, PromobConfig.AutomationIdHost);
            // }

            if (menuArquivo == null){
                throw new Exception("Menu 'Arquivo' não encontrado na janela do Promob.");
            }

            // 2.5. Verifica se o botão "Atualizar o Promob" já está visível e habilitado na tela.
            // Se já estiver visível (Ribbon expandido na aba Arquivo), clicar no menu "Arquivo" iria
            // recolher/esconder o Ribbon, fazendo o botão desaparecer!
            Logger.Log("  [2.5/6] Verificando se o botão 'Atualizar o Promob' já está visível...");
            AutomationElement? btnPrevia = WindowFinder.BuscarElementoComFallback(
                buscaEm,
                cf => cf.ByAutomationId("OpenProcadUpdate"),
                e => (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("OpenProcadUpdate", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.Name.ValueOrDefault ?? "").Equals("Atualizar o Promob", StringComparison.OrdinalIgnoreCase),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            if (btnPrevia != null && btnPrevia.IsEnabled) {
                Logger.Log("  [OK] O botão 'Atualizar o Promob' já está visível e ativo na tela. Pulando clique no menu 'Arquivo'.");
            }
            else {
                Logger.Log("  [3/6] Clicando no menu 'Arquivo' para abrir as opções...");
                InteractionHelper.ClicarComFallback(menuArquivo);
                InteractionHelper.EsperarUiRespirar(1000); // Aguarda o menu/Ribbon abrir
            }

            Logger.Log("  [4/6] Localizando botão 'Atualizar o Promob' (ID: OpenProcadUpdate)...");
            AutomationElement? btnAtualizarPromob = null;
            
            var swBusca = Stopwatch.StartNew();
            const int timeoutBuscaMs = 5000; // 5 segundos de retry
            
            while (swBusca.ElapsedMilliseconds < timeoutBuscaMs){
                // Invalida cache de host para busca limpa
                WindowFinder.CachedHost = null;

                // 1. Tenta buscar a partir do Host elementHost1 (buscaEm) para cruzar a barreira WPF de forma rápida e precisa
                btnAtualizarPromob = WindowFinder.BuscarElementoComFallback(
                    buscaEm,
                    cf => cf.ByAutomationId("OpenProcadUpdate"),
                    e => (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("OpenProcadUpdate", StringComparison.OrdinalIgnoreCase) ||
                         (e.Properties.Name.ValueOrDefault ?? "").Equals("Atualizar o Promob", StringComparison.OrdinalIgnoreCase),
                    limitarAoMesmoProcesso: true,
                    processId: PromobWindowHelper.CachedProcessIdPromob
                );

                if (btnAtualizarPromob != null) break;

                // 2. Tenta buscar nas janelas/popups do Desktop pertencentes ao mesmo processo (comum para menus dropdown do WPF/WinForms)
                var desktopChildren = automation.GetDesktop().FindAllChildren();
                foreach (var child in desktopChildren){
                    try{
                        if (child.Properties.ProcessId.ValueOrDefault == PromobWindowHelper.CachedProcessIdPromob){
                            btnAtualizarPromob = child.FindFirstDescendant(cf => cf.ByAutomationId("OpenProcadUpdate"))
                                              ?? child.FindFirstDescendant(cf => cf.ByName("Atualizar o Promob"));
                            if (btnAtualizarPromob != null){
                                Logger.Log($"  [OK] Botão encontrado em uma sub-janela/popup do Desktop: '{btnAtualizarPromob.Name}'");
                                break;
                            }
                        }
                    }
                    catch { }
                }

                if (btnAtualizarPromob != null) break;

                Thread.Sleep(500); // Aguarda antes da próxima tentativa
            }

            if (btnAtualizarPromob == null){
                throw new Exception("Botão 'Atualizar o Promob' (OpenProcadUpdate) não encontrado no menu 'Arquivo' (tentativas na janela principal e popups do desktop esgotadas).");
            }

            Logger.Log("  [5/6] Clicando em 'Atualizar o Promob'...");
            InteractionHelper.ClicarComFallback(btnAtualizarPromob);

            Logger.Log("  [6/6] Aguardando janela do 'Promob Update' abrir...");
            Window? janelaUpdate = null;
            const int timeoutMs = 25000; // Aguarda até 25 segundos pela abertura
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs){
                var desktopWindows = automation.GetDesktop().FindAllChildren()
                    .Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Window)
                    .Select(e => e.AsWindow())
                    .ToList();
                
                var found = desktopWindows.FirstOrDefault(w => 
                    (w.Title ?? "").Contains("Promob Update", StringComparison.OrdinalIgnoreCase));
                
                if (found != null){
                    janelaUpdate = found;
                    break;
                }
                Thread.Sleep(800);
            }

            if (janelaUpdate == null){
                throw new Exception("Janela 'Promob Update' não apareceu a tempo.");
            }

            Logger.Log("  [OK] Janela 'Promob Update' detectada com sucesso!");
            InteractionHelper.AtivarJanela(janelaUpdate);
            InteractionHelper.EsperarUiRespirar(1500); // Aguarda o carregamento visual da janela

            Logger.Log("  [ACTION] Procurando e clicando na aba/botão 'Status' no menu lateral esquerdo...");
            var btnStatus = janelaUpdate.FindFirstDescendant(cf => cf.ByName("Status"));
            if (btnStatus == null){
                btnStatus = janelaUpdate.FindAllDescendants()
                    .FirstOrDefault(e => (e.Name ?? "").Contains("Status", StringComparison.OrdinalIgnoreCase));
            }

            if (btnStatus == null){
                throw new Exception("Aba/Botão 'Status' não encontrado na janela 'Promob Update'.");
            }

            InteractionHelper.ClicarComFallback(btnStatus);
            Logger.Log("  [INFO] Aba 'Status' clicada. Aguardando 3 segundos para carregar o status de todos os módulos...");
            InteractionHelper.EsperarUiRespirar(3000);

            Logger.Log("  [INFO] Analisando status dos módulos do Promob...");
            var todosElementos = janelaUpdate.FindAllDescendants().ToList();
            
            bool temDesatualizado = todosElementos.Any(e => 
                (e.Name ?? "").Contains("Desatualizado", StringComparison.OrdinalIgnoreCase) ||
                (e.Properties.AutomationId.ValueOrDefault ?? "").Contains("Desatualizado", StringComparison.OrdinalIgnoreCase)
            );

            if (temDesatualizado){
                Logger.Log("  [AVISO] ATENÇÃO: Há módulos DESATUALIZADOS detectados!", LogLevel.Warn);
                Logger.Log("  [ACTION] Procurando e clicando no botão/aba 'Atualizar' para aplicar as atualizações...");
                
                var btnAtualizarAba = janelaUpdate.FindFirstDescendant(cf => cf.ByName("Atualizar"));
                if (btnAtualizarAba == null){
                    btnAtualizarAba = janelaUpdate.FindAllDescendants()
                        .FirstOrDefault(e => (e.Name ?? "").Equals("Atualizar", StringComparison.OrdinalIgnoreCase));
                }

                if (btnAtualizarAba == null){
                    throw new Exception("Botão/Aba 'Atualizar' não encontrado no painel lateral.");
                }

                InteractionHelper.ClicarComFallback(btnAtualizarAba);
                Logger.Log("[SUCESSO] Clique na aba 'Atualizar' efetuado. O processo de download/instalação foi iniciado!", LogLevel.Info);
            }
            else{
                Logger.Log("  [OK] Parabéns! Todos os módulos do Promob estão 100% ATUALIZADOS.");
                Logger.Log("  [ACTION] Clicando no botão 'Fechar' para encerrar a janela do assistente...");
                
                var btnFechar = janelaUpdate.FindFirstDescendant(cf => cf.ByName("Fechar"));
                if (btnFechar == null){
                    btnFechar = janelaUpdate.FindAllDescendants()
                        .FirstOrDefault(e => (e.Name ?? "").Equals("Fechar", StringComparison.OrdinalIgnoreCase));
                }

                if (btnFechar == null){
                    throw new Exception("Botão 'Fechar' não encontrado na janela 'Promob Update'.");
                }

                InteractionHelper.ClicarComFallback(btnFechar);
                Logger.Log("[SUCESSO] Janela 'Promob Update' fechada com sucesso.");
            }

            Logger.Log("══════════════════════════════════════════");
            Logger.Log("║   Rotina de Atualização Concluída      ║");
            Logger.Log("══════════════════════════════════════════");
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Interage com a janela "Promob Update" que foi aberta automaticamente pelo próprio Promob.
            /// Ao contrário de <see cref="ExecutarAtualizacao"/>, aqui a janela já está aberta —
            /// o método apenas localiza e clica no botão "Atualizar" para iniciar o download.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        //--------------------------------------------------------------------------------------
        public static void ExecutarAtualizacaoUpdate(UIA3Automation automation){
            Logger.Log("══════════════════════════════════════════");
            Logger.Log("║  [UPDATE] Iniciando Atualização Auto   ║");
            Logger.Log("══════════════════════════════════════════");

            // 1. ANTES DE QUALQUER COISA: Verifica se o popup de sucesso "PromobUpdate" já está aberto no Desktop!
            // Se estiver, pulamos todas as etapas intermediárias e fechamos ele diretamente.
            Logger.Log("  [UPDATE] Verificando se a atualização já foi concluída anteriormente...");
            Window? janelaSucessoPrevia = null;
            AutomationElement? btnFecharSucessoPrevio = null;

            try {
                var janelasDesktop = automation.GetDesktop().FindAllChildren();
                foreach (var child in janelasDesktop) {
                    if (child.ControlType != FlaUI.Core.Definitions.ControlType.Window) continue;
                    string nome = child.Name ?? "";
                    if (nome.Equals("PromobUpdate", StringComparison.OrdinalIgnoreCase)) {
                        var descSucesso = child.FindAllDescendants();
                        var btnCheck = descSucesso.FirstOrDefault(e =>
                            (e.ControlType == FlaUI.Core.Definitions.ControlType.Button) &&
                            (e.Name ?? "").Equals("Fechar", StringComparison.OrdinalIgnoreCase)
                        );
                        if (btnCheck != null) {
                            janelaSucessoPrevia = child.AsWindow();
                            btnFecharSucessoPrevio = btnCheck;
                            break;
                        }
                    }
                }
            }
            catch (Exception exCheck) {
                Logger.Log($"  [UPDATE] [Aviso] Falha ao verificar popup de sucesso prévio: {exCheck.Message}", LogLevel.Debug);
            }

            if (janelaSucessoPrevia != null && btnFecharSucessoPrevio != null) {
                Logger.Log("  [UPDATE] A atualização já foi concluída! Popup de sucesso detectado. Pulando direto para o fechamento...");
                FinalizarEFecharPopupSucesso(janelaSucessoPrevia, btnFecharSucessoPrevio);
                return;
            }

            // 2. Fluxo Normal: Re-localiza a janela principal "Promob Update"
            Logger.Log("  [UPDATE] Re-localizando janela 'Promob Update' no desktop...");
            Window? janelaUpdate = null;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 10000){
                var janelas = automation.GetDesktop().FindAllChildren();
                foreach (var child in janelas){
                    if (child.ControlType != FlaUI.Core.Definitions.ControlType.Window) continue;
                    try{
                        var titulo = child.Name ?? child.Properties.Name.ValueOrDefault ?? "";
                        if (titulo.Contains("Promob Update", StringComparison.OrdinalIgnoreCase)){
                            janelaUpdate = child.AsWindow();
                            break;
                        }
                    }
                    catch { }
                }
                if (janelaUpdate != null) break;
                Thread.Sleep(500);
            }

            if (janelaUpdate == null){
                throw new Exception("[UPDATE] Janela 'Promob Update' não encontrada para clicar em Atualizar.");
            }

            Logger.Log("  [UPDATE] Janela localizada. Forçando foco...");
            try { janelaUpdate.SetForeground(); } catch { }
            InteractionHelper.EsperarUiRespirar(800);
            try { janelaUpdate.SetForeground(); } catch { }
            InteractionHelper.EsperarUiRespirar(1200);

            Logger.Log("  [UPDATE] Aguardando o carregamento das atualizações e o aparecimento do botão 'Atualizar' no rodapé...");
            
            AutomationElement? btnAtualizar = null;
            var swCarregando = Stopwatch.StartNew();
            const int timeoutCarregamentoMs = 120000; // Aguarda até 2 minutos (120 segundos)
            
            while (swCarregando.ElapsedMilliseconds < timeoutCarregamentoMs){
                var todosElementos = janelaUpdate.FindAllDescendants();
                
                // 1. Tenta encontrar diretamente pelo AutomationId 'btnUpdate' e se ele está habilitado
                btnAtualizar = todosElementos.FirstOrDefault(e =>
                    (e.ControlType == FlaUI.Core.Definitions.ControlType.Button || e.ControlType == FlaUI.Core.Definitions.ControlType.Custom) &&
                    (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnUpdate", StringComparison.OrdinalIgnoreCase) &&
                    e.IsEnabled
                );

                if (btnAtualizar != null) {
                    Logger.Log("  [UPDATE] Botão 'btnUpdate' localizado e habilitado!");
                    break;
                }

                // Fallback: se não tiver o ID, mas tivermos um botão "Atualizar" no rodapé
                // (próximo ao botão "Fechar" do rodapé, não o Close do topo)
                var btnFecharRodape = todosElementos.FirstOrDefault(e =>
                    e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                    (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnClose", StringComparison.OrdinalIgnoreCase)
                );

                if (btnFecharRodape != null) {
                    var candidatos = todosElementos.Where(e =>
                        (e.ControlType == FlaUI.Core.Definitions.ControlType.Button || e.ControlType == FlaUI.Core.Definitions.ControlType.Custom) &&
                        (e.Name ?? "").Contains("Atualizar", StringComparison.OrdinalIgnoreCase) &&
                        e.IsEnabled
                    ).ToList();

                    var yFechar = btnFecharRodape.BoundingRectangle.Y;
                    // Procura o candidato que esteja no mesmo alinhamento Y do botão Fechar (com tolerância de 25 pixels)
                    var candidatoRodape = candidatos
                        .FirstOrDefault(c => Math.Abs(c.BoundingRectangle.Y - yFechar) < 25);

                    if (candidatoRodape != null) {
                        btnAtualizar = candidatoRodape;
                        Logger.Log($"  [UPDATE] Botão 'Atualizar' no rodapé localizado via proximidade de 'Fechar'!");
                        break;
                    }
                }

                // 2. Verifica se a verificação de atualizações terminou e NÃO há novas atualizações
                var txtSemAtualizacao = todosElementos.FirstOrDefault(e =>
                    (e.Name ?? "").Contains("Não existem novas atualizações", StringComparison.OrdinalIgnoreCase) ||
                    (e.Name ?? "").Contains("Não existem atualizações", StringComparison.OrdinalIgnoreCase)
                );

                if (txtSemAtualizacao != null) {
                    Logger.Log("  [UPDATE] Verificação Concluída: 'Não existem novas atualizações' detectado!");
                    
                    // Localiza o botão "Fechar" no rodapé para encerrar a janela
                    var btnFecharRodapeUpdate = todosElementos.FirstOrDefault(e =>
                        e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                        ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnClose", StringComparison.OrdinalIgnoreCase) ||
                         (e.Name ?? "").Equals("Fechar", StringComparison.OrdinalIgnoreCase) && !(e.Properties.AutomationId.ValueOrDefault ?? "").Equals("Close", StringComparison.OrdinalIgnoreCase))
                    );

                    if (btnFecharRodapeUpdate != null) {
                        Logger.Log("  [UPDATE] Clicando no botão 'Fechar' para encerrar o assistente...");
                        
                        // Garante foco
                        try { janelaUpdate.SetForeground(); } catch {}
                        InteractionHelper.EsperarUiRespirar(200);
                        try { btnFecharRodapeUpdate.Focus(); } catch {}
                        InteractionHelper.EsperarUiRespirar(200);

                        // Clique físico por coordenadas
                        var rectFechar = btnFecharRodapeUpdate.BoundingRectangle;
                        if (!rectFechar.IsEmpty) {
                            try {
                                int x = (int)(rectFechar.X + (rectFechar.Width / 2));
                                int y = (int)(rectFechar.Y + (rectFechar.Height / 2));
                                Mouse.MoveTo(x, y);
                                InteractionHelper.EsperarUiRespirar(250);
                                Mouse.Click();
                                InteractionHelper.EsperarUiRespirar(1000);
                            }
                            catch {}
                        }

                        // Clique fallback UIA
                        InteractionHelper.ClicarComFallback(btnFecharRodapeUpdate);
                        InteractionHelper.EsperarUiRespirar(800);

                        // ==============================================================================
                        // CONFIRMAR O FECHAMENTO/CANCELAMENTO CLICANDO EM "SIM" NO MODAL DE ALERTA
                        // ==============================================================================
                        Logger.Log("  [UPDATE] Aguardando o surgimento do alerta de confirmação 'Sim' (Alerta) para fechar o assistente (até 15 segundos)...");
                        
                        AutomationElement? btnSim = null;
                        var swSim = Stopwatch.StartNew();
                        const int timeoutSimMs = 15000; // 15 segundos
                        
                        while (swSim.ElapsedMilliseconds < timeoutSimMs) {
                            var todosElementosAlerta = janelaUpdate.FindAllDescendants();
                            
                            // Busca o botão "Sim"
                            btnSim = todosElementosAlerta.FirstOrDefault(e =>
                                (e.ControlType == FlaUI.Core.Definitions.ControlType.Button) &&
                                ((e.Name ?? "").Equals("Sim", StringComparison.OrdinalIgnoreCase) ||
                                 (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnYes", StringComparison.OrdinalIgnoreCase)) &&
                                e.IsEnabled
                            );

                            if (btnSim != null) {
                                Logger.Log("  [UPDATE] Botão 'Sim' de confirmação localizado e habilitado!");
                                break;
                            }

                            Thread.Sleep(500);
                        }

                        if (btnSim != null) {
                            Logger.Log($"  [UPDATE] Botão 'Sim' definido: Tipo={btnSim.ControlType}, Nome='{btnSim.Name}'");
                            
                            // Garante foco
                            try { janelaUpdate.SetForeground(); } catch {}
                            InteractionHelper.EsperarUiRespirar(200);
                            try { btnSim.Focus(); } catch {}
                            InteractionHelper.EsperarUiRespirar(200);

                            // Clique físico por coordenadas
                            var rectSim = btnSim.BoundingRectangle;
                            if (!rectSim.IsEmpty) {
                                try {
                                    int x = (int)(rectSim.X + (rectSim.Width / 2));
                                    int y = (int)(rectSim.Y + (rectSim.Height / 2));
                                    Mouse.MoveTo(x, y);
                                    InteractionHelper.EsperarUiRespirar(250);
                                    Mouse.Click();
                                    InteractionHelper.EsperarUiRespirar(1000);
                                }
                                catch {}
                            }

                            // Fallback UIA
                            InteractionHelper.ClicarComFallback(btnSim);
                            InteractionHelper.EsperarUiRespirar(300);

                            // Fallback teclado
                            try {
                                btnSim.Focus();
                                InteractionHelper.EsperarUiRespirar(150);
                                Keyboard.Type(VirtualKeyShort.ENTER);
                                InteractionHelper.EsperarUiRespirar(150);
                                Keyboard.Type(VirtualKeyShort.SPACE);
                            }
                            catch {}
                            
                            Logger.Log("  [UPDATE] Clique em 'Sim' efetuado! Assistente encerrado com sucesso.");
                        }
                        else {
                            Logger.Log("  [UPDATE] [Aviso] Botão 'Sim' de confirmação de fechamento não apareceu na tela.", LogLevel.Warn);
                        }
                        
                        Logger.Log("  [UPDATE] Janela de atualizações fechada com sucesso!");
                        Logger.Log("══════════════════════════════════════════");
                        return; // Retorna com sucesso
                    }
                    else {
                        Logger.Log("  [UPDATE] [Aviso] Botão 'Fechar' não pôde ser localizado após verificação sem atualizações.", LogLevel.Warn);
                    }
                }

                // Log periódico do progresso da busca a cada 4 segundos
                if (((int)swCarregando.Elapsed.TotalSeconds) % 4 == 0) {
                    var txtBuscando = todosElementos.FirstOrDefault(e => 
                        (e.Name ?? "").Contains("Buscando atualizações", StringComparison.OrdinalIgnoreCase) ||
                        (e.Name ?? "").Contains("Verificando arquivos", StringComparison.OrdinalIgnoreCase)
                    );
                    if (txtBuscando != null) {
                        Logger.Log($"  [UPDATE] Promob ainda está buscando atualizações ('{txtBuscando.Name}'). Aguardando...");
                    }
                    else {
                        Logger.Log("  [UPDATE] Aguardando o carregamento/verificação de arquivos terminar...");
                    }
                }

                Thread.Sleep(1000);
            }

            if (btnAtualizar == null){
                // Caso não tenha encontrado, tenta um scan e log final antes de falhar
                var todosElementosFinal = janelaUpdate.FindAllDescendants();
                var botoes = todosElementosFinal.Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button).ToList();
                Logger.Log($"  [UPDATE] [Erro] Botões disponíveis na janela após timeout: " +
                    string.Join(" | ", botoes.Select(b => $"'{b.Name}' (ID: {b.Properties.AutomationId.ValueOrDefault})")));
                throw new Exception("[UPDATE] Timeout: O botão 'Atualizar' (btnUpdate) não apareceu no rodapé da janela do Promob Update.");
            }

            Logger.Log($"  [UPDATE] Botão 'Atualizar' definido: Tipo={btnAtualizar.ControlType}, Nome='{btnAtualizar.Name}', Id='{btnAtualizar.Properties.AutomationId.ValueOrDefault}'");
            
            // Garante foco
            try { janelaUpdate.SetForeground(); } catch {}
            InteractionHelper.EsperarUiRespirar(300);
            try { btnAtualizar.Focus(); } catch {}
            InteractionHelper.EsperarUiRespirar(300);

            // ESTRATÉGIA 1: Clique Físico de Mouse usando coordenadas absolutas (Muito seguro para botões customizados)
            var rectBotao = btnAtualizar.BoundingRectangle;
            if (!rectBotao.IsEmpty) {
                try {
                    int x = (int)(rectBotao.X + (rectBotao.Width / 2));
                    int y = (int)(rectBotao.Y + (rectBotao.Height / 2));
                    
                    Logger.Log($"  [UPDATE] [CLIQUE FÍSICO] Movendo cursor e clicando em X={x}, Y={y}...");
                    Mouse.MoveTo(x, y);
                    InteractionHelper.EsperarUiRespirar(250);
                    Mouse.Click();
                    InteractionHelper.EsperarUiRespirar(1000);
                }
                catch (Exception exMouse) {
                    Logger.Log($"  [UPDATE] [Aviso] Falha no clique físico de mouse: {exMouse.Message}", LogLevel.Warn);
                }
            }

            // ESTRATÉGIA 2: Clicar com Fallback (UIA Invoke -> UIA Click -> Keyboard Space)
            Logger.Log("  [UPDATE] [UIA FALLBACK] Acionando ClicarComFallback...");
            InteractionHelper.ClicarComFallback(btnAtualizar);
            InteractionHelper.EsperarUiRespirar(500);

            // ESTRATÉGIA 3: Envio Direto de Teclado (Focus + ENTER / ESPAÇO)
            try {
                Logger.Log("  [UPDATE] [KEYBOARD FALLBACK] Enviando teclas Foco + ENTER + SPACE...");
                btnAtualizar.Focus();
                InteractionHelper.EsperarUiRespirar(150);
                Keyboard.Type(VirtualKeyShort.ENTER);
                InteractionHelper.EsperarUiRespirar(150);
                Keyboard.Type(VirtualKeyShort.SPACE);
            }
            catch (Exception exKey) {
                Logger.Log($"  [UPDATE] [Aviso] Falha ao enviar teclado: {exKey.Message}", LogLevel.Debug);
            }

            Logger.Log("  [UPDATE] Sequência de cliques em 'Atualizar' finalizada.");

            // ==================================================================================
            // NOVO PROCESSO: AGUARDAR O DOWNLOAD E CLICAR EM "INSTALAR"
            // ==================================================================================
            Logger.Log("  [UPDATE] Aguardando o download das atualizações concluir (até 10 minutos)...");
            
            AutomationElement? btnInstalar = null;
            var swDownload = Stopwatch.StartNew();
            const int timeoutDownloadMs = 600000; // 10 minutos
            
            while (swDownload.ElapsedMilliseconds < timeoutDownloadMs) {
                // Re-localiza a janela e os elementos frescos
                var todosElementosFresh = janelaUpdate.FindAllDescendants();
                
                // Tenta encontrar o botão pelo nome "Instalar" ou pelo ID de automação que possa ter mudado para 'btnUpdate' ou 'btnInstall'
                btnInstalar = todosElementosFresh.FirstOrDefault(e =>
                    (e.ControlType == FlaUI.Core.Definitions.ControlType.Button || e.ControlType == FlaUI.Core.Definitions.ControlType.Custom) &&
                    ((e.Name ?? "").Contains("Instalar", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnInstall", StringComparison.OrdinalIgnoreCase) ||
                     ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnUpdate", StringComparison.OrdinalIgnoreCase) && (e.Name ?? "").Contains("Instalar", StringComparison.OrdinalIgnoreCase))) &&
                    e.IsEnabled
                );

                if (btnInstalar != null) {
                    Logger.Log("  [UPDATE] Botão 'Instalar' localizado e habilitado!");
                    break;
                }

                // Loga o status do download a cada 5 segundos
                if (((int)swDownload.Elapsed.TotalSeconds) % 5 == 0) {
                    var txtProgresso = todosElementosFresh.FirstOrDefault(e =>
                        (e.Name ?? "").Contains("Baixando", StringComparison.OrdinalIgnoreCase) ||
                        (e.Name ?? "").Contains("Download", StringComparison.OrdinalIgnoreCase) ||
                        (e.Name ?? "").Contains("%", StringComparison.OrdinalIgnoreCase)
                    );

                    if (txtProgresso != null) {
                        Logger.Log($"  [UPDATE] Baixando atualizações: '{txtProgresso.Name}'. Por favor, aguarde...");
                    }
                    else {
                        Logger.Log($"  [UPDATE] Download em andamento... (Tempo decorrido: {swDownload.Elapsed.Minutes}m {swDownload.Elapsed.Seconds}s)");
                    }
                }

                Thread.Sleep(1500);
            }

            if (btnInstalar == null) {
                throw new Exception("[UPDATE] Timeout: O download demorou mais de 10 minutos ou o botão 'Instalar' não apareceu.");
            }

            Logger.Log($"  [UPDATE] Botão 'Instalar' definido: Tipo={btnInstalar.ControlType}, Nome='{btnInstalar.Name}', Id='{btnInstalar.Properties.AutomationId.ValueOrDefault}'");
            
            // Garante foco
            try { janelaUpdate.SetForeground(); } catch {}
            InteractionHelper.EsperarUiRespirar(300);
            try { btnInstalar.Focus(); } catch {}
            InteractionHelper.EsperarUiRespirar(300);

            // ESTRATÉGIA 1: Clique Físico de Mouse usando coordenadas absolutas (Muito seguro)
            var rectInstalar = btnInstalar.BoundingRectangle;
            if (!rectInstalar.IsEmpty) {
                try {
                    int x = (int)(rectInstalar.X + (rectInstalar.Width / 2));
                    int y = (int)(rectInstalar.Y + (rectInstalar.Height / 2));
                    
                    Logger.Log($"  [UPDATE] [CLIQUE FÍSICO INSTALAR] Movendo cursor e clicando em X={x}, Y={y}...");
                    Mouse.MoveTo(x, y);
                    InteractionHelper.EsperarUiRespirar(250);
                    Mouse.Click();
                    InteractionHelper.EsperarUiRespirar(1000);
                }
                catch (Exception exMouse) {
                    Logger.Log($"  [UPDATE] [Aviso] Falha no clique físico de mouse em Instalar: {exMouse.Message}", LogLevel.Warn);
                }
            }

            // ESTRATÉGIA 2: Clicar com Fallback (UIA Invoke -> UIA Click -> Keyboard Space)
            Logger.Log("  [UPDATE] [UIA FALLBACK INSTALAR] Acionando ClicarComFallback...");
            InteractionHelper.ClicarComFallback(btnInstalar);
            InteractionHelper.EsperarUiRespirar(500);

            // ESTRATÉGIA 3: Envio Direto de Teclado (Focus + ENTER / ESPAÇO)
            try {
                Logger.Log("  [UPDATE] [KEYBOARD FALLBACK INSTALAR] Enviando teclas Foco + ENTER + SPACE...");
                btnInstalar.Focus();
                InteractionHelper.EsperarUiRespirar(150);
                Keyboard.Type(VirtualKeyShort.ENTER);
                InteractionHelper.EsperarUiRespirar(150);
                Keyboard.Type(VirtualKeyShort.SPACE);
            }
            catch (Exception exKey) {
                Logger.Log($"  [UPDATE] [Aviso] Falha ao enviar teclado para Instalar: {exKey.Message}", LogLevel.Debug);
            }

            Logger.Log("  [UPDATE] Clique em 'Instalar' concluído com sucesso!");

            // ==================================================================================
            // NOVO PROCESSO: CLICAR NO BOTÃO "OK" DA JANELA DE ALERTA DE FECHAMENTO
            // ==================================================================================
            Logger.Log("  [UPDATE] Aguardando o surgimento da confirmação 'Ok' (Alerta) na tela para fechar o Promob (até 25 segundos)...");
            
            AutomationElement? btnOk = null;
            var swAlerta = Stopwatch.StartNew();
            const int timeoutAlertaMs = 25000; // 25 segundos (dando bastante margem)
            
            while (swAlerta.ElapsedMilliseconds < timeoutAlertaMs) {
                var todosElementosFresh = janelaUpdate.FindAllDescendants();
                
                // Encontra candidatos pelo Nome "Ok" ou pelo AutomationId "btnOk"/"btnOK"
                var candidatosOk = todosElementosFresh.Where(e =>
                    ((e.Name ?? "").Equals("Ok", StringComparison.OrdinalIgnoreCase) ||
                     (e.Name ?? "").Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnOk", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnOK", StringComparison.OrdinalIgnoreCase)) &&
                    e.IsEnabled
                ).ToList();

                if (candidatosOk.Count > 0) {
                    // Escolhe preferencialmente o elemento de ControlType Button
                    btnOk = candidatosOk.FirstOrDefault(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button);
                    if (btnOk == null) {
                        btnOk = candidatosOk.First(); // Fallback para TextBlock ou outro elemento
                    }
                    
                    Logger.Log($"  [UPDATE] Elemento 'Ok' localizado! Nome: '{btnOk.Name}', Tipo: {btnOk.ControlType}, Id: '{btnOk.Properties.AutomationId.ValueOrDefault}'");
                    break;
                }

                Thread.Sleep(500);
            }

            if (btnOk == null) {
                throw new Exception("[UPDATE] Timeout: O botão 'Ok' de confirmação de fechamento não apareceu na tela.");
            }

            if (btnOk != null) {
                Logger.Log($"  [UPDATE] Botão 'Ok' definido para ação: Tipo={btnOk.ControlType}, Nome='{btnOk.Name}', Id='{btnOk.Properties.AutomationId.ValueOrDefault}'");
                
                // Garante foco
                try { janelaUpdate.SetForeground(); } catch {}
                InteractionHelper.EsperarUiRespirar(200);
                try { btnOk.Focus(); } catch {}
                InteractionHelper.EsperarUiRespirar(200);

                // ESTRATÉGIA 1: Clique Físico por coordenadas absolutas (Mouse real)
                var rectOk = btnOk.BoundingRectangle;
                if (!rectOk.IsEmpty) {
                    try {
                        int x = (int)(rectOk.X + (rectOk.Width / 2));
                        int y = (int)(rectOk.Y + (rectOk.Height / 2));
                        
                        Logger.Log($"  [UPDATE] [CLIQUE FÍSICO OK] Movendo cursor e clicando em X={x}, Y={y}...");
                        Mouse.MoveTo(x, y);
                        InteractionHelper.EsperarUiRespirar(250);
                        Mouse.Click();
                        InteractionHelper.EsperarUiRespirar(1000);
                    }
                    catch (Exception exMouse) {
                        Logger.Log($"  [UPDATE] [Aviso] Falha no clique físico de mouse em Ok: {exMouse.Message}", LogLevel.Warn);
                    }
                }

                // ESTRATÉGIA 2: Clicar com Fallback (UIA Invoke -> UIA Click -> Keyboard Space)
                Logger.Log("  [UPDATE] [UIA FALLBACK OK] Acionando ClicarComFallback...");
                InteractionHelper.ClicarComFallback(btnOk);
                InteractionHelper.EsperarUiRespirar(500);

                // ESTRATÉGIA 3: Teclado Focus + Enter/Space
                try {
                    Logger.Log("  [UPDATE] [KEYBOARD FALLBACK OK] Enviando Focus + ENTER + SPACE...");
                    btnOk.Focus();
                    InteractionHelper.EsperarUiRespirar(150);
                    Keyboard.Type(VirtualKeyShort.ENTER);
                    InteractionHelper.EsperarUiRespirar(150);
                    Keyboard.Type(VirtualKeyShort.SPACE);
                }
                catch {}
                
                Logger.Log("  [UPDATE] Clique no botão 'Ok' do Alerta concluído com sucesso! Promob será fechado e atualizado.");
            }
            else {
                Logger.Log("  [UPDATE] [Aviso] Botão 'Ok' de confirmação de fechamento não pôde ser localizado.", LogLevel.Warn);
            }

            // ==================================================================================
            // NOVO PROCESSO: CLICAR NO BOTÃO "FECHAR" DA JANELA DE SUCESSO DA INSTALAÇÃO
            // ==================================================================================
            Logger.Log("  [UPDATE] Aguardando a conclusão da instalação e o surgimento do popup 'Execução foi bem sucedida' (até 5 minutos)...");
            
            AutomationElement? btnFecharSucesso = null;
            Window? janelaSucesso = null;
            var swSucesso = Stopwatch.StartNew();
            const int timeoutSucessoMs = 300000; // 5 minutos
            
            while (swSucesso.ElapsedMilliseconds < timeoutSucessoMs) {
                try {
                    var janelasDesktop = automation.GetDesktop().FindAllChildren();
                    foreach (var child in janelasDesktop) {
                        if (child.ControlType != FlaUI.Core.Definitions.ControlType.Window) continue;
                        
                        string nome = "";
                        try { nome = child.Name ?? ""; } catch { continue; }
                        
                        // O popup final de sucesso chama-se exatamente "PromobUpdate" (sem espaços)
                        if (nome.Equals("PromobUpdate", StringComparison.OrdinalIgnoreCase)) {
                            var descSucesso = child.FindAllDescendants();
                            var btnCheck = descSucesso.FirstOrDefault(e =>
                                (e.ControlType == FlaUI.Core.Definitions.ControlType.Button) &&
                                ((e.Name ?? "").Equals("Fechar", StringComparison.OrdinalIgnoreCase) ||
                                 (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnClose", StringComparison.OrdinalIgnoreCase))
                            );

                            if (btnCheck != null) {
                                janelaSucesso = child.AsWindow();
                                btnFecharSucesso = btnCheck;
                                break;
                            }
                        }
                    }
                }
                catch (Exception exLoop) {
                    Logger.Log($"  [UPDATE] [Espera] Conectando ao assistente ({exLoop.Message}). Monitorando...", LogLevel.Debug);
                }

                if (btnFecharSucesso != null) {
                    break;
                }

                if (((int)swSucesso.Elapsed.TotalSeconds) % 10 == 0) {
                    Logger.Log($"  [UPDATE] Instalando atualizações... (Tempo decorrido: {swSucesso.Elapsed.Minutes}m {swSucesso.Elapsed.Seconds}s)");
                }

                Thread.Sleep(2000);
            }

            if (btnFecharSucesso == null || janelaSucesso == null) {
                throw new Exception("[UPDATE] Timeout: O popup de conclusão da instalação não apareceu após 5 minutos.");
            }

            FinalizarEFecharPopupSucesso(janelaSucesso, btnFecharSucesso);
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Executa a sequência redundante de cliques e atalhos de teclado para focar e
        /// acionar o botão "Fechar" da janela de sucesso "PromobUpdate".
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static void FinalizarEFecharPopupSucesso(Window janelaSucesso, AutomationElement btnFecharSucesso) {
            Logger.Log($"  [UPDATE] Botão 'Fechar' de sucesso definido: Tipo={btnFecharSucesso.ControlType}, Nome='{btnFecharSucesso.Name}', Id='{btnFecharSucesso.Properties.AutomationId.ValueOrDefault}'");
            
            // Garante foco na janela de sucesso
            try { janelaSucesso.SetForeground(); } catch {}
            InteractionHelper.EsperarUiRespirar(300);
            try { btnFecharSucesso.Focus(); } catch {}
            InteractionHelper.EsperarUiRespirar(300);

            // ESTRATÉGIA 1: Clique Físico por coordenadas
            var rectFecharSucesso = btnFecharSucesso.BoundingRectangle;
            if (!rectFecharSucesso.IsEmpty) {
                try {
                    int x = (int)(rectFecharSucesso.X + (rectFecharSucesso.Width / 2));
                    int y = (int)(rectFecharSucesso.Y + (rectFecharSucesso.Height / 2));
                    
                    Logger.Log($"  [UPDATE] [CLIQUE FÍSICO FECHAR SUCESSO] Movendo cursor e clicando em X={x}, Y={y}...");
                    Mouse.MoveTo(x, y);
                    InteractionHelper.EsperarUiRespirar(250);
                    Mouse.Click();
                    InteractionHelper.EsperarUiRespirar(1000);
                }
                catch (Exception exMouse) {
                    Logger.Log($"  [UPDATE] [Aviso] Falha no clique físico em Fechar Sucesso: {exMouse.Message}", LogLevel.Warn);
                }
            }

            // ESTRATÉGIA 2: Clicar com Fallback (UIA Invoke -> UIA Click -> Keyboard Space)
            Logger.Log("  [UPDATE] [UIA FALLBACK FECHAR SUCESSO] Acionando ClicarComFallback...");
            InteractionHelper.ClicarComFallback(btnFecharSucesso);
            InteractionHelper.EsperarUiRespirar(500);

            // ESTRATÉGIA 3: Teclado Focus + Enter/Space
            try {
                Logger.Log("  [UPDATE] [KEYBOARD FALLBACK FECHAR SUCESSO] Enviando Focus + ENTER + SPACE...");
                btnFecharSucesso.Focus();
                InteractionHelper.EsperarUiRespirar(150);
                Keyboard.Type(VirtualKeyShort.ENTER);
                InteractionHelper.EsperarUiRespirar(150);
                Keyboard.Type(VirtualKeyShort.SPACE);
            }
            catch {}
            
            Logger.Log("  [UPDATE] Clique no botão 'Fechar' de sucesso concluído! A atualização foi finalizada com êxito!");
            Logger.Log("══════════════════════════════════════════");
        }

    }
}
