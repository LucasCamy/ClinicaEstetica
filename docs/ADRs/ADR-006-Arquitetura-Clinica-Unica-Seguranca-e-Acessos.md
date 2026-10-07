# ADR-006: Arquitetura para Clínica Única, Painel Privado e Controle de Acessos

## Status

Aceito em 05/08/2026.

## Contexto

O PainelEstetica deixará de ser apenas uma demonstração e será utilizado na operação real de uma clínica de estética. O sistema tratará dados cadastrais, financeiros e dados pessoais sensíveis relacionados à saúde, incluindo anamnese, evolução clínica, documentos e fotografias.

O produto atenderá uma única clínica e uma única profissional responsável. Poderão existir vários usuários internos, especialmente a profissional, o proprietário/administrador e recepcionistas, com acessos diferentes conforme suas funções.

A única área pública será a landing page destinada à apresentação da profissional e captação de leads. Não haverá portal do paciente nesta etapa. O painel administrativo será privado e hospedado inicialmente em infraestrutura doméstica com Docker e Cloudflare Tunnel.

O protótipo atual contém autenticação JWT artesanal, endpoints administrativos sem autorização no servidor, dados simulados no frontend, uploads públicos e acoplamento entre transporte HTTP, regras de negócio e persistência. Essa estrutura não é adequada para dados reais.

## Decisão

### 1. Escopo do produto

O sistema será desenvolvido como uma aplicação de clínica única, sem multi-tenancy e sem portal do paciente nesta fase.

Serão mantidas duas superfícies claramente separadas:

- **Landing pública**: apresentação, procedimentos públicos, WhatsApp, Instagram e formulário de captação de lead.
- **Painel privado**: clientes, agenda, anamnese, prontuário, documentos, fotografias, pagamentos, relatórios, CMS, usuários e permissões.

A API também será dividida em contratos públicos e privados. Nenhum endpoint privado poderá depender apenas de ocultação na interface.

### 2. Estilo arquitetural

Será adotado um **monólito modular organizado por funcionalidades**, evitando tanto o arquivo único atual quanto a complexidade prematura de microsserviços.

Os módulos iniciais serão:

- Identity;
- UsersAndPermissions;
- Leads;
- Clients;
- Appointments;
- ClinicalRecords;
- Consents;
- Procedures;
- Payments;
- Files;
- Cms;
- Reports;
- Audit.

Cada módulo deverá concentrar seus endpoints, contratos, validações, casos de uso e regras. Entidades de persistência não serão usadas diretamente como entrada ou resposta da API.

As dependências seguirão o fluxo:

```text
Web/API -> Application/Use Cases -> Domain
                 |
                 v
           Infrastructure
```

Não serão criadas camadas vazias apenas para satisfazer um desenho. A separação deverá representar responsabilidades reais e permitir testes independentes das regras relevantes.

### 3. Autenticação e sessão

Será utilizado **ASP.NET Core Identity**, substituindo a autenticação JWT artesanal do protótipo.

Como o produto será inicialmente uma aplicação web same-origin, a sessão utilizará cookie com, no mínimo:

- `HttpOnly`;
- `Secure`;
- política `SameSite` compatível com o fluxo;
- expiração e renovação controladas;
- proteção antiforgery nas operações mutáveis;
- bloqueio temporário após tentativas inválidas;
- recuperação segura de senha;
- MFA para contas administrativas e profissionais antes da entrada em produção.

Não serão armazenados tokens de autenticação no `localStorage`.

### 4. Autorização

A autorização será aplicada no backend por permissões. Os papéis serão conjuntos iniciais de permissões, e não a única fonte de decisão.

Papéis iniciais:

- **Administrador/Proprietário**: administração geral, usuários, permissões, configurações, CMS e todos os módulos;
- **Profissional**: agenda, clientes, anamnese, prontuário, documentos e fotografias clínicas, procedimentos e relatórios autorizados;
- **Recepcionista**: leads, dados cadastrais básicos, agenda, contato e operações financeiras permitidas, sem acesso padrão a anamnese, evoluções clínicas e arquivos sensíveis.

Permissões iniciais:

- `Users.Manage`;
- `Permissions.Manage`;
- `Leads.Read`, `Leads.Manage`;
- `Clients.ReadBasic`, `Clients.ManageBasic`;
- `Clients.ReadSensitive`;
- `Appointments.Read`, `Appointments.Manage`;
- `ClinicalRecords.Read`, `ClinicalRecords.Write`;
- `Consents.Read`, `Consents.Manage`;
- `Files.ReadClinical`, `Files.ManageClinical`;
- `Payments.Read`, `Payments.Manage`;
- `Reports.Operational`, `Reports.Financial`;
- `Procedures.Manage`;
- `Cms.Manage`;
- `Audit.Read`.

Toda rota privada deverá exigir autenticação e permissão explícita. A aplicação deverá responder corretamente com `401` para usuário não autenticado e `403` para usuário autenticado sem permissão.

### 5. Persistência e evolução do banco

PostgreSQL continuará sendo o banco principal. A evolução do schema será feita exclusivamente por migrations versionadas do Entity Framework Core.

O sistema deverá:

- falhar ao iniciar em produção se o banco não estiver configurado;
- não utilizar banco em memória como fallback de produção;
- possuir chaves estrangeiras, índices e restrições de unicidade adequados;
- armazenar timestamps em UTC e manter o fuso da clínica como configuração;
- usar exclusão lógica onde houver necessidade operacional ou legal;
- impedir exclusão destrutiva de registros clínicos finalizados;
- preservar histórico financeiro por lançamentos, ajustes e estornos, sem sobrescrever fatos anteriores.

