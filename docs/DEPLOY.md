# Execução e Implantação

## Estado

O Compose executa PostgreSQL, um migrador de schema de execução única, API e frontend/Nginx. Os três serviços operacionais são vinculados somente ao loopback do servidor: frontend em `32150`, API em `32151` e PostgreSQL em `32152`. O Cloudflare Tunnel aponta exclusivamente para o frontend.

**Não use dados reais ainda.** O código das Fases 0 a 3 está concluído, mas a operação só poderá ser liberada após backup externo criptografado, restauração testada, HTTPS/Cloudflare Access, aviso de privacidade e revisão dos acessos.

## Primeira instalação sem dados

```powershell
Copy-Item .env.example .env
docker compose config
docker compose up --build -d
docker compose ps
```

Em `.env`, defina:

- senha exclusiva do PostgreSQL;
- usuário, email e senha temporária forte do primeiro administrador;
- fuso IANA da clínica, normalmente `America/Cuiaba`;
- hostname HTTPS público, portas locais e limites de armazenamento.

`ALLOW_INSECURE_HTTP_COOKIES` deve permanecer `false` em implantação. O valor `true` existe somente para testes locais por `http://127.0.0.1` e nunca deve ser combinado com Cloudflare Tunnel ou exposição em rede.

O serviço `migrations` deve terminar com código zero antes de a API iniciar. No primeiro login, troque a senha temporária e ative MFA. Em seguida, remova do `.env` as três variáveis `ADMIN_BOOTSTRAP_*` e recrie somente o serviço backend.

## Atualização de um volume legado

O migrador detecta o schema anterior, cria estruturas do Identity, permissões e auditoria, registra a linha de base e aplica as migrations seguintes. Isso foi validado automaticamente em PostgreSQL isolado, com preservação dos registros sentinela.

Antes de executar no volume operacional:

1. Pare escritas na aplicação antiga.
2. Faça backup consistente do PostgreSQL e do volume de arquivos privados.
3. Copie os backups para armazenamento externo e criptografado.
4. Restaure ambos em ambiente isolado.
5. Execute a nova imagem e o migrador sobre a cópia restaurada.
6. Valide contagens, vínculos, login, arquivos e relatórios.
7. Defina uma janela de manutenção e somente então atualize o ambiente real.

O rollback operacional é a restauração dos backups, não uma migration destrutiva. Não execute `docker compose down -v` em ambiente com dados a preservar.

## Recriar um ambiente descartável

Somente quando todos os dados puderem ser perdidos, remova a stack e seus volumes com:

```powershell
docker compose down --volumes --remove-orphans
docker compose up --build -d
```

O primeiro comando apaga banco, arquivos privados e chaves de sessão do projeto. Nunca o utilize em produção ou homologação com registros a preservar.

## Portas e fronteiras de rede

- `frontend`: `127.0.0.1:32150`, único destino do Cloudflare Tunnel;
- `backend`: `127.0.0.1:32151`, somente para diagnóstico local do servidor;
- `postgres`: `127.0.0.1:32152`, somente para administração local do servidor;
- `migrations`: encerra após atualizar o banco;
- arquivos clínicos e chaves de proteção: volumes privados separados;
- nenhuma porta de entrada deve ser aberta no roteador.

Os binds em `127.0.0.1` são intencionais. Não substitua por `0.0.0.0`, não exponha `32151` ou `32152` no Cloudflare Tunnel e não crie regras de encaminhamento de porta para eles. Uma rede Docker auxiliar existe apenas para que o Docker faça esse encaminhamento local; ela não recebe tráfego externo. A landing usa a API pelo caminho `/api` no próprio frontend, portanto não exige que a API seja pública.

O CORS de produção fica limitado à origem configurada em `CORS_ALLOWED_ORIGIN`. Para o domínio provisório, o valor é `https://leilainearakaki.cloudlane.com.br`; altere-o junto com `ALLOWED_HOSTS` quando o domínio definitivo for configurado.

## Persistência e espaço em disco

Três volumes Docker nomeados sobrevivem a reinícios, recriações de containers e a `docker compose down` normal:

- `painelestetica_postgres_data`: banco PostgreSQL, incluindo usuários, agenda, prontuários, formulários, auditoria e metadados;
- `painelestetica_private_storage`: fotos, documentos clínicos e imagens enviadas para a landing page;
- `painelestetica_data_protection_keys`: chaves que protegem sessões, antiforgery e dados criptografados pela aplicação.

Eles só são apagados por `docker compose down --volumes`, remoção explícita dos volumes ou perda do disco do servidor. O limite padrão da aplicação é 500 MB por paciente e 20 GB somando fotos e documentos clínicos. Reserve espaço adicional para PostgreSQL, imagens da landing, logs e backups; monitore o disco do servidor e nunca trate o volume como backup.

## Verificações pós-subida

```powershell
docker compose ps
docker compose logs migrations
docker compose logs --tail 100 backend
Invoke-WebRequest http://127.0.0.1:32150/health
Invoke-WebRequest http://127.0.0.1:32150/api/public/cms
```

Uma chamada anônima a `/api/clients` deve retornar `401`. O fluxo administrativo deve exigir troca da senha temporária e MFA antes de liberar operações protegidas.

## Cloudflare

O desenho operacional prevê:

- hostname público `leilainearakaki.cloudlane.com.br` para a landing;
- Cloudflare Access protegendo a rota administrativa antes do login da aplicação;
- Tunnel apontando para o frontend/Nginx em loopback;
- HTTPS externo e nenhuma porta de entrada aberta;
- token do túnel mantido fora do repositório e do Compose.

Configuração mínima de ingresso do Tunnel:

```yaml
ingress:
  - hostname: leilainearakaki.cloudlane.com.br
    service: http://127.0.0.1:32150
  - service: http_status:404
```

O Nginx preserva o HTTPS informado pelo Tunnel para o backend. Não exponha nenhum bind local diretamente à internet.

## Backup obrigatório antes de dados reais

O volume Docker não é backup. A liberação exige:

- backup automático e consistente do PostgreSQL;
- backup dos arquivos privados e das chaves de proteção de dados;
- criptografia e cópia fora da residência;
- retenção de múltiplas versões;
- restauração completa testada e documentada;
- alerta para falha, falta de espaço e backup atrasado.
