using System;
using System.Linq;
using AutomacaoPromobTeste.Automation;
using AutomacaoPromobTeste.Utils;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace AutomacaoPromobTeste.Promob{
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

            if (promobProc != null)
                CachedProcessIdPromob = promobProc.Id;

            // Executa buscas periódicas até que a janela do Promob seja instanciada ou ocorra timeout
            InteractionHelper.EsperarAte(() =>{
                var desktop = automation.GetDesktop();
                var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

                foreach (var j in janelas){
                    if (promobProc != null){
                        // Busca precisa: valida se a janela pertence exatamente ao Process ID do Promob que identificamos
                        if (j.Properties.ProcessId.ValueOrDefault == promobProc.Id){
                            var name = j.Name ?? "";
                            if (EhJanelaPromob(name) || name.Contains("Promob", StringComparison.OrdinalIgnoreCase)){
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

            if (encontrada != null)
                Logger.Log($"  [OK] Janela encontrada (PID: {CachedProcessIdPromob}): '{encontrada.Name}'");

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
            
            if (string.IsNullOrWhiteSpace(nome))
                return false;

            // Filtro de segurança para evitar falsos positivos se o desenvolvedor estiver com o VS Code aberto trabalhando neste projeto
            if (nome.Contains("Visual Studio Code", StringComparison.OrdinalIgnoreCase) ||
                nome.Contains("VS Code", StringComparison.OrdinalIgnoreCase))
                return false;

            return nome.Contains("- Promob Studio", StringComparison.OrdinalIgnoreCase) ||
                   nome.Contains("Promob Studio Bartz", StringComparison.OrdinalIgnoreCase);
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

            // Tentativa 1: Procurar como janela órfã no Desktop
            var wizardDesktop = desktop.FindFirstChild(cf => cf.ByName(PromobConfig.NomeJanelaWizardImportacao));
            if (wizardDesktop != null)
                return wizardDesktop.AsWindow();

            // Tentativa 2: Procurar dentro da hierarquia da janela principal
            var wizardDesc = janelaPrincipal.FindFirstDescendant(cf => cf.ByName(PromobConfig.NomeJanelaWizardImportacao));
            return wizardDesc?.AsWindow();
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
    }
}
