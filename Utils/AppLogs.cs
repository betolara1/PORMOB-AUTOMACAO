using System;

namespace AutomacaoPromobTeste.Utils{
    //--------------------------------------------------------------------------------------
    /// <summary>
    /// Classe utilitária estática centralizadora que encapsula todas as mensagens de log
    /// da aplicação. Garante que nenhuma string de log ou nível de severidade fique
    /// espalhado pelo código de lógica de negócios.
    /// </summary>
    //--------------------------------------------------------------------------------------
    public static class AppLogs{

        // ==================================================================================
        // --- 1. UTILS: NATIVE CLIPBOARD ---
        // ==================================================================================

        public static void LogClipboardReadFailure(string detail){
            //Logger.Log($"  [AVISO] Falha ao ler do clipboard: {detail}", LogLevel.Debug);
        }

        public static void LogClipboardCopiedWin32(){
            //Logger.Log("  [OK] Caminho copiado para o Clipboard (Win32).", LogLevel.Debug);
        }

        public static void LogClipboardNativeFallback(string detail){
            //Logger.Log($"Falha no clipboard nativo: {detail}. Tentando PowerShell...", LogLevel.Debug);
        }

        public static void LogClipboardCopiedPowerShell(){
            //Logger.Log("  [OK] Caminho copiado via PowerShell.", LogLevel.Debug);
        }

        public static void LogClipboardCopyFailure(string detail){
            //Logger.Log($"Falha ao copiar para Clipboard: {detail}", LogLevel.Debug);
        }


        // ==================================================================================
        // --- 2. UTILS: DIAGNOSTICS ---
        // ==================================================================================

        public static void LogTempoOperacao(string nome, long ms){
            //Logger.Log($"  [TEMPO] {nome}: {ms} ms", LogLevel.Debug);
        }

        public static void LogAnalisandoEstruturaProcesso(int processId){
            //Logger.Log($"Analisando estrutura para Processo: {processId}", LogLevel.Debug);
        }

        public static void LogJanelasEncontradasProcesso(int count){
            //Logger.Log($"Encontradas {count} janelas para este processo:", LogLevel.Debug);
        }

        public static void LogDetalheJanelaProcesso(string? name, string? automationId){
            //Logger.Log($"  - Window: '{name}' (ID: {automationId})", LogLevel.Debug);
        }

        public static void LogElementHostEncontrado(){
            //Logger.Log("'elementHost1' encontrado! Escaneando conteúdo profundo...", LogLevel.Debug);
        }

        public static void LogDetalheElementoUI(string? controlType, string? name, string? automationId){
            //Logger.Log($"  -> Tipo: {controlType}, Nome: '{name}', Id: '{automationId}'", LogLevel.Debug);
        }

        public static void LogElementHostNaoEncontrado(){
            //Logger.Log("elementHost1 não encontrado. Escaneando janela toda...", LogLevel.Debug);
        }

        public static void LogDivisor(){
            //Logger.Log("------------------------------------------", LogLevel.Debug);
        }


        // ==================================================================================
        // --- 3. AUTOMATION: WINDOW FINDER ---
        // ==================================================================================

        public static void LogWindowFinderHostSearchFailure(string detail){
            //Logger.Log($"    [DEBUG] Falha na busca superficial do host: {detail}", LogLevel.Debug);
        }

        public static void LogWindowFinderRasaSuccess(long ms){
            //Logger.Log($"      [PERF] Varredura Rasa encontrou o elemento em {ms}ms.", LogLevel.Debug);
        }


        // ==================================================================================
        // --- 4. AUTOMATION: INTERACTION HELPER ---
        // ==================================================================================

        public static void LogInteractionHelperUiaInvoke(){
            //Logger.Log("    [UIA] Acionando via Invoke Pattern.");
        }

        public static void LogInteractionHelperUiaToggle(){
            //Logger.Log("    [UIA] Acionando via Toggle Pattern.");
        }

        public static void LogInteractionHelperUiaExpandCollapse(){
            //Logger.Log("    [UIA] Acionando via ExpandCollapse Pattern.");
        }

        public static void LogInteractionHelperUiaSelectionItem(){
            //Logger.Log("    [UIA] Acionando via SelectionItem Pattern.");
        }

        public static void LogInteractionHelperUiaNoPatternFocusSpace(){
            //Logger.Log("    [UIA] Nenhum Pattern suportado. Tentando Focus + SPACE...");
        }

        public static void LogInteractionHelperFallbackMouseClick(){
            //Logger.Log("    [FALLBACK] Usando clique de mouse como último recurso.", LogLevel.Warn);
        }


        // ==================================================================================
        // --- 5. PROMOB: PROMOB WINDOW HELPER ---
        // ==================================================================================

        public static void LogWizardEncontradoDesktop(string name){
            //Logger.Log($"Janela Wizard localizada no Desktop ({name}).", LogLevel.Debug);
        }

        public static void LogWizardSearchDesktopError(string detail){
            //Logger.Log($"    [DEBUG] Erro ao buscar wizard no Desktop: {detail}", LogLevel.Debug);
        }

        public static void LogWizardEncontradoJanelaPrincipal(string name){
            //Logger.Log($"Janela Wizard localizada sob a Janela Principal ({name}).", LogLevel.Debug);
        }

        public static void LogWizardSearchMainWindowError(string detail){
            //Logger.Log($"    [DEBUG] Erro ao buscar wizard na janela principal: {detail}", LogLevel.Debug);
        }


        // ==================================================================================
        // --- 6. PROMOB: PROMOB RECUPERACAO ---
        // ==================================================================================

        public static void LogRecoveryIniciando(){
            Logger.Log("  [INFO] Executando rotina de recuperação...");
        }

        public static void LogRecoveryPopupCancelamentoDetectado(string btnNao){
            Logger.Log($"    [RECOVERY] Popup de cancelamento detectado. Clicando em '{btnNao}' para manter a aplicação aberta.");
        }

        public static void LogRecoveryFalhou(string detail){
            Logger.Log($"  [AVISO] Recuperação falhou: {detail}", LogLevel.Warn);
        }

        public static void LogRecoveryTentandoFecharProjeto(){
            Logger.Log("  [RECOVERY] Tentando fechar projeto atual de forma segura...");
        }

        public static void LogRecoveryFechandoProjetoUia(){
            Logger.Log("    [RECOVERY] Fechando projeto via UIA Pattern (Background)...");
        }

        public static void LogRecoveryFallbackTecladoFecharProjeto(){
            Logger.Log("    [RECOVERY] Fallback de teclado para fechar projeto...");
        }

        public static void LogRecoveryTratandoPopupSalvamento(){
            Logger.Log("    [RECOVERY] Tratando popup de salvamento...");
        }


        // ==================================================================================
        // --- 7. PROMOB: PROMOB IMPORTADOR ---
        // ==================================================================================

        public static void LogImportadorProcurandoBotao(int tentativas){
            //Logger.Log($"  [INFO] Procurando botão 'Importar' (Tentativa {tentativas})...");
        }

