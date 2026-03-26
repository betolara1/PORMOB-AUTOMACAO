# Análise de Bugs e Riscos de Automação (Promob)

Esta análise detalha possíveis pontos de falha no arquivo [Program.cs](file:///c:/Users/Ralf/Desktop/Programa%C3%A7%C3%A3o/AutomacaoPromobTeste/Program.cs) (linhas 1-1311), divididos por fase do processo.

## 3. Acionamento do Botão Importar ([ClicarBotaoImportar](file:///c:/Users/Ralf/Desktop/Programa%C3%A7%C3%A3o/AutomacaoPromobTeste/Program.cs#344-396))

*   **Cache de Elementos (Stale Elements):** `_cachedHost` armazena o `elementHost1`. No WPF (tecnologia do Promob), se a aba mudar ou a ribbon for redesenhada, o elemento antigo torna-se "Stale" (inválido). O uso de [ElementoValido](file:///c:/Users/Ralf/Desktop/Programa%C3%A7%C3%A3o/AutomacaoPromobTeste/Program.cs#970-986) ajuda, mas acessar propriedades de um elemento "morto" pode lançar exceções de automação não tratadas em alguns pontos.
*   **Busca Profunda Limitada:** A função [BuscarAteNivel(maxNivel: 4)](file:///c:/Users/Ralf/Desktop/Programa%C3%A7%C3%A3o/AutomacaoPromobTeste/Program.cs#951-969) é uma ótima otimização de performance, mas se o botão "Importar" for movido para dentro de um contêiner mais profundo (ex: um RibbonGroup dentro de um RibbonTab dentro de outro contêiner), o script deixará de encontrá-lo.

## 4. Preenchimento de Diálogo ([PreencherDialogoNativo](file:///c:/Users/Ralf/Desktop/Programa%C3%A7%C3%A3o/AutomacaoPromobTeste/Program.cs#436-565))

*   **Identificadores Fixos (ID 1148):** O uso do ID `1148` para o campo "Nome" funciona em diálogos padrão do Windows 10/11. Se o sistema operacional mudar ou se o Promob usar um diálogo customizado (ex: via bibliotecas de tema), esse ID falhará.
*   **Corrida no Fechamento:** O loop de espera pelo fechamento do diálogo (`dialogo.Properties.IsOffscreen`) é bom, mas se o Windows mostrar um popup "O nome do arquivo não é válido", o script detecta o popup genericamente, mas pode ter dificuldade em focar novamente no campo correto para corrigir o erro.


## 6. Navegação de Menus ([AbrirListagem](file:///c:/Users/Ralf/Desktop/Programa%C3%A7%C3%A3o/AutomacaoPromobTeste/Program.cs#265-343))

*   **Toggle do Orçamento:** O botão `PART_ToggleButton` é um nome comum em controles WPF. Se houver mais de um botão com esse ID de template na tela, o `FindFirstDescendant` pegará o primeiro, que pode não ser o de "Orçamento".
*   **Itens de Menu Dinâmicos:** O menu de "Listagem" é injetado via código após o clique no Orçamento. Se houver lag na rede/servidor de licenças, o menu pode demorar mais de 4s para aparecer, causando o `[AVISO] Item 'Listagem' não encontrado`.

## 7. Fechamento e Salvamento ([FecharProjeto](file:///c:/Users/Ralf/Desktop/Programa%C3%A7%C3%A3o/AutomacaoPromobTeste/Program.cs#201-264))

*   **Destaque da Janela:** Se houver DOIS popups de atenção (raro, mas possível), o [EncontrarPopupAtencao](file:///c:/Users/Ralf/Desktop/Programa%C3%A7%C3%A3o/AutomacaoPromobTeste/Program.cs#886-908) pegará um, mas o outro continuará bloqueando a UI.
*   **Atalho Alt+F:** Se o foco não estiver perfeito na janela principal ao disparar `Alt+F`, o menu Arquivo não abrirá e o comando "f" (fechar) pode ser digitado em algum campo de texto aberto por engano.

## Recomendações de Melhoria

1.  **Limpeza de Cache Profunda:** No início de cada arquivo, zerar não apenas o cache de botões, mas re-verificar o PID do processo Promob atual.
2.  **Validação de "UI Ready":** Antes de clicar em Orçamento, verificar se o Promob não está em estado "Busy" (se possível via propriedade `IsEnabled` do elemento raiz).
3.  **Logging de Coordenadas:** Em caso de erro ao clicar via UIA, logar as coordenadas em que o elemento deveria estar para ajudar no debug visual.
4.  **Escape Global:** No [TentarRecuperar](file:///c:/Users/Ralf/Desktop/Programa%C3%A7%C3%A3o/AutomacaoPromobTeste/Program.cs#736-780), adicionar uma tecla `ESC` extra e um [AtivarJanela](file:///c:/Users/Ralf/Desktop/Programa%C3%A7%C3%A3o/AutomacaoPromobTeste/Program.cs#993-1028) reforçado na janela principal.

> [!TIP]
> A implementação atual é muito resiliente por causa dos loops de retry, mas a maioria dos bugs residuais virá de **tempos de carregamento variáveis** (lag) e **popups inesperados** de plugins de terceiros.
