# AutomacaoPromobTeste

**Automação robótica (RPA) para processamento contínuo de projetos no software Promob.**  
O projeto automatiza o fluxo completo de importação, geração de exportações via Promob ERP (XML) e fechamento de projetos, eliminando tarefas manuais repetitivas.

## 2. Fluxo de Funcionamento
```mermaid
graph TD
    Start([Início]) --> InitVision[Inicializar VisionHelper]
    InitVision --> CheckFolder{Pasta 'promob' existe?}
    CheckFolder -- Não --> EndError([Erro: Pasta não encontrada])
    CheckFolder -- Sim --> MainLoop[Loop: Monitorar Pasta]
    
    MainLoop --> GetFile{Tem arquivo .promob?}
    GetFile -- Não --> Wait[Aguardar 3s] --> MainLoop
    GetFile -- Sim --> Process[Processar Arquivo]
    
    subgraph Passo a Passo do Processamento
    Process --> FindPromob["1. Localizar/Focar Janela Promob"]
    FindPromob --> ClickImport["2. Acionar Botão Importar"]
    ClickImport --> SelectFile["3. Selecionar Arquivo no Windows"]
    SelectFile --> AdvWizard["4. Avançar Wizard de Importação"]
    AdvWizard --> HandlePopups["5. Tratar Popups de Novo Projeto"]
    HandlePopups --> OpenProj["6. Abrir Projeto e Validar Carga"]
    OpenProj --> Navigate["7. Integradores > Promob ERP (XML)"]
    Navigate --> CloseProj["8. Fechar Projeto e Resetar UI"]
    end
    
    CloseProj --> Success["✅ Sucesso: Excluir Arquivo"] --> MainLoop
    
    Process -.-> OnError{Ocorreu Erro?}
    OnError -- Sim --> Recovery["⚠️ Recuperação: ESC + Reset UI"]
    Recovery --> MainLoop
```

## 3. Status do Projeto
🚀 **MVP Concluído / Em Desenvolvimento**  
O sistema já opera em modo contínuo com alta confiabilidade, integrando automação de UI, busca persistente de elementos e tratamento de janelas nativas do Windows.

## 4. Arquitetura / Visão Técnica
A solução utiliza uma arquitetura de **Automação de Desktop baseada em Eventos e UI Automation (UIA3)**.

