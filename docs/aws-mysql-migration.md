## AWS MySQL migration (SQLite -> MySQL)

This repo currently uses a local SQLite file at `ClientTracker/data/client-tracker.db3`.

To migrate that data into a MySQL database (e.g. AWS RDS), use the `ClientTracker.Migrator` tool.

### 1) Create a database (once)

Connect to your MySQL instance as an admin user and create a database (choose a name you want to use):

```sql
CREATE DATABASE client_tracker CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

### 2) Run the migrator

Set a MySQL connection string (do not commit it to git):

PowerShell:

```powershell
$env:CLIENTTRACKER_MYSQL_CONNECTION = "Server=<host>;Port=3306;Database=client_tracker;User ID=<user>;Password=<password>;SslMode=Required"
dotnet run --project .\\ClientTracker.Migrator\\ClientTracker.Migrator.csproj -c Release
```

Optional: specify the SQLite file explicitly:

```powershell
dotnet run --project .\\ClientTracker.Migrator\\ClientTracker.Migrator.csproj -c Release -- --sqlite .\\ClientTracker\\data\\client-tracker.db3
```

Notes:
- The migrator creates tables if they don't exist.
- The migrator **truncates** existing rows in those tables before inserting (intended for one-time migration).

### 3) Next: don’t ship DB credentials in the app

MySQL credentials cannot be securely hidden inside a client application (Windows/Android/macOS). If the app can connect, a determined user can extract them.

The correct architecture is:
- MAUI app talks to a backend API (authenticated).
- Backend API talks to MySQL with the credentials stored as server-side secrets (AWS Secrets Manager / env vars).

This repo includes a starter backend at `ClientTracker.Api` with a `/health` endpoint that validates the DB connection.

Run locally:

```powershell
$env:CLIENTTRACKER_MYSQL_CONNECTION = "Server=<host>;Port=3306;Database=client_tracker;User ID=<user>;Password=<password>;SslMode=Required"
dotnet run --project .\\ClientTracker.Api\\ClientTracker.Api.csproj
```
