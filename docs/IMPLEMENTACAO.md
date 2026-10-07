# Estado da Implementação

## Fases 0 e 1 — concluídas

- Linha de base segura, secrets por ambiente e dependências atualizadas;
- separação entre superfícies pública e privada;
- rate limit de login e captação;
- uploads privados e remoção de conteúdo clínico do web root;
- fronteiras Docker e cabeçalhos do Nginx;
- CI, typecheck e testes de integração.

## Fase 2 — concluída

- Monólito modular com projetos `Domain`, `Application`, `Infrastructure`, `WebAPI` e `DatabaseMigrator`;
- endpoints separados por módulo e regras de negócio movidas para serviços de aplicação;
- DTOs, validação, paginação, respostas padronizadas e tratamento de erros;
- relógio abstrato e fuso configurável da clínica;
- relacionamentos, índices, limites, unicidade e exclusão lógica mapeados;
- migrations EF Core para banco limpo e adaptador transacional para o schema legado;
- testes Testcontainers comprovando criação limpa e preservação de dados legados.

## Fase 3 — concluída

- ASP.NET Core Identity e sessão por cookie seguro;
- antiforgery nas mutações privadas;
- política de senha, bloqueio de tentativas, senha temporária e invalidação de sessões;
- MFA TOTP obrigatório para administrador e profissional, com códigos de recuperação de uso único;
- papéis `Admin`, `Professional` e `Receptionist`, permissões granulares e sobrescritas individuais;
- criação, edição de acesso, desativação, reativação e reset administrativo de usuários;
- proteção contra desativação do último administrador;
- trilha de auditoria com ator, IP, user-agent, recurso, resultado e horário;
- interface administrativa de usuários e onboarding de segurança;
- testes automatizados da matriz de acesso e das fronteiras clínicas.

Há 42 testes automatizados. Eles cobrem autenticação, CSRF, MFA, RBAC, auditoria, desativação de sessão, migrations em PostgreSQL, validação estrutural de arquivos clínicos, normalização de fotos, hashes, quotas e os ciclos completos de modelos e preenchimentos versionados.

## Fase 5 — modelos e arquivos clínicos em andamento

- ADR-007 registra formulários clínicos versionados, arquivos privados, controle de volume e assinatura presencial desenhada;
- ciclo de foco das modais corrigido para não interromper a digitação durante atualizações de estado;
- fotos e documentos deixaram de ser simulados no frontend e passaram a usar os endpoints privados persistentes;
- documentos PDF, DOCX, XLSX e CSV UTF-8 aceitos por lista restritiva e limite de 15 MB;
- assinatura de PDF, estrutura interna de Office, limite de expansão, macros e conteúdo binário disfarçado de CSV validados no servidor;
- seletores reais, estados de envio, erros e links autenticados de abertura/download adicionados ao prontuário;
- fotos recebidas em JPG, PNG ou WEBP são decodificadas, orientadas, reduzidas para no máximo 2560 px e reencodadas em JPEG sem reaproveitar EXIF;
- limite adicional de 32 megapixels protege a etapa de decodificação;
- tamanho original/final, dimensões, tipo MIME e SHA-256 são persistidos na nova migration `AddClinicalFileMetadata`;
- quotas configuráveis de 500 MB por paciente e 20 GB por instalação são verificadas no servidor antes da persistência;
- o prontuário exibe consumo por paciente e da instalação, além do tamanho de cada arquivo;
- a rotina de imagem e os controles de quota foram executados também em Linux, no mesmo perfil de sistema operacional do contêiner de produção;
- modelos de formulários ganharam catálogo, rascunho editável, duplicação, arquivamento e permissões próprias de leitura e gestão;
- o construtor visual oferece 12 tipos de campo, opções dinâmicas, obrigatoriedade, reordenação, duplicação e pré-visualização;
- campos e opções mantêm identificadores estáveis, com validação de tamanho, quantidade, unicidade e tipos no servidor;
- cada publicação congela metadados e schema em uma versão imutável, numerada e identificada por hash SHA-256;
- concorrência de edição é detectada por revisão do rascunho, evitando sobrescrita silenciosa;
- histórico e visualização somente para leitura permitem conferir versões anteriores no painel;
- o fluxo foi validado na stack Docker real com duas versões: a alteração da versão 2 não modificou o conteúdo preservado na versão 1;
- preenchimentos foram vinculados ao cliente, opcionalmente ao atendimento e sempre à versão exata publicada;
- respostas são validadas novamente no servidor e possuem limite de 256 KB, hash SHA-256 e concorrência otimista por revisão;
- finalização congela o conteúdo; correções posteriores usam adendos numerados e justificados sem sobrescrever a fotografia original;
- anulação lógica preserva respostas, assinaturas, autoria, datas e cadeia de hashes;
- campos de assinatura abrem um modo dedicado em tela cheia para tablet, com canvas para toque, caneta ou mouse, confirmação explícita e aviso sobre modo quiosque do dispositivo;
- a API persiste traços vetoriais normalizados, SVG seguro gerado no servidor, declaração, método, condutor, horário UTC e hashes do schema, respostas e assinatura;
- a migration `AddFormSubmissionsAndSignatures` cria os relacionamentos, índices e colunas `jsonb` necessários;
- o fluxo automatizado cobre criação, listagem, rascunho, rejeição de assinatura inválida, finalização, leitura do SVG autenticado, bloqueio de sobrescrita, adendo e anulação;
- miniaturas, alertas operacionais, reconciliação de arquivos órfãos e migração da anamnese legada permanecem para as próximas fatias.

## Validação Docker limpa — 5 de agosto de 2026

- Stack anterior e volumes descartáveis removidos pelo escopo do projeto Compose;
- banco recriado do zero exclusivamente pelas migrations versionadas;
- migrador finalizado com código zero e todos os serviços ficaram saudáveis;
- PostgreSQL e API confirmados sem portas publicadas no host;
- login inicial, troca obrigatória de senha, MFA e novo login com segundo fator validados;
- oito códigos de recuperação gerados, login alternativo validado e reutilização bloqueada por teste;
- criação de recepcionista, cliente, lead e agendamento executada contra a stack real;
- bloqueios de prontuário e relatórios para recepcionista confirmados com `403`;
- auditoria administrativa confirmada;
- modo HTTP local validado com cookies e antiforgery normais, mediante opção explícita e desabilitada por padrão.
- o financeiro passou a separar recebimentos de atendimentos, lançamentos manuais de receita/despesa, contas a receber e projeção de caixa; lançamentos cancelados permanecem auditáveis.

## Próximo ciclo

A Fase 4 continuará com os fluxos operacionais completos de clientes, leads e agenda. Na Fase 5, as próximas fatias são a migração comparável da anamnese legada, miniaturas, alertas de capacidade e reconciliação de arquivos órfãos. Antes de cadastrar pacientes reais, ainda é obrigatório concluir o marco operacional de segurança: backup automático externo e criptografado, restauração testada, configuração Cloudflare/HTTPS, aviso de privacidade e revisão de acessos.

O detalhamento está no [Plano de Implementação](./PLANO_DE_IMPLEMENTACAO.md).
