# Plano de Implementação do PainelEstetica

## 1. Objetivo

Transformar o protótipo atual em um sistema confiável para uma clínica de estética real, com landing page pública, painel privado, múltiplos usuários internos, prontuário, agenda, captação de leads, pagamentos, relatórios e gestão de conteúdo.

Este plano implementa as decisões do [ADR-006](./ADRs/ADR-006-Arquitetura-Clinica-Unica-Seguranca-e-Acessos.md) e do [ADR-007](./ADRs/ADR-007-Formularios-Versionados-Arquivos-Clinicos-e-Assinatura-Presencial.md).

## 2. Limites confirmados

- Uma única clínica;
- uma única profissional responsável;
- usuários internos com permissões diferentes;
- nenhuma área pública além da landing page;
- nenhum portal do paciente nesta etapa;
- implantação inicial em Docker, hospedada localmente e publicada por Cloudflare Tunnel;
- dados reais somente após a aprovação do marco de segurança.

## 3. Princípios de implementação

1. Segurança e privacidade são critérios de aceite, não melhorias futuras.
2. Nenhuma interface deve informar sucesso antes da confirmação do servidor.
3. Nenhum dado simulado será usado como fallback silencioso.
4. O backend é a autoridade para autenticação, autorização e regras de negócio.
5. Registros clínicos e financeiros devem preservar histórico.
6. Funcionalidades serão entregues verticalmente, com API, persistência, interface e testes.
7. A estrutura será modular, mas continuará sendo uma aplicação implantável como unidade.
8. A documentação deverá representar o código existente, e não funcionalidades planejadas.

## 4. Estrutura-alvo do repositório

```text
PainelEstetica/
├── backend/
│   ├── PainelEstetica.sln
│   ├── src/
│   │   ├── PainelEstetica.Api/
│   │   │   ├── Configuration/
│   │   │   ├── Middleware/
│   │   │   └── Program.cs
│   │   ├── PainelEstetica.Application/
│   │   │   ├── Abstractions/
│   │   │   └── Common/
│   │   ├── PainelEstetica.Domain/
│   │   │   ├── Common/
│   │   │   └── Shared/
│   │   ├── PainelEstetica.Infrastructure/
│   │   │   ├── Identity/
│   │   │   ├── Persistence/
│   │   │   ├── Storage/
│   │   │   └── Time/
│   │   └── Modules/
│   │       ├── Identity/
│   │       ├── UsersAndPermissions/
│   │       ├── Leads/
│   │       ├── Clients/
│   │       ├── Appointments/
│   │       ├── ClinicalRecords/
│   │       ├── Consents/
│   │       ├── Procedures/
│   │       ├── Payments/
│   │       ├── Files/
│   │       ├── Cms/
│   │       ├── Reports/
│   │       └── Audit/
│   └── tests/
│       ├── PainelEstetica.UnitTests/
│       ├── PainelEstetica.IntegrationTests/
│       └── PainelEstetica.ArchitectureTests/
├── frontend/
│   ├── src/
│   │   ├── app/
│   │   │   ├── providers/
│   │   │   ├── router/
│   │   │   └── layouts/
│   │   ├── features/
│   │   │   ├── auth/
│   │   │   ├── leads/
│   │   │   ├── clients/
│   │   │   ├── appointments/
│   │   │   ├── clinical-records/
│   │   │   ├── consents/
│   │   │   ├── procedures/
│   │   │   ├── payments/
│   │   │   ├── cms/
│   │   │   └── reports/
│   │   ├── pages/
│   │   │   ├── public/
│   │   │   └── private/
│   │   ├── shared/
│   │   │   ├── api/
│   │   │   ├── components/
│   │   │   ├── forms/
│   │   │   ├── hooks/
│   │   │   ├── lib/
│   │   │   └── types/
│   │   └── styles/
│   └── tests/
│       ├── unit/
│       └── e2e/
├── deploy/
│   ├── compose/
│   ├── cloudflare/
│   ├── nginx/
│   ├── backup/
│   └── monitoring/
├── docs/
│   ├── ADRs/
│   ├── runbooks/
│   ├── security/
│   └── schemas/
├── .env.example
├── .gitignore
├── .dockerignore
└── README.md
```

### Observação sobre os módulos do backend

