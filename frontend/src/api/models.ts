import { apiDelete, apiGet, apiPatch, apiPost } from './client';

export interface ModelMapping {
  id: string;
  externalName: string;
  upstreamName: string;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export async function listModels(): Promise<ModelMapping[]> {
  return apiGet<ModelMapping[]>('/models');
}

export async function createModel(payload: {
  externalName: string;
  upstreamName: string;
  isEnabled?: boolean;
}): Promise<ModelMapping> {
  return apiPost<ModelMapping>('/models', payload);
}

export async function updateModel(id: string, patch: Partial<{
  externalName: string;
  upstreamName: string;
  isEnabled: boolean;
}>): Promise<ModelMapping> {
  return apiPatch<ModelMapping>(`/models/${id}`, patch);
}

export async function deleteModel(id: string): Promise<void> {
  await apiDelete(`/models/${id}`);
}
