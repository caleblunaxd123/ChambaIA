import { Tabs } from 'expo-router';
import { Platform } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useOverview } from '@/api/queries';
import { Icon, type IconName } from '@/ui/Icon';
import { fontFamily, useTheme } from '@/ui/theme';

type TabDef = { name: string; title: string; icon: IconName; focused: IconName };

const TABS: TabDef[] = [
  { name: 'index', title: 'Inicio', icon: 'home-outline', focused: 'home' },
  { name: 'jobs', title: 'Empleos', icon: 'briefcase-outline', focused: 'briefcase' },
  { name: 'applications', title: 'Postulaciones', icon: 'paper-plane-outline', focused: 'paper-plane' },
  { name: 'agent', title: 'Agente', icon: 'sparkles-outline', focused: 'sparkles' },
  { name: 'profile', title: 'Perfil', icon: 'person-outline', focused: 'person' },
];

export default function TabsLayout() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const overview = useOverview();
  const newCount = overview.data?.newTotal ?? 0;
  // Explicit height: the default one clips the labels on web and on Android with gesture navigation.
  const bottom = Math.max(insets.bottom, Platform.OS === 'web' ? 8 : 6);

  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.textSubtle,
        tabBarLabelStyle: { fontFamily: fontFamily.semibold, fontSize: 10, letterSpacing: -0.1, marginTop: 2 },
        tabBarStyle: { backgroundColor: colors.surface, borderTopColor: colors.border, borderTopWidth: 1, height: 58 + bottom, paddingTop: 6, paddingBottom: bottom },
        tabBarBadgeStyle: { backgroundColor: colors.accent, color: colors.onHero, fontFamily: fontFamily.bold, fontSize: 10 },
        sceneStyle: { backgroundColor: colors.bg },
      }}
    >
      {TABS.map((t) => (
        <Tabs.Screen
          key={t.name}
          name={t.name}
          options={{
            title: t.title,
            tabBarButtonTestID: `tab-${t.name}`,
            tabBarBadge: t.name === 'jobs' && newCount > 0 ? (newCount > 99 ? '99+' : newCount) : undefined,
            tabBarIcon: ({ focused, color }) => <Icon name={focused ? t.focused : t.icon} size={23} color={color} />,
          }}
        />
      ))}
    </Tabs>
  );
}
