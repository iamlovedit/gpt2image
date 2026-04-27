import {
  ApiOutlined,
  FileSearchOutlined,
  LoginOutlined,
  PictureOutlined,
  ThunderboltOutlined,
} from "@ant-design/icons";
import {
  Alert,
  Button,
  Card,
  Col,
  Row,
  Space,
  Tabs,
  Tag,
  Typography,
} from "antd";
import { useNavigate } from "react-router-dom";
import imageGenerationRequestFile from "../../../image_generation_request.json";
import imageParseRequestFile from "../../../image_parse_request.json";
import { useIsMobile } from "@/hooks/useIsMobile";

const { Paragraph, Text, Title } = Typography;

type GenerationRequestFile = {
  body: unknown;
};

type ExampleDefinition = {
  key: string;
  title: string;
  icon: React.ReactNode;
  summary: string;
  requestCode: string;
  curlCode: string;
  responseCode: string;
  notes: string[];
};

const generationRequestCode = JSON.stringify(
  (imageGenerationRequestFile as GenerationRequestFile).body,
  null,
  2,
);

const parseRequestCode = JSON.stringify(imageParseRequestFile, null, 2);

const generationResponseSnippet = `event: response.created
data: {
  "type": "response.created",
  "response": {
    "id": "resp_xxx",
    "status": "in_progress",
    "model": "gpt-5.4"
  },
  "sequence_number": 0
}

event: response.output_item.added
data: {
  "type": "response.output_item.added",
  "item": {
    "id": "ig_xxx",
    "type": "image_generation_call",
    "status": "in_progress"
  },
  "output_index": 0,
  "sequence_number": 2
}

event: response.image_generation_call.partial_image
data: {
  "type": "response.image_generation_call.partial_image",
  "item_id": "ig_xxx",
  "output_format": "png",
  "partial_image_b64": "<base64 omitted>",
  "sequence_number": 10
}

event: response.completed
data: {
  "type": "response.completed",
  "response": {
    "id": "resp_xxx",
    "status": "completed"
  },
  "sequence_number": 12
}`;

const parseResponseSnippet = `event: response.created
data: {
  "type": "response.created",
  "response": {
    "id": "resp_xxx",
    "status": "in_progress",
    "model": "gpt-5.4"
  },
  "sequence_number": 0
}

event: response.output_item.added
data: {
  "type": "response.output_item.added",
  "item": {
    "id": "msg_xxx",
    "type": "message",
    "status": "in_progress"
  },
  "output_index": 0,
  "sequence_number": 2
}

event: response.output_text.delta
data: {
  "type": "response.output_text.delta",
  "item_id": "msg_xxx",
  "delta": "A. 空间整体描述\\n\\n这是一个由多个区域组成的室内空间……",
  "sequence_number": 4
}

event: response.output_text.delta
data: {
  "type": "response.output_text.delta",
  "item_id": "msg_xxx",
  "delta": "B. 带文字注释家具清单\\n1. 床\\n2. 衣柜\\n3. 书桌",
  "sequence_number": 5
}

event: response.completed
data: {
  "type": "response.completed",
  "response": {
    "id": "resp_xxx",
    "status": "completed"
  },
  "sequence_number": 9
}`;

function buildCurlSnippet(origin: string, bodyFileName: string) {
  return `export BASE_URL="${origin}"

curl -N "$BASE_URL/v1/responses" \\
  -H "Authorization: Bearer sk-your-api-key" \\
  -H "Content-Type: application/json" \\
  --data @"${bodyFileName}"`;
}

function CodeBlock({ code }: { code: string }) {
  return (
    <pre className="docs-code-block mono">
      <code>{code}</code>
    </pre>
  );
}

function ExampleCard({
  title,
  description,
  code,
}: {
  title: string;
  description: string;
  code?: string;
}) {
  return (
    <Card className="glow-box docs-example-card" bordered={false}>
      <Space direction="vertical" size={12} style={{ width: "100%" }}>
        <div>
          <div className="tech-title" style={{ fontSize: 12 }}>
            {title}
          </div>
          <Paragraph
            style={{
              marginTop: 8,
              marginBottom: 0,
              color: "#8A9ABF",
              lineHeight: 1.75,
            }}
          >
            {description}
          </Paragraph>
        </div>
        {code ? <CodeBlock code={code} /> : null}
      </Space>
    </Card>
  );
}

