# ADR-001: Adoção de .NET 8 Minimal APIs para o Backend

## Status
Aprovado

## Contexto
O projeto **PainelEstetica** necessita de uma API resiliente, de altíssimo desempenho e fácil manutenção para servir tanto a Landing Page pública de alta conversão quanto o Painel Administrativo de gestão da clínica. A arquitetura anterior dependia de Controllers MVC pesados com boilerplate extenso.

## Decisão
Decidimos migrar e construir a API utilizando **.NET 8 Minimal APIs**, estruturada com o padrão de Handlers/Endpoints modularizados (`EndpointRouteBuilder`).

### Principais Diretrizes da Arquitetura:
1. **Mapeamento Modular por Recursos**:
   - `ClientEndpoints.cs` (`/api/clients`)
   - `AppointmentEndpoints.cs` (`/api/appointments`)
   - `ProcedureEndpoints.cs` (`/api/procedures`)
   - `CmsEndpoints.cs` (`/api/cms`)
   - `LeadEndpoints.cs` (`/api/leads`)
   - `ReportEndpoints.cs` (`/api/reports`)
2. **Validação & Pipeline**:
   - Utilização de `FluentValidation` embutido nos handlers de rotas.
   - Filtros de rota (`AddEndpointFilter`) para validação de DTOs e tratamento de exceções global.
3. **Documentação Nativa OpenAPI / Swagger**:
   - Mapeamento explícito com `.WithSummary()`, `.WithTags()`, `.Produces<T>()`.

## Consequências
### Positivas:
- **Redução drástica de boilerplate**: Eliminação de classes Controller verbosas.
- **Desempenho Superior**: Menos alocação de memória por requisição HTTP.
- **Leitura e Testabilidade**: Endpoints concentram a lógica de transporte de forma desacoplada dos UseCases/Services.

### Negativas / Riscos:
- Necessidade de manter organização estrita dos arquivos de extensão de rotas para evitar um `Program.cs` gigante.
