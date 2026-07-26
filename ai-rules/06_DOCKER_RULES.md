# 06 - Docker Rules

## Mục tiêu Docker local
Docker dùng để chạy các data service local ổn định:

- SQL Server.
- Redis.
- InfluxDB.

Ba process .NET được chạy độc lập:

```text
Syntwin.DbMigrator
Syntwin.Worker
Syntwin.Api
```

Thứ tự local:

```text
Start data services
-> run DbMigrator
-> start Worker
-> start API
```

## .env rule
Dùng `.env` cho local dev:

```env
SQLSERVER_SA_PASSWORD=<strong-local-password>
SQLSERVER_PORT=11433
REDIS_PORT=16379
INFLUXDB_PORT=18086
ASPNETCORE_ENVIRONMENT=Development
```

Không hard-code secret production vào git.

## Swagger rule
Khi chạy Docker full, Swagger phải mở được:

```text
http://localhost:<api-port>/swagger
```

Nếu dùng minimal OpenAPI mặc định, cần đảm bảo route docs dễ truy cập. Ưu tiên Swashbuckle Swagger UI cho người mới.

## Network rule
Trong container API, connection string dùng hostname service name:

```text
Server=syntwin-sqlserver,1433;Database=SyntwinDb;User Id=sa;Password=${SQLSERVER_SA_PASSWORD};TrustServerCertificate=True;
```

Khi chạy API ngoài Docker, dùng:

```text
Server=localhost,11433;Database=SyntwinDb;User Id=sa;Password=<strong-local-password>;TrustServerCertificate=True;
```

## Phase 4

Phase 4 tạo image riêng:

```text
Dockerfile.api
Dockerfile.worker
Dockerfile.migrator
```

Không dùng API container để chạy migration hoặc background worker.

## Không làm
- Không Kubernetes.
- Không Docker Swarm.
- Không đóng secret thật vào image.
- Không dùng `latest` làm rollback version chính.