        public static void LogImportadorIniciandoBusca(){
            //Logger.Log("    [SEARCH] Iniciando busca persistente do botão 'Importar Projeto'...");
        }

        public static void LogImportadorBotaoLocalizado(long ms){
            //Logger.Log($"    [OK] Botão localizado em {ms}ms.");
        }

        public static void LogImportadorBotaoNaoEncontrado(long ms){
            //Logger.Log($"    [AVISO] Botão não encontrado após {ms}ms.");
        }

        public static void LogImportadorAguardandoEstabilizacao(){
            //Logger.Log("    [INFO] Botão 'Importar' encontrado. Aguardando 2 segundos para estabilização antes de clicar...");
        }

        public static void LogImportadorClicandoBotao(){
            //Logger.Log("    [ACTION] Clicando no botão 'Importar'...");
        }

        public static void LogImportadorCliqueSucesso(long ms){
            //Logger.Log($"  [SUCESSO] Clique executado com sucesso (Tempo total: {ms}ms).");
        }

        public static void LogImportadorTentativaFalhou(int tentativas, long ms){
            //Logger.Log($"  [AVISO] Tentativa {tentativas} falhou ({ms}ms). Aguardando 5s...", LogLevel.Warn);
        }

        public static void LogImportadorErroBusca(int tentativas, string detail){
            //Logger.Log($"  [AVISO] Erro na busca do botão 'Importar' na tentativa {tentativas}: {detail}. Aguardando 5s...", LogLevel.Warn);
        }

        public static void LogImportadorAnalisandoEstruturaWizard(){
            //Logger.Log("  [INFO] Analisando estrutura visual do wizard de importação...");
        }

        public static void LogImportadorWizardCorreto(long ms, string? btnName, string? btnId){
            //Logger.Log($"  [OK] Wizard correto detectado in {ms}ms: botão '{btnName}' (ID: '{btnId}') encontrado.");
        }

        public static void LogImportadorWizardIncorreto(long ms){
            Logger.Log($"  [AVISO] Wizard INCORRETO detectado em {ms}ms: botão '...' não encontrado. Iniciando sequência de recomeço.", LogLevel.Warn);
        }

        public static void LogImportadorErroVerificacaoWizard(string detail){
            Logger.Log($"  [AVISO] Erro ao verificar wizard: {detail}. Assumindo wizard incorreto.", LogLevel.Warn);
        }

        public static void LogImportadorFechandoWizardIncorreto(){
            //Logger.Log("  [INFO] Fechando wizard incorreto para tentar novamente...");
        }

        public static void LogImportadorClicandoCancelarWizard(){
            //Logger.Log("    [ACTION] Clicando em 'Cancelar' para fechar o wizard...");
        }

        public static void LogImportadorWizardFechadoCancelar(){
            //Logger.Log("    [OK] Wizard fechado via 'Cancelar'.");
        }

        public static void LogImportadorBotaoCancelarNaoEncontrado(){
            //Logger.Log("    [AVISO] Botão 'Cancelar' não encontrado superficialmente. Usando ESC...", LogLevel.Warn);
        }

        public static void LogImportadorWizardFechadoEsc(){
            //Logger.Log("    [OK] Wizard fechado via ESC.");
        }

        public static void LogImportadorErroFecharWizard(string detail){
            //Logger.Log($"    [AVISO] Erro ao fechar wizard: {detail}. Tentando ESC como último recurso...", LogLevel.Warn);
        }

        public static void LogImportadorBotaoBuscaEncontrado(string name){
            //Logger.Log($"  [OK] Botão de busca encontrado: {name}");
        }

        public static void LogImportadorBotaoBuscaNaoEncontrado(){
            //Logger.Log("  [AVISO] Botão de busca não encontrado. Usando TAB + SPACE...", LogLevel.Warn);
        }

        public static void LogImportadorDialogoEncontrado(string name){
            //Logger.Log($"  [OK] Diálogo encontrado: {name}");
        }

        public static void LogImportadorPreenchendoCaminhoUia(string caminhoCompleto){
            //Logger.Log($"  [INFO] Preenchendo caminho completo via UIA: {caminhoCompleto}");
        }

        public static void LogImportadorCampoNomeEncontrado(string id, FlaUI.Core.Definitions.ControlType controlType){
            //Logger.Log($"  [INFO] Campo 'Nome' encontrado (Id: {id}, Tipo: {controlType}).");
        }

        public static void LogImportadorUsandoEditInterno(){
            //Logger.Log("  [INFO] Usando elemento 'Edit' interno do ComboBox para SetValue.");
        }

        public static void LogImportadorValorDefinidoUia(){
            //Logger.Log("  [OK] Valor definido via UIA (SetValue).");
        }

        public static void LogImportadorSetValueFalhou(){
            //Logger.Log("  [INFO] SetValue falhou. Tentando foco + seleção + digitação...");
        }

        public static void LogImportadorAguardandoDigitacao(){
            //Logger.Log("  [INFO] Aguardando campo refletir a digitação...");
        }

        public static void LogImportadorFallbackTecladoFalhou(string detail){
            //Logger.Log($"  [AVISO] Fallback de teclado falhou: {detail}", LogLevel.Warn);
        }

        public static void LogImportadorCampoNomeNaoEncontrado(){
            //Logger.Log("  [AVISO] Campo 'Nome' não encontrado via UIA.", LogLevel.Warn);
        }

        public static void LogImportadorUsandoClipboardFallback(){
            //Logger.Log("  [AVISO] Usando clipboard como último recurso...", LogLevel.Warn);
        }

        public static void LogImportadorAguardandoCtrlV(){
            //Logger.Log("  [INFO] Aguardando o Ctrl+V surtir efeito...");
        }

        public static void LogImportadorClipboardRestaurado(){
            //Logger.Log("  [OK] Clipboard do usuário restaurado.");
        }

        public static void LogImportadorTentativaCliqueAbrir(int tentativa){
            //Logger.Log($"  [INFO] Tentativa {tentativa} de clicar no botão 'Abrir'...");
        }

        public static void LogImportadorTentativaConfirmarEnter(int tentativa){
            //Logger.Log($"  [INFO] Tentativa {tentativa} de confirmar diálogo (ENTER)...");
        }

        public static void LogImportadorAguardandoFechamentoDialogo(){
            //Logger.Log("  [INFO] Aguardando fechamento do diálogo...");
        }

        public static void LogImportadorNotificacaoRoubouFoco(string name){
            Logger.Log($"  [AVISO] Notificação do Promob roubou o foco do clique: '{name}'. Fechando...");
        }

        public static void LogImportadorDialogoNaoFechouAviso(){
            Logger.Log("  [AVISO] Diálogo não fechou após 4s. O clique pode ter sido ignorado.", LogLevel.Warn);
        }

        public static void LogImportadorDialogoFechadoSucesso(){
            //Logger.Log("  [OK] Diálogo de arquivo fechado e projeto selecionado com sucesso.");
        }

