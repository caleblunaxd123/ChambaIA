import {
  PlusJakartaSans_400Regular,
  PlusJakartaSans_500Medium,
  PlusJakartaSans_600SemiBold,
  PlusJakartaSans_700Bold,
  PlusJakartaSans_800ExtraBold,
  useFonts,
} from '@expo-google-fonts/plus-jakarta-sans';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { ApiError } from '@/api/client';
import { useProfile } from '@/api/queries';
import { useAuthStore } from '@/state/auth-store';
import { useTheme } from '@/ui/theme';

void SplashScreen.preventAutoHideAsync();

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        // Client errors (4xx) will not fix themselves: only retry network/server failures, once.
        retry: (failureCount, error) => failureCount < 1 && !(error instanceof ApiError && error.status >= 400 && error.status < 500),
      },
    },
  });
}

export default function RootLayout() {
  const [queryClient] = useState(createQueryClient);
  const status = useAuthStore((s) => s.status);
  const [fontsLoaded] = useFonts({
    PlusJakartaSans_400Regular,
    PlusJakartaSans_500Medium,
    PlusJakartaSans_600SemiBold,
    PlusJakartaSans_700Bold,
    PlusJakartaSans_800ExtraBold,
  });

  useEffect(() => {
    void useAuthStore.getState().hydrate();
  }, []);

  // Never show one user's cached data to the next person who signs in on this device.
  useEffect(() => {
    if (status === 'signedOut') queryClient.clear();
  }, [status, queryClient]);

  const ready = fontsLoaded && status !== 'loading';
  useEffect(() => {
    if (ready) void SplashScreen.hideAsync();
  }, [ready]);

  if (!ready) return null;

  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <StatusBar style="dark" />
        <RootStack />
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}

/**
 * Route guards. A signed-in user whose onboarding is unfinished can only reach the onboarding flow; once it is
 * completed the tabs open by themselves (the profile query flips and the guards re-evaluate).
 */
function RootStack() {
  const { colors } = useTheme();
  const status = useAuthStore((s) => s.status);
  const signedIn = status === 'signedIn';
  const profile = useProfile(signedIn);
  // If the profile cannot be loaded (offline) do not trap the user in onboarding: the screens show their own errors.
  const needsOnboarding = signedIn && profile.data?.onboardingCompleted === false;

  if (signedIn && profile.isLoading) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg }}>
        <ActivityIndicator color={colors.primary} />
      </View>
    );
  }

  return (
    <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.bg } }}>
      <Stack.Protected guard={signedIn && !needsOnboarding}>
        <Stack.Screen name="(tabs)" />
        <Stack.Screen name="job/[id]" />
        <Stack.Screen name="edit-profile" options={{ presentation: 'modal' }} />
        <Stack.Screen name="edit-preferences" options={{ presentation: 'modal' }} />
      </Stack.Protected>
      <Stack.Protected guard={signedIn}>
        <Stack.Screen name="onboarding" options={{ gestureEnabled: false }} />
      </Stack.Protected>
      <Stack.Protected guard={status === 'signedOut'}>
        <Stack.Screen name="login" />
        <Stack.Screen name="register" />
      </Stack.Protected>
    </Stack>
  );
}
