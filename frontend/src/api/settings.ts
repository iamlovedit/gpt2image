import { http } from './client';

export interface UpstreamHeaderSettings {
  userAgent: string;
  version: string;
  originator: string;
  sessionId: string | null;
}

export async function getUpstreamHeaderSettings(): Promise<UpstreamHeaderSettings> {
  const { data } = await http.get('/settings/upstream-headers');
  return data;
}

export async function updateUpstreamHeaderSettings(
  payload: UpstreamHeaderSettings
): Promise<UpstreamHeaderSettings> {
  const { data } = await http.put('/settings/upstream-headers', payload);
  return data;
}