        public static void LogImportadorValidandoCamposWizard(){
            //Logger.Log("  [INFO] Validando preenchimento dos campos obrigatórios no Wizard...");
        }

        public static void LogImportadorCampoCaminhoVazio(){
            Logger.Log("  [ERRO] O campo 'Caminho' está vazio no Wizard.", LogLevel.Error);
        }

        public static void LogImportadorCampoCaminhoPreenchido(string valor){
            //Logger.Log($"  [OK] Campo 'Caminho' preenchido: {valor}");
        }

        public static void LogImportadorCampoCaminhoNaoEncontradoVerificacao(){
            //Logger.Log("  [AVISO] Não foi possível encontrar o campo 'Caminho' para validação. Prosseguindo no escuro...", LogLevel.Warn);
        }

        public static void LogImportadorCamposVaziosAvançarAviso(){
            //Logger.Log("  [AVISO] Campos obrigatórios parecem estar vazios. Tentando avançar mesmo assim para ver o erro do Promob...");
        }

        public static void LogImportadorProcurandoAvancar(int tentativa){
            //Logger.Log($"  [INFO] Procurando botão 'Avançar' (Tentativa {tentativa}/3)...");
        }

        public static void LogImportadorBotaoAvancarDesabilitado(){
            Logger.Log("  [ERRO] O botão 'Avançar' está desabilitado. Provavelmente faltam campos obrigatórios.", LogLevel.Error);
        }

        public static void LogImportadorBotaoAvancarClicado(){
            //Logger.Log("  [OK] Botão 'Avançar' clicado.");
        }

        public static void LogImportadorBotaoAvancarNaoEncontrado(){
            //Logger.Log("  [AVISO] Botão 'Avançar' não encontrado. Tentando ENTER...", LogLevel.Warn);
        }

        public static void LogImportadorAnalisandoWizardAposClique(){
            //Logger.Log("  [INFO] Analisando comportamento do Wizard após o clique...");
        }

        public static void LogImportadorPopupCancelamentoInterceptado(string texto, string btnNao){
            Logger.Log($"  [AVISO] Popup de cancelamento interceptado: '{texto}'. Clicando em '{btnNao}'...");
        }

        public static void LogImportadorPopupAtencaoGenerico(string texto){
            //Logger.Log($"  [INFO] Popup de Atenção detectado ('{texto}'). Tratando como informativo (OK/Nao).");
        }

        public static void LogImportadorPromobErroExibido(string texto){
            Logger.Log($"  [ERRO] O Promob exibiu um erro/aviso: '{texto}'", LogLevel.Error);
        }

        public static void LogImportadorRetornandoLoopResolucao(){
            //Logger.Log("  [INFO] Voltando ao loop para tentar resolver campos ou avançar novamente.");
        }

        public static void LogImportadorAguardandoConclusaoImportacao(){
            //Logger.Log("  [INFO] Aguardando conclusão da importação e verificando popups (Timeout: 45s)...");
        }

        public static void LogImportadorWizardFechadoImportacaoConcluida(){
            //Logger.Log("  [OK] Janela do Wizard foi fechada. Importação concluída.");
        }

        public static void LogImportadorWizardInacessivelImportacaoConcluida(){
            //Logger.Log("  [OK] Janela do Wizard não está mais acessível. Importação concluída.");
        }

        public static void LogImportadorPopupDuranteImportacao(string name){
            //Logger.Log($"  [INFO] Popup encontrado durante importação: {name}");
        }

        public static void LogImportadorPopupDuranteImportacaoTexto(string texto){
            //Logger.Log($"  [INFO] Texto do popup: {texto}");
        }

        public static void LogImportadorPopupNovoProjeto(){
            //Logger.Log("  [INFO] O Promob perguntou se deseja importar como novo projeto. Selecionando 'Cancelar'...");
        }

        public static void LogImportadorPopupSubstituirProjeto(){
            //Logger.Log("  [INFO] O Promob perguntou se deseja substituir o projeto existente. Selecionando 'Sim'...");
        }

        public static void LogImportadorClicandoBotaoPopup(string btnName){
            //Logger.Log($"  [ACTION] Clicando no botão '{btnName}' no popup...");
        }

        public static void LogImportadorBotaoConfirmacaoNaoEncontradoPopup(){
            //Logger.Log("  [AVISO] Botão de confirmação não encontrado no popup. Tentando ENTER...", LogLevel.Warn);
        }

        public static void LogImportadorAguardandoEstabilizacaoListaProjetos(){
            //Logger.Log("  [INFO] Aguardando 1.5 segundos para estabilização da lista de projetos...");
        }


        // ==================================================================================
        // --- 8. PROMOB: PROMOB FECHAR PROJETO ---
        // ==================================================================================

        public static void LogFecharProjetoVerificandoEstado(int tentativa, int maxTentativas){
            //Logger.Log($"    -> Verificando estado do Promob (Tentativa {tentativa}/{maxTentativas})...");
        }

        public static void LogFecharProjetoInterfaceAindaNaoRenderizada(){
            //Logger.Log("    [INFO] Interface gráfica (elementHost1) ainda não foi renderizada pelo Promob. Aguardando inicialização...");
        }

        public static void LogFecharProjetoNaoRespondeuOuCarregando(string detail){
            //Logger.Log($"    [INFO] Promob ainda não respondeu ou está carregando ({detail}). Continuando busca...", LogLevel.Debug);
        }

        public static void LogFecharProjetoPopupSalvarDetectado(string name){
            //Logger.Log($"    [INFO] Popup '{name}' detectado. Buscando botão 'Não'...");
        }

        public static void LogFecharProjetoBotaoNaoLocalizadoClicando(){
            //Logger.Log("    [OK] Botão 'Não' localizado. Clicando...");
        }

        public static void LogFecharProjetoBotaoNaoNaoEncontradoTeclado(string name){
            //Logger.Log($"    [AVISO] Botão 'Não' não encontrado na árvore de '{name}'. Enviando Alt+N via teclado...", LogLevel.Warn);
        }

        public static void LogFecharProjetoDisparandoAltNPreventivo(){
            //Logger.Log("    [DEBUG] Nenhum popup UIA detectado. Disparando Alt+N preventivo...", LogLevel.Debug);
        }

        public static void LogFecharProjetoTimeoutImportarNaoDetectado(){
            Logger.Log("    [AVISO] Timeout de 60s atingido e botão 'Importar' não foi detectado. O Promob pode estar travado.", LogLevel.Warn);
        }

        public static void LogFecharProjetoConcluido(long ms){
            //Logger.Log($"  [SUCESSO] Sequência de fechamento concluída em {ms}ms.");
        }


        // ==================================================================================
        // --- 9. PROMOB: PROMOB EXPORTADOR ERP ---
        // ==================================================================================

        public static void LogExportadorProcurandoAbaFerramentas(){
            //Logger.Log("  [INFO] Procurando aba 'Ferramentas'...");
        }

        public static void LogExportadorAbaFerramentasEncontrada(){
            //Logger.Log("  [OK] Aba 'Ferramentas' encontrada. Selecionando...");
        }

