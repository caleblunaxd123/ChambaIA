import Constants from 'expo-constants';
import { Platform } from 'react-native';

const API_PORT = 5180;

/**
 * Where the API lives. Order: EXPO_PUBLIC_API_URL → same host Metro is served from (physical device on the
 * same Wi-Fi) → emulator loopback aliases → localhost.
 */
export function resolveApiUrl(env: string | undefined = process.env.EXPO_PUBLIC_API_URL): string {
  if (env && env.trim().length > 0) return env.trim().replace(/\/+$/, '');

  if (Platform.OS === 'web') {
    const host = typeof window !== 'undefined' && window.location?.hostname ? window.location.hostname : 'localhost';
    return `http://${host}:${API_PORT}`;
  }

  const metroHost = Constants.expoConfig?.hostUri?.split(':')[0];
  if (metroHost) return `http://${metroHost}:${API_PORT}`;

  return Platform.OS === 'android' ? `http://10.0.2.2:${API_PORT}` : `http://localhost:${API_PORT}`;
}

export const API_URL = resolveApiUrl();
export const API_BASE = `${API_URL}/api/v1`;
