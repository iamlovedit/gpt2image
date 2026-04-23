import { useState } from 'react';
import {
  Card, Table, Space, Button, Input, Select, Tag, Modal, Form,
  InputNumber, Drawer, Popconfirm, App,
} from 'antd';
import { ReloadOutlined, ImportOutlined, EditOutlined, DeleteOutlined, SyncOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import {
  Account, AccountStatusClass, AccountStatusLabel, ImportStrategy, UpstreamAccountStatus,
  deleteAccount, importAccounts, listAccounts, refreshAccount, updateAccount,
} from '../api/accounts';
import { extractError } from '../api/client';

const statusOptions = [
  { value: UpstreamAccountStatus.Healthy, label: AccountStatusLabel[UpstreamAccountStatus.Healthy] },
  { value: UpstreamAccountStatus.Cooling, label: AccountStatusLabel[UpstreamAccountStatus.Cooling] },
  { value: UpstreamAccountStatus.RateLimited, label: AccountStatusLabel[UpstreamAccountStatus.RateLimited] },
  { value: UpstreamAccountStatus.Banned, label: AccountStatusLabel[UpstreamAccountStatus.Banned] },
  { value: UpstreamAccountStatus.Invalid, label: AccountStatusLabel[UpstreamAccountStatus.Invalid] },
  { value: UpstreamAccountStatus.Disabled, label: AccountStatusLabel[UpstreamAccountStatus.Disabled] },
];

export default function AccountsPage() {
  const qc = useQueryClient();
  const { message } = App.useApp();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [status, setStatus] = useState<UpstreamAccountStatus | undefined>();
  const [keyword, setKeyword] = useState('');
  const [importOpen, setImportOpen] = useState(false);
  const [editing, setEditing] = useState<Account | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ['accounts', page, pageSize, status, keyword],
    queryFn: () => listAccounts({ page, pageSize, status, keyword: keyword || undefined }),
  });

  const refreshMut = useMutation({
    mutationFn: refreshAccount,
    onSuccess: () => { message.success('刷新成功'); qc.invalidateQueries({ queryKey: ['accounts'] }); },
    onError: (e) => message.error(extractError(e, '刷新失败')),
  });

  const deleteMut = useMutation({
    mutationFn: deleteAccount,
    onSuccess: () => { message.success('已删除'); qc.invalidateQueries({ queryKey: ['accounts'] }); },
    onError: (e) => message.error(extractError(e, '删除失败')),
  });

  const toggleMut = useMutation({
    mutationFn: ({ id, status }: { id: string; status: UpstreamAccountStatus }) => updateAccount(id, { status }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['accounts'] }),
    onError: (e) => message.error(extractError(e, '更新失败')),
  });

  return (
    <Card
      className="glow-box"
      bordered={false}
      title={<span className="tech-title" style={{ fontSize: 13 }}>UPSTREAM · ACCOUNTS</span>}
      extra={
        <Space>
          <Select
            allowClear
            placeholder="状态筛选"
            options={statusOptions}
            style={{ width: 140 }}
            value={status}
            onChange={(v) => { setStatus(v); setPage(1); }}
          />
          <Input.Search
            placeholder="备注 / 错误关键词"
            allowClear
            onSearch={(v) => { setKeyword(v); setPage(1); }}
            style={{ width: 240 }}
          />
          <Button icon={<ReloadOutlined />} onClick={() => qc.invalidateQueries({ queryKey: ['accounts'] })}>
            刷新
          </Button>
          <Button type="primary" icon={<ImportOutlined />} onClick={() => setImportOpen(true)}>
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
          onChange: (p, s) => { setPage(p); setPageSize(s); },
        }}
        columns={[
          {
            title: '状态',
            dataIndex: 'status',
            width: 100,
            render: (s: UpstreamAccountStatus) => (
              <span className={AccountStatusClass[s]} style={{
                padding: '2px 10px', borderRadius: 10, border: '1px solid',
                fontSize: 12, letterSpacing: '0.05em',
              }}>
                {AccountStatusLabel[s]}
              </span>
            ),
          },
          { title: 'access_token', dataIndex: 'accessTokenPreview', render: (v) => <span className="mono">{v}</span> },
          { title: 'refresh_token', dataIndex: 'refreshTokenPreview', render: (v) => <span className="mono">{v}</span> },
          {
            title: 'chatgpt-account-id',
            dataIndex: 'chatGptAccountId',
            width: 220,
            ellipsis: true,
            render: (v) => v ? <span className="mono">{v}</span> : '—',
          },
          {
            title: '过期时间',
            dataIndex: 'accessTokenExpiresAt',
            width: 160,
            render: (v) => v ? <span className="mono">{dayjs(v).format('MM-DD HH:mm:ss')}</span> : '—',
          },
          { title: '成功', dataIndex: 'successCount', width: 70 },
          { title: '失败', dataIndex: 'failureCount', width: 70 },
          {
            title: '最近使用',
            dataIndex: 'lastUsedAt',
            width: 140,
            render: (v) => v ? <span className="mono">{dayjs(v).format('MM-DD HH:mm')}</span> : '—',
          },
          { title: '并发', dataIndex: 'concurrencyLimit', width: 70 },
          {
            title: '最近错误',
            dataIndex: 'lastError',
            ellipsis: true,
            render: (v) => v || '—',
          },
          {
            title: '操作',
            fixed: 'right',
            width: 200,
            render: (_, row) => (
              <Space size={4}>
                <Button
                  size="small"
                  icon={<SyncOutlined />}
                  loading={refreshMut.isPending && refreshMut.variables === row.id}
                  onClick={() => refreshMut.mutate(row.id)}
                >
                  刷新
                </Button>
                <Button size="small" icon={<EditOutlined />} onClick={() => setEditing(row)}>编辑</Button>
                {row.status === UpstreamAccountStatus.Disabled ? (
                  <Button size="small" onClick={() =>
                    toggleMut.mutate({ id: row.id, status: UpstreamAccountStatus.Healthy })
                  }>启用</Button>
                ) : (
                  <Button size="small" danger onClick={() =>
                    toggleMut.mutate({ id: row.id, status: UpstreamAccountStatus.Disabled })
                  }>禁用</Button>
                )}
                <Popconfirm title="确认删除？" onConfirm={() => deleteMut.mutate(row.id)}>
                  <Button size="small" danger type="text" icon={<DeleteOutlined />} />
                </Popconfirm>
              </Space>
            ),
          },
        ]}
        scroll={{ x: 1540 }}
      />

      <ImportModal
        open={importOpen}
        onClose={() => setImportOpen(false)}
        onDone={() => { setImportOpen(false); qc.invalidateQueries({ queryKey: ['accounts'] }); }}
      />
      <EditDrawer
        account={editing}
        onClose={() => setEditing(null)}
        onDone={() => { setEditing(null); qc.invalidateQueries({ queryKey: ['accounts'] }); }}
      />
    </Card>
  );
}

