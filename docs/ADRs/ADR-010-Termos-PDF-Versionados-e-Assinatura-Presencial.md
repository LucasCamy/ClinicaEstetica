# ADR-010: Termos PDF Versionados e Preenchimento Presencial

## Status

Aceito em 10 de agosto de 2026.

## Contexto

Além dos formulários estruturados, a clínica necessita aplicar termos de consentimento já existentes em PDF. Alguns termos possuem várias páginas, campos de texto em posições específicas e áreas para escrita ou assinatura manuscrita no tablet.

O documento entregue ao paciente precisa preservar o PDF originalmente aprovado pela profissional, a versão exata aplicada, os valores preenchidos, os traços manuscritos, o nome do signatário, o aceite explícito da declaração e a evidência de quando e por quem o atendimento foi conduzido. Termos finalizados não podem ser substituídos nem apagados silenciosamente.

## Decisão

### Modelo e versionamento

- `TermTemplate` representa o termo lógico, seu rascunho e a situação de uso.
- `TermVersion` contém uma cópia imutável do PDF, o layout dos campos, hash do PDF, hash do layout, autor e data de publicação.
- `TermSubmission` vincula um termo aplicado ao cliente, opcionalmente ao atendimento, e contém o PDF final, os valores, hashes, situação e histórico de anulação.
- Alterar PDF, posição, tipo ou obrigatoriedade de um campo exige uma nova publicação. Preenchimentos existentes continuam vinculados à versão anterior.

### Campos sobre o PDF

O editor trabalhará com coordenadas normalizadas por página, evitando dependência de resolução ou tamanho do tablet. A primeira versão oferecerá:

- texto curto e texto livre;
- data;
- checkbox;
- escrita manuscrita;
- assinatura manuscrita com declaração de aceite.

O padrão é liberar apenas campos delimitados. Uma zona de escrita pode ocupar toda uma página quando realmente necessária, mas o PDF-base nunca é editado diretamente.

### Finalização

No atendimento, a aplicação renderiza o PDF e apresenta apenas os campos liberados. Ao finalizar, gera um novo PDF com os textos e traços incorporados ao conteúdo, preserva o arquivo final no volume privado e grava seu SHA-256.

O servidor valida a versão, os campos, os limites de traços, a revisão concorrente e o PDF recebido. O arquivo final, valores normalizados, hash do layout e hash do PDF compõem a evidência do termo. A anulação preserva o PDF e exige motivo; não há exclusão de termo finalizado.

### Segurança e operação

- apenas PDF é aceito; assinatura de arquivo, tamanho e nome são validados pelo servidor;
- PDFs-base, rascunhos e termos finalizados nunca são públicos e só podem ser lidos por API autenticada;
- administrador e profissional administram modelos; apenas quem possui acesso clínico pode iniciar, visualizar ou finalizar termos de clientes;
- upload, publicação, visualização, finalização, download e anulação são auditados sem registrar conteúdo sensível nos logs;
- a escrita e a assinatura ocorrem sobre a área delimitada no próprio PDF, dentro da modal do termo; ela não substitui o modo quiosque do sistema operacional.

### Limite jurídico explícito

A assinatura desenhada presencialmente é registrada como evidência de aceite e não será apresentada pelo sistema como assinatura ICP-Brasil, qualificada ou automaticamente avançada. O fluxo de assinatura da profissional deverá ser avaliado separadamente antes de ser usado como assinatura profissional de documentos de saúde.

## Consequências

### Positivas

- a profissional reaproveita seus termos atuais sem reconstruí-los como formulário;
- o paciente assina no próprio PDF, no campo correto e com boa experiência no tablet;
- versões e termos aplicados permanecem verificáveis;
- o prontuário passa a concentrar formulário, documento anexado e termo assinado sem expor arquivos publicamente.

### Custos e riscos

- a composição visual do PDF ocorre no cliente e o servidor registra e valida o resultado, mas não substitui uma assinatura criptográfica qualificada;
- PDFs com recursos incomuns ou protegidos por senha podem não ser processados e devem ser recusados;
- cópias de segurança do banco e do volume privado precisam permanecer coordenadas e criptografadas.

## Alternativas consideradas

### Permitir edição livre de todo o PDF

Rejeitada como padrão. Facilitaria rabiscos fora de contexto, ocultaria a intenção de cada campo e prejudicaria a experiência e a rastreabilidade. Uma área livre configurável resolve os casos necessários sem alterar o PDF-base.

### Guardar apenas uma imagem do termo assinado

Rejeitada. Perderia a capacidade de pesquisa, a qualidade do PDF e os metadados estruturados dos campos.

### Substituir o PDF final a cada edição

Rejeitada. O termo finalizado é evidência clínica e deve ser imutável; correções futuras devem ocorrer por novo termo ou anulação justificada.
