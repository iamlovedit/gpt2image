import { useState } from "react";
import {
  Card,
  Table,
  Space,
  Button,
  Modal,
  Form,
  Input,
  InputNumber,
  DatePicker,
  App,
  Popconfirm,
  Drawer,
  Tag,
  Typography,
} from "antd";
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  CopyOutlined,
} from "@ant-design/icons";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import dayjs from "dayjs";
import {
  ClientKey,
  ClientKeyStatus,
  createKey,
  deleteKey,
  listKeys,
  updateKey,
} from "@/api/keys";
import { extractError } from "@/api/client";

export default function KeysPage() {
  const qc = useQueryClient();
  const { message } = App.useApp();
  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<ClientKey | null>(null);
  const [plaintext, setPlaintext] = useState<string | null>(null);

  const { data = [], isLoading } = useQuery({
    queryKey: ["keys"],
    queryFn: listKeys,
  });

  const delMut = useMutation({
    mutationFn: deleteKey,
    onSuccess: () => {
      message.success("已删除");
      qc.invalidateQueries({ queryKey: ["keys"] });
    },
    onError: (e) => message.error(extractError(e, "删除失败")),
  });

  const toggleMut = useMutation({
    mutationFn: ({ id, status }: { id: string; status: ClientKeyStatus }) =>
      updateKey(id, { status }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["keys"] }),
    onError: (e) => message.error(extractError(e, "更新失败")),
  });

  return (
    <Card
      className="glow-box"
      bordered={false}
      title={
        <span className="tech-title" style={{ fontSize: 13 }}>
          CLIENT · API KEYS
        </span>
      }
      extra={
        <Button
          type="primary"
          icon={<PlusOutlined />}
          onClick={() => setCreateOpen(true)}
        >
          创建 Key
        </Button>
      }
    >
      <Table<ClientKey>
        rowKey="id"
        size="small"
        loading={isLoading}
        dataSource={data}
        pagination={false}
        columns={[
          { title: "名称", dataIndex: "name", width: 160 },
          {
            title: "Key 前缀",
            dataIndex: "keyPrefix",
            width: 140,
            render: (v) => <span className="mono">{v}…</span>,
          },
          {
            title: "状态",
            dataIndex: "status",
            width: 100,
            render: (s: ClientKeyStatus) =>
              s === ClientKeyStatus.Active ? (
                <Tag color="cyan">启用</Tag>
              ) : (
                <Tag>已禁用</Tag>
              ),
          },
          { title: "RPM", dataIndex: "rpmLimit", width: 80 },
          { title: "并发", dataIndex: "concurrencyLimit", width: 80 },
          {
            title: "过期时间",
            dataIndex: "expiresAt",
            width: 160,
            render: (v) =>
              v ? (
                <span className="mono">
                  {dayjs(v).format("YYYY-MM-DD HH:mm")}
                </span>
              ) : (
                "永不过期"
              ),
          },
          {
            title: "创建时间",
            dataIndex: "createdAt",
            width: 160,
            render: (v) => (
              <span className="mono">
                {dayjs(v).format("YYYY-MM-DD HH:mm")}
              </span>
            ),
          },
          {
            title: "备注",
            dataIndex: "notes",
            ellipsis: true,
            render: (v) => v || "—",
          },
          {
            title: "操作",
            fixed: "right",
            width: 220,
            render: (_, row) => (
              <Space size={4}>
                <Button
                  size="small"
                  icon={<EditOutlined />}
                  onClick={() => setEditing(row)}
                >
                  编辑
                </Button>
                {row.status === ClientKeyStatus.Active ? (
                  <Button
                    size="small"
                    danger
                    onClick={() =>
                      toggleMut.mutate({
                        id: row.id,
                        status: ClientKeyStatus.Disabled,
                      })
                    }
                  >
                    禁用
                  </Button>
                ) : (
                  <Button
                    size="small"
                    onClick={() =>
                      toggleMut.mutate({
                        id: row.id,
                        status: ClientKeyStatus.Active,
                      })
                    }
                  >
                    启用
                  </Button>
                )}
                <Popconfirm
                  title="确认删除该 Key？删除后持有者立即失效。"
                  onConfirm={() => delMut.mutate(row.id)}
                >
                  <Button
                    size="small"
                    danger
                    type="text"
                    icon={<DeleteOutlined />}
                  />
                </Popconfirm>
              </Space>
            ),
          },
        ]}
        scroll={{ x: 1200 }}
      />

      <CreateModal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={(txt) => {
          setCreateOpen(false);
          setPlaintext(txt);
          qc.invalidateQueries({ queryKey: ["keys"] });
        }}
      />
      <EditDrawer
        keyItem={editing}
        onClose={() => setEditing(null)}
        onDone={() => {
          setEditing(null);
          qc.invalidateQueries({ queryKey: ["keys"] });
        }}
      />
      <PlaintextModal
        plaintext={plaintext}
        onClose={() => setPlaintext(null)}
      />
    </Card>
  );
}

