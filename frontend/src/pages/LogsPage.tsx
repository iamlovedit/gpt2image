import { useState } from 'react';
import { Card, Table, Space, Select, DatePicker, Modal, Tag, Descriptions, Input } from 'antd';
import { useQuery } from '@tanstack/react-query';
import dayjs from 'dayjs';
import {
  BusinessStatusLabel, RequestBusinessStatus, RequestLog, listLogs,
} from '../api/logs';

const statusOptions = Object.entries(BusinessStatusLabel).map(([v, label]) => ({
  value: Number(v) as RequestBusinessStatus,
  label,
}));

export default function LogsPage() {
  const [status, setStatus] = useState<RequestBusinessStatus | undefined>();
  const [clientKeyId, setClientKeyId] = useState('');
  const [accountId, setAccountId] = useState('');
  const [range, setRange] = useState<[dayjs.Dayjs, dayjs.Dayjs] | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [detail, setDetail] = useState<RequestLog | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ['logs', status, clientKeyId, accountId, range, page, pageSize],
    queryFn: () => listLogs({
      status,
      clientKeyId: clientKeyId || undefined,
      accountId: accountId || undefined,
      from: range?.[0]?.toISOString(),
      to: range?.[1]?.toISOString(),
      page, pageSize,
    }),
    refetchInterval: 15_000,
  });

  return (
    <Card
      className="glow-box"
      bordered={false}
      title={<span className="tech-title" style={{ fontSize: 13 }}>REQUEST · LOGS</span>}
      extra={
        <Space wrap>
          <Select
            allowClear
            placeholder="状态"
            options={statusOptions}
            style={{ width: 160 }}
            value={status}
            onChange={(v) => { setStatus(v); setPage(1); }}
          />
          <Input
            allowClear
            placeholder="Client Key ID"
            style={{ width: 260 }}
            value={clientKeyId}
            onChange={(e) => { setClientKeyId(e.target.value); setPage(1); }}
          />
          <Input
            allowClear
            placeholder="Account ID"
            style={{ width: 260 }}
            value={accountId}
            onChange={(e) => { setAccountId(e.target.value); setPage(1); }}
          />
          <DatePicker.RangePicker
            showTime
            value={range as any}
            onChange={(r) => { setRange(r as any); setPage(1); }}
          />
        </Space>
      }
    >
      <Table<RequestLog>
        rowKey="id"
        size="small"
        loading={isLoading}
        dataSource={data?.items ?? []}
        pagination={{
          current: page, pageSize, total: data?.total ?? 0,
          onChange: (p, s) => { setPage(p); setPageSize(s); },
        }}
        onRow={(row) => ({ onClick: () => setDetail(row) })}
        columns={[
          {
            title: '时间', dataIndex: 'startedAt', width: 170,
            render: (v: string) => <span className="mono">{dayjs(v).format('YYYY-MM-DD HH:mm:ss')}</span>,
          },
          { title: '模型', dataIndex: 'externalModel', width: 120 },
          { title: '上游模型', dataIndex: 'upstreamModel', width: 120 },
          {
            title: '状态', dataIndex: 'businessStatus', width: 110,
            render: (s: RequestBusinessStatus) => (
              <Tag color={s === RequestBusinessStatus.Success ? 'cyan' : 'red'}>
                {BusinessStatusLabel[s]}
              </Tag>
            ),
          },
          { title: 'HTTP', dataIndex: 'httpStatus', width: 80, render: (v) => v ?? '—' },
          { title: '耗时', dataIndex: 'durationMs', width: 90, render: (v) => v == null ? '—' : <span className="mono">{v}ms</span> },
          { title: '重试', dataIndex: 'retryCount', width: 60 },
          { title: '事件数', dataIndex: 'sseEventCount', width: 80 },
          { title: '错误', dataIndex: 'errorMessage', ellipsis: true, render: (v) => v || '—' },
        ]}
      />

      <Modal
        title="请求详情"
        open={!!detail}
        onCancel={() => setDetail(null)}
        footer={null}
        width={720}
      >
        {detail && (
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label="RequestId"><span className="mono">{detail.requestId}</span></Descriptions.Item>
            <Descriptions.Item label="Client Key Id"><span className="mono">{detail.clientKeyId ?? '—'}</span></Descriptions.Item>
            <Descriptions.Item label="Account Id"><span className="mono">{detail.upstreamAccountId ?? '—'}</span></Descriptions.Item>
            <Descriptions.Item label="模型">{detail.externalModel} → {detail.upstreamModel}</Descriptions.Item>
            <Descriptions.Item label="开始时间"><span className="mono">{dayjs(detail.startedAt).format('YYYY-MM-DD HH:mm:ss.SSS')}</span></Descriptions.Item>
            <Descriptions.Item label="结束时间"><span className="mono">{detail.completedAt ? dayjs(detail.completedAt).format('YYYY-MM-DD HH:mm:ss.SSS') : '—'}</span></Descriptions.Item>
            <Descriptions.Item label="耗时">{detail.durationMs == null ? '—' : `${detail.durationMs}ms`}</Descriptions.Item>
            <Descriptions.Item label="HTTP 状态">{detail.httpStatus ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="业务状态">{BusinessStatusLabel[detail.businessStatus]}</Descriptions.Item>
            <Descriptions.Item label="重试次数">{detail.retryCount}</Descriptions.Item>
            <Descriptions.Item label="SSE 事件数">{detail.sseEventCount}</Descriptions.Item>
            <Descriptions.Item label="错误类型">{detail.errorType ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="错误信息"><span style={{ whiteSpace: 'pre-wrap' }}>{detail.errorMessage ?? '—'}</span></Descriptions.Item>
          </Descriptions>
        )}
      </Modal>
    </Card>
  );
}
