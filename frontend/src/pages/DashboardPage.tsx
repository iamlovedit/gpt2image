import { Card, Col, Empty, Row, Segmented, Space, Table } from "antd";
import { useQuery } from "@tanstack/react-query";
import {
  getRequestStats,
  getSummary,
  type DashboardStatsRange,
  type KeyBreakdownItem,
} from "@/api/dashboard";
import { BusinessStatusLabel } from "@/api/logs";
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { useMemo, useState } from "react";

const chartColors = {
  cyan: "#00D4FF",
  green: "#22D3A1",
  red: "#FF4F6D",
  yellow: "#FFB13C",
  purple: "#7B61FF",
  blue: "#5B8CFF",
};

const pieColors = [
  chartColors.green,
  chartColors.red,
  chartColors.yellow,
  chartColors.purple,
  chartColors.cyan,
  chartColors.blue,
  "#8A9ABF",
];

function StatCard({
  label,
  value,
  hint,
}: {
  label: string;
  value: React.ReactNode;
  hint?: string;
}) {
  return (
    <Card
      className="glow-box"
      variant={"borderless"}
      styles={{ body: { padding: 20 } }}
    >
      <div
        style={{
          color: "#8A9ABF",
          fontSize: 12,
          letterSpacing: "0.15em",
          textTransform: "uppercase",
        }}
      >
        {label}
      </div>
      <div className="num-kpi" style={{ marginTop: 10 }}>
        {value}
      </div>
      {hint && (
        <div style={{ color: "#5A6C91", fontSize: 12, marginTop: 6 }}>
          {hint}
        </div>
      )}
    </Card>
  );
}

function ChartCard({
  title,
  loading,
  children,
}: {
  title: string;
  loading?: boolean;
  children: React.ReactNode;
}) {
  return (
    <Card
      className="glow-box"
      variant="borderless"
      title={
        <span className="tech-title" style={{ fontSize: 13 }}>
          {title}
        </span>
      }
      loading={loading}
      styles={{ body: { height: 320 } }}
    >
      {children}
    </Card>
  );
}

function EmptyChart() {
  return (
    <div style={{ height: "100%", display: "grid", placeItems: "center" }}>
      <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="暂无统计数据" />
    </div>
  );
}

function formatToken(value: number | null | undefined) {
  return value == null ? "—" : value.toLocaleString();
}

function formatPercent(value: number | null | undefined) {
  return value == null ? "—" : `${(value * 100).toFixed(1)}%`;
}

function formatMetric(value: number | string | readonly (number | string)[] | undefined) {
  if (typeof value === "number") return value.toLocaleString();
  if (Array.isArray(value)) return value.join(" ~ ");
  return value ?? "—";
}

function chartThemeProps() {
  return {
    tick: { fill: "#8A9ABF", fontSize: 12 },
    axisLine: { stroke: "rgba(138, 154, 191, 0.24)" },
    tickLine: { stroke: "rgba(138, 154, 191, 0.24)" },
  };
}

function tooltipProps() {
  return {
    contentStyle: {
      background: "rgba(10, 15, 29, 0.96)",
      border: "1px solid rgba(0, 212, 255, 0.22)",
      borderRadius: 8,
      color: "#E6ECF5",
    },
    labelStyle: { color: "#C5D2E8" },
    formatter: formatMetric,
  };
}

