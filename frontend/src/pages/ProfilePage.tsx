import { Card, Form, Input, Button, App } from "antd";
import { useMutation } from "@tanstack/react-query";
import { changePassword } from "@/api/auth";
import { extractError } from "@/api/client";

export default function ProfilePage() {
  const { message } = App.useApp();
  const [form] = Form.useForm();

  const mut = useMutation({
    mutationFn: (v: { oldPassword: string; newPassword: string }) =>
      changePassword(v.oldPassword, v.newPassword),
    onSuccess: () => {
      message.success("密码已更新");
      form.resetFields();
    },
    onError: (e) => message.error(extractError(e, "修改失败")),
  });

  return (
    <Card
      className="glow-box"
      bordered={false}
      title={
        <span className="tech-title" style={{ fontSize: 13 }}>
          PROFILE · CHANGE PASSWORD
        </span>
      }
      style={{ maxWidth: 480 }}
    >
      <Form
        form={form}
        layout="vertical"
        onFinish={(v) => {
          if (v.newPassword !== v.confirmPassword) {
            message.error("两次输入的新密码不一致");
            return;
          }
          mut.mutate({
            oldPassword: v.oldPassword,
            newPassword: v.newPassword,
          });
        }}
      >
        <Form.Item
          name="oldPassword"
          label="当前密码"
          rules={[{ required: true }]}
        >
          <Input.Password autoComplete="current-password" />
        </Form.Item>
        <Form.Item
          name="newPassword"
          label="新密码"
          rules={[{ required: true, min: 6 }]}
        >
          <Input.Password autoComplete="new-password" />
        </Form.Item>
        <Form.Item
          name="confirmPassword"
          label="确认新密码"
          rules={[{ required: true, min: 6 }]}
        >
          <Input.Password autoComplete="new-password" />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={mut.isPending}>
          保存
        </Button>
      </Form>
    </Card>
  );
}
