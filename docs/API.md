# Contrato Atual da API

## Convenções

- Base: `/api`;
- JSON em camelCase e enums como texto;
- autenticação por cookie de sessão, sem token no `localStorage`;
- operações privadas que alteram estado exigem o header `X-CSRF-TOKEN`;
- obtenha o token em `GET /api/auth/csrf`, preservando cookie e token na mesma sessão;
- `401` indica ausência ou invalidade da sessão;
- `403` indica permissão insuficiente ou onboarding de segurança pendente;
- `423` indica conta temporariamente bloqueada;
- falhas padronizadas usam `application/problem+json`;
- listagens paginadas recebem `page` e `pageSize` e retornam itens e totais.

A API recebe DTOs validados; entidades de persistência não são contratos HTTP.

## Saúde

- `GET /` — identificação mínima do serviço;
- `GET /health` — healthcheck sem detalhes internos.

## Autenticação e onboarding

- `GET /api/auth/csrf` — emite o token antiforgery;
- `POST /api/auth/login` — inicia a sessão ou informa que o segundo fator é necessário;
- `POST /api/auth/login/mfa` — conclui login com código TOTP;
- `POST /api/auth/login/recovery` — conclui login com um código de recuperação de uso único;
- `GET /api/auth/me` — retorna usuário, papéis, permissões e pendências de segurança;
- `POST /api/auth/logout` — encerra a sessão;
- `POST /api/auth/change-password` — troca a senha, inclusive a temporária obrigatória;
- `POST /api/auth/mfa/setup` — cria/fornece chave e URI para o autenticador;
- `POST /api/auth/mfa/enable` — valida o código, ativa MFA e retorna oito códigos de recuperação uma única vez;
- `POST /api/auth/mfa/recovery-codes` — invalida os códigos anteriores e gera um novo conjunto.

Administrador e profissional ficam sem acesso às operações de negócio enquanto não concluírem troca de senha e MFA. Não existe credencial padrão no código.

## Superfície pública

- `GET /api/public/cms`;
- `GET /api/public/procedures`;
- `POST /api/public/leads` — rate limited;
- `GET /api/public/photos`;
- `GET /api/public/photos/{photoId}/content`.

Somente fotos explicitamente públicas e consentidas são retornadas.

## Superfície privada

### Clientes e conteúdo clínico

- `GET /api/clients`;
- `GET /api/clients/{id}`;
- `POST /api/clients`;
- `PUT /api/clients/{id}`;
- `DELETE /api/clients/{id}` — exclusão lógica;
- `GET /api/clients/{id}/clinical`;
- `GET /api/clients/{id}/summary` — resumo do paciente, limitado a Administradora e Profissional;
- `POST /api/clients/{id}/medical-records`;
- `POST /api/clients/{id}/photos`;
- `GET /api/clients/{id}/photos/{photoId}/content`;
- `POST /api/clients/{id}/documents`;
- `GET /api/clients/{id}/documents/{documentId}/content`;
- `GET /api/clients/{id}/storage-usage`.

Procedimentos e clientes são inativados logicamente. A remoção de um agendamento o marca como `Cancelled`, preservando o histórico assistencial e financeiro.

Fotos aceitam JPG, PNG ou WEBP até 25 MB e 32 megapixels. O servidor corrige a orientação, limita a maior dimensão a 2560 px, reencoda em JPEG e descarta metadados da imagem. Documentos aceitam PDF, DOCX, XLSX ou CSV UTF-8 até 15 MB. Nome, extensão, assinatura e estrutura interna do conteúdo são tratados no servidor; documentos Office com macros são rejeitados.

Fotos e documentos recebem tamanho persistido e hash SHA-256. A quota padrão é de 500 MB por paciente e 20 GB por instalação, configurável por `FileStorage:MaxClientBytes` e `FileStorage:MaxInstallationBytes`. O endpoint de uso permite apresentar os dois níveis no prontuário. Leituras clínicas e alterações são auditadas.

### Modelos de formulários clínicos

- `GET /api/forms` — lista o catálogo; `includeArchived=true` inclui modelos arquivados;
- `GET /api/forms/{id}` — retorna o rascunho atual e o histórico de versões;
- `GET /api/forms/{id}/versions/{versionNumber}` — retorna uma versão publicada somente para leitura;
- `POST /api/forms` — cria um rascunho;
- `PUT /api/forms/{id}/draft` — salva o rascunho com controle otimista por `draftRevision`;
- `POST /api/forms/{id}/publish` — congela o schema atual em uma nova versão;
- `POST /api/forms/{id}/duplicate` — cria outro rascunho a partir do modelo;
- `POST /api/forms/{id}/archive` — arquiva o modelo sem remover versões publicadas.

