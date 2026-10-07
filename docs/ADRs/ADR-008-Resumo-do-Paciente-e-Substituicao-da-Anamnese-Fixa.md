# ADR-008: Resumo do Paciente e Substituição da Anamnese Fixa

- **Status:** Aceito
- **Data:** 06/08/2026

## Contexto

O prontuário possuía uma ficha de anamnese fixa, com campos definidos em código. Em paralelo, o sistema já permite criar formulários dinâmicos e versionados para anamneses, consentimentos e questionários específicos de cada procedimento.

Manter os dois modelos gera duplicidade, campos clínicos difíceis de evoluir e uma área de cliente que não mostra rapidamente a situação operacional do paciente. O projeto ainda está em desenvolvimento: não há dados reais nem ambiente de produção a preservar.

## Decisão

1. Remover integralmente a anamnese fixa: entidade, tabela `Anamneses`, contratos, API e interface.
2. Usar formulários dinâmicos e versionados como a fonte para anamneses, questionários e termos. Cada preenchimento conserva a versão publicada que estava vigente no atendimento.
3. Substituir a área removida por um **Resumo do Paciente**, calculado a partir de dados persistidos, sem duplicar os dados clínicos:
   - atendimentos totais, concluídos e futuros, com o próximo horário;
   - evoluções clínicas e última evolução;
   - procedimentos mais realizados, com base nas evoluções registradas;
   - documentos, fotos e formulários por estado (rascunho, finalizado e assinado);
   - valores contratados, recebidos e saldo em aberto.
4. Valores financeiros do resumo serão disponibilizados exclusivamente para os papéis **Administradora** e **Profissional**. Essa restrição será aplicada também no endpoint, não apenas na tela. Recepcionistas não acessam o prontuário/resumo clínico do paciente.
5. Leituras do resumo serão auditadas. O painel é apenas uma visão agregada; evoluções, formulários, fotos e documentos continuam em suas seções próprias e preservam seus controles de acesso.

## Consequências

### Positivas

- elimina uma estrutura clínica rígida e duplicada;
- a profissional passa a adaptar anamneses sem alterações de código;
- o detalhe do cliente passa a abrir com contexto assistencial e operacional;
- a visualização financeira tem uma regra de acesso clara e verificável pelo servidor.

### Custos e riscos

- a migration remove fisicamente a tabela `Anamneses`; não há conversão para dados antigos, por decisão explícita para este ambiente pré-produção;
- os valores do resumo são operacionais: somam atendimentos não cancelados e não substituem fechamento contábil;
- a qualidade dos indicadores de procedimentos depende do registro das evoluções clínicas.

## Alternativas rejeitadas

- **Manter anamnese fixa e formulários em paralelo:** duplicaria a origem dos dados clínicos.
- **Migrar a ficha fixa para um formulário legado:** agrega complexidade sem benefício enquanto não existem dados reais.
- **Exibir valores para recepção:** não atende ao princípio de menor privilégio definido para a clínica.
