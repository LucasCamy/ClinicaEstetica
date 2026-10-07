# Estrutura do Projeto

O backend é um monólito modular: uma unidade de implantação com limites internos explícitos.

```text
PainelEstetica/
├── backend/
│   ├── PainelEstetica.sln
│   ├── .config/dotnet-tools.json
│   ├── src/
│   │   ├── Domain/              # entidades e regras essenciais
│   │   ├── Application/         # contratos, DTOs, permissões e casos de uso
│   │   ├── Infrastructure/      # EF Core, Identity, migrations e serviços
│   │   ├── WebAPI/              # transporte HTTP, auth e módulos de endpoints
│   │   └── DatabaseMigrator/    # atualização controlada do schema
│   └── tests/
│       └── PainelEstetica.IntegrationTests/
├── frontend/
│   ├── src/
│   │   ├── components/
│   │   ├── contexts/
│   │   ├── pages/
│   │   └── services/
│   ├── Dockerfile
│   └── nginx.conf
├── docs/
├── .env.example
└── docker-compose.yml
```

As dependências seguem `Domain <- Application <- Infrastructure/WebAPI`. A API referencia os projetos internos, registra as implementações e divide rotas por módulo; endpoints não acessam regras de negócio por meio de entidades recebidas diretamente.

Consulte o [ADR-006](../ADRs/ADR-006-Arquitetura-Clinica-Unica-Seguranca-e-Acessos.md) e o [Plano de Implementação](../PLANO_DE_IMPLEMENTACAO.md).
