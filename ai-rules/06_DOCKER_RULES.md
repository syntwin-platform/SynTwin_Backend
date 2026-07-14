# 06 - Docker Rules

## Mục tiêu Docker MVP
Docker dùng để chuẩn hóa môi trường database trước. Sau khi database ổn mới Dockerfile API.

## Thứ tự đúng
1. Chạy SQL Server container.
2. Kiểm tra container healthy/running.
3. App local kết nối vào SQL Server container.
4. Chạy EF migration vào SQL Server container.
5. Khi API local ổn, mới viết Dockerfile cho API.
6. Sau cùng mới docker-compose full: API + SQL Server.

## Compose service đề xuất
MVP:

```text
syntwin-sqlserver
syntwin-api
```

Phase sau tùy chọn:

```text
syntwin-mosquitto
```

## .env rule
Dùng `.env` cho local dev:

```env
SQLSERVER_SA_PASSWORD=<strong-local-password>
SQLSERVER_PORT=11433
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

## Không làm trong MVP
- Không Kubernetes.
- Không Docker Swarm.
- Không Redis nếu chưa cần.
- Không multi-stage quá khó hiểu nếu team chưa debug được; nhưng Dockerfile API nên dùng multi-stage cơ bản.
