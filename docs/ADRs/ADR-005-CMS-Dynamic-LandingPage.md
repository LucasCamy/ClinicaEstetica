# ADR-005: CMS Integrado para Gestão Dinâmica da Landing Page

## Status
Aprovado

## Contexto
A clínica de estética frequentemente lança novos procedimentos, altera valores de tratamentos em destaque, atualiza fotos de perfil da profissional ou insere banners promocionais. Alterar o código-fonte a cada mudança é inviável para o modelo de negócios da cliente.

## Decisão
Desenvolver um módulo de **CMS (Content Management System) Integrado** que permite controlar a Landing Page em tempo real a partir do Painel Administrativo.

### Componentes Gerenciáveis pelo Admin:
1. **Procedimentos em Destaque no Carrossel**:
   - Marcar procedimentos com `IsPublicWebsite = true`.
   - Adicionar banner, preço a partir de, duração e breve descrição comercial.
2. **Dados do Perfil Profissional**:
   - Bio da Dra./Esteticista, frase de efeito, foto de apresentação, especialidades e certificados.
3. **Seção de Depoimentos & Avaliações**:
   - Cadastro e moderação de depoimentos de clientes (Nome, Depoimento, Estrelas, Tratamento Realizado).
4. **Galeria Pública (Antes/Depois)**:
   - Exibição dinâmica apenas de fotos autorizadas com consentimento.
5. **Configurações Globais**:
   - Número do WhatsApp de atendimento, link de mapa/endereço, horários de funcionamento, aviso da barra de topo (ex: "Promoção de Botox este mês").

## Consequências
- Autonomia total para a cliente de estética atualizar o conteúdo do site sem programador.
- Landing Page sempre atualizada com ofertas e tratamentos recentes.
