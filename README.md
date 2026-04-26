# OpenAI image2 中转站

OpenAI image2 中转站是一个同源交付的管理后台与 OpenAI 兼容代理服务。生产环境通过一个应用容器提供前端静态页面、后台管理 API 和 `/v1/responses` 代理接口，并通过 Docker Compose 同时编排 PostgreSQL 数据库与可选 Seq 日志服务。

## 项目概览

应用由三部分组成：

- 管理后台：用于维护上游账号、调用方 API Key、模型映射、请求日志和系统设置。
- 后端服务：提供管理 API、调用方鉴权、请求限流、上游账号调度、SSE 转发和后台任务。
- PostgreSQL：保存管理员、上游账号、调用方 API Key、模型映射、请求日志和上游请求头设置。

生产部署采用同源模式：

- 浏览器访问 `/` 加载管理后台。
- 管理后台调用 `/api/*`。
- 调用方通过 `POST /v1/responses` 使用 OpenAI 兼容代理接口。
- 静态资源、管理 API 和代理接口都由同一个 ASP.NET Core 应用提供，不需要额外配置 CORS。

## 技术架构

| 层级   | 技术栈                                           | 说明                                                                   |
| ------ | ------------------------------------------------ | ---------------------------------------------------------------------- |
| 前端   | React 18、Vite、Ant Design、React Query、Zustand | 构建后输出到 `dist`，由后端容器内的 ASP.NET Core 同源托管              |
| 后端   | .NET 10 Minimal API                              | 提供 `/api/*` 管理接口、`/v1/responses` 代理接口和 `/healthz` 健康检查 |
| 数据库 | PostgreSQL 17、EF Core migrations                | 应用启动时自动执行数据库迁移，并执行初始化种子数据                     |
| 认证   | JWT、API Key                                     | 管理后台使用 JWT；调用方访问 `/v1/responses` 使用 API Key              |
| 代理   | HttpClient、SSE 透传                             | 校验调用方 API Key 后选择上游账号，转发到 ChatGPT 上游接口并透传事件流 |
| 日志   | Serilog、Console、Seq                            | 默认输出到容器日志；设置 Seq 后可集中查看结构化日志                    |
| 容器   | Docker multi-stage build、Docker Compose         | 一个应用镜像内完成前端构建、后端发布和静态资源托管                     |

## 运行时组件

Docker Compose 默认启动两个服务：

- `app`：应用服务，镜像名为 `image-relay:${IMAGE_TAG:-local}`，容器内监听 `5000`，宿主机端口由 `APP_PORT` 控制。
- `db`：PostgreSQL 17，使用 `postgres_data` 数据卷持久化数据。

可选启动一个日志服务：

- `seq`：Seq 日志服务，通过 `observability` profile 启用，使用 `seq_data` 数据卷持久化数据。

`app` 会等待 `db` 通过 healthcheck 后再启动。应用自身提供 `/healthz`，Compose 会使用该接口检查服务健康状态。

## 请求链路

管理后台链路：

```text
Browser
  -> GET /
  -> ASP.NET Core static files
  -> frontend wwwroot

Browser
  -> /api/*
  -> ASP.NET Core Minimal API
  -> PostgreSQL
```

代理链路：

```text
Client
  -> POST /v1/responses
  -> API Key 校验
  -> 调用方限流与并发控制
  -> 上游账号选择
  -> https://chatgpt.com/backend-api/codex/responses
  -> SSE 事件流透传
```

上游账号的 access token 在转发过程中维护。后端会处理 token 刷新、账号冷却恢复和账号状态更新；请求结果会写入请求日志，供后台查询和 Dashboard 汇总。

## Docker 镜像构建方式

`Dockerfile` 使用 multi-stage build：

1. `node:20-alpine` 构建前端：
   - 安装 pnpm。
   - 执行前端依赖安装。
   - 执行 `pnpm build` 生成静态资源。
2. `mcr.microsoft.com/dotnet/sdk:10.0` 发布后端：
   - 还原 .NET 依赖。
   - 执行 `dotnet publish` 输出发布产物。
3. `mcr.microsoft.com/dotnet/aspnet:10.0` 作为运行时镜像：
   - 复制后端发布产物。
   - 复制前端构建产物到 `wwwroot`。
   - 通过 `dotnet ImageRelay.Api.dll` 启动应用。

容器内应用监听 `http://+:5000`。

## Docker Compose 部署