- **Motor de Automação**: [FlaUI](https://github.com/FlaUI/FlaUI) para interação direta com os elementos WPF/WinForms do Promob.
- **Camada de Inteligência Visual**: `VisionHelper` atua como um sistema de suporte, utilizando IA de visão computacional para validar estados da tela que o UIA não consegue mapear (opcional).
- **Gerenciamento de Estado**: Monitoramento em tempo real de pastas do sistema e tratamento dinâmico de exceções (Recovery Mode).
- **Resiliência**: Tratamento de timeouts longos (até 40 min) para processos de exportação pesados.

## 5. Stack Técnica
- **Linguagem**: C# (.NET 8.0)
- **Framework**: .NET Windows Desktop (WPF/UIA)
- **Bibliotecas Principais**: 
  - `FlaUI.UIA3`: Interação com a interface.
  - `System.Drawing.Common`: Manipulação de capturas de tela.
  - `DotNetEnv`: Gerenciamento de segredos e configurações.
- **Integração**: APIs de Visão Computacional (OpenAI/Claude).

## 6. Funcionalidades Principais
- ✅ **Monitoramento Contínuo**: Verifica a entrada de novos arquivos `.promob` em tempo real.
- ✅ **Wizard de Importação**: Automação inteligente do assistente de importação do Promob, preenchendo caminhos via UIA ou Clipboard.
- ✅ **Geração de XML (ERP)**: Navega pelos menus `Ferramentas > Integradores > Promob ERP` e aguarda o sucesso da exportação.
- ✅ **Auto-Recuperação**: Detecta e fecha popups inesperados ou diálogos de erro do sistema durante todo o processo.
- ✅ **Limpeza Automática**: Fecha janelas do Windows Explorer abertas após exportações e exclui arquivos processados.

## 7. Fluxos Principais (Processamento)
1. **MainLoop**: Loop infinito que monitora a pasta configurada (padrão `Desktop/promob`).
2. **ProcessarArquivo**: Orquestrador que executa do passo [1/8] ao [8/8].
3. **AguardarEFinalizarExportacaoErp**: Monitoramento de barra de progresso e textos de sucesso com timeout dinâmico.
4. **TentarRecuperar**: Rotina de emergência que reseta a UI do Promob em caso de erro crítico.

## 8. Como Rodar Localmente
### Pré-requisitos
- Windows OS (suporte a UIA3).
- [SDK .NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0).
- Promob instalado e aberto na tela inicial.

### Comandos
1. Clone o repositório.
2. Configure o arquivo `.env` (veja seção abaixo).
3. Na raiz do projeto, execute:
   ```powershell
   dotnet run
   ```

## 9. Variáveis de Ambiente / Configuração
Crie um arquivo `.env` na raiz do projeto:
```env
# API Key para o VisionHelper (opcional)
VISION_API_KEY=sua_chave_aqui

# Configurações de pastas (opcional, usa caminhos do Windows por padrão)
PASTA_PROMOB=C:\Users\Nome\Desktop\promob
PASTA_XML=C:\Users\Nome\Desktop\xml
```

## 10. Segurança
- **Segredos**: Chaves de API e caminhos locais são mantidos fora do código-fonte via `.env`.
- **Integridade**: O sistema não altera os arquivos originais antes de garantir o processamento bem-sucedido.
- **Workflow Limpo**: Restaura o foco da janela principal e limpa janelas auxiliares (Explorer) automaticamente.

## 11. Estrutura do Projeto
```text
AutomacaoPromobTeste/
├── Program.cs          # Ponto de entrada e loop de monitoramento
├── Promob/
│   ├── PromobWorkflow.cs   # Lógica sequencial da automação (Workflow)
│   ├── PromobConfig.cs     # Constantes, IDs e Strings de UI
│   └── PromobWindowHelper.cs# Utilitários para busca de janelas específicas
├── Automation/
│   ├── WindowFinder.cs     # Motor de busca robusto com fallback e cache
│   └── InteractionHelper.cs# Facades para cliques, foco e preenchimento
└── Utils/
    ├── Logger.cs           # Sistema de logs coloridos
    ├── Diagnostics.cs      # Medição de tempo de execução
    └── NativeClipboard.cs  # Manipulação segura do clipboard do Windows
```

## 12. Próximos Passos
- [ ] Implementar suporte a múltiplos idiomas (Detecção de nomes de botões dinâmica).
- [ ] Criar Dashboard de monitoramento com estatísticas de processamento em tempo real.
- [ ] Adicionar suporte para exportação via "Orçamento" como alternativa opcional (Toggle).

## 13. Logs de Exemplo
A aplicação exibe um banner personalizado e logs detalhados via console:
```text
══════════════════════════════════════════
[NOVO] Processando: Cozinha_Luxo.promob
       Processados até agora: 12 | Erros: 0
══════════════════════════════════════════
[INFO] [1/8] Localizando janela do Promob...
[INFO] [7/8] Navegando até Ferramentas > Integradores > Promob ERP...
[OK] Aba 'Ferramentas' encontrada.
[OK] Botão 'Integradores' encontrado.
[SUCESSO] Exportação ERP concluída! ('Completado com sucesso!'). Tempo gasto: 42s.
[OK] Janela da pasta detectada: '01_XML'. Fechando...
[INFO] Fluxo concluído para este arquivo.
```
══════════════
[NOVO] Processando: Cozinha_Luxo.promob
       Processados até agora: 12 | Erros: 0
══════════════════════════════════════════
[INFO] [1/8] Localizando janela do Promob...
[OK] Aba 'Ferramentas' detectada.
```
