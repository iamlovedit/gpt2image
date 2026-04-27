import type { CSSProperties, ReactNode } from "react";
import { useState } from "react";
import { Card, Form, Input, Button, App } from "antd";
import {
  ApiOutlined,
  CloudServerOutlined,
  DashboardOutlined,
  FileSearchOutlined,
  KeyOutlined,
  LockOutlined,
  LoginOutlined,
  SafetyCertificateOutlined,
  UserOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router-dom";
import { useAuthStore } from "@/stores/authStore";
import { login } from "@/api/auth";
import { extractError } from "@/api/client";
import { useIsMobile } from "@/hooks/useIsMobile";

const colors = {
  cyan: "#00D4FF",
  purple: "#7B61FF",
  green: "#22D3A1",
  yellow: "#FFB13C",
  text: "#E6ECF5",
  secondary: "#8A9ABF",
  tertiary: "#5A6C91",
  panel: "rgba(16, 23, 42, 0.82)",
  panelStrong: "rgba(10, 15, 29, 0.92)",
  border: "rgba(0, 212, 255, 0.18)",
};

const styles: Record<string, CSSProperties> = {
  shell: {
    minHeight: "100vh",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    padding: "clamp(18px, 4vw, 40px)",
    overflow: "auto",
    background:
      "linear-gradient(135deg, rgba(7, 10, 19, 0.92), rgba(10, 15, 29, 0.72))",
  },
  stage: {
    width: "100%",
    maxWidth: 1180,
    display: "flex",
    flexWrap: "wrap-reverse",
    alignItems: "stretch",
    justifyContent: "center",
    gap: 28,
  },
  brandPane: {
    flex: "1 1 500px",
    minWidth: 0,
    display: "flex",
    flexDirection: "column",
    justifyContent: "center",
    gap: 22,
    padding: "8px 0",
  },
  eyebrow: {
    color: colors.secondary,
    fontSize: 12,
    letterSpacing: "0.16em",
    textTransform: "uppercase",
  },
  title: {
    margin: 0,
    color: colors.cyan,
    fontFamily: '"JetBrains Mono", ui-monospace, monospace',
    fontSize: 46,
    fontWeight: 700,
    letterSpacing: "0.06em",
    lineHeight: 1.05,
    textShadow: "0 0 26px rgba(0, 212, 255, 0.28)",
    overflowWrap: "anywhere",
  },
  subtitle: {
    maxWidth: 620,
    margin: 0,
    color: "#C5D2E8",
    fontSize: 16,
    lineHeight: 1.8,
  },
  statusGrid: {
    display: "flex",
    flexWrap: "wrap",
    gap: 12,
  },
  topologyPanel: {
    width: "100%",
    maxWidth: 680,
    padding: 20,
    background:
      "linear-gradient(135deg, rgba(16, 23, 42, 0.86), rgba(15, 24, 41, 0.66))",
    backdropFilter: "blur(10px)",
  },
  topologyHeader: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    gap: 12,
    marginBottom: 16,
  },
  topologyNodes: {
    display: "flex",
    flexWrap: "wrap",
    alignItems: "stretch",
    gap: 12,
  },
  loginColumn: {
    flex: "0 1 420px",
    width: "min(100%, 420px)",
    display: "flex",
    alignItems: "center",
  },
  loginCard: {
    width: "100%",
    padding: 0,
    background: colors.panel,
    backdropFilter: "blur(12px)",
    boxShadow: "0 24px 80px rgba(0, 0, 0, 0.34)",
  },
  cardHeader: {
    marginBottom: 28,
  },
  accessBadge: {
    width: "fit-content",
    display: "inline-flex",
    alignItems: "center",
    gap: 8,
    padding: "7px 10px",
    border: "1px solid rgba(34, 211, 161, 0.28)",
    borderRadius: 8,
    color: colors.green,
    background: "rgba(34, 211, 161, 0.08)",
    fontFamily: '"JetBrains Mono", ui-monospace, monospace',
    fontSize: 12,
    letterSpacing: "0.08em",
    textTransform: "uppercase",
  },
  loginTitle: {
    margin: "18px 0 8px",
    color: colors.text,
    fontSize: 26,
    lineHeight: 1.25,
    fontWeight: 650,
  },
  loginHint: {
    margin: 0,
    color: colors.secondary,
    fontSize: 14,
    lineHeight: 1.7,
  },
  footnote: {
    display: "flex",
    gap: 10,
    alignItems: "flex-start",
    marginTop: 18,
    padding: "14px 16px",
    border: "1px solid rgba(138, 154, 191, 0.14)",
    borderRadius: 8,
    background: "rgba(5, 9, 18, 0.36)",
    color: colors.tertiary,
    fontSize: 12,
    lineHeight: 1.6,
  },
};

