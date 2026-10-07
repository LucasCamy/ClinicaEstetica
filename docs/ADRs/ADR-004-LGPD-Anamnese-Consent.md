# ADR-004: LGPD, Prontuário com Anamnese e Consentimento para Fotos

## Status
Superado pelo ADR-006. Mantido apenas como registro histórico; suas promessas não representam o estado atual do produto.

## Contexto
Na área da estética, lida-se com dados de saúde (sensíveis) e fotos corporais/faciais. A Lei Geral de Proteção de Dados (LGPD - Lei nº 13.709/2018) exige consentimento explícito para coleta de dados de saúde e uso de imagem para fins de portfólio.

## Decisão
Implementar mecanismos nativos no sistema para garantia legal da clínica e dos profissionais de estética:

1. **Termo de Consentimento e Aceite Digital na Anamnese**:
   - Cada Ficha de Anamnese registra o IP, Timestamp UTC e Flag de Aceite explícito do Termo de Consentimento para Procedimentos Estéticos.
   - Assinatura digital/desenho ou confirmação via código do cliente.
2. **Segregação de Fotos de Acompanhamento (Antes/Depois)**:
   - Toda foto possui a flag `IsPublicForWebsite`.
   - Fotos são armazenadas por padrão como **PRIVADAS** (visíveis apenas no Prontuário pelo Admin/Profissional).
   - O toggle para tornar a foto **PÚBLICA** (exibida no Carrossel/Galeria do site) exige a marcação de aceite de consentimento de imagem pelo paciente.
3. **Auditoria de Acessos**:
   - Registros de upload e visualização de prontuários com timestamp e identificação do usuário logado.

## Consequências
- Proteção jurídica integral para a clínica de estética em caso de contestações.
- Transparência e ética com o cliente final.
