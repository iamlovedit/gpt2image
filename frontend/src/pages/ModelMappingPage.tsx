import { useState } from "react";
import {
  Alert,
  App,
  Button,
  Card,
  Drawer,
  Form,
  Input,
  Modal,
  Popconfirm,
  Space,
  Table,
  Tag,
} from "antd";
import { DeleteOutlined, EditOutlined, PlusOutlined } from "@ant-design/icons";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import dayjs from "dayjs";
import {
  createModel,
  deleteModel,
  listModels,
  type ModelMapping,
  updateModel,
} from "@/api/models";
import { extractError } from "@/api/client";

export default function ModelMappingPage() {
  const qc = useQueryClient();
  const { message } = App.useApp();
  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<ModelMapping | null>(null);

  const { data = [], isLoading } = useQuery({
    queryKey: ["models"],
    queryFn: listModels,
  });

  const deleteMut = useMutation({
    mutationFn: deleteModel,
    onSuccess: () => {
      message.success("已删除");
      qc.invalidateQueries({ queryKey: ["models"] });
    },
    onError: (e) => message.error(extractError(e, "删除失败")),
  });

  const toggleMut = useMutation({
    mutationFn: ({ id, isEnabled }: { id: string; isEnabled: boolean }) =>
      updateModel(id, { isEnabled }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["models"] }),
    onError: (e) => message.error(extractError(e, "更新失败")),
  });

  return (
    <Card
      className="glow-box"
      variant="borderless"
      title={
        <span className="tech-title" style={{ fontSize: 13 }}>
          MODEL · MAPPING
        </span>
      }
      extra={
        <Button
          type="primary"
          icon={<PlusOutlined />}
          onClick={() => setCreateOpen(true)}
        >
          新增映射
        </Button>
      }
    >
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16, background: "rgba(0, 212, 255, 0.06)" }}
        message="只启用的模型映射会进入代理白名单；请求会从对外模型名改写为上游模型名后转发。"
      />
      <Table<ModelMapping>
        rowKey="id"
        size="small"
        loading={isLoading}
        dataSource={data}
        pagination={false}
        columns={[
          {
            title: "对外模型",
            dataIndex: "externalName",
            render: (v) => <span className="mono">{v}</span>,
          },
          {
            title: "上游模型",
            dataIndex: "upstreamName",
            render: (v) => <span className="mono">{v}</span>,
          },
          {
            title: "状态",
            dataIndex: "isEnabled",
            width: 100,
            render: (v: boolean) =>
              v ? <Tag color="cyan">启用</Tag> : <Tag>禁用</Tag>,
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
            title: "更新时间",
            dataIndex: "updatedAt",
            width: 160,
            render: (v) => (
              <span className="mono">
                {dayjs(v).format("YYYY-MM-DD HH:mm")}
              </span>
            ),
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
                {row.isEnabled ? (
                  <Button
                    size="small"
                    danger
                    onClick={() =>
                      toggleMut.mutate({ id: row.id, isEnabled: false })
                    }
                  >
                    禁用
                  </Button>
                ) : (
                  <Button
                    size="small"
                    onClick={() =>
                      toggleMut.mutate({ id: row.id, isEnabled: true })
                    }
                  >
                    启用
                  </Button>
                )}
                <Popconfirm
                  title="确认删除该模型映射？"
                  onConfirm={() => deleteMut.mutate(row.id)}
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
        scroll={{ x: 1000 }}
      />

      <CreateModal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onDone={() => {
          setCreateOpen(false);
          qc.invalidateQueries({ queryKey: ["models"] });
        }}
      />
      <EditDrawer
        model={editing}
        onClose={() => setEditing(null)}
        onDone={() => {
          setEditing(null);
          qc.invalidateQueries({ queryKey: ["models"] });
        }}
      />
    </Card>
  );
}

function CreateModal({
  open,
  onClose,
  onDone,
}: {
  open: boolean;
  onClose: () => void;
  onDone: () => void;
}) {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const mut = useMutation({
    mutationFn: async (v: any) =>
      createModel({
        externalName: v.externalName,
        upstreamName: v.upstreamName,
        isEnabled: v.isEnabled,
      }),
    onSuccess: () => {
      message.success("已创建");
      form.resetFields();
      onDone();
    },
    onError: (e) => message.error(extractError(e, "创建失败")),
  });

  return (
    <Modal
      title="新增模型映射"
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
        initialValues={{ isEnabled: true }}
        onFinish={(v) => mut.mutate(v)}
      >
        <Form.Item
          name="externalName"
          label="对外模型"
          rules={[
            { required: true, message: "请输入对外模型名" },
            { max: 128 },
          ]}
        >
          <Input placeholder="例如：gpt-5.4" />
        </Form.Item>
        <Form.Item
          name="upstreamName"
          label="上游模型"
          rules={[
            { required: true, message: "请输入上游模型名" },
            { max: 128 },
          ]}
        >
          <Input placeholder="例如：gpt-5.4" />
        </Form.Item>
      </Form>
    </Modal>
  );
}

function EditDrawer({
  model,
  onClose,
  onDone,
}: {
  model: ModelMapping | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const mut = useMutation({
    mutationFn: async (v: any) =>
      updateModel(model!.id, {
        externalName: v.externalName,
        upstreamName: v.upstreamName,
      }),
    onSuccess: () => {
      message.success("已保存");
      onDone();
    },
    onError: (e) => message.error(extractError(e, "保存失败")),
  });

  return (
    <Drawer
      title="编辑模型映射"
      open={!!model}
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
      {model && (
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            externalName: model.externalName,
            upstreamName: model.upstreamName,
          }}
          onFinish={(v) => mut.mutate(v)}
        >
          <Form.Item
            name="externalName"
            label="对外模型"
            rules={[
              { required: true, message: "请输入对外模型名" },
              { max: 128 },
            ]}
          >
            <Input />
          </Form.Item>
          <Form.Item
            name="upstreamName"
            label="上游模型"
            rules={[
              { required: true, message: "请输入上游模型名" },
              { max: 128 },
            ]}
          >
            <Input />
          </Form.Item>
        </Form>
      )}
    </Drawer>
  );
}