        public static void LogExportadorAbaFerramentasNaoEncontrada(){
            //Logger.Log("  [AVISO] Aba 'Ferramentas' não encontrada.", LogLevel.Warn);
        }

        public static void LogExportadorProcurandoBotaoIntegradores(){
            //Logger.Log("  [INFO] Procurando botão 'Integradores'...");
        }

        public static void LogExportadorBotaoIntegradoresEncontrado(){
            //Logger.Log("  [OK] Botão 'Integradores' encontrado. Acionando via UIA (sem mouse)...");
        }

        public static void LogExportadorAguardandoDropdown(){
            //Logger.Log("  [INFO] Aguardando menu dropdown e procurando 'Promob ERP'...");
        }

        public static void LogExportadorOpcaoErpEncontrada(FlaUI.Core.Definitions.ControlType controlType){
            //Logger.Log($"  [OK] Opção 'Promob ERP' encontrada (Tipo: {controlType}). Acionando via UIA (sem mouse)...");
        }

        public static void LogExportadorOpcaoErpNaoEncontrada(){
            Logger.Log("  [ERRO] Opção 'Promob ERP' não encontrada no menu dropdown.", LogLevel.Error);
        }

        public static void LogExportadorBotaoIntegradoresNaoEncontrado(){
            //Logger.Log("  [AVISO] Botão 'Integradores' não encontrado.", LogLevel.Warn);
        }

        public static void LogExportadorAguardandoExportacaoFinalizar(){
            //Logger.Log("  [INFO] Aguardando a exportação do Promob ERP finalizar (timeout: 35min)...");
        }

        public static void LogExportadorEmAndamento(long s){
            //Logger.Log($"    [AGUARDE] Exportação em andamento... ({s}s decorridos)");
        }

        public static void LogExportadorErroDetectado(string detail){
            Logger.Log($"  [ERRO] Mensagem de erro detectada: '{detail}'", LogLevel.Error);
        }

        public static void LogExportadorTimeoutSemResultado(){
            Logger.Log("  [ERRO] Timeout de 35 minutos atingido sem detectar resultado. Exportação pode ter falhado.", LogLevel.Error);
        }

        public static void LogExportadorProcurandoBotaoFechar(){
            //Logger.Log("  [INFO] Procurando botão 'Fechar' na janela de exportação...");
        }

        public static void LogExportadorBotaoFecharClicando(){
            //Logger.Log("  [OK] Botão 'Fechar' encontrado e habilitado. Clicando...");
        }

        public static void LogExportadorBotaoFecharNaoEncontradoAltF4(){
            //Logger.Log("  [AVISO] Botão 'Fechar' não encontrado. Tentando ALT+F4...", LogLevel.Warn);
        }

        public static void LogExportadorAbortadaErro(){
            Logger.Log("  [ERRO] Exportação ERP abortada com erro. Sinalizando para mover arquivo e continuar.", LogLevel.Error);
        }

        public static void LogExportadorCompletadoSucesso(long s){
            //Logger.Log($"  [SUCESSO] Mensagem 'completado com sucesso' detectada após {s}s!");
        }

        public static void LogExportadorProcurandoExplorer(){
            //Logger.Log("  [INFO] Procurando janela do Explorer (pasta 01_XML) para fechar...");
        }

        public static void LogExportadorExplorerEncontradoFechando(string name){
            //Logger.Log($"  [OK] Janela do Explorer encontrada: '{name}'. Fechando...");
        }

        public static void LogExportadorExplorerNaoDetectado(){
            //Logger.Log("  [AVISO] Janela do Explorer com '01_XML' não foi detectada. Prosseguindo...", LogLevel.Warn);
        }

        public static void LogExportadorRetornandoFoco(){
            //Logger.Log("  [INFO] Retornando foco para o Promob...");
        }

        public static void LogExportadorConcluidaSucesso(long s){
            //Logger.Log($"  [SUCESSO] Exportação ERP concluída em {s}s.");
        }


        // ==================================================================================
        public static void LogCarregadorLocalizandoPrimeiroProjetoRecentes(){
            //Logger.Log("  [INFO] Localizando o primeiro projeto da lista de recentes...");
        }

        public static void LogCarregadorPrimeiroProjetoLocalizado(string name){
            //Logger.Log($"  [OK] Primeiro projeto localizado na lista: '{name}'. Procurando botão 'Abrir projeto'...");
        }

        public static void LogCarregadorBotaoAbrirEncontrado(int i){
            //Logger.Log($"  [OK] Botão 'Abrir projeto' encontrado na tentativa {i}. Clicando...");
        }

        public static void LogCarregadorBotaoAbrirNaoEncontradoTentativa(int i){
            //Logger.Log($"  [AVISO] Botão 'Abrir projeto' não encontrado na tentativa {i}. Aguardando 5s...", LogLevel.Warn);
        }

        public static void LogCarregadorBotaoAbrirNaoEncontradoDuploClique(){
            //Logger.Log("  [AVISO] Botão 'Abrir projeto' não encontrado após 3 tentativas. Executando duplo clique no item do projeto...", LogLevel.Warn);
        }

        public static void LogCarregadorListItemNaoEncontradoGenerico(){
            //Logger.Log("  [AVISO] Nenhum item de projeto (ListItem/DataItem) encontrado na tela. Tentando botão de abrir genérico...", LogLevel.Warn);
        }

        public static void LogCarregadorBotaoAbrirGenericoClicado(){
            //Logger.Log("  [OK] Botão de abrir genérico encontrado. Clicando...");
        }

        public static void LogCarregadorFormaAbrirNaoEncontradaEnter(){
            //Logger.Log("  [AVISO] Nenhuma forma de abrir encontrada. Tentando ENTER...", LogLevel.Warn);
        }

        public static void LogCarregadorAguardandoCarregamentoProjeto(int tentativa, int timeoutS){
            //Logger.Log($"  [INFO] Aguardando o carregamento do projeto (Tentativa {tentativa}, timeout: {timeoutS}s)...");
        }

        public static void LogCarregadorIniciandoCicloVerificacao(){
            //Logger.Log("    [DEBUG] Iniciando ciclo de verificação UI...", LogLevel.Debug);
        }

        public static void LogCarregadorPopupApenasLeituraDetectado(){
            Logger.Log("[AVISO-POPUP] Popup 'Aviso (Apenas Leitura / Sem Conexão)' detectado preventivamente. Fechando popup...", LogLevel.Warn);
        }

        public static void LogCarregadorPopupClicandoOk(){
            //Logger.Log("[AVISO-POPUP] Clicando no botão OK.");
        }

        public static void LogCarregadorPopupClicandoPrimeiroBotao(){
            //Logger.Log("[AVISO-POPUP] Clicando no primeiro botão encontrado.");
        }

        public static void LogCarregadorPopupEnviandoAltF4(){
            //Logger.Log("[AVISO-POPUP] Enviando ALT+F4...");
        }

