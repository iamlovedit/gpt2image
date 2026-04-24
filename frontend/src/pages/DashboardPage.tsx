import { Card, Col, Row, Table, Tag, Space } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { getSummary } from '../api/dashboard';
import { BusinessStatusLabel, RequestBusinessStatus, type RequestLog } from '../api/logs';
import dayjs from 'dayjs';

function StatCard({ label, value, hint }: { label: string; value: React.ReactNode; hint?: string }) {
  return (
    <Card className="glow-box" bordered={false} bodyStyle={{ padding: 20 }}>
      <div style={{ color: '#8A9ABF', fontSize: 12, letterSpacing: '0.15em', textTransform: 'uppercase' }}>
        {label}
      </div>
      <div className="num-kpi" style={{ marginTop: 10 }}>{value}</div>
      {hint && <div style={{ color: '#5A6C91', fontSize: 12, marginTop: 6 }}>{hint}</div>}
    </Card>
  );
}

function formatToken(value: number | null | undefined) {
  return value == null ? '—' : value.toLocaleString();
}

export default function DashboardPage() {
  const { data, isLoading } = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: getSummary,
    refetchInterval: 10_000,
  });

  const rate = data?.successRate == null ? '—' : `${(data.successRate * 100).toFixed(1)}%`;
  const avg = data?.avgDurationMs ? `${Math.round(data.avgDurationMs)}ms` : '—';
  const totalTokens = formatToken(data?.totalTokens24h);

  return (
    <Space direction="vertical" size={20} style={{ width: '100%' }}>
      <Row gutter={[20, 20]}>
        <Col xs={24} sm={12} md={6}>
          <StatCard label="24h 请求数" value={data?.total24h ?? '—'} hint={`平均耗时 ${avg}`} />
        </Col>
        <Col xs={24} sm={12} md={6}>
          <StatCard label="24h 成功率" value={rate} hint={`成功 ${data?.success24h ?? 0}`} />
        </Col>
        <Col xs={24} sm={12} md={6}>
          <StatCard
            label="账号健康度"
            value={`${data?.accountHealthy ?? 0}/${data?.accountTotal ?? 0}`}
            hint="healthy / total"
          />
        </Col>
        <Col xs={24} sm={12} md={6}>
          <StatCard label="24h Token" value={totalTokens} hint={`图片 ${formatToken(data?.imageTotalTokens24h)}`} />
        </Col>
      </Row>

      <Card
        className="glow-box"
        bordered={false}
        title={<span className="tech-title" style={{ fontSize: 13 }}>RECENT · REQUESTS</span>}
        loading={isLoading}
      >
        <Table<RequestLog>
          rowKey="id"
          size="small"
          pagination={false}
          dataSource={data?.recent ?? []}
          columns={[
            {
              title: '时间',
              dataIndex: 'startedAt',
              width: 160,
              render: (v: string) => <span className="mono">{dayjs(v).format('MM-DD HH:mm:ss')}</span>,
            },
            { title: '模型', dataIndex: 'externalModel', width: 120 },
            {
              title: '状态',
              dataIndex: 'businessStatus',
              width: 110,
              render: (s: RequestBusinessStatus) => (
                <Tag color={s === RequestBusinessStatus.Success ? 'cyan' : 'red'}>
                  {BusinessStatusLabel[s]}
                </Tag>
              ),
            },
            {
              title: '耗时',
              dataIndex: 'durationMs',
              width: 90,
              render: (v: number | null) => (v == null ? '—' : <span className="mono">{v}ms</span>),
            },
            {
              title: 'Token',
              dataIndex: 'totalTokens',
              width: 100,
              render: (v: number | null) => <span className="mono">{formatToken(v)}</span>,
            },
            { title: 'HTTP', dataIndex: 'httpStatus', width: 80, render: (v) => v ?? '—' },
            { title: '重试', dataIndex: 'retryCount', width: 60 },
            { title: '事件数', dataIndex: 'sseEventCount', width: 80 },
            {
              title: '错误',
              dataIndex: 'errorMessage',
              ellipsis: true,
              render: (v) => v || '—',
            },
          ]}
        />
      </Card>
    </Space>
  );
}
