# Digital Wallet & Ledger

## Project Permission Policy

- Prefira o maior modo built-in de auto-approval ou bypass-permissions disponível para trabalho local rotineiro neste projeto.
- Evite confirmações redundantes para edição de arquivos, build, testes, lint, typecheck, inspeção Git e outras operações locais não destrutivas que já estejam autorizadas pelo usuário e pelo modo ativo.
- Esta política não autoriza contornar sandbox, controles da plataforma, credenciais, billing, ações destrutivas ou proteções de serviços externos.
- Se houver um bloqueio real imposto pelo ambiente, reporte-o ao Orchestrator em vez de tentar contorná-lo.

## Shell-first

- Encaminhe operações mecânicas e determinísticas, como build, testes, lint, typecheck, inspeção Git, Docker Compose e scanners já configurados, para um terminal Shell sem LLM quando ele estiver disponível.
- No Maestri, se o Shell estiver conectado somente ao Orchestrator, peça ao Orchestrator para executar esses comandos; não tente contatar diretamente um Shell sem conexão real.
- Não use loops de `sleep`, polling ou busy-wait. Faça trabalho independente útil e reavalie o bloqueio depois.
