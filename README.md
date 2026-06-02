# PromobAutomacao

**Automação robótica (RPA) de alta resiliência para processamento contínuo de projetos no software Promob.**  
O projeto automatiza o fluxo completo de monitoramento, importação de arquivos `.promob`, geração de exportações via Promob ERP (XML), fechamento seguro de projetos e reset de interface, eliminando tarefas manuais repetitivas com controle robusto de janelas e tratamento de erros.

---

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

---

## 3. Status do Projeto
🚀 **MVP Concluído / Em Desenvolvimento Contínuo**  
O sistema já opera em modo contínuo (24/7 se necessário) com altíssima confiabilidade, integrando automação nativa do Windows (UIA3), busca persistente com cache de elementos e tratamento dinâmico de exceções com fallback inteligente.

---

## 4. Arquitetura e Visão Técnica
A solução utiliza uma arquitetura de **Automação de Desktop baseada em Eventos e UI Automation (UIA3)** modularizada e blindada contra falhas:

- **Motor de Automação Principal**: [FlaUI (UIA3)](https://github.com/FlaUI/FlaUI) para interagir diretamente com os elementos WPF/WinForms expostos pelo Promob na árvore de acessibilidade do Windows.
- **Camada de Clique Resiliente com Fallback**: Implementa acionamentos em 3 níveis (Invoke UIA nativo $\rightarrow$ Clique Físico em Coordenadas Dinâmicas $\rightarrow$ Simulação física de Teclado Space/Enter).
- **Inteligência Visual Assistida (`VisionHelper`)**: Utiliza IA Multimodal (modelo `gemini-2.0-flash` via OpenRouter) para tomar decisões visuais inteligentes e localização de elementos quando a árvore UIA é ausente ou inadequada (opcional).
- **Gerenciamento de Estado**: Sistema de logs centralizado e controle de estado persistente (`AutomacaoEstado`) para monitorar o andamento dos arquivos, sucessos e taxa de falhas.

---

## 5. Stack Técnica
- **Linguagem**: C# (.NET 8.0)
- **Interface Gráfica (WPF)**: Painel do operador com estados e gerenciamento.
- **Bibliotecas Principais**: 
  - `FlaUI.UIA3`: Comunicação de baixo nível com a API de Acessibilidade do Windows.
  - `System.Drawing.Common`: Captura dinâmica e processamento de screenshots.
  - `DotNetEnv`: Gerenciamento seguro de configurações de variáveis locais.
- **Integração Externa**: API OpenRouter (LLM Multimodal Gemini).

---

## 6. Funcionalidades Principais
- ✅ **Monitoramento Contínuo**: Varredura inteligente de diretórios monitorados em tempo real sem sobrecarga de CPU.
- ✅ **Tratamento de Atualizações (`PromobUpdater`)**: Detecta telas de atualização pendentes e as descarta ou confirma de forma robusta.
- ✅ **Wizard de Importação Resiliente**: Preenche caminhos e executa o assistente contornando limitações de campos travados usando clipboard e digitação direta.
- ✅ **Geração de XML (Promob ERP)**: Navega pelos menus `Ferramentas > Integradores > Promob ERP` e monitora o processamento longo (suporta projetos pesados com mais de 40 minutos de espera).
- ✅ **Limpeza de Janelas Órfãs**: Encerra automaticamente as janelas do Windows Explorer que o Promob abre após exportar arquivos.
- ✅ **Recovery Mode (Autocura)**: Monitora diálogos inesperados, popups de erro e reseta a UI do Promob para o estado neutro caso ocorra alguma falha crítica.

---

## 7. Fluxos Principais (Processamento)
1. **MainLoop (`Program.cs`)**: Laço infinito que busca arquivos e gerencia o ciclo de vida.
2. **Workflow (`PromobWorkflow.cs`)**: Orquestrador das 8 etapas sequenciais de processamento.
3. **Importação (`PromobImportador.cs`)**: Gestão de caixas de diálogo do Windows (File Explorer dialog) e assistente de importação.
4. **Fechamento e Reset (`PromobFecharProjeto.cs`)**: Reseta o estado do Promob limpando a área de trabalho 3D para o próximo ciclo de processamento.

---

## 8. Como Rodar Localmente
### Pré-requisitos
- Sistema Operacional Windows 10/11.
- [SDK .NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0).
- Promob aberto na tela inicial (sem projetos carregados).

### Inicialização rápida
1. Clone este repositório.
2. Crie e configure o arquivo `.env` na raiz do projeto (detalhado abaixo).
3. Execute o comando no PowerShell na pasta raiz:
   ```powershell
   dotnet run
   ```

---

## 9. Variáveis de Ambiente e Configuração
Crie um arquivo `.env` na raiz do projeto contendo as seguintes definições:
```env
# API Key do OpenRouter/Gemini para a IA visual do VisionHelper (Opcional)
GEMINI_API_KEY=sua_chave_aqui

# Configurações de pastas (Se omitido, usa pastas padrão criadas na área de trabalho)
PASTA_PROMOB=C:\Users\Nome\Desktop\promob
PASTA_XML=C:\Users\Nome\Desktop\xml
```

---

## 10. Estrutura Detalhada do Projeto
```text
PromobAutomacao/
├── Program.cs              # Ponto de entrada do executável e loop de monitoramento
├── AppMode.cs              # Configuração dos modos de inicialização do robô
├── AutomacaoEstado.cs      # Modelo de persistência e estatísticas de processamento
├── VisionHelper.cs         # Integração de Visão Computacional com Gemini 2.0 Flash
├── MainWindow.xaml / .cs   # Janela principal do operador (Interface WPF)
├── StartupWindow.xaml / .cs# Splash screen de carregamento inicial
├── LoginWindow.xaml / .cs  # Tela de autenticação e controle de acessos
├── Promob/                 # Regras de Negócio e Passos da Automação do Promob
│   ├── PromobWorkflow.cs        # Orquestrador das 8 etapas principais
│   ├── PromobConfig.cs          # Dicionário de IDs de elementos UIA, nomes e caminhos
│   ├── PromobWindowHelper.cs    # Lógica de localização e foco em janelas/popups
│   ├── PromobUpdater.cs         # Bypass resiliente de telas de atualização
│   ├── PromobImportador.cs      # Automação do wizard de arquivos .promob
│   ├── PromobCarregadorProjeto.cs# Tratamento de ambiente 3D e validação de carregamento
│   ├── PromobExportadorErp.cs   # Processamento e exportação do XML ERP
│   ├── PromobFecharProjeto.cs   # Encerramento seguro de projetos e reset de tela
│   ├── PromobRecuperacao.cs     # Tratador de diálogos inesperados (Recovery Mode)
│   └── PromobExportException.cs # Exceções de negócio para falhas na geração ERP
├── Automation/             # Ferramentas Genéricas de Automação de Interface
│   ├── WindowFinder.cs          # Motor de busca UIA resiliente com cache e fallback
│   ├── InteractionHelper.cs     # Ações de Clique, Foco, Digitação e Polling robustos
│   └── PromobWatchdog.cs        # Monitoramento e controle do processo Promob.exe
└── Utils/                  # Utilitários de Diagnóstico e Sistema
    ├── AppLogs.cs               # Central de mensagens estruturadas de logs
    ├── Logger.cs                # Formatação e cores de console para o operador
    ├── Diagnostics.cs           # Marcadores de tempo de execução e performance
    └── NativeClipboard.cs       # Manipulação segura da Área de Transferência (Clipboard)
```

---

## 11. Segurança e Resiliência
- **Manipulação Segura do Clipboard**: O robô faz backup da área de transferência do usuário antes de injetar caminhos de arquivos e a restaura logo em seguida (`NativeClipboard.cs`), prevenindo interferências.
- **Proteção Contra Diálogos Inesperados**: O robô verifica constantemente a presença de diálogos "Salvar Alterações?", "Erro Interno" ou "Aviso de Backup" e os fecha de forma segura antes que o fluxo trave.
- **Workflow Limpo**: Caso ocorra um erro grave, o robô executa a rotina `TentarRecuperar`, que fecha tudo via atalhos de teclado (ESC) e força o reset do Promob para que o próximo arquivo da fila não seja prejudicado.

---

