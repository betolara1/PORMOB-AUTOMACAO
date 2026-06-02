using System;
using System.Linq;
using System.Runtime.InteropServices;
using PromobAutomacao.Automation;
using PromobAutomacao.Utils;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.UIA3;

namespace PromobAutomacao.Promob{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Classe auxiliar (Helper) responsável pela identificação, busca e monitoramento 
        /// de janelas do Promob e seus diálogos nativos (popups, wizards e caixas de seleção de arquivos).
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class PromobWindowHelper{

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// ID do processo do Promob em execução, cacheado para otimizar pesquisas e evitar chamadas repetitivas ao sistema operacional.
            /// </summary>
            /// O '?' indica que o valor pode ser nulo (não definido ainda)
        //--------------------------------------------------------------------------------------
        public static int? CachedProcessIdPromob; // Memória temporária para guardar o ID do processo do Promob.

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Localiza e aguarda até que a janela principal do Promob esteja visível e pronta na tela do Windows.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
            /// <param name="timeoutMs">Tempo máximo de espera em milissegundos antes de desistir da busca.</param>
            /// <returns>A janela do Promob ativa como um objeto <see cref="Window"/>, ou <c>null</c> se a janela não for encontrada no prazo.</returns>
        //--------------------------------------------------------------------------------------
        public static Window? AguardarJanelaPromob(UIA3Automation automation, int timeoutMs = PromobConfig.TimeoutPadrao){
            
            Window? encontrada = null;

            // Evita identificar a nossa própria aplicação de automação como se fosse o Promob
            var currentProcId = System.Diagnostics.Process.GetCurrentProcess().Id;
            var promobProc = System.Diagnostics.Process.GetProcesses()
                .FirstOrDefault(p => p.Id != currentProcId &&
                                     p.ProcessName.Contains("Promob", StringComparison.OrdinalIgnoreCase) &&
                                     !p.ProcessName.Contains("Uploader", StringComparison.OrdinalIgnoreCase) &&
                                     !p.ProcessName.Contains("Automacao", StringComparison.OrdinalIgnoreCase));

            if (promobProc != null) CachedProcessIdPromob = promobProc.Id;

            // Executa buscas periódicas até que a janela do Promob seja instanciada ou ocorra timeout
            InteractionHelper.EsperarAte(() =>{
                var desktop = automation.GetDesktop();
                var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

                foreach (var j in janelas){
                    if (promobProc != null){
                        // Busca precisa: valida se a janela pertence exatamente ao Process ID do Promob que identificamos
                        if (j.Properties.ProcessId.ValueOrDefault == promobProc.Id){
                            var name = j.Name ?? "";
                            if (EhJanelaPromob(name)){
                                encontrada = j.AsWindow();
                                return true;
                            }
                        }
                    }
                    else{
                        // Fallback: se não achamos o processo de antemão, busca puramente pelo título da janela
                        var fallbackName = j.Name ?? "";
                        if (EhJanelaPromob(fallbackName)){
                            encontrada = j.AsWindow();
                            try {
                                CachedProcessIdPromob = encontrada.Properties.ProcessId.ValueOrDefault; 
                            }
                            catch { }
                            return true;
                        }
                    }
                }

                return false;
            }, timeoutMs);
            
            return encontrada;
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Valida se um determinado título de janela corresponde às janelas padrão do Promob Studio.
            /// </summary>
            /// <param name="nome">O título/nome da janela a ser validada.</param>
            /// <returns><c>true</c> se a janela corresponder ao Promob e não a ferramentas de desenvolvimento; caso contrário, <c>false</c>.</returns>
        //--------------------------------------------------------------------------------------
        public static bool EhJanelaPromob(string? nome){
            
            if (string.IsNullOrWhiteSpace(nome)) return false;

            // Filtro de segurança para evitar falsos positivos se o desenvolvedor estiver com o VS Code aberto trabalhando neste projeto
            if (nome.Contains("Visual Studio Code", StringComparison.OrdinalIgnoreCase) ||
                nome.Contains("VS Code", StringComparison.OrdinalIgnoreCase))
                return false;

            // Explicitamente ignorar a splash screen do Promob (que tem o título exato "Promob" ou apenas "Promob" com espaços)
            if (nome.Trim().Equals("Promob", StringComparison.OrdinalIgnoreCase))
                return false;

            return nome.Contains("- Promob Studio", StringComparison.OrdinalIgnoreCase) ||
                   nome.Contains("Promob Studio Bartz", StringComparison.OrdinalIgnoreCase) ||
                   nome.Contains("Promob Studio", StringComparison.OrdinalIgnoreCase) ||
                   nome.Contains("Studio Bartz", StringComparison.OrdinalIgnoreCase);
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Localiza a janela de Wizard (Assistente) de Importação do Promob.
            /// Procura tanto no nível do Desktop (caso seja uma janela flutuante) quanto como filha direta da janela principal.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
            /// <param name="janelaPrincipal">A janela principal ativa do Promob.</param>
            /// <returns>A janela do assistente como <see cref="Window"/>, ou <c>null</c> se ela não for encontrada.</returns>
        //--------------------------------------------------------------------------------------
        public static Window? EncontrarJanelaWizard(UIA3Automation automation, Window janelaPrincipal){
            
            var desktop = automation.GetDesktop();

            // Tentativa 1: Procurar como janela de topo no Desktop pertencente ao processo do Promob.
            // Isso é instantâneo e super preciso, cobrendo 99% dos casos já que modais WPF/WinForms são janelas de topo nativas.
            try{
                var janelasDesktop = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
                var wizard = janelasDesktop.FirstOrDefault(j =>
                    (j.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.NomeJanelaWizardImportacao, StringComparison.OrdinalIgnoreCase) &&
                    (!CachedProcessIdPromob.HasValue || j.Properties.ProcessId.ValueOrDefault == CachedProcessIdPromob.Value));

                if (wizard != null){
                    AppLogs.LogWizardEncontradoDesktop(wizard.Name);
                    return wizard.AsWindow();
                }
            }
            catch (Exception ex){
                AppLogs.LogWizardSearchDesktopError(ex.Message);
            }

            // Tentativa 2: Busca rasa (até nível 2) apenas dentro da janela principal se não foi achado no desktop.
            // Evita a pesadíssima busca profunda (FindFirstDescendant) na árvore do Promob.
            try{
                var filhosSuperficiais = WindowFinder.BuscarAteNivel(janelaPrincipal, maxNivel: 2);
                var wizardDesc = filhosSuperficiais.FirstOrDefault(e =>
                    e.ControlType == FlaUI.Core.Definitions.ControlType.Window &&
                    (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.NomeJanelaWizardImportacao, StringComparison.OrdinalIgnoreCase));

                if (wizardDesc != null){
                    AppLogs.LogWizardEncontradoJanelaPrincipal(wizardDesc.Name);
                    return wizardDesc.AsWindow();
                }
            }
            catch (Exception ex){
                AppLogs.LogWizardSearchMainWindowError(ex.Message);
            }

            return null;
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Detecta se existe algum diálogo de seleção de arquivo aberto (ex: caixa de diálogo de "Abrir" ou "Salvar Como").
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
            /// <param name="targetProcessId">ID opcional do processo do Promob para restringir a busca de caixas de diálogo pertencentes ao programa.</param>
            /// <returns>A janela de seleção de arquivo ativa como <see cref="Window"/>, ou <c>null</c> se nenhuma estiver ativa.</returns>
        //--------------------------------------------------------------------------------------
        public static Window? JanelaArquivoAberta(UIA3Automation automation, int? targetProcessId = null){
            
            var desktop = automation.GetDesktop();
            var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

            var consulta = janelas.AsEnumerable();
            if (targetProcessId.HasValue){
                consulta = consulta.Where(j =>{
                    try { return j.Properties.ProcessId.ValueOrDefault == targetProcessId.Value; }
                    catch { return false; }
                });
            }

            // Busca rápida (Rasa): verifica o título das janelas de topo
            var dialogo = consulta.FirstOrDefault(j =>
                InteractionHelper.ContemQualquer(j.Name, PromobConfig.TermosDialogoArquivo));

            if (dialogo != null)
                return dialogo.AsWindow();

            // Busca lenta (Profunda): varre o Desktop à procura de botões típicos de diálogos de arquivos (Abrir/Salvar)
            var profundo = desktop.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)
                  .And(cf.ByName(PromobConfig.BtnAbrir).Or(cf.ByName(PromobConfig.BtnOpen)).Or(cf.ByName(PromobConfig.BtnSalvarComo)).Or(cf.ByName(PromobConfig.BtnSaveAs))));

            return profundo?.AsWindow();
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Procura por popups ou janelas de atenção/aviso (ex: modais informando que a operação foi concluída ou que houve erro).
            /// </summary>
            /// <param name="desktop">O elemento raiz Desktop ativo.</param>
            /// <param name="targetProcessId">ID opcional do processo do Promob para isolar diálogos específicos deste software.</param>
            /// <returns>A janela de alerta ativa como <see cref="Window"/>, ou <c>null</c> se nenhum popup estiver visível.</returns>
        //--------------------------------------------------------------------------------------
        public static Window? EncontrarPopupAtencao(AutomationElement desktop, int? targetProcessId = null){
            
            var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

            var consulta = janelas.AsEnumerable();
            if (targetProcessId.HasValue){
                consulta = consulta.Where(j =>{
                    try { return j.Properties.ProcessId.ValueOrDefault == targetProcessId.Value; }
                    catch { return false; }
                });
            }

            // Procura janelas com títulos típicos de aviso (ex: "Atenção", "Aviso", "Mensagem")
            var popup = consulta.FirstOrDefault(j => InteractionHelper.ContemQualquer(j.Name, PromobConfig.TitulosAviso));

            if (popup == null){
                // Fallback: se não achar pelo título de aviso exato, procura janelas genéricas com a palavra "Promob" no título
                popup = consulta.FirstOrDefault(j => (j.Name ?? "").Contains("Promob", StringComparison.OrdinalIgnoreCase));
            }

            if (popup != null)
                return popup.AsWindow();

            return null;
        }

        // ==================================================================================
        // WIN32 TRAY INTERACTIVE APIS
        // ==================================================================================

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SendMessage(IntPtr hWnd, uint Msg, int wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesRead);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, int nSize, out int lpNumberOfBytesRead);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left; public int top; public int right; public int bottom; }

        private const uint TB_BUTTONCOUNT = 0x0418;
        private const uint TB_GETBUTTON = 0x0417;
        private const uint TB_GETITEMRECT = 0x041D;
        
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 0x04;

        private static IntPtr FindToolbarWindow32(IntPtr parent) {
            IntPtr hwnd = FindWindowEx(parent, IntPtr.Zero, "ToolbarWindow32", null);
            if (hwnd != IntPtr.Zero) return hwnd;
            IntPtr child = IntPtr.Zero;
            while ((child = FindWindowEx(parent, child, null, null)) != IntPtr.Zero) {
                IntPtr result = FindToolbarWindow32(child);
                if (result != IntPtr.Zero) return result;
            }
            return IntPtr.Zero;
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Busca o ícone de atualização do Promob/Procad na bandeja do sistema (system tray)
        /// ou no menu de ícones ocultos (overflow) e executa um duplo clique para trazer a janela ao primeiro plano.
        /// </summary>
        /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        /// <returns><c>true</c> se o ícone foi encontrado e acionado; caso contrário, <c>false</c>.</returns>
        //--------------------------------------------------------------------------------------
        private static bool ScanAndClickUiaIcon(AutomationElement container) {
            var items = container.FindAllDescendants();
            foreach (var item in items) {
                var name = item.Name ?? "";
                var autoId = item.Properties.AutomationId.ValueOrDefault ?? "";
                var helpText = item.Properties.HelpText.ValueOrDefault ?? "";
                
                bool isUpdateOrNews = name.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                                      name.Contains("Uploader", StringComparison.OrdinalIgnoreCase) ||
                                      name.Contains("Atualiz", StringComparison.OrdinalIgnoreCase) ||
                                      name.Contains("Notic", StringComparison.OrdinalIgnoreCase) ||
                                      name.Contains("Notíc", StringComparison.OrdinalIgnoreCase) ||
                                      name.Contains("Procad", StringComparison.OrdinalIgnoreCase) ||
                                      autoId.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                                      autoId.Contains("Uploader", StringComparison.OrdinalIgnoreCase) ||
                                      autoId.Contains("Atualiz", StringComparison.OrdinalIgnoreCase) ||
                                      autoId.Contains("Notic", StringComparison.OrdinalIgnoreCase) ||
                                      autoId.Contains("Notíc", StringComparison.OrdinalIgnoreCase) ||
                                      autoId.Contains("Procad", StringComparison.OrdinalIgnoreCase) ||
                                      helpText.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                                      helpText.Contains("Uploader", StringComparison.OrdinalIgnoreCase) ||
                                      helpText.Contains("Atualiz", StringComparison.OrdinalIgnoreCase) ||
                                      helpText.Contains("Notic", StringComparison.OrdinalIgnoreCase) ||
                                      helpText.Contains("Notíc", StringComparison.OrdinalIgnoreCase) ||
                                      helpText.Contains("Procad", StringComparison.OrdinalIgnoreCase);

                bool isMainStudio = name.Contains("Studio", StringComparison.OrdinalIgnoreCase) ||
                                    name.Contains("Bartz", StringComparison.OrdinalIgnoreCase) ||
                                    autoId.Contains("Studio", StringComparison.OrdinalIgnoreCase) ||
                                    autoId.Contains("Bartz", StringComparison.OrdinalIgnoreCase) ||
                                    helpText.Contains("Studio", StringComparison.OrdinalIgnoreCase) ||
                                    helpText.Contains("Bartz", StringComparison.OrdinalIgnoreCase);

                bool matches = isUpdateOrNews && !isMainStudio;
                               
                if (matches) {
                    AppLogs.LogTrayIconFoundUia(name, autoId, helpText);
                    var rect = item.BoundingRectangle;
                    if (!rect.IsEmpty) {
                        int x = (int)(rect.X + (rect.Width / 2));
                        int y = (int)(rect.Y + (rect.Height / 2));
                        Mouse.MoveTo(x, y);
                        InteractionHelper.EsperarUiRespirar(250);
                        Mouse.DoubleClick();
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool RestaurarJanelaUpdateDoTray(UIA3Automation automation) {
            try {
                var desktop = automation.GetDesktop();
                
                // 1. Tenta buscar no tray principal usando UIA primeiro
                var taskbar = desktop.FindFirstChild(cf => cf.ByClassName("Shell_TrayWnd"));
                if (taskbar != null) {
                    var trayNotify = taskbar.FindFirstDescendant(cf => cf.ByClassName("TrayNotifyWnd"));
                    if (trayNotify != null) {
                        AppLogs.LogTrayScanPrincipalUia();
                        if (ScanAndClickUiaIcon(trayNotify)) {
                            return true;
                        }
                    }
                }
                
                // 2. Tenta buscar no tray principal usando Win32 Toolbar como fallback
                IntPtr hMainToolbar = IntPtr.Zero;
                IntPtr hShellTray = FindWindow("Shell_TrayWnd", null);
                if (hShellTray != IntPtr.Zero) {
                    IntPtr hTrayNotify = FindWindowEx(hShellTray, IntPtr.Zero, "TrayNotifyWnd", null);
                    if (hTrayNotify != IntPtr.Zero) {
                        hMainToolbar = FindToolbarWindow32(hTrayNotify);
                    }
                }
                if (hMainToolbar != IntPtr.Zero) {
                    AppLogs.LogTrayScanPrincipalWin32();
                    if (ScanAndClickTrayIcon(hMainToolbar, desktop.AsWindow())) {
                        return true;
                    }
                }
                
                // 3. Se não encontrou no tray principal, clica no chevron para abrir a overflow
                if (taskbar != null) {
                    var trayNotify = taskbar.FindFirstDescendant(cf => cf.ByClassName("TrayNotifyWnd"));
                    var chevron = trayNotify?.FindFirstChild(cf => cf.ByClassName("Button")) ??
                                  taskbar.FindFirstDescendant(cf => cf.ByClassName("Button")) ??
                                  trayNotify?.FindAllChildren().FirstOrDefault(c => 
                                      (c.Name ?? "").Contains("chevron", StringComparison.OrdinalIgnoreCase) ||
                                      (c.Name ?? "").Contains("oculto", StringComparison.OrdinalIgnoreCase) ||
                                      (c.Name ?? "").Contains("hidden", StringComparison.OrdinalIgnoreCase) ||
                                      (c.Properties.AutomationId.ValueOrDefault ?? "").Contains("chevron", StringComparison.OrdinalIgnoreCase)
                                  );
                                  
                    if (chevron != null) {
                        AppLogs.LogTrayChevronEncontrado();
                        var rectChevron = chevron.BoundingRectangle;
                        int cx = 0, cy = 0;
                        if (!rectChevron.IsEmpty) {
                            cx = (int)(rectChevron.X + (rectChevron.Width / 2));
                            cy = (int)(rectChevron.Y + (rectChevron.Height / 2));
                            Mouse.MoveTo(cx, cy);
                            InteractionHelper.EsperarUiRespirar(200);
                            Mouse.Click();
                            InteractionHelper.EsperarUiRespirar(1000); // Aguarda a janela de overflow aparecer
                        }
                        
                        // Busca a janela de overflow (XAML ou legacy)
                        IntPtr hOverflow = FindWindow("NotifyIconOverflowWindow", null);
                        if (hOverflow == IntPtr.Zero) {
                            hOverflow = FindWindow("TopLevelWindowForOverflowXamlIsland", null);
                        }
                        
                        // Fallback UIA para achar a janela
                        Window? overflowWin = null;
                        if (hOverflow != IntPtr.Zero) {
                            try {
                                overflowWin = automation.FromHandle(hOverflow).AsWindow();
                            } catch {}
                        }
                        
                        if (overflowWin == null) {
                            var desktopChildren = desktop.FindAllChildren();
                            foreach (var child in desktopChildren) {
                                if (child.ControlType == FlaUI.Core.Definitions.ControlType.Window) {
                                    var className = child.Properties.ClassName.ValueOrDefault ?? "";
                                    var name = child.Name ?? "";
                                    if (className.Contains("Overflow", StringComparison.OrdinalIgnoreCase) || 
                                        name.Contains("Overflow", StringComparison.OrdinalIgnoreCase) ||
                                        name.Contains("oculto", StringComparison.OrdinalIgnoreCase)) {
                                        overflowWin = child.AsWindow();
                                        hOverflow = overflowWin.Properties.NativeWindowHandle.ValueOrDefault;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        if (overflowWin != null) {
                            AppLogs.LogTrayOverflowLocalizado(overflowWin.Name, overflowWin.Properties.ClassName.ValueOrDefault, hOverflow);
                            if (ScanAndClickUiaIcon(overflowWin)) {
                                // Fecha a overflow clicando novamente no chevron
                                if (cx > 0 && cy > 0) {
                                    InteractionHelper.EsperarUiRespirar(500);
                                    Mouse.MoveTo(cx, cy);
                                    InteractionHelper.EsperarUiRespirar(250);
                                    Mouse.Click();
                                }
                                return true;
                            }
                        }
                        
                        if (hOverflow != IntPtr.Zero) {
                            IntPtr hOverflowToolbar = FindToolbarWindow32(hOverflow);
                            if (hOverflowToolbar != IntPtr.Zero) {
                                AppLogs.LogTrayScanOverflowWin32();
                                if (ScanAndClickTrayIcon(hOverflowToolbar, desktop.AsWindow())) {
                                    // Fecha a overflow clicando novamente no chevron
                                    if (cx > 0 && cy > 0) {
                                        InteractionHelper.EsperarUiRespirar(500);
                                        Mouse.MoveTo(cx, cy);
                                        InteractionHelper.EsperarUiRespirar(250);
                                        Mouse.Click();
                                    }
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                AppLogs.LogTrayErroBuscaIcone(ex.Message);
            }
            return false;
        }

        private static bool ScanAndClickTrayIcon(IntPtr hToolbar, Window desktopWindow) {
            int count = SendMessage(hToolbar, TB_BUTTONCOUNT, 0, 0);
            AppLogs.LogTrayScanAndClickIconCount(count, hToolbar);
            if (count <= 0) return false;

            uint explorerPid = 0;
            GetWindowThreadProcessId(hToolbar, out explorerPid);
            if (explorerPid == 0) return false;

            IntPtr hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, explorerPid);
            if (hProcess == IntPtr.Zero) {
                AppLogs.LogTrayFailedOpenProcess(explorerPid);
                return false;
            }

            try {
                // Aloca memória no processo do Explorer
                IntPtr ipMem = VirtualAllocEx(hProcess, IntPtr.Zero, 32, MEM_COMMIT, PAGE_READWRITE);
                if (ipMem == IntPtr.Zero) {
                    AppLogs.LogTrayFailedVirtualAlloc();
                    return false;
                }

                try {
                    for (int i = 0; i < count; i++) {
                        int res = SendMessage(hToolbar, TB_GETBUTTON, i, ipMem);
                        if (res == 0) {
                            AppLogs.LogTrayTbGetButtonReturnedZero(i);
                            continue;
                        }

                        byte[] btnBytes = new byte[32];
                        int bytesRead = 0;
                        bool success = ReadProcessMemory(hProcess, ipMem, btnBytes, 32, out bytesRead);
                        if (!success) {
                            AppLogs.LogTrayReadProcessMemoryFailed(i);
                            continue;
                        }

                        long dwDataLong = BitConverter.ToInt64(btnBytes, 16);
                        if (dwDataLong == 0) {
                            AppLogs.LogTrayDwDataZero(i);
                            continue;
                        }

                        IntPtr dwData = (IntPtr)dwDataLong;
                        byte[] hWndBytes = new byte[8];
                        bool success2 = ReadProcessMemory(hProcess, dwData, hWndBytes, 8, out bytesRead);
                        if (!success2) {
                            AppLogs.LogTrayReadProcessMemoryDwDataFailed(i);
                            continue;
                        }

                        IntPtr ownerHWnd = (IntPtr)BitConverter.ToInt64(hWndBytes, 0);
                        if (ownerHWnd == IntPtr.Zero) {
                            AppLogs.LogTrayOwnerHwndZero(i);
                            continue;
                        }

                        uint ownerPid = 0;
                        GetWindowThreadProcessId(ownerHWnd, out ownerPid);
                        if (ownerPid == 0) {
                            AppLogs.LogTrayOwnerPidZero(ownerHWnd, i);
                            continue;
                        }

                        string processName = "";
                        try {
                            using (var proc = System.Diagnostics.Process.GetProcessById((int)ownerPid)) {
                                processName = proc.ProcessName;
                            }
                        } catch (Exception exProc) {
                            processName = $"[Unknown: {exProc.Message}]";
                        }

                        AppLogs.LogTrayIconDetail(i, ownerHWnd, processName, ownerPid);

                        bool isUpdateProc = processName.Contains("Procad", StringComparison.OrdinalIgnoreCase) ||
                                            processName.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                                            processName.Contains("Uploader", StringComparison.OrdinalIgnoreCase);

                        bool isMainProc = processName.Equals("Promob5", StringComparison.OrdinalIgnoreCase) ||
                                          processName.Contains("Automacao", StringComparison.OrdinalIgnoreCase);

                        if (isUpdateProc && !isMainProc) {

                            AppLogs.LogTrayIconCorrespondingFound(processName, ownerPid);

                            // Aloca memória para a RECT
                            IntPtr ipMemRect = VirtualAllocEx(hProcess, IntPtr.Zero, 16, MEM_COMMIT, PAGE_READWRITE);
                            if (ipMemRect != IntPtr.Zero) {
                                try {
                                    int resRect = SendMessage(hToolbar, TB_GETITEMRECT, i, ipMemRect);
                                    if (resRect != 0) {
                                        byte[] rectBytes = new byte[16];
                                        bool successRect = ReadProcessMemory(hProcess, ipMemRect, rectBytes, 16, out bytesRead);
                                        if (successRect) {
                                            int left = BitConverter.ToInt32(rectBytes, 0);
                                            int top = BitConverter.ToInt32(rectBytes, 4);
                                            int right = BitConverter.ToInt32(rectBytes, 8);
                                            int bottom = BitConverter.ToInt32(rectBytes, 12);

                                            RECT tbRect = new RECT();
                                            GetWindowRect(hToolbar, out tbRect);

                                            int midX = tbRect.left + (left + right) / 2;
                                            int midY = tbRect.top + (top + bottom) / 2;

                                            InteractionHelper.AtivarJanela(desktopWindow);
                                            Mouse.MoveTo(midX, midY);
                                            InteractionHelper.EsperarUiRespirar(250);
                                            Mouse.DoubleClick();
                                            return true;
                                        }
                                    }
                                }
                                finally {
                                    VirtualFreeEx(hProcess, ipMemRect, 0, MEM_RELEASE);
                                }
                            }
                        }
                    }
                }
                finally {
                    VirtualFreeEx(hProcess, ipMem, 0, MEM_RELEASE);
                }
            }
            finally {
                CloseHandle(hProcess);
            }
            return false;
        }
    }
}