        public static void LogCarregadorErroPopupAviso(string detail){
            //Logger.Log($"[DEBUG] Erro na verificação preventiva de popup 'Aviso': {detail}", LogLevel.Debug);
        }

        public static void LogCarregadorProcurandoAbaFerramentas(){
            //Logger.Log("      -> Procurando aba 'Ferramentas' (TabItem)...");
        }

        public static void LogCarregadorAbaFerramentasEncontrada(long ms){
            //Logger.Log($"      [OK] Aba encontrada em {ms}ms.");
        }

        public static void LogCarregadorAbaFerramentasNaoVisivel(long ms){
            //Logger.Log($"      [AGUARDE] Aba não visível após {ms}ms.");
        }

        public static void LogCarregadorVerificandoMensagemCarregando(){
            //Logger.Log("      -> Verificando mensagem de carregamento (Text/Label)...");
        }

        public static void LogCarregadorModulosCarregando(long ms){
            //Logger.Log($"      [LOADING] Módulos carregando ({ms}ms).");
        }

        public static void LogCarregadorSemMensagemCarregando(long ms){
            //Logger.Log($"      [READY] Sem mensagem de carregamento ({ms}ms).");
        }

        public static void LogCarregadorProcurandoPopupsBloqueio(){
            //Logger.Log("    [INFO] Procurando popups de bloqueio...");
        }

        public static void LogCarregadorPopupTratado(string name, long ms){
            //Logger.Log($"    [AVISO] Popup '{name}' tratado ({ms}ms).");
        }

        public static void LogCarregadorSemPopupsDetectados(long ms){
            //Logger.Log($"    [INFO] Sem popups detectados em {ms}ms.");
        }

        public static void LogCarregadorCondicoesConcluidas(){
            //Logger.Log("    [SUCESSO] Condições de carregamento concluídas.");
        }

        public static void LogCarregadorCicloFinalizado(long ms){
            //Logger.Log($"    [DEBUG] Ciclo finalizado em {ms}ms total.", LogLevel.Debug);
        }

        public static void LogCarregadorProjetoCarregadoSucesso(){
            //Logger.Log("  [OK] Projeto carregado e validado com sucesso.");
        }

        public static void LogCarregadorTimeoutCarregamentoUia(int timeoutS){
            Logger.Log($"  [AVISO] Timeout de {timeoutS}s atingido sem concluir o carregamento por UIA.", LogLevel.Warn);
        }

        public static void LogCarregadorVisionIniciando(){
            //Logger.Log("  [VISION] Iniciando verificação visual (IA) como fallback final para este ciclo...");
        }

        public static void LogCarregadorVisionPronta(){
            //Logger.Log("  [VISION] IA detectou que a tela parece estar pronta (carregada). Prosseguindo.");
        }

        public static void LogCarregadorVisionInconsistente(){
            //Logger.Log("  [VISION] IA confirmou que o projeto ainda parece estar carregando ou em estado inconsistente.");
        }

        public static void LogCarregadorReiniciandoVerificacao(){
            //Logger.Log("  [INFO] Reiniciando verificação para novo ciclo de 10s...");
        }

        public static void LogCarregadorTratandoPopup(string name){
            //Logger.Log($"  [INFO] Tratando popup: '{name}'");
        }

        public static void LogCarregadorClicandoOkPopup(string btnName){
            //Logger.Log($"  [OK] Clicando em '{btnName}' no popup.");
        }

        public static void LogCarregadorEnviandoAltF4Popup(){
            //Logger.Log("  [OK] Enviando ALT+F4 para fechar o popup.");
        }


        // ==================================================================================
        // --- 11. PROMOB: PROMOB WORKFLOW ---
        // ==================================================================================

        public static void LogWorkflowLocalizandoJanelaPromob(){
            //Logger.Log("Localizando janela do Promob...", LogLevel.Debug);
        }

        public static void LogWorkflowNovoProcessIdDetectado(){
            //Logger.Log("Novo ProcessId detectado. Invalidando cache de UI.", LogLevel.Debug);
        }

        public static void LogWorkflowVerificandoEstadoInicial(){
            //Logger.Log("Verificando estado inicial do Promob...", LogLevel.Debug);
        }

        public static void LogWorkflowProcurandoJanelaBotoes(){
            //Logger.Log("[INFO] Procurando janela do Promob para listar botões...");
        }

        public static void LogWorkflowPasso1AbrindoAssistente(int tentativa, int maxTentativas){
            Logger.Log($"[PASSO 1/4] Abrindo assistente de importação... (Tentativa {tentativa}/{maxTentativas})");
        }

        public static void LogWorkflowAguardandoWizard(){
            //Logger.Log("Aguardando e verificando wizard de importação...", LogLevel.Debug);
        }

        public static void LogWorkflowOperacaoNaoAutorizada(){
            Logger.Log("[AVISO] Mensagem 'Operação não autorizada' detectada. Fechando popup e reiniciando rotina de importação...", LogLevel.Warn);
        }

        public static void LogWorkflowForcarClientes(){
            //Logger.Log("Clicando na aba 'Clientes' para forçar atualização da UI...", LogLevel.Debug);
        }

        public static void LogWorkflowAbaClientesNaoEncontrada(){
            //Logger.Log("Aba 'Clientes' não encontrada na janela inteira.", LogLevel.Debug);
        }

        public static void LogWorkflowRetornandoProjetos(){
            //Logger.Log("Retornando para a aba 'Projetos'...", LogLevel.Debug);
        }

        public static void LogWorkflowAbaProjetosNaoEncontrada(){
            //Logger.Log("Aba 'Projetos' não encontrada na janela inteira.", LogLevel.Debug);
        }

        public static void LogWorkflowWizardConfirmado(int tentativa){
            //Logger.Log($"Wizard correto confirmado na tentativa {tentativa}.", LogLevel.Debug);
        }

        public static void LogWorkflowWizardIncorreto(int tentativa){
            Logger.Log($"[AVISO] Janela incorreta na tentativa {tentativa}. Fechando e tentando novamente...", LogLevel.Warn);
        }

        public static void LogWorkflowPasso2SelecionandoArquivo(){
            Logger.Log("[PASSO 2/4] Selecionando o arquivo no assistente...");
        }

        public static void LogWorkflowClicandoAvancar(){
            //Logger.Log("Clicando em Avançar no Wizard...", LogLevel.Debug);
        }

        public static void LogWorkflowPasso3Importando(){
            Logger.Log("[PASSO 3/4] Importando o projeto no Promob...");
        }

        public static void LogWorkflowPasso4Abrindo(){
            Logger.Log("[PASSO 4/4] Abrindo o projeto importado no Promob...");
        }

        public static void LogWorkflowFechandoProjeto(){
            Logger.Log("[PROCESSO] Fechando o projeto atual...");
        }

        public static void LogWorkflowSinalFechouProjetoEmitido(){
            //Logger.Log("Sinal FechouProjetoAtual emitido para o monitor de atualização.", LogLevel.Debug);
        }

        public static void LogWorkflowFluxoConcluido(){
            //Logger.Log("Fluxo concluído para este arquivo.", LogLevel.Debug);
        }