function StatusPill({
  icon,
  label,
  value,
  color,
}: {
  icon: ReactNode;
  label: string;
  value: string;
  color: string;
}) {
  return (
    <div
      style={{
        flex: "1 1 156px",
        minWidth: 0,
        display: "flex",
        alignItems: "center",
        gap: 10,
        padding: "12px 14px",
        border: `1px solid ${color}44`,
        borderRadius: 8,
        background: "rgba(10, 15, 29, 0.54)",
      }}
    >
      <span
        style={{
          width: 32,
          height: 32,
          flex: "0 0 32px",
          display: "grid",
          placeItems: "center",
          borderRadius: 8,
          color,
          background: `${color}16`,
        }}
      >
        {icon}
      </span>
      <span style={{ minWidth: 0 }}>
        <span
          className="mono"
          style={{
            display: "block",
            color: colors.secondary,
            fontSize: 11,
            letterSpacing: "0.08em",
            textTransform: "uppercase",
          }}
        >
          {label}
        </span>
        <span
          style={{
            display: "block",
            color: colors.text,
            fontSize: 13,
            whiteSpace: "nowrap",
            overflow: "hidden",
            textOverflow: "ellipsis",
          }}
          title={value}
        >
          {value}
        </span>
      </span>
    </div>
  );
}

function TopologyNode({
  icon,
  title,
  caption,
  color,
}: {
  icon: ReactNode;
  title: string;
  caption: string;
  color: string;
}) {
  return (
    <div
      style={{
        flex: "1 1 145px",
        minWidth: 0,
        padding: 14,
        border: "1px solid rgba(0, 212, 255, 0.14)",
        borderRadius: 8,
        background: colors.panelStrong,
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 10,
          marginBottom: 14,
        }}
      >
        <span style={{ color, fontSize: 20 }}>{icon}</span>
        <span
          aria-hidden="true"
          style={{
            flex: 1,
            height: 1,
            background: `linear-gradient(90deg, ${color}66, transparent)`,
          }}
        />
      </div>
      <div
        style={{
          color: colors.text,
          fontSize: 14,
          fontWeight: 600,
          lineHeight: 1.35,
        }}
      >
        {title}
      </div>
      <div
        className="mono"
        style={{
          marginTop: 7,
          color: colors.tertiary,
          fontSize: 12,
          lineHeight: 1.45,
          overflowWrap: "anywhere",
        }}
      >
        {caption}
      </div>
    </div>
  );
}

function TopologyPanel() {
  return (
    <div className="glow-box" style={styles.topologyPanel}>
      <div style={styles.topologyHeader}>
        <span className="tech-title" style={{ fontSize: 13 }}>
          REQUEST · CONTROL PATH
        </span>
        <span
          className="mono"
          style={{
            color: colors.green,
            fontSize: 12,
            padding: "4px 8px",
            borderRadius: 6,
            background: "rgba(34, 211, 161, 0.08)",
          }}
        >
          guarded
        </span>
      </div>
      <div style={styles.topologyNodes}>
        <TopologyNode
          icon={<KeyOutlined />}
          title="Client API"
          caption="Bearer sk-..."
          color={colors.yellow}
        />
        <TopologyNode
          icon={<ApiOutlined />}
          title="Relay Gateway"
          caption="/v1/responses"
          color={colors.cyan}
        />
        <TopologyNode
          icon={<CloudServerOutlined />}
          title="Upstream Pool"
          caption="LRU account routing"
          color={colors.purple}
        />
      </div>
    </div>
  );
}

