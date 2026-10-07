# ADR-007: Formulários Versionados, Arquivos Clínicos e Assinatura Presencial

## Status

Aceito em 5 de agosto de 2026.

Implementação em 6 de agosto de 2026: as entregas 1 a 7 estão presentes no código e cobertas por migrations e testes. A migração comparável da anamnese legada, item 8, permanece pendente.

## Contexto

O painel será usado com pacientes reais e precisa substituir a anamnese fixa e os anexos simulados por fluxos clínicos persistentes, auditáveis e adequados ao uso diário da profissional.

A profissional deverá poder criar vários questionários e documentos digitais, combinando campos abertos, seleções, caixas de marcação e outros controles. Um preenchimento precisa permanecer vinculado ao cliente, opcionalmente ao atendimento, e à versão exata do formulário utilizada. Alterações futuras no modelo não podem modificar respostas históricas.

Fotos e documentos enviados também fazem parte do prontuário. O sistema deve receber arquivos reais, otimizar imagens grandes, limitar formatos e tamanho, controlar o consumo do volume privado e impedir acesso público ou execução de conteúdo enviado.

Não haverá portal do paciente nem assinatura remota nesta etapa. Entretanto, durante um atendimento presencial, a profissional poderá entregar o tablet ao paciente e ativar um modo dedicado para desenhar a assinatura no próprio formulário.

Esta decisão complementa o [ADR-006](./ADR-006-Arquitetura-Clinica-Unica-Seguranca-e-Acessos.md), especialmente os módulos `ClinicalRecords`, `Consents` e `Files`.

## Decisão

### 1. Escopo incremental

A implementação será dividida em entregas verticais:

1. corrigir foco e acessibilidade das modais;
2. substituir anexos simulados por upload persistente;
3. consolidar o armazenamento privado, processamento de imagens e controle de volume;
4. implementar modelos e versões de formulários;
5. implementar o construtor visual;
6. integrar preenchimentos ao cliente e ao atendimento;
7. implementar assinatura presencial e evidências;
8. migrar a anamnese legada após validação.

Cada entrega deverá possuir migration, testes e critérios de aceite próprios. Nenhuma etapa poderá exigir a exclusão do volume existente.

### 2. Modelo de formulários

O módulo de formulários terá, no mínimo, os seguintes conceitos:

- `FormTemplate`: identidade lógica, nome, categoria, descrição e situação do formulário;
- `FormVersion`: definição imutável de uma versão publicada, autor, datas, resumo das alterações, hash e schema dos campos;
- `FormSubmission`: preenchimento vinculado ao cliente, à versão utilizada e opcionalmente a um atendimento;
- `FormSubmissionAmendment`: correção posterior identificada, com justificativa e preservação do conteúdo original;
- `FormSignature`: assinatura presencial e evidências vinculadas ao preenchimento finalizado.

Definições de campos e respostas variáveis serão armazenadas em `jsonb`. Relacionamentos, estados, autores, datas e campos usados para autorização ou busca permanecerão em colunas relacionais.

Cada campo terá um identificador estável que não depende do rótulo nem da posição. A primeira versão suportará:

- seção ou texto informativo;
- texto curto;
- texto longo;
- número;
- data;
- sim ou não;
- checkbox;
- grupo de checkboxes;
- dropdown de seleção única;
- seleção múltipla;
- assinatura presencial.

Regras condicionais complexas, cálculos e preenchimento remoto não fazem parte da primeira entrega, mas o schema não deverá impedir sua inclusão futura.

### 3. Ciclo de vida e versionamento

Um modelo poderá estar `Draft`, `Published` ou `Archived`.

- rascunhos podem ser editados;
- publicar cria uma versão numerada e imutável;
- editar uma versão publicada cria um novo rascunho;
- versões que possuam preenchimentos não podem ser removidas;
- arquivar impede novos usos sem ocultar o histórico;
- um preenchimento sempre referencia a versão exata que o originou.

Um preenchimento poderá estar `Draft`, `Finalized`, `Amended` ou `Voided`.