### 6. Prontuário, consentimentos e auditoria

Evoluções clínicas finalizadas serão tratadas como registros históricos. Correções deverão ocorrer por adendo identificado, preservando o conteúdo original.

Consentimentos serão versionados e separados por finalidade, incluindo quando aplicável:

- tratamento de dados;
- realização de procedimento;
- uso de imagem;
- comunicação ou marketing.

O aceite deverá guardar a versão do termo, finalidade, data/hora, forma de aceite e evidências técnicas necessárias. Revogações também serão registradas.

Será mantida trilha de auditoria para ações relevantes, incluindo:

- login e falhas de login;
- consulta de dados clínicos;
- criação e adendo de prontuário;
- upload, visualização e remoção lógica de arquivos;
- alteração de permissões;
- publicação de conteúdo;
- movimentações financeiras relevantes.

Logs técnicos não deverão conter senhas, tokens, anamnese, documentos, CPF completo ou outros dados sensíveis desnecessários.

### 7. Arquivos e fotografias

Arquivos clínicos serão privados e armazenados fora da pasta pública do servidor web.

O módulo de arquivos deverá validar:

- usuário e permissão;
- vínculo com o paciente;
- tamanho máximo;
- extensões permitidas;
- tipo MIME e assinatura real do arquivo;
- nome gerado pelo servidor;
- hash de integridade;
- classificação e finalidade;
- consentimento vigente antes de publicação de imagem.

Downloads privados passarão pela autorização da aplicação ou utilizarão URLs temporárias. A landing consumirá apenas cópias explicitamente publicadas e desvinculadas do prontuário privado.

### 8. Frontend

Mocks serão permitidos somente em ambiente de demonstração explicitamente identificado e nunca como fallback silencioso.

O frontend utilizará:

- rotas reais para landing, login e áreas do painel;
- cliente HTTP centralizado e configurável por ambiente;
- tratamento explícito de `401`, `403`, validação e indisponibilidade;
- cache e sincronização de dados do servidor;
- componentes acessíveis;
- separação entre componentes visuais, formulários e operações de dados;
- estados de carregamento, vazio, erro e confirmação.

O painel e a API serão preferencialmente acessados pelo mesmo domínio privado e por reverse proxy, evitando CORS amplo.

### 9. Infraestrutura doméstica

O Cloudflare Tunnel será executado como serviço/container com conexão apenas de saída. Nenhuma porta do PostgreSQL ou da API será publicada na internet ou no roteador.

Serão usados hostnames separados:

- domínio público para a landing;
- subdomínio privado para o painel, protegido adicionalmente por Cloudflare Access com identidades autorizadas.

A infraestrutura deverá possuir:

- backups automáticos e criptografados;
- pelo menos uma cópia externa à residência;
- teste periódico de restauração;
- healthchecks;
- reinício automático;
- monitoramento de disponibilidade, disco e falhas de backup;
- atualização controlada de imagens e dependências;
- documentação de recuperação de desastre;
- nobreak recomendado para o equipamento e rede.

Cloudflare Access será uma camada adicional de perímetro e não substituirá as permissões internas da aplicação.

### 10. Regra para entrada em produção

Dados reais de pacientes não poderão ser cadastrados antes da conclusão do marco de segurança definido no plano de implementação.

A aplicação não afirmará estar em “100% de conformidade com a LGPD”. Ela deverá comunicar práticas de privacidade concretas e manter documentação operacional revisada com apoio jurídico adequado.

## Alternativas consideradas

### Manter JWT artesanal e token no localStorage

Rejeitada por aumentar a superfície de ataque e a complexidade de renovação, revogação e proteção do token em uma aplicação web same-origin.

### Adotar microsserviços

Rejeitada nesta fase. A clínica única, a equipe pequena e a implantação doméstica não justificam custo operacional, rede distribuída e consistência entre múltiplos serviços.

### Criar portal do paciente agora

Rejeitada nesta fase. O portal adicionaria recuperação de identidade externa, autorização por titular, exposição de documentos e fluxos adicionais de privacidade antes de os processos internos estarem consolidados.

### Publicar diretamente frontend, API e banco em portas distintas

Rejeitada. Apenas o reverse proxy/túnel deverá ser o ponto de entrada, mantendo os demais serviços em rede interna.

### Manter dados mock como fallback automático

Rejeitada porque pode apresentar sucesso falso e perda silenciosa de dados em uma operação clínica real.

## Consequências

### Positivas

- Menor superfície pública;
- autorização adequada por função;
- melhor proteção dos dados clínicos;
- estrutura modular sem complexidade de microsserviços;
- banco evoluído de forma rastreável;
- maior confiabilidade operacional;
- caminho claro para testes, auditoria e manutenção.

### Negativas e custos

- Parte relevante do backend e da integração frontend precisará ser reorganizada;
- mocks e funcionalidades apenas visuais deixarão de aparentar completude;
- serão necessários processos de backup, recuperação e atualização;
- permissões e auditoria aumentam o esforço de cada funcionalidade;
- a hospedagem residencial continuará tendo riscos de energia, conectividade e disponibilidade.

## Critérios de revisão desta decisão

Este ADR deverá ser revisto se ocorrer qualquer uma destas mudanças:

- criação de portal do paciente;
- atendimento de mais de uma clínica ou unidade juridicamente independente;
- aplicativo móvel ou API para terceiros;
- migração para identidade corporativa externa como fonte principal;
- mudança da infraestrutura doméstica para serviço gerenciado;
- crescimento que justifique separar algum módulo em serviço independente.
