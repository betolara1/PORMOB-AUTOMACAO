using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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

        // ==================================================================================
        // WIN32 P/INVOKE — necessário para detectar janelas ocultas/em segundo plano
        // ==================================================================================

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT {
            public int length;
            public int flags;
            public int showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Rectangle rcNormalPosition;
        }

        private const int SW_RESTORE  = 9;
        private const int SW_SHOW     = 5;
        private const int SW_SHOWNA   = 8;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMINIMIZED = 2;  // janela minimizada na barra de tarefas
        private const int SW_HIDE     = 0;       // janela completamente oculta
        private const int GWL_STYLE   = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_VISIBLE  = 0x10000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080; // oculto da barra de tarefas

        // ==================================================================================
        // MÉTODOS PÚBLICOS
        // ==================================================================================

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Executa a rotina completa de atualização acionada pelo botão "Atualizar Promob" na UI.
            /// Abre o menu Arquivo, clica em "Atualizar o Promob", e delega para o fluxo unificado.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        //--------------------------------------------------------------------------------------
        public static void ExecutarAtualizacao(UIA3Automation automation){
            Logger.Log("══════════════════════════════════════════");
            Logger.Log("║   Iniciando Rotina de Atualização      ║");
            Logger.Log("══════════════════════════════════════════");

            Logger.Log("  [1/4] Localizando janela principal do Promob...");
            var janela = PromobWindowHelper.AguardarJanelaPromob(automation, 15000)
                ?? throw new Exception("Janela do Promob não encontrada. Abra o Promob antes de atualizar.");

            InteractionHelper.AtivarJanela(janela);
            
            Logger.Log("  [2/4] Localizando menu 'Arquivo'...");
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

            // Verifica se o botão "Atualizar o Promob" já está visível e habilitado na tela.
            // Se já estiver visível (Ribbon expandido na aba Arquivo), clicar no menu "Arquivo" iria
            // recolher/esconder o Ribbon, fazendo o botão desaparecer!
            Logger.Log("  [2.5/4] Verificando se o botão 'Atualizar o Promob' já está visível...");
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
                Logger.Log("  [3/4] Clicando no menu 'Arquivo' para abrir as opções...");
                InteractionHelper.ClicarComFallback(menuArquivo);
                InteractionHelper.EsperarUiRespirar(1000);
            }

            Logger.Log("  [3/4] Localizando botão 'Atualizar o Promob' (ID: OpenProcadUpdate)...");
            AutomationElement? btnAtualizarPromob = LocalizarBotaoAtualizarPromob(automation, buscaEm);

            if (btnAtualizarPromob == null){
                throw new Exception("Botão 'Atualizar o Promob' (OpenProcadUpdate) não encontrado no menu 'Arquivo' (tentativas na janela principal e popups do desktop esgotadas).");
            }

            Logger.Log("  [4/4] Clicando em 'Atualizar o Promob'...");
            InteractionHelper.ClicarComFallback(btnAtualizarPromob);

            Logger.Log("  [INFO] Aguardando janela do 'Promob Update' abrir...");
            Window? janelaUpdate = AguardarJanelaUpdate(automation, 25000);

            if (janelaUpdate == null){
                throw new Exception("Janela 'Promob Update' não apareceu a tempo.");
            }

            Logger.Log("  [OK] Janela 'Promob Update' detectada com sucesso!");

            // Delega para o fluxo unificado de verificação + atualização
            VerificarStatusEAtualizar(janelaUpdate, automation);

            Logger.Log("══════════════════════════════════════════");
            Logger.Log("║   Rotina de Atualização Concluída      ║");
            Logger.Log("══════════════════════════════════════════");
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Interage com a janela "Promob Update" que foi aberta automaticamente pelo próprio Promob.
            /// Ao contrário de <see cref="ExecutarAtualizacao"/>, aqui a janela já está aberta —
            /// o método apenas a localiza e delega para o fluxo unificado.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        //--------------------------------------------------------------------------------------
        public static void ExecutarAtualizacaoUpdate(UIA3Automation automation){
            Logger.Log("══════════════════════════════════════════");
            Logger.Log("║  [UPDATE] Iniciando Atualização Auto   ║");
            Logger.Log("══════════════════════════════════════════");

            // 1. ANTES DE QUALQUER COISA: Verifica se o popup de sucesso "PromobUpdate" já está aberto.
            // Se estiver, pulamos todas as etapas intermediárias e fechamos ele diretamente.
            Logger.Log("  [UPDATE] Verificando se a atualização já foi concluída anteriormente...");
            var (janelaSucessoPrevia, btnFecharSucessoPrevio) = BuscarPopupSucessoNoDesktop(automation);

            if (janelaSucessoPrevia != null && btnFecharSucessoPrevio != null) {
                Logger.Log("  [UPDATE] A atualização já foi concluída! Popup de sucesso detectado. Pulando direto para o fechamento...");
                FinalizarEFecharPopupSucesso(janelaSucessoPrevia, btnFecharSucessoPrevio);
                return;
            }

            // 2. Fluxo Normal: Re-localiza a janela principal "Promob Update"
            Logger.Log("  [UPDATE] Re-localizando janela 'Promob Update' no desktop...");
            Window? janelaUpdate = AguardarJanelaUpdate(automation, 10000);

            if (janelaUpdate == null){
                throw new Exception("[UPDATE] Janela 'Promob Update' não encontrada para clicar em Atualizar.");
            }

            Logger.Log("  [UPDATE] Janela localizada. Forçando foco...");
            try { janelaUpdate.SetForeground(); } catch { }
            InteractionHelper.EsperarUiRespirar(800);
            try { janelaUpdate.SetForeground(); } catch { }
            InteractionHelper.EsperarUiRespirar(1200);

            // Delega para o fluxo unificado de verificação + atualização
            VerificarStatusEAtualizar(janelaUpdate, automation);

            Logger.Log("══════════════════════════════════════════");
            Logger.Log("║  [UPDATE] Atualização Auto Concluída   ║");
            Logger.Log("══════════════════════════════════════════");
        }

        // ==================================================================================
        // MÉTODOS PRIVADOS — FLUXO UNIFICADO
        // ==================================================================================

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Fluxo unificado: clica na aba "Status", verifica se há módulos desatualizados
            /// e decide se fecha a janela ou inicia o processo completo de atualização.
            /// </summary>
        //--------------------------------------------------------------------------------------
        private static void VerificarStatusEAtualizar(Window janelaUpdate, UIA3Automation automation){
            InteractionHelper.AtivarJanela(janelaUpdate);
            InteractionHelper.EsperarUiRespirar(1500);

            // --- Clicar na aba "Status" ---
            Logger.Log("  [STATUS] Procurando e clicando na aba/botão 'Status' no menu lateral esquerdo...");
            var btnStatus = janelaUpdate.FindFirstDescendant(cf => cf.ByName("Status"))
                ?? janelaUpdate.FindAllDescendants()
                    .FirstOrDefault(e => (e.Name ?? "").Contains("Status", StringComparison.OrdinalIgnoreCase));

            if (btnStatus == null){
                throw new Exception("Aba/Botão 'Status' não encontrado na janela 'Promob Update'.");
            }

            InteractionHelper.ClicarComFallback(btnStatus);
            Logger.Log("  [STATUS] Aba 'Status' clicada. Aguardando 3 segundos para carregar o status dos módulos...");
            InteractionHelper.EsperarUiRespirar(3000);

            // --- Analisar status dos módulos ---
            Logger.Log("  [STATUS] Analisando status dos módulos do Promob...");
            var todosElementos = janelaUpdate.FindAllDescendants().ToList();
            
            bool temDesatualizado = todosElementos.Any(e => 
                (e.Name ?? "").Contains("Desatualizado", StringComparison.OrdinalIgnoreCase) ||
                (e.Properties.AutomationId.ValueOrDefault ?? "").Contains("Desatualizado", StringComparison.OrdinalIgnoreCase)
            );

            if (!temDesatualizado){
                Logger.Log("  [OK] Parabéns! Todos os módulos do Promob estão 100% ATUALIZADOS.");
                FecharJanela(janelaUpdate);
                return;
            }

            // --- Há módulos desatualizados: iniciar atualização ---
            Logger.Log("  [AVISO] ATENÇÃO: Há módulos DESATUALIZADOS detectados!", LogLevel.Warn);
            Logger.Log("  [ACTION] Procurando e clicando no botão/aba 'Atualizar' para voltar à tela de atualizações...");
            
            var btnAtualizarAba = janelaUpdate.FindFirstDescendant(cf => cf.ByName("Atualizar"))
                ?? janelaUpdate.FindAllDescendants()
                    .FirstOrDefault(e => (e.Name ?? "").Equals("Atualizar", StringComparison.OrdinalIgnoreCase));

            if (btnAtualizarAba == null){
                throw new Exception("Botão/Aba 'Atualizar' não encontrado no painel lateral.");
            }

            InteractionHelper.ClicarComFallback(btnAtualizarAba);
            Logger.Log("  [OK] Clique na aba 'Atualizar' efetuado. Iniciando fluxo completo de atualização...");

            ExecutarFluxoAtualizacao(janelaUpdate, automation);
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Executa o fluxo completo de atualização após a decisão de atualizar:
            /// aguarda botão "Atualizar" no rodapé → clica → aguarda "Instalar" → clica →
            /// aguarda "Ok" no alerta → clica → aguarda popup de sucesso → fecha.
            /// Também trata o caso de "Não existem novas atualizações".
            /// </summary>
        //--------------------------------------------------------------------------------------
        private static void ExecutarFluxoAtualizacao(Window janelaUpdate, UIA3Automation automation){
            Logger.Log("  [FLUXO] Aguardando o carregamento das atualizações e o aparecimento do botão 'Atualizar' no rodapé...");
            
            AutomationElement? btnAtualizar = null;
            var swCarregando = Stopwatch.StartNew();
            const int timeoutCarregamentoMs = 120000; // 2 minutos
            
            while (swCarregando.ElapsedMilliseconds < timeoutCarregamentoMs){
                var todosElementos = janelaUpdate.FindAllDescendants();
                
                // 1. Tenta encontrar diretamente pelo AutomationId 'btnUpdate' habilitado
                btnAtualizar = todosElementos.FirstOrDefault(e =>
                    (e.ControlType == FlaUI.Core.Definitions.ControlType.Button || e.ControlType == FlaUI.Core.Definitions.ControlType.Custom) &&
                    (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnUpdate", StringComparison.OrdinalIgnoreCase) &&
                    e.IsEnabled
                );

                if (btnAtualizar != null) {
                    Logger.Log("  [FLUXO] Botão 'btnUpdate' localizado e habilitado!");
                    break;
                }

                // Fallback: botão "Atualizar" no rodapé (próximo ao botão "Fechar" do rodapé)
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
                    var candidatoRodape = candidatos
                        .FirstOrDefault(c => Math.Abs(c.BoundingRectangle.Y - yFechar) < 25);

                    if (candidatoRodape != null) {
                        btnAtualizar = candidatoRodape;
                        Logger.Log("  [FLUXO] Botão 'Atualizar' no rodapé localizado via proximidade de 'Fechar'!");
                        break;
                    }
                }

                // 2. Verifica se "Não existem novas atualizações" — nesse caso, fecha a janela
                var txtSemAtualizacao = todosElementos.FirstOrDefault(e =>
                    (e.Name ?? "").Contains("Não existem novas atualizações", StringComparison.OrdinalIgnoreCase) ||
                    (e.Name ?? "").Contains("Não existem atualizações", StringComparison.OrdinalIgnoreCase)
                );

                if (txtSemAtualizacao != null) {
                    Logger.Log("  [FLUXO] Verificação Concluída: 'Não existem novas atualizações' detectado!");
                    FecharJanela(janelaUpdate);
                    return;
                }

                // Log periódico do progresso a cada 4 segundos
                if (((int)swCarregando.Elapsed.TotalSeconds) % 4 == 0) {
                    var txtBuscando = todosElementos.FirstOrDefault(e => 
                        (e.Name ?? "").Contains("Buscando atualizações", StringComparison.OrdinalIgnoreCase) ||
                        (e.Name ?? "").Contains("Verificando arquivos", StringComparison.OrdinalIgnoreCase)
                    );
                    if (txtBuscando != null) {
                        Logger.Log($"  [FLUXO] Promob ainda está buscando atualizações ('{txtBuscando.Name}'). Aguardando...");
                    }
                    else {
                        Logger.Log("  [FLUXO] Aguardando o carregamento/verificação de arquivos terminar...");
                    }
                }

                Thread.Sleep(1000);
            }

            if (btnAtualizar == null){
                var todosElementosFinal = janelaUpdate.FindAllDescendants();
                var botoes = todosElementosFinal.Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button).ToList();
                Logger.Log($"  [FLUXO] [Erro] Botões disponíveis na janela após timeout: " +
                    string.Join(" | ", botoes.Select(b => $"'{b.Name}' (ID: {b.Properties.AutomationId.ValueOrDefault})")));
                throw new Exception("[FLUXO] Timeout: O botão 'Atualizar' (btnUpdate) não apareceu no rodapé da janela do Promob Update.");
            }

            // --- Clicar no botão "Atualizar" do rodapé ---
            Logger.Log($"  [FLUXO] Botão 'Atualizar' definido: Tipo={btnAtualizar.ControlType}, Nome='{btnAtualizar.Name}', Id='{btnAtualizar.Properties.AutomationId.ValueOrDefault}'");
            ClicarBotaoComEstrategias(janelaUpdate, btnAtualizar, "Atualizar");

            // --- Aguardar download e clicar em "Instalar" ---
            Logger.Log("  [FLUXO] Aguardando o download das atualizações concluir (até 10 minutos)...");
            
            AutomationElement? btnInstalar = null;
            var swDownload = Stopwatch.StartNew();
            const int timeoutDownloadMs = 600000; // 10 minutos
            
            while (swDownload.ElapsedMilliseconds < timeoutDownloadMs) {
                var todosElementosFresh = janelaUpdate.FindAllDescendants();
                
                btnInstalar = todosElementosFresh.FirstOrDefault(e =>
                    (e.ControlType == FlaUI.Core.Definitions.ControlType.Button || e.ControlType == FlaUI.Core.Definitions.ControlType.Custom) &&
                    ((e.Name ?? "").Contains("Instalar", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnInstall", StringComparison.OrdinalIgnoreCase) ||
                     ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnUpdate", StringComparison.OrdinalIgnoreCase) && (e.Name ?? "").Contains("Instalar", StringComparison.OrdinalIgnoreCase))) &&
                    e.IsEnabled
                );

                if (btnInstalar != null) {
                    Logger.Log("  [FLUXO] Botão 'Instalar' localizado e habilitado!");
                    break;
                }

                // Log do progresso do download a cada 5 segundos
                if (((int)swDownload.Elapsed.TotalSeconds) % 5 == 0) {
                    var txtProgresso = todosElementosFresh.FirstOrDefault(e =>
                        (e.Name ?? "").Contains("Baixando", StringComparison.OrdinalIgnoreCase) ||
                        (e.Name ?? "").Contains("Download", StringComparison.OrdinalIgnoreCase) ||
                        (e.Name ?? "").Contains("%", StringComparison.OrdinalIgnoreCase)
                    );

                    if (txtProgresso != null) {
                        Logger.Log($"  [FLUXO] Baixando atualizações: '{txtProgresso.Name}'. Por favor, aguarde...");
                    }
                    else {
                        Logger.Log($"  [FLUXO] Download em andamento... (Tempo decorrido: {swDownload.Elapsed.Minutes}m {swDownload.Elapsed.Seconds}s)");
                    }
                }

                Thread.Sleep(1500);
            }

            if (btnInstalar == null) {
                throw new Exception("[FLUXO] Timeout: O download demorou mais de 10 minutos ou o botão 'Instalar' não apareceu.");
            }

            Logger.Log($"  [FLUXO] Botão 'Instalar' definido: Tipo={btnInstalar.ControlType}, Nome='{btnInstalar.Name}', Id='{btnInstalar.Properties.AutomationId.ValueOrDefault}'");
            ClicarBotaoComEstrategias(janelaUpdate, btnInstalar, "Instalar");

            // --- Aguardar e clicar no botão "Ok" do alerta de fechamento ---
            Logger.Log("  [FLUXO] Aguardando o surgimento da confirmação 'Ok' (Alerta) para fechar o Promob (até 25 segundos)...");
            
            AutomationElement? btnOk = null;
            var swAlerta = Stopwatch.StartNew();
            const int timeoutAlertaMs = 25000;
            
            while (swAlerta.ElapsedMilliseconds < timeoutAlertaMs) {
                var todosElementosFresh = janelaUpdate.FindAllDescendants();
                
                var candidatosOk = todosElementosFresh.Where(e =>
                    ((e.Name ?? "").Equals("Ok", StringComparison.OrdinalIgnoreCase) ||
                     (e.Name ?? "").Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnOk", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnOK", StringComparison.OrdinalIgnoreCase)) &&
                    e.IsEnabled
                ).ToList();

                if (candidatosOk.Count > 0) {
                    btnOk = candidatosOk.FirstOrDefault(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button)
                         ?? candidatosOk.First();
                    
                    Logger.Log($"  [FLUXO] Elemento 'Ok' localizado! Nome: '{btnOk.Name}', Tipo: {btnOk.ControlType}, Id: '{btnOk.Properties.AutomationId.ValueOrDefault}'");
                    break;
                }

                Thread.Sleep(500);
            }

            if (btnOk == null) {
                throw new Exception("[FLUXO] Timeout: O botão 'Ok' de confirmação de fechamento não apareceu na tela.");
            }

            Logger.Log($"  [FLUXO] Botão 'Ok' definido para ação: Tipo={btnOk.ControlType}, Nome='{btnOk.Name}', Id='{btnOk.Properties.AutomationId.ValueOrDefault}'");
            ClicarBotaoComEstrategias(janelaUpdate, btnOk, "Ok");
            Logger.Log("  [FLUXO] Clique no botão 'Ok' do Alerta concluído com sucesso! Promob será fechado e atualizado.");

            // --- Aguardar popup de sucesso da instalação e fechar ---
            AguardarEFecharPopupSucesso(automation);
        }

        // ==================================================================================
        // MÉTODOS PRIVADOS — UTILITÁRIOS
        // ==================================================================================

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Executa a sequência completa de estratégias de clique para acionar um botão:
            /// 1. Clique físico por coordenadas absolutas (Mouse real)
            /// 2. Clique via UIA Invoke/Click fallback
            /// 3. Envio direto de teclado (Focus + ENTER + SPACE)
            /// </summary>
        //--------------------------------------------------------------------------------------
        private static void ClicarBotaoComEstrategias(Window janela, AutomationElement botao, string label){
            // Garante foco na janela e no botão
            try { janela.SetForeground(); } catch {}
            InteractionHelper.EsperarUiRespirar(300);
            try { botao.Focus(); } catch {}
            InteractionHelper.EsperarUiRespirar(300);

            // ESTRATÉGIA 1: Clique Físico por coordenadas absolutas
            var rect = botao.BoundingRectangle;
            if (!rect.IsEmpty) {
                try {
                    int x = (int)(rect.X + (rect.Width / 2));
                    int y = (int)(rect.Y + (rect.Height / 2));
                    
                    Logger.Log($"  [CLIQUE FÍSICO {label.ToUpper()}] Movendo cursor e clicando em X={x}, Y={y}...");
                    Mouse.MoveTo(x, y);
                    InteractionHelper.EsperarUiRespirar(250);
                    Mouse.Click();
                    InteractionHelper.EsperarUiRespirar(1000);
                }
                catch (Exception exMouse) {
                    Logger.Log($"  [Aviso] Falha no clique físico de mouse em '{label}': {exMouse.Message}", LogLevel.Warn);
                }
            }

            // ESTRATÉGIA 2: Clicar com Fallback UIA (Invoke -> Click -> Keyboard Space)
            Logger.Log($"  [UIA FALLBACK {label.ToUpper()}] Acionando ClicarComFallback...");
            InteractionHelper.ClicarComFallback(botao);
            InteractionHelper.EsperarUiRespirar(500);

            // ESTRATÉGIA 3: Envio Direto de Teclado (Focus + ENTER / ESPAÇO)
            try {
                Logger.Log($"  [KEYBOARD FALLBACK {label.ToUpper()}] Enviando Focus + ENTER + SPACE...");
                botao.Focus();
                InteractionHelper.EsperarUiRespirar(150);
                Keyboard.Type(VirtualKeyShort.ENTER);
                InteractionHelper.EsperarUiRespirar(150);
                Keyboard.Type(VirtualKeyShort.SPACE);
            }
            catch (Exception exKey) {
                Logger.Log($"  [Aviso] Falha ao enviar teclado para '{label}': {exKey.Message}", LogLevel.Debug);
            }
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Faz polling buscando a janela "Promob Update" até que seja encontrada ou o
            /// timeout expire. Além do scan normal do desktop via FlaUI, usa EnumWindows para
            /// detectar janelas ocultas/em segundo plano (ícone na área de notificação) e as
            /// restaura automaticamente via ShowWindow antes de retorná-las.
            /// </summary>
        //--------------------------------------------------------------------------------------
        private static Window? AguardarJanelaUpdate(UIA3Automation automation, int timeoutMs){
            var sw = Stopwatch.StartNew();
            bool logDiagnosticoFeito = false; // Loga janelas Promob apenas uma vez no primeiro ciclo

            while (sw.ElapsedMilliseconds < timeoutMs){

                // ETAPA 1: Scan normal via FlaUI (janelas visíveis no desktop)
                var janelas = automation.GetDesktop().FindAllChildren();
                foreach (var child in janelas){
                    if (child.ControlType != FlaUI.Core.Definitions.ControlType.Window) continue;
                    try {
                        var titulo = child.Name ?? child.Properties.Name.ValueOrDefault ?? "";
                        if (titulo.Contains("Promob Update", StringComparison.OrdinalIgnoreCase)){
                            return child.AsWindow();
                        }
                    }
                    catch { }
                }

                // ETAPA 2: Win32 EnumWindows — busca em TODAS as janelas (visíveis e ocultas)
                // Cobre: janelas minimizadas para tray, WS_EX_TOOLWINDOW, showCmd=SW_HIDE
                var hWnd = BuscarERestaurarJanelaOculta("Promob Update", logDiagnostico: !logDiagnosticoFeito);
                logDiagnosticoFeito = true;

                if (hWnd != IntPtr.Zero){
                    // Aguarda o Windows processar a restauração
                    InteractionHelper.EsperarUiRespirar(1200);

                    // Nova varredura FlaUI após restauração
                    var janelasAposRestore = automation.GetDesktop().FindAllChildren();
                    foreach (var child in janelasAposRestore){
                        if (child.ControlType != FlaUI.Core.Definitions.ControlType.Window) continue;
                        try {
                            var titulo = child.Name ?? child.Properties.Name.ValueOrDefault ?? "";
                            if (titulo.Contains("Promob Update", StringComparison.OrdinalIgnoreCase)){
                                Logger.Log("  [WIN32] Janela 'Promob Update' restaurada e localizada via FlaUI!");
                                return child.AsWindow();
                            }
                        }
                        catch { }
                    }

                    // Se o FlaUI ainda não vê, tenta acessar direto pelo HWND via UIA
                    Logger.Log("  [WIN32] FlaUI não localizou após restauração. Tentando acesso direto pelo HWND...");
                    try {
                        var elDireto = automation.FromHandle(hWnd);
                        if (elDireto != null) {
                            Logger.Log($"  [WIN32] Elemento obtido diretamente pelo HWND: '{elDireto.Name}'");
                            return elDireto.AsWindow();
                        }
                    }
                    catch (Exception exDirect) {
                        Logger.Log($"  [WIN32] Falha ao acessar HWND diretamente: {exDirect.Message}", LogLevel.Debug);
                    }
                }

                Thread.Sleep(500);
            }
            return null;
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Usa EnumWindows do Win32 para varrer TODAS as janelas do sistema (incluindo
            /// visíveis e ocultas) buscando aquelas cujo título contém <paramref name="tituloContem"/>.
            /// Loga TODAS as janelas relacionadas ao Promob encontradas para diagnóstico.
            /// Restaura via ShowWindow qualquer janela encontrada que esteja minimizada/oculta.
            /// </summary>
            /// <returns>O HWND da janela encontrada e restaurada, ou IntPtr.Zero se não encontrada.</returns>
        //--------------------------------------------------------------------------------------
        private static IntPtr BuscarERestaurarJanelaOculta(string tituloContem, bool logDiagnostico = false){
            IntPtr resultado = IntPtr.Zero;
            var sb = new StringBuilder(512);

            EnumWindows((hWnd, _) => {
                // Lê o título da janela (todas, sem filtro de visibilidade)
                sb.Clear();
                GetWindowText(hWnd, sb, sb.Capacity);
                string titulo = sb.ToString();

                // Log diagnóstico: lista qualquer janela com "Promob" no título
                if (logDiagnostico && titulo.Contains("Promob", StringComparison.OrdinalIgnoreCase)) {
                    bool visivel = IsWindowVisible(hWnd);
                    var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)) };
                    GetWindowPlacement(hWnd, ref wp);
                    int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                    bool isToolWin = (exStyle & WS_EX_TOOLWINDOW) != 0;
                    Logger.Log($"  [DIAG-WIN32] HWND={hWnd} | Título='{titulo}' | Visível={visivel} | showCmd={wp.showCmd} | ToolWindow={isToolWin}");
                }

                if (!titulo.Contains(tituloContem, StringComparison.OrdinalIgnoreCase))
                    return true; // Continua enumerando

                // Janela encontrada — verifica se precisa ser restaurada
                var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)) };
                GetWindowPlacement(hWnd, ref placement);
                bool janelaVisivel = IsWindowVisible(hWnd);
                int exStyleJanela  = GetWindowLong(hWnd, GWL_EXSTYLE);

                Logger.Log($"  [WIN32] Janela '{tituloContem}' encontrada! HWND={hWnd}, Título='{titulo}'");
                Logger.Log($"  [WIN32] Estado: Visível={janelaVisivel}, showCmd={placement.showCmd}, ToolWindow={(exStyleJanela & WS_EX_TOOLWINDOW) != 0}");

                // Restaura em todos os casos onde não está em estado normal visível
                bool precisaRestaurar = !janelaVisivel
                    || placement.showCmd == SW_HIDE
                    || placement.showCmd == SW_SHOWMINIMIZED
                    || (exStyleJanela & WS_EX_TOOLWINDOW) != 0;

                if (precisaRestaurar) {
                    Logger.Log($"  [WIN32] Restaurando janela do segundo plano/tray...");
                    ShowWindow(hWnd, SW_RESTORE);
                    InteractionHelper.EsperarUiRespirar(200);
                    ShowWindow(hWnd, SW_SHOWNORMAL);
                    InteractionHelper.EsperarUiRespirar(200);
                    ShowWindow(hWnd, SW_SHOW);
                    SetForegroundWindow(hWnd);
                    Logger.Log($"  [WIN32] ShowWindow executado. Janela restaurada para primeiro plano.");
                }
                else {
                    Logger.Log($"  [WIN32] Janela já está visível/normal. Apenas trazendo para frente...");
                    SetForegroundWindow(hWnd);
                }

                resultado = hWnd;
                return false; // Para a enumeração
            }, IntPtr.Zero);

            return resultado;
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Localiza o botão "Atualizar o Promob" (OpenProcadUpdate) na janela principal
            /// ou nos popups do desktop pertencentes ao processo do Promob.
            /// </summary>
        //--------------------------------------------------------------------------------------
        private static AutomationElement? LocalizarBotaoAtualizarPromob(UIA3Automation automation, AutomationElement buscaEm){
            AutomationElement? btnAtualizarPromob = null;
            var swBusca = Stopwatch.StartNew();
            const int timeoutBuscaMs = 5000;
            
            while (swBusca.ElapsedMilliseconds < timeoutBuscaMs){
                // Invalida cache de host para busca limpa
                WindowFinder.CachedHost = null;

                // 1. Tenta buscar a partir do Host elementHost1 (buscaEm)
                btnAtualizarPromob = WindowFinder.BuscarElementoComFallback(
                    buscaEm,
                    cf => cf.ByAutomationId("OpenProcadUpdate"),
                    e => (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("OpenProcadUpdate", StringComparison.OrdinalIgnoreCase) ||
                         (e.Properties.Name.ValueOrDefault ?? "").Equals("Atualizar o Promob", StringComparison.OrdinalIgnoreCase),
                    limitarAoMesmoProcesso: true,
                    processId: PromobWindowHelper.CachedProcessIdPromob
                );

                if (btnAtualizarPromob != null) return btnAtualizarPromob;

                // 2. Tenta buscar nas janelas/popups do Desktop pertencentes ao mesmo processo
                var desktopChildren = automation.GetDesktop().FindAllChildren();
                foreach (var child in desktopChildren){
                    try{
                        if (child.Properties.ProcessId.ValueOrDefault == PromobWindowHelper.CachedProcessIdPromob){
                            btnAtualizarPromob = child.FindFirstDescendant(cf => cf.ByAutomationId("OpenProcadUpdate"))
                                              ?? child.FindFirstDescendant(cf => cf.ByName("Atualizar o Promob"));
                            if (btnAtualizarPromob != null){
                                Logger.Log($"  [OK] Botão encontrado em uma sub-janela/popup do Desktop: '{btnAtualizarPromob.Name}'");
                                return btnAtualizarPromob;
                            }
                        }
                    }
                    catch { }
                }

                Thread.Sleep(500);
            }

            return null;
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Fecha a janela "Promob Update" clicando no botão "Fechar" do rodapé
            /// e, se necessário, confirma o alerta clicando em "Sim".
            /// </summary>
        //--------------------------------------------------------------------------------------
        private static void FecharJanela(Window janelaUpdate){
            Logger.Log("  [FECHAR] Clicando no botão 'Fechar' para encerrar a janela do assistente...");

            var todosElementos = janelaUpdate.FindAllDescendants();

            // Localiza o botão "Fechar" do rodapé (btnClose ou pelo nome, excluindo o Close do topo)
            var btnFechar = todosElementos.FirstOrDefault(e =>
                e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnClose", StringComparison.OrdinalIgnoreCase) ||
                 ((e.Name ?? "").Equals("Fechar", StringComparison.OrdinalIgnoreCase) && 
                  !(e.Properties.AutomationId.ValueOrDefault ?? "").Equals("Close", StringComparison.OrdinalIgnoreCase)))
            );

            if (btnFechar == null){
                // Fallback: busca qualquer elemento com nome "Fechar"
                btnFechar = todosElementos.FirstOrDefault(e => 
                    (e.Name ?? "").Equals("Fechar", StringComparison.OrdinalIgnoreCase));
            }

            if (btnFechar == null){
                throw new Exception("Botão 'Fechar' não encontrado na janela 'Promob Update'.");
            }

            ClicarBotaoComEstrategias(janelaUpdate, btnFechar, "Fechar");

            // --- Confirmar fechamento clicando em "Sim" no alerta (se aparecer) ---
            Logger.Log("  [FECHAR] Aguardando o surgimento do alerta de confirmação 'Sim' (até 15 segundos)...");
            
            AutomationElement? btnSim = null;
            var swSim = Stopwatch.StartNew();
            const int timeoutSimMs = 15000;
            
            while (swSim.ElapsedMilliseconds < timeoutSimMs) {
                var todosElementosAlerta = janelaUpdate.FindAllDescendants();
                
                btnSim = todosElementosAlerta.FirstOrDefault(e =>
                    (e.ControlType == FlaUI.Core.Definitions.ControlType.Button) &&
                    ((e.Name ?? "").Equals("Sim", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnYes", StringComparison.OrdinalIgnoreCase)) &&
                    e.IsEnabled
                );

                if (btnSim != null) {
                    Logger.Log("  [FECHAR] Botão 'Sim' de confirmação localizado e habilitado!");
                    break;
                }

                Thread.Sleep(500);
            }

            if (btnSim != null) {
                Logger.Log($"  [FECHAR] Botão 'Sim' definido: Tipo={btnSim.ControlType}, Nome='{btnSim.Name}'");
                ClicarBotaoComEstrategias(janelaUpdate, btnSim, "Sim");
                Logger.Log("  [FECHAR] Clique em 'Sim' efetuado! Assistente encerrado com sucesso.");
            }
            else {
                Logger.Log("  [FECHAR] Alerta de confirmação 'Sim' não apareceu (janela pode ter fechado diretamente).");
            }
            
            Logger.Log("  [FECHAR] Janela de atualizações fechada com sucesso!");
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Busca no desktop o popup de sucesso "PromobUpdate" (sem espaço) que contém
            /// um botão "Fechar". Retorna a janela e o botão, ou (null, null).
            /// </summary>
        //--------------------------------------------------------------------------------------
        private static (Window? janela, AutomationElement? btnFechar) BuscarPopupSucessoNoDesktop(UIA3Automation automation){
            // ETAPA 1: Tenta restaurar a janela caso esteja oculta (ícone em segundo plano)
            // O popup "PromobUpdate" pode estar invisível para o FlaUI
            var hOculta = BuscarERestaurarJanelaOculta("PromobUpdate");
            if (hOculta != IntPtr.Zero){
                Logger.Log("  [WIN32] Popup 'PromobUpdate' oculto restaurado. Aguardando tornar-se acessível...");
                InteractionHelper.EsperarUiRespirar(800);
            }

            // ETAPA 2: Scan via FlaUI (janelas visíveis, incluindo a recém-restaurada)
            try {
                var janelasDesktop = automation.GetDesktop().FindAllChildren();
                foreach (var child in janelasDesktop) {
                    if (child.ControlType != FlaUI.Core.Definitions.ControlType.Window) continue;
                    string nome = child.Name ?? "";
                    if (nome.Equals("PromobUpdate", StringComparison.OrdinalIgnoreCase)) {
                        var descSucesso = child.FindAllDescendants();
                        var btnCheck = descSucesso.FirstOrDefault(e =>
                            (e.ControlType == FlaUI.Core.Definitions.ControlType.Button) &&
                            ((e.Name ?? "").Equals("Fechar", StringComparison.OrdinalIgnoreCase) ||
                             (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnClose", StringComparison.OrdinalIgnoreCase))
                        );
                        if (btnCheck != null) {
                            return (child.AsWindow(), btnCheck);
                        }
                    }
                }
            }
            catch (Exception exCheck) {
                Logger.Log($"  [Aviso] Falha ao verificar popup de sucesso: {exCheck.Message}", LogLevel.Debug);
            }

            return (null, null);
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Aguarda o popup de sucesso "PromobUpdate" aparecer no desktop (até 5 minutos)
            /// e aciona o fechamento via <see cref="FinalizarEFecharPopupSucesso"/>.
            /// </summary>
        //--------------------------------------------------------------------------------------
        private static void AguardarEFecharPopupSucesso(UIA3Automation automation){
            Logger.Log("  [SUCESSO] Aguardando a conclusão da instalação e o surgimento do popup de sucesso (até 5 minutos)...");
            
            AutomationElement? btnFecharSucesso = null;
            Window? janelaSucesso = null;
            var swSucesso = Stopwatch.StartNew();
            const int timeoutSucessoMs = 300000; // 5 minutos
            
            while (swSucesso.ElapsedMilliseconds < timeoutSucessoMs) {
                var (janela, btn) = BuscarPopupSucessoNoDesktop(automation);
                if (janela != null && btn != null) {
                    janelaSucesso = janela;
                    btnFecharSucesso = btn;
                    break;
                }

                if (((int)swSucesso.Elapsed.TotalSeconds) % 10 == 0) {
                    Logger.Log($"  [SUCESSO] Instalando atualizações... (Tempo decorrido: {swSucesso.Elapsed.Minutes}m {swSucesso.Elapsed.Seconds}s)");
                }

                Thread.Sleep(2000);
            }

            if (btnFecharSucesso == null || janelaSucesso == null) {
                throw new Exception("[SUCESSO] Timeout: O popup de conclusão da instalação não apareceu após 5 minutos.");
            }

            FinalizarEFecharPopupSucesso(janelaSucesso, btnFecharSucesso);
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Executa a sequência de cliques e atalhos de teclado para focar e
            /// acionar o botão "Fechar" da janela de sucesso "PromobUpdate".
            /// </summary>
        //--------------------------------------------------------------------------------------
        private static void FinalizarEFecharPopupSucesso(Window janelaSucesso, AutomationElement btnFecharSucesso) {
            Logger.Log($"  [SUCESSO] Botão 'Fechar' de sucesso definido: Tipo={btnFecharSucesso.ControlType}, Nome='{btnFecharSucesso.Name}', Id='{btnFecharSucesso.Properties.AutomationId.ValueOrDefault}'");
            ClicarBotaoComEstrategias(janelaSucesso, btnFecharSucesso, "Fechar Sucesso");
            Logger.Log("  [SUCESSO] Clique no botão 'Fechar' de sucesso concluído! A atualização foi finalizada com êxito!");
            Logger.Log("══════════════════════════════════════════");
        }

    }
}