Não será criado um projeto `.csproj` por módulo neste primeiro momento. Os módulos serão pastas e namespaces bem delimitados dentro de uma solução modular. A separação em assemblies só ocorrerá quando trouxer isolamento ou testabilidade reais.

## 5. Matriz inicial de permissões

| Área | Administrador | Profissional | Recepcionista |
|---|---:|---:|---:|
| Usuários e permissões | Gerenciar | Não | Não |
| Leads | Gerenciar | Consultar | Gerenciar |
| Dados básicos de clientes | Gerenciar | Gerenciar | Gerenciar |
| Anamnese | Gerenciar | Gerenciar | Sem acesso |
| Evolução clínica | Gerenciar | Gerenciar | Sem acesso |
| Fotos e documentos clínicos | Gerenciar | Gerenciar | Sem acesso |
| Agenda | Gerenciar | Gerenciar | Gerenciar |
| Procedimentos | Gerenciar | Consultar | Consultar |
| Pagamentos operacionais | Gerenciar | Consultar conforme configuração | Gerenciar |
| Relatórios financeiros | Gerenciar | Consultar conforme configuração | Sem acesso por padrão |
| CMS e landing | Gerenciar | Consultar | Sem acesso |
| Auditoria | Consultar | Sem acesso | Sem acesso |

A matriz será implementada por permissões e poderá ser ajustada pelo administrador sem conceder automaticamente acesso clínico à recepção.

## 6. Fases de execução

## Fase 0 — Preservar e criar uma linha de base

### Objetivo

Colocar o projeto sob controle antes de reorganizar o código.

### Entregas

- Inicializar ou conectar o repositório Git correto;
- criar `.gitignore` e `.dockerignore`;
- remover da árvore versionada `node_modules`, `dist`, `bin` e `obj`;
- registrar comandos oficiais de build e execução;
- corrigir documentação que declara funcionalidades inexistentes;
- criar testes mínimos de caracterização para os fluxos que serão preservados;
- registrar o schema atual e preparar estratégia de migração de dados.

### Critério de aceite

- Repositório limpo e reproduzível;
- frontend e backend compilam a partir de checkout limpo;
- nenhum segredo é versionado;
- estado atual documentado sem afirmações incorretas.

## Fase 1 — Contenção imediata de segurança

### Objetivo

Impedir acesso anônimo a informações administrativas enquanto a nova fundação é construída.

### Entregas

- Separar grupos públicos e privados da API;
- exigir autenticação em clientes, agenda, prontuário, pagamentos, relatórios, usuários, arquivos e escrita do CMS;
- limitar CORS ou removê-lo com uso de reverse proxy same-origin;
- retirar Swagger da exposição pública de produção;
- retirar segredo JWT, senha do banco e credenciais padrão do código;
- remover login e persistência mock como fallback;
- bloquear o upload geral público;
- retirar a afirmação de “100% Segurança LGPD”;
- atualizar dependências com vulnerabilidades conhecidas;
- impedir fallback para banco em memória em produção.

### Critério de aceite

- Requisições anônimas a endpoints privados retornam `401`;
- usuários sem permissão retornam `403` nos primeiros endpoints protegidos;
- nenhum arquivo clínico é servido como conteúdo estático público;
- nenhuma credencial padrão aparece na interface ou no repositório;
- falha da API é apresentada como erro, sem sucesso simulado.

## Fase 2 — Fundação modular e banco versionado

**Status: concluída em 5 de agosto de 2026.** A migration inicial, a adoção transacional do schema legado e a preservação de dados são verificadas em PostgreSQL isolado por Testcontainers. A aplicação do migrador ao volume operacional continua condicionada a backup e restauração testada.

### Objetivo

Criar a estrutura que sustentará as próximas funcionalidades.

### Entregas

- Criar solution e projetos/pastas-alvo necessários;
- dividir endpoints por módulo;
- introduzir DTOs, validações e respostas padronizadas;
- configurar migrations do EF Core;
- mapear relacionamentos, índices, tamanhos e unicidades;
- criar abstração de relógio e configuração do fuso da clínica;
- criar paginação e filtros básicos;
- remover regra de negócio dos endpoints HTTP.

### Critério de aceite

- Migration cria o banco do zero;
- migration de atualização preserva dados de teste existentes;
- entidades não são aceitas diretamente como contratos da API;
- testes de integração executam contra PostgreSQL isolado;
- cada módulo tem limites e dependências identificáveis.

