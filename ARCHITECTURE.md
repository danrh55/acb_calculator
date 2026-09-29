# ACB Calculator - Architecture

## Why

Canadian investors must track the Adjusted Cost Base (ACB) of securities held in
non-registered accounts to report capital gains correctly. Broker transaction
exports are fragmented, overlapping, and mutually redundant, so reconciling them
by hand is error-prone. This tool ingests broker CSVs, normalizes them into a
single transaction schema, and computes ACB and realized gains on demand.

## Architecture at a glance

```
  +----------------------------+
  |      UI  (Acb.View)        |
  |  broker / mapping authoring|
  +-------------+--------------+
                |
      +---------+---------+
      |                   |
      v                   v
 +-------------+     +-------------+
 |  Ingestion  |     | ACB Engine |
 |  + FX       |     |  (queries) |
 +------+------+     +------+------+
        |                   |
        | produces          | reads
        v                   v
  +----------------------------+
  |           Store            |
  |  schema, persistence,      |
  |  idempotent (dedup) writes |
  +----------------------------+
```

- **Store** owns the transaction schema and all persistence. Writes are idempotent.
- **Ingestion** reads broker CSVs and produces normalized transactions, including
  FX conversion. Depends on the Store.
- **ACB Engine** computes ACB and realized gains by querying the Store. It owns
  what a transaction *means*.
- **UI** is the Avalonia desktop app. It owns authoring broker column mappings.

## Data flow: importing a file

1. User picks a local CSV. It is opened read-only; the source file is never
   modified.
2. Ingestion selects the broker's column-mapping spec.
3. Rows are mapped into the normalized transaction schema.
4. Non-CAD rows resolve an FX rate.
5. Registered-account transactions are filtered out.
6. Survivors are written to the Store, which deduplicates them.

## FX conversion

Owned by Ingestion. The Store persists rates but never calls the network.

- CAD transactions need no rate.
- The Store is asked for a cached rate for a given currency and trade date.
- On a miss, Ingestion calls the Bank of Canada Valet API, then hands the
  resolved rate back to the Store to persist.
- Valet has gaps on weekends and holidays; the most recent prior rate is carried
  forward.
- A transaction retains both the applied rate and the resulting CAD amount, so
  the engine's ACB query stays a simple sum and a bad rate can be corrected
  without re-importing.

## Data model

The Store owns the schema, shaped by the ACB Engine's query need: ACB per
security, computed chronologically across the transaction log.

Constraints the schema must satisfy:

- It carries every field needed to compute ACB, so the engine never has to
  return to raw broker files.
- It carries a dedup key, so writes stay idempotent across overlapping exports
  and repeat imports.
- Rates are cached separately from transactions, keyed by currency and date, so
  re-importing a file does not re-call Valet for dates already resolved.
- Security identity survives renames and broker-specific ticker formatting, so
  ACB is not split across what are really the same holding.

## Broker mappings

A broker is a **data mapping specification, not a code adapter**. The user
authors it in the UI by assigning each CSV column to a field in the transaction
schema; the stored spec is then applied to any file from that broker.

Mappings cover **columns only**. What a transaction *means* - how a buy affects
ACB, how fees are treated - belongs to the ACB Engine.

## ACB rules

- ACB is computed per security across all non-registered accounts (CRA
  "identical properties" rule), not per account.
- Realized gains are computed per sale.
- ACB is derived from the transaction log on every computation. There is no
  running total to keep in sync.

## Known requirements (not yet implemented)

- Oversells rejected (no shorts/options); negative ACB is a warning; commission
  may be zero.
- Transfers between brokers are an inert `transfer` type with no ACB effect,
  removing the need for cross-broker pairing.
- Return of capital and reinvested distributions - schema-aware, not computed.
- Corporate actions (splits/mergers). Unrealized gains.

## Deliberately out of scope

- **Cloud or remote files** - local paths only.
- **High throughput** - ACB is recomputed on read, never materialized.
- **Code-level broker adapters** - mappings are data, not code.
- **Automatic column inference** - the user authors mappings.
