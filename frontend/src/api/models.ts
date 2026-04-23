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
