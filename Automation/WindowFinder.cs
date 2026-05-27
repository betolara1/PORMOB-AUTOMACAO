using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AutomacaoPromobTeste.Utils;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;

namespace AutomacaoPromobTeste.Automation{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Classe utilitária responsável pela busca otimizada de elementos visuais do Windows.
        /// Utiliza um padrão de busca em largura (BFS) híbrido acoplado a um fallback nativo profunda para máxima performance.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class WindowFinder{

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Cache do elemento principal da interface gráfica (Host) para evitar buscas redundantes e pesadas.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public static AutomationElement? CachedHost;

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Obtém o Host principal da janela de forma otimizada utilizando cache. 
            /// Se o Host não estiver cacheado, realiza a busca física e o armazena em memória.
            /// </summary>
            /// <param name="janela">A janela principal do aplicativo (Promob).</param>
            /// <param name="automationIdHost">O AutomationId que identifica o container/host principal.</param>
            /// <param name="processId">O ID do processo da janela para garantir o isolamento da busca.</param>
            /// <returns>O elemento host localizado ou a própria janela principal como fallback.</returns>
        //--------------------------------------------------------------------------------------
        public static AutomationElement ObterHostOuJanela(Window janela, string automationIdHost, int? processId){
            
            // Se o elemento em cache ainda for válido e estiver disponível na tela, retorna ele imediatamente
            if (InteractionHelper.ElementoValido(CachedHost))
                return CachedHost!;

            var swHost = Stopwatch.StartNew();

            // Sendo o elementHost1 (o painel WPF principal), ele sempre fica nos níveis superficiais (1 ou 2) da janela.
            // Para evitar a pesada e extremamente demorada varredura profunda do UIA (FindFirstDescendant) na inicialização do Promob,
            // faremos apenas uma busca rápida nas redondezas superficiais (nível 2 no máximo).
            try{
                var filhosSuperficiais = BuscarAteNivel(janela, maxNivel: 2);
                CachedHost = filhosSuperficiais.FirstOrDefault(e => 
                    (e.Properties.AutomationId.ValueOrDefault ?? "").Equals(automationIdHost, StringComparison.OrdinalIgnoreCase) &&
                    (!processId.HasValue || e.Properties.ProcessId.ValueOrDefault == processId.Value));
            }
            catch (Exception ex){
                AppLogs.LogWindowFinderHostSearchFailure(ex.Message);
            }

            swHost.Stop();

            return CachedHost ?? janela;
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Busca um elemento na árvore do Windows utilizando uma estratégia híbrida resiliente em duas fases.
            /// Fase 1: Busca em largura rápida e superficial (Rasa) até o nível 4.
            /// Fase 2: Busca profunda tradicional utilizando a engine nativa de automação do Windows (UIA).
            /// </summary>
            /// <param name="raiz">O elemento raiz por onde a busca deve ser iniciada.</param>
            /// <param name="buscaPrincipal">A expressão lambda contendo a condição nativa de busca do FlaUI.</param>
            /// <param name="filtroFallback">O predicado de validação anônimo em LINQ utilizado na busca rasa.</param>
            /// <param name="limitarAoMesmoProcesso">Indica se a busca deve ignorar elementos de outros processos.</param>
            /// <param name="processId">O ID do processo do aplicativo alvo para filtrar elementos.</param>
            /// <returns>O <see cref="AutomationElement"/> correspondente encontrado, ou <c>null</c> se expirar o tempo limite.</returns>
        //--------------------------------------------------------------------------------------
        public static AutomationElement? BuscarElementoComFallback(
            
            AutomationElement raiz,
            Func<ConditionFactory, ConditionBase> buscaPrincipal,
            Func<AutomationElement, bool>? filtroFallback = null,
            bool limitarAoMesmoProcesso = false,
            int? processId = null){
            
            // ==========================================
            // FASE 1: Varredura Rasa (BFS-like)
            // ==========================================
            // Muito rápida porque analisa apenas os primeiros níveis de filhos (até nível 4), ideal para menus e botões estruturais.
            var swFase1 = Stopwatch.StartNew();
            
            if (filtroFallback != null){
                var todos = BuscarAteNivel(raiz, maxNivel: 4);
                var consulta = todos.AsEnumerable();

                // Filtra para garantir que o elemento pertence exatamente ao mesmo processo do Promob ativo
                if (limitarAoMesmoProcesso && processId.HasValue){
                    consulta = consulta.Where(e =>{
                        try { return e.Properties.ProcessId.ValueOrDefault == processId.Value; }
                        catch { return false; }
                    });
                }

                var resultado = consulta.FirstOrDefault(filtroFallback);
                swFase1.Stop();

                if (resultado != null){
                    AppLogs.LogWindowFinderRasaSuccess(swFase1.ElapsedMilliseconds);
                    return resultado;
                }
            }

            // ==========================================
            // FASE 2: Busca Profunda (UIA Nativo)
            // ==========================================
            // Ativada como Plano B. A varredura rasa falhou ou não pôde ser utilizada.
            // Ela varre toda a estrutura em profundidade. É mais demorada, mas é 100% precisa.
            
            var swFase2 = Stopwatch.StartNew();
            var direto = raiz.FindFirstDescendant(buscaPrincipal);
            swFase2.Stop();

            if (direto != null){
                return direto;
            }

            return null;
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Percorre a árvore de elementos de forma recursiva até um nível determinado (Depth Limit),
            /// gerando os elementos de forma preguiçosa (Lazy) via <c>yield return</c>.
            /// </summary>
            /// <param name="raiz">O elemento pai de partida.</param>
            /// <param name="maxNivel">O nível de profundidade máximo permitido (limite de níveis na árvore).</param>
            /// <param name="nivelAtual">O nível atual controlado pela recursividade.</param>
            /// <returns>Uma coleção contendo os elementos filhos localizados.</returns>
        //--------------------------------------------------------------------------------------
        public static IEnumerable<AutomationElement> BuscarAteNivel(AutomationElement raiz, int maxNivel, int nivelAtual = 0){
            
            // Condição de parada da recursividade
            if (nivelAtual > maxNivel)
                yield break;

            AutomationElement[] filhos;
            try { 
                filhos = raiz.FindAllChildren(); 
            }
            catch { 
                yield break; 
            }

            foreach (var filho in filhos){
                yield return filho; // Retorna o filho direto
                
                // Entra recursivamente nos descendentes incrementando o nível atual
                foreach (var desc in BuscarAteNivel(filho, maxNivel, nivelAtual + 1))
                    yield return desc;
            }
        }
    }
}
