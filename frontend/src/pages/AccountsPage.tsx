import { useState } from "react";
import {
  Card,
  Table,
  Space,
  Button,
  Input,
  Select,
  Tag,
  Modal,
  Form,
  InputNumber,
  Drawer,
  Popconfirm,
  App,
} from "antd";
import {
  ReloadOutlined,
  ImportOutlined,
  EditOutlined,
  DeleteOutlined,
  SyncOutlined,
} from "@ant-design/icons";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import dayjs from "dayjs";
import {
  Account,
  AccountStatusClass,
  AccountStatusLabel,
  ImportItem,
  ImportStrategy,
  UpstreamAccountStatus,
  deleteAccount,
  importAccounts,
  listAccounts,
  refreshAccount,
  testAccount,
  updateAccount,
} from "@/api/accounts";
import { extractError } from "@/api/client";

const statusOptions = [
  {
    value: UpstreamAccountStatus.Healthy,
    label: AccountStatusLabel[UpstreamAccountStatus.Healthy],
  },
  {
    value: UpstreamAccountStatus.Cooling,
    label: AccountStatusLabel[UpstreamAccountStatus.Cooling],
  },
  {
    value: UpstreamAccountStatus.RateLimited,
    label: AccountStatusLabel[UpstreamAccountStatus.RateLimited],
  },
  {
    value: UpstreamAccountStatus.Banned,
    label: AccountStatusLabel[UpstreamAccountStatus.Banned],
  },
  {
    value: UpstreamAccountStatus.Invalid,
    label: AccountStatusLabel[UpstreamAccountStatus.Invalid],
  },
  {
    value: UpstreamAccountStatus.Disabled,
    label: AccountStatusLabel[UpstreamAccountStatus.Disabled],
  },
];

