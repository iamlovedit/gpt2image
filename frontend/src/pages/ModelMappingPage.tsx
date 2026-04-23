import { Alert, Card, Table, Tag } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { listModels, type ModelMapping } from '../api/models';

export default function ModelMappingPage() {
  const { data = [], isLoading } = useQuery({ queryKey: ['models'], queryFn: listModels });

  return (
    <Card
      className="glow-box"
      bordered={false}
      title={<span className="tech-title" style={{ fontSize: 13 }}>MODEL · MAPPING</span>}
    >
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16, background: 'rgba(0, 212, 255, 0.06)' }}
        message="本页当前只读：MVP 阶段使用默认映射。未来可通过此页面增删改。"
      />
      <Table<ModelMapping>
        rowKey="id"
        size="small"
        loading={isLoading}
        dataSource={data}
        pagination={false}
        columns={[
          { title: '对外模型', dataIndex: 'externalName', render: (v) => <span className="mono">{v}</span> },
          { title: '上游模型', dataIndex: 'upstreamName', render: (v) => <span className="mono">{v}</span> },
          {
            title: '状态',
            dataIndex: 'isEnabled',
            width: 100,
            render: (v: boolean) => v ? <Tag color="cyan">启用</Tag> : <Tag>禁用</Tag>,
          },
        ]}
      />
    </Card>
  );
}
