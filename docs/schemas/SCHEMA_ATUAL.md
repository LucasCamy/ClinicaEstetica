# Schema Atual e Compatibilidade Legada

## Estratégia vigente

O schema é versionado exclusivamente por migrations do Entity Framework Core. `EnsureCreated` não é usado pela aplicação. Em Docker, `PainelEstetica.DatabaseMigrator` executa antes da API e a API de produção se recusa a aplicar migrations durante sua própria inicialização.

Migrations atuais:

- `20260805185052_InitialSchema` — linha de base completa;
- `20260805191152_UseDateForClientBirthDate` — converte nascimento de timestamp para `date`;
- `20260805205041_AddClinicalFileMetadata` — metadados, hashes, dimensões e tamanhos de arquivos clínicos;
- `20260805214053_AddVersionedForms` — modelos, rascunhos e versões imutáveis de formulários;
- `20260806134613_AddFormSubmissionsAndSignatures` — preenchimentos, adendos e assinaturas presenciais.

## Modelo

Além dos módulos de clientes, anamnese, prontuário, arquivos, agenda, procedimentos, leads e CMS, o modelo contém:

- tabelas ASP.NET Core Identity para usuários, papéis, claims, logins e tokens;
- `UserPermissionOverrides` para concessões e negações individuais;
- `AuditEvents` para a trilha de segurança e acesso;
- `FormTemplates` e `FormVersions` para o catálogo e schemas publicados imutáveis;
- `FormSubmissions`, `FormSubmissionAmendments` e `FormSignatures` para respostas clínicas, correções rastreáveis e evidências presenciais;
- respostas, traços e schemas variáveis em `jsonb`, mantendo vínculos, estados, autores, horários e hashes em colunas relacionais;
- constraints, índices, tamanhos máximos, precisão decimal e relacionamentos explícitos;
- exclusão lógica de clientes e filtro global correspondente.

O nome físico `Users` e alguns nomes de colunas legados foram preservados deliberadamente para permitir atualização sem perda de dados.

## Adoção do banco legado

`LegacyDatabaseAdapter` detecta um banco criado pelo protótipo quando existe `Users` mas não existe histórico de migrations. Em uma única transação ele:

1. adiciona colunas e tabelas necessárias para Identity, permissões e auditoria;
2. cria os papéis padrão e converte papéis antigos conhecidos;
3. associa usuários aos novos papéis;
4. cria índices e chaves estrangeiras compatíveis;
5. registra a migration inicial como aplicada;
6. permite que o EF aplique as migrations posteriores normalmente.

Testes automatizados executam tanto o banco vazio quanto um fixture completo do schema anterior em PostgreSQL descartável e verificam a preservação dos registros e da data de nascimento.

## Regra de preservação

O adaptador automatizado reduz o risco técnico, mas não substitui backup. Nenhuma migration deve ser aplicada ao volume operacional antes de backup verificado e ensaio sobre uma restauração isolada. O rollback previsto é restaurar banco e arquivos a partir desses backups.