### 1. 准备环境变量

复制示例配置：

```bash
cp .env.example .env
```

编辑 `.env`，至少修改以下值：

```dotenv
POSTGRES_PASSWORD=change-me-postgres-password
JWT_SECRET=please-replace-with-32-byte-random-string
BOOTSTRAP_ADMIN_PASSWORD=please-change-this-admin-password
```

`JWT_SECRET` 建议使用足够长的随机字符串。`BOOTSTRAP_ADMIN_PASSWORD` 是首次初始化管理员账号时使用的密码。

### 2. 启动应用和数据库

```bash
docker compose up -d
```

启动后会执行以下动作：

- 拉区最新应用镜像。
- 启动 PostgreSQL。
- 等待数据库健康检查通过。
- 启动应用服务。
- 自动执行 EF Core migrations。
- 初始化管理员账号和默认配置数据。

默认访问地址：

- 管理后台：`http://localhost:5000/`
- 健康检查：`http://localhost:5000/healthz`
- 管理 API：`/api/*`
- OpenAI 兼容代理接口：`POST /v1/responses`

如果在 `.env` 中修改了 `APP_PORT`，访问地址中的端口也需要同步替换。

### 3. 可选启用 Seq

如需启用 Seq，先在 `.env` 中设置：

```dotenv
SEQ_URL=http://seq:80
```

然后使用 `observability` profile 启动：

```bash
docker compose --profile observability up -d
```

默认 Seq 访问地址：

```text
http://localhost:5341/
```

如果在 `.env` 中修改了 `SEQ_PORT`，访问地址中的端口也需要同步替换。

## 环境变量

| 变量                       | 默认值        | 说明                                                  |
| -------------------------- | ------------- | ----------------------------------------------------- |
| `APP_PORT`                 | `5000`        | 应用映射到宿主机的端口                                |
| `IMAGE_TAG`                | `local`       | 应用镜像 tag，最终镜像名为 `image-relay:${IMAGE_TAG}` |
| `POSTGRES_DB`              | `image_relay` | PostgreSQL 数据库名                                   |
| `POSTGRES_USER`            | `postgres`    | PostgreSQL 用户名                                     |
| `POSTGRES_PASSWORD`        | 无            | PostgreSQL 密码，必须在 `.env` 中设置                 |
| `POSTGRES_PORT`            | `5432`        | PostgreSQL 映射到宿主机的端口                         |
| `JWT_SECRET`               | 无            | 后台 JWT 签名密钥，必须在 `.env` 中设置               |
| `BOOTSTRAP_ADMIN_USERNAME` | `admin`       | 首次初始化管理员用户名                                |
| `BOOTSTRAP_ADMIN_PASSWORD` | 无            | 首次初始化管理员密码，必须在 `.env` 中设置            |
| `SEQ_URL`                  | 空            | Seq 地址；留空表示不启用 Seq sink                     |
| `SEQ_PORT`                 | `5341`        | Seq 映射到宿主机的端口                                |

Compose 会在容器内生成应用使用的数据库连接串：

```text
Host=db;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
```

## 访问与验证

查看服务状态：

```bash
docker compose ps
```

查看应用日志：

```bash
docker compose logs -f app
```

验证健康检查：

```bash
curl http://localhost:5000/healthz
```

健康接口正常时会返回 JSON 响应，表示应用进程已启动并可处理请求。

登录管理后台：

```text
http://localhost:5000/
```

使用 `.env` 中的 `BOOTSTRAP_ADMIN_USERNAME` 和 `BOOTSTRAP_ADMIN_PASSWORD` 登录。管理员账号只在数据库内不存在管理员时自动创建。

调用方接口入口：

```text
POST http://localhost:5000/v1/responses
Authorization: Bearer <client-api-key>
Content-Type: application/json
```

调用方 API Key 需要先在管理后台创建。API Key 明文只在创建时展示一次，数据库中保存的是哈希值。

## 常用运维命令

停止服务：

```bash
docker compose down
```

停止服务并删除数据卷：

```bash
docker compose down -v
```

重新构建并启动：

```bash
docker compose up -d --build
```

查看数据库日志：

```bash
docker compose logs -f db
```

查看应用容器健康状态：

```bash
docker inspect --format='{{json .State.Health}}' "$(docker compose ps -q app)"
```

进入应用容器：

```bash
docker compose exec app bash
```

进入数据库：

```bash
docker compose exec db sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```
