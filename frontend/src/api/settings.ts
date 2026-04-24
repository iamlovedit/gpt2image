import { apiGet, apiPut } from './client';

export interface UpstreamHeaderSettings {
  userAgent: string;
  version: string;
  originator: string;
  sessionId: string | null;
}

export async function getUpstreamHeaderSettings(): Promise<UpstreamHeaderSettings> {
  return apiGet<UpstreamHeaderSettings>('/settings/upstream-headers');
}

export async function updateUpstreamHeaderSettings(
  payload: UpstreamHeaderSettings
): Promise<UpstreamHeaderSettings> {
  return apiPut<UpstreamHeaderSettings>('/settings/upstream-headers', payload);
}
