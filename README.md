# PainelEstetica

Sistema web para a operação de uma clínica de estética, com landing page pública e painel privado para administrador, profissional e recepcionista.

> **Estado atual:** as Fases 0 a 3 estão implementadas e a Fase 5 já possui armazenamento clínico privado, modelos versionados, preenchimentos vinculados ao prontuário e assinatura eletrônica presencial. O sistema **ainda não está liberado para dados reais de pacientes**, pois faltam os controles operacionais descritos no [marco de segurança](./docs/PLANO_DE_IMPLEMENTACAO.md#marco-de-segurança-para-dados-reais), especialmente backup externo criptografado e restauração testada.

## Escopo

- Uma clínica e uma profissional responsável;
- landing page como única superfície pública;
- captação por formulário, WhatsApp e Instagram;
- painel interno com clientes, agenda, anamnese, prontuário, pagamentos, relatórios e CMS;
- construtor de formulários clínicos com rascunhos e versões imutáveis;
- preenchimentos clínicos com adendos, anulação e assinatura presencial em tablet;
- usuários internos com permissões diferentes;
- sem portal do paciente nesta etapa;
- implantação inicial com Docker e Cloudflare Tunnel.

## Tecnologia e arquitetura

- Frontend: React, TypeScript, Vite e Tailwind CSS;
- backend: ASP.NET Core 8 Minimal API, ASP.NET Core Identity e Entity Framework Core;
- banco: PostgreSQL 16 com migrations versionadas;
- entrega: Docker Compose, migrador de execução única e Nginx;
- organização: monólito modular dividido em `Domain`, `Application`, `Infrastructure`, `WebAPI` e `DatabaseMigrator`.

As decisões arquiteturais e os limites do produto estão no [ADR-006](./docs/ADRs/ADR-006-Arquitetura-Clinica-Unica-Seguranca-e-Acessos.md). Formulários clínicos versionados, arquivos privados e assinatura presencial estão detalhados no [ADR-007](./docs/ADRs/ADR-007-Formularios-Versionados-Arquivos-Clinicos-e-Assinatura-Presencial.md). A primeira camada de caixa, contas a receber e lançamentos manuais está registrada no [ADR-008](./docs/ADRs/ADR-008-Caixa-e-Lancamentos-Financeiros.md).

## Segurança implementada

- Sessão em cookie `HttpOnly`, `SameSite=Strict` e `Secure` em produção;
- antiforgery em todas as operações privadas que alteram estado;
- ASP.NET Core Identity, bloqueio de tentativas e política de senha;
- troca obrigatória da senha temporária;
- MFA TOTP obrigatório para administrador e profissional, com códigos de recuperação de uso único;
- papéis, permissões granulares e exceções individuais de acesso;
- recepcionista sem acesso padrão a anamnese, prontuário ou arquivos clínicos;
- permissões separadas para leitura e gestão de modelos de formulários;
- respostas clínicas e assinaturas protegidas pelas permissões clínicas e por auditoria;
- auditoria de autenticação, dados clínicos e alterações administrativas;
- uploads clínicos privados, limitados e validados;
- chaves de proteção de dados persistidas fora da imagem;
- banco e API sem portas publicadas no Compose;
- testes de integração das fronteiras de segurança e das migrations.

Esses controles não constituem, isoladamente, declaração de conformidade integral com a LGPD.

## Executar com Docker

1. Copie `.env.example` para `.env`.
2. Defina senha do PostgreSQL e credenciais temporárias do primeiro administrador.
3. Execute:

```powershell
docker compose config
docker compose up --build -d
docker compose ps
```

O serviço `migrations` atualiza o schema antes da API. O frontend é vinculado, por padrão, somente a `127.0.0.1:3000`; PostgreSQL e backend permanecem na rede privada.

O volume clínico usa, por padrão, quota de 500 MB por paciente e 20 GB por instalação. Os valores podem ser alterados em `.env` por `FILE_STORAGE_MAX_CLIENT_BYTES` e `FILE_STORAGE_MAX_INSTALLATION_BYTES` conforme o disco e a política de backup. PDFs finalizados de termos e formulários também entram nessa quota e permanecem em armazenamento privado.

Formulários finalizados geram um PDF imutável no servidor, com a versão do formulário, respostas, assinaturas e hashes de integridade. A geração usa QuestPDF localmente; configure `PDF_QUESTPDF_LICENSE` como `Community`, `Professional` ou `Enterprise` conforme a licença aplicável à clínica.

Para testar o login diretamente por `http://127.0.0.1`, defina temporariamente `ALLOW_INSECURE_HTTP_COOKIES=true`. Mantenha essa opção `false` quando publicar pelo Cloudflare Tunnel ou permitir qualquer acesso fora do loopback.

No primeiro acesso, o administrador deve trocar a senha temporária e ativar MFA. Depois disso, remova `ADMIN_BOOTSTRAP_USERNAME`, `ADMIN_BOOTSTRAP_EMAIL` e `ADMIN_BOOTSTRAP_PASSWORD` do `.env`.

Não versione `.env` e não aplique migrations ao volume existente antes do procedimento de backup e ensaio descrito em [DEPLOY.md](./docs/DEPLOY.md).

## Desenvolvimento local

Pré-requisitos: .NET 8 SDK, Node.js 24, npm e PostgreSQL. Os segredos podem ser configurados com o Secret Manager:

```powershell
cd backend\src\WebAPI
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=estetica_dev;Username=estetica_app;Password=senha-local"
dotnet user-secrets set "BootstrapAdmin:Username" "administrador"
dotnet user-secrets set "BootstrapAdmin:Email" "admin@exemplo.local"
dotnet user-secrets set "BootstrapAdmin:Password" "uma-senha-temporaria-forte"
dotnet run
```

Em outro terminal:

```powershell
cd frontend
npm ci
npm run dev
```

## Verificações

```powershell
cd backend
dotnet restore PainelEstetica.sln --configfile nuget.config
dotnet test PainelEstetica.sln --configuration Release

cd ..\frontend
npm ci
npm run build
npm audit --audit-level=high
```

Os testes de migrations utilizam PostgreSQL isolado por Testcontainers e, portanto, precisam do Docker em execução.

## Documentação vigente

- [Estado da implementação](./docs/IMPLEMENTACAO.md);
- [contrato da API](./docs/API.md);
- [execução e implantação](./docs/DEPLOY.md);
- [schema e transição legada](./docs/schemas/SCHEMA_ATUAL.md);
- [plano de implementação](./docs/PLANO_DE_IMPLEMENTACAO.md).
