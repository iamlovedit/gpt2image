import axios from 'axios';
import { useAuthStore } from '@/stores/authStore';

export interface ApiSuccess<T = unknown> {
  success: true;
  data?: T;
}

export interface ApiFailure {
  success: false;
  error: string;
}

export type ApiResponse<T = unknown> = ApiSuccess<T> | ApiFailure;

export const http = axios.create({
  baseURL: (import.meta.env.VITE_API_BASE as string | undefined) || '/api',
  timeout: 30_000,
});

export async function apiGet<T>(url: string, config?: Parameters<typeof http.get>[1]): Promise<T> {
  return (await http.get<T>(url, config)).data;
}

export async function apiPost<T>(url: string, data?: unknown, config?: Parameters<typeof http.post>[2]): Promise<T> {
  return (await http.post<T>(url, data, config)).data;
}

export async function apiPatch<T>(url: string, data?: unknown, config?: Parameters<typeof http.patch>[2]): Promise<T> {
  return (await http.patch<T>(url, data, config)).data;
}

export async function apiPut<T>(url: string, data?: unknown, config?: Parameters<typeof http.put>[2]): Promise<T> {
  return (await http.put<T>(url, data, config)).data;
}

export async function apiDelete(url: string, config?: Parameters<typeof http.delete>[1]): Promise<void> {
  await http.delete(url, config);
}

http.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token;
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`);
  }
  return config;
});

http.interceptors.response.use(
  (resp) => {
    const body = resp.data as ApiResponse | unknown;
    if (isApiResponse(body)) {
      if (body.success) {
        resp.data = body.data;
        return resp;
      }

      return Promise.reject(new Error(body.error));
    }

    return resp;
  },
  (error) => {
    if (error.response?.status === 401) {
      useAuthStore.getState().clear();
      if (!location.pathname.startsWith('/login')) {
        location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);

function isApiResponse(value: unknown): value is ApiResponse {
  return typeof value === 'object' && value !== null && 'success' in value;
}

export function extractError(err: unknown, fallback = '请求失败'): string {
  if (axios.isAxiosError(err)) {
    const data = err.response?.data as any;
    if (typeof data?.error === 'string') return data.error;
    if (typeof data?.error?.message === 'string') return data.error.message;
    if (typeof data?.message === 'string') return data.message;
    return err.message || fallback;
  }
  return (err as Error)?.message ?? fallback;
}
