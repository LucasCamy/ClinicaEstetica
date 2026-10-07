# ADR-011: Atualização segura do visualizador PDF

## Status

Aceito em 10/08/2026.

## Contexto

Os termos digitais usam `pdfjs-dist` no navegador para abrir e renderizar PDFs enviados pela clínica. Esses arquivos não podem ser tratados como confiáveis, mesmo quando vêm de usuários autenticados: um PDF malformado pode ser enviado por engano, obtido de terceiros ou reutilizado a partir de um modelo antigo.

Uma verificação de dependências identificou a versão `5.7.284` de `pdfjs-dist` dentro do intervalo afetado pelo aviso de segurança `GHSA-hq66-cqwq-w95j`, relacionado à execução de JavaScript ao abrir PDFs especialmente preparados. O pacote corrigido disponível é a versão `6.2.108`, que é uma atualização principal e exige validação do fluxo de termos.

## Decisão

1. Atualizar `pdfjs-dist` para a versão exata `6.2.108`.
2. Continuar usando o worker distribuído pelo mesmo pacote e importado pelo Vite via `?url`, evitando discrepância de versão entre motor e worker.
3. Manter a entrega do worker como módulo JavaScript (`.mjs`) pelo Nginx e verificar seu MIME type após a publicação.
4. Usar somente as APIs de carregamento, página e renderização em canvas; a aplicação não solicita nem executa ações JavaScript, links ou anotações interativas do PDF.
5. Configurar `stopAtErrors: true` para que um PDF malformado falhe de forma explícita, em vez de produzir uma renderização recuperada parcialmente.
6. Não cachear PDFs clínicos no PWA: somente os assets estáticos da interface podem ser cacheados pelo service worker.
7. Fixar a versão corrigida no `package.json` sem intervalo semântico até a próxima revisão de segurança deliberada.

## Validação obrigatória

Após a atualização, devem ser verificados:

- typecheck e build de produção do frontend;
- ausência de vulnerabilidade conhecida para `pdfjs-dist` no `npm audit --omit=dev`;
- carregamento de um PDF-base de termo com mais de uma página;
- navegação entre páginas, inserção/movimentação de campos e assinatura manuscrita;
- geração e abertura do PDF finalizado;
- resposta do worker PDF com MIME type JavaScript no ambiente Docker.

## Consequências

### Positivas

- Remove a vulnerabilidade conhecida do componente que interpreta PDFs.
- Mantém a defesa em profundidade ao impedir avaliação dinâmica e ao não persistir PDFs clínicos no cache do PWA.
- Documenta um processo repetível para futuras atualizações de bibliotecas que manipulam arquivos clínicos.

### Riscos e mitigação

- A versão 6 é uma atualização principal e pode alterar APIs ou o comportamento de renderização. Por isso, a atualização não é considerada concluída apenas com o `npm audit`: os fluxos de termo devem ser verificados antes de produção.
- PDFs raros que dependam de recursos legados podem renderizar de modo diferente. O original permanece preservado no backend e o editor deve informar falha sem produzir um PDF final inválido.