function CreateModal({
  open,
  onClose,
  onCreated,
}: {
  open: boolean;
  onClose: () => void;
  onCreated: (plaintext: string) => void;
}) {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const mut = useMutation({
    mutationFn: async (v: any) =>
      createKey({
        name: v.name,
        expiresAt: v.expiresAt ? v.expiresAt.toISOString() : undefined,
        rpmLimit: v.rpmLimit,
        concurrencyLimit: v.concurrencyLimit,
        notes: v.notes,
      }),
    onSuccess: (r) => {
      form.resetFields();
      onCreated(r.plaintext);
    },
    onError: (e) => message.error(extractError(e, "创建失败")),
  });

  return (
    <Modal
      title="创建 API Key"
      open={open}
      onCancel={onClose}
      onOk={() => form.submit()}
      confirmLoading={mut.isPending}
      destroyOnHidden
      okText="创建"
      cancelText="取消"
    >
      <Form
        form={form}
        layout="vertical"
        initialValues={{ rpmLimit: 60, concurrencyLimit: 4 }}
        onFinish={(v) => mut.mutate(v)}
      >
        <Form.Item
          name="name"
          label="名称"
          rules={[{ required: true, message: "请输入名称" }]}
        >
          <Input placeholder="例如：demo-app" />
        </Form.Item>
        <Space style={{ width: "100%" }} size={16}>
          <Form.Item name="rpmLimit" label="RPM 限额" style={{ flex: 1 }}>
            <InputNumber min={0} style={{ width: "100%" }} />
          </Form.Item>
          <Form.Item
            name="concurrencyLimit"
            label="并发上限"
            style={{ flex: 1 }}
          >
            <InputNumber min={0} style={{ width: "100%" }} />
          </Form.Item>
        </Space>
        <Form.Item name="expiresAt" label="过期时间 (可选)">
          <DatePicker showTime style={{ width: "100%" }} />
        </Form.Item>
        <Form.Item name="notes" label="备注 (可选)">
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Modal>
  );
}

function EditDrawer({
  keyItem,
  onClose,
  onDone,
}: {
  keyItem: ClientKey | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const { message } = App.useApp();
  const [form] = Form.useForm();

  const mut = useMutation({
    mutationFn: async (v: any) =>
      updateKey(keyItem!.id, {
        name: v.name,
        rpmLimit: v.rpmLimit,
        concurrencyLimit: v.concurrencyLimit,
        expiresAt: v.expiresAt ? v.expiresAt.toISOString() : null,
        notes: v.notes,
      }),
    onSuccess: () => {
      message.success("已保存");
      onDone();
    },
    onError: (e) => message.error(extractError(e, "保存失败")),
  });

  return (
    <Drawer
      title="编辑 Key"
      open={!!keyItem}
      onClose={onClose}
      width={360}
      destroyOnClose
      extra={
        <Button
          type="primary"
          onClick={() => form.submit()}
          loading={mut.isPending}
        >
          保存
        </Button>
      }
    >
      {keyItem && (
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            name: keyItem.name,
            rpmLimit: keyItem.rpmLimit,
            concurrencyLimit: keyItem.concurrencyLimit,
            expiresAt: keyItem.expiresAt ? dayjs(keyItem.expiresAt) : null,
            notes: keyItem.notes ?? "",
          }}
          onFinish={(v) => mut.mutate(v)}
        >
          <Form.Item name="name" label="名称" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="rpmLimit" label="RPM 限额">
            <InputNumber min={0} style={{ width: "100%" }} />
          </Form.Item>
          <Form.Item name="concurrencyLimit" label="并发上限">
            <InputNumber min={0} style={{ width: "100%" }} />
          </Form.Item>
          <Form.Item name="expiresAt" label="过期时间">
            <DatePicker showTime style={{ width: "100%" }} />
          </Form.Item>
          <Form.Item name="notes" label="备注">
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      )}
    </Drawer>
  );
}

function PlaintextModal({
  plaintext,
  onClose,
}: {
  plaintext: string | null;
  onClose: () => void;
}) {
  const { message } = App.useApp();

  const copy = async () => {
    if (!plaintext) return;
    await navigator.clipboard.writeText(plaintext);
    message.success("已复制到剪贴板");
  };

  return (
    <Modal
      title="Key 创建成功"
      open={!!plaintext}
      onCancel={onClose}
      onOk={onClose}
      okText="我已保存"
      cancelButtonProps={{ style: { display: "none" } }}
      maskClosable={false}
      closable={false}
      width={560}
    >
      <Typography.Paragraph type="warning">
        这是该 Key 唯一一次可见明文，关闭后无法再次查看，请立刻保存。
      </Typography.Paragraph>
      <div className="glow-box" style={{ padding: 16, wordBreak: "break-all" }}>
        <div className="mono" style={{ fontSize: 16, color: "#00D4FF" }}>
          {plaintext}
        </div>
      </div>
      <div style={{ marginTop: 12, textAlign: "right" }}>
        <Button icon={<CopyOutlined />} onClick={copy}>
          复制
        </Button>
      </div>
    </Modal>
  );
}