function ExampleTab({ example }: { example: ExampleDefinition }) {
  return (
    <Space direction="vertical" size={20} style={{ width: "100%" }}>
      <Card className="glow-box docs-example-card" bordered={false}>
        <Space direction="vertical" size={14} style={{ width: "100%" }}>
          <Space size={12} wrap>
            <div className="tech-title" style={{ fontSize: 14 }}>
              {example.title}
            </div>
            <Tag color="cyan">POST /v1/responses</Tag>
            <Tag color="blue">text/event-stream</Tag>
          </Space>
          <Paragraph style={{ margin: 0, color: "#C5D2E8", lineHeight: 1.8 }}>
            {example.summary}
          </Paragraph>
          <ul className="docs-list">
            {example.notes.map((note) => (
              <li key={note}>{note}</li>
            ))}
          </ul>
        </Space>
      </Card>

      <Row gutter={[20, 20]}>
        <Col xs={24} xl={12}>
          <ExampleCard
            title="请求 JSON"
            description="请求体直接发送到 `/v1/responses`。文档内容基于仓库样本整理，便于直接对照。"
            code={example.requestCode}
          />
        </Col>
        <Col xs={24} xl={12}>
          <ExampleCard
            title="cURL 示例"
            description="将上方 JSON 保存为本地文件后即可直接调用；`sk-` Key 由后台“API Key”页创建。"
            code={example.curlCode}
          />
        </Col>
        <Col xs={24}>
          <ExampleCard
            title="响应片段"
            description="这里只展示关键 SSE 事件，帮助确认流式时序和字段结构；完整响应会更长。"
            code={example.responseCode}
          />
        </Col>
      </Row>
    </Space>
  );
}

function QuickStartCard({
  title,
  value,
  description,
}: {
  title: string;
  value: string;
  description: string;
}) {
  return (
    <Card className="glow-box docs-info-card" bordered={false}>
      <div className="docs-label">{title}</div>
      <Text className="mono" style={{ color: "#00D4FF", fontSize: 16 }}>
        {value}
      </Text>
      <Paragraph style={{ margin: 0, color: "#8A9ABF", lineHeight: 1.75 }}>
        {description}
      </Paragraph>
    </Card>
  );
}

