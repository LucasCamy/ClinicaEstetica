# ADR-009 — Relatórios seguros, retornos recomendados e rotas persistentes

- Status: Aceito
- Data: 2026-08-10

## Contexto

O painel será utilizado com dados reais de pacientes. A clínica precisa consultar atendimentos, pagamentos, procedimentos, aniversários e retornos sem expor o banco a consultas arbitrárias. Também precisa preservar a tela administrativa atual ao atualizar o navegador ou acessar um link direto.

## Decisões

1. Relatórios personalizados usam um catálogo fechado de campos, filtros e ordenações implementados no servidor. Não haverá SQL, expressões ou nomes de tabela informados pela interface.
2. A exportação CSV é gerada no servidor, limitada a 10.000 linhas, codificada para Excel e auditada com o conjunto de filtros utilizado. Respostas de formulários e evoluções clínicas não são exportáveis nesta funcionalidade.
3. Cada procedimento pode configurar, de forma opcional, `RecommendedReturnDays`. O retorno é calculado a partir do último atendimento concluído do mesmo cliente e procedimento; o sistema alerta, mas não cria agendamentos automaticamente.
4. A data de conclusão passa a ser registrada em `CompletedAtUtc`. Dados antigos concluídos usam a data agendada como histórico inicial na migração.
5. O frontend utiliza rotas de navegador: `/` para a landing page e `/admin/...` para cada área restrita. O Nginx mantém fallback para `index.html`, permitindo atualização e links diretos.

## Consequências

- Relatórios permanecem previsíveis, paginados e protegidos por `reports.read`.
- Administradora e profissional recebem a permissão de leitura de relatórios; demais permissões continuam no backend.
- O dashboard pode apontar retornos vencidos ou previstos para os próximos 14 dias.
- A interface pode crescer por módulos, evitando centralizar novas telas em `AdminComponents.tsx`.