export default function LoginPage() {
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);
  const { message } = App.useApp();
  const isMobile = useIsMobile();

  async function onFinish(v: { username: string; password: string }) {
    try {
      setLoading(true);
      const { token, username } = await login(v.username, v.password);
      setAuth(token, username);
      navigate("/dashboard", { replace: true });
    } catch (e) {
      message.error(extractError(e, "登录失败"));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div
      style={{
        ...styles.shell,
        alignItems: isMobile ? "flex-start" : styles.shell.alignItems,
        padding: isMobile ? 12 : styles.shell.padding,
      }}
    >
      <main
        style={{
          ...styles.stage,
          flexDirection: isMobile ? "column-reverse" : undefined,
          flexWrap: isMobile ? "nowrap" : styles.stage.flexWrap,
          gap: isMobile ? 16 : styles.stage.gap,
        }}
      >
        <section
          style={{
            ...styles.brandPane,
            flex: isMobile ? "0 0 auto" : styles.brandPane.flex,
            gap: isMobile ? 14 : styles.brandPane.gap,
          }}
          aria-label="Image Relay 控制台概览"
        >
          <div
            className="tech-title"
            style={styles.eyebrow}
          >
            OPENAI IMAGE2 · RELAY CONTROL
          </div>
          <h1
            style={{
              ...styles.title,
              fontSize: isMobile ? 34 : styles.title.fontSize,
              letterSpacing: isMobile ? "0.03em" : styles.title.letterSpacing,
            }}
          >
            IMAGE · RELAY
          </h1>
          <p
            style={{
              ...styles.subtitle,
              fontSize: isMobile ? 14 : styles.subtitle.fontSize,
              lineHeight: isMobile ? 1.7 : styles.subtitle.lineHeight,
            }}
          >
            面向管理员的图片生成中转控制台，集中管理上游账号、调用方
            API Key、请求日志与模型映射。
          </p>
          <div className="tech-divider" style={{ maxWidth: 420 }} />
          <div style={styles.statusGrid}>
            <StatusPill
              icon={<SafetyCertificateOutlined />}
              label="auth"
              value="JWT console guard"
              color={colors.green}
            />
            <StatusPill
              icon={<FileSearchOutlined />}
              label="audit"
              value="request logs"
              color={colors.cyan}
            />
            <StatusPill
              icon={<DashboardOutlined />}
              label="ops"
              value="health dashboard"
              color={colors.purple}
            />
          </div>
          {!isMobile && <TopologyPanel />}
        </section>

        <aside
          style={{
            ...styles.loginColumn,
            flex: isMobile ? "0 0 auto" : styles.loginColumn.flex,
            width: "100%",
          }}
          aria-label="管理员登录"
        >
          <Card
            className="glow-box login-card"
            style={styles.loginCard}
            styles={{ body: { padding: isMobile ? 20 : 30 } }}
            variant="borderless"
          >
            <div style={styles.cardHeader}>
              <div style={styles.accessBadge}>
                <LockOutlined />
                secure access
              </div>
              <h2 style={styles.loginTitle}>管理员登录</h2>
              <p style={styles.loginHint}>
                使用后台管理员账号进入控制台，管理图片生成中转服务。
              </p>
              <div className="tech-divider" style={{ marginTop: 18 }} />
            </div>

            <Form
              layout="vertical"
              onFinish={onFinish}
              autoComplete="off"
              requiredMark={false}
            >
              <Form.Item
                name="username"
                label="用户名"
                rules={[{ required: true, message: "请输入用户名" }]}
              >
                <Input
                  size="large"
                  prefix={<UserOutlined />}
                  placeholder="admin"
                  autoFocus
                  autoComplete="username"
                />
              </Form.Item>
              <Form.Item
                name="password"
                label="密码"
                rules={[{ required: true, message: "请输入密码" }]}
              >
                <Input.Password
                  size="large"
                  prefix={<LockOutlined />}
                  placeholder="••••••••"
                  autoComplete="current-password"
                />
              </Form.Item>
              <Form.Item style={{ marginTop: 26, marginBottom: 0 }}>
                <Button
                  block
                  type="primary"
                  htmlType="submit"
                  size="large"
                  icon={<LoginOutlined />}
                  loading={loading}
                  style={{
                    height: 46,
                    fontWeight: 600,
                    letterSpacing: "0.04em",
                  }}
                >
                  进入控制台
                </Button>
              </Form.Item>
            </Form>

            <div style={styles.footnote}>
              <SafetyCertificateOutlined
                style={{ color: colors.cyan, marginTop: 3 }}
              />
              <span>
                管理员登录态仅用于后台控制台；调用方 API Key
                仍通过独立鉴权访问转发接口。
              </span>
            </div>
          </Card>
        </aside>
      </main>
    </div>
  );
}
