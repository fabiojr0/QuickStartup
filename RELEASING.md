# Como lançar uma nova versão

> **Ordem importa:** o bump cria a tag apontando pro commit atual. Se você
> rodar o bump e só depois commitar mais alguma coisa, essa mudança **fica de
> fora do release** (foi o que aconteceu com a v1.2.0 — o redesign da UI foi
> commitado depois do bump, e o release saiu sem ele). Sempre:
> **1) commita/termina tudo → 2) só então roda o bump → 3) push.**

0. **Termine e commite todas as mudanças** que devem entrar nesse release.

1. **Bump da versão** (por último — edita `QuickStartup.csproj`, commita e cria a tag local):
   ```
   .\bump-version.ps1 1.2.0
   ```

2. **Enviar tudo** (commit + tag anotada):
   ```
   git push origin main --follow-tags
   ```

3. **Acompanhar o build** — abre a aba *Actions* do repo no GitHub e espera o
   workflow **Release** (disparado pela tag `vX.Y.Z`) terminar. Ele:
   - compila o app (`dotnet publish`, self-contained win-x64);
   - gera o instalador (Inno Setup);
   - publica um GitHub Release com os dois `.exe` anexados.

4. **Conferir o release** em `https://github.com/fabiojr0/QuickStartup/releases/latest`
   — deve ter `QuickStartup-vX.Y.Z.exe` (portátil) e `QuickStartup-Setup-vX.Y.Z.exe`
   (instalador) anexados.

5. Pronto — quem já tem o app instalado recebe o aviso de atualização sozinho
   (checagem via GitHub Releases, ver `UpdateService.cs`) e atualiza com um clique.

## Se o workflow falhar

- Abre o run com falha na aba *Actions*, expande o passo que falhou e lê o erro.
- Corrige o problema, commita e dá push pro `main` **sem mudar a versão de novo**.
- Move a tag pro commit corrigido e reenvia:
  ```
  git tag -d vX.Y.Z
  git tag -a vX.Y.Z -m "Release vX.Y.Z"
  git push origin :refs/tags/vX.Y.Z
  git push origin vX.Y.Z
  ```

## Coisas que só existem num lugar (não duplicar na mão)

- **Versão do app**: só em `<Version>` de `QuickStartup.csproj` — tudo o resto
  (instalador, nome dos arquivos gerados, o que `UpdateService` compara) lê
  esse valor. Use sempre `bump-version.ps1`, nunca edite a versão à mão.
- **Mutex de instância única**: `SingleInstanceId` em `App.xaml.cs` e
  `AppMutexName` em `installer/setup.iss` precisam ser o mesmo GUID — é o que
  permite ao instalador fechar/reabrir o app sozinho numa atualização silenciosa.