export default function AccountsPage() {
  const qc = useQueryClient();
  const { message } = App.useApp();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [status, setStatus] = useState<UpstreamAccountStatus | undefined>();
  const [keyword, setKeyword] = useState("");
  const [importOpen, setImportOpen] = useState(false);
  const [editing, setEditing] = useState<Account | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["accounts", page, pageSize, status, keyword],
    queryFn: () =>
      listAccounts({ page, pageSize, status, keyword: keyword || undefined }),
  });

  const refreshMut = useMutation({
    mutationFn: refreshAccount,
    onSuccess: () => {
      message.success("刷新成功");
      qc.invalidateQueries({ queryKey: ["accounts"] });
    },
    onError: (e) => message.error(extractError(e, "刷新失败")),
  });

  const testMut = useMutation({
    mutationFn: testAccount,
    onSuccess: (r) => {
      const status = r.httpStatus ? `HTTP ${r.httpStatus}` : "网络/刷新错误";
      const text = `${r.message} · ${status} · ${r.durationMs}ms`;
      if (r.ok) message.success(text);
      else message.error(text);
      qc.invalidateQueries({ queryKey: ["accounts"] });
    },
    onError: (e) => message.error(extractError(e, "测试失败")),
  });

  const deleteMut = useMutation({
    mutationFn: deleteAccount,
    onSuccess: () => {
      message.success("已删除");
      qc.invalidateQueries({ queryKey: ["accounts"] });
    },
    onError: (e) => message.error(extractError(e, "删除失败")),
  });

  const toggleMut = useMutation({
    mutationFn: ({
      id,
      status,
    }: {
      id: string;
      status: UpstreamAccountStatus;
    }) => updateAccount(id, { status }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["accounts"] }),
    onError: (e) => message.error(extractError(e, "更新失败")),
  });

  return (
    <Card
      className="glow-box"
      variant={"borderless"}
      title={
        <span className="tech-title" style={{ fontSize: 13 }}>
          UPSTREAM · ACCOUNTS
        </span>
      }
      extra={
        <Space>
          <Select
            allowClear
            placeholder="状态筛选"
            options={statusOptions}
            style={{ width: 140 }}
            value={status}
            onChange={(v) => {
              setStatus(v);
              setPage(1);
            }}
          />
          <Input.Search
            placeholder="备注 / 错误关键词"
            allowClear
            onSearch={(v) => {
              setKeyword(v);
              setPage(1);
            }}
            style={{ width: 240 }}
          />
          <Button
            icon={<ReloadOutlined />}
            onClick={() => qc.invalidateQueries({ queryKey: ["accounts"] })}
          >
            刷新
          </Button>
          <Button
            type="primary"
            icon={<ImportOutlined />}
            onClick={() => setImportOpen(true)}
          >
            批量导入
          </Button>
        </Space>
      }
    >
      <Table<Account>
        rowKey="id"
        size="small"
        loading={isLoading}
        dataSource={data?.items ?? []}
        pagination={{
          current: page,
          pageSize,
          total: data?.total ?? 0,
          onChange: (p, s) => {
            setPage(p);
            setPageSize(s);
          },
        }}
        columns={[
          {
            title: "状态",
            dataIndex: "status",
            width: 100,
            render: (s: UpstreamAccountStatus) => (
              <span
                className={AccountStatusClass[s]}
                style={{
                  padding: "2px 10px",
                  borderRadius: 10,
                  border: "1px solid",
                  fontSize: 12,
                  letterSpacing: "0.05em",
                }}
              >
                {AccountStatusLabel[s]}
              </span>
            ),
          },
          {
            title: "账号信息",
            width: 220,
            ellipsis: true,
            render: (_, row) => {
              const primary = row.email || row.name || row.notes || "—";
              return (
                <Space direction="vertical" size={0}>
                  <span>{primary}</span>
                  {row.proxyKey ? (
                    <span
                      className="mono"
                      style={{ color: "#8A9ABF", fontSize: 12 }}
                    >
                      {row.proxyKey}
                    </span>
                  ) : null}
                </Space>
              );
            },
          },
          {
            title: "chatgpt-account-id",
            dataIndex: "chatGptAccountId",
            width: 220,
            ellipsis: true,
            render: (v) => (v ? <span className="mono">{v}</span> : "—"),
          },
          {
            title: "过期时间",
            dataIndex: "accessTokenExpiresAt",
            width: 160,
            render: (v) =>
              v ? (
                <span className="mono">
                  {dayjs(v).format("MM-DD HH:mm:ss")}
                </span>
              ) : (
                "—"
              ),
          },
          { title: "成功", dataIndex: "successCount", width: 70 },
          { title: "失败", dataIndex: "failureCount", width: 70 },
          {
            title: "最近使用",
            dataIndex: "lastUsedAt",
            width: 140,
            render: (v) =>
              v ? (
                <span className="mono">{dayjs(v).format("MM-DD HH:mm")}</span>
              ) : (
                "—"
              ),
          },
          { title: "并发", dataIndex: "concurrencyLimit", width: 70 },
          {
            title: "最近错误",
            dataIndex: "lastError",
            ellipsis: true,
            render: (v) => v || "—",
          },
          {
            title: "操作",
            fixed: "right",
            width: 240,
            render: (_, row) => (
              <Space size={4}>
                <Button
                  size="small"
                  loading={testMut.isPending && testMut.variables === row.id}
                  onClick={() => testMut.mutate(row.id)}
                >
                  测试
                </Button>
                <Button
                  size="small"
                  icon={<SyncOutlined />}
                  loading={
                    refreshMut.isPending && refreshMut.variables === row.id
                  }
                  onClick={() => refreshMut.mutate(row.id)}
                >
                  刷新
                </Button>
                <Button
                  size="small"
                  icon={<EditOutlined />}
                  onClick={() => setEditing(row)}
                >
                  编辑
                </Button>
                {row.status === UpstreamAccountStatus.Disabled ? (
                  <Button
                    size="small"
                    onClick={() =>
                      toggleMut.mutate({
                        id: row.id,
                        status: UpstreamAccountStatus.Healthy,
                      })
                    }
                  >
                    启用
                  </Button>
                ) : (
                  <Button
                    size="small"
                    danger
                    onClick={() =>
                      toggleMut.mutate({
                        id: row.id,
                        status: UpstreamAccountStatus.Disabled,
                      })
                    }
                  >
                    禁用
                  </Button>
                )}
                <Popconfirm
                  title="确认删除？"
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
        scroll={{ x: 1320 }}
      />

      <ImportModal
        open={importOpen}
        onClose={() => setImportOpen(false)}
        onDone={() => {
          setImportOpen(false);
          qc.invalidateQueries({ queryKey: ["accounts"] });
        }}
      />
      <EditDrawer
        account={editing}
        onClose={() => setEditing(null)}
        onDone={() => {
          setEditing(null);
          qc.invalidateQueries({ queryKey: ["accounts"] });
        }}
      />
    </Card>
  );
}

function ImportModal({
  open,
  onClose,
  onDone,
}: {
  open: boolean;
  onClose: () => void;
  onDone: () => void;
}) {
  const { message } = App.useApp();
  const [text, setText] = useState("");
  const [strategy, setStrategy] = useState<ImportStrategy>(ImportStrategy.Skip);
  const mut = useMutation({
    mutationFn: async () => {
      let parsed: any;
      try {
        parsed = JSON.parse(text);
      } catch {
        throw new Error("JSON 格式错误");
      }
      const items = normalizeImportItems(parsed);
      if (items.length === 0)
        throw new Error(
          "未找到可导入账号，请检查 JSON 顶层数组或 accounts 字段",
        );
      return importAccounts(items, strategy);
    },
    onSuccess: (r) => {
      message.success(
        `导入完成：新增 ${r.inserted} · 覆盖 ${r.updated} · 跳过 ${r.skipped}`,
      );
      setText("");
      onDone();
    },
    onError: (e) => message.error(extractError(e, "导入失败")),
  });

  return (
    <Modal
      title="批量导入上游账号"
      open={open}
      onCancel={onClose}
      onOk={() => mut.mutate()}
      confirmLoading={mut.isPending}
      width={760}
      okText="导入"
      cancelText="取消"
    >
      <div style={{ marginBottom: 12, color: "#8A9ABF", fontSize: 12 }}>
        支持粘贴旧版 JSON 数组，或完整导出文件{" "}
        <code>{'{"accounts":[...]}'}</code>。 导出格式会自动读取{" "}
        <code>credentials.access_token</code>、
        <code>credentials.refresh_token</code>、备注、邮箱、代理和并发等字段。
      </div>
      <div style={{ marginBottom: 12 }}>
        <Space>
          <span>重复策略：</span>
          <Select
            value={strategy}
            onChange={setStrategy}
            style={{ width: 160 }}
            options={[
              { value: ImportStrategy.Skip, label: "跳过重复" },
              { value: ImportStrategy.Overwrite, label: "覆盖重复" },
              { value: ImportStrategy.Fail, label: "遇到重复报错" },
            ]}
          />
        </Space>
      </div>
      <Input.TextArea
        rows={16}
        className="mono"
        placeholder='{"accounts":[{"name":"...","notes":"...","credentials":{"access_token":"...","refresh_token":"...","chatgpt_account_id":"..."}}]}'
        value={text}
        onChange={(e) => setText(e.target.value)}
      />
    </Modal>
  );
}

function normalizeImportItems(parsed: any): ImportItem[] {
  const source = Array.isArray(parsed) ? parsed : parsed?.accounts;
  if (!Array.isArray(source))
    throw new Error("顶层必须是账号数组或包含 accounts 数组的对象");

  return source.map((it: any) => {
    const credentials = it?.credentials ?? {};
    return {
      accessToken:
        credentials.access_token ??
        credentials.accessToken ??
        it.accessToken ??
        it.access_token,
      refreshToken:
        credentials.refresh_token ??
        credentials.refreshToken ??
        it.refreshToken ??
        it.refresh_token,
      chatGptAccountId:
        credentials.chatgpt_account_id ??
        credentials.chatGptAccountId ??
        credentials.chatgptAccountId ??
        it.chatGptAccountId ??
        it.chatgptAccountId ??
        it.chatgpt_account_id,
      accessTokenExpiresAt:
        credentials.expires_at ??
        it.accessTokenExpiresAt ??
        it.access_token_expires_at ??
        it.expires_at,
      concurrencyLimit:
        it.concurrency ?? it.concurrencyLimit ?? it.concurrency_limit,
      notes: it.notes ?? credentials.email ?? it.name,
      name: it.name,
      email: credentials.email ?? it.email,
      platform: it.platform,
      accountType: it.type ?? it.accountType ?? it.account_type,
      proxyKey: it.proxy_key ?? it.proxyKey,
      priority: it.priority,
      rateMultiplier: it.rate_multiplier ?? it.rateMultiplier,
      autoPauseOnExpired: it.auto_pause_on_expired ?? it.autoPauseOnExpired,
      chatGptUserId:
        credentials.chatgpt_user_id ??
        credentials.chatGptUserId ??
        credentials.chatgptUserId,
      clientId: credentials.client_id ?? credentials.clientId,
      organizationId: credentials.organization_id ?? credentials.organizationId,
      planType: credentials.plan_type ?? credentials.planType,
      subscriptionExpiresAt:
        credentials.subscription_expires_at ??
        credentials.subscriptionExpiresAt,
      rawMetadataJson: buildRawMetadataJson(it, credentials),
    };
  });
}

function buildRawMetadataJson(item: any, credentials: any): string | undefined {
  const metadata = {
    extra: item?.extra,
    model_mapping: credentials?.model_mapping,
    _token_version: credentials?._token_version,
    id_token: credentials?.id_token,
  };
  const compact = Object.fromEntries(
    Object.entries(metadata).filter(
      ([, value]) => value !== undefined && value !== null,
    ),
  );
  return Object.keys(compact).length > 0 ? JSON.stringify(compact) : undefined;
}

function EditDrawer({
  account,
  onClose,
  onDone,
}: {
  account: Account | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const { message } = App.useApp();
  const [form] = Form.useForm();

  const mut = useMutation({
    mutationFn: (v: any) => updateAccount(account!.id, v),
    onSuccess: () => {
      message.success("已保存");
      onDone();
    },
    onError: (e) => message.error(extractError(e, "保存失败")),
  });

  return (
    <Drawer
      title="编辑账号"
      open={!!account}
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
      {account && (
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            status: account.status,
            concurrencyLimit: account.concurrencyLimit,
            chatGptAccountId: account.chatGptAccountId ?? "",
            notes: account.notes ?? "",
          }}
          onFinish={(v) => mut.mutate(v)}
        >
          <Form.Item label="状态" name="status">
            <Select options={statusOptions} />
          </Form.Item>
          <Form.Item label="并发上限" name="concurrencyLimit">
            <InputNumber min={1} max={32} style={{ width: "100%" }} />
          </Form.Item>
          <Form.Item label="chatgpt-account-id" name="chatGptAccountId">
            <Input className="mono" placeholder="可选，转发时写入请求头" />
          </Form.Item>
          <Form.Item label="备注" name="notes">
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      )}
    </Drawer>
  );
}
