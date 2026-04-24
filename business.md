## 业务需求

创建一个 OpenAI image2 中转站系统。前端是一个管理后台，需要账号登录才能进入；后端用于转发 API 请求到 ChatGPT backend（`chatgpt.com/backend-api/codex/responses`），对外提供可被第三方客户端调用的图片生成 / 图片解析接口。

转发参考当前目录中 `image_` 开头的请求 / 响应样本文件。

---

## 一、角色与鉴权

系统存在两类主体，必须分开设计：

1. **管理员（Admin）**：登录管理后台，管理上游 OpenAI 账号、调用方 API Key、查看日志和统计。
2. **调用方（Client）**：持有 API Key 通过 HTTP 调用转发接口，不登录管理后台。

### 1.1 管理后台登录

- 账号密码登录，JWT 或 Cookie Session 皆可
- 支持修改密码
- 首次部署通过配置文件或环境变量初始化首个管理员

### 1.2 调用方 API Key

- 管理后台可创建 / 编辑 / 禁用 / 删除 Key
- 每个 Key 字段：名称、Key 值（`sk-` 前缀）、状态、过期时间、RPM 限额、并发上限、备注
- Key 值创建后只明文展示一次，库内存哈希
- 转发接口通过 `Authorization: Bearer sk-xxx` 识别调用方

---

## 二、上游 OpenAI 账号管理

### 2.1 账号字段

- `access_token`（会过期）
- `refresh_token`
- `client_id`（必须与该 `refresh_token` 来源匹配，用于刷新 token）
- `access_token_expires_at`
- 状态：`healthy` / `cooling` / `rate_limited` / `banned` / `invalid`
- 冷却截止时间 `cooling_until`
- 最近一次错误信息、最近一次使用时间
- 累计调用次数、累计失败次数
- 备注

### 2.2 批量导入

- JSON 数组格式，每项包含 `access_token`、`refresh_token` 和 `client_id`
- 导入时支持选择重复策略：跳过 / 覆盖 / 报错中止
- 导入后可选立即触发一次有效性校验（刷新 token 试探）

### 2.3 Token 生命周期

- 转发请求默认直接使用当前 `access_token`，不再因过期时间主动刷新
- 转发收到 401 后触发刷新并使用新 token 重试同一账号一次
- 刷新时使用账号库里的 `client_id`；缺失则直接置为 `invalid`，不使用全局默认值
- 同一账号刷新需加锁，防止并发重复刷新
- 刷新失败分级处理：400/401/403 或缺少 `access_token` → `invalid`；429/5xx/网络超时等临时错误 → `cooling`

### 2.4 状态机

| 当前状态 | 触发条件 | 新状态 |
|---|---|---|
| healthy | 收到 429 | cooling（默认 5 分钟，可配置） |
| healthy | 收到 403 / 账号封禁类错误 | banned |
| healthy | refresh 凭据错误 | invalid |
| healthy | refresh 临时错误 | cooling |
| cooling | 冷却时间到 | healthy |
| 任意 | 管理员手动禁用 / 启用 | disabled / healthy |

---

## 三、转发接口

### 3.1 对外协议

对外保持 **OpenAI 兼容**，客户端可以直接将 `baseURL` 指向本中转站：

- `POST /v1/responses`（图片生成 / 图片解析共用，根据 `tools` 区分）
- 响应保持 `text/event-stream` 流式透传
- 鉴权：`Authorization: Bearer <调用方 API Key>`

### 3.2 上游调度

- 从 `healthy` 账号池中选择：默认**最少最近使用（LRU）** 策略，后续可扩展为权重 / 轮询
- 请求中持有的账号需加并发计数（每账号并发上限可配置，默认 2）
- 请求成功：更新 `last_used_at`、累计次数
- 请求失败：根据错误码转换账号状态（见 2.4），并对**同一用户请求**换下一个 healthy 账号重试，最多重试 N 次（默认 2 次）

### 3.3 模型映射

- 支持配置对外模型名 → 上游模型名的映射表
- 例：`gpt-5.4` → `gpt-5.4`
- 未在白名单中的模型名直接拒绝，返回 400

### 3.4 流式透传要求

- .NET 端禁用响应 Buffering，`Response.Body` 直写
- 保留原始 SSE 事件名（`event:` / `data:`），不要解析后再序列化
- 对 `response.image_generation_call.partial_image` 这类大事件注意背压，不要整条缓存到内存

### 3.5 图片产物处理

上游返回的 partial / final image 是 base64，单次可达数 MB：

- 默认**透传 base64**，不落库
- 可选开关：将最终图片落到本地 / 对象存储，响应改写为 URL（后续可做，先留扩展点）
- Postgres 中**不存**图片 base64

### 3.6 限流

- 调用方维度：每 Key 的 RPM、并发
- 账号维度：每上游账号并发（见 3.2）
- 全局维度：系统总 QPS（兜底保护）

---

## 四、日志与统计

### 4.1 业务日志（Serilog → Seq）

每次转发请求必须记录：

- 请求 ID、调用方 Key ID、使用的上游账号 ID
- 对外模型名 / 上游模型名
- 开始时间、结束时间、耗时
- 是否流式、SSE 事件计数
- 上游最终 HTTP 状态、业务状态（成功 / 失败 / 重试）
- 错误信息、错误分类
- 图片产物大小（若有）
- **不记录** `access_token` / `refresh_token` 明文

### 4.2 管理后台 Dashboard

- 近 24h / 7d 调用量曲线
- 成功率、平均耗时、P95 耗时
- 上游账号健康度分布（饼图）
- Top 错误类型
- 调用方 Key 用量排行

---

## 五、前端页面清单

整体科技风。

1. **登录页**
2. **Dashboard**：见 4.2
3. **上游账号管理**：列表（含状态、最近使用、失败次数筛选）、批量导入（JSON）、单条编辑、手动禁用 / 启用、手动触发刷新、查看最近错误、删除
4. **调用方 Key 管理**：列表、创建（首次展示明文）、编辑限额、禁用 / 启用、删除
5. **模型映射管理**：映射表增删改
6. **请求日志**：按调用方 / 上游账号 / 状态 / 时间范围筛选，详情页展示请求摘要和错误信息
7. **系统设置**：重试次数、冷却时长、并发上限等可调参数
8. **个人中心**：修改密码

---

## 六、技术栈

### 前端

- React + Vite + pnpm
- 状态管理：zustand
- 整体 UI 科技风（深色基调、霓虹点缀、数据可视化为主视觉）
- 图表：推荐 echarts 或 recharts
- 组件库：推荐 Ant Design（深色主题）或 shadcn-style 自定义

### 后端

- .NET 10
- PostgreSQL
- Serilog + Seq
- 流式转发使用 `HttpClient` + `HttpCompletionOption.ResponseHeadersRead`
- 账号池并发控制使用进程内信号量（单实例部署），多实例扩展再引入 Redis

---

## 七、部署 / 非功能

- 单实例部署优先，预留多实例扩展点（账号锁、限流计数可切到 Redis）
- 配置项通过环境变量 / `appsettings.json` 注入：初始管理员、JWT Secret、Seq 地址、数据库连接、上游 base URL、默认冷却时间、默认并发上限
- CORS：管理后台接口仅允许前端域名；转发接口 `/v1/*` 允许任意来源（第三方客户端直连）
- 所有对外接口走 HTTPS（部署侧负责）