        public static void LogWorkflowEscaneandoElementos(int processId){
            //Logger.Log($"[INFO] Escaneando TODOS os elementos da janela inteira para Processo: {processId}", LogLevel.Debug);
        }

        public static void LogWorkflowElementosEncontrados(int count){
            //Logger.Log($"[INFO] Foram encontrados {count} elementos únicos com Nome ou ID na tela.", LogLevel.Debug);
        }


        // ==================================================================================
        // --- 12. PROMOB: PROMOB UPDATER ---
        // ==================================================================================

        public static void LogUpdaterIniciandoRotina(){
            Logger.Log("[ATUALIZAÇÃO] Iniciando rotina de verificação e atualização...");
        }

        public static void LogUpdaterLocalizandoJanela(){
            //Logger.Log("Localizando janela principal do Promob...", LogLevel.Debug);
        }

        public static void LogUpdaterLocalizandoMenuArquivo(){
            //Logger.Log("  [2/4] Localizando menu 'Arquivo'...");
        }

        public static void LogUpdaterVerificandoBotaoVisivel(){
            //Logger.Log("Verificando se o botão 'Atualizar o Promob' já está visível...", LogLevel.Debug);
        }

        public static void LogUpdaterBotaoJaVisivel(){
            //Logger.Log("O botão 'Atualizar o Promob' já está visível e ativo na tela. Pulando clique no menu 'Arquivo'.", LogLevel.Debug);
        }

        public static void LogUpdaterClicandoMenuArquivo(){
            //Logger.Log("Clicando no menu 'Arquivo' para abrir as opções...", LogLevel.Debug);
        }

        public static void LogUpdaterLocalizandoBotaoAtualizar(){
            //Logger.Log("Localizando botão 'Atualizar o Promob' (ID: OpenProcadUpdate)...", LogLevel.Debug);
        }

        public static void LogUpdaterClicandoAtualizar(){
            //Logger.Log("Clicando em 'Atualizar o Promob'...", LogLevel.Debug);
        }

        public static void LogUpdaterAguardandoJanelaUpdate(){
            //Logger.Log("Aguardando janela do 'Promob Update' abrir...", LogLevel.Debug);
        }

        public static void LogUpdaterJanelaUpdateDetectada(){
            //Logger.Log("Janela 'Promob Update' detectada com sucesso!", LogLevel.Debug);
        }

        public static void LogUpdaterIniciandoVerificacaoAutomatica(){
            Logger.Log("[ATUALIZAÇÃO] Iniciando verificação automática de atualizações...");
        }

        public static void LogUpdaterVerificandoConclusaoAnterior(){
            //Logger.Log("Verificando se a atualização já foi concluída anteriormente...", LogLevel.Debug);
        }

        public static void LogUpdaterConcluidaSucessoAnterior(){
            Logger.Log("[ATUALIZAÇÃO] A atualização já foi concluída com sucesso!");
        }

        public static void LogUpdaterRelocalizandoJanelaUpdate(){
            //Logger.Log("Re-localizando janela 'Promob Update' no desktop...", LogLevel.Debug);
        }

        public static void LogUpdaterForcandoFoco(){
            //Logger.Log("Janela localizada. Forçando foco...", LogLevel.Debug);
        }

        public static void LogUpdaterClicandoAbaStatus(){
            //Logger.Log("Procurando e clicando na aba/botão 'Status' no menu lateral esquerdo...", LogLevel.Debug);
        }

        public static void LogUpdaterAbaStatusClicada(){
            //Logger.Log("Aba 'Status' clicada. Aguardando 3 segundos para carregar o status dos módulos...", LogLevel.Debug);
        }

        public static void LogUpdaterAnalisandoStatusModulos(){
            //Logger.Log("Analisando status dos módulos do Promob...", LogLevel.Debug);
        }

        public static void LogUpdaterModulosAtualizados(){
            Logger.Log("[ATUALIZAÇÃO] Todos os módulos do Promob estão 100% atualizados.");
        }

        public static void LogUpdaterModulosDesatualizadosDetectados(){
            Logger.Log("[ATUALIZAÇÃO] Há módulos desatualizados detectados. Iniciando processo...", LogLevel.Warn);
        }

        public static void LogUpdaterClicandoAbaAtualizar(){
            //Logger.Log("Procurando e clicando no botão/aba 'Atualizar' para voltar à tela de atualizações...", LogLevel.Debug);
        }

        public static void LogUpdaterAbaAtualizarClicada(){
            //Logger.Log("Clique na aba 'Atualizar' efetuado. Iniciando fluxo completo de atualização...", LogLevel.Debug);
        }

        public static void LogUpdaterAguardandoRodape(){
            //Logger.Log("Aguardando carregamento das atualizações no rodapé...", LogLevel.Debug);
        }

        public static void LogUpdaterBtnUpdateHabilitado(){
            //Logger.Log("Botão 'btnUpdate' localizado e habilitado!", LogLevel.Debug);
        }

        public static void LogUpdaterBtnUpdateProximidade(){
            //Logger.Log("Botão 'Atualizar' no rodapé localizado via proximidade de 'Fechar'!", LogLevel.Debug);
        }

        public static void LogUpdaterNenhumaAtualizacaoDisponivel(){
            Logger.Log("[ATUALIZAÇÃO] Nenhuma nova atualização disponível.");
        }

        public static void LogUpdaterBuscandoAtualizacoesPendentes(string name){
            //Logger.Log($"Promob ainda está buscando atualizações ('{name}'). Aguardando...", LogLevel.Debug);
        }

        public static void LogUpdaterAguardandoCarregamentoArquivos(){
            //Logger.Log("Aguardando carregamento/verificação de arquivos terminar...", LogLevel.Debug);
        }

        public static void LogUpdaterBotoesDisponiveisTimeout(string detail){
            //Logger.Log($"[Erro] Botões disponíveis na janela após timeout: {detail}", LogLevel.Debug);
        }

        public static void LogUpdaterBotaoAtualizarDefinido(string? controlType, string? name, string? id){
            //Logger.Log($"Botão 'Atualizar' definido: Tipo={controlType}, Nome='{name}', Id={id}", LogLevel.Debug);
        }

        public static void LogUpdaterBaixandoAtualizacoes(){
            Logger.Log("[ATUALIZAÇÃO] Baixando atualizações do Promob...");
        }

        public static void LogUpdaterBtnInstalarHabilitado(){
            //Logger.Log("Botão 'Instalar' localizado e habilitado!", LogLevel.Debug);
        }

        public static void LogUpdaterProgressoDownload(string name){
            //Logger.Log($"Baixando atualizações: '{name}'. Por favor, aguarde...", LogLevel.Debug);
        }

        public static void LogUpdaterTempoDownload(int minutes, int seconds){
            //Logger.Log($"Download em andamento... (Tempo decorrido: {minutes}m {seconds}s)", LogLevel.Debug);
        }

