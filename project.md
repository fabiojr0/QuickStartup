# Prompt: App de Perfis de Monitores e Inicialização Automática

Crie um aplicativo desktop para Windows usando **C# com WPF (.NET 8)**, com as seguintes características:

## Objetivo geral
O app deve detectar automaticamente todos os monitores conectados ao computador e permitir que o usuário crie **perfis personalizados**. Cada perfil define uma lista de aplicativos que devem ser abertos automaticamente, especificando em qual monitor e em qual posição/tamanho de janela cada um deve aparecer.

## Funcionalidades principais

### 1. Detecção de monitores
- Detectar todos os monitores conectados (via `System.Windows.Forms.Screen.AllScreens` ou equivalente).
- Exibir para o usuário: nome/identificador de cada monitor, resolução e posição relativa (para ele entender qual é qual visualmente, idealmente com uma representação gráfica simples da disposição dos monitores).

### 2. Gerenciamento de perfis
- Permitir criar, editar, duplicar e excluir perfis.
- Cada perfil deve conter uma lista de "itens", onde cada item tem:
  - Caminho do executável ou atalho do aplicativo (ex: Discord, WhatsApp Desktop, navegador com URL específica, etc).
  - Argumentos de linha de comando opcionais (ex: abrir uma URL específica no navegador).
  - Monitor de destino.
  - Posição (X, Y) e tamanho (largura, altura) da janela dentro do monitor, ou opção de "maximizar" na tela escolhida.
  - Um pequeno delay/tempo de espera configurável antes de tentar reposicionar (para dar tempo do app abrir).
- Salvar os perfis em um arquivo local (JSON ou SQLite) na pasta de dados do usuário (`%AppData%`).

### 3. Execução de perfil
- Ao selecionar um perfil, o app deve:
  1. Abrir cada aplicativo da lista (`Process.Start`).
  2. Aguardar a janela do processo aparecer (loop com timeout, verificando `Process.MainWindowHandle` ou usando `EnumWindows`/`FindWindow` para apps que criam múltiplos processos, como navegadores e Discord).
  3. Mover e redimensionar a janela para o monitor e posição definidos, usando `SetWindowPos` via P/Invoke (`user32.dll`).
- Tratar casos onde o app já está aberto (não abrir duplicado, apenas reposicionar a janela existente).
- Mostrar feedback visual (barra de progresso ou lista de status) enquanto os apps estão sendo abertos e posicionados.

### 4. Inicialização com o Windows
- Opção nas configurações para "Iniciar com o Windows", implementada via chave no Registro (`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`) ou atalho na pasta de Inicialização.
- Opção de definir um **perfil padrão** que será executado automaticamente assim que o Windows iniciar.
- Ao iniciar automaticamente, o app pode rodar minimizado na bandeja do sistema (system tray), executando o perfil padrão sem precisar abrir a janela principal.

### 5. Interface (WPF)
- Tela principal: lista de perfis existentes, botão para executar cada um, botão para criar novo.
- Tela de edição de perfil: lista de apps adicionados, formulário para adicionar novo app (caminho, argumentos, monitor, posição), representação visual simples dos monitores para facilitar a escolha de posição (arrastar e soltar seria um diferencial, mas campos numéricos também são aceitáveis na primeira versão).
- Ícone na bandeja do sistema com menu rápido (executar perfil X, abrir configurações, sair).
- Interface simples e funcional, sem necessidade de design muito elaborado.

## Requisito de distribuição: Instalador
- Gerar um **instalador para Windows** usando **Inno Setup** ou **WiX Toolset** (escolher o que for mais simples de configurar).
- O instalador deve:
  - Copiar os arquivos do app para `Program Files` (ou pasta escolhida pelo usuário).
  - Criar atalho no Menu Iniciar e, opcionalmente, na Área de Trabalho.
  - Perguntar se o usuário deseja habilitar "iniciar com o Windows" já durante a instalação.
  - Incluir desinstalador.
- O resultado final deve ser um único arquivo `.exe` de instalação, fácil de compartilhar com outras pessoas sem precisar delas terem o .NET SDK instalado (publicar como *self-contained* ou indicar a necessidade do .NET Runtime, o que for mais prático).

## Estrutura de entrega esperada
1. Código-fonte organizado do projeto WPF (.NET 8).
2. Lógica de detecção de monitores.
3. Lógica de abertura e reposicionamento de janelas via P/Invoke.
4. Sistema de perfis com persistência local (JSON).
5. Integração com inicialização do Windows.
6. Ícone de bandeja do sistema.
7. Script de instalador (Inno Setup ou WiX) pronto para gerar o `.exe` de distribuição.

Comente o código de forma clara, especialmente as partes que usam P/Invoke, já que são as mais sensíveis do projeto.