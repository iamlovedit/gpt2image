# OpenAI image2 中转站

前端管理后台（React + Ant Design 暗色）+ 后端 SSE 转发（.NET 10 + PostgreSQL），
对外保持 OpenAI 兼容协议（`POST /v1/responses`）。

需求来源：`business.md`。方案：`.claude/plans/synthetic-wondering-bachman.md`。

---

## 目录

```
agents-fdd777ad09/
├── Dockerfile                  单容器生产构建
├── business.md                 需求文档
├── image_*                     上游协议样本
├── backend/
│   └── ImageRelay.Api/         .NET 10 Minimal API
└── frontend/                   React + Vite 管理后台
```

## 环境依赖

| 组件 | 版本 |
|---|---|
| .NET SDK | 10.0 |
| Node.js | ≥ 20 |
| pnpm | ≥ 9 |
| PostgreSQL | ≥ 14 |
| Seq (可选) | 日志聚合，不配置则跳过 |
| Docker (可选) | 用于单容器生产构建 / 本地联调 |

## 首次启动

默认采用“生产同源、开发代理”的模型：

- 生产环境：前端打包产物由 ASP.NET Core 直接托管，浏览器访问后台页面以及调用 `/api`、`/v1` 都走同一 origin。
- 开发环境：前端继续跑 `Vite`，通过 dev server proxy 把 `/api` 和 `/v1` 转发到本地后端。
- 后端不再配置 CORS；浏览器跨域直连 `/v1/responses` 不再是支持场景。

### 1. 准备数据库

```bash
# 本地用 docker 起 postgres
docker run --name image-relay-db -d \
  -e POSTGRES_DB=image_relay \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 postgres:16

# 可选：Seq
docker run --name image-relay-seq -d \
  -e ACCEPT_EULA=Y \
  -p 5341:80 datalust/seq
```

### 2. 启动后端

```bash
cd backend

# 推荐：设置关键环境变量
export DATABASE_URL='Host=localhost;Port=5432;Database=image_relay;Username=postgres;Password=postgres'
export JWT_SECRET='please-replace-with-32-byte-random-string'
export BOOTSTRAP_ADMIN_USERNAME='admin'
export BOOTSTRAP_ADMIN_PASSWORD='admin123!'
export SEQ_URL='http://localhost:5341'    # 可选

dotnet restore
dotnet run --project ImageRelay.Api
```

启动后会：
- 自动 `EnsureCreatedAsync` 创建所有表
- 若无 admin 则用上面的环境变量种子一个
- 若无 model mapping 则种子默认 `gpt-5.4 → gpt-5.4`
- 监听 `http://localhost:5000`

### 3. 启动前端

```bash
cd frontend
pnpm install
pnpm dev
```

访问 `http://localhost:5173`，用 bootstrap 账号登录。
Vite 会将 `/api` 和 `/v1` 代理到 `http://localhost:5000`，本地开发不需要再配置后端允许来源。

### 4. 生产单容器运行

```bash
docker build -t image-relay .

docker run --rm -p 5000:5000 \
  -e DATABASE_URL='Host=host.docker.internal;Port=5432;Database=image_relay;Username=postgres;Password=postgres' \
  -e JWT_SECRET='please-replace-with-32-byte-random-string' \
  -e BOOTSTRAP_ADMIN_USERNAME='admin' \
  -e BOOTSTRAP_ADMIN_PASSWORD='admin123!' \
  image-relay
```

启动后可直接访问 `http://localhost:5000`：

- `/` 返回管理后台页面
- `/api/*` 提供后台 API
- `/v1/responses` 继续提供 OpenAI 兼容代理接口

---

## 环境变量

| 名称 | 作用 | 默认 |
|---|---|---|
| `DATABASE_URL` | Postgres 连接串（覆盖 `ConnectionStrings:Default`） | `appsettings.json` 中的值 |
| `JWT_SECRET` | JWT 签名密钥，至少 16 字节 | — |
| `BOOTSTRAP_ADMIN_USERNAME` | 首次管理员用户名 | `admin` |
| `BOOTSTRAP_ADMIN_PASSWORD` | 首次管理员密码 | — |
| `SEQ_URL` | Seq 地址；不设置则不启用 Seq sink | 未设置 |
| `UPSTREAM_BASE_URL` | 上游 base URL | `https://chatgpt.com` |
| `UPSTREAM_TOKEN_URL` | refresh_token 交换端点 | `https://auth.openai.com/oauth/token` |
| `UPSTREAM_TOKEN_CLIENT_ID` | 历史兼容配置；刷新 token 优先使用账号库 `ClientId` 字段 | appsettings 默认值 |
| `PROXY_MAX_RETRIES` | 单请求最大换账号重试次数 | `2` |
| `PROXY_COOLING_MINUTES` | 429 冷却分钟数 | `5` |
| `PROXY_ACCOUNT_CONCURRENCY` | 单账号默认并发上限 | `2` |
| `PROXY_REFRESH_SKEW_SECONDS` | 预留的 token 提前刷新窗口；当前转发链路仅在 401 后刷新 | `300` |