- respostas em rascunho podem ser atualizadas;
- finalizar torna respostas e versão imutáveis;
- correções posteriores exigem adendo, motivo e autor;
- anulação preserva o registro e a justificativa;
- não será permitido substituir silenciosamente um fato clínico finalizado.

O servidor validará as respostas usando o schema da versão. Validações apenas no frontend não serão consideradas suficientes.

### 4. Assinatura presencial no tablet

O tipo de campo `Signature` capturará uma assinatura desenhada presencialmente. Ao iniciar a assinatura, a aplicação entrará em modo dedicado:

- modal em tela cheia acima de toda a navegação;
- canvas adequado a toque e caneta;
- rolagem e ações do painel bloqueadas;
- comandos claros para limpar, cancelar e confirmar;
- saída protegida contra toque acidental;
- orientação para entregar o tablet ao signatário somente nessa tela.

O navegador não consegue bloquear completamente o sistema operacional do tablet. Um bloqueio de dispositivo mais rígido dependerá de modo quiosque ou gerenciamento do aparelho e será uma decisão operacional separada.

A evidência armazenada incluirá:

- traços vetoriais da assinatura e uma representação renderizada;
- identificador e declaração do signatário;
- versão do formulário;
- hash das respostas, do schema e da assinatura;
- data e hora UTC;
- método `InPersonDrawn`;
- usuário interno que conduziu o atendimento;
- metadados técnicos estritamente necessários;
- texto de confirmação apresentado no momento do aceite.

A confirmação finalizará o preenchimento em uma única operação transacional. Alterar qualquer resposta depois da assinatura exigirá novo adendo e, quando aplicável, nova assinatura.

Essa captura será tratada como assinatura eletrônica presencial e evidência de aceite. A aplicação não afirmará, sem validação jurídica, que ela equivale a assinatura digital qualificada ou certificada pela ICP-Brasil.

### 5. Arquivos clínicos

Conteúdo binário permanecerá fora do PostgreSQL e fora da pasta pública. O banco armazenará metadados e vínculos por meio de uma abstração de arquivo, permitindo usar o volume local agora e armazenamento compatível com objetos no futuro.

Cada arquivo armazenará, no mínimo:

- identificador e chave interna gerados pelo servidor;
- nome original apenas como metadado não confiável;
- extensão e tipo MIME validados;
- tamanho recebido e tamanho final;
- hash SHA-256;
- categoria, cliente e atendimento opcional;
- usuário responsável;
- status de quarentena, processamento, disponibilidade ou rejeição;
- data de criação e exclusão lógica.

O pipeline será:

1. receber em área temporária com limite de requisição;
2. validar permissão, vínculo, tamanho e lista de extensões;
3. validar assinatura real e estrutura interna;
4. verificar conteúdo malicioso quando o scanner estiver disponível;
5. processar ou otimizar;
6. calcular hash;
7. mover atomicamente para o volume privado;
8. persistir metadados e auditoria;
9. remover temporários e arquivos órfãos em falhas.

### 6. Formatos e imagens

A lista inicial será restritiva:

- imagens: JPG, JPEG, PNG e WEBP;
- documentos: PDF, DOCX, XLSX e CSV;
- HEIC poderá ser incluído após escolha e validação do decodificador;
- executáveis, SVG, HTML, arquivos com macros, DOCM, XLSM, DOC e XLS serão rejeitados na primeira versão.

Imagens serão decodificadas e reencodadas pelo servidor. O processamento deverá:

- impor limite de bytes e de pixels;
- corrigir orientação;
- remover EXIF e localização;
- limitar a maior dimensão por configuração;
- gerar uma versão clínica otimizada e uma miniatura;
- nunca confiar somente no `Content-Type` informado pelo navegador.

Os limites serão configuráveis. A linha de base implementada será 25 MB e 32 megapixels para a imagem recebida, com maior dimensão final de 2560 px, e 15 MB por documento, sujeita a ajuste antes da produção.

### 7. Volume, quotas e backup