export default function DashboardPage() {
  const [range, setRange] = useState<DashboardStatsRange>("today");
  const { data, isLoading } = useQuery({
    queryKey: ["dashboard-summary"],
    queryFn: getSummary,
    refetchInterval: 10_000,
  });
  const { data: stats, isLoading: statsLoading } = useQuery({
    queryKey: ["dashboard-request-stats", range],
    queryFn: () => getRequestStats(range),
    refetchInterval: 10_000,
  });

  const chartData = useMemo(
    () =>
      stats?.series.map((item) => ({
        ...item,
        successRatePercent:
          item.successRate == null ? null : Number((item.successRate * 100).toFixed(2)),
        avgDurationRounded: Math.round(item.avgDurationMs),
      })) ?? [],
    [stats?.series],
  );
  const statusData = useMemo(
    () =>
      stats?.statusBreakdown.map((item) => ({
        name: BusinessStatusLabel[item.status],
        value: item.count,
      })) ?? [],
    [stats?.statusBreakdown],
  );

  const rate = formatPercent(data?.successRate);
  const avg = data?.avgDurationMs ? `${Math.round(data.avgDurationMs)}ms` : "—";
  const totalTokens = formatToken(data?.totalTokens24h);
  const hasSeriesData = chartData.some((item) => item.requestCount > 0);
  const hasStatusData = statusData.length > 0;
  const hasErrorData = (stats?.errorTypeBreakdown.length ?? 0) > 0;
  const hasKeyData = (stats?.keyBreakdown.length ?? 0) > 0;

  return (
    <Space direction="vertical" size={20} style={{ width: "100%" }}>
      <Row gutter={[20, 20]}>
        <Col xs={24} sm={12} md={6}>
          <StatCard
            label="24h 请求数"
            value={data?.total24h ?? "—"}
            hint={`平均耗时 ${avg}`}
          />
        </Col>
        <Col xs={24} sm={12} md={6}>
          <StatCard
            label="24h 成功率"
            value={rate}
            hint={`成功 ${data?.success24h ?? 0}`}
          />
        </Col>
        <Col xs={24} sm={12} md={6}>
          <StatCard
            label="账号健康度"
            value={`${data?.accountHealthy ?? 0}/${data?.accountTotal ?? 0}`}
            hint="healthy / total"
          />
        </Col>
        <Col xs={24} sm={12} md={6}>
          <StatCard
            label="24h Token"
            value={totalTokens}
            hint={`图片 ${formatToken(data?.imageTotalTokens24h)}`}
          />
        </Col>
      </Row>

      <Card
        className="glow-box"
        variant="borderless"
        title={
          <Space align="center" wrap>
            <span className="tech-title" style={{ fontSize: 13 }}>
              REQUEST · ANALYTICS
            </span>
            <Segmented<DashboardStatsRange>
              size="small"
              value={range}
              onChange={setRange}
              options={[
                { label: "今天", value: "today" },
                { label: "7day", value: "7d" },
                { label: "30day", value: "30d" },
              ]}
            />
          </Space>
        }
      >
        <Row gutter={[20, 20]}>
          <Col xs={24} xl={12}>
            <ChartCard title="REQUESTS · TREND" loading={statsLoading}>
              {!hasSeriesData ? (
                <EmptyChart />
              ) : (
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart data={chartData}>
                    <CartesianGrid stroke="rgba(138,154,191,0.12)" vertical={false} />
                    <XAxis dataKey="label" {...chartThemeProps()} />
                    <YAxis {...chartThemeProps()} />
                    <Tooltip {...tooltipProps()} />
                    <Legend />
                    <Area
                      type="monotone"
                      dataKey="requestCount"
                      name="请求数"
                      stroke={chartColors.cyan}
                      fill="rgba(0, 212, 255, 0.16)"
                    />
                    <Area
                      type="monotone"
                      dataKey="successCount"
                      name="成功"
                      stroke={chartColors.green}
                      fill="rgba(34, 211, 161, 0.12)"
                    />
                    <Area
                      type="monotone"
                      dataKey="failureCount"
                      name="失败"
                      stroke={chartColors.red}
                      fill="rgba(255, 79, 109, 0.10)"
                    />
                  </AreaChart>
                </ResponsiveContainer>
              )}
            </ChartCard>
          </Col>
          <Col xs={24} xl={12}>
            <ChartCard title="SUCCESS RATE · LATENCY" loading={statsLoading}>
              {!hasSeriesData ? (
                <EmptyChart />
              ) : (
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={chartData}>
                    <CartesianGrid stroke="rgba(138,154,191,0.12)" vertical={false} />
                    <XAxis dataKey="label" {...chartThemeProps()} />
                    <YAxis yAxisId="left" unit="%" {...chartThemeProps()} />
                    <YAxis yAxisId="right" orientation="right" unit="ms" {...chartThemeProps()} />
                    <Tooltip {...tooltipProps()} />
                    <Legend />
                    <Line
                      yAxisId="left"
                      type="monotone"
                      dataKey="successRatePercent"
                      name="成功率"
                      stroke={chartColors.green}
                      strokeWidth={2}
                      dot={false}
                      connectNulls
                    />
                    <Line
                      yAxisId="right"
                      type="monotone"
                      dataKey="avgDurationRounded"
                      name="平均耗时"
                      stroke={chartColors.yellow}
                      strokeWidth={2}
                      dot={false}
                    />
                  </LineChart>
                </ResponsiveContainer>
              )}
            </ChartCard>
          </Col>
          <Col xs={24} xl={12}>
            <ChartCard title="TOKENS · USAGE" loading={statsLoading}>
              {!hasSeriesData ? (
                <EmptyChart />
              ) : (
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={chartData}>
                    <CartesianGrid stroke="rgba(138,154,191,0.12)" vertical={false} />
                    <XAxis dataKey="label" {...chartThemeProps()} />
                    <YAxis {...chartThemeProps()} />
                    <Tooltip {...tooltipProps()} />
                    <Legend />
                    <Bar dataKey="inputTokens" name="输入 Token" fill={chartColors.blue} stackId="tokens" />
                    <Bar dataKey="outputTokens" name="输出 Token" fill={chartColors.cyan} stackId="tokens" />
                    <Bar dataKey="imageTotalTokens" name="图片 Token" fill={chartColors.purple} />
                  </BarChart>
                </ResponsiveContainer>
              )}
            </ChartCard>
          </Col>
          <Col xs={24} xl={12}>
            <ChartCard title="STATUS · BREAKDOWN" loading={statsLoading}>
              {!hasStatusData ? (
                <EmptyChart />
              ) : (
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Tooltip {...tooltipProps()} />
                    <Legend />
                    <Pie
                      data={statusData}
                      dataKey="value"
                      nameKey="name"
                      innerRadius={58}
                      outerRadius={104}
                      paddingAngle={3}
                    >
                      {statusData.map((_, index) => (
                        <Cell key={index} fill={pieColors[index % pieColors.length]} />
                      ))}
                    </Pie>
                  </PieChart>
                </ResponsiveContainer>
              )}
            </ChartCard>
          </Col>
          <Col xs={24} xl={12}>
            <ChartCard title="ERRORS · TOP" loading={statsLoading}>
              {!hasErrorData ? (
                <EmptyChart />
              ) : (
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={stats?.errorTypeBreakdown ?? []} layout="vertical" margin={{ left: 32 }}>
                    <CartesianGrid stroke="rgba(138,154,191,0.12)" horizontal={false} />
                    <XAxis type="number" {...chartThemeProps()} />
                    <YAxis type="category" dataKey="errorType" width={130} {...chartThemeProps()} />
                    <Tooltip {...tooltipProps()} />
                    <Bar dataKey="count" name="错误数" fill={chartColors.red} />
                  </BarChart>
                </ResponsiveContainer>
              )}
            </ChartCard>
          </Col>
          <Col xs={24} xl={12}>
            <ChartCard title="API KEYS · TOP" loading={statsLoading}>
              {!hasKeyData ? (
                <EmptyChart />
              ) : (
                <Table<KeyBreakdownItem>
                  rowKey={(row) => row.clientKeyId ?? row.name}
                  size="small"
                  pagination={false}
                  dataSource={stats?.keyBreakdown ?? []}
                  columns={[
                    { title: "API Key", dataIndex: "name", ellipsis: true },
                    { title: "请求", dataIndex: "requestCount", width: 80 },
                    { title: "成功", dataIndex: "successCount", width: 80 },
                    { title: "失败", dataIndex: "failureCount", width: 80 },
                  ]}
                />
              )}
            </ChartCard>
          </Col>
        </Row>
      </Card>
    </Space>
  );
}
