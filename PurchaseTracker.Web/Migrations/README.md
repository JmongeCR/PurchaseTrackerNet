# Migrations

Run the following command from the solution root to create the initial migration:

```bash
dotnet ef migrations add InitialCreate --project PurchaseTracker.Shared --startup-project PurchaseTracker.Web
```

Then apply to database:

```bash
dotnet ef database update --project PurchaseTracker.Shared --startup-project PurchaseTracker.Web
```

Note: Migrations are also applied automatically on startup via `db.Database.Migrate()` in Program.cs.
