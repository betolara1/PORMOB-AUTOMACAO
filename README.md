# AutomacaoPromobTeste

**Automação robótica (RPA) para processamento contínuo de projetos no software Promob.**  
O projeto automatiza o fluxo completo de importação, geração de listagens e exportação de dados (XML), eliminando tarefas manuais repetitivas.

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
    OpenProj --> Navigate["7. Navegar: Ferramentas > Orçamento"]
    Navigate --> CloseProj["8. Fechar Projeto (Ignorar Salvar)"]
    end
    
    CloseProj --> Success["✅ Sucesso: Excluir Arquivo"] --> MainLoop
    
    Process -.-> OnError{Ocorreu Erro?}
    OnError -- Sim --> Recovery["⚠️ Recuperação: ESC + Reset UI"]
    Recovery --> MainLoop
```

## 3. Status do Projeto
🚀 **MVP Concluído / Em Desenvolvimento**  
O sistema já opera em modo contínuo com alta confiabilidade, integrando automação de UI e visão computacional para auto-recuperação.

## 4. Arquitetura / Visão Técnica
A solução utiliza uma arquitetura de **Automação de Desktop baseada em Eventos e UI Automation (UIA3)**.

- **Motor de Automação**: [FlaUI](https://github.com/FlaUI/FlaUI) para interação direta com os elementos WPF/WinForms do Promob.
- **Camada de Inteligência Visual**: `VisionHelper` atua como um sistema de suporte, utilizando IA de visão computacional para validar estados da tela que o UIA não consegue mapear (ex: carregamento de texturas ou popups complexos).
- **Gerenciamento de Estado**: Monitoramento em tempo real de pastas do sistema e tratamento dinâmico de exceções (Recovery Mode).

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
- ✅ **Wizard de Importação**: Automação inteligente do assistente de importação do Promob.
- ✅ **Auto-Recuperação**: Detecta e fecha popups inesperados ou diálogos de erro do sistema.
- ✅ **Validação por Visão**: Garante que o projeto está 100% carregado antes de prosseguir com a extração de dados.
- ✅ **Limpeza Automática**: Exclui ou move arquivos processados após o sucesso.

## 7. Fluxos Principais (Processamento)
1. **MainLoop**: Loop infinito que monitora `Desktop/promob`.
2. **ProcessarArquivo**: Orquestrador que executa do passo [1/8] ao [8/8].
3. **VisionHelper.AguardarEstadoTela**: Chamada assíncrona para validação visual de carregamento.
4. **TentarRecuperar**: Rotina de emergência que reseta a UI do Promob em caso de travamento.

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
# API Key para o VisionHelper (opcional se desabilitado)
VISION_API_KEY=sua_chave_aqui

# Configurações de pastas (padrão: Desktop)
PASTA_PROMOB=C:\Users\Nome\Desktop\promob
PASTA_XML=C:\Users\Nome\Desktop\xml
```

## 10. Segurança
- **Segredos**: Chaves de API e caminhos locais são mantidos fora do código-fonte via `.env`.
- **Integridade**: O sistema não altera os arquivos originais antes de garantir o processamento bem-sucedido.
- **Isolamento**: Execução local sem necessidade de exposição de portas de rede.

## 11. Estrutura do Projeto
```text
AutomacaoPromobTeste/
├── Program.cs          # Lógica central e fluxos de automação
├── VisionHelper.cs     # Camada de integração com IA Visual
├── .env                # Configurações sensíveis (não commitado)
├── AutomacaoPromobTeste.csproj
└── backup/             # Histórico de versões do código
```

## 12. Próximos Passos
- [ ] Implementar suporte a múltiplos idiomas (Detecção de nomes de botões dinâmica).
- [ ] Refatorar para o padrão **Page Object Model (POM)** para facilitar manutenção.
- [ ] Criar Dashboard de monitoramento com estatísticas de processamento em tempo real.

## 13. Prints / Logs
A aplicação exibe um banner personalizado e logs coloridos via console para monitoramento fácil do operador:
```text
══════════════════════════════════════════
[NOVO] Processando: Cozinha_Luxo.promob
       Processados até agora: 12 | Erros: 0
══════════════════════════════════════════
[INFO] [1/8] Localizando janela do Promob...
[OK] Aba 'Ferramentas' detectada.
```
