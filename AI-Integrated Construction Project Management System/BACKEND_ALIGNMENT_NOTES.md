# BuildSense backend alignment notes

The EF model is aligned to `../Capstone_Revised.sql` by migration
`20260713065552_AlignWithCapstoneRevised`.

## Migration assumptions

- The migration creates one active default variant for every existing material. Its SKU is
  `LEGACY-{MaterialId}`, its unit is copied from `Materials.DefaultUnit`, and all operational
  `MaterialId` values are backfilled to the generated `VariantId` before variant foreign keys
  are enabled.
- Existing material requests with status `APPROVED` were already physically issued by the old
  service. They are migrated to `ISSUED`, with approved and issued quantities equal to the old
  requested quantity.
- Old inventory reservations were aggregate values without an allocation record and could not
  be mapped reliably. `ReservedQuantity` is reset to zero. New approvals create explicit
  `InventoryReservations`.
- Existing purchase orders had no warehouse. They are assigned to the lowest active
  `WarehouseId`; deployments must review and correct that allocation after migration if needed.
- Existing `DeliveryDate` values are preserved by renaming the column to
  `ExpectedDeliveryDate`. Delivered PO lines are treated as fully received; approved lines are
  backfilled into `OnOrderQuantity`.
- The migration aborts before schema changes if it finds duplicate active warehouse/material,
  task/material, or supplier/material pairs, invalid non-positive line quantities, or legacy
  purchase orders without any active warehouse.

## Operational workflow

1. A PM creates a pending material request. No inventory changes occur at creation.
2. A warehouse manager approves/reserves against one warehouse and per-line approved quantities.
3. The warehouse manager either issues the active reservations (creating immutable `ISSUE`
   transactions) or releases them.
4. Purchase-order approval increases `OnOrderQuantity`. Partial receipts update received/on-order/
   on-hand quantities and append immutable `RECEIPT` transactions.

## Deployment

### Local secrets

Never commit connection strings, API keys, refresh tokens, or JWT signing keys. The API
project already has a `UserSecretsId`, so configure local values from the solution directory
once with:

```powershell
dotnet user-secrets --project cpms_API set "ConnectionStrings:DefaultConnection" "<connection-string>"
dotnet user-secrets --project cpms_API set "SecretToken:Value" "<random-value-at-least-64-bytes>"
dotnet user-secrets --project cpms_API set "GoogleAI:ApiKey" "<google-ai-key>"
```

These values are stored outside the repository and remain available across commits. The
tracked [cpms_API/.env.example](cpms_API/.env.example) lists the equivalent environment
variable names for deployment configuration; it is only a template and is not loaded as a
local dotenv file automatically.

Use deployment-platform secret variables for non-development environments. Rotate every
credential that was previously committed, including the database password and Google AI key.
If the commits were pushed, remove the values from repository history with a history rewrite
tool after rotation, then force-push the rewritten branches according to your team's process.

Do not edit `__EFMigrationsHistory` manually. Back up the database, review the generated migration
SQL, then run the normal EF command from this solution directory:

```powershell
dotnet ef database update --project cpms_Infrastructure --startup-project cpms_API
```

The migration was applied successfully to the local `CapstoneDB_Clean` database on 2026-07-13.
Before applying it, a verified `COPY_ONLY` backup was created at:

`C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\CapstoneDB_Clean_pre_AlignWithCapstoneRevised_20260713_140851.bak`
