import { useState } from "react";
import { Card, Form, Input, Button, App } from "antd";
import { LockOutlined, UserOutlined } from "@ant-design/icons";
import { useNavigate } from "react-router-dom";
import { useAuthStore } from "../stores/authStore";
import { login } from "../api/auth";
import { extractError } from "../api/client";

export default function LoginPage() {
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);
  const { message } = App.useApp();

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
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: 24,
      }}
    >
      <Card
        className="glow-box"
        style={{
          width: 400,
          padding: 8,
          background: "rgba(16, 23, 42, 0.85)",
          backdropFilter: "blur(8px)",
        }}
        variant={"borderless"}
      >
        <div style={{ textAlign: "center", marginBottom: 28 }}>
          <div
            className="tech-title"
            style={{ fontSize: 22, letterSpacing: "0.22em" }}
          >
            IMAGE · RELAY
          </div>
          <div
            style={{
              color: "#8A9ABF",
              marginTop: 6,
              fontSize: 12,
              letterSpacing: "0.1em",
            }}
          >
            OPENAI IMAGE2 CONTROL CONSOLE
          </div>
          <div className="tech-divider" style={{ marginTop: 14 }} />
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
            />
          </Form.Item>
          <Form.Item style={{ marginTop: 24, marginBottom: 0 }}>
            <Button
              block
              type="primary"
              htmlType="submit"
              size="large"
              loading={loading}
            >
              进入控制台
            </Button>
          </Form.Item>
        </Form>
      </Card>
    </div>
  );
}
