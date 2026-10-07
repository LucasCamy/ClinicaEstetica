# Mapeamento de Campo, Processos Operacionais e Métricas de Negócio na Estética

Este documento detalha o funcionamento operacional real de uma clínica de estética profissional, orientando o fluxo do software para atender a **Secretária**, a **Profissional de Estética** e a **Gestora do Negócio**.

---

## 1. Fluxo de Atendimento Operacional

```
[Lead / Cliente] ──> (Landing Page / WhatsApp) ──> [Agendamento Recepção]
                                                           │
                                                           ▼
[Caixa & Fechamento] <── [Sessão / Prontuário] <── [Atendimento Estético & Anamnese]
```

### A. Papel: Recepção & Secretária
* **Captação & Primeiro Contato**:
  - Atende os leads vindos do formulário do site ou WhatsApp.
  - Cadastra os dados básicos do cliente e a origem ("De onde veio": Instagram, Indicação, Google, Evento).
* **Gestão da Agenda**:
  - Seleciona o procedimento e verifica o tempo de duração estimado (ex: Limpeza de pele = 60 min, Botox = 30 min).
  - Altera status do agendamento: `Agendado` ➔ `Confirmado` (após envio de lembrete) ➔ `Presente` (quando chega na clínica) ou `Faltou / Cancelado`.
* **Cobrança & Caixa da Recepção**:
  - Registra o pagamento ao final ou início do atendimento (PIX, Cartão de Crédito/Débito, Dinheiro, Pacote).
  - Emite o comprovante/recibo e faz o acerto de caixa diário.

---

### B. Papel: Profissional de Estética (Dermatofuncional / Cosmiatra / Esteticista)
* **Preenchimento do Prontuário Eletrônico**:
  - Revisa o histórico de saúde na **Ficha de Anamnese** antes de aplicar qualquer produto ou equipamento.
  - Coleta a assinatura do Termo de Consentimento para o procedimento do dia.
* **Registro de Sessão & Parâmetros Técnicos**:
  - Registra a área tratada, dosagem/parâmetros (ex: energia do laser, tipo de ácido e porcentagem, agulhamento).
  - Anota observações da reação cutânea ou recomendações pós-procedimento dadas à cliente.
* **Acompanhamento Fotográfico (Evolução)**:
  - Tira fotos padronizadas do local (Antes da 1ª sessão, Durante o tratamento, Depois ao concluir).
  - Anexa as fotos à pasta do cliente com data e observação.

---

## 2. Métricas de Negócio & KPIs Financeiros

Para garantir a saúde financeira e o crescimento da clínica, o sistema calcula e exibe no Dashboard:

### 📈 KPIs Operacionais & Comercial
1. **Taxa de Comparecimento (Attendance Rate)**:
   $$\text{Taxa de Comparecimento} = \frac{\text{Atendimentos Concluídos}}{\text{Total de Agendamentos}} \times 100$$
2. **Taxa de Faltas (No-Show Rate)**:
   - Permite identificar horários ou tratamentos com alta taxa de desistência para adotar políticas de sinal/reserva de horário.
3. **Métrica de Origem de Leads (Atração)**:
   - Quantidade e faturamento gerados por canal (`Instagram`, `Indicação`, `Google Ads`, `WhatsApp`).
   - Identifica onde vale a pena investir mais marketing.

### 💰 KPIs Financeiros & Caixa
1. **Faturamento Total Diário / Mensal**:
   - Soma de todas as entradas efetivadas por método de pagamento.
2. **Ticket Médio por Cliente / Procedimento**:
   - Identifica quais tratamentos são mais rentáveis por hora de trabalho da clínica.
3. **Extrato de Pendências / Contas a Receber**:
   - Clientes que realizaram procedimento com pagamento parcial ou parcelado.

### 🔄 KPIs de Retenção & Relacionamento (CRM)
1. **Clientes Inativos (> 30 / 60 / 90 dias)**:
   - Lista de clientes que não retornaram após concluir o tratamento, para campanhas de reconquista.
2. **Aniversariantes do Mês**:
   - Notificações automáticas na tela da secretária para envio de felicitações e cupom presente.
