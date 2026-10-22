# Finance

API REST em .NET 10 e front em react para transferências imediatas e agendadas entre contas, com cheque especial, limites por hora diferentes para o dia e para a noite e controle de concorrência. Os dados ficam no postgres, os agendamentos passam pelo rabbitMQ e são executados por um worker separado da API. Tudo sobe com um único `docker compose up`.

Todos os diferenciais foram implementados.

Além do enunciado, incluímos abertura de conta, busca de contas por nome ou cpf, extrato, histórico por conta, limites personalizados por conta e testes de arquitetura que garantem as fronteiras entre módulos.

Como um plus, adicionei um [benchmark](#benchmark)/ teste de carga em ambiente parecido com produção (nginx, limites de cpu e memória, 200 mil contas), os resultados desse ajuste também refletiram em ajustes reais no código.

## Índice

- [Requisitos](#requisitos)
- [Como executar](#como-executar)
- [Decisões técnicas](#decisões-técnicas)
  - [Banco de dados](#banco-de-dados)
  - [Eventos e processamento assíncrono](#eventos-e-processamento-assíncrono)
  - [Injeção e inversão de dependências](#injeção-e-inversão-de-dependências)
  - [Processor e interactor](#processor-e-interactor)
  - [Concorrência e idempotência](#concorrência-e-idempotência)
  - [Erros de negócio sem exceções](#erros-de-negócio-sem-exceções)
- [Arquitetura](#arquitetura)
- [Regras de negócio](#regras-de-negócio)
- [Benchmark](#benchmark)
- [Uma forma mais correta de tratar dinheiro](#uma-forma-mais-correta-de-tratar-dinheiro)

## Requisitos

| Dependência        | Versão                                            | Download                                          |
| ------------------ | ------------------------------------------------- | ------------------------------------------------- |
| docker com compose | compose v2 ou superior, testado com docker 29.4.2 | https://docs.docker.com/get-started/get-docker/   |
| .NET SDK           | 10.0.100 ou superior                              | https://dotnet.microsoft.com/download/dotnet/10.0 |
| Node.js            | 24.15.0 ou superior                               | https://nodejs.org/en/download                    |

Para apenas subir a aplicação, só o docker é necessário.

## Como executar

```bash
docker compose up -d --build
```

O `migrator` aplica as migrations, cria as contas de exemplo e encerra. Depois sobem `api`, `worker` e `front`.

| O quê        | Endereço                                            |
| ------------ | --------------------------------------------------- |
| front        | http://localhost:3000                               |
| swagger      | http://localhost:8080/swagger                       |
| health check | http://localhost:8080/health                        |
| rabbitMQ     | http://localhost:15672, usuário e senha `finance`   |
| postgres     | `localhost:55432`, banco, usuário e senha `finance` |

O postgres usa a porta 55432 no host para não conflitar com uma instalação local. Para trocar: `POSTGRES_HOST_PORT=5432 docker compose up -d`.

Contas de exemplo:

| Titular       | Saldo     | Cheque especial | Status   |
| ------------- | --------- | --------------- | -------- |
| Ana Souza     | 500,00    | 1.000,00        | Active   |
| Bruno Lima    | -800,00   | 1.000,00        | Active   |
| Carla Mendes  | 10.000,00 | 0,00            | Active   |
| Daniel Rocha  | 300,00    | 0,00            | Blocked  |
| Elisa Martins | 2.000,00  | 500,00          | Inactive |

Os testes do backend usam docker para subir postgres e rabbitMQ via testcontainers:

```bash
cd finance-api && dotnet test Finance.slnx
cd finance-front && npm ci && npm run test
```

São 209 testes no backend e 83 no front.

Para rodar fora do docker, suba só a infraestrutura com `docker compose up -d postgres rabbitmq` e, em terminais separados:

```bash
cd finance-api
dotnet run --project src/Hosts/Finance.Migrator
dotnet run --project src/Hosts/Finance.Api
dotnet run --project src/Hosts/Finance.Worker

cd finance-front
cp .env.example .env && npm ci && npm run dev
```

## Decisões técnicas

### Banco de dados

**Modelagem**

- **Um banco, um `FinanceDbContext` e um único schema para todos os módulos.** Débito, crédito, status da transferência e evento precisam ser gravados juntos. Por isso os módulos compartilham o contexto e a transação, o que deixa a atomicidade com o banco, sem o uso do pattern saga ou alguma outra compensação.
  Evitei o uso de saga, já que isso só adicionaria complexidade desnecessária. Enquanto, o benefício só seria perceptível em aplicações distribuidas.
- **Saldo calculado a partir do ledger, e não guardado na conta.** Toda movimentação vira um lançamento imutável em `account_entries`, inclusive o saldo inicial, gravado como lançamento de abertura. O saldo é a soma desses lançamentos, exposta pela view `account_balances`. Por isso não existe um campo que possa divergir do histórico: o ledger é a única fonte da verdade, e o índice único `(transfer_id, entry_type)` impede um segundo débito ou crédito da mesma transferência. O `balance_after` de cada lançamento continua no extrato e deixa visível a cobertura do cheque especial: uma conta com -800 que recebe 1.000 fica com 200.
  - **Custo:** a soma é servida pelo índice `(account_id, id)` com `INCLUDE (entry_type, amount)`, então o postgres lê só o índice, sem tocar na tabela.
  - **Para escalar, e que não fizemos para não complicar o projeto:**
    - **Cache em memória, com Redis ou similar:** guardar, por um intervalo de tempo, o saldo já calculado das contas que foram consultadas. Serve só para as telas de leitura, nunca para validar uma transferência, pois uma transferência concorrente deixaria o cache atrasado e o cheque especial poderia ser ultrapassado. A validação continuaria somando o ledger com as contas travadas.
    - **View materializada:** no postgres ela não se atualiza a cada lançamento. É preciso um `REFRESH`, que recalcula o saldo de todas as contas, e entre um refresh e outro o saldo fica desatualizado. Atualizar a cada transferência custaria mais do que a soma atual e serializaria as transferências do sistema inteiro. Uma tabela de resumo mantida por trigger, ou pela extensão `pg_ivm`, resolveria a atualização, mas acrescenta uma escrita a cada lançamento e volta a guardar o saldo.
    - **Snapshot periódico:** guardar o saldo consolidado até um instante e somar só os lançamentos posteriores. O ledger continua sendo a fonte da verdade e o custo da soma deixa de crescer sem limite. É o caminho natural quando o histórico das contas crescer.
      Seria uma simples consolidação periódica.
- **Invariantes repetidas no banco.** O domínio já valida tudo, mas os CHECKs de valor positivo, origem diferente do destino e cheque especial não negativo, o `numeric(18,2)` e o CPF único seguram o dado mesmo diante de um bug ou de uma escrita fora da aplicação. Ou seja, a validação está tanto na camada de aplicação, quanto de dados.
- **Tentativas são transferências persistidas.** Uma transferência rejeitada é gravada como `Failed` com `failure_code`. Por isso o limite de tentativas é um `COUNT` sobre a própria tabela de transferências, sem contador paralelo que poderia divergir.

**Índices**

Cada índice nasce de uma consulta real do sistema, e não da tabela. Por isso alguns são compostos ou parciais, cobrindo exatamente o filtro e a ordenação da consulta:

| Índice                                                                         | Tipo                                        | Consulta que ele serve                                  |
| ------------------------------------------------------------------------------ | ------------------------------------------- | ------------------------------------------------------- |
| `ix_transfers_source_account_id_attempted_at`                                  | composto                                    | janela de 60 minutos dos limites por hora               |
| `ix_transfers_source_account_id_id` e `ix_transfers_destination_account_id_id` | composto, um por ramo do `UNION ALL`        | histórico de enviadas e recebidas por cursor            |
| `ix_transfers_scheduled_for_due`                                               | parcial em `status = 'Scheduled'`           | agendamentos vencidos                                   |
| `ix_transfers_processing_started_at_stale`                                     | parcial em `status = 'Processing'`          | transferências presas em processamento                  |
| `ix_outbox_messages_unprocessed`                                               | parcial em `processed_at IS NULL`           | mensagens pendentes do outbox                           |
| `ix_account_entries_account_id_id`                                             | composto com `INCLUDE (entry_type, amount)` | extrato por cursor e cálculo do saldo lendo só o índice |
| `ux_account_entries_opening`                                                   | único e parcial em `transfer_id IS NULL`    | no máximo um lançamento de abertura por conta           |
| `ix_persons_full_name_id`                                                      | composto                                    | listagem de contas por nome                             |
| `ix_persons_full_name_trgm`                                                    | GIN trigram                                 | busca por trecho do nome com `ILIKE '%termo%'`          |
| `ux_persons_document_number`                                                   | único com `varchar_pattern_ops`             | unicidade do CPF e busca por prefixo, no mesmo índice   |

Os índices parciais guardam só as linhas que a consulta procura. Uma transferência concluída nunca volta a ser `Scheduled`, então o índice dos vencidos continua pequeno mesmo com milhões de transferências no histórico.

A relação custo benefício de que cada indice criado faz com que o banco consuma mais espaço para armazenar as informações de uma mesma tabela é irrisória no nosso atual cenário. Além disso, o custo de armazenamento em cloud é mais barato do que o custo computacional extra exigido do banco.

**Chaves primárias sem índice explícito**

As PKs têm nome no formato `pk_<tabela>`, mas nenhum índice extra, pois o postgres já cria um índice único para cada PK, e um segundo índice sobre `id` só dobraria o custo de escrita e o espaço. Com as FKs é o contrário: o postgres não as indexa sozinho, então toda FK tem um índice explícito, garantido pelo teste `DatabaseTests.ForeignKeyColumns_AreAllIndexed`.

**UUID v7 em vez de v4**

Os ids são UUID v7, gerados no domínio com `Guid.CreateVersion7`.

- O v7 começa pelo timestamp, então cada id novo é maior que o anterior. Com isso, o postgres insere a nova entrada sempre na última página do índice B-tree, sem abrir espaço no meio da estrutura.
- Com o v4, que é aleatório, cada inserção cai em um ponto qualquer do índice, o que força divisões de página, deixa páginas pela metade e espalha as escritas por páginas que precisam ser lidas do disco. Embora, possa ser minimizado o impacto com vacuums e reindexs periódicos.
- Resultado: escritas mais baratas e um índice mais compacto.
- Como o id é ordenado por tempo, "mais recente primeiro" vira `ORDER BY id DESC`. Por isso o cursor do extrato e do histórico é só o id, servido pelos índices compostos que terminam em `id`.
- Gerar no domínio faz o agregado ter identidade antes de ser salvo, o que permite que os domain events referenciem o id na mesma transação.

**Paginação por cursor em vez de offset**

Todas as listagens usam keyset: `WHERE (full_name, id) > (cursor) ORDER BY full_name, id LIMIT n + 1`.

- Com offset, o banco lê e descarta todas as linhas anteriores à página, então o custo cresce com a profundidade, além do `COUNT(*)` por requisição para montar o "página X de Y".
- Com cursor, o banco vai direto ao ponto do índice e lê só `n + 1` linhas, e a linha extra diz se existe próxima página. Por isso o custo é o mesmo em qualquer página: no benchmark, o p50 ficou entre 3,3 e 3,4 ms em todas as faixas, da página 1 à 50.
- O cursor também não pula nem repete itens quando novas contas são criadas entre duas páginas, pois a posição é um valor da chave, e não uma contagem de linhas.

O que se perde é o "página X de Y" e o salto para uma página arbitrária. Aqui isso pesa pouco: uma conta específica é encontrada pela busca no servidor, extrato e histórico são lidos do mais recente para trás, e a tabela de contas só precisa de próxima e anterior.

### Eventos e processamento assíncrono

Os agendamentos são executados por um worker separado da api, alimentado por um outbox transacional e pelo rabbitMQ, em vez de um timer dentro da api ou de uma publicação direta no broker durante a requisição.

- **Sem evento perdido nem evento fantasma.** O agregado `Transfer` levanta domain events. O `UnitOfWork` os despacha no `SaveChanges`, e os handlers gravam as mensagens na tabela de outbox na mesma transação da mudança de estado. Se a transação falha, o evento não existe, e se ela é confirmada, o evento está salvo mesmo com o broker fora. O `OutboxPublisherJob` publica com publisher confirms e só marca a mensagem como processada depois da confirmação.
- **Entrega pelo menos uma vez, com consumidor idempotente.** Como uma mensagem pode chegar duas vezes, o consumidor trava a transferência e só executa se ela ainda está `Processing`. Uma reentrega, então, não move dinheiro de novo.
- **A API não espera ninguém.** Agendar é gravar uma linha e responder. Quem executa é o worker:
  - o dispatcher pega os vencidos em lotes com `FOR UPDATE SKIP LOCKED`, o que permite várias instâncias do worker dividirem o trabalho sem disputar a mesma linha;
  - os consumidores usam prefetch e processamento concorrente;
  - os jobs usam `PeriodicTimer`, e o consumo é todo assíncrono.
- **Ritmo definido por medição.** O outbox publica até 500 mensagens por segundo, porque o padrão anterior, de 50 por segundo, ficou abaixo dos 80 a 190 eventos por segundo medidos no benchmark.

O trade-off é a consistência eventual: no benchmark, uma agendada terminou em média de 1 a 2 s depois do vencimento, e o sistema ganha duas peças a mais para operar: o broker e o worker. Uma transferência presa em `Processing` por mais de 10 minutos, por queda do worker, vira `Failed` com `ProcessingTimeout`.

### Injeção e inversão de dependências

Usamos o container nativo do .NET. Cada módulo expõe um único ponto de registro, e os hosts só compõem módulos.

- **Testabilidade sem mocks de regra.** As classes recebem abstrações pelo construtor e não sabem como as implementações são construídas. Por isso os testes trocam só a infraestrutura.
- **Evitei o uso de dependências explícitas.** Com patterns como Service Locator, as dependências ficam escondidas no corpo do método e uma ausência só aparece em tempo de execução, no meio de um fluxo. Pelo construtor, elas ficam na assinatura, e como os testes constroem o container, um registro faltando quebra o teste antes de chegar a produção.

Os use cases são classes concretas, sem interface. Cada um tem uma única implementação, e os testes usam a real, então uma interface ali seria só cerimônia.

### Processor e interactor

Separamos quem recebe a mensagem de quem executa a regra:

- o `RabbitMqConsumerHostedService` cuida do transporte: desserializa, abre um escopo de DI, faz ack, nack ou manda para a dead-letter;
- o processor, um `IMessageProcessor<T>`, só traduz a mensagem em uma chamada;
- o interactor, `ExecuteScheduledTransferUseCase`, contém a regra e não sabe que veio de uma fila.

Por isso a regra é testada chamando o use case direto, sem broker, e a agendada reaproveita o `TransferExecutor` da transferência imediata: limites, saldo e cheque especial são reavaliados pelo mesmo código nos dois caminhos. Trocar o broker afeta só o consumidor, e um novo consumidor da mesma fila, como o `TransferNotificationProcessor`, é só mais uma classe registrada.

### Concorrência e idempotência

- **Um lock por conta de origem.** Antes de avaliar os limites, a transação pega um `pg_advisory_xact_lock` da conta de origem. Isso transforma contagem da janela, avaliação dos limites e registro da tentativa em uma seção crítica por conta, então duas requisições simultâneas não passam juntas pelo limite. Preferimos isso a `SERIALIZABLE`, que exigiria repetir o use case inteiro a cada conflito e bloquearia mais do que o necessário.
- **As duas contas travadas em ordem fixa.** O débito e o crédito usam `SELECT ... FOR UPDATE ORDER BY id`. Como toda transação trava as contas na mesma ordem, A→B e B→A não entram em deadlock. Todo lançamento de uma conta é gravado com a linha dela travada, então a segunda transação espera e, ao obter o lock, soma um ledger que já inclui a primeira. Assim, em duas transferências simultâneas com o mesmo saldo, uma conclui e a outra falha com `InsufficientFunds`. Uma concorrência otimista viraria uma tempestade de conflitos e retries justamente nas contas mais disputadas.
- **Idempotência.** O header `Idempotency-Key` vem com um fingerprint SHA-256 do pedido. Sob um advisory lock da chave:
  - a mesma chave com o mesmo pedido devolve a resposta original com `Idempotent-Replayed: true`;
  - a mesma chave com outro pedido devolve 422 `IdempotencyKeyReused`.

  Um índice único parcial é a última barreira. O front reaproveita a chave quando a requisição falha por rede, então um clique repetido não duplica a transferência.

### Erros de negócio sem exceptions

As regras retornam `Either<Error, T>`, com os erros em catálogos. Como a falha faz parte da assinatura, quem chama é obrigado a tratá-la, e o fluxo de uma rejeição fica visível no código, em vez de escondido em um `throw` que sobe por várias camadas. O mapeamento de erro para os status HTTP 400, 404, 409 e 422 fica em um único lugar, `Finance.Shared.Presentation`, então nenhum controller escolhe status. Exceções ficam para o que é falha de verdade, como banco fora e bug, e viram 500.

## Arquitetura

A organização mais comum em .NET com DDD e SOLID é a Clean Architecture em camadas horizontais: um projeto `Domain`, um `Application`, um `Infrastructure` e um `Web` para o sistema inteiro.

Nós seguimos as mesmas camadas, mas cortadas por módulo de negócio, adotando algumas práticas de um monolito modular:

```
finance-api/
├── src/
│   ├── Modules/
│   │   ├── Accounts/    Domain, Application, Infrastructure, Presentation, Contracts
│   │   └── Transfers/   Domain, Application, Infrastructure, Presentation
│   ├── Shared/          Domain, Application, Infrastructure, Presentation
│   └── Hosts/           Finance.Api, Finance.Worker, Finance.Migrator
└── tests/               por módulo, Shared, ponta a ponta e arquitetura
finance-front/
└── src/app/<módulo>, src/shared
```

**Por que não seguimos as camadas horizontais**

- Nelas, o `Application` reúne os use cases de todos os contextos, e nada impede um use case de transferência de usar a entidade e o repositório de conta diretamente. O acoplamento cresce sem ninguém perceber.
- Aqui, Transfers só referencia `Finance.Modules.Accounts.Contracts`, com a interface `IAccountsModule` e quatro métodos. O compilador bloqueia o acesso ao resto de Accounts, e o `Finance.Architecture.Tests` confere as referências a cada execução.
- Cada módulo reúne tudo de uma capacidade de negócio, então uma mudança na regra de transferência fica em uma pasta.
- O contrato já é o ponto de corte, caso um módulo precise virar um serviço.
- `Shared` guarda só o que é de todos, como `Either`, `Entity`, unit of work, outbox, rabbitMQ e paginação, e não conhece contas nem transferências.

**Trade-offs aceitos**

- São 22 projetos para um escopo pequeno, o que deixa a solution mais longa e o build mais lento. Em troca, a fronteira é garantida pelo compilador, e não por convenção.
- Um `DbContext` e um banco compartilhados, em nome da atomicidade da transferência.
- Os módulos conversam por um contrato em processo, e não por mensagens.

**SOLID com critério**

- **Uma responsabilidade por classe onde ela muda sozinha.** Há um use case por operação, e o `TransferExecutor` concentra a execução comum à imediata e à agendada.
- **Extensão sem edição.** Um novo efeito de um evento é um novo `IDomainEventHandler` registrado, e um novo consumidor é um `AddRabbitMqConsumer<TMessage, TProcessor>`. O dispatcher e o consumidor genérico não mudam.
- **Contratos enxutos.** Transfers enxerga de Accounts só o que precisa, e não um repositório inteiro.
- **Dependência de abstrações onde há troca real.** Banco, broker, lock e relógio ficam atrás de interfaces, porque é exatamente isso que os testes e a infraestrutura trocam.

Onde não seguimos à risca, foi por escolha:

- os use cases não têm interface;
- as consultas de leitura projetam direto para o response sem passar pelos agregados, já que uma leitura não precisa proteger invariantes e assim evita carregar entidades;
- `Shared.Infrastructure` conhece EF Core e rabbitMQ.

Em todos esses casos, a abstração a mais não traria nenhuma vantagem concreta.

## Regras de negócio

**Cenários do enunciado e onde são testados**

| Cenário                            | Teste de módulo                                                                                             | Teste ponta a ponta                                                                                       |
| ---------------------------------- | ----------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Saldo suficiente                   | `ImmediateTransferTests.TransferWithSufficientBalance_CompletesAndMovesFunds`                               | `TransfersEndpointTests.ImmediateTransfer_Returns201CompletedAndMovesFunds`                               |
| Usando cheque especial             | `ImmediateTransferTests.TransferUsingOverdraft_CompletesLeavingNegativeBalance`                             | `TransfersEndpointTests.ImmediateTransferUsingOverdraft_Returns201AndLeavesBalanceNegative`               |
| Ultrapassa o cheque especial       | `ImmediateTransferTests.TransferAboveBalancePlusOverdraft_FailsWithInsufficientFundsAndIsPersistedAsFailed` | `TransfersEndpointTests.ImmediateTransferWithoutFunds_Returns422WithCodeAndTransferId`                    |
| Recebimento usando cheque especial | `ImmediateTransferTests.TransferToAccountUsingOverdraft_CoversOverdraftFirst`                               | `AccountsEndpointTests.ReceivingWhileUsingOverdraft_StatementShowsOverdraftCoveredFirst`                  |
| Acima do limite da hora            | `HourlyLimitTests.TransfersAboveHourlyAmountLimit_FailWithHourlyAmountLimitExceeded`                        | `HourlyLimitsEndToEndTests.DaytimeTransferAboveHourlyAmountLimit_Returns422HourlyAmountLimitExceeded`     |
| Excesso de tentativas              | `HourlyLimitTests.AttemptsBeyondHourlyLimit_FailWithAttemptLimitExceeded`                                   | `HourlyLimitsEndToEndTests.SixthAttemptInTheHour_Returns422AttemptLimitExceeded`                          |
| Período noturno                    | `HourlyLimitTests.NighttimeTransfer_UsesNighttimeAmountLimit`                                               | `HourlyLimitsEndToEndTests.NighttimeTransferAboveNightLimit_Returns422AndWithinLimitSucceeds`             |
| Agendada sem saldo na execução     | `ScheduledTransferTests.ExecutingScheduledTransfer_WithoutFundsAtExecutionTime_FailsWithInsufficientFunds`  | `ScheduledTransferEndToEndTests.ScheduledTransferWithoutFundsAtExecution_EndsFailedWithInsufficientFunds` |
| Simultâneas com o mesmo saldo      | `ConcurrencyTests.SimultaneousTransfersSharingTheSameBalance_OnlyOneCompletes`                              | `ConcurrencyEndToEndTests.SimultaneousHttpTransfersSharingTheSameBalance_OnlyOneCompletes`                |

**Regras que inferimos além do enunciado**

- **Janela móvel de 60 minutos, e não a hora cheia.** A janela é contada a partir de cada tentativa: vale tudo o que aconteceu nos 60 minutos anteriores. Com a hora do relógio, alguém faria o limite inteiro às 10h59 e de novo às 11h01, ou seja, o dobro em dois minutos.
- **Dia e noite no fuso de São Paulo.** O dia vai das 06h às 20h em `America/Sao_Paulo`, independentemente do fuso do servidor, que em container é UTC. Sem isso, a regra noturna mudaria conforme onde a aplicação roda. Horários e fuso são configuráveis.
- **O que conta como tentativa.** Toda transferência que chega à execução conta, concluída ou rejeitada. Entrada inválida e conta inexistente, que respondem 400 e 404, não contam, porque nem chegaram a tentar mover dinheiro. Rejeitadas contam porque é justamente o caso que o limite deve conter: alguém testando valores.
- **O limite de valor soma só as concluídas.** Uma transferência rejeitada não movimentou dinheiro, então consumir o limite de valor com ela puniria o usuário duas vezes pela mesma recusa.
- **Ordem de avaliação.** Primeiro as tentativas, depois o valor por hora, e só então saldo e status. Assim, quem já esgotou as tentativas nem chega a travar as linhas das contas.
- **Toda tentativa imediata é persistida.** Uma transferência rejeitada é gravada como `Failed` com o código do motivo e devolve 422 com o `transferId`. É isso que torna a tentativa auditável e contável.
- **Status da conta checado duas vezes no agendamento.** Ao agendar, uma conta inativa já recusa o pedido sem gravar nada. Na execução, o status é checado de novo, pois a conta pode ter sido bloqueada nesse meio-tempo.
- **Presa em processamento vira falha, e não nova tentativa.** Uma agendada em `Processing` por mais de 10 minutos é marcada `Failed` com `ProcessingTimeout`. Reexecutar arriscaria mover o dinheiro duas vezes sem um mecanismo de reconciliação.
- **Teto do saldo.** Nenhum crédito leva o saldo acima do maior valor de `numeric(18,2)`. A transferência termina `Failed` com `DestinationBalanceLimitExceeded`, em vez de um erro de estouro no banco.
- **Limites personalizados por conta.** Cada conta pode ter a própria política de dia e de noite, já que o enunciado diz que "cada pessoa possui um limite". Sem política própria, valem os padrões do enunciado.
- **Reduzir o cheque especial abaixo do valor em uso é permitido.** A conta só deixa de conseguir enviar até cobrir a diferença. Recusar a alteração impediria o banco de reduzir o risco de um cliente.
- **CPF com ou sem máscara.** A API aceita só os dígitos ou a máscara completa, e grava só os dígitos. Assim, `98765432100` e `987.654.321-00` são a mesma pessoa para a regra de unicidade.
  Não adicionamos o calculo para verificar se o CPF é valido, mas seria adequado em um projeto real.

## Benchmark

Um ambiente parecido com produção, separado do compose principal, em `benchmark/`:

- **Componentes:** nginx servindo o front e fazendo proxy para a API, CORS restrito à origem do gateway, e API e worker em modo `Production`.
- **Limites por container:** API com 1 CPU e 512 MiB, e postgres com 1 CPU e 1 GiB.
- **Massa de dados:** 200 mil contas, 300 mil transferências e 800 mil lançamentos, dos quais 600 mil são de transferências e o restante é uma abertura por conta.
- **Carga:** o k6 roda oito cenários: busca, paginação profunda, transferências imediatas, contas disputadas, repetição idempotente, agendadas, leituras e CORS.

A primeira execução revelou dois problemas que não aparecem em uma base pequena:

- **Nenhuma de 300 agendadas concluída em 60 s.**
  - **Causa:** o outbox publicava no máximo 50 mensagens por segundo, abaixo dos 80 a 190 eventos por segundo gerados, então a fila só crescia.
  - **Correção:** o ritmo passou a 500 por segundo. As 300 terminaram, em média cerca de 1 s depois do vencimento.
- Além disso, a varredura de transferências presas não tinha índice. O índice parcial `ix_transfers_processing_started_at_stale` levou a consulta de 406 ms para 0,26 ms.

Resultado final, com 0% de erros HTTP:

| Indicador                                   | Saldo guardado | Saldo calculado pelo ledger |
| ------------------------------------------- | -------------- | --------------------------- |
| Busca de contas, p95                        | 11,7 ms        | 15,2 ms                     |
| Paginação profunda, requisições por segundo | 1.104,7        | 494,6                       |
| Paginação profunda, p95                     | 71,8 ms        | 87,9 ms                     |
| Transferências imediatas a 80 req/s, p95    | 10,4 ms        | 11,6 ms                     |
| 5 contas disputadas por 40 usuários, p95    | 499 ms         | 498 ms, sem deadlock        |
| Leituras de conta, extrato e histórico, p95 | 4,0 ms         | 4,5 ms                      |

Limites do experimento: uma única instância da API, e o k6 no mesmo host.

Para reproduzir, o que leva cerca de 15 minutos:

```bash
docker compose down
cd benchmark && ./run.sh
```

## Uma forma mais correta de tratar dinheiro

Hoje o valor é `decimal` no C# e `numeric(18,2)` no postgres. Os dois trabalham em base 10, então 0,10 + 0,70 dá exatamente 0,80, e o back não sofre o erro de arredondamento do ponto flutuante binário.

O ponto fraco está no contrato: o valor trafega como número JSON e vira `number`, um double, no front. Hoje isso é seguro, porque o front só converte o valor na borda e não faz contas. Qualquer cálculo futuro no cliente, porém, herdaria a imprecisão binária.

A abordagem mais correta do que a que usamos é um value object `Money`, com o valor inteiro na menor unidade da moeda, os centavos em `long` e `bigint`, mais a moeda, usado de ponta a ponta, inclusive na API. Um valor de R$ 1.050,99 viraria `{ "amountInCents": 105099, "currency": "BRL" }`:

- **Precisão:** um inteiro é representado em binário sem aproximação. Por isso somas e subtrações nunca arredondam, em nenhuma camada, e o arredondamento só aparece onde há divisão, como em juros e rateio, em um único ponto explícito e com regra definida.
- **Desempenho:** a aritmética com `long` é feita direto pela CPU, enquanto o `decimal` de 128 bits é calculado em software. No banco, `bigint` ocupa 8 bytes fixos, enquanto `numeric` tem tamanho variável e aritmética mais cara. Por isso somas e comparações em volume custam menos CPU e menos espaço.
- **Conversão de moedas:** como o `Money` carrega a moeda, somar BRL com USD vira erro de tipo, e não um bug silencioso. A conversão passa a ser uma operação explícita, com taxa e regra de arredondamento para a menor unidade da moeda de destino, que nem sempre tem duas casas: o iene não tem nenhuma.

Não implementamos essa abordagem porque ela exigiria um novo tipo no domínio, conversões no EF Core e na API e uma mudança de contrato com o front. Com uma única moeda e nenhuma conta com divisão, esse custo não se paga. Preferimos a simplicidade, sabendo exatamente o que trocaríamos se o escopo crescesse.