O schema aceita seções, texto informativo, textos curto e longo, número, data, sim/não, checkbox, grupos de opções, dropdown, seleção múltipla e assinatura presencial. Identificadores de campos e opções são estáveis e únicos; o servidor limita o schema a 100 campos, 50 opções por campo e 128 KB. Cada publicação persiste uma cópia imutável com número sequencial e hash SHA-256. Alterações posteriores ocorrem apenas no rascunho e geram outra versão.

As permissões são separadas em `forms.read` e `forms.manage`. O papel profissional recebe ambas por padrão; a recepção não recebe acesso a modelos clínicos sem concessão explícita.

### Preenchimentos e assinatura presencial

- `GET /api/forms/available` — lista a versão publicada mais recente de cada modelo disponível;
- `GET /api/clients/{clientId}/form-submissions` — lista os preenchimentos do prontuário;
- `GET /api/clients/{clientId}/form-submissions/{submissionId}` — retorna schema congelado, respostas, evidências e adendos;
- `POST /api/clients/{clientId}/form-submissions` — cria um rascunho e fixa a versão, com atendimento opcional;
- `PUT /api/clients/{clientId}/form-submissions/{submissionId}/draft` — salva respostas enquanto o registro estiver em rascunho;
- `POST /api/clients/{clientId}/form-submissions/{submissionId}/finalize` — valida respostas e assinaturas e congela o preenchimento;
- `POST /api/clients/{clientId}/form-submissions/{submissionId}/amend` — registra nova fotografia das respostas, justificativa e novas evidências sem sobrescrever o original;
- `POST /api/clients/{clientId}/form-submissions/{submissionId}/void` — anula logicamente e preserva o histórico;
- `GET /api/clients/{clientId}/form-submissions/{submissionId}/signatures/{signatureId}/content` — entrega o SVG gerado pelo servidor em resposta privada sem cache.

Novos preenchimentos usam somente a versão publicada mais recente. O vínculo opcional de atendimento é aceito apenas quando ele pertence ao mesmo cliente. A API rejeita campos desconhecidos, tipos e opções incompatíveis, respostas maiores que 256 KB, assinaturas duplicadas ou excessivamente complexas e finalização sem campos obrigatórios.

Os estados são `Draft`, `Finalized`, `Amended` e `Voided`. `revision` implementa concorrência otimista. Uma finalização não pode voltar a ser editada: correções exigem um adendo justificado, com o conteúdo original e a cadeia de hashes preservados. A evidência presencial contém traços normalizados, SVG produzido pelo servidor, nome e declaração do signatário, método, data UTC, condutor e hashes do schema, das respostas e da própria assinatura.

Leitura exige `clinical.read`; criação, edição, finalização, adendo e anulação exigem `clinical.manage`. A recepção não recebe essas permissões por padrão. Respostas e traços não são copiados para a auditoria.

### Agenda, catálogo, leads, CMS e relatórios

- `GET|POST /api/appointments`;
- `PUT /api/appointments/{id}`;
- `DELETE /api/appointments/{id}` — cancela o agendamento, preservando o histórico;
- `PUT /api/appointments/{id}/status`;
- `GET|POST /api/procedures`;
- `PUT /api/procedures/{id}`;
- `DELETE /api/procedures/{id}` — inativa o procedimento, sem apagar seus vínculos;
- `GET /api/leads`;
- `PUT /api/cms`;
- `GET /api/reports/dashboard` — indicadores da clínica, status mensais da agenda, próximos sete dias, aniversariantes e ranking de procedimentos.

Horários recebidos como hora local da clínica são convertidos para UTC usando `Clinic:TimeZoneId`.

### Financeiro e caixa

- `GET /api/finance/overview?from=AAAA-MM-DD&to=AAAA-MM-DD`;
- `GET|POST /api/finance/entries`;
- `PUT /api/finance/entries/{id}`;
- `POST /api/finance/entries/{id}/settle`;
- `DELETE /api/finance/entries/{id}` — cancela o lançamento de forma lógica, sem remover sua trilha de auditoria.

O resumo deriva os recebimentos e saldos de atendimentos não cancelados e combina receitas/despesas manuais realizadas ou previstas. As permissões `finance.read` e `finance.manage` são atribuídas por padrão apenas a `Admin` e `Professional`.

### Usuários, acessos e auditoria

- `GET|POST /api/users`;
- `GET /api/users/access-catalog`;
- `PUT /api/users/{id}/access`;
- `PUT /api/users/{id}/status`;
- `POST /api/users/{id}/reset-password`;
- `GET /api/audit`.

A recuperação administrativa gera uma senha temporária aleatória, exibida uma única vez, força nova troca e invalida sessões anteriores. A API impede desativar o último administrador ativo.

## Papéis padrão

- `Admin`: todas as permissões;
- `Professional`: clientes, dados clínicos, agenda, procedimentos, leads, dashboard e financeiro;
- `Receptionist`: cadastro básico, agenda, procedimentos, leads e dashboard, sem dados clínicos.

Concessões e negações individuais podem sobrescrever o padrão do papel e são auditadas.