## Fase 3 — Identity, RBAC e auditoria

**Status: concluída em 5 de agosto de 2026.** Identity, cookie seguro, antiforgery, MFA TOTP, papéis, permissões individuais, recuperação administrativa, auditoria e a tela de acessos estão implementados e cobertos por testes.

### Objetivo

Substituir a autenticação demonstrativa por identidade e autorização adequadas.

### Entregas

- ASP.NET Core Identity;
- sessão por cookie seguro;
- antiforgery;
- criação e desativação de usuários;
- papéis e permissões;
- seed seguro apenas do primeiro administrador, por configuração de implantação;
- troca obrigatória da senha inicial;
- bloqueio de tentativas;
- recuperação de senha;
- MFA para administrador e profissional;
- trilha de auditoria;
- tela administrativa de usuários e acessos.

### Critério de aceite

- Matriz de permissões coberta por testes automatizados;
- recepcionista não acessa anamnese nem arquivos clínicos;
- mudança de permissão gera auditoria;
- sessão expirada não mantém o painel falsamente autenticado;
- eventos críticos identificam usuário e horário.

## Marco de segurança para dados reais

Dados reais somente poderão ser inseridos quando as Fases 0 a 3 estiverem concluídas e também existirem:

- armazenamento privado para arquivos;
- backup automático criptografado;
- cópia externa;
- restauração testada;
- HTTPS pelo túnel;
- banco e API sem exposição direta;
- política mínima de contas e senhas;
- aviso de privacidade e canal de atendimento ao titular;
- revisão operacional de quem pode acessar cada tipo de dado.

## Fase 4 — Clientes, leads e agenda

### Objetivo

Entregar o primeiro fluxo operacional completo para recepção e profissional.

### Entregas

- Captação de lead pela landing;
- proteção contra spam e rate limit;
- pipeline de lead: novo, contatado, agendado, convertido e perdido;
- conversão de lead em cliente sem duplicação;
- cadastro básico com validação de CPF, telefone e email;
- agenda por data, horário e profissional;
- duração baseada no procedimento;
- prevenção de conflito de horário;
- status e histórico do agendamento;
- notificações internas;
- busca e paginação no servidor.

### Critério de aceite

- Lead percorre landing, recepção, cliente e agendamento sem redigitação desnecessária;
- conflitos de agenda são impedidos no servidor;
- recepcionista opera o fluxo sem receber dados clínicos;
- datas são exibidas corretamente no fuso configurado.

## Fase 5 — Prontuário, anamnese, consentimentos e arquivos

### Objetivo

Entregar o fluxo clínico com histórico e proteção adequados.

### Entregas

- Modelos versionados de anamnese;
- evolução clínica ligada ao atendimento e autor;
- finalização e adendo de prontuário;
- termos versionados por finalidade;
- aceite, revogação e evidências;
- upload privado de fotos e documentos;
- validação de arquivos e hash;
- autorização de download;
- publicação controlada de cópias de imagens autorizadas;
- auditoria de consulta e alteração.

### Critério de aceite

- Registro finalizado não pode ser sobrescrito;
- toda correção deixa histórico;
- consentimento identifica termo, versão e finalidade;
- URL pública não permite acessar arquivo clínico;
- recepcionista recebe `403` ao tentar consultar conteúdo clínico.

## Fase 6 — Procedimentos, pagamentos e relatórios

### Objetivo

Substituir valores demonstrativos por informações operacionais confiáveis.

### Entregas

- CRUD completo de procedimentos;
- preço e duração vigentes com histórico quando necessário;
- lançamentos de pagamento separados do agendamento;
- pagamento parcial, estorno, desconto e ajuste auditados;
- fechamento e relatórios operacionais;
- exclusão de valores fixos do frontend;
- regras para cancelamento e no-show;
- exportações autorizadas.

### Critério de aceite

- Relatórios são reconciliáveis com lançamentos individuais;
- cancelamentos e estornos não inflam receita;
- valores negativos ou acima dos limites definidos são validados;
- alterações financeiras relevantes geram auditoria.

## Fase 7 — Frontend, acessibilidade e design system

### Objetivo

Reorganizar a interface sobre os fluxos reais já estabilizados.

### Entregas