function ImportModal({
  open, onClose, onDone,
}: { open: boolean; onClose: () => void; onDone: () => void }) {
  const { message } = App.useApp();
  const [text, setText] = useState('');
  const [strategy, setStrategy] = useState<ImportStrategy>(ImportStrategy.Skip);
  const mut = useMutation({
    mutationFn: async () => {
      let parsed: any;
      try { parsed = JSON.parse(text); } catch { throw new Error('JSON 格式错误'); }
      if (!Array.isArray(parsed)) throw new Error('顶层必须是数组');
      const items = parsed.map((it: any) => ({
        accessToken: it.accessToken ?? it.access_token,
        refreshToken: it.refreshToken ?? it.refresh_token,
        chatGptAccountId: it.chatGptAccountId ?? it.chatgptAccountId ?? it.chatgpt_account_id,
      }));
      return importAccounts(items, strategy);
    },
    onSuccess: (r) => {
      message.success(`导入完成：新增 ${r.inserted} · 覆盖 ${r.updated} · 跳过 ${r.skipped}`);
      setText('');
      onDone();
    },
    onError: (e) => message.error(extractError(e, '导入失败')),
  });

  return (
    <Modal
      title="批量导入上游账号"
      open={open}
      onCancel={onClose}
      onOk={() => mut.mutate()}
      confirmLoading={mut.isPending}
      width={640}
      okText="导入"
      cancelText="取消"
    >
      <div style={{ marginBottom: 12, color: '#8A9ABF', fontSize: 12 }}>
        粘贴 JSON 数组，每项必须包含 <code>access_token</code> 与 <code>refresh_token</code>，
        也可选填 <code>chatgpt_account_id</code>。
      </div>
      <div style={{ marginBottom: 12 }}>
        <Space>
          <span>重复策略：</span>
          <Select
            value={strategy}
            onChange={setStrategy}
            style={{ width: 160 }}
            options={[
              { value: ImportStrategy.Skip, label: '跳过重复' },
              { value: ImportStrategy.Overwrite, label: '覆盖重复' },
              { value: ImportStrategy.Fail, label: '遇到重复报错' },
            ]}
          />
        </Space>
      </div>
      <Input.TextArea
        rows={14}
        className="mono"
        placeholder='[{"access_token":"...","refresh_token":"...","chatgpt_account_id":"..."}]'
        value={text}
        onChange={(e) => setText(e.target.value)}
      />
    </Modal>
  );
}

function EditDrawer({
  account, onClose, onDone,
}: { account: Account | null; onClose: () => void; onDone: () => void }) {
  const { message } = App.useApp();
  const [form] = Form.useForm();

  const mut = useMutation({
    mutationFn: (v: any) => updateAccount(account!.id, v),
    onSuccess: () => { message.success('已保存'); onDone(); },
    onError: (e) => message.error(extractError(e, '保存失败')),
  });

  return (
    <Drawer
      title="编辑账号"
      open={!!account}
      onClose={onClose}
      width={360}
      destroyOnClose
      extra={<Button type="primary" onClick={() => form.submit()} loading={mut.isPending}>保存</Button>}
    >
      {account && (
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            status: account.status,
            concurrencyLimit: account.concurrencyLimit,
            chatGptAccountId: account.chatGptAccountId ?? '',
            notes: account.notes ?? '',
          }}
          onFinish={(v) => mut.mutate(v)}
        >
          <Form.Item label="状态" name="status">
            <Select options={statusOptions} />
          </Form.Item>
          <Form.Item label="并发上限" name="concurrencyLimit">
            <InputNumber min={1} max={32} style={{ width: '100%' }} />
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