        public static void LogUpdaterBotaoInstalarDefinido(string? controlType, string? name, string? id){
            //Logger.Log($"Botão 'Instalar' definido: Tipo={controlType}, Nome='{name}', Id={id}", LogLevel.Debug);
        }

        public static void LogUpdaterConfirmandoFechamento(){
            Logger.Log("[ATUALIZAÇÃO] Confirmando o fechamento do Promob para instalação...");
        }

        public static void LogUpdaterElementoOkLocalizado(string? name, string? controlType, string? id){
            //Logger.Log($"Elemento 'Ok' localizado! Nome: '{name}', Tipo: {controlType}, Id: '{id}'", LogLevel.Debug);
        }

        public static void LogUpdaterBotaoOkDefinido(string? controlType, string? name, string? id){
            //Logger.Log($"Botão 'Ok' definido para ação: Tipo={controlType}, Nome='{name}', Id='{id}'", LogLevel.Debug);
        }

        public static void LogUpdaterCliqueOkSucesso(){
            //Logger.Log("Clique no botão 'Ok' do Alerta concluído com sucesso!", LogLevel.Debug);
        }

        public static void LogUpdaterMovendoCursor(int x, int y){
            //Logger.Log($"Movendo cursor e clicando em X={x}, Y={y}...", LogLevel.Debug);
        }

        public static void LogUpdaterFalhaCliqueFisico(string label, string detail){
            //Logger.Log($"Falha no clique físico de mouse em '{label}': {detail}", LogLevel.Debug);
        }

        public static void LogUpdaterAcionandoFallback(){
            //Logger.Log("Acionando ClicarComFallback...", LogLevel.Debug);
        }

        public static void LogUpdaterEnviandoFocusEnterSpace(){
            //Logger.Log("Enviando Focus + ENTER + SPACE...", LogLevel.Debug);
        }

        public static void LogUpdaterFalhaTeclado(string label, string detail){
            //Logger.Log($"Falha ao enviar teclado para '{label}': {detail}", LogLevel.Debug);
        }

        public static void LogUpdaterJanelaRestaurada(){
            //Logger.Log("Janela 'Promob Update' restaurada e localizada via FlaUI!", LogLevel.Debug);
        }

        public static void LogUpdaterAcessoDiretoHwnd(){
            //Logger.Log("FlaUI não localizou após restauração. Tentando acesso direto pelo HWND...", LogLevel.Debug);
        }

        public static void LogUpdaterElementoObtidoHwnd(string name){
            //Logger.Log($"Elemento obtido diretamente pelo HWND: '{name}'", LogLevel.Debug);
        }

        public static void LogUpdaterFalhaHwnd(string detail){
            //Logger.Log($"Falha ao acessar HWND diretamente: {detail}", LogLevel.Debug);
        }

        public static void LogUpdaterHwndDetalhe(IntPtr hWnd, string titulo, bool visivel, int showCmd, bool isToolWin){
            //Logger.Log($"HWND={hWnd} | Título='{titulo}' | Visível={visivel} | showCmd={showCmd} | ToolWindow={isToolWin}", LogLevel.Debug);
        }

        public static void LogUpdaterJanelaEncontradaHwnd(string titlePart, IntPtr hWnd, string title){
            //Logger.Log($"Janela '{titlePart}' encontrada! HWND={hWnd}, Título='{title}'", LogLevel.Debug);
        }

        public static void LogUpdaterJanelaEstado(bool visivel, int showCmd, bool isToolWin){
            //Logger.Log($"Estado: Visível={visivel}, showCmd={showCmd}, ToolWindow={isToolWin}", LogLevel.Debug);
        }

        public static void LogUpdaterRestaurandoTray(){
            //Logger.Log("Restaurando janela do segundo plano/tray...", LogLevel.Debug);
        }

        public static void LogUpdaterRestauradaPrimeiroPlano(){
            //Logger.Log("ShowWindow executado. Janela restaurada para primeiro plano.", LogLevel.Debug);
        }

        public static void LogUpdaterJanelaVisivelTrazendoFrente(){
            //Logger.Log("Janela já está visível/normal. Apenas trazendo para frente...", LogLevel.Debug);
        }

        public static void LogUpdaterBotaoEncontradoPopup(string name){
            //Logger.Log($"Botão encontrado em uma sub-janela/popup do Desktop: '{name}'", LogLevel.Debug);
        }

        public static void LogUpdaterClicandoFecharJanela(){
            //Logger.Log("Clicando no botão 'Fechar' para encerrar a janela do assistente...", LogLevel.Debug);
        }

        public static void LogUpdaterBtnSimLocalizado(){
            //Logger.Log("Botão 'Sim' de confirmação localizado e habilitado!", LogLevel.Debug);
        }

        public static void LogUpdaterBtnSimDefinido(string? controlType, string? name){
            //Logger.Log($"Botão 'Sim' definido: Tipo={controlType}, Nome='{name}'", LogLevel.Debug);
        }

        public static void LogUpdaterSimClicado(){
            //Logger.Log("Clique em 'Sim' efetuado!", LogLevel.Debug);
        }

        public static void LogUpdaterAlertaSimNaoApareceu(){
            //Logger.Log("Alerta de confirmação 'Sim' não apareceu.", LogLevel.Debug);
        }

        public static void LogUpdaterJanelaFechada(){
            //Logger.Log("Janela de atualizações fechada.", LogLevel.Debug);
        }

        public static void LogUpdaterPopupSucessoOcultoRestaurado(){
            //Logger.Log("Popup 'PromobUpdate' oculto restaurado. Aguardando tornar-se acessível...", LogLevel.Debug);
        }

        public static void LogUpdaterFalhaVerificarPopupSucesso(string detail){
            //Logger.Log($"Falha ao verificar popup de sucesso: {detail}", LogLevel.Debug);
        }

        public static void LogUpdaterTempoInstalacao(int minutes, int seconds){
            //Logger.Log($"Instalando atualizações... (Tempo decorrido: {minutes}m {seconds}s)", LogLevel.Debug);
        }

        public static void LogUpdaterBtnFecharSucessoDefinido(string? controlType, string? name){
            //Logger.Log($"Botão 'Fechar' de sucesso definido: Tipo={controlType}, Nome='{name}'", LogLevel.Debug);
        }

        public static void LogUpdaterConcluidaSucesso(){
            Logger.Log("[ATUALIZAÇÃO] Atualização do Promob concluída com sucesso!");
        }


        // ==================================================================================
        // --- 13. MAIN WINDOW ---
        // ==================================================================================

        public static void LogMainWindowPanelReady(){
            Logger.Log("[SISTEMA] Painel de controle de automação pronto.");
        }

        public static void LogMainWindowClosingPromobByUser(){
            Logger.Log("[INFO] Fechando o Promob conforme solicitado pelo usuário...");
        }

        public static void LogMainWindowProcessesClosedSuccess(){
            Logger.Log("[OK] Processos do Promob encerrados com sucesso.");
        }

        public static void LogMainWindowClosingPromobError(string detail){
            Logger.Log($"[ERRO] Falha ao fechar o Promob: {detail}", LogLevel.Error);
        }

