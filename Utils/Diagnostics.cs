using System;
using System.Diagnostics;
using System.Linq;
using PromobAutomacao.Utils;
using FlaUI.Core.AutomationElements;

namespace PromobAutomacao.Utils{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Classe utilitária para diagnósticos, medições de desempenho e análise estrutural 
        /// de elementos de interface gráfica (UI) na árvore de automação.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class Diagnostics{
        
        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Executa uma ação delegada e mede o tempo exato de sua execução em milissegundos, logando o resultado obtido.
            /// </summary>
            /// <param name="nome">Nome descritivo da operação para identificação no log de tempos.</param>
            /// <param name="acao">O bloco de código ou método delegado a ser executado e monitorado.</param>
        //--------------------------------------------------------------------------------------
        public static void Medir(string nome, Action acao){
            var sw = Stopwatch.StartNew();
            try { 
                acao(); 
            }
            finally { 
                AppLogs.LogTempoOperacao(nome, sw.ElapsedMilliseconds); 
            }
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Escaneia, agrupa e lista no console todos os elementos visuais mapeados do processo ativo do Promob,
            /// ajudando o desenvolvedor a rastrear e descobrir novos AutomationIDs e propriedades de componentes em tela.
            /// </summary>
            /// <param name="janela">A janela principal do Promob por onde iniciar a busca estrutural.</param>
            /// <param name="automationIdHost">O AutomationID do painel host para focar a análise de elementos internos.</param>
        //--------------------------------------------------------------------------------------
        public static void ListarBotoesProject(Window janela, string automationIdHost){
            var processId = janela.Properties.ProcessId.ValueOrDefault;
            AppLogs.LogAnalisandoEstruturaProcesso(processId);

            // Busca todas as janelas do Windows que pertencem especificamente a este ProcessID do Promob.
            // Popups ou assistentes nativos às vezes flutuam como janelas de topo órfãs no Desktop.
            var janelasDoProcesso = janela.Automation.GetDesktop().FindAllChildren()
                .Where(x => x.Properties.ProcessId.ValueOrDefault == processId)
                .ToList();

            if (janelasDoProcesso.Count > 1){
                AppLogs.LogJanelasEncontradasProcesso(janelasDoProcesso.Count);
                foreach (var j in janelasDoProcesso)
                    AppLogs.LogDetalheJanelaProcesso(j.Name, j.Properties.AutomationId.ValueOrDefault);
            }

            // Tenta localizar o container WPF principal ("elementHost1")
            var host = janela.FindFirstDescendant(automationIdHost);
            if (host != null){
                AppLogs.LogElementHostEncontrado();
                
                // Mágica do LINQ:
                // 1. Filtra elementos válidos (que tenham Name ou AutomationId preenchido).
                // 2. Agrupa-os combinando "ID|Nome" para remover duplicados de layout (ex: células repetidas).
                // 3. Seleciona o primeiro item de cada grupo exclusivo para gerar a listagem limpa.
                var items = host.FindAllDescendants()
                    .Where(e => !string.IsNullOrEmpty(e.Name) || !string.IsNullOrEmpty(e.Properties.AutomationId.ValueOrDefault))
                    .GroupBy(e => (e.Properties.AutomationId.ValueOrDefault ?? "") + "|" + (e.Name ?? ""))
                    .Select(g => g.First())
                    .ToList();

                foreach (var e in items)
                    AppLogs.LogDetalheElementoUI(e.ControlType.ToString(), e.Name, e.Properties.AutomationId.ValueOrDefault);
            }
            else{
                AppLogs.LogElementHostNaoEncontrado();
                
                // Fallback: Varre a janela inteira, agrupando duplicados para não poluir o console,
                // e limita o relatório a no máximo 120 elementos para manter o log legível.
                var all = janela.FindAllDescendants()
                    .Where(e => !string.IsNullOrEmpty(e.Name) || !string.IsNullOrEmpty(e.Properties.AutomationId.ValueOrDefault))
                    .GroupBy(e => (e.Properties.AutomationId.ValueOrDefault ?? "") + "|" + (e.Name ?? ""))
                    .Select(g => g.First())
                    .Take(120)
                    .ToList();

                foreach (var e in all)
                    AppLogs.LogDetalheElementoUI(e.ControlType.ToString(), e.Name, e.Properties.AutomationId.ValueOrDefault);
            }

            AppLogs.LogDivisor();
        }

        

    }
}