export default function DocsPage() {
  const navigate = useNavigate();
  const isMobile = useIsMobile();
  const origin =
    typeof window === "undefined"
      ? "https://your-image-relay-host"
      : window.location.origin;

  const examples: ExampleDefinition[] = [
    {
      key: "generation",
      title: "生图",
      icon: <PictureOutlined />,
      summary:
        '通过同一个 `/v1/responses` 接口发起图片生成，请求体中用 `tools: [{ type: "image_generation" }]` 指定生图能力。服务端会保持 SSE 透传，生成过程中可能收到多段 `partial_image`。',
      requestCode: generationRequestCode,
      curlCode: buildCurlSnippet(origin, "image_generation_body.json"),
      responseCode: generationResponseSnippet,
      notes: [
        "对外模型名保持 `gpt-5.4`，由中转站映射到上游 Codex responses 支持的模型。默认映射为 `gpt-5.4`。",
        "默认返回流式 SSE，生图过程中会出现 `response.image_generation_call.partial_image`。",
        "最终图片仍以 base64 事件透传，不需要额外轮询下载接口。",
      ],
    },
    {
      key: "parse",
      title: "解析图",
      icon: <FileSearchOutlined />,
      summary:
        "图片解析同样走 `/v1/responses`，区别只在输入内容：将图片 URL 放入 `input_image`，并通过文本指令约束输出格式。返回仍是 SSE，只是核心事件会变成文本增量输出。",
      requestCode: parseRequestCode,
      curlCode: buildCurlSnippet(origin, "image_parse_body.json"),
      responseCode: parseResponseSnippet,
      notes: [
        "和生图共用同一个接口，无需切换 SDK 或不同 base URL。",
        "请求中可同时包含 `input_text` 与 `input_image`，适合做结构化解析或图像理解。",
        "解析结果通常以 `response.output_text.delta` 连续输出，客户端应按顺序拼接。",
      ],
    },
  ];

  return (
    <div className="docs-shell">
      <div className="docs-container">
        <section className="docs-hero">
          <div className="docs-hero-content">
            <Space direction="vertical" size={18} style={{ width: "100%" }}>
              <Space size={[10, 10]} wrap>
                <Tag color="cyan">OPENAI IMAGE2 RELAY</Tag>
                <Tag color="blue">PUBLIC API DOCS</Tag>
                <Tag color="purple">SSE STREAMING</Tag>
              </Space>

              <div>
                <Title
                  level={1}
                  style={{
                    margin: 0,
                    color: "#E6ECF5",
                    fontSize: isMobile ? 30 : 42,
                    lineHeight: 1.15,
                  }}
                >
                  同一个接口，覆盖生图与解析图
                </Title>
                <Paragraph
                  style={{
                    maxWidth: 860,
                    marginTop: 16,
                    marginBottom: 0,
                    color: "#C5D2E8",
                    fontSize: isMobile ? 14 : 16,
                    lineHeight: isMobile ? 1.75 : 1.85,
                  }}
                >
                  面向第三方客户端的公开文档首页。对外统一使用{" "}
                  <Text className="mono" style={{ color: "#00D4FF" }}>
                    POST /v1/responses
                  </Text>
                  ，请求体通过 `tools` 与 `input`
                  区分图片生成和图片解析；响应保持{" "}
                  <Text className="mono" style={{ color: "#00D4FF" }}>
                    text/event-stream
                  </Text>{" "}
                  流式透传。
                </Paragraph>
              </div>

              <Space size={[8, 8]} wrap>
                <Tag icon={<ApiOutlined />} color="cyan">
                  Endpoint: /v1/responses
                </Tag>
                <Tag icon={<ThunderboltOutlined />} color="gold">
                  Bearer API Key
                </Tag>
                <Tag color="geekblue">Model Map: gpt-5.4 → gpt-5.4</Tag>
              </Space>

              <div className="docs-hero-actions">
                <Button
                  type="primary"
                  size={isMobile ? "middle" : "large"}
                  icon={<LoginOutlined />}
                  onClick={() => navigate("/login")}
                >
                  后台登录
                </Button>
                <Button
                  size={isMobile ? "middle" : "large"}
                  onClick={() => {
                    document
                      .getElementById("quick-start")
                      ?.scrollIntoView({ behavior: "smooth", block: "start" });
                  }}
                >
                  查看接入说明
                </Button>
              </div>
            </Space>
          </div>
        </section>

        <section id="quick-start" className="docs-section">
          <Space direction="vertical" size={18} style={{ width: "100%" }}>
            <div>
              <h2 className="docs-section-title">Quick Start</h2>
              <p className="docs-section-text">
                使用任意支持 OpenAI
                兼容协议的客户端都可以直接对接本站。唯一需要的调用信息是站点域名、一个
                `sk-` 前缀的 API Key，以及向 `/v1/responses` 发送 JSON 请求体。
              </p>
            </div>

            <Row gutter={[20, 20]}>
              <Col xs={24} sm={12} xl={6}>
                <QuickStartCard
                  title="Base URL"
                  value={origin}
                  description="浏览器当前访问域名就是推荐的调用入口。SDK 若支持 `baseURL`，可直接指向当前站点。"
                />
              </Col>
              <Col xs={24} sm={12} xl={6}>
                <QuickStartCard
                  title="Auth"
                  value="Authorization: Bearer sk-..."
                  description="调用方凭后台创建的 API Key 访问，不使用管理员登录态。"
                />
              </Col>
              <Col xs={24} sm={12} xl={6}>
                <QuickStartCard
                  title="Model"
                  value="gpt-5.4"
                  description="对外保持统一模型名，服务端内部映射到上游生图模型。"
                />
              </Col>
              <Col xs={24} sm={12} xl={6}>
                <QuickStartCard
                  title="Stream"
                  value="text/event-stream"
                  description="服务端按 SSE 原样透传事件，客户端应持续读取并按顺序消费。"
                />
              </Col>
            </Row>

            <Alert
              type="info"
              showIcon
              className="glow-box"
              style={{
                background: "rgba(0, 212, 255, 0.06)",
                border: "1px solid rgba(0, 212, 255, 0.18)",
              }}
              message="接入约定"
              description="生图和解析图共用同一个 `/v1/responses` 接口。差异只体现在请求体：生图依赖 `image_generation` tool，解析图依赖 `input_image + input_text` 组合输入。"
            />
          </Space>
        </section>

        <section className="docs-section">
          <Space direction="vertical" size={18} style={{ width: "100%" }}>
            <div>
              <h2 className="docs-section-title">Examples</h2>
              <p className="docs-section-text">
                以下示例覆盖最常见的两类调用。请求样本直接来自仓库中的联调文件，响应只保留关键
                SSE 事件，避免页面被超长 base64 或完整文本流淹没。
              </p>
            </div>

            <Tabs
              className="docs-tabs"
              size={isMobile ? "middle" : "large"}
              items={examples.map((example) => ({
                key: example.key,
                label: (
                  <Space size={8}>
                    {example.icon}
                    <span>{example.title}</span>
                  </Space>
                ),
                children: <ExampleTab example={example} />,
              }))}
            />
          </Space>
        </section>
      </div>
    </div>
  );
}
