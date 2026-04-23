import { useEffect } from 'react';
import { Alert, App, Button, Card, Form, Input, Space } from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getUpstreamHeaderSettings,
  updateUpstreamHeaderSettings,
  type UpstreamHeaderSettings,
} from '../api/settings';
import { extractError } from '../api/client';

export default function SettingsPage() {
  const [form] = Form.useForm<UpstreamHeaderSettings>();
  const { message } = App.useApp();
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['upstream-header-settings'],
    queryFn: getUpstreamHeaderSettings,
  });

  useEffect(() => {
    if (data) form.setFieldsValue(data);
  }, [data, form]);

  const mut = useMutation({
    mutationFn: async (values: UpstreamHeaderSettings) => updateUpstreamHeaderSettings({
      userAgent: values.userAgent.trim(),
      version: values.version.trim(),
      originator: values.originator.trim(),
      sessionId: values.sessionId?.trim() || null,
    }),
    onSuccess: (next) => {
      message.success('设置已保存');
      qc.setQueryData(['upstream-header-settings'], next);
      form.setFieldsValue(next);
    },
    onError: (e) => message.error(extractError(e, '保存失败')),
  });

  return (
    <Card
      className="glow-box"
      bordered={false}
      title={<span className="tech-title" style={{ fontSize: 13 }}>SYSTEM · SETTINGS</span>}
      extra={
        <Button type="primary" onClick={() => form.submit()} loading={mut.isPending}>
          保存
        </Button>
      }
      loading={isLoading}
    >
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16, background: 'rgba(0, 212, 255, 0.06)' }}
        message="这些值会在转发到上游 `chatgpt.com` 时作为统一请求头发送。`session_id` 可留空。"
      />
      <Form
        form={form}
        layout="vertical"
        onFinish={(values) => mut.mutate(values)}
        requiredMark={false}
      >
        <Space direction="vertical" size={8} style={{ width: '100%' }}>
          <Form.Item
            label="user-agent"
            name="userAgent"
            rules={[{ required: true, message: '请输入 user-agent' }]}
          >
            <Input className="mono" placeholder="例如：ImageRelay/1.0" />
          </Form.Item>
          <Form.Item
            label="version"
            name="version"
            rules={[{ required: true, message: '请输入 version' }]}
          >
            <Input className="mono" placeholder="例如：1.0.0" />
          </Form.Item>
          <Form.Item
            label="originator"
            name="originator"
            rules={[{ required: true, message: '请输入 originator' }]}
          >
            <Input className="mono" placeholder="例如：image-relay" />
          </Form.Item>
          <Form.Item label="session_id" name="sessionId">
            <Input className="mono" placeholder="可选，不填则不发送该 header" />
          </Form.Item>
        </Space>
      </Form>
    </Card>
  );
}
