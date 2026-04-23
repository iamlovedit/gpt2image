import { Layout, Menu, Dropdown, Avatar, Space } from "antd";
import {
  DashboardOutlined,
  CloudServerOutlined,
  KeyOutlined,
  BranchesOutlined,
  FileTextOutlined,
  SettingOutlined,
  UserOutlined,
  LogoutOutlined,
} from "@ant-design/icons";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuthStore } from "../stores/authStore";
import type { CSSProperties } from "react";

const { Sider, Header, Content } = Layout;

const menuItems = [
  { key: "/dashboard", icon: <DashboardOutlined />, label: "Dashboard" },
  { key: "/accounts", icon: <CloudServerOutlined />, label: "上游账号" },
  { key: "/keys", icon: <KeyOutlined />, label: "API Key" },
  { key: "/logs", icon: <FileTextOutlined />, label: "请求日志" },
  { key: "/models", icon: <BranchesOutlined />, label: "模型映射" },
  { key: "/settings", icon: <SettingOutlined />, label: "系统设置" },
  { key: "/profile", icon: <UserOutlined />, label: "个人中心" },
];

const headerStyle: CSSProperties = {
  background: "#0A0F1D",
  padding: "0 24px",
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  borderBottom: "1px solid rgba(0, 212, 255, 0.2)",
  boxShadow: "0 1px 24px rgba(0, 212, 255, 0.08)",
};

const brandStyle: CSSProperties = {
  color: "#00D4FF",
  fontFamily: '"JetBrains Mono", ui-monospace, monospace',
  letterSpacing: "0.2em",
  fontSize: 16,
  fontWeight: 600,
  textShadow: "0 0 12px rgba(0, 212, 255, 0.4)",
};

export default function AdminLayout() {
  const navigate = useNavigate();
  const loc = useLocation();
  const username = useAuthStore((s) => s.username);
  const clear = useAuthStore((s) => s.clear);

  const onLogout = () => {
    clear();
    navigate("/login", { replace: true });
  };

  const active =
    menuItems.find((m) => loc.pathname.startsWith(m.key))?.key ?? "/dashboard";

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Sider
        width={220}
        theme="dark"
        style={{ borderRight: "1px solid rgba(0,212,255,0.1)" }}
      >
        <div style={{ padding: "20px 24px", ...brandStyle }}>IMAGE · RELAY</div>
        <div className="tech-divider" style={{ margin: "4px 18px 12px" }} />
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[active]}
          items={menuItems}
          onClick={(e) => navigate(e.key)}
          style={{ background: "transparent", borderRight: "none" }}
        />
      </Sider>
      <Layout>
        <Header style={headerStyle}>
          <span className="tech-title" style={{ fontSize: 12 }}>
            OPENAI IMAGE2 · RELAY CONTROL
          </span>
          <Dropdown
            menu={{
              items: [
                {
                  key: "profile",
                  icon: <UserOutlined />,
                  label: "个人中心",
                  onClick: () => navigate("/profile"),
                },
                { type: "divider" },
                {
                  key: "logout",
                  icon: <LogoutOutlined />,
                  label: "退出登录",
                  onClick: onLogout,
                },
              ],
            }}
          >
            <Space style={{ cursor: "pointer", color: "#E6ECF5" }}>
              <Avatar
                size="small"
                style={{ background: "#00D4FF", color: "#0A0F1D" }}
              >
                {username?.[0]?.toUpperCase() ?? "A"}
              </Avatar>
              <span>{username ?? "管理员"}</span>
            </Space>
          </Dropdown>
        </Header>
        <Content style={{ padding: 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
