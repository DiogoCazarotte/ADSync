# Frontend no VS Code

Abra a raiz da solução no VS Code. As páginas continuam sendo Blazor (`.razor`, com HTML e C#); Tailwind CSS 4 e daisyUI 5 fornecem os estilos compilados localmente. Bootstrap deixou de ser carregado no aplicativo.

## Preparação

Instale Node.js/npm e o SDK .NET 10. Na raiz, execute `npm.cmd ci` no PowerShell (ou `npm ci` em outros terminais). As versões são fixadas em `package-lock.json`. O VS Code recomenda as extensões C# Dev Kit e Tailwind CSS IntelliSense.

## Editar e visualizar

- Páginas: `ADSync.Client/Pages/`.
- Estrutura visual: `ADSync.Client/Layout/MainLayout.razor`.
- Estilos e tema: `ADSync.Client/Styles/app.css`.
- CSS gerado: `ADSync.Client/wwwroot/css/app.css`; não editar diretamente.

Em um terminal, execute `npm.cmd run watch:css`. Em outro, inicie o cliente com `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Start-DemoClient.ps1`. A API deve estar disponível na porta 5099 para login e dados reais. Abra [http://localhost:5125](http://localhost:5125). Alterações de CSS são compiladas pelo watcher; recarregue a página para vê-las. Para compilar uma vez, use `npm.cmd run build:css`. Essas tarefas também estão no menu **Terminal → Executar Tarefa**.

A compilação .NET do cliente também atualiza o CSS quando as entradas mudam. O CSS gerado fica no repositório e entra na publicação e no cache da PWA; não há dependência de CDN em execução. O modo de desenvolvimento continua tendo os limites offline descritos no README.

## Componentes

As classes Tailwind podem ser usadas diretamente no HTML das páginas, por exemplo `flex gap-4 p-6`. As classes daisyUI usam o prefixo `ui-` para evitar conflito com as classes anteriores:

```html
<button class="ui-btn ui-btn-primary">Salvar</button>
<span class="ui-badge ui-badge-outline">Ativo</span>
```

Preserve os eventos Blazor, validações, restrições por perfil e estados de carregamento ao alterar o HTML. Use nomes de classes completos nas páginas para que o compilador identifique os estilos.

Referências: [Tailwind CLI](https://tailwindcss.com/docs/installation/tailwind-cli) e [daisyUI com CLI](https://daisyui.com/docs/install/cli/).