前端只有一个可选变量 `VITE_API_BASE`：

- 默认值为 `/api`
- 开发环境通过 Vite dev server 代理到后端
- 生产环境应保持同源访问，不建议配置为跨域地址

---

## 端到端冒烟测试

1. 登录后台 → 进入「上游账号」→ 点击「批量导入」粘贴：
   ```json
   [
     {"access_token":"<有效 access_token>","refresh_token":"<refresh_token>","client_id":"<匹配该 refresh_token 的 client_id>"}
   ]
   ```
   重复策略选「跳过重复」，点「导入」。

2. 进入「API Key」→ 点「创建 Key」→ 填名称 → 保存 → 弹窗复制 **完整明文 Key**（只显示一次）。

3. 终端测试流式转发：
   ```bash
   curl -N http://localhost:5000/v1/responses \
     -H "Authorization: Bearer sk-xxx" \
     -H "Content-Type: application/json" \
     -d @image_generation_request.json
   ```
   期待看到与 `image_generation_response` 同形态的 SSE 事件流。该接口面向服务端客户端或同源页面调用；若是其他浏览器 origin，需自行通过网关或后端代理转发。

4. 回到「请求日志」→ 刚刚那条记录应该出现；状态为「成功」，SSE 事件数 > 0。

5. 「Dashboard」上 24h 请求数 +1，成功率变化。

## 失败链路验证

- **token 过期**：转发链路不会因 `AccessTokenExpiresAt` 主动刷新；当上游返回 401 时才用 `refresh_token` 强制刷新并重试同一账号一次。
- **账号失效**：把 `access_token` 改成垃圾值 + refresh_token 保持有效 → 首次 401 触发刷新 → 刷新成功继续。
- **缺少 client_id**：账号记录没有 `ClientId` 时不会请求上游，刷新会直接失败并把账号置为 `invalid`；需重新导入或编辑补齐。
- **refresh 也失败**：明确凭据错误（400/401/403 或响应缺少 `access_token`）会置为 `invalid`；429/5xx/网络超时等临时错误会进入 `cooling`。
- **429**：用 mock server 返回 429 → 账号进 `cooling`，5 分钟后后台服务自动回 `healthy`。

---

## 当前 MVP 范围

✅ 已实现
- 管理员 JWT 登录 / 改密
- 管理后台静态资源同源托管（ASP.NET Core 直接提供前端构建产物）
- 上游账号：列表 / 筛选 / 批量导入 / 编辑 / 禁用启用 / 手动刷新 / 删除
- 调用方 API Key：列表 / 创建（明文一次性展示）/ 编辑 / 禁用启用 / 删除
- SSE 转发（`POST /v1/responses`）：零缓冲透传、LRU 调度、per-account 并发、401 后 token 刷新、状态机（cooling/banned/invalid）、换账号重试
- 请求日志：结构化入库、多条件筛选、详情查看
- Dashboard：核心数字卡片 + 最近 20 条日志
- 模型映射：只读（种子 `gpt-5.4 → gpt-5.4`）
- 调用方限流：per-Key RPM + 并发
- 冷却恢复后台服务（每 30s 扫描）
- Serilog → Console + Seq（可选）
- 单 Docker 镜像构建前后端并同源交付

🔜 后续迭代（MVP 范围外）
- Dashboard 趋势图
- 模型映射增删改 UI
- 系统设置可视化
- 图片产物落对象存储
- 单元 / 集成测试
- Redis / 多实例扩展
- CI

## 安全注意

- 生产环境**必须**用强随机 `JWT_SECRET`、修改默认 bootstrap 密码。
- API Key 库内存 SHA-256 不存明文；access_token / refresh_token 建议在应用层加密（目前明文存储，MVP 简化）。
- 敏感字段 access_token / refresh_token / 请求体 / base64 图片**不写日志**。
- 当前设计默认不向浏览器跨 origin 暴露 CORS；若未来要开放第三方网页直连 `/v1/*`，需要单独恢复并审查该策略。