- Router e layouts público/privado;
- camada HTTP centralizada;
- cache de dados do servidor;
- formulários validados;
- componentes acessíveis;
- dialogs com foco e semântica adequados;
- estados de carregamento, vazio, erro e confirmação;
- responsividade revisada;
- suporte a redução de movimento;
- testes dos fluxos críticos.

### Critério de aceite

- Recarregar ou compartilhar uma URL mantém a tela correta;
- erros de autenticação, permissão e validação são distinguíveis;
- navegação principal funciona por teclado;
- formulários críticos são utilizáveis em celular e desktop;
- não há operação que comunique sucesso antes da resposta do servidor.

## Fase 8 — Marca, landing e CMS

### Objetivo

Construir a presença pública e o fluxo de conversão sobre uma identidade validada com a profissional.

### Entregas

- Workshop curto de identidade;
- nome, posicionamento, paleta, tipografia e direção fotográfica;
- landing com apresentação, procedimentos, diferenciais, resultados autorizados, FAQ e contatos;
- WhatsApp, Instagram e formulário;
- CMS apenas para campos realmente editáveis;
- SEO técnico e social;
- métricas de conversão respeitando privacidade;
- aviso de privacidade e gestão de conteúdo publicado.

### Critério de aceite

- Nenhum link aponta para seção inexistente;
- CTA principal é mensurável;
- conteúdo público não expõe paciente ou arquivo clínico;
- imagens publicadas possuem consentimento vigente e rastreável;
- landing atende aos requisitos mínimos de desempenho e acessibilidade definidos.

## Fase 9 — Implantação doméstica e operação

### Objetivo

Colocar o sistema em operação com capacidade de recuperação.

### Entregas

- Compose de produção separado do desenvolvimento;
- Nginx como único ponto HTTP interno;
- cloudflared em container ou serviço dedicado;
- Cloudflare Access no painel;
- PostgreSQL sem porta publicada;
- volumes e permissões mínimos;
- imagens fixadas em versões aprovadas;
- healthchecks e restart policies;
- backup do banco e arquivos;
- cópia externa criptografada;
- teste documentado de restauração;
- monitoramento e alertas;
- runbooks de atualização, rollback, indisponibilidade e incidente.

### Critério de aceite

- Nenhuma porta é aberta no roteador;
- somente os hostnames esperados são publicados;
- painel exige Cloudflare Access e login da aplicação;
- restauração completa é demonstrada em ambiente isolado;
- falha de backup ou falta de espaço gera alerta;
- atualização possui rollback documentado.

## 7. Estratégia de testes

### Backend

- Testes unitários para regras clínicas, agenda, permissões e financeiro;
- testes de integração para endpoints, autorização e PostgreSQL;
- testes de arquitetura para dependências entre módulos;
- testes de upload com arquivos inválidos e limites;
- testes de concorrência para conflito de agenda.

### Frontend

- Testes de componentes críticos;
- testes de formulário e erros da API;
- testes E2E para login, lead, cliente, agenda, prontuário e permissões;
- testes de acessibilidade automatizados complementados por revisão manual.

### Infraestrutura

- Verificação do Compose;
- smoke test após deploy;
- teste de restauração;
- verificação periódica de dependências e imagens vulneráveis.

## 8. Primeiro ciclo de trabalho

O primeiro ciclo deverá conter somente atividades das Fases 0 e 1:

1. Criar a linha de base Git e arquivos de exclusão;
2. separar configuração de desenvolvimento e produção;
3. remover segredos e credenciais padrão;
4. separar rotas públicas e privadas;
5. proteger imediatamente os endpoints administrativos;
6. remover mocks silenciosos do frontend;
7. bloquear uploads públicos;
8. corrigir Docker Compose para não expor PostgreSQL;
9. atualizar dependências vulneráveis;
10. criar testes de integração que comprovem `401` nos endpoints privados.

Ao fim desse ciclo o protótipo ainda não estará liberado para dados reais, mas sua superfície mais perigosa estará contida e a reorganização poderá avançar com segurança.

## 9. Itens deliberadamente adiados

- Portal do paciente;
- aplicativo móvel;
- múltiplas clínicas;
- múltiplas unidades independentes;
- integrações com convênios;
- microsserviços;
- automações avançadas de marketing;
- inteligência artificial sobre prontuários;
- integrações externas que exijam exposição ampla da API.
