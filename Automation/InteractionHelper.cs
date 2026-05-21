using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using AutomacaoPromobTeste.Utils;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace AutomacaoPromobTeste.Automation{
    
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Classe utilitária responsável por gerenciar interações diretas e robustas com elementos da interface do Windows.
        /// Contém lógica de cliques resiliêntes, preenchimento de campos, ativação de janelas e esperas inteligentes.
        /// </summary>
    //--------------------------------------------------------------------------------------
    
    public static class InteractionHelper{

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Importa a função nativa do Windows (User32.dll) para identificar qual janela está atualmente em foco na tela.
            /// </summary>
            /// <returns>O ponteiro de memória (Handle) da janela ativa em primeiro plano.</returns>
        //--------------------------------------------------------------------------------------
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Garante que uma determinada janela seja trazida para o primeiro plano (foco do usuário),
            /// restaurando-a se estiver minimizada e garantindo o foco do teclado.
            /// </summary>
            /// <param name="janela">A janela que deve ser ativada.</param>
        //--------------------------------------------------------------------------------------
        public static void AtivarJanela(Window janela){
            
            if (janela == null) return;

            try{
                // Se a janela já for a janela ativa atual do Windows, ignora para evitar flickers/lentidão
                var top = GetForegroundWindow();
                if (top != IntPtr.Zero && top == (IntPtr)janela.Properties.NativeWindowHandle.ValueOrDefault)
                    return;

                // Verifica se a janela suporta o padrão WindowPattern (UIA nativo)
                if (janela.Patterns.Window.IsSupported){
                    var estadoVisual = janela.Patterns.Window.Pattern.WindowVisualState.ValueOrDefault;
                    // Se a janela estiver minimizada, restaura para o estado Normal
                    if (estadoVisual == FlaUI.Core.Definitions.WindowVisualState.Minimized){
                        janela.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Normal);
                        EsperarUiRespirar(400); // Aguarda o Windows terminar a animação de restauração
                    }
                }
            }
            catch { }

            try{
                janela.SetForeground(); // Traz a janela para a frente de todas as outras
                janela.Focus();          // Foca o teclado na janela
                EsperarUiRespirar(200);
            }
            catch{
                try { janela.Focus(); } catch { }
            }
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Tenta focar o teclado em um elemento de automação específico de forma segura.
            /// </summary>
            /// <param name="el">O elemento que receberá o foco.</param>
        //--------------------------------------------------------------------------------------
        public static void Focar(AutomationElement el){
            try { el.Focus(); } catch { }
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Executa um clique resiliente em um elemento usando três níveis de fallback (plano A, B e C).
            /// Ideal para evitar falhas onde elementos estão parcialmente ocultos ou não respondem a cliques normais.
            /// </summary>
            /// <param name="el">O elemento que será clicado.</param>
        //--------------------------------------------------------------------------------------
        public static void ClicarComFallback(AutomationElement el){
            if (el == null) return;

            // FASE A: Tenta o padrão nativo "Invoke" (Executa a ação do botão por trás dos panos, sem mexer o mouse físico)
            try{
                if (el.Patterns.Invoke.IsSupported){
                    el.Patterns.Invoke.Pattern.Invoke();
                    return;
                }
            }
            catch { }

            // FASE B: Se o "Invoke" falhar, simula um clique físico do mouse no centro das coordenadas do elemento
            try{
                el.Click();
                return;
            }
            catch { }

            // FASE C: Se tudo falhar, foca no elemento e simula o pressionamento físico da tecla ESPAÇO (atalho universal de botões)
            Focar(el);
            Keyboard.Type(VirtualKeyShort.SPACE);
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Seleciona um item (útil para Abas/Tabs, ComboBoxes ou listas de seleção).
            /// Se o elemento não suportar seleção nativa, executa um clique resiliente.
            /// </summary>
            /// <param name="el">O elemento a ser selecionado ou clicado.</param>
        //--------------------------------------------------------------------------------------
        public static void SelecionarOuClicar(AutomationElement el){
            try{
                // Tenta usar o padrão nativo de seleção do Windows (SelectionItem)
                if (el.Patterns.SelectionItem.IsSupported){
                    el.Patterns.SelectionItem.Pattern.Select();
                    return;
                }
            }
            catch { }

            // Se não der suporte à seleção nativa, resolve via clique comum
            ClicarComFallback(el);
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Insere ou define um texto/valor dentro de um campo de entrada (Input, TextBox ou ComboBox) de forma resiliente.
            /// </summary>
            /// <param name="el">O elemento de entrada de dados.</param>
            /// <param name="valor">O texto/valor a ser preenchido.</param>
            /// <returns><c>true</c> se o preenchimento for bem-sucedido; caso contrário, <c>false</c>.</returns>
        //--------------------------------------------------------------------------------------
        public static bool TentarDefinirValor(AutomationElement el, string valor){
            // TENTATIVA 1: Altera o valor diretamente por API interna do Windows (ValuePattern) - Instantâneo
            try{
                if (el.Patterns.Value.IsSupported){
                    el.Patterns.Value.Pattern.SetValue(valor);
                    return true;
                }
            }
            catch { }

            // TENTATIVA 2: Trata como ComboBox e seta a propriedade de valor
            try{
                if (el.ControlType == FlaUI.Core.Definitions.ControlType.ComboBox){
                    el.AsComboBox().Value = valor;
                    return true;
                }
            }
            catch { }

            // TENTATIVA 3: Simulação física. Foca no campo, dá um "CTRL+A" para limpar e digita letra por letra pelo teclado simulado
            try{
                if (el.Patterns.Text.IsSupported){
                    Focar(el);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); // Seleciona tudo
                    EsperarUiRespirar(80);
                    Keyboard.Type(valor); // Digita o novo valor
                    return true;
                }
            }
            catch { }

            return false;
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Aguarda repetidamente (Polling) até que uma determinada condição lógica seja verdadeira.
            /// Evita travamento infinito usando um mecanismo de Timeout.
            /// </summary>
            /// <param name="condicao">A função lambda que representa a condição esperada.</param>
            /// <param name="timeoutMs">Tempo máximo de espera em milissegundos antes de abortar.</param>
            /// <param name="intervaloMs">Intervalo de tempo entre as verificações.</param>
            /// <returns><c>true</c> se a condição for atendida dentro do timeout; caso contrário, <c>false</c>.</returns>
        //--------------------------------------------------------------------------------------
        public static bool EsperarAte(Func<bool> condicao, int timeoutMs = 5000, int intervaloMs = 200){
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


        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Versão genérica de espera. Aguarda até que uma função retorne um objeto válido (não nulo)
            /// ou estoure o tempo limite. Muito útil para esperar que janelas e modais apareçam na tela.
            /// </summary>
            /// <typeparam name="T">O tipo da classe esperada (deve ser uma classe).</typeparam>
            /// <param name="produtor">A função de busca que tenta instanciar ou localizar o objeto.</param>
            /// <param name="timeoutMs">Tempo máximo de espera em milissegundos.</param>
            /// <param name="intervaloMs">Intervalo de tempo entre as tentativas.</param>
            /// <returns>O objeto localizado do tipo <typeparamref name="T"/>, ou <c>null</c> se expirar o tempo limite.</returns>
        //--------------------------------------------------------------------------------------
        public static T? EsperarAteRetorno<T>(Func<T?> produtor, int timeoutMs = 5000, int intervaloMs = 200) where T : class{
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

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Pausa a thread de execução por um curto período para permitir que o sistema operacional 
            /// processe eventos pendentes de renderização de tela (UI Breathing).
            /// </summary>
            /// <param name="ms">O tempo de pausa em milissegundos (padrão é 150ms).</param>
        //--------------------------------------------------------------------------------------
        public static void EsperarUiRespirar(int ms = 150){
            Thread.Sleep(ms);
        }


        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Verifica se um texto contém qualquer uma das palavras-chave especificadas na lista, ignorando maiúsculas e minúsculas.
            /// </summary>
            /// <param name="texto">O texto base que será analisado.</param>
            /// <param name="valores">As palavras-chave que serão buscadas no texto base.</param>
            /// <returns><c>true</c> se pelo menos uma das palavras-chave estiver contida no texto; caso contrário, <c>false</c>.</returns>
        //--------------------------------------------------------------------------------------
        public static bool ContemQualquer(string? texto, params string[] valores){
            if (string.IsNullOrWhiteSpace(texto))
                return false;

            return valores.Any(v => texto.Contains(v, StringComparison.OrdinalIgnoreCase));
        }
        
        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Valida se um objeto <see cref="AutomationElement"/> ainda existe, está ativo e pode ser lido 
            /// sem disparar exceções de conexão perdida com o Windows.
            /// </summary>
            /// <param name="el">O elemento de automação a ser verificado.</param>
            /// <returns><c>true</c> se o elemento for válido e estiver disponível; caso contrário, <c>false</c>.</returns>
        //--------------------------------------------------------------------------------------
        public static bool ElementoValido(AutomationElement? el){
            if (el == null)
                return false;

            try{
                // Tenta acessar propriedades do elemento. Se o elemento tiver sido destruído pelo Windows, lançará um erro.
                _ = el.Name; 
                _ = el.ControlType;
                return true;
            }
            catch{
                return false;
            }
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Aciona um elemento de automação visual usando estritamente padrões UIA corporativos
            /// (Invoke, Toggle, Expand, Selection) sem usar interações físicas de mouse.
            /// O mouse físico só é empregado como último recurso absoluto de segurança.
            /// </summary>
            /// <param name="el">O elemento visual a ser disparado.</param>
        //--------------------------------------------------------------------------------------
        public static void AcionarElementoSemMouse(AutomationElement el){
            if (el == null) return;

            // 1. Invoke Pattern (botão padrão, menu item)
            try {
                if (el.Patterns.Invoke.IsSupported){
                    Logger.Log("    [UIA] Acionando via Invoke Pattern.");
                    el.Patterns.Invoke.Pattern.Invoke();
                    return;
                }
            } catch { }

            // 2. Toggle Pattern (RibbonToggleButton, como Integradores)
            try {
                if (el.Patterns.Toggle.IsSupported){
                    Logger.Log("    [UIA] Acionando via Toggle Pattern.");
                    el.Patterns.Toggle.Pattern.Toggle();
                    return;
                }
            } catch { }

            // 3. ExpandCollapse Pattern (menus que expandem)
            try {
                if (el.Patterns.ExpandCollapse.IsSupported){
                    Logger.Log("    [UIA] Acionando via ExpandCollapse Pattern.");
                    el.Patterns.ExpandCollapse.Pattern.Expand();
                    return;
                }
            } catch { }

            // 4. SelectionItem Pattern (itens de lista/menu)
            try {
                if (el.Patterns.SelectionItem.IsSupported){
                    Logger.Log("    [UIA] Acionando via SelectionItem Pattern.");
                    el.Patterns.SelectionItem.Pattern.Select();
                    return;
                }
            } catch { }

            // 5. Focus + SPACE (simula ação sem mouse)
            try {
                Logger.Log("    [UIA] Nenhum Pattern suportado. Tentando Focus + SPACE...");
                el.Focus();
                EsperarUiRespirar(200);
                Keyboard.Type(VirtualKeyShort.SPACE);
                return;
            } catch { }

            // 6. Último recurso: mouse
            Logger.Log("    [FALLBACK] Usando clique de mouse como último recurso.", LogLevel.Warn);
            try { el.Click(); } catch { }
        }
    }
}
