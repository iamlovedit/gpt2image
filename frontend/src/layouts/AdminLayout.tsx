import { Layout, Menu, Dropdown, Avatar, Space, Drawer, Button } from "antd";
import {
  DashboardOutlined,
  CloudServerOutlined,
  KeyOutlined,
  BranchesOutlined,
  FileTextOutlined,
  SettingOutlined,
  UserOutlined,
  LogoutOutlined,
  MenuOutlined,
} from "@ant-design/icons";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuthStore } from "@/stores/authStore";
import type { CSSProperties } from "react";
import { useState } from "react";
import { useIsMobile } from "@/hooks/useIsMobile";

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
  const isMobile = useIsMobile();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const onLogout = () => {
    clear();
    navigate("/login", { replace: true });
  };

  const active =
    menuItems.find((m) => loc.pathname.startsWith(m.key))?.key ?? "/dashboard";

  const navigateMenu = (key: string) => {
    navigate(key);
    setMobileMenuOpen(false);
  };

  const navMenu = (
    <Menu
      theme="dark"
      mode="inline"
      selectedKeys={[active]}
      items={menuItems}
      onClick={(e) => navigateMenu(e.key)}
      style={{ background: "transparent", borderRight: "none" }}
    />
  );

  return (
    <Layout style={{ minHeight: "100vh" }}>
      {!isMobile && (
        <Sider
          width={220}
          theme="dark"
          style={{ borderRight: "1px solid rgba(0,212,255,0.1)" }}
        >
          <div style={{ padding: "20px 24px", ...brandStyle }}>IMAGE · RELAY</div>
          <div className="tech-divider" style={{ margin: "4px 18px 12px" }} />
          {navMenu}
        </Sider>
      )}
      <Drawer
        className="mobile-nav-drawer"
        title={<span style={brandStyle}>IMAGE · RELAY</span>}
        placement="left"
        open={mobileMenuOpen}
        onClose={() => setMobileMenuOpen(false)}
        width={280}
        styles={{
          header: {
            background: "#0A0F1D",
            borderBottom: "1px solid rgba(0, 212, 255, 0.16)",
          },
          body: { padding: 0, background: "#0A0F1D" },
          content: { background: "#0A0F1D" },
        }}
      >
        <div className="tech-divider" style={{ margin: "0 18px 12px" }} />
        {navMenu}
      </Drawer>
      <Layout style={{ minWidth: 0 }}>
        <Header
          style={{
            ...headerStyle,
            padding: isMobile ? "0 12px" : headerStyle.padding,
            gap: 12,
          }}
        >
          <Space align="center" size={isMobile ? 8 : 12} style={{ minWidth: 0 }}>
            {isMobile && (
              <Button
                type="text"
                aria-label="打开导航菜单"
                icon={<MenuOutlined />}
                onClick={() => setMobileMenuOpen(true)}
                style={{ color: "#00D4FF", flex: "0 0 auto" }}
              />
            )}
            <span
              className="tech-title"
              style={{
                fontSize: 12,
                minWidth: 0,
                whiteSpace: "nowrap",
                overflow: "hidden",
                textOverflow: "ellipsis",
              }}
            >
              {isMobile ? "IMAGE RELAY" : "OPENAI IMAGE2 · RELAY CONTROL"}
            </span>
          </Space>
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
            <Space
              style={{
                cursor: "pointer",
                color: "#E6ECF5",
                minWidth: 0,
                flex: "0 0 auto",
              }}
            >
              <Avatar
                size="small"
                style={{ background: "#00D4FF", color: "#0A0F1D" }}
              >
                {username?.[0]?.toUpperCase() ?? "A"}
              </Avatar>
              {!isMobile && <span>{username ?? "管理员"}</span>}
            </Space>
          </Dropdown>
        </Header>
        <Content style={{ padding: isMobile ? 12 : 24, minWidth: 0 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