O sistema acompanhará o espaço lógico utilizado por cliente, categoria e total. Alertas operacionais ocorrerão em níveis configuráveis, inicialmente 70%, 85% e 95% da capacidade planejada.

Uploads serão recusados antes de esgotar o disco. O painel administrativo exibirá uso, arquivos rejeitados e falhas de processamento. Exclusão lógica não liberará imediatamente um registro clínico; purga dependerá de política de retenção e autorização explícita.

Banco e volume privado fazem parte do mesmo conjunto de recuperação. Backups deverão ser criptografados, possuir cópia externa à residência e ser submetidos a teste periódico de restauração.

### 8. Autorização e auditoria

Serão adicionadas permissões específicas para administrar e publicar modelos de formulário. Leitura e preenchimento de respostas clínicas continuarão sob permissões clínicas.

- administrador e profissional poderão criar e publicar modelos;
- profissional poderá preencher e finalizar documentos clínicos;
- recepcionista não receberá acesso padrão a respostas, assinaturas, fotos ou documentos clínicos;
- downloads e visualizações passarão pela API autenticada;
- URLs internas não serão previsíveis nem públicas;
- upload, download, visualização, publicação, preenchimento, finalização, assinatura, adendo e anulação gerarão auditoria.

Logs técnicos não incluirão respostas, traços de assinatura, conteúdo de arquivos ou identificadores pessoais desnecessários.

### 9. Experiência no painel

O menu privado ganhará a área `Formulários`, com lista, criação, duplicação, edição de rascunho, pré-visualização, comparação de versões, publicação e arquivamento.

O detalhe do cliente será organizado para distinguir:

- formulários digitais;
- evoluções clínicas;
- fotos;
- documentos e termos enviados;
- histórico e auditoria autorizada.

Ao iniciar um formulário pelo cliente ou atendimento, a versão será fixada. A tela exibirá claramente rascunho, finalizado, assinado, adendo ou anulado, além de versão, atendimento, autor e datas.

### 10. Migração da anamnese atual (superada)

Esta decisão foi superada pelo ADR-008. Como o ambiente ainda não possui dados reais nem está em produção, o módulo e a tabela de anamnese fixa serão removidos sem migração. As anamneses, consentimentos e questionários passarão a ser formulários versionados.

## Consequências

### Positivas

- questionários deixam de depender de código e migrations para cada alteração;
- respostas antigas permanecem legíveis com seu significado original;
- arquivos tornam-se persistentes, privados e controláveis;
- fotos grandes deixam de consumir o volume sem necessidade;
- assinatura presencial integra o fluxo do consultório;
- permissões e auditoria mantêm a separação entre recepção e área clínica;
- o armazenamento local poderá ser substituído futuramente sem alterar o domínio.

### Custos e riscos

- o construtor e o versionamento elevam a complexidade de validação e testes;
- processamento de imagens e inspeção de Office exigem bibliotecas mantidas e monitoramento de segurança;
- scanner antimalware consome recursos na infraestrutura doméstica;
- banco e arquivos precisam de backup e restauração coordenados;
- assinatura desenhada exige revisão jurídica e operacional do texto de aceite;
- modo de assinatura da aplicação não equivale a modo quiosque do dispositivo.

## Alternativas consideradas

### Armazenar cada tipo de questionário em novas colunas

Rejeitada. Exigiria mudança de código e banco para cada formulário e não preservaria versões de maneira sustentável.

### Editar formulários publicados no lugar

Rejeitada. Alteraria o significado de respostas históricas e prejudicaria a rastreabilidade.

### Guardar arquivos binários dentro do PostgreSQL

Rejeitada nesta fase. Aumentaria o tamanho e o custo dos backups do banco sem benefício suficiente para a instalação local.

### Aceitar qualquer extensão de documento

Rejeitada. Ampliaria desnecessariamente a superfície para malware, conteúdo ativo e negação de serviço.

### Implementar assinatura remota e portal do paciente agora

Rejeitada para manter o escopo coerente com o ADR-006. O modelo poderá ser ampliado futuramente sem confundir a assinatura presencial desta etapa.
