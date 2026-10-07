# ADR-012: Auditoria de Segurança, Defesa em Profundidade e Princípios de Movimento na Interface

## Status

Aceito em 30/08/2026.

## Contexto

O sistema **PainelEstetica** é responsável pela gestão clínica de uma clínica de estética, manipulando dados cadastrais, financeiros e dados pessoais altamente sensíveis (LGPD - dados de saúde, fichas de anamnese, fotografias de tratamentos estéticos e termos de consentimento com assinatura presencial).

Para assegurar a robustez da aplicação em ambiente de produção (hospedagem local exposta com segurança via Cloudflare Tunnel), faz-se necessária uma revisão ampla e a formalização dos pilares de **segurança em profundidade (Defense-in-Depth)** e de **experiência do usuário (UX / Motion Principles)**.

## Decisões

### 1. Arquitetura de Autenticação e Sessão
- **Armazenamento de Sessão**: Não utilização de tokens de autenticação ou chaves JWT em `localStorage` ou `sessionStorage`, prevenindo vulnerabilidades de exfiltração por XSS.
- **Cookies de Sessão Seguros**: Sessões baseadas em cookies com prefixo de isolamento `__Host-` em produção (`__Host-Clinica.Session`), flags `HttpOnly`, `SameSite=Strict`, `Secure=Always` e expiração deslizante controlada de 2 horas.
- **Proteção Antiforgery (CSRF)**: Header `X-CSRF-TOKEN` exigido em todas as rotas mutantes (`POST`, `PUT`, `DELETE`), emitido via cookie antiforgery `__Host-Clinica.Antiforgery`.
- **MFA Obrigatório e Senhas Fortes**: Obrigatoriedade de MFA (TOTP via Google Authenticator/Authy) e troca de senha no primeiro acesso para operadores e administradores. Política de senhas mínimas de 12 caracteres, com maiúsculas, minúsculas, dígitos e símbolos.

### 2. Defesa Contra Injeção e Mass Assignment
- **Contratos Tipados (DTOs)**: Nenhum endpoint recebe ou persiste diretamente entidades de domínio ou de banco de dados. Todas as operações utilizam classes ou records DTO tipados e validados via `ValidationFilter<T>` e DataAnnotations.
- **Consultas Parametrizadas**: Consultas realizadas exclusivamente através do LINQ / Entity Framework Core com provedor Npgsql, garantindo que todos os parâmetros de busca sejam tratados como valores literais seguros pelo PostgreSQL, sem concatenação manual de SQL.
- **Sanitização Global de Entradas**: Utilização de `JsonStringTrimConverter` global no pipeline do `System.Text.Json`, aparando automaticamente espaços em branco de strings em todas as requisições recebidas.

### 3. Proteção Anti-Bot e Limitação de Taxa (Rate Limiting)
- **Honeypot Silencioso**: Endpoints públicos de captação de leads (`/api/public/leads`) possuem campo honeypot oculto na interface (`Website` / `HpField`). Robôs que preenchem esse campo recebem resposta 202 Accepted, mas a requisição é descartada silenciosamente sem persistência nem envio de notificações.
- **Políticas de Rate Limit**:
  - `auth`: 5 requisições por minuto por IP, com bloqueio progressivo por falha via ASP.NET Identity Lockout (5 tentativas = 15 minutos de bloqueio).
  - `public-leads`: 5 envios por 10 minutos por IP.
  - `uploads`: 20 uploads por 10 minutos por usuário/IP.

### 4. Prevenção de Vazamento de Dados e Cabeçalhos de Segurança
- **Tratamento de Exceções**: `ExceptionHandlingMiddleware` intercepta falhas inesperadas e retorna `ProblemDetails` genérico, sem expor stack traces, strings de conexão ou esquemas de banco de dados.
- **Headers HTTP de Segurança**:
  - `Content-Security-Policy`: Políticas estritas para scripts, estilos e fontes.
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: strict-origin-when-cross-origin`
  - `Permissions-Policy: camera=(), microphone=(), geolocation=()`
  - `Cross-Origin-Opener-Policy: same-origin`
  - `Cache-Control: no-store` para rotas autenticadas da API.

### 5. Princípios de Movimento e Percepção de Desempenho (Motion Principles)
- **Skeletons de Alta Fidelidade**: Em vez de telas vazias ou spinners genéricos que causam Layout Shift (CLS), todas as visualizações exibem skeletons que replicam a geometria do conteúdo carregado (`SkeletonCard`, `SkeletonTable`, `SkeletonMetric`, `SkeletonForm`).
- **Lazy Loading e Divisão de Código**: Sub-módulos e páginas pesadas (Financeiro, Relatórios, Termos, Fichas, Palco de Assinatura PDF) são carregados sob demanda com `React.lazy` e `<Suspense>`.
- **Transições Suaves e Microinterações**: Aceleração e desaceleração naturais com curvas bezier padronizadas (`cubic-bezier(0.16, 1, 0.3, 1)`), transições suaves de entrada e saída.
- **Indicadores de Progresso**: Todas as operações assíncronas fornecem feedback imediato através de botões com spinners suaves e barras de progresso (`ProgressBar`).
- **Acessibilidade a Movimento**: Respeito à preferência do sistema operacional via `@media (prefers-reduced-motion: reduce)`, desativando transições desnecessárias para usuários com sensibilidade a movimento.

## Consequências

- O sistema atende plenamente aos critérios de segurança em conformidade com as boas práticas OWASP e LGPD.
- A experiência de navegação do painel torna-se fluida, com carregamento percebido veloz e sem sobressaltos visuais.