        public static void LogMainWindowPromobAlreadyRunning(int processId){
            Logger.Log($"[INFO] Promob já está em execução (PID: {processId}). Trazendo para a tela...");
        }

        public static void LogMainWindowStartingPromob(){
            Logger.Log($"[INFO] Iniciando Promob");
        }

        public static void LogMainWindowPromobExeNotFound(){
            Logger.Log("[AVISO] Operação cancelada ou executável do Promob não foi encontrado.", LogLevel.Warn);
        }

        public static void LogMainWindowStartingPromobError(string detail){
            Logger.Log($"[ERRO] Não foi possível iniciar o Promob: {detail}", LogLevel.Error);
        }

        public static void LogMainWindowUpdateError(string detail){
            Logger.Log($"[ERRO ATUALIZAÇÃO] Falha ao atualizar: {detail}", LogLevel.Error);
        }

        public static void LogMainWindowAutomationStarted(){
            Logger.Log("[SISTEMA] Automação de importação activa. Monitorando pasta de arquivos...");
        }

        public static void LogMainWindowStoppingAutomationRequested(){
            Logger.Log("[INFO] Solicitando parada da automação... Por favor, aguarde a conclusão da etapa atual.");
        }

        public static void LogMainWindowDesktopFolderNotFound(string path){
            Logger.Log($"[ERRO] Pasta do Promob na Área de Trabalho não encontrada: {path}", LogLevel.Error);
        }

        public static void LogMainWindowWaitingForFiles(){
            Logger.Log("[AGUARDANDO] Aguardando novos arquivos na pasta...");
        }

        public static void LogMainWindowPausandoProcessamentoAtualizacao(){
            Logger.Log("[ATUALIZAÇÃO] Pausando processamento: atualização do Promob em andamento...");
        }

        public static void LogMainWindowAtualizacaoFinalizadaRetomando(){
            Logger.Log("[ATUALIZAÇÃO] Atualização finalizada. Retomando processamento de arquivos.");
        }

        public static void LogMainWindowStartingProcessingFile(string name){
            Logger.Log($"[PROCESSANDO] Iniciando processamento do arquivo: {name}");
        }

        public static void LogMainWindowProcessingSuccess(string name){
            Logger.Log($"[SUCESSO] Arquivo '{name}' processado com sucesso!");
        }

        public static void LogMainWindowDeleteFileWarning(string name, string detail){
            Logger.Log($"[AVISO] Não foi possível excluir '{name}': {detail}", LogLevel.Warn);
        }

        public static void LogMainWindowExportFailure(string name, string detail){
            Logger.Log($"[FALHA] Arquivo '{name}' falhou na exportação. Erro: {detail}", LogLevel.Error);
        }

        public static void LogMainWindowFileMovedToErrorFolder(string folderName){
            Logger.Log($"[AVISO] Arquivo com erro movido para a pasta '{folderName}'.");
        }

        public static void LogMainWindowMoveToErrorFolderWarning(string name, string detail){
            Logger.Log($"[AVISO] Não foi possível mover '{name}' para 'promob erro': {detail}", LogLevel.Warn);
        }

        public static void LogMainWindowProcessingError(string name, string detail){
            Logger.Log($"[ERRO] Falha no processamento de '{name}': {detail}", LogLevel.Error);
        }

        public static void LogMainWindowFileKeptForReprocessing(string name){
            Logger.Log($"[INFO] O arquivo '{name}' permanecerá na pasta para reprocessamento.");
        }

        public static void LogMainWindowUpdateWindowDetected(){
            Logger.Log("[ATUALIZAÇÃO] Janela 'Promob Update' detectada.");
        }

        public static void LogMainWindowProjectInProgressAwaitingClose(){
            Logger.Log("[ATUALIZAÇÃO] Projeto aberto em andamento. Aguardando fechamento do projeto...");
        }

        public static void LogMainWindowTimeoutAwaitingClose(){
            Logger.Log("[ATUALIZAÇÃO] Tempo limite excedido aguardando fechamento do projeto.", LogLevel.Warn);
        }

        public static void LogMainWindowProjectFinishedStartingUpdateCheck(){
            Logger.Log("[ATUALIZAÇÃO] Projeto concluído. Iniciando verificação de atualizações...");
        }

        public static void LogMainWindowStartingUpdateCheckDirectly(){
            Logger.Log("[ATUALIZAÇÃO] Iniciando verificação de atualizações...");
        }

        public static void LogMainWindowUpdateExecutionError(string detail){
            Logger.Log($"[ERRO] Falha ao atualizar: {detail}", LogLevel.Error);
        }

        public static void LogMainWindowUpdateSuccess(){
            Logger.Log("[ATUALIZAÇÃO] Atualização concluída com sucesso!");
        }

        public static void LogMainWindowUpdateMonitorError(string detail){
            //Logger.Log($"[UPDATE MONITOR] Erro no monitor: {detail}", LogLevel.Debug);
        }

        public static void LogMainWindowUpdateMonitorFinished(){
            //Logger.Log("[MONITOR] Monitor de atualização encerrado.", LogLevel.Debug);
        }

        public static void LogMainWindowAutomationStopped(){
            Logger.Log("[SISTEMA] Automação parada. Monitoramento encerrado.");
        }

        public static void LogMainWindowServerActive(int port){
            Logger.Log($"[REDE] Modo Servidor ativo. Porta: {port}. Aguardando clientes...");
        }

        public static void LogMainWindowClientConnecting(string host, int port){
            Logger.Log($"[REDE] Modo Cliente. Conectando ao servidor {host}:{port}...");
        }

        public static void LogMainWindowClientConnected(){
            Logger.Log("[REDE] Conectado ao servidor com sucesso!");
        }

        public static void LogMainWindowClientConnectionFailed(string host, int port){
            Logger.Log($"[REDE] Falha ao conectar em {host}:{port}. Verifique se o Servidor está rodando.", LogLevel.Error);
        }

        public static void LogMainWindowRemotePromobStarted(){
            Logger.Log("[INFO] Promob iniciado remotamente pelo operador.");
        }

        public static void LogMainWindowRemoteStartError(string detail){
            Logger.Log($"[ERRO] Falha ao iniciar Promob remotamente: {detail}", LogLevel.Error);
        }

        public static void LogMainWindowRemoteStartExeNotFound(){
            Logger.Log("[AVISO] Executável do Promob não encontrado. Configure o caminho primeiro.", LogLevel.Warn);
        }

        public static void LogMainWindowServerConnectionLost(){
            Logger.Log("[REDE] Conexão com o servidor foi perdida.", LogLevel.Error);
        }

        public static void LogMainWindowRemotePromobClosed(){
            Logger.Log("[OK] Promob encerrado remotamente pelo operador.");
        }

        public static void LogMainWindowRemoteCloseError(string detail){
            Logger.Log($"[ERRO] Falha ao encerrar o Promob remotamente: {detail}", LogLevel.Error);
        }

    }
}
