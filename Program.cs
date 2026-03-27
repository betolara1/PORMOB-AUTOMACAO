using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.Core.Patterns;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace AutomacaoPromobTeste{
    internal class Program{
        // ────────────────────────────────────────────────────────────────────
        // Pastas
        static readonly string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        static readonly string PastaPromob = Path.Combine(DesktopPath, "promob");
        static readonly string PastaXml = Path.Combine(DesktopPath, "xml");
        static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "erros.log");

        // ────────────────────────────────────────────────────────────────────
        // Timeouts / intervalos
        const int TimeoutCurto = 2000;
        const int TimeoutPadrao = 5000;
        const int TimeoutLongo = 10000;
        const int PollMs = 200;
        const int DelayMinimo = 150;

        // ────────────────────────────────────────────────────────────────────
        // Seletores / textos
        const string NomeJanelaWizardImportacao = "Importar projeto";
        const string AutomationIdImportar = "ProjectImport";
        const string AutomationIdHost = "elementHost1";

        // ────────────────────────────────────────────────────────────────────
        // Config de log
        enum LogLevel { Error = 0, Warn = 1, Info = 2, Debug = 3 }
        static LogLevel NivelAtual = LogLevel.Info;

        // ────────────────────────────────────────────────────────────────────
        // Cache
        static AutomationElement? _cachedBotaoImportar;
        static AutomationElement? _cachedHost;
        static int? _cachedProcessIdPromob;

        // ────────────────────────────────────────────────────────────────────
        // Clipboard nativo via Win32 (evita spawnar PowerShell por arquivo)
        [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool CloseClipboard();
        [DllImport("user32.dll")] static extern bool EmptyClipboard();
        [DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        const uint CF_UNICODETEXT = 13;
        const uint GMEM_MOVEABLE = 0x0002;

        static void Main(string[] args){
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Banner();

            VisionHelper.Inicializar();

            if (!Directory.Exists(PastaPromob)){
                Log($"[ERRO] Pasta não encontrada: {PastaPromob}", LogLevel.Error);
                Console.ReadKey();
                return;
            }

            Directory.CreateDirectory(PastaXml);

            using var automation = new UIA3Automation();

            int processados = 0;
            int erros = 0;

            Log("[INFO] Modo contínuo ativado. Monitorando pasta para novos arquivos...");
            Log($"[INFO] Pasta: {PastaPromob}\n");

            // Loop eterno: sempre monitora a pasta
            while (true){
                // Lê os arquivos a cada iteração (a pasta muda ao longo do tempo)
                var arquivo = Directory.GetFiles(PastaPromob, "*.promob")
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (arquivo == null){
                    // Pasta vazia: aguarda e tenta novamente
                    Console.Write($"\r[AGUARDANDO] Nenhum arquivo na pasta. Processados: {processados} | Erros: {erros} — Verificando...");
                    Thread.Sleep(3000);
                    continue;
                }

                var nome = Path.GetFileName(arquivo);

                Console.WriteLine();
                Console.WriteLine("══════════════════════════════════════════");
                Console.WriteLine($"[NOVO] Processando: {nome}");
                Console.WriteLine($"       Processados até agora: {processados} | Erros: {erros}");
                Console.WriteLine("══════════════════════════════════════════");

                try{
                    Medir("Processar arquivo", () => ProcessarArquivo(automation, arquivo));
                    processados++;
                    Console.WriteLine($"\n[OK] {nome} processado com sucesso!");

                    // Exclui o arquivo processado da pasta
                    try{
                        File.Delete(arquivo);
                        Log($"  [OK] Arquivo '{nome}' excluído da pasta.");
                    }
                    catch (Exception exDel){
                        Log($"  [AVISO] Não foi possível excluir '{nome}': {exDel.Message}", LogLevel.Warn);
                    }
                }
                catch (Exception ex){
                    erros++;
                    Console.WriteLine($"\n[ERRO] Falha no processamento de {nome}: {ex.Message}");
                    RegistrarErro(nome, ex);
                    TentarRecuperar(automation);
                    
                    Log($"  [INFO] O arquivo '{nome}' permanecerá na pasta para reprocessamento.");
                }

                Console.WriteLine();
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Fluxo principal
        static void ProcessarArquivo(UIA3Automation automation, string caminhoArquivo){
            Log("  [1/8] Localizando janela do Promob...");
            var janela = AguardarJanelaPromob(automation, TimeoutLongo)
                ?? throw new Exception("Janela do Promob não encontrada. O Promob está aberto?");
            
            // OPTIMIZAÇÃO: Só invalida o cache se o ProcessId mudou (Promob reiniciou)
            int currentPid = janela.Properties.ProcessId.ValueOrDefault;
            if (_cachedProcessIdPromob.HasValue && _cachedProcessIdPromob.Value != currentPid){
                Log("  [INFO] Novo ProcessId detectado. Invalidando cache de UI.");
                InvalidarCacheUi();
            }
            _cachedProcessIdPromob = currentPid;

            AtivarJanela(janela);

            // // LISTAR OS BOTÕES
            // Console.WriteLine("[INFO] Procurando janela do Promob para listar botões...");
            // var janelaInicial = AguardarJanelaPromob(automation, 5000);
            // if (janelaInicial != null){
            //     ListarBotoesProject(janelaInicial);
            // }

            Log("  [2/8] Acionando Importar...");
            AtivarJanela(janela);
            Medir("Clicar botão Importar", () => ClicarBotaoImportar(janela));

            Log("  [3/8] Abrindo busca de arquivo e preenchendo caminho...");
            var janelaWizard = EncontrarJanelaWizard(automation, janela) ?? janela;
            Medir("Selecionar arquivo", () => AbrirDialogoEPreencher(automation, janelaWizard, caminhoArquivo));

            Log("  [4/8] Clicando em Avançar no Wizard...");
            AtivarJanela(janelaWizard);
            Medir("Avançar wizard", () => ClicarAvancarWizard(janelaWizard));

            Log("  [5/8] Tratando popup de Novo Projeto...");
            Medir("Tratar popup", () => CancelarPopupNovoProjeto(automation));

            Log("  [6/8] Abrindo o projeto selecionado...");
            var nomeProjeto = Path.GetFileNameWithoutExtension(caminhoArquivo);
            Medir("Abrir projeto", () => AbrirProjetoSelecionado(janela, nomeProjeto));

            Log("  [7/8] Navegando até Ferramentas > Orçamento > Listagem...");
            Medir("Abrir listagem", () => AbrirListagem(automation, janela));
            
            Log("  [8/8] Fechando o projeto atual...");
            Medir("Fechar projeto", () => FecharProjeto(automation, janela));

            Log("  [INFO] Fluxo concluído para este arquivo.");
        }

        // ────────────────────────────────────────────────────────────────────
        // Fechamento de projeto
        static void FecharProjeto(UIA3Automation automation, Window janela){
            AtivarJanela(janela);
            var raizBusca = ObterHostOuJanela(janela);

            Log("  [INFO] Procurando aba 'Arquivo' (FileTab)...");
            var abaArquivo = BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)
                        .And(cf.ByAutomationId("FileTab").Or(cf.ByName("Arquivo"))),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.TabItem &&
                     ((e.AutomationId ?? "").Equals("FileTab", StringComparison.OrdinalIgnoreCase) ||
                      (e.Name ?? "").Equals("Arquivo", StringComparison.OrdinalIgnoreCase)),
                limitarAoMesmoProcesso: true,
                processId: _cachedProcessIdPromob
            );

            if (abaArquivo != null){
                Log("  [OK] Aba 'Arquivo' encontrada. Clicando...");
                SelecionarOuClicar(abaArquivo);
                EsperarUiRespirar(400);
            }

            Log("  [INFO] Procurando botão 'Fechar' (ProjectClose)...");
            var btnFechar = BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                        .And(cf.ByAutomationId("ProjectClose").Or(cf.ByName("Fechar"))),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                     ((e.AutomationId ?? "").Equals("ProjectClose", StringComparison.OrdinalIgnoreCase) ||
                      (e.Name ?? "").Equals("Fechar", StringComparison.OrdinalIgnoreCase)),
                limitarAoMesmoProcesso: true,
                processId: _cachedProcessIdPromob
            );

            if (btnFechar != null){
                Log("  [OK] Botão 'Fechar' encontrado. Clicando...");
                ClicarComFallback(btnFechar);
                
                // Aguardar um momento para o popup de salvar
                Log("  [INFO] Aguardando possível popup 'Deseja salvar?'...");
                var popup = EsperarAteRetorno(() => EncontrarPopupAtencao(automation.GetDesktop()), 3000);
                
                if (popup != null){
                    Log($"  [OK] Popup detectado: '{popup.Name}'. Clicando em 'Não'...");
                    var btnNao = popup.FindFirstDescendant(cf => 
                        cf.ByName("Não").Or(cf.ByName("Nao")).Or(cf.ByName("No")));
                    
                    if (btnNao != null)
                        ClicarComFallback(btnNao);
                    else
                        Keyboard.Type("n");
                    
                    EsperarUiRespirar(800);
                }
            }
            else{
                Log("  [AVISO] Botão 'Fechar' não encontrado. Tentando atalho Alt+F...", LogLevel.Warn);
                Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_F);
                EsperarUiRespirar(800);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Navegação final
        static void AbrirListagem(UIA3Automation automation, Window janela){
            AtivarJanela(janela);

            var raizBusca = ObterHostOuJanela(janela);

            Log("  [INFO] Procurando aba 'Ferramentas'...");
            var abaFerramentas = BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)
                        .And(cf.ByAutomationId("ToolsTab").Or(cf.ByName("Ferramentas"))),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.TabItem &&
                     ((e.AutomationId ?? "").Equals("ToolsTab", StringComparison.OrdinalIgnoreCase) ||
                      (e.Name ?? "").Equals("Ferramentas", StringComparison.OrdinalIgnoreCase)),
                limitarAoMesmoProcesso: true,
                processId: _cachedProcessIdPromob
            );

            if (abaFerramentas != null){
                Log("  [OK] Aba 'Ferramentas' encontrada. Clicando...");
                SelecionarOuClicar(abaFerramentas);
                EsperarUiRespirar();
            }
            else{
                Log("  [AVISO] Aba 'Ferramentas' não encontrada.", LogLevel.Warn);
            }

            Log("  [INFO] Procurando botão de 'Orçamento'...");
            var btnOrcamento = BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                        .And(cf.ByName("Orçamento").Or(cf.ByName("Orcamento")))
                        .And(cf.ByAutomationId("PART_ToggleButton")),
                e => (e.AutomationId ?? "") == "PART_ToggleButton" &&
                     ((e.Name ?? "").Equals("Orçamento", StringComparison.OrdinalIgnoreCase) ||
                      (e.Name ?? "").Equals("Orcamento", StringComparison.OrdinalIgnoreCase)),
                limitarAoMesmoProcesso: true,
                processId: _cachedProcessIdPromob
            );

            btnOrcamento ??= raizBusca.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.MenuBar)
                  .And(cf.ByAutomationId("bOrçamento").Or(cf.ByAutomationId("bOrcamento"))));

            if (btnOrcamento != null){
                Log("  [OK] Botão 'Orçamento' encontrado. Clicando...");
                ClicarComFallback(btnOrcamento);
                EsperarUiRespirar();
            }
            else{
                Log("  [AVISO] Botão 'Orçamento' não encontrado.", LogLevel.Warn);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Importação
        static void ClicarBotaoImportar(Window janelaPromob){
            while (true){
                AutomationElement? btnFound = null;

                // 1. Tenta usar o cache global primeiro
                if (ElementoValido(_cachedBotaoImportar)){
                    Log("  [OK] Usando botão 'Importar' do cache.");
                    btnFound = _cachedBotaoImportar;
                }
                else {
                    Log("  [INFO] Iniciando busca persistente do botão 'Importar Projeto'...");
                    
                    // Tenta localizar o host (Ribbon) do botão de forma otimizada
                    var buscaEm = ObterHostOuJanela(janelaPromob);

                    // Busca pelo ID ou Fallback (limitado a 4 níveis)
                    btnFound = BuscarElementoComFallback(
                        buscaEm,
                        cf => cf.ByAutomationId(AutomationIdImportar),
                        e => (e.AutomationId ?? "").Equals(AutomationIdImportar, StringComparison.OrdinalIgnoreCase) ||
                             (e.Name ?? "").Equals("Importar projeto", StringComparison.OrdinalIgnoreCase),
                        limitarAoMesmoProcesso: true,
                        processId: _cachedProcessIdPromob
                    );
                }

                if (btnFound != null){
                    // 2. Garante que o Promob está visível e focado
                    AtivarJanela(janelaPromob);

                    // 3. Clicar no botão
                    ClicarComFallback(btnFound);
                    _cachedBotaoImportar = btnFound; // Garante que está em cache se foi achado agora
                    
                    break; // Sucesso! Sai do loop.

                    Log("  [AVISO] Clique não surtiu efeito após 5s. Invalidando cache e tentando novamente...", LogLevel.Warn);
                    _cachedBotaoImportar = null; // Invalida para forçar nova busca se necessário
                }
                else {
                    Log("  [AVISO] Botão 'Importar' não encontrado. Tentando novamente em 5s...", LogLevel.Warn);
                }

                Thread.Sleep(5000); // Aguarda 5 segundos conforme solicitado pelo usuário
            }
        }

        //────────────────────────────────────────────────────────────────────
        static void AbrirDialogoEPreencher(UIA3Automation automation, Window janelaPromob, string caminhoArquivo){
            AtivarJanela(janelaPromob);

            var btnBrowse = janelaPromob.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                  .And(cf.ByName("...").Or(cf.ByName("Procurar")).Or(cf.ByAutomationId("BrowseButton"))));

            if (btnBrowse == null){
                btnBrowse = BuscarElementoComFallback(
                    janelaPromob,
                    cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                            .And(cf.ByName("...")),
                    e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                         (((e.Name ?? "").Contains("...")) ||
                          ((e.AutomationId ?? "").Contains("Browse", StringComparison.OrdinalIgnoreCase)))
                );
            }

            if (btnBrowse != null){
                Log($"  [OK] Botão de busca encontrado: {btnBrowse.Name}");
                ClicarComFallback(btnBrowse);
            }
            else{
                Log("  [AVISO] Botão de busca não encontrado. Usando TAB + SPACE...", LogLevel.Warn);
                Keyboard.Press(VirtualKeyShort.TAB);
                EsperarUiRespirar();
                Keyboard.Press(VirtualKeyShort.TAB);
                EsperarUiRespirar();
                Keyboard.Press(VirtualKeyShort.SPACE);
            }

            var dialogo = EsperarAteRetorno(() => JanelaArquivoAberta(automation), TimeoutLongo);
            if (dialogo == null)
                throw new Exception("Diálogo do Windows (Abrir/Salvar) não apareceu no tempo esperado.");

            PreencherDialogoNativo(automation, caminhoArquivo, dialogo);
        }

        //────────────────────────────────────────────────────────────────────
        static void PreencherDialogoNativo(UIA3Automation automation, string caminhoCompleto, Window dialogo){
            Log($"  [OK] Diálogo encontrado: {dialogo.Name}");
            AtivarJanela(dialogo);

            var nomeArquivo = Path.GetFileName(caminhoCompleto);
            Log($"  [INFO] Preenchendo campo 'Nome' via UIA: {nomeArquivo}");

            // ── Estratégia 1: SetValue direto no campo 'Nome' ──
            // O diálogo padrão do Windows tem AutomationId "1148" (campo Nome)
            bool preenchidoViaUia = false;
            AutomationElement? campoNome =
                dialogo.FindFirstDescendant(cf => cf.ByAutomationId("1148")) ??
                dialogo.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox)
                      .And(cf.ByAutomationId("FileNameControlHost"))) ??
                dialogo.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)
                      .And(cf.ByAutomationId("1148")));

            // Se não achou pelo id fixo, tenta o último ComboBox do diálogo (campo Nome fica no final)
            if (campoNome == null){
                var combos = dialogo.FindAllDescendants(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox));
                campoNome = combos.LastOrDefault();
            }

            if (campoNome != null){
                Log($"  [INFO] Campo 'Nome' encontrado (Id: {campoNome.AutomationId}, Tipo: {campoNome.ControlType}).");

                // NOVIDADE: Tenta achar o Edit interno para o SetValue ser mais "direto" e aceito pelo Windows
                var editInterno = campoNome.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
                var alvo = editInterno ?? campoNome;
                if (editInterno != null) Log("  [INFO] Usando elemento 'Edit' interno do ComboBox para SetValue.");

                // Tenta SetValue diretamente (sem teclado, sem foco)
                if (TentarDefinirValor(alvo, nomeArquivo)){
                    preenchidoViaUia = true;
                    Log("  [OK] Valor definido via UIA (SetValue).");
                }
                else{
                    // Fallback: foca o campo e digita (mas dentro do campo identificado via UIA)
                    Log("  [INFO] SetValue falhou. Tentando foco + seleção + digitação...");
                    try{
                        campoNome.Focus();
                        EsperarUiRespirar(200);
                        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                        EsperarUiRespirar(100);
                        Keyboard.Type(nomeArquivo);
                        EsperarUiRespirar(400);
                        preenchidoViaUia = true;
                    }
                    catch (Exception ex){
                        Log($"  [AVISO] Fallback de teclado falhou: {ex.Message}", LogLevel.Warn);
                    }
                }
            }
            else{
                Log("  [AVISO] Campo 'Nome' não encontrado via UIA.", LogLevel.Warn);
            }

            // ── Estratégia 2 (último recurso): clipboard + Enter ──
            if (!preenchidoViaUia){
                Log("  [AVISO] Usando clipboard como último recurso...", LogLevel.Warn);
                AtivarJanela(dialogo);
                CopiarParaClipboardNativo(nomeArquivo);
                EsperarUiRespirar(400);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                EsperarUiRespirar(800);
                Keyboard.Type(VirtualKeyShort.RETURN);
                EsperarUiRespirar(500);
                return;
            }

            // ── Clica em "Abrir" via UIA (sem pressionar Enter) ──
            // Prioriza o AutomationId "1" que é o padrão do botão Abrir/OK no Windows
            var btnAbrir = 
                dialogo.FindFirstDescendant(cf => cf.ByAutomationId("1").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))) ??
                dialogo.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                      .And(cf.ByName("Abrir").Or(cf.ByName("Open"))));

            if (btnAbrir != null){
                Log("  [OK] Clicando em 'Abrir' via Clique Físico (UIA).");
                try {
                    AtivarJanela(dialogo);
                    btnAbrir.AsButton().Click(); // Clique físico simula o usuário real
                } catch {
                    Log("  [AVISO] Falha no Clique Físico. Tentando Invoke...", LogLevel.Warn);
                    ClicarComFallback(btnAbrir);
                }
            }
            else{
                Log("  [AVISO] Botão 'Abrir' não encontrado. Usando Enter...", LogLevel.Warn);
                AtivarJanela(dialogo);
                Keyboard.Type(VirtualKeyShort.RETURN);
            }

            // ── Aguardar o fechamento do diálogo (evita corrida com o próximo passo) ──
            Log("  [INFO] Aguardando fechamento do diálogo...");
            bool fechou = EsperarAte(() => {
                try {
                    // Se der erro ao acessar as propriedades ou estiver offscreen, fechou
                    if (dialogo.Properties.IsOffscreen.ValueOrDefault) return true;

                    // Enquanto espera, verifica se apareceu algum popup de erro do Windows 
                    // (ex: "Arquivo não encontrado") que esteja bloqueando o fechamento
                    var desktop = automation.GetDesktop();
                    var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
                    var popupOS = janelas.FirstOrDefault(j => 
                        j.Properties.ProcessId == dialogo.Properties.ProcessId && 
                        ContemQualquer(j.Name, "Aviso", "Erro", "Atenção", "Atencao") &&
                        j.Name != dialogo.Name);

                    if (popupOS != null) {
                        Log($"  [AVISO] Popup detectado bloqueando o diálogo: '{popupOS.Name}'. Fechando...");
                        TratarPopupGenerico(popupOS.AsWindow());
                        AtivarJanela(dialogo);
                    }

                    return false;
                } catch { return true; }
            }, 2000);

            if (fechou) Log("  [OK] Diálogo de arquivo fechado com sucesso.");
            else Log("  [AVISO] Diálogo não fechou no tempo esperado.", LogLevel.Warn);

            EsperarUiRespirar(500);
        }


        //────────────────────────────────────────────────────────────────────
        static void ClicarAvancarWizard(Window janelaPromob){
            AtivarJanela(janelaPromob);

            Log("  [INFO] Procurando botão 'Avançar'...");
            var btnAvancar = janelaPromob.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                  .And(cf.ByName("Avançar").Or(cf.ByName("Avancar")).Or(cf.ByName("Next"))));

            if (btnAvancar != null){
                ClicarComFallback(btnAvancar);
                Log("  [OK] Botão 'Avançar' clicado.");
            }
            else{
                Log("  [AVISO] Botão 'Avançar' não encontrado. Tentando ENTER...", LogLevel.Warn);
                Keyboard.Type(VirtualKeyShort.RETURN);
            }

            // CORREÇÃO: timeout aumentado de 1000ms para 5000ms antes de cair no Vision
            var mudou = EsperarAte(() =>{
                var desktop = janelaPromob.Automation.GetDesktop();
                var popup = EncontrarPopupAtencao(desktop);
                if (popup != null) return true;

                var projetoOuLista = janelaPromob.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)
                      .Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.DataItem)));
                return projetoOuLista != null;
            }, 5000);  // era 1000ms — muito curto para o Promob processar o arquivo

            if (!mudou){
                VisionHelper.AguardarEstadoTela(
                    "Popup de 'Atenção' ou 'Confirmação' do Promob visível, OU lista de projetos importados apareceu",
                    maxTentativas: 8, intervaloMs: 1500, fallbackMs: 2000);
            }
        }

        //────────────────────────────────────────────────────────────────────
        static void CancelarPopupNovoProjeto(UIA3Automation automation){
            Log("  [INFO] Aguardando popup 'Atenção'...");

            var popup = EsperarAteRetorno(() => EncontrarPopupAtencao(automation.GetDesktop()), 5000);
            if (popup == null){
                Log("  [INFO] Popup de novo projeto não apareceu.");
                return;
            }

            Log($"  [OK] Popup encontrado: {popup.Name}");
            AtivarJanela(popup);

            var btnCancelar = popup.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                  .And(cf.ByName("Cancelar").Or(cf.ByName("Não")).Or(cf.ByName("Nao")).Or(cf.ByName("No"))));

            if (btnCancelar != null){
                ClicarComFallback(btnCancelar);
                Log("  [OK] Botão de cancelamento clicado no popup.");
            }
            else{
                Log("  [AVISO] Botão de cancelamento não encontrado. Usando ESC...", LogLevel.Warn);
                Keyboard.Type(VirtualKeyShort.ESCAPE);
            }
        }

        //────────────────────────────────────────────────────────────────────
        static void AbrirProjetoSelecionado(Window janelaPromob, string nomeProjeto){
            AtivarJanela(janelaPromob);
            Log($"  [INFO] Procurando projeto '{nomeProjeto}' para abrir...");

            var itemProjeto = BuscarElementoComFallback(
                janelaPromob,
                cf => cf.ByName(nomeProjeto).Or(cf.ByName(nomeProjeto.ToUpperInvariant())),
                e => string.Equals(e.Name, nomeProjeto, StringComparison.OrdinalIgnoreCase),
                limitarAoMesmoProcesso: true,
                processId: _cachedProcessIdPromob
            );

            if (itemProjeto != null){
                Log("  [OK] Projeto encontrado na lista. Executando duplo clique...");
                itemProjeto.DoubleClick();
            }
            else{
                Log("  [AVISO] Elemento do projeto não encontrado via nome. Tentando localizar o primeiro item da lista...", LogLevel.Warn);

                // Busca por QUALQUER item de lista que possa ser um projeto
                var qualquerItem = BuscarElementoComFallback(
                    janelaPromob,
                    cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem).Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.DataItem)),
                    e => e.ControlType == FlaUI.Core.Definitions.ControlType.ListItem || e.ControlType == FlaUI.Core.Definitions.ControlType.DataItem,
                    limitarAoMesmoProcesso: true,
                    processId: _cachedProcessIdPromob
                );

                if (qualquerItem != null){
                    Log($"  [OK] Item genérico encontrado ('{qualquerItem.Name}'). Executando duplo clique...");
                    qualquerItem.DoubleClick();
                    EsperarUiRespirar(800);
                }
                else{
                    Log("  [AVISO] Nenhum item de lista encontrado. Tentando botão 'Abrir projeto'...", LogLevel.Warn);

                    var btnAbrir = janelaPromob.FindFirstDescendant(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                          .And(cf.ByName("Abrir projeto").Or(cf.ByName("Abrir")).Or(cf.ByName("Acessar")).Or(cf.ByName("Editar"))));

                    if (btnAbrir != null){
                        Log("  [OK] Botão de abrir encontrado. Clicando...");
                        ClicarComFallback(btnAbrir);
                    }
                    else{
                        Log("  [AVISO] Nenhuma forma de abrir encontrada. Tentando ENTER...", LogLevel.Warn);
                        Keyboard.Type(VirtualKeyShort.RETURN);
                    }
                }
            }

            // Loop de espera dinâmico para garantir carregamento total (conforme solicitado pelo usuário)
            int timeoutAtual = 5000;
            int tentativaLoop = 1;

            while (true){
                Log($"  [INFO] Aguardando o carregamento do projeto (Tentativa {tentativaLoop}, timeout: {timeoutAtual/1000}s)...");

                bool carregou = EsperarAte(() =>{
                    // 1. Procura a aba Ferramentas (indicação de que a UI base carregou)
                    var aba = janelaPromob.FindFirstDescendant(cf => 
                        cf.ByAutomationId("ToolsTab").Or(cf.ByName("Ferramentas")));
                    
                    // 2. Procura a mensagem de "carregando" (indicação de que o carregamento de módulos ainda ocorre)
                    var msgCarregando = janelaPromob.FindFirstDescendant(cf => 
                        cf.ByName("Alguns itens ainda estão sendo carregados"));

                    // Só está pronto se a aba existir E a mensagem de carregamento não estiver presente
                    bool pronto = (aba != null) && (msgCarregando == null);
                    
                    if (!pronto){
                        // Trata popups que podem bloquear o carregamento
                        var desktop = janelaPromob.Automation.GetDesktop();
                        var popup = EncontrarPopupAtencao(desktop);
                        if (popup != null) {
                            Log($"  [AVISO] Popup detectado durante carregamento: '{popup.Name}'. Tratando...");
                            TratarPopupGenerico(popup);
                        }
                    }
                    else {
                        Log("  [OK] Aba 'Ferramentas' detectada e sem mensagens de carregamento pendente.");
                        SelecionarOuClicar(aba!);
                    }

                    return pronto;
                }, timeoutMs: timeoutAtual, intervaloMs: 2500);

                if (carregou){
                    Log("  [OK] Projeto carregado e validado com sucesso.");
                    EsperarUiRespirar(1000);
                    break; 
                }

                // Se chegou aqui, deu timeout na tentativa atual
                Log($"  [AVISO] Timeout de {timeoutAtual/1000}s atingido sem concluir o carregamento.", LogLevel.Warn);
                
                // Realiza uma varredura visual final como "voto de Minerva" antes de resetar o loop
                if (VisionHelper.Habilitado){
                    Log("  [VISION] Consultando se a tela parece carregada...");
                    var visao = VisionHelper.AguardarEstadoTela(
                        "A aba 'Ferramentas' está visível e não há mensagens de 'Carregando' ou 'Módulos Invisíveis' na parte inferior da tela.",
                        maxTentativas: 1, fallbackMs: 500);
                    
                    if (visao){
                        Log("  [VISION] IA confirmou que o projeto parece carregado. Prosseguindo.");
                        break;
                    }
                }

                tentativaLoop++;
                timeoutAtual = 10000; // Ciclos subsequentes de 30s conforme solicitado
                Log("  [INFO] Reiniciando verificação para novo ciclo de 10s...");
            }
        }

        //────────────────────────────────────────────────────────────────────
        static void TratarPopupGenerico(Window popup){
            Log($"  [INFO] Tratando popup: '{popup.Name}'");
            AtivarJanela(popup);

            // "Não é possível salvar enquanto há erros nos campos" ou similar
            if (ContemQualquer(popup.Name, "Aviso", "Erro", "Atencao", "Atenção")){
                var btnOk = popup.FindFirstDescendant(cf => cf.ByName("OK").Or(cf.ByName("Ok")).Or(cf.ByName("Concluir")));
                if (btnOk != null){
                    Log("  [OK] Clicando em OK no popup.");
                    ClicarComFallback(btnOk);
                }
                else{
                    Log("  [OK] Enviando ALT+F4 para fechar o popup.");
                    Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
                }
                EsperarUiRespirar(500);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Recuperação
        static void TentarRecuperar(UIA3Automation automation){
            Log("  [INFO] Executando rotina de recuperação...");

            try{
                InvalidarCacheUi();

                Keyboard.Press(VirtualKeyShort.ESCAPE);
                EsperarUiRespirar(250);

                Keyboard.Press(VirtualKeyShort.ESCAPE);
                EsperarUiRespirar(250);

                var desktop = automation.GetDesktop();
                var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

                foreach (var j in janelas){
                    var titulo = j.Name ?? "";
                    if (titulo.Contains("Erro", StringComparison.OrdinalIgnoreCase) ||
                        titulo.Contains("Atenção", StringComparison.OrdinalIgnoreCase) ||
                        titulo.Contains("Atencao", StringComparison.OrdinalIgnoreCase)){
                        try{
                            j.AsWindow().SetForeground();
                            Keyboard.Press(VirtualKeyShort.ESCAPE);
                            EsperarUiRespirar(200);
                        }
                        catch{
                            // ignora
                        }
                    }
                }

                var janela = AguardarJanelaPromob(automation, 2500);
                if (janela != null){
                    FecharProjetoEIgnorarSalvar(janela);
                }
            }
            catch (Exception ex){
                Log($"  [AVISO] Recuperação falhou: {ex.Message}", LogLevel.Warn);
            }

            EsperarUiRespirar(1000);
        }

        //────────────────────────────────────────────────────────────────────
        static void FecharProjetoEIgnorarSalvar(Window janelaPromob){
            AtivarJanela(janelaPromob);

            Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_A);
            EsperarUiRespirar();

            Keyboard.Type("f");
            EsperarUiRespirar(800);

            Keyboard.Type("n");
            EsperarUiRespirar(800);
        }

        // ────────────────────────────────────────────────────────────────────
        // Busca / helpers de janela
        static Window? AguardarJanelaPromob(UIA3Automation automation, int timeoutMs = TimeoutPadrao){
            Window? encontrada = null;

            // CORREÇÃO: busca o processo Promob apenas 1 vez antes do loop de poll
            // Antes chamava GetProcesses() a cada 200ms — muito caro
            var promobProc = System.Diagnostics.Process.GetProcesses()
                .FirstOrDefault(p => p.ProcessName.Contains("Promob", StringComparison.OrdinalIgnoreCase) && 
                                   !p.ProcessName.Contains("Uploader", StringComparison.OrdinalIgnoreCase));

            if (promobProc != null)
                _cachedProcessIdPromob = promobProc.Id;

            EsperarAte(() =>{
                var desktop = automation.GetDesktop();
                var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

                foreach (var j in janelas){
                    if (promobProc != null){
                        if (j.Properties.ProcessId.ValueOrDefault == promobProc.Id){
                            var name = j.Name ?? "";
                            if (EhJanelaPromob(name) || name.Contains("Promob", StringComparison.OrdinalIgnoreCase)){
                                encontrada = j.AsWindow();
                                return true;
                            }
                        }
                    }
                    else{
                        var fallbackName = j.Name ?? "";
                        if (EhJanelaPromob(fallbackName)){
                            encontrada = j.AsWindow();
                            try { _cachedProcessIdPromob = encontrada.Properties.ProcessId.ValueOrDefault; } catch { }
                            return true;
                        }
                    }
                }

                return false;
            }, timeoutMs);

            if (encontrada != null)
                Log($"  [OK] Janela encontrada (PID: {_cachedProcessIdPromob}): '{encontrada.Name}'");

            return encontrada;
        }

        //────────────────────────────────────────────────────────────────────
        static bool EhJanelaPromob(string? nome){
            if (string.IsNullOrWhiteSpace(nome))
                return false;

            if (nome.Contains("Visual Studio Code", StringComparison.OrdinalIgnoreCase) || 
                nome.Contains("VS Code", StringComparison.OrdinalIgnoreCase))
                return false;

            return nome.Contains("- Promob Studio", StringComparison.OrdinalIgnoreCase) || 
                   nome.Contains("Promob Studio Bartz", StringComparison.OrdinalIgnoreCase);
        }

        //────────────────────────────────────────────────────────────────────
        static Window? EncontrarJanelaWizard(UIA3Automation automation, Window janelaPrincipal){
            var desktop = automation.GetDesktop();

            var wizardDesktop = desktop.FindFirstChild(cf => cf.ByName(NomeJanelaWizardImportacao));
            if (wizardDesktop != null)
                return wizardDesktop.AsWindow();

            var wizardDesc = janelaPrincipal.FindFirstDescendant(cf => cf.ByName(NomeJanelaWizardImportacao));
            return wizardDesc?.AsWindow();
        }

        //────────────────────────────────────────────────────────────────────
        static Window? JanelaArquivoAberta(UIA3Automation automation){
            var desktop = automation.GetDesktop();

            var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
            var dialogo = janelas.FirstOrDefault(j =>
                ContemQualquer(j.Name, "Abrir", "Open", "Salvar Como", "Save As"));

            if (dialogo != null)
                return dialogo.AsWindow();

            var profundo = desktop.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)
                  .And(cf.ByName("Abrir").Or(cf.ByName("Open")).Or(cf.ByName("Salvar Como")).Or(cf.ByName("Save As"))));

            return profundo?.AsWindow();
        }


        //────────────────────────────────────────────────────────────────────
        static Window? EncontrarPopupAtencao(AutomationElement desktop){
            var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

            var popup = janelas.FirstOrDefault(j =>
                ContemQualquer(j.Name, "Atenção", "Atencao", "Atençao", "Confirmação", "Confirmacao", "Salvar", "Save"));

            if (popup != null)
                return popup.AsWindow();

            var profundo = desktop.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)
                  .And(cf.ByName("Atenção")
                  .Or(cf.ByName("Atencao"))
                  .Or(cf.ByName("Confirmação"))
                  .Or(cf.ByName("Confirmacao"))
                  .Or(cf.ByName("Salvar"))
                  .Or(cf.ByName("Save"))
                  .Or(cf.ByName("Promob"))));

            return profundo?.AsWindow();
        }

        //────────────────────────────────────────────────────────────────────
        static AutomationElement ObterHostOuJanela(Window janela){
            if (ElementoValido(_cachedHost))
                return _cachedHost!;

            // OPTIMIZAÇÃO: Busca o host apenas nos níveis superficiais do Promob (mais rápido)
            // Geralmente elementHost1 está nos primeiros 3-4 níveis
            _cachedHost = BuscarElementoComFallback(
                janela,
                cf => cf.ByAutomationId(AutomationIdHost),
                e => (e.AutomationId ?? "").Equals(AutomationIdHost, StringComparison.OrdinalIgnoreCase),
                limitarAoMesmoProcesso: true,
                processId: _cachedProcessIdPromob
            );

            if (_cachedHost != null)
                Log($"  [OK] {AutomationIdHost} localizado e cacheado para uso futuro.");
            else{
                Log($"  [AVISO] {AutomationIdHost} não encontrado. Usando janela principal.", LogLevel.Debug);
            }

            return _cachedHost ?? janela;
        }

        //────────────────────────────────────────────────────────────────────
        static AutomationElement? BuscarElementoComFallback(
            AutomationElement raiz,
            Func<ConditionFactory, ConditionBase> buscaPrincipal,
            Func<AutomationElement, bool>? filtroFallback = null,
            bool limitarAoMesmoProcesso = false,
            int? processId = null){
            var direto = raiz.FindFirstDescendant(buscaPrincipal);
            if (direto != null)
                return direto;

            if (filtroFallback == null)
                return null;

            // CORREÇÃO: FindAllDescendants() é muito caro — limita a profundidade máxima da busca
            // usando FindAllChildren recursivo até 4 níveis, evitando varrer a árvore inteira
            var todos = BuscarAteNivel(raiz, maxNivel: 4);
            var consulta = todos.AsEnumerable();

            if (limitarAoMesmoProcesso && processId.HasValue){
                consulta = consulta.Where(e =>{
                    try { return e.Properties.ProcessId.ValueOrDefault == processId.Value; }
                    catch { return true; }
                });
            }

            return consulta.FirstOrDefault(filtroFallback);
        }


        //────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Percorre a árvore de UI até uma profundidade máxima (evita varrer tudo com FindAllDescendants).
        /// </summary>
        static System.Collections.Generic.IEnumerable<AutomationElement> BuscarAteNivel(AutomationElement raiz, int maxNivel, int nivelAtual = 0){
            if (nivelAtual > maxNivel)
                yield break;

            AutomationElement[] filhos;
            try { filhos = raiz.FindAllChildren(); }
            catch { yield break; }

            foreach (var filho in filhos){
                yield return filho;
                foreach (var desc in BuscarAteNivel(filho, maxNivel, nivelAtual + 1))
                    yield return desc;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Utilitários de UI
        //────────────────────────────────────────────────────────────────────
        static bool ElementoValido(AutomationElement? el){
            if (el == null)
                return false;

            try{
                _ = el.Name;
                _ = el.ControlType;
                return true;
            }
            catch{
                return false;
            }
        }

        //────────────────────────────────────────────────────────────────────
        static void InvalidarCacheUi(){
            _cachedBotaoImportar = null;
            _cachedHost = null;
        }

        //────────────────────────────────────────────────────────────────────
        static void AtivarJanela(Window janela){
            if (janela == null) return;

            try{
                // Se já estiver no topo, não faz nada
                var top = GetForegroundWindow();
                if (top != IntPtr.Zero && top == (IntPtr)janela.Properties.NativeWindowHandle.ValueOrDefault) 
                    return;

                if (janela.Patterns.Window.IsSupported){
                    var estadoVisual = janela.Patterns.Window.Pattern.WindowVisualState.ValueOrDefault;
                    if (estadoVisual == FlaUI.Core.Definitions.WindowVisualState.Minimized){
                        janela.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Normal);
                        EsperarUiRespirar(400);
                    }
                }
            }
            catch { }

            try{
                janela.SetForeground();
                janela.Focus();
                EsperarUiRespirar(200);
            }
            catch{
                try { janela.Focus(); } catch { }
            }
        }

        //────────────────────────────────────────────────────────────────────
        static void Focar(AutomationElement el){
            try { el.Focus(); } catch { }
        }

        //────────────────────────────────────────────────────────────────────
        static void ClicarComFallback(AutomationElement el){
            if (el == null) return;

            try{
                if (el.Patterns.Invoke.IsSupported){
                    el.Patterns.Invoke.Pattern.Invoke();
                    return;
                }
            }
            catch { }

            try{
                el.Click();
                return;
            }
            catch { }

            Focar(el);
            Keyboard.Type(VirtualKeyShort.SPACE);
        }

        //────────────────────────────────────────────────────────────────────
        static void SelecionarOuClicar(AutomationElement el){
            try{
                if (el.Patterns.SelectionItem.IsSupported){
                    el.Patterns.SelectionItem.Pattern.Select();
                    return;
                }
            }
            catch { }

            ClicarComFallback(el);
        }

        //────────────────────────────────────────────────────────────────────
        static bool TentarDefinirValor(AutomationElement el, string valor){
            try{
                if (el.Patterns.Value.IsSupported){
                    el.Patterns.Value.Pattern.SetValue(valor);
                    return true;
                }
            }
            catch { }

            try{
                if (el.ControlType == FlaUI.Core.Definitions.ControlType.ComboBox){
                    el.AsComboBox().Value = valor;
                    return true;
                }
            }
            catch { }

            try{
                if (el.Patterns.Text.IsSupported){
                    Focar(el);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                    EsperarUiRespirar(80);
                    Keyboard.Type(valor);
                    return true;
                }
            }
            catch { }

            return false;
        }

        // ────────────────────────────────────────────────────────────────────
        // Utilitários genéricos
        static bool EsperarAte(Func<bool> condicao, int timeoutMs = TimeoutPadrao, int intervaloMs = PollMs){
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs){
                try{
                    if (condicao())
                        return true;
                }
                catch { }

                Thread.Sleep(intervaloMs);
            }

            return false;
        }

        //────────────────────────────────────────────────────────────────────
        static T? EsperarAteRetorno<T>(Func<T?> produtor, int timeoutMs = TimeoutPadrao, int intervaloMs = PollMs) where T : class{
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs){
                try{
                    var valor = produtor();
                    if (valor != null)
                        return valor;
                }
                catch { }

                Thread.Sleep(intervaloMs);
            }

            return null;
        }


        //────────────────────────────────────────────────────────────────────
        static void EsperarUiRespirar(int ms = DelayMinimo){
            Thread.Sleep(ms);
        }

        //────────────────────────────────────────────────────────────────────
        static void Medir(string nome, Action acao){
            var sw = Stopwatch.StartNew();
            try { acao(); }
            finally { Log($"  [TEMPO] {nome}: {sw.ElapsedMilliseconds} ms", LogLevel.Debug); }
        }

        //────────────────────────────────────────────────────────────────────
        static bool ContemQualquer(string? texto, params string[] valores){
            if (string.IsNullOrWhiteSpace(texto))
                return false;

            return valores.Any(v => texto.Contains(v, StringComparison.OrdinalIgnoreCase));
        }

        //────────────────────────────────────────────────────────────────────
        static void RegistrarErro(string nomeArquivo, Exception ex){
            try{
                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Arquivo: {nomeArquivo}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}"
                );
            }
            catch { }
        }


        //────────────────────────────────────────────────────────────────────
        static void Log(string mensagem, LogLevel nivel = LogLevel.Info){
            if (nivel <= NivelAtual)
                Console.WriteLine(mensagem);
        }

        //────────────────────────────────────────────────────────────────────
        /// <summary>
        /// CORREÇÃO: Copia texto para o clipboard via Win32 nativo.
        /// Evita spawnar um processo PowerShell a cada arquivo (que bloqueava até 2 segundos).
        /// </summary>
        static void CopiarParaClipboardNativo(string texto){
            try{
                // Tenta até 5 vezes (o clipboard pode estar bloqueado por outra aplicação)
                for (int tentativa = 0; tentativa < 5; tentativa++){
                    if (OpenClipboard(IntPtr.Zero)){
                        try{
                            EmptyClipboard();

                            // Aloca memória global para o texto Unicode (UTF-16 + null terminator)
                            int byteCount = (texto.Length + 1) * 2;
                            var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)byteCount);
                            if (hGlobal == IntPtr.Zero)
                                throw new Exception("GlobalAlloc falhou");

                            var pGlobal = GlobalLock(hGlobal);
                            try{
                                Marshal.Copy(texto.ToCharArray(), 0, pGlobal, texto.Length);
                                Marshal.WriteInt16(pGlobal, texto.Length * 2, 0); // null terminator
                            }
                            finally{
                                GlobalUnlock(hGlobal);
                            }

                            SetClipboardData(CF_UNICODETEXT, hGlobal);
                            Log("  [OK] Caminho copiado para o Clipboard (Win32).", LogLevel.Debug);
                            return;
                        }
                        finally{
                            CloseClipboard();
                        }
                    }

                    Thread.Sleep(50);
                }

                // Fallback: PowerShell (caso Win32 falhe por algum motivo inesperado)
                CopiarParaClipboardPowerShell(texto);
            }
            catch (Exception ex){
                Log($"  [AVISO] Falha no clipboard nativo: {ex.Message}. Tentando PowerShell...", LogLevel.Warn);
                CopiarParaClipboardPowerShell(texto);
            }
        }


        // ────────────────────────────────────────────────────────────────────
        // Clipboard PowerShell
        static void CopiarParaClipboardPowerShell(string texto){
            try{
                var escapado = texto.Replace("'", "''");
                var startInfo = new ProcessStartInfo("powershell", $"-NoProfile -NonInteractive -Command \"Set-Clipboard -Value '{escapado}'\""){
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var p = System.Diagnostics.Process.Start(startInfo);
                p?.WaitForExit(2000);
                Log("  [OK] Caminho copiado via PowerShell.", LogLevel.Debug);
            }
            catch (Exception ex){
                Log($"  [AVISO] Falha ao copiar para Clipboard: {ex.Message}", LogLevel.Warn);
            }
        }


        // ────────────────────────────────────────────────────────────────────
        // Banner
        static void Banner(){
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║   Automação Promob - Gerador de XML      ║");
            Console.WriteLine("║      Versão otimizada com FlaUI + IA     ║");
            Console.WriteLine("╚══════════════════════════════════════════╝\n");
        }


        // ────────────────────────────────────────────────────────────────────
        // Diagnóstico opcional
        static void ListarBotoesProject(Window janela){
            var processId = janela.Properties.ProcessId.ValueOrDefault;
            Console.WriteLine($"[INFO] Analisando estrutura para Processo: {processId}");

            var janelasDoProcesso = janela.Automation.GetDesktop().FindAllChildren()
                .Where(x => x.Properties.ProcessId.ValueOrDefault == processId)
                .ToList();

            if (janelasDoProcesso.Count > 1){
                Console.WriteLine($"[AVISO] Encontradas {janelasDoProcesso.Count} janelas para este processo:");
                foreach (var j in janelasDoProcesso)
                    Console.WriteLine($"  - Window: '{j.Name}' (ID: {j.Properties.AutomationId.ValueOrDefault})");
            }

            var host = janela.FindFirstDescendant(AutomationIdHost);
            if (host != null){
                Console.WriteLine("[OK] 'elementHost1' encontrado! Escaneando conteúdo profundo...");
                var items = host.FindAllDescendants()
                    .Where(e => !string.IsNullOrEmpty(e.Name) || !string.IsNullOrEmpty(e.Properties.AutomationId.ValueOrDefault))
                    .GroupBy(e => (e.Properties.AutomationId.ValueOrDefault ?? "") + "|" + (e.Name ?? ""))
                    .Select(g => g.First())
                    .ToList();

                foreach (var e in items)
                    Console.WriteLine($"  -> Tipo: {e.ControlType}, Nome: '{e.Name}', Id: '{e.Properties.AutomationId.ValueOrDefault}'");
            }
            else{
                Console.WriteLine("[AVISO] elementHost1 não encontrado. Escaneando janela toda...");
                var all = janela.FindAllDescendants()
                    .Where(e => !string.IsNullOrEmpty(e.Name))
                    .GroupBy(e => (e.Properties.AutomationId.ValueOrDefault ?? "") + "|" + (e.Name ?? ""))
                    .Select(g => g.First())
                    .Take(50)
                    .ToList();

                foreach (var e in all)
                    Console.WriteLine($"  -> Tipo: {e.ControlType}, Nome: '{e.Name}', Id: '{e.Properties.AutomationId.ValueOrDefault}'");
            }

            Console.WriteLine("------------------------------------------");
        }
    }
}