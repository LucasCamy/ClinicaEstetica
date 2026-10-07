# ADR-008 — Caixa, contas a receber e lançamentos financeiros

- Status: aceito
- Data: 2026-08-06

## Contexto

Os atendimentos já registram valor contratado, valor pago e método de pagamento. Esses campos permitem identificar o saldo por paciente, mas não representam despesas, entradas que não pertencem a um atendimento, previsões ou um fechamento de caixa confiável.

O produto opera uma clínica única e ainda não é um sistema fiscal ou de conciliação bancária. A primeira camada financeira deve ser útil para a operação diária sem apresentar resultados estimados como se fossem dados conciliados.

## Decisão

1. O valor recebido e o saldo dos atendimentos continuam tendo o agendamento como fonte de verdade.
2. Uma tabela `FinancialEntries` registra somente receitas manuais e despesas, com tipo, categoria, descrição, valor, forma de pagamento, data efetiva, situação e autoria.
3. Lançamentos podem estar `Planned`, `Settled` ou `Cancelled`. Cancelamento é lógico: o registro e sua auditoria permanecem, mas deixam de compor o caixa.
4. O resumo financeiro calcula separadamente recebimentos de atendimentos, entradas manuais, despesas, caixa líquido, contas a receber, previsões e fluxo diário.
5. A ação de registrar recebimento altera o valor acumulado pago no próprio atendimento e reaplica a regra existente de que o pago não pode exceder o contratado.
6. Leitura e gestão do financeiro usam permissões próprias, concedidas por padrão apenas a administradora e profissional. Todas as alterações financeiras são auditadas.

## Consequências

- A clínica consegue acompanhar caixa e despesas sem duplicar o valor já mantido no atendimento.
- Um lançamento cancelado pode ser explicado em auditoria e não desaparece do histórico.
- O módulo não emite nota fiscal, não importa extratos, não concilia cartão/PIX e não mantém parcelas ou histórico granular de cada recebimento. Essas capacidades exigem uma próxima ADR antes de serem tratadas como contabilidade ou conciliação.
