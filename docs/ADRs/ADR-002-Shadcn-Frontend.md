# ADR-002: Frontend com React, Vite, Tailwind CSS v4 e shadcn/ui

## Status
Aprovado

## Contexto
Tanto a **Landing Page de Captação de Leads** quanto o **Painel Admin da Clínica de Estética** precisam passar uma imagem de alto nível, sofisticação, profissionalismo e confiabilidade. Estilos genéricos ou componentes padrão de navegadores não atendem aos critérios de UX/UI para o mercado de estética de luxo.

## Decisão
Adotar **React + Vite + TypeScript**, combinados com **Tailwind CSS v4** e a biblioteca de componentes **shadcn/ui** (baseada em primitivos acessíveis Radix UI).

### Pilares da Interface:
1. **Design System Personalizado (Estética Luxo)**:
   - Paleta de cores Tailored: Rose Gold `#E07A5F`, Warm Cream `#FBF8F5`, Slate/Charcoal `#0F172A`.
   - Tipografia: *Outfit* para títulos elegantes, *Inter* para legibilidade de dados médicos/prontuários.
2. **Primitivos shadcn/ui Reutilizáveis**:
   - `Button`, `Card`, `Dialog` (Modais), `Select`, `Calendar`, `Table`, `Badge`, `Carousel`, `Tabs`.
3. **Animações e Interatividade**:
   - Transições suaves no carrossel de procedimentos.
   - Modais responsivos para ficha de anamnese e upload de arquivos.
   - Indicadores visuais de status (Agendado, Concluído, Faltou, Pago, Pendente).

## Consequências
### Positivas:
- Interface extremamente moderna e impressionante à primeira vista ("WOW factor").
- Total controle sobre os arquivos dos componentes (código-fonte reside na pasta `components/ui`).
- Acessibilidade WAI-ARIA garantida pelos primitivos Radix UI.
- Performance impecável com Vite e bundling mínimo.
