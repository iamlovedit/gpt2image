import { http } from './client';

export interface ModelMapping {
  id: string;
  externalName: string;
  upstreamName: string;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export async function listModels(): Promise<ModelMapping[]> {
  const { data } = await http.get('/models');
  return data;
}

export async function createModel(payload: {
  externalName: string;
  upstreamName: string;
  isEnabled?: boolean;
}): Promise<ModelMapping> {
  const { data } = await http.post('/models', payload);
  return data;
}

export async function updateModel(id: string, patch: Partial<{
  externalName: string;
  upstreamName: string;
  isEnabled: boolean;
}>): Promise<ModelMapping> {
  const { data } = await http.patch(`/models/${id}`, patch);
  return data;
}

export async function deleteModel(id: string): Promise<void> {
  await http.delete(`/models/${id}`);
}
